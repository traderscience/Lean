using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.ToolBox.Tiingo
{
    public class SubscriptionIdMessage
    {
        [JsonProperty(Order = 1)]
        public string SubscriptionId { get; set; }
    }

    public class SubscriptionResponseMessage
    {
        [JsonProperty(Order = 1)]
        int threshold { get; set; }
        [JsonProperty(Order = 2)]
        string thresholdLevel { get; set; }
        [JsonProperty(Order = 3)]
        string[] tickers { get; set; }
    }


    public class HeartbeatMessage
    {
        [JsonProperty(Order = 1)]
        public string Message { get; set; }
    }

    public class QuoteMessage
    {
        [JsonProperty(Order = 1)]
        public DateTime TimeStamp { get; set; }
        [JsonProperty(Order = 2)]
        public long TimestampEpoch { get; set; }

        [JsonProperty(Order = 3)]
        public Symbol Symbol { get; set; }

        [JsonProperty(Order = 4)]
        public double BidSize { get; set; }

        [JsonProperty(Order = 5)]
        public decimal BidPrice { get; set; }

        [JsonProperty(Order = 6)]
        public decimal MidPrice { get; set; }

        [JsonProperty(Order = 7)]
        public decimal AskPrice { get; set; }
        [JsonProperty(Order = 8)]
        public double AskSize { get; set; }


    }

    public class TradeMessage
    {
        [JsonProperty(Order = 1)]
        public DateTime TimeStamp { get; set; }
        [JsonProperty(Order = 2)]
        public Int64 TimestampEpoch { get; set; }
        [JsonProperty(Order = 3)]
        public Symbol Symbol { get; set; }

        [JsonProperty(Order = 9)]
        public decimal TradePrice { get; set; }

        [JsonProperty(Order = 10)]
        public double Quantity { get; set; }
    }

    public class CryptoTradeMessage
    {
        [JsonProperty(Order = 1)]
        public string Symbol { get; set; }

        [JsonProperty(Order = 2)]
        public DateTime TimeStamp { get; set; }
        [JsonProperty(Order = 3)]
        public string Broker { get; set; }

        [JsonProperty(Order = 4)]
        public double Quantity { get; set; }

        [JsonProperty(Order = 5)]
        public decimal TradePrice { get; set; }

    }

}
