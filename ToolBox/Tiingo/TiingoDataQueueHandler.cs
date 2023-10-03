/*
 * TraderScience extension for QuantConnect Lean Engine.
 * TiingoDataQueueHandler.cs
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Logging;
using QuantConnect.Configuration;
using QuantConnect.Util;
using IQFeed.CSharpApiClient.Common.Interfaces;
using System.Threading;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using QuantConnect.ToolBox.IQFeed;
using QuantConnect.ToolBox.CoinApi.Messages;
using System.Threading.Tasks;
using RestSharp;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Net.WebSockets;
using QuantConnect.Lean.Engine.RealTime;
using ProtoBuf.WellKnownTypes;

namespace QuantConnect.ToolBox.Tiingo
{
    /// <summary>
    /// Tiingo DataQueueHandler is a default handler when no other feed is available
    /// </summary>
    public class TiingoDataQueueHandler : IDataQueueHandler
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan SubscribeDelay = TimeSpan.FromMilliseconds(1500);
        private static bool _invalidHistDataTypeWarningFired;

        //private readonly TiingoEventSourceCollection _clients;
        private TiingoWebSocket _iexConnection;
        private TiingoWebSocket _forexConnection;
        private TiingoWebSocket _cryptoConnection;

        private readonly ManualResetEvent _refreshEvent = new ManualResetEvent(false);

        private readonly ConcurrentDictionary<string, int> _subscribedSymbols = new ConcurrentDictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _LastTradeTime = new ConcurrentDictionary<string, long>();

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _dataPointCount;

        // Tiingo Api Key
        private string _apiKey = null;

        private readonly IDataAggregator _aggregator = Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(
            Config.Get("data-aggregator", "QuantConnect.Lean.Engine.DataFeeds.AggregationManager"));

        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;
        private int _dataQueueCount;
        private Task _runTimeTask;
        private TiingoWebSocketMessageHandlers _messageHandlers;

        public TiingoDataQueueHandler()
        {
            _subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();
            var apiKey = Config.Get("tiingo-api-key", null);
            var threshold = Config.GetInt("tiingo-api-threshold", 1);
            if (String.IsNullOrEmpty(apiKey))
                throw new ArgumentException($"TiingoDataQueueHandler: required ApiKey is missing");         
        }

        private void Initialize(string ApiKey, int threshold=0)
        {
            try
            {
                _messageHandlers = new TiingoWebSocketMessageHandlers(
                    (data) =>  {Emit(data); },
                    _subscribedSymbols
                    );

                _iexConnection = new TiingoWebSocket(ApiKey, TiingoSocketType.Equities, threshold, (message) =>
                {
                    _messageHandlers.ProcessIexMessage(message);
                });

                _forexConnection = new TiingoWebSocket(ApiKey, TiingoSocketType.Forex, threshold, (message) =>
                {
                    _messageHandlers.ProcessForexMessage(message);
                });

                _cryptoConnection = new TiingoWebSocket(ApiKey, TiingoSocketType.Crypto, threshold, (message) =>
                {
                    _messageHandlers.ProcessCryptoMessage(message);
                });
            }
            catch (Exception ex)
            {
                Log.Error($"TiingoDataQueueHandler:Initialize: Exception {ex.ToString()}");
                throw new IOException($"TiingoDataQueueHandler:Initialize Exception");
            }

            _subscriptionManager.SubscribeImpl += (symbols, t) =>
            {
                symbols.Where(x => x.SecurityType == SecurityType.Equity).DoForEach(symbol =>
                {
                    int n = _subscribedSymbols.AddOrUpdate(symbol.Value, 1, (key, oldValue) => oldValue + 1);
                    if (n > 1)
                    {
                        Log.Error($"TiingoDataQueueHandler tried to subscribe to already subscribed IEX symbol : {symbol.Value}");
                    }
                    else
                        if (_messageHandlers.SubscriptionIds.ContainsKey("iex"))
                            _iexConnection?.Subscribe(symbol, _messageHandlers.SubscriptionIds["iex"]);
                        else
                        {
                            Log.Error($"TiingoDataQueueHandler tried to subscribe to IEX symbol : {symbol.Value} but no subscription id found");    
                        }
                });

                symbols.Where(x => x.SecurityType == SecurityType.Forex).DoForEach(symbol =>
                {
                    int n = _subscribedSymbols.AddOrUpdate(symbol.Value, 1, (key, oldValue) => oldValue + 1);
                    if (n > 1)
                    {
                        Log.Error($"TiingoDataQueueHandler tried to subscribe to already subscribed Forex symbol : {symbol.Value}");
                    }
                    else
                    if (_messageHandlers.SubscriptionIds.ContainsKey("forex"))
                        _forexConnection?.Subscribe(symbol, _messageHandlers.SubscriptionIds["forex"]);
                    else
                    {
                        Log.Error($"TiingoDataQueueHandler tried to subscribe to Forex symbol : {symbol.Value} but no subscription id found");
                    }
                });

                symbols.Where(x => x.SecurityType == SecurityType.Crypto).DoForEach(symbol =>
                {
                    int n = _subscribedSymbols.AddOrUpdate(symbol.Value, 1, (key, oldValue) => oldValue + 1);
                    if (n > 1)
                    {
                        Log.Error($"TiingoDataQueueHandler tried to subscribe to already subscribed Crypto symbol : {symbol.Value}");
                    }
                    else
                    if (_messageHandlers.SubscriptionIds.ContainsKey("crypto"))
                        _cryptoConnection?.Subscribe(symbol, _messageHandlers.SubscriptionIds["crypto"]);
                    else
                    {
                        Log.Error($"TiingoDataQueueHandler tried to subscribe to Crypto symbol : {symbol.Value} but no subscription id found");
                    }
                });

                Refresh();
                return true;
            };

            _subscriptionManager.UnsubscribeImpl += (symbols, t) =>
            {
                symbols.Where(x => x.SecurityType == SecurityType.Equity).DoForEach(symbol =>
                {
                    int n = _subscribedSymbols.AddOrUpdate(symbol.Value, 0, (key, oldValue) => oldValue - 1);
                    if (n == 0)
                    {
                        _iexConnection?.Unsubscribe(symbol, _messageHandlers.SubscriptionIds["iex"]);
                    }
                });
                symbols.Where(x => x.SecurityType == SecurityType.Forex).DoForEach(symbol =>
                {
                    int n = _subscribedSymbols.AddOrUpdate(symbol.Value, 0, (key, oldValue) => oldValue - 1);
                    if (n == 0)
                    {
                        _forexConnection?.Unsubscribe(symbol, _messageHandlers.SubscriptionIds["forex"]);
                    }
                });
                symbols.Where(x => x.SecurityType == SecurityType.Crypto).DoForEach(symbol =>
                {
                    int n = _subscribedSymbols.AddOrUpdate(symbol.Value, 0, (key, oldValue) => oldValue - 1);
                    if (n == 0)
                    {
                        _cryptoConnection?.Unsubscribe(symbol, _messageHandlers.SubscriptionIds["crypto"]);
                    }
                });

                Refresh();
                return true;
            };

            // In this thread, we check at each interval whether the client needs to be updated
            // Subscription renewal requests may come in dozens and all at relatively same time -
            // we cannot update them one by one when work with SSE
            var clientUpdateThread = new Thread(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    _refreshEvent.WaitOne();
                    Thread.Sleep(SubscribeDelay);

                    _refreshEvent.Reset();

                    try
                    {
                        //if (_clients != null)
                        //    _clients.UpdateSubscription(_symbols.Keys.ToArray());
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        throw;
                    }
                }

            })
            { IsBackground = true };
            clientUpdateThread.Start();

        }

        private void Connect()
        {
            //_runTimeTask = Task.Run(() =>
            {
                _iexConnection.Connect();
                _forexConnection.Connect();
                _cryptoConnection.Connect();
            } //, _cts.Token);
        }


        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
            if (job == null)
                return;
            if (!job.Config.ContainsKey("tiingo-api-key"))
            {
                Log.Error($"TiingoDataQueueHandler: required ApiKey value was not provided");
                throw new ArgumentException("TiingoDataQueueHandler: required ApiKey value was not provided");
            }
            _apiKey = job.Config["tiingo-api-key"];
            int threshold = 0;
            // check if the websocket threshold setting was provided
            // but default to 1 (all updates) if not found
            if (job.Config.ContainsKey("tiingo-api-threshold"))
                int.TryParse(job.Config["tiingo-api-threshold"], out threshold);
            if (!String.IsNullOrEmpty(_apiKey))
            {
                Initialize(_apiKey, threshold);
                Connect();
            }
        }

        public bool Ready
        {
            get
            {
                return _messageHandlers.SubscriptionIds.Count >= 3;
            }
        }   

        private void Emit(BaseData tick)
        {
            _aggregator.Update(tick);
            Interlocked.Increment(ref _dataQueueCount);
        }

        /// <summary>
        /// Set the interal clock time.
        /// </summary>
        private void OnLevel1TimerEvent(object sender, Level1TimerEventArgs e)
        {
            //If there was a bad tick and the time didn't set right, skip setting it here and just use our millisecond timer to set the time from last time it was set.
            if (e.DateTimeStamp != DateTime.MinValue)
            {
                FeedTime = e.DateTimeStamp;
            }
        }

        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!dataConfig.Symbol.Value.Contains("universe", StringComparison.InvariantCultureIgnoreCase))
            {

                switch (dataConfig.SecurityType)
                {
                    case SecurityType.Equity:
                        if (!_messageHandlers.SubscriptionIds.ContainsKey("iex"))
                        {
                            Log.Error($"TiingoDataHandler: Iex websocket is not ready to accept subscriptions");
                            return Enumerable.Empty<BaseData>().GetEnumerator();
                        }
                        _iexConnection.Subscribe(dataConfig.Symbol, _messageHandlers.SubscriptionIds["iex"]);
                        break;
                    case SecurityType.Forex:
                        if (!_messageHandlers.SubscriptionIds.ContainsKey("forex"))
                        {
                            Log.Error($"TiingoDataHandler: Forex websocket is not ready to accept subscriptions");
                            return Enumerable.Empty<BaseData>().GetEnumerator();
                        }
                        _forexConnection.Subscribe(dataConfig.Symbol, _messageHandlers.SubscriptionIds["forex"]);
                        break;
                    case SecurityType.Crypto:
                        if (!_messageHandlers.SubscriptionIds.ContainsKey("crypto"))
                        {
                            Log.Error($"TiingoDataHandler: Crypto websocket is not ready to accept subscriptions");
                            return Enumerable.Empty<BaseData>().GetEnumerator();
                        }
                        _cryptoConnection.Subscribe(dataConfig.Symbol, _messageHandlers.SubscriptionIds["crypto"]);
                        break;
                    default:
                        Log.Error($"TiingoDataHandler: SecurityType {dataConfig.SecurityType.ToString()} is not supported by Tiingo");
                        return Enumerable.Empty<BaseData>().GetEnumerator();
                }
            }

            var enumerator = _aggregator.Add(dataConfig, newDataAvailableHandler);
            _subscriptionManager.Subscribe(dataConfig);

            return enumerator;
        }

        /// <summary>
        /// Unsubscribe
        /// </summary>
        public virtual void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            _subscriptionManager.Unsubscribe(dataConfig);
            _aggregator.Remove(dataConfig);
        }

        private void Refresh()
        {
            _refreshEvent.Set();
        }

        /// <summary>
        /// Returns whether the data provider is connected
        /// </summary>
        /// <returns>true if the data provider is connected</returns>
        public bool IsConnected
        {
            get
            {
                if (_messageHandlers == null || _messageHandlers.SubscriptionIds.IsNullOrEmpty())
                    return false;
                return _messageHandlers.SubscriptionIds.Keys.Count >= 3;
            }
        }

        public DateTime FeedTime { get; private set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void Dispose(bool disposing)
        {
            _aggregator.DisposeSafely();
            _cts.Cancel();

            //_clients.Dispose();

            Log.Trace("TiingoDataQueueHandler.Dispose(): Disconnected from Tiingo data provider");
        }

        ~TiingoDataQueueHandler()
        {
            Dispose(false);
        }

    }
}
