using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Indicators;
using QuantConnect.Brokerages;
using System.Linq.Expressions;

namespace QuantConnect.ToolBox.Tiingo
{
    public enum TiingoSocketType
    {
        Undefined = 0,
        Equities = 1,
        Forex = 2,
        Crypto = 3
    }

    public class TiingoWebSocket
    {
        private WebSocketClientWrapper _ws;
        private string _apiKey;
        private Action<string> _messageHandler;
        private int _threshold;
        private string _url;
        private TiingoSocketType _socketType;
        private readonly ManualResetEvent _refreshEvent = new ManualResetEvent(false);
        private CancellationTokenSource _cts;
        private int SubscribeDelay = 1500;
        private Thread _clientMonitorThread;
        private List<Symbol> _subscribedSymbols = new List<Symbol>();
        public int OnMessageReceived { get; private set; }
        public int OnOpened { get; private set; }

        public TiingoWebSocket(string ApiKey, TiingoSocketType socketType, int threshold, Action<string> messageHandler)
        {
            _apiKey = ApiKey;
            _socketType = socketType;
            switch (socketType)
            {
                case TiingoSocketType.Equities:
                    _url = "wss://api.tiingo.com/iex";
                    break;
                case TiingoSocketType.Forex:
                    _url = "wss://api.tiingo.com/fx";
                    break;
                case TiingoSocketType.Crypto:
                    _url = "wss://api.tiingo.com/crypto";
                    break;
                default:
                    throw new ArgumentException("Invalid socket type");
            }
            /*
            _subscribedSymbols = new List<Symbol>()
            {
                Symbol.Create("SPY", SecurityType.Equity, Market.USA),  
                Symbol.Create("EURUSD", SecurityType.Forex, Market.Oanda),
                Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Binance)
            };
            */

            _threshold = threshold;
            _messageHandler = messageHandler;
            _cts = new CancellationTokenSource();

            _ws = new WebSocketClientWrapper();
            _ws.Initialize(_url);
            _ws.Message += OnMessage;
            _ws.Closed += OnClosed;
            _ws.Error += OnError;
            _ws.Open += OnOpen;
        }
        public void Connect()
        {
            _ws.Connect();
            // In this thread, we check at each interval whether the client needs to be updated
            // Subscription renewal requests may come in dozens and all at relatively same time -
            // we cannot update them one by one when work with SSE
            _clientMonitorThread = new Thread(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    _refreshEvent.WaitOne();
                    Thread.Sleep(SubscribeDelay);

                    _refreshEvent.Reset();

                    try
                    {
                        if (!_ws.IsOpen)
                        {
                            Log.Error($"TiingoWebSocket:Status: {_socketType.ToString()} is closed.");
                        }
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
            _clientMonitorThread.Start();

        }

        private void OnOpen(object sender, EventArgs e)
        {
            Log.Trace($"TiingoWebSocket: Socket {_socketType.ToString()} Opened");
            List<string> tickers = new List<string>();
            switch (_socketType)
            {
                case TiingoSocketType.Equities:
                    tickers.AddRange(_subscribedSymbols.Where(x => x.SecurityType == SecurityType.Equity).Select(s => s.Value).ToList());
                    break;
                case TiingoSocketType.Forex:
                    tickers.AddRange(_subscribedSymbols.Where(x => x.SecurityType == SecurityType.Forex).Select(s => s.Value).ToList());
                    break;
                case TiingoSocketType.Crypto:
                    tickers.AddRange(_subscribedSymbols.Where(x => x.SecurityType == SecurityType.Crypto).Select(s => s.Value).ToList());
                    break;
            }
            var subscribe = new
            {
                eventName = "subscribe",
                authorization = _apiKey,
                eventData = new
                {
                    threshold = _threshold,
                    tickers = tickers.ToArray()
                }
            };
            // send initial subscription request, this will return a subscription id
            Send(subscribe);
        }

        private void OnError(object sender, Brokerages.WebSocketError e)
        {
            Log.Error($"TiingoWebSocket: Socket Error: {e.Message}");
        }

        private void OnClosed(object sender, WebSocketCloseData e)
        {
            Log.Error($"TiingoWebSocket: Socket Closed by Remote: {e.Reason}");
        }

        private void OnMessage(object sender, WebSocketMessage e)
        {

            switch (e.Data.MessageType)
            {
                case WebSocketMessageType.Text:
                    var txtMsg = (WebSocketClientWrapper.TextMessage)e.Data;
                    _messageHandler(txtMsg.Message);
                    break;
                case WebSocketMessageType.Binary:
                    break;
                case WebSocketMessageType.Close:
                    Log.Error($"TiingoWebSocket: Socket Closed by Remote");
                    break;
                default:
                    break;
            }
        }

        public void Send(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);

            if (Log.DebuggingEnabled)
            {
                Log.Debug("TiingoWebSocketClient.Send(): " + json);
            }
            _ws.Send(json);
        }

        public void Subscribe(Symbol symbol, string subscId)
        {
            if (!_subscribedSymbols.Contains(symbol))
                _subscribedSymbols.Add(symbol);

            var subscribe = new
            {
                eventName = "subscribe",
                authorization = _apiKey,
                eventData = new
                {
                    subscriptionId = long.Parse(subscId),
                    tickers = _subscribedSymbols.Select(s => s.Value).ToArray()
                }
            };
            Send(subscribe);
        }

        public void Subscribe(List<Symbol> symbols, string subscId)
        {
            var tickers = symbols.Select(x => x.Value).ToArray();
            var subscribe = new
            {
                eventName = "subscribe",
                authorization = _apiKey,
                eventData = new
                {
                    subscriptionId = long.Parse(subscId),
                    tickers = tickers
                }
            };
            Send(subscribe);
        }


        public void Subscribex(Symbol symbol, string subscId)
        {
            if (symbol == null || String.IsNullOrEmpty(subscId))
                return;

            var client = new WebSocketClientWrapper();
            client.Initialize(_url);
            client.Connect();
            var subscribe = new
            {
                eventName = "subscribe",
                authorization = _apiKey,
                eventData = new
                {
                    subscriptionId = long.Parse(subscId),
                    tickers = new string[] { symbol.Value }
                }
            };
            string subscribeJson = JsonConvert.SerializeObject(subscribe);
            ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(subscribeJson));
            client.Send(subscribeJson);
            client.Close();
        }

