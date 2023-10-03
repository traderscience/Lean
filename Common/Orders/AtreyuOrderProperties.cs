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

using QuantConnect.Interfaces;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Contains additional properties and settings for an order submitted to Atreyu brokerage
    /// </summary>
    public class AtreyuOrderProperties : IOrderProperties
    {
        /// <summary>
        /// Defines the length of time over which an order will continue working before it is cancelled
        /// </summary>
        public TimeInForce TimeInForce { get; set; }

        /// <summary>
        /// This flag will ensure the order add liquidity only
        /// </summary>
        public bool PostOnly { get; set; }

        /// <summary>
        /// Market Price for security (submitted by client)
        /// </summary>
        public decimal MarketPrice { get; set; }

        /// <summary>
        /// Parameter list for Broker Algo order
        /// </summary>
        public string AlgoJsonParameters { get; set; }

        /// <summary>
        /// IgnoreMissingPrices
        /// </summary>
        public bool IgnoreMissingPrices { get; set; }

        /// <summary>
        /// Parent order (if any)
        /// </summary>
        public long ParentOrder { get; set; }


        /// <summary>
        /// Parent Order at Broker
        /// </summary>
        public long BrokerParentOrderId { get; set; }

        /// <summary>
        /// Broker Order Id
        /// </summary>
        public long BrokerOrderId { get; set; }
        /// <summary>
        /// One Cancels All order group
        /// </summary>
        public string OcaGroup { get; set; }
        /// <summary>
        /// Order Intent
        /// </summary>
        public OrderIntent Intent { get; set; }
        /// <summary>
        /// Returns a new instance clone of this object
        /// </summary>

        /// <summary>
        /// Creates a new instance and sets <see cref="TimeInForce"/> to Day
        /// </summary>
        public AtreyuOrderProperties()
        {
            TimeInForce = TimeInForce.Day;
        }

        /// <summary>
        /// Returns a new instance clone of this object
        /// </summary>
        public IOrderProperties Clone()
        {
            return (AtreyuOrderProperties)MemberwiseClone();
        }
    }
}
