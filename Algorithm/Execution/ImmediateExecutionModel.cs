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

using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Algorithm.Framework.Portfolio;
using System;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Provides an implementation of <see cref="IExecutionModel"/> that immediately submits
    /// market orders to achieve the desired portfolio targets
    /// </summary>
    public class ImmediateExecutionModel : ExecutionModel
    {
        private readonly PortfolioTargetCollection _targetsCollection = new PortfolioTargetCollection();
        private string _datefmt = "yyyy-MM-dd HH:mm:ss";

        public decimal MinimumQuantity { get; set; } = 100m;
        public decimal LotSize { get; set; } = 100m;

        /// <summary>
        /// Immediately submits orders for the specified portfolio targets.
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The portfolio targets to be ordered</param>
        public override void Execute(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            _targetsCollection.AddRange(targets);
            // for performance we use IsEmpty, OrderByMarginImpact and ClearFulfilled are expensive to call
            if (!_targetsCollection.IsEmpty)
            {
                foreach (var target in _targetsCollection.OrderByMarginImpact(algorithm))
                {
                    var security = algorithm.Securities[target.Symbol];

                    // calculate remaining quantity to be ordered
                    if (target.Quantity == 0)
                    {
                        // We've been requested to close the position.
                        // First, cancel any pending bracket orders for this symbol
                        algorithm.Transactions.CancelOpenOrders(target.Symbol);
                        //algorithm.Insights.RemoveInsights((x) => x.Symbol == target.Symbol);
                    }

                    var quantity = OrderSizing.GetUnorderedQuantity(algorithm, target, security);
                    if (quantity != 0)
                    {
                        if (security.BuyingPowerModel.AboveMinimumOrderMarginPortfolioPercentage(security, quantity,
                            algorithm.Portfolio, algorithm.Settings.MinimumOrderMarginPortfolioPercentage))
                        {
                            var currentQty = algorithm.Portfolio[security.Symbol].Quantity;
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
                            var buyingPower = security.BuyingPowerModel.GetBuyingPower(
                                        new BuyingPowerParameters(
                                                algorithm.Portfolio, security, quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell));
                            var marginRequired = security.BuyingPowerModel.GetInitialMarginRequirement(security, Math.Abs(quantity));

                            if (buyingPower.Value >= marginRequired)
                            {
                                if (intent == OrderIntent.BTO || intent == OrderIntent.STO)
                                    quantity = RoundToLotSize(security, quantity);
                                if (intent == OrderIntent.STC || intent == OrderIntent.BTC || Math.Abs(quantity) >= MinimumQuantity)
                                    algorithm.MarketOrder(security, quantity, tag:"ImmediateExecutionModel", orderProperties: orderProps);
                            }
                            else
                            {
                                // try reducing the order quantity by using the available margin + 10%
                                var old_quantity = quantity;
                                quantity = Orders.OrderSizing.GetOrderSizeForMaximumValue(security, buyingPower.Value * .9m, quantity);
                                quantity = RoundToLotSize(security, quantity);
                                if (intent == OrderIntent.STC || intent == OrderIntent.BTC || Math.Abs(quantity) >= MinimumQuantity)
                                {
                                    if (algorithm.DebugMode)
                                        algorithm.Error($"{algorithm.Time.ToString(_datefmt)} ImmediateExecutionModel: Buying Power:${buyingPower.Value:F0} insufficient for Qty:{old_quantity} {security.Symbol}. Adjusted to:{quantity}");
                                    if (quantity != 0)
                                        algorithm.MarketOrder(security, quantity, tag: $"ImmediateExecutionModel: Size adjusted from {old_quantity} to {quantity}", orderProperties: orderProps);
                                }
                            }
                        }
                        else if (!PortfolioTarget.MinimumOrderMarginPercentageWarningSent.HasValue)
                        {
                            // will trigger the warning if it has not already been sent
                            PortfolioTarget.MinimumOrderMarginPercentageWarningSent = false;
                        }
                    }
                }

                _targetsCollection.ClearFulfilled(algorithm);
            }
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
        }

        private decimal RoundToLotSize(Security security, decimal quantity)
        {
            if (LotSize <= 1.0m)
                return quantity;

            // Round quantity based on security type
            switch (security.Symbol.SecurityType)
            {
                case SecurityType.Equity:
                    // round quantity to nearest smaller LotSize
                    if (LotSize > 1 && Math.Abs(quantity) > LotSize)
                        quantity = Math.Round(quantity / LotSize, 0) * LotSize;
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

    }
}
