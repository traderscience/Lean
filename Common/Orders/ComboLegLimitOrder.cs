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
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Combo leg limit order type
    /// </summary>
    /// <remarks>Limit price per leg in the combo order</remarks>
    public class ComboLegLimitOrder : LimitOrder
    {
        /// <summary>
        /// Combo Limit Leg Order Type
        /// </summary>
        public override OrderType Type => OrderType.ComboLegLimit;

        /// <summary>
        /// Added a default constructor for JSON Deserialization:
        /// </summary>
        public ComboLegLimitOrder() : base()
        {
        }

        /// <summary>
        /// New limit order constructor
        /// </summary>
        /// <param name="symbol">Symbol asset we're seeking to trade</param>
        /// <param name="quantity">Quantity of the asset we're seeking to trade</param>
        /// <param name="time">Time the order was placed</param>
        /// <param name="groupOrderManager">Manager for the orders in the group</param>
        /// <param name="limitPrice">Price the order should be filled at if a limit order</param>
        /// <param name="tag">User defined data tag for this order</param>
        /// <param name="properties">The order properties for this order</param>
        public ComboLegLimitOrder(Symbol symbol, decimal quantity, decimal limitPrice, DateTime time, GroupOrderManager groupOrderManager,
            string tag = "", IOrderProperties properties = null)
            : base(symbol, quantity, limitPrice, time, tag, properties)
        {
            GroupOrderManager = groupOrderManager;
        }

        /// <summary>
        /// Gets the order value in units of the security's quote currency
        /// </summary>
        /// <param name="security">The security matching this order's symbol</param>
        protected override decimal GetValueImpl(Security security)
        {
            return base.GetValueImpl(security) * GroupOrderManager.Quantity;
        }

        /// <summary>
        /// Creates a deep-copy clone of this order
        /// </summary>
        /// <returns>A copy of this order</returns>
        public override Order Clone()
        {
            var order = new ComboLegLimitOrder { LimitPrice = LimitPrice };
            CopyTo(order);
            return order;
        }
    }
}
