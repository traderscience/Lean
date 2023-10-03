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

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Contains additional properties and settings for an order
    /// </summary>
    public interface IOrderProperties
    {
        /// <summary>
        /// Defines the length of time over which an order will continue working before it is cancelled
        /// </summary>
        TimeInForce TimeInForce { get; set; }

        /// <summary>
        /// Market Price for security (submitted by client)
        /// </summary>
        decimal MarketPrice { get; set; }

        /// <summary>
        /// Parameter list for Broker Algo order
        /// </summary>
        string AlgoJsonParameters { get; set; }

        /// <summary>
        /// IgnoreMissingPrices
        /// </summary>
        bool IgnoreMissingPrices { get; set; }

        /// <summary>
        /// Parent order (if any)
        /// </summary>
        long ParentOrder { get; set; }

        /// <summary>
        /// Parent Id for Broker Order
        /// </summary>
        long BrokerParentOrderId { get; set; }  
        /// <summary>
        /// Order Id from Brokerage
        /// </summary>
        long BrokerOrderId { get; set; }

        /// <summary>
        /// One Cancels All order group
        /// </summary>
        string OcaGroup { get; set; }
        /// <summary>
        /// Order Intent
        /// </summary>
        OrderIntent Intent { get; set; }
        /// <summary>
        /// Returns a new instance clone of this object
        /// </summary>
        IOrderProperties Clone();
    }
}
