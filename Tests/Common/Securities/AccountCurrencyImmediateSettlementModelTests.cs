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
using NUnit.Framework;
using QuantConnect.Securities;
using QuantConnect.Tests.Engine.DataFeeds;

namespace QuantConnect.Tests.Common.Securities
{
    [TestFixture]
    public class AccountCurrencyImmediateSettlementModelTests
    {
        private static readonly DateTime Noon = new DateTime(2014, 6, 24, 12, 0, 0);

        [Test]
        public void FundsAreSettledImmediately()
        {
            var algorithm = new AlgorithmStub();
            algorithm.SetDateTime(Noon);
            var portfolio = algorithm.Portfolio;
            var security = algorithm.AddSecurity(Symbols.SPY);
            var model = new AccountCurrencyImmediateSettlementModel();

            portfolio.SetCash(1000);
            Assert.AreEqual(1000, portfolio.Cash);
            Assert.AreEqual(0, portfolio.UnsettledCash);

            var timeUtc = Noon.ConvertToUtc(TimeZones.NewYork);
            model.ApplyFunds(new ApplyFundsSettlementModelParameters(portfolio, security, timeUtc, new CashAmount(1000, Currencies.USD), null));

            Assert.AreEqual(2000, portfolio.Cash);
            Assert.AreEqual(0, portfolio.UnsettledCash);

            model.ApplyFunds(new ApplyFundsSettlementModelParameters(portfolio, security, timeUtc, new CashAmount(-500, Currencies.USD), null));

            Assert.AreEqual(1500, portfolio.Cash);
            Assert.AreEqual(0, portfolio.UnsettledCash);

            model.ApplyFunds(new ApplyFundsSettlementModelParameters(portfolio, security, timeUtc, new CashAmount(1000, Currencies.USD), null));

            Assert.AreEqual(2500, portfolio.Cash);
            Assert.AreEqual(0, portfolio.UnsettledCash);
        }

        [Test]
        public void FundsAreSettledInAccountCurrency()
        {
            var algorithm = new AlgorithmStub();
            algorithm.SetDateTime(Noon);
            var portfolio = algorithm.Portfolio;
            var security = algorithm.AddSecurity(Symbols.DE30EUR);
            var model = new AccountCurrencyImmediateSettlementModel();
            portfolio.SetCash(1000);
            portfolio.SetCash("EUR", 0, 1.1m);

            Assert.AreEqual(1000, portfolio.Cash);
            Assert.AreEqual(0, portfolio.UnsettledCash);

            var timeUtc = Noon.ConvertToUtc(TimeZones.NewYork);
            model.ApplyFunds(new ApplyFundsSettlementModelParameters(portfolio, security, timeUtc, new CashAmount(1000, Currencies.EUR), null));

            // 1000 + 1000 * 1.1 = 2100
            Assert.AreEqual(2100, portfolio.Cash);
            Assert.AreEqual(0, portfolio.UnsettledCash);

            model.ApplyFunds(new ApplyFundsSettlementModelParameters(portfolio, security, timeUtc, new CashAmount(-500, Currencies.EUR), null));

            // 2100 - 500 * 1.1 = 1550
            Assert.AreEqual(1550, portfolio.Cash);
            Assert.AreEqual(0, portfolio.UnsettledCash);

            model.ApplyFunds(new ApplyFundsSettlementModelParameters(portfolio, security, timeUtc, new CashAmount(1000, Currencies.EUR), null));

            // 1550 + 1000 * 1.1 = 2650
            Assert.AreEqual(2650, portfolio.Cash);
            Assert.AreEqual(0, portfolio.UnsettledCash);
        }
    }
}
