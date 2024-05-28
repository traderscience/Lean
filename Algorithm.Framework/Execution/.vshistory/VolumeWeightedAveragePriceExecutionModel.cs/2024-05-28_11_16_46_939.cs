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
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Securities;
using QuantConnect.Orders;
using static QuantConnect.Messages;
using QuantConnect.Algorithm.Framework.Alphas;
using System.Net;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Execution model that submits orders while the current market price is more favorable that the current volume weighted average price.
    /// </summary>
    public class VolumeWeightedAveragePriceExecutionModel : ExecutionModel
    {
        private readonly PortfolioTargetCollection _targetsCollection = new PortfolioTargetCollection();
        private readonly Dictionary<Symbol, SymbolData> _symbolData = new Dictionary<Symbol, SymbolData>();
        private string _datefmt = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Gets or sets the maximum order quantity as a percentage of the current bar's volume.
        /// This defaults to 0.01m = 1%. For example, if the current bar's volume is 100, then
        /// the maximum order size would equal 1 share.
        /// </summary>
        public decimal MaximumOrderQuantityPercentVolume { get; set; } = 0.01m;

        /// <summary>
        /// Wiggle room for determining a favorable entry price
        /// </summary>
        public decimal Wiggle = .005m;


        /// <summary>
        /// Resolution to use for VWAP calculations
        /// </summary>
        public Resolution? SecurityResolution { get; set; } = Resolution.Hour;

        /// <summary>
        /// Determines if orders can be place in extended hours trading
        /// </summary>
        public bool ExtendedHours = false;

        public decimal MinimumQuantity = 100;

        public decimal LotSize = 100;

        public bool AllowMarketOnOpenOrders = true;

        /// <summary>
        /// Default constructor
        /// </summary>
        public VolumeWeightedAveragePriceExecutionModel()
        {
            MaximumOrderQuantityPercentVolume = .01m;
            ExtendedHours = false;
            SecurityResolution = null;
            MinimumQuantity = 100;
            AllowMarketOnOpenOrders = true;
            Wiggle = .005m;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="MaxPct"></param>
        /// <param name="extendedHours"></param>
        /// <param name="secResolution"></param>
        public VolumeWeightedAveragePriceExecutionModel(decimal MaxPct = .01m, bool extendedHours = false, Resolution? secResolution = null, decimal MinimumQty=100m, decimal LotSize=100m,  bool AllowMarketOnOpenOrders = true, decimal Wiggle = .005m)
        {
            this.MaximumOrderQuantityPercentVolume = MaxPct;
            this.ExtendedHours = extendedHours;
            this.SecurityResolution = secResolution;
            this.MinimumQuantity = MinimumQty;
            this.LotSize = LotSize;
            this.AllowMarketOnOpenOrders = AllowMarketOnOpenOrders;
            this.Wiggle = Wiggle;
        }


        /// <summary>
        /// Submit orders for the specified portfolio targets.
        /// This model is free to delay or spread out these orders as it sees fit
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The portfolio targets to be ordered</param>
        public override void Execute(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            // update the complete set of portfolio targets with the new targets
            _targetsCollection.AddRange(targets);

            // for performance we check count value, OrderByMarginImpact and ClearFulfilled are expensive to call
            if (!_targetsCollection.IsEmpty)
            {
                foreach (var target in _targetsCollection.OrderByMarginImpact(algorithm))
                {
                    var symbol = target.Symbol;

                    // calculate remaining quantity to be ordered
                    var unorderedQuantity = OrderSizing.GetUnorderedQuantity(algorithm, target);

                    // Check if quantity to order is < MinimumQuantity, or 0 
                    if (unorderedQuantity == 0 || MinimumQuantity > 0 && Math.Abs(unorderedQuantity) < MinimumQuantity)
                    {
                        continue;
                    }

                    // fetch our symbol data containing our VWAP indicator
                    SymbolData data;
                    if (!_symbolData.TryGetValue(symbol, out data))
                    {
                        continue;
                    }

                    // If ExtendedHours is false, then we need to check if we are in extended hours
                    if (!ExtendedHours)
                    {
                        var time = algorithm.Time;
                        var regularHours = data.Security.Exchange.Hours.IsOpen(time, false);
                        var extendedHours = !regularHours && data.Security.Exchange.Hours.IsOpen(time, true);
                        if (extendedHours)
                            continue;
                    }

                    // Check if Market on Open Orders on Permitted
                    if (!AllowMarketOnOpenOrders && !data.Security.Exchange.ExchangeOpen)
                        continue;

                    // check order entry conditions
                    // TraderScience fix - check if target is 0 (eg trailing stop)
                    // if so, then we need to close the position immediately
                    /*
                    if (target.Quantity == 0)
                    {
                        OrderIntent intent = OrderIntent.Undefined;
                        var qtyHeld = data.Security.Holdings.Quantity;
                        intent = qtyHeld > 0 ? OrderIntent.STC : OrderIntent.BTC;
                        var orderProps = new OrderProperties()
                        {
                            Intent = intent
                        };
                        algorithm.MarketOrder(data.Security, -qtyHeld, tag: $"VWAP Closing Qty {qtyHeld} (target=0 specified)", orderProperties: orderProps);
                    }
                    else
                    */
                    // if the target is flat, or price is favorable, then proceed with an order
                    if (target.Quantity == 0 || PriceIsFavorable(data, unorderedQuantity))
                    {
                        // adjust order size to respect maximum order size based on a percentage of current volume
                        var quantity = OrderSizing.GetOrderSizeForPercentVolume(
                            data.Security, MaximumOrderQuantityPercentVolume, unorderedQuantity);

                        if (quantity != 0)
                        {
                            var currentQty = algorithm.Portfolio[data.Security.Symbol].Quantity;
                            OrderIntent intent = OrderIntent.Undefined;
                            if (currentQty == 0)
                                intent = quantity > 0 ? OrderIntent.BTO : OrderIntent.STO;
                            else
                            {
                                if (currentQty > 0)
                                    intent = quantity < 0 ? OrderIntent.STC : OrderIntent.BTO;
                                else
                                    intent = quantity > 0 ? OrderIntent.BTC : OrderIntent.STO;
                            }
                            var orderProps = new OrderProperties()
                            {
                                Intent = intent
                            };

                            // Run margin check for the proposed order
                            var buyingPower = data.Security.BuyingPowerModel.GetBuyingPower(
                                        new BuyingPowerParameters(
                                                algorithm.Portfolio, data.Security, quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell));
                            var marginRequired = data.Security.BuyingPowerModel.GetInitialMarginRequirement(data.Security, Math.Abs(quantity));

                            if (buyingPower.Value >= marginRequired)
                            {
                                quantity = RoundToLotSize(data, quantity);
                                algorithm.MarketOrder(data.Security, quantity, tag: "VWAP Order", orderProperties: orderProps);
                            }
                            else
                            {
                                // try reducing the order quantity by using the available margin + 10%
                                var old_quantity = quantity;
                                quantity = Orders.OrderSizing.GetOrderSizeForMaximumValue(data.Security, buyingPower.Value * .9m, quantity);
                                quantity = RoundToLotSize(data, quantity);
                                if (MinimumQuantity > 0 && quantity < MinimumQuantity)
                                    continue;
                                if (algorithm.DebugMode)
                                    algorithm.Error($"{algorithm.Time.ToString(_datefmt)} VWAP Execution Model: Buying Power:${buyingPower.Value:F0} insufficient for Qty:{old_quantity} {data.Security.Symbol}. Adjusted to:{quantity}");
                                if (quantity != 0)
                                    algorithm.MarketOrder(data.Security, quantity, tag: "VWAP Order (size adjusted)", orderProperties: orderProps);
                            }
                        }
                    }
                }

                _targetsCollection.ClearFulfilled(algorithm);
            }
        }

        private decimal RoundToLotSize(SymbolData data, decimal quantity)
        {
            // Round quantity based on security type
            switch (data.Security.Symbol.SecurityType)
            {
                case SecurityType.Equity:
                    // round quantity to nearest smaller LotSize
                    if (LotSize > 1)
                        quantity = Math.Floor(quantity / LotSize) * LotSize;
                    else
                        quantity = Math.Round(quantity, 2);
                    break;
                case SecurityType.Future:
                case SecurityType.Option:
                    quantity = Math.Round(quantity, 0);
                    break;
                case SecurityType.Forex:
                case SecurityType.Crypto:
                    quantity = Math.Round(quantity, 4);
                    break;
            }
            return quantity;
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
            foreach (var added in changes.AddedSecurities)
            {
                if (!_symbolData.ContainsKey(added.Symbol))
                {
                    _symbolData[added.Symbol] = new SymbolData(algorithm, added, SecurityResolution);
                }
            }

            foreach (var removed in changes.RemovedSecurities)
            {
                // clean up removed security data
                SymbolData data;
                if (_symbolData.TryGetValue(removed.Symbol, out data))
                {
                    if (IsSafeToRemove(algorithm, removed.Symbol))
                    {
                        _symbolData.Remove(removed.Symbol);
                        algorithm.SubscriptionManager.RemoveConsolidator(removed.Symbol, data.Consolidator);
                    }
                }
            }
        }

        /// <summary>
        /// Determines if it's safe to remove the associated symbol data
        /// </summary>
        protected virtual bool IsSafeToRemove(QCAlgorithm algorithm, Symbol symbol)
        {
            // confirm the security isn't currently a member of any universe
            return !algorithm.UniverseManager.Any(kvp => kvp.Value.ContainsMember(symbol));
        }

        /// <summary>
        /// Determines if the current price is better than VWAP
        /// </summary>
        protected virtual bool PriceIsFavorable(SymbolData data, decimal unorderedQuantity)
        {
            var diff = Wiggle * data.VWAP;
            if (unorderedQuantity > 0)
            {
                if (data.Security.BidPrice < data.VWAP + diff)
                {
                    return true;
                }
            }
            else
            {
                if (data.Security.AskPrice > data.VWAP - Wiggle)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Symbol data for this Execution Model
        /// </summary>
        protected class SymbolData
        {
            /// <summary>
            /// Security
            /// </summary>
            public QuantConnect.Securities.Security Security { get; }

            /// <summary>
            /// VWAP Indicator
            /// </summary>
            public IntradayVwap VWAP { get; }

            /// <summary>
            /// Data Consolidator
            /// </summary>
            public IDataConsolidator Consolidator { get; }

            /// <summary>
            /// Initialize a new instance of <see cref="SymbolData"/>
            /// </summary>
            public SymbolData(QCAlgorithm algorithm, QuantConnect.Securities.Security security, Resolution? secResolution)
            {
                Security = security;
                try
                {
                    Consolidator = algorithm.ResolveConsolidator(security.Symbol, secResolution == null ? security.Resolution : secResolution);
                    var name = algorithm.CreateIndicatorName(security.Symbol, "VWAPExecution", secResolution == null ? security.Resolution : secResolution);
                    VWAP = new IntradayVwap(name);
                    algorithm.RegisterIndicator(security.Symbol, VWAP, Consolidator, bd => (BaseData)bd);
                }
                catch (Exception ex)
                {
                    Logging.Log.Error($"Exception adding {security.Symbol.Value} for VWAP Execution Model ex={ex.ToString()}");
                }
            }
        }
    }
}
