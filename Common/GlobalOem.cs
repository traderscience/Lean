using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect
{
    /// <summary>
    /// DataFeedProviderName - this is used to set default feed specific attributes for TickType
    /// </summary>
    public enum DataFeedProviderName
    {
        /// All other external data feeds
        Default = 0,
        /// AlphaVantage
        AlphaVantage = 1,
        /// Binance
        Binance = 2,
        /// Bitfinex
        Bitfinex = 3,
        /// CryptoIq
        CryptoIq = 4,
        /// Ducascopy
        Dukascopy = 5,
        /// FXCM external data feed
        FXCM = 6,
        /// Coinbase (GDAX)
        GDAX = 7,
        /// IB external data feed
        InteractiveBrokers = 8,
        /// IEX external data feed
        IEX = 9,
        /// IQFeed external data feed
        IQFeed = 10,
        /// Kraken
        Kraken = 11,
        /// Oanda data feed
        Oanda = 12,
        /// Polygon
        Polygon = 13,
        /// Quandl
        Quandl = 14,
        /// Yahoo Finance
        Yahoo = 15,
        /// Zerodha
        Zerodha = 16
    }
}
