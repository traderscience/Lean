using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Securities
{
    /// <summary>
    /// HoldEvent - generated when brokerage detects a change in the account portfolio
    /// </summary>
    public class HoldingEvent : Holding
    {
        /// <summary>
        /// Time stamp for the holding change
        /// </summary>
        public DateTime UtcTime;

        /// <summary>
        /// HoldingEvent - from Holding
        /// </summary>
        /// <param name="holding"></param>
        public HoldingEvent(Holding holding)
        {
            AveragePrice = holding.AveragePrice;
            Symbol = holding.Symbol;
            //Type = holding.Type; // rje - check
            Quantity = holding.Quantity;
            MarketPrice = holding.MarketPrice;
            MarketValue = holding.MarketValue;
            UnrealizedPnL = holding.UnrealizedPnL;
            ConversionRate = holding.ConversionRate;
            CurrencySymbol = holding.CurrencySymbol;
        }
    }
}
