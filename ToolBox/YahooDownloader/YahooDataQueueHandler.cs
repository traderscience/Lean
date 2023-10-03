/*
    * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
    * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
    *
    * Licensed under the Apache License, Version 2.0 (the "License");
    * you may not use this file except in compliance with the License.
    * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
    *
    * Unless required by applicable law or agreed to in writing, software
    * distributed under the License is distributed on an "AS IS" BASIS,
    * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    * See the License for the specific language governing permissions and
    * limitations under the License.
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

namespace QuantConnect.ToolBox.YahooDownloader
{
    /// <summary>
    /// Yahoo DataQueueHandler is a default handler when no other feed is available
    /// </summary>
    public class YahooDataQueueHandler : IDataQueueHandler
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan SubscribeDelay = TimeSpan.FromMilliseconds(1500);
        private static bool _invalidHistDataTypeWarningFired;

        private readonly YahooEventSourceCollection _clients;
        private readonly ManualResetEvent _refreshEvent = new ManualResetEvent(false);

        private readonly ConcurrentDictionary<string, Symbol> _symbols = new ConcurrentDictionary<string, Symbol>(StringComparer.InvariantCultureIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _LastTradeTime = new ConcurrentDictionary<string, long>();

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _dataPointCount;


        private readonly IDataAggregator _aggregator = Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(
            Config.Get("data-aggregator", "QuantConnect.Lean.Engine.DataFeeds.AggregationManager"));

        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;

        public YahooDataQueueHandler()
        {
            _subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();

            _subscriptionManager.SubscribeImpl += (symbols, t) =>
            {
                symbols.DoForEach(symbol =>
                {
                    if (!_symbols.TryAdd(symbol.Value, symbol))
                    {
                        throw new InvalidOperationException($"Invalid logic, SubscriptionManager tried to subscribe to existing symbol : {symbol.Value}");
                    }
                });

                Refresh();
                return true;
            };

            _subscriptionManager.UnsubscribeImpl += (symbols, t) =>
            {
                symbols.DoForEach(symbol =>
                {
                    Symbol tmp;
                    _symbols.TryRemove(symbol.Value, out tmp);
                });

                Refresh();
                return true;
            };

            /*
            // Set the sse-clients collection
            _clients = new IEXEventSourceCollection(((o, args) =>
            {
                var message = args.Message.Data;
                ProcessJsonObject(message);

            }), _apiKey);
            */

            // In this thread, we check at each interval whether the client needs to be updated
            // Subscription renewal requests may come in dozens and all at relatively same time - we cannot update them one by one when work with SSE
            var clientUpdateThread = new Thread(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    _refreshEvent.WaitOne();
                    Thread.Sleep(SubscribeDelay);

                    _refreshEvent.Reset();

                    try
                    {
                        if (_clients != null)
                            _clients.UpdateSubscription(_symbols.Keys.ToArray());
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
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (dataConfig.ExtendedMarketHours)
            {
                Log.Error("YahooDataQueueHandler.Subscribe(): Unfortunately no updates could be received from Yahoo outside the regular exchange open hours. " +
                          "Please be aware that only regular hours updates will be submitted to an algorithm.");
            }

            if (dataConfig.Resolution < Resolution.Daily)
            {
                Log.Error($"YahooDataQueueHandler.Subscribe(): Selected data resolution ({dataConfig.Resolution}) " +
                          "is not supported by current implementation of  YahooDataQueueHandler. Sorry. Please use Daily or higher resolution.");
            }

            if (dataConfig.SecurityType != SecurityType.Equity)
            {
                return Enumerable.Empty<BaseData>().GetEnumerator();
            }

            var enumerator = _aggregator.Add(dataConfig, newDataAvailableHandler);
            _subscriptionManager.Subscribe(dataConfig);

            return enumerator;
        }

        /// <summary>
        /// Desktop/Local doesn't support live data from this handler
        /// </summary>
        public virtual void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            _subscriptionManager.Unsubscribe(dataConfig);
            _aggregator.Remove(dataConfig);
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
        }

        private void Refresh()
        {
            _refreshEvent.Set();
        }

        /// <summary>
        /// Returns whether the data provider is connected
        /// </summary>
        /// <returns>true if the data provider is connected</returns>
        public bool IsConnected => false;

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

            _clients.Dispose();

            Log.Trace("YahooDataQueueHandler.Dispose(): Disconnected from Yahoo data provider");
        }

        ~YahooDataQueueHandler()
        {
            Dispose(false);
        }

    }
}