        public void Unsubscribe(Symbol symbol, string subscId)
        {
            var unsubscribe = new
            {
                eventName = "unsubscribe",
                authorization = _apiKey,
                eventData = new
                {
                    subscriptionId = long.Parse(subscId),
                    tickers = new string[] { symbol.Value }
                }
            };
            Send(unsubscribe);
        }       
    }

    //-----------------------------------------------------------------------
    // TiingoWebSocketConnection base class
    //-----------------------------------------------------------------------
    public class TiingoWebSocket2 : IDisposable
    {
        private string _apiKey;
        private string _url;
        private int _threshold = 1;
        private string[] _initial_tickers;
        private bool _subscribeAll = false;
        private CancellationTokenSource _cancelTokenSource;
        public TiingoWebSocket2(string apiKey, string url, string[] tickers, int threshold = 3)
        {
            _apiKey = apiKey;
            _url = url;
            _threshold = threshold;
            _initial_tickers = tickers;
            if (_initial_tickers == null)
            {
                _subscribeAll = false;
                _initial_tickers = Array.Empty<string>();
            }
            else
            if (_initial_tickers.Any() && _initial_tickers[0] == "*")
                _subscribeAll = true;
        }

        public async Task StartListeningAsync(Action<string> onMessageReceived)
        {
            using (var ws = new ClientWebSocket())
            {
                _cancelTokenSource = new CancellationTokenSource();
                // Connect to Tiingo WebSocket endpoint
                await ws.ConnectAsync(new Uri(_url), _cancelTokenSource.Token).ConfigureAwait(false);

                // setup subscription options
                var subscribe = new
                {
                    eventName = "subscribe",
                    authorization = _apiKey,
                    eventData = new
                    {
                        threshold = _threshold,
                        tickers = _initial_tickers // _initial_tickers
                    }
                };
                string subscribeJson = JsonConvert.SerializeObject(subscribe);
                ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(subscribeJson));
                await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);

                _isConnected = ws.State == WebSocketState.Open;

                while (ws.State == WebSocketState.Open && !_cancelTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var buffer = new ArraySegment<byte>(new byte[1024]);
                        var result = await ws.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
                        }
                        else
                        {
                            var message = System.Text.Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                            //JObject messageObject = JObject.Parse(message);
                            //string messageType = (string)messageObject["messageType"];
                            onMessageReceived(message);
                        }
                    }
                    catch (System.Net.WebSockets.WebSocketException ex)
                    {
                        Log.Error($"TiingoWebSocketConnection: Socket exception during ReceiveAsync {ex.Message}");
                        // TODO: we may want to reconnect after this type of exception
                        // re-throw exeception
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"TiingoWebSocketConnection: exception during ReceiveAsync {ex.ToString}");
                    }
                }

                _isConnected = false;
            }
        }

        public void StopListening() 
        {
            if (_cancelTokenSource != null)
                _cancelTokenSource.Cancel();
        }

        private bool _isConnected;
        public bool IsConnected { get { return _isConnected; } }

        /// <summary>
        /// Subscribe to real time updates for new symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="subscId"></param>
        public async Task<bool> Subscribe(Symbol symbol, string subscId)
        {
            if (symbol == null || String.IsNullOrEmpty(subscId))
                return false;
            if (_subscribeAll)
                return true;

            using (var ws = new ClientWebSocket())
            {
                // Connect to Tiingo WebSocket endpoint
                await ws.ConnectAsync(new Uri(_url), CancellationToken.None).ConfigureAwait(false);

                var subscribe = new
                {
                    eventName = "subscribe",
                    authorization = _apiKey,
                    eventData = new
                    {
                        subscriptionId = long.Parse(subscId),
                        tickers = new string[] { symbol.Value }
                    }
                };
                string subscribeJson = JsonConvert.SerializeObject(subscribe);
                ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(subscribeJson));
                await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            return true;
        }

        /// <summary>
        /// Unsubscribe a symbol from real time updates
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="subscriptionId"></param>
        public async void Unsubscribe(Symbol symbol, string subscriptionId)
        {
            if (_subscribeAll)
                return;

            if (symbol == null || String.IsNullOrEmpty(subscriptionId))
                return;

            using (var ws = new ClientWebSocket())
            {
                // Connect to Tiingo Firehose WebSocket endpoint
                await ws.ConnectAsync(new Uri(_url), CancellationToken.None).ConfigureAwait(false);

                var subscribe = new
                {
                    eventName = "unsubscribe",
                    authorization = _apiKey,
                    eventData = new
                    {
                        subscriptionId = subscriptionId,
                        tickers = new string[] { symbol.Value }
                    }
                };
                string subscribeJson = JsonConvert.SerializeObject(subscribe);
                ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(subscribeJson));
                await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            return;
        }

        public void Dispose()
        {
        }
    }
}
