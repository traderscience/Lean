using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using static LaunchDarkly.Logging.LogCapture;

namespace QuantConnect.ToolBox.Tiingo
{
    public class TiingoWebSocketMessageHandlers
    {
        private Action<BaseData> _tickHandler;
        private ConcurrentDictionary<string, int> _subscribedSymbols;

        // Callback to handle socket ready condition

        private ConcurrentDictionary<string,  string[]> ServerSubscribedTickers = new ConcurrentDictionary<string, string[]>();


        public ConcurrentDictionary<string, string> SubscriptionIds { get; private set; }

        public TiingoWebSocketMessageHandlers(Action<BaseData> tickHandler, ConcurrentDictionary<string, int> subscribedSymbols)
        {
            _tickHandler = tickHandler;
            SubscriptionIds = new ConcurrentDictionary<string, string>();
            _subscribedSymbols = subscribedSymbols;

        }
        public void ProcessIexMessage(string jsonMessage)
        {
            // Process the message from Tiingo and extract the stock symbol and price
            JObject messageObject = JObject.Parse(jsonMessage);
            string messageType = (string)messageObject["messageType"];

            if (messageType == "A")
            {
                var data = (JArray)messageObject["data"];
                var symbol = Symbol.Create((string)data[3], SecurityType.Equity, Market.USA);

                if (!_subscribedSymbols.ContainsKey(symbol.Value))
                    return;

                string dataType = data[0].Value<string>();

                switch (dataType)
                {
                    case "Q":
                        try
                        {
                            var quoteMessage = new QuoteMessage()
                            {
                                TimeStamp = (DateTime)data[1],
                                TimestampEpoch = (long)data[2],
                                Symbol = symbol,
                                BidSize = (double)(float)data[4],
                                BidPrice = (decimal)(float)data[5],
                                MidPrice = (decimal)(float)data[6],
                                AskPrice = (decimal)(float)data[7],
                                AskSize = (double)(float)data[8]
                            };
                            // process new Quote
                            //
                            string saleCondition = null;
                            string exchange = null;
                            var quoteTick = new Tick(quoteMessage.TimeStamp,
                                                        quoteMessage.Symbol,
                                                        saleCondition,
                                                        exchange,
                                                        (decimal)quoteMessage.BidSize,
                                                        (decimal)quoteMessage.BidPrice,
                                                        (decimal)quoteMessage.AskSize,
                                                        (decimal)quoteMessage.AskPrice);

                            _tickHandler(quoteTick);
                        }
                        catch (Exception ex)
                        {

                        }
                        break;
                    case "T":
                        try
                        {
                            var tradeMessage = new TradeMessage()
                            {
                                TimeStamp = (DateTime)data[1],
                                TimestampEpoch = (Int64)data[2],
                                Symbol = symbol,
                                TradePrice = (decimal)data[9],
                                Quantity = (double)data[10]
                            };
                            bool halted = data[11].HasValues && (int)data[11] == 1;
                            bool afterHours = data[12].HasValues && (int)data[12] == 1;
                            bool intermarketSweepOrder = data[13].HasValues && (int)data[13] == 1;
                            bool oddLot = data[14].HasValues && (int)data[14] == 1;
                            bool nmsRule611 = data[15].HasValues && (int)data[15] == 1;
                            string saleCondition = null;
                            string exchange = "IEX";
                            // build a Trade tick
                            var tick = new Tick(tradeMessage.TimeStamp, tradeMessage.Symbol, saleCondition, exchange,
                                    (decimal)tradeMessage.Quantity, (decimal)tradeMessage.TradePrice);
                            _tickHandler(tick);
                        }
                        catch (Exception ex)
                        {

                        }
                        break;
                    default:
                        break;
                }
            }
            else if (messageType == "H")
            {
                //QuantConnect.Logging.Log.Trace("Tiingo:Iex:WebSocket: Heartbeat received");
            }
            else if (messageType == "I")
            {
                var idMessage = messageObject["data"].ToObject<SubscriptionIdMessage>();
                if (messageObject["data"].Value<JObject>().ContainsKey("subscriptionId"))
                {
                    var subscriptionId = messageObject["data"]["subscriptionId"].ToObject<string>();
                    SubscriptionIds.AddOrUpdate("iex", subscriptionId);
                    QuantConnect.Logging.Log.Trace($"Tiingo:Iex:WebSocket:SubscriptionId: Started subscribing to Tiingo WebSocket {subscriptionId}");
                }
                if (messageObject["data"].Value<JObject>().ContainsKey("tickers"))
                {
                    // Write the updated ticker list to the log
                    var tickerList = messageObject["data"]["tickers"].ToObject<List<string>>();
                    QuantConnect.Logging.Log.Trace($"Tiingo:Iex:WebSocket:IEX Subscribed Tickers: {string.Join(",", tickerList)}");
                    ServerSubscribedTickers.AddOrUpdate("iex", tickerList.ToArray());
                }
            }
            else if (messageType == "E")
            {
                try
                {
                    string code = messageObject["response"]["code"].ToString();
                    string errmsg = messageObject["response"]["message"].ToString();
                    QuantConnect.Logging.Log.Error($"Tiingo:Iex:WebSocket: Error:{code} Message: {errmsg}");
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                QuantConnect.Logging.Log.Error($"Tiingo:IEX:WebSocket: unknown message type: {messageType}");
            }
        }

        public void ProcessForexMessage(string jsonMessage)
        {
            // Process the message from Tiingo and extract the symbol and price
            JObject messageObject = JObject.Parse(jsonMessage);
            string messageType = (string)messageObject["messageType"];
            if (messageType == "A")
            {
                var data = (JArray)messageObject["data"];
                var symbol = Symbol.Create((string)data[1], SecurityType.Forex, Market.Oanda);

                if (!_subscribedSymbols.ContainsKey(symbol.Value))
                    return;

                string dataType = data[0].Value<string>();
                switch (dataType)
                {
                    case "Q":
                        try
                        {
                            var quoteMessage = new QuoteMessage()
                            {
                                Symbol = symbol,
                                TimeStamp = (DateTime)data[2],
                                BidSize = (double)(float)data[3],
                                BidPrice = (decimal)(float)data[4],
                                MidPrice = (decimal)(float)data[5],
                                AskSize = (double)(float)data[6],
                                AskPrice = (decimal)(float)data[7]
                            };
                            // process new Quote
                            //
                            string saleCondition = null;
                            string exchange = null;
                            var quoteTick = new Tick(quoteMessage.TimeStamp, quoteMessage.Symbol, 
                                                        saleCondition,
                                                        exchange,
                                                        (decimal)quoteMessage.BidSize,
                                                        (decimal)quoteMessage.BidPrice,
                                                        (decimal)quoteMessage.AskSize,
                                                        (decimal)quoteMessage.AskPrice);
                            _tickHandler(quoteTick);
                        }
                        catch (Exception ex)
                        {

                        }
                        break;
                    case "T":
                        try
                        {
                            // process new Trade
                            var tradeBar = new QuantConnect.Data.Market.TradeBar()
                            {
                                Symbol = symbol,
                                Time = (DateTime)data[2],
                                Close = (decimal)(float)data[9],
                                Volume = (decimal)(float)data[10]
                            };
                            _tickHandler(tradeBar);
                        }
                        catch (Exception ex)
                        {

                        }
                        break;
                    default:
                        break;
                }
            }
            else if (messageType == "H")
            {
                //QuantConnect.Logging.Log.Trace("Tiingo:Forex:WebSocket: Heartbeat received");
            }
            else if (messageType == "I")
            {
                string code = messageObject["response"]["code"].ToString();
                string message = messageObject["response"]["message"].ToString();
                if (messageObject["data"].Value<JObject>().ContainsKey("subscriptionId"))
                {
                    string subscriptionId = messageObject["data"]["subscriptionId"].ToString();
                    SubscriptionIds.AddOrUpdate("forex", subscriptionId);
                    QuantConnect.Logging.Log.Trace($"Tiingo:Forex:WebSocket:SubscriptionId: Started subscribing to Tiingo WebSocket {subscriptionId}");
                }
                if (messageObject["data"].Value<JObject>().ContainsKey("tickers"))
                {
                    var tickerList = messageObject["data"]["tickers"].ToObject<List<string>>();
                    QuantConnect.Logging.Log.Trace($"Tiingo:Forex:WebSocket:Forex Subscribed Tickers: {string.Join(",", tickerList)}");
                    ServerSubscribedTickers.AddOrUpdate("forex", tickerList.ToArray());
                }
            }
            else if (messageType == "E")
            {
                try
                {
                    string code = messageObject["response"]["code"].ToString();
                    string errmsg = messageObject["response"]["message"].ToString();
                    QuantConnect.Logging.Log.Error($"Tiingo:Forex:WebSocket: Error:{code} Message: {errmsg}");
                    if (code == "404")
                    {
                        // if we get a 404, we need to reconnect
                    }
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                QuantConnect.Logging.Log.Error($"Tiingo:Forex:WebSocket: unknown message type: {messageType}");
            }
        }

        public void ProcessCryptoMessage(string jsonMessage)
        {
            // Process the message from Tiingo and extract the symbol and price
            JObject messageObject = JObject.Parse(jsonMessage);
            string messageType = (string)messageObject["messageType"];

            if (messageType == "A")
            {
                var data = (JArray)messageObject["data"];
                string dataType = data[0].Value<string>();
                var symbol = Symbol.Create((string)data[1], SecurityType.Crypto, Market.Binance);

                if (!_subscribedSymbols.ContainsKey(symbol.Value))
                    return;

                string exchange = (string)data[3];

                switch (dataType)
                {
                    case "Q":
                        try
                        {
                            var quoteMessage = new QuoteMessage()
                            {
                                TimeStamp = (DateTime)data[1],
                                TimestampEpoch = (long)data[2],
                                Symbol = symbol,
                                BidSize = (double)data[4],
                                BidPrice = (decimal)data[5],
                                MidPrice = (decimal)data[6],
                                AskPrice = (decimal)data[7],
                                AskSize = (double)data[8]
                            };
                            // process new Quote
                            //
                            string saleCondition = null;
                            var quoteTick = new Tick(quoteMessage.TimeStamp, quoteMessage.Symbol,
                                                        saleCondition,
                                                        exchange,
                                                        (decimal)quoteMessage.BidSize,
                                                        (decimal)quoteMessage.BidPrice,
                                                        (decimal)quoteMessage.AskSize,
                                                        (decimal)quoteMessage.AskPrice);

                            _tickHandler(quoteTick);
                        }
                        catch (Exception ex)
                        {

                        }
                        break;
                    case "T":
                        try
                        {
                            var tradeMessage = new CryptoTradeMessage()
                            {
                                Symbol = symbol,
                                TimeStamp = (DateTime)data[2],
                                Broker = (string)data[3],
                                Quantity = (float)data[4],
                                TradePrice = (decimal)(float)data[5],
                            };
                            var tick = new QuantConnect.Data.Market.Tick(tradeMessage.TimeStamp, symbol, "", tradeMessage.Broker, (decimal)tradeMessage.Quantity, tradeMessage.TradePrice);
                            _tickHandler(tick);
                        }
                        catch (Exception ex)
                        {

                        }
                        break;
                    default:
                        break;
                }
            }
            else if (messageType == "H")
            {
                //QuantConnect.Logging.Log.Trace("Tiingo:Crypto:WebSocket: Heartbeat received");
            }
            else if (messageType == "I")
            {
                if (messageObject["data"].Value<JObject>().ContainsKey("subscriptionId"))
                {
                    var subscriptionId = messageObject["data"]["subscriptionId"].ToObject<string>();
                    SubscriptionIds.AddOrUpdate("crypto", subscriptionId);
                    QuantConnect.Logging.Log.Trace($"Tiingo:Crypto:WebSocket:SubscriptionId: Started subscribing to Tiingo Websocket {subscriptionId}");
                }
                if (messageObject["data"].Value<JObject>().ContainsKey("tickers"))
                {
                    if (messageObject["data"].Value<JObject>().ContainsKey("tickers"))
                    {
                        var tickerList = messageObject["data"]["tickers"].ToObject<List<string>>();
                        QuantConnect.Logging.Log.Trace($"Tiingo:Crypto:WebSocket:Crypto Subscribed Tickers: {string.Join(",", tickerList)}");
                        ServerSubscribedTickers.AddOrUpdate("crypto", tickerList.ToArray());
                    }
                }
            }
            else if (messageType == "E")
            {
                try
                {
                    string code = messageObject["response"]["code"].ToString();
                    string errmsg = messageObject["response"]["message"].ToString();
                    QuantConnect.Logging.Log.Error($"Tiingo:Crypto:WebSocket: Error:{code} Message: {errmsg}");
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                QuantConnect.Logging.Log.Error($"Tiingo:Crypto:WebSocket: unknown message type: {messageType}");
            }
        }


    }
}
