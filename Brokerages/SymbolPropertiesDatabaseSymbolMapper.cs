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
using System.Linq;
using QuantConnect.Securities;
using QuantConnect.Logging;
using System.Collections.Generic;
//using System.Runtime.Remoting.Messaging;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Provides the mapping between Lean symbols and brokerage symbols using the symbol properties database
    /// </summary>
    public class SymbolPropertiesDatabaseSymbolMapper : ISymbolMapper
    {
        private readonly string _market;

        // map Lean symbols to symbol properties
        private readonly Dictionary<Symbol, SymbolProperties> _symbolPropertiesMap;

        // map brokerage symbols to Lean symbols we do it per security type because they could overlap, for example binance futures and spot
        private readonly Dictionary<SecurityType, Dictionary<string, Symbol>> _symbolMap;

        /// <summary>
        /// Creates a new instance of the <see cref="SymbolPropertiesDatabaseSymbolMapper"/> class.
        /// </summary>
        /// <param name="market">The Lean market</param>
        public SymbolPropertiesDatabaseSymbolMapper(string market)
        {
            _market = market;

            var symbolPropertiesList =
                SymbolPropertiesDatabase
                    .FromDataFolder()
                    .GetSymbolPropertiesList(_market)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Value.MarketTicker))
                    .ToList();

            _symbolPropertiesMap =
                symbolPropertiesList
                    .ToDictionary(
                        x => Symbol.Create(x.Key.Symbol, x.Key.SecurityType, x.Key.Market),
                        x => x.Value);

            _symbolMap = new();
            foreach (var group in _symbolPropertiesMap.GroupBy(x => x.Key.SecurityType))
            {
                _symbolMap[group.Key] = group.ToDictionary(
                            x => x.Value.MarketTicker,
                            x => x.Key);
            }
        }

        /// <summary>
        /// Converts a Lean symbol instance to a brokerage symbol
        /// </summary>
        /// <param name="symbol">A Lean symbol instance</param>
        /// <returns>The brokerage symbol</returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null || string.IsNullOrWhiteSpace(symbol.Value))
            {
                Log.Error($"GetBrokerageSymbol: Invalid symbol: {(symbol == null ? "null" : symbol.Value)}");
                return null;
            }

            if (symbol.ID.Market != _market)
            {
                Log.Error($"GetBrokerageSymbol: Invalid market: {symbol.ID.Market}");
                return null;
            }

            SymbolProperties symbolProperties;
            if (!_symbolPropertiesMap.TryGetValue(symbol, out symbolProperties) )
            {
                Log.Error($"Unknown symbol: {symbol.Value}/{symbol.SecurityType}/{symbol.ID.Market}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(symbolProperties.MarketTicker))
            {
                throw new ArgumentException($"MarketTicker not found in database for symbol: {symbol.Value}");
            }

            return symbolProperties.MarketTicker;
        }

        /// <summary>
        /// Converts a brokerage symbol to a Lean symbol instance
        /// </summary>
        /// <param name="brokerageSymbol">The brokerage symbol</param>
        /// <param name="securityType">The security type</param>
        /// <param name="market">The market</param>
        /// <param name="expirationDate">Expiration date of the security(if applicable)</param>
        /// <param name="strike">The strike of the security (if applicable)</param>
        /// <param name="optionRight">The option right of the security (if applicable)</param>
        /// <returns>A new Lean Symbol instance</returns>
        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, DateTime expirationDate = default(DateTime), decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
            {
                throw new ArgumentException($"Invalid brokerage symbol: {brokerageSymbol}");
            }

            if (market != _market)
            {
                throw new ArgumentException($"Invalid market: {market}");
            }

            if (!_symbolMap.TryGetValue(securityType, out var symbols))
            {
                throw new ArgumentException($"Unknown brokerage security type: {securityType}");
            }

            if (!symbols.TryGetValue(brokerageSymbol, out var symbol))
            {
                throw new ArgumentException($"Unknown brokerage symbol: {brokerageSymbol}");
            }

            return symbol;
        }

        /// <summary>
        /// Checks if the Lean symbol is supported by the brokerage
        /// </summary>
        /// <param name="symbol">The Lean symbol</param>
        /// <returns>True if the brokerage supports the symbol</returns>
        public bool IsKnownLeanSymbol(Symbol symbol)
        {
            return !string.IsNullOrWhiteSpace(symbol?.Value) && _symbolPropertiesMap.ContainsKey(symbol);
        }

        /// <summary>
        /// Returns the security type for a brokerage symbol
        /// </summary>
        /// <param name="brokerageSymbol">The brokerage symbol</param>
        /// <returns>The security type</returns>
        public SecurityType GetBrokerageSecurityType(string brokerageSymbol)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
            {
                throw new ArgumentException($"Invalid brokerage symbol: {brokerageSymbol}");
            }

            var result = _symbolMap.Select(kvp =>
            {
                kvp.Value.TryGetValue(brokerageSymbol, out var symbol);
                return symbol;
            }).Where(symbol => symbol != null).ToList();

            if (result.Count == 0)
            {
                throw new ArgumentException($"Unknown brokerage symbol: {brokerageSymbol}");
            }
            if (result.Count > 1)
            {
                throw new ArgumentException($"Found multiple brokerage symbols: {string.Join(",", result)}");
            }

            return result[0].SecurityType;
        }

        /// <summary>
        /// Checks if the symbol is supported by the brokerage
        /// </summary>
        /// <param name="brokerageSymbol">The brokerage symbol</param>
        /// <returns>True if the brokerage supports the symbol</returns>
        public bool IsKnownBrokerageSymbol(string brokerageSymbol)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
            {
                return false;
            }

            return _symbolMap.Any(kvp => kvp.Value.ContainsKey(brokerageSymbol));
        }
    }
}
