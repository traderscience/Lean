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
using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.ToolBox.YahooDownloader
{
    /// <summary>
    /// Yahoo Data Downloader class
    /// </summary>
    public class YahooDataDownloader : IDataDownloader
    {
        /// <summary>
        /// Get historical data enumerable for a single symbol, type and resolution given this start and end time (in UTC).
        /// </summary>
        /// <param name="dataDownloaderGetParameters">model class for passing in parameters for historical data</param>
        /// <returns>Enumerable of base data for this symbol</returns>
        public IEnumerable<BaseData> Get(DataDownloaderGetParameters dataDownloaderGetParameters)
        {
            var symbol = dataDownloaderGetParameters.Symbol;
            var resolution = dataDownloaderGetParameters.Resolution;
            var startUtc = dataDownloaderGetParameters.StartUtc;
            var endUtc = dataDownloaderGetParameters.EndUtc;
            var tickType = dataDownloaderGetParameters.TickType;

            if (tickType != TickType.Trade)
            {
                return Enumerable.Empty<BaseData>();
            }

            if (resolution != Resolution.Daily)
            {
                throw new ArgumentException("The YahooDataDownloader can only download daily data.");
            }

            if (symbol.ID.SecurityType != SecurityType.Equity)
            {
                throw new NotSupportedException("SecurityType not available: " + symbol.ID.SecurityType);
            }

            if (endUtc < startUtc)
            {
                throw new ArgumentException("The end date must be greater or equal than the start date.");
            }

            return GetEnumerator(symbol, startUtc, endUtc);
        }

        private static IEnumerable<BaseData> GetEnumerator(Symbol symbol, DateTime startDateTime, DateTime endDateTime)
        {
            var data = Historical.Get(symbol.Value, startDateTime, endDateTime, "history");

            foreach (var item in data)
            {
                yield return new TradeBar(item.Date, symbol, item.Open, item.High, item.Low, item.Close, (long)item.Volume, TimeSpan.FromDays(1));
            }
        }


        /// <summary>
        /// Download Dividend and Split data from Yahoo
        /// </summary>
        /// <param name="symbol">Symbol of the data to download</param>
        /// <param name="startUtc">Get data after this time</param>
        /// <param name="endUtc">Get data before this time</param>
        /// <returns></returns>
        public Queue<BaseData> DownloadSplitAndDividendData(Symbol symbol, DateTime startUtc, DateTime endUtc)
        {
            var split = Historical.GetRaw(symbol.Value, startUtc, endUtc, "split");
            var dividend = Historical.GetRaw(symbol.Value, startUtc, endUtc, "dividend");
            var parsed = new List<BaseData>();

            foreach (var data in new[] { split, dividend })
            {

                bool isSplit = false;

                foreach (var item in data.Split('\n'))
                {
                    if (String.IsNullOrEmpty(item))
                    {
                        break;
                    }
                    if (item == "Date,Stock Splits")
                    {
                        isSplit = true;
                        continue;
                    }
                    if (item == "Date,Dividends")
                    {
                        continue;
                    }

                    string[] values = item.Split(',');
                    decimal value = 1.0m;

                    if (isSplit)
                    {
                        var time = Parse.DateTimeExact(values[0].Replace("-", string.Empty), DateFormat.EightCharacter);
                        var splitFactor = ParseAmount(values[1]);
                        value = splitFactor;
                        parsed.Add(new Split { Time = time, Value = splitFactor });
                    }
                    else
                    {
                        var time = Parse.DateTimeExact(values[0].Replace("-", string.Empty), DateFormat.EightCharacter);
                        var distribution = Decimal.Parse(values[1]);
                        parsed.Add(new Dividend { Time = time, Value = distribution});
                    }
                }
            }

            return new Queue<BaseData>(parsed.OrderByDescending(x => x.Time));
        }

        /// <summary>
        /// Put the split ratio into a decimal format
        /// </summary>
        /// <param name="splitFactor">Split ratio</param>
        /// <returns>Decimal representing the split ratio</returns>
        private decimal ParseAmount(string splitFactor)
        {
            var factors = splitFactor.Split(':');
            return Parse.Decimal(factors[0]) / Parse.Decimal(factors[1]);
        }

        public IEnumerable<string> GetColumnNames()
        {
            return null;
        }
    }
}
