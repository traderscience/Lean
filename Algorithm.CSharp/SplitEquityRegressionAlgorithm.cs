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

using QuantConnect.Data;
using QuantConnect.Interfaces;
using System.Collections.Generic;
using QuantConnect.Orders;
using QuantConnect.Util;
using System;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Simple regression algorithm asserting certain order fields update properly when a
    /// split in the data happens
    /// </summary>
    public class SplitEquityRegressionAlgorithm: QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _aapl;
        private List<OrderTicket> _tickets = new();

        public override void Initialize()
        {
            SetStartDate(2014, 6, 5);
            SetEndDate(2014, 6, 11);
            SetCash(100000);

            _aapl = AddEquity("AAPL", Resolution.Hour, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
        }

        public override void OnData(Slice slice)
        {
            if (Transactions.GetOrders().IsNullOrEmpty())
            {
                _tickets.Add(LimitIfTouchedOrder(_aapl, 10, 10, 10));
                _tickets.Add(LimitOrder(_aapl, 10, 5));
                _tickets.Add(StopLimitOrder(_aapl, 10, 15, 15));
            }
        }

        public override void OnEndOfAlgorithm()
        {
            foreach (var ticket in _tickets)
            {
                if (ticket.Quantity != 69.0m)
                {
                    throw new Exception($"The Quantity of order with ID: {ticket.OrderId} should be 69, but was {ticket.Quantity}");
                }
                switch (ticket.OrderType)
                {
                    case OrderType.LimitIfTouched:
                        if (ticket.Get(OrderField.TriggerPrice) != 1.43m)
                        {
                            throw new Exception($"Order with ID: {ticket.OrderId} should have a Trigger Price equal to 1.43, but was {ticket.Get(OrderField.TriggerPrice)}");
                        }

                        if (ticket.Get(OrderField.LimitPrice) != 1.43m)
                        {
                            throw new Exception($"Order with ID: {ticket.OrderId} should have a Limit Price equal to 1.43, but was {ticket.Get(OrderField.LimitPrice)}");
                        }
                        break;
                    case OrderType.Limit:
                        if (ticket.Get(OrderField.LimitPrice) != 0.7143m)
                        {
                            throw new Exception($"Order with ID: {ticket.OrderId} should have a Limit Price equal to 0.7143, but was {ticket.Get(OrderField.LimitPrice)}");
                        }
                        break;
                    case OrderType.StopLimit:
                        if (ticket.Get(OrderField.StopPrice) != 2.14m)
                        {
                            throw new Exception($"Order with ID: {ticket.OrderId} should have a Stop Price equal to 2.14, but was {ticket.Get(OrderField.StopPrice)}");
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp };

        /// <summary>
        /// Data Points count of all timeslices of algorithm
        /// </summary>
        public long DataPoints => 80;

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        public int AlgorithmHistoryDataPoints => 0;

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "0"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "0%"},
            {"Drawdown", "0%"},
            {"Expectancy", "0"},
            {"Net Profit", "0%"},
            {"Sharpe Ratio", "0"},
            {"Probabilistic Sharpe Ratio", "0%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0"},
            {"Beta", "0"},
            {"Annual Standard Deviation", "0"},
            {"Annual Variance", "0"},
            {"Information Ratio", "-2.491"},
            {"Tracking Error", "0.042"},
            {"Treynor Ratio", "0"},
            {"Total Fees", "$0.00"},
            {"Estimated Strategy Capacity", "$0"},
            {"Lowest Capacity Asset", ""},
            {"Portfolio Turnover", "0%"},
            {"OrderListHash", "fb7383d38257493fe4f3427e781d9a34"}
        };
    }
}
