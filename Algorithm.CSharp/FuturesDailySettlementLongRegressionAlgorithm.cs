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
 *
*/

using System;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using System.Collections.Generic;
using QuantConnect.Securities.Future;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Regression algorithm asserting the futures daily cash settlement behavior taking long positions
    /// </summary>
    public class FuturesDailySettlementLongRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private decimal _initialPortfolioValue;
        private int _lastTradedDay;
        private Symbol _contractSymbol;
        private Future _future;

        /// <summary>
        /// Expected cash balance for each day
        /// </summary>
        protected virtual Dictionary<DateTime, decimal> ExpectedCash { get; } = new()
        {
            { new DateTime(2013, 10, 07), 100000 },
            { new DateTime(2013, 10, 08), 103264.45m },
            { new DateTime(2013, 10, 09), 101231.05m },
            { new DateTime(2013, 10, 10), 101962.10m },
            { new DateTime(2013, 10, 10, 17, 0, 0), 100905.65m }
        };

        /// <summary>
        /// Order side factor
        /// </summary>
        protected virtual int OrderSide => 1;

        /// <summary>
        /// Initialize your algorithm and add desired assets.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);
            SetEndDate(2013, 10, 10);

            var future = QuantConnect.Symbol.Create(Futures.Indices.SP500EMini, SecurityType.Future, Market.CME);

            _contractSymbol = FutureChainProvider.GetFutureContractList(future, Time).OrderBy(x => x.ID.Date).FirstOrDefault();
            _future = AddFutureContract(_contractSymbol);

            _future.Holdings.SetHoldings(1600, 1 * OrderSide);
        }

        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {
            AssertCash(Time.Date);

            if (Transactions.OrdersCount == 0)
            {
                // initial trade
                _initialPortfolioValue = Portfolio.TotalPortfolioValue - _future.Holdings.UnrealizedProfit;
                MarketOrder(_contractSymbol, 1 * OrderSide);
            }
            else if(Time.Day == 7 && _lastTradedDay != Time.Day)
            {
                _lastTradedDay = Time.Day;
                // increase position
                MarketOrder(_contractSymbol, 1 * OrderSide);
            }
            else if (Time.Day == 8 && _lastTradedDay != Time.Day)
            {
                _lastTradedDay = Time.Day;
                // reduce position
                MarketOrder(_contractSymbol, -1 * OrderSide);
            }
            else if (Time.Day == 9 && _lastTradedDay != Time.Day)
            {
                _lastTradedDay = Time.Day;
                // cross position
                MarketOrder(_contractSymbol, -3 * OrderSide);
            }
            else if (Time.Day == 10)
            {
                if(_lastTradedDay != Time.Day)
                {
                    _lastTradedDay = Time.Day;
                    // increase position
                    MarketOrder(_contractSymbol, -1 * OrderSide);
                }
                else
                {
                    // finally liquidate
                    Liquidate();
                }
            }
        }

        private void AssertCash(DateTime currentTime)
        {
            if (ExpectedCash.Remove(currentTime, out var expected))
            {
                var value = Portfolio.CashBook.TotalValueInAccountCurrency;
                if (expected != value)
                {
                    throw new Exception($"Unexpected cash balance {value} expected {expected}");
                }
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status.IsFill())
            {
                Debug($"{orderEvent}");
            }
        }

        public override void OnEndOfAlgorithm()
        {
            var holdings = (FutureHolding)_future.Holdings;
            Debug($"{Environment.NewLine}InitialPortfolioValue: {_initialPortfolioValue}. CurrentPortfolioValue: {Portfolio.TotalPortfolioValue}" +
                $"{Environment.NewLine}Profit: {holdings.Profit}" +
                $"{Environment.NewLine}Fees: {holdings.TotalFees}" +
                $"{Environment.NewLine}CashBook:{Environment.NewLine}{Portfolio.CashBook}" +
                $"{Environment.NewLine}UnsettledCashBook:{Environment.NewLine}{Portfolio.UnsettledCashBook}");

            var expected = Math.Round(_initialPortfolioValue + holdings.NetProfit, 5);
            if (expected != Portfolio.TotalPortfolioValue || expected != Portfolio.CashBook[Currencies.USD].Amount)
            {
                throw new Exception($"Unexpected future profit {holdings.NetProfit}");
            }
            if(holdings.SettledProfit != 0)
            {
                throw new Exception($"Unexpected SettledProfit value {holdings.SettledProfit}");
            }
            if (holdings.UnrealizedProfit != 0)
            {
                throw new Exception($"Unexpected UnrealizedProfit value {holdings.UnrealizedProfit}");
            }

            AssertCash(Time);
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
        public long DataPoints => 5444;

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        public int AlgorithmHistoryDataPoints => 0;

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public virtual Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "6"},
            {"Average Win", "0.89%"},
            {"Average Loss", "-0.87%"},
            {"Compounding Annual Return", "142.879%"},
            {"Drawdown", "3.800%"},
            {"Expectancy", "0.349"},
            {"Net Profit", "0.906%"},
            {"Sharpe Ratio", "-3.934"},
            {"Probabilistic Sharpe Ratio", "0%"},
            {"Loss Rate", "33%"},
            {"Win Rate", "67%"},
            {"Profit-Loss Ratio", "1.02"},
            {"Alpha", "-1.084"},
            {"Beta", "0.151"},
            {"Annual Standard Deviation", "0.216"},
            {"Annual Variance", "0.047"},
            {"Information Ratio", "-7.634"},
            {"Tracking Error", "0.313"},
            {"Treynor Ratio", "-5.625"},
            {"Total Fees", "$19.35"},
            {"Estimated Strategy Capacity", "$100000000.00"},
            {"Lowest Capacity Asset", "ES VMKLFZIH2MTD"},
            {"Portfolio Turnover", "183.82%"},
            {"OrderListHash", "f47b76908bc78f13100008a6d13560fc"}
        };
    }
}
