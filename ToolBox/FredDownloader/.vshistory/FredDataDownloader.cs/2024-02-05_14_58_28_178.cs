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
using Accord;
using MathNet.Numerics.LinearAlgebra.Factorization;
using Quandl.NET;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;

namespace QuantConnect.ToolBox.Fred
{
    /// <summary>
    /// Quandl Data Downloader class
    /// </summary>
    public class FredDataDownloader : IDataDownloader
    {
        private string _apiKey;
        public FredDataDownloader(string apiKey)
        {
            _apiKey = apiKey;
        }

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

            string[] symParts = symbol.Value.Split('/');
            if (symParts.Length != 2)
            {
                Log.Error($"Invalid symbol: {symbol.Value} is missing dataset prefix, eg. FRED/GDPPOT");
                return Enumerable.Empty<BaseData>();
            }

            string datasetCode = symParts[0];
            string targetSymbol = symParts[1];


            switch (resolution)
            {
                case Resolution.Daily:
                case Resolution.Weekly:
                case Resolution.Monthly:
                    break;
                default:
                    return Enumerable.Empty<BaseData>();
            }

            if (symbol.ID.SecurityType != SecurityType.Auxiliary)
            {
                Log.Error("SecurityType not available: " + symbol.ID.SecurityType);
                return Enumerable.Empty<BaseData>();
            }

            if (endUtc < startUtc)
            {
                Log.Error("The end date must be greater or equal than the start date.");
                return Enumerable.Empty<BaseData>();
            }
            // parse symbol to get the datasetCode
            return GetEnumerator(targetSymbol, datasetCode, startUtc, endUtc, _apiKey);
        }

        public IEnumerable<string> GetColumnNames()
        {
            return null;
        }

        private static IEnumerable<BaseData> GetEnumerator(string symbol, string datasetCode, DateTime start, DateTime end, string apiKey)
        {
            IEnumerable<BaseData> data = new List<AuxiliaryData>();
            List<object[]> rawData = new List<object[]>();
            List<string> columnNames;
            int dateIndex = 0;
            string frequency = null;
            Transform? transform = Transform.None;

            try
            {
                var client = new FredRestClient(apiKey);
                var result = client.Timeseries.GetDataAsync(datasetCode, symbol, null, null, start, end, Order.Ascending).Result;
                if (result != null && result.DatasetData.Data.Any())
                {
                    columnNames = result.DatasetData.ColumnNames;
                    frequency = result.DatasetData.Frequency;
                    transform = result.DatasetData.Transform;   
                    rawData = result.DatasetData.Data;
                    dateIndex = columnNames.IndexOf("Date");
                    if (dateIndex < 0)
                        dateIndex = 0;
                }   

            }
            catch (Exception ex)
            {
                Log.Error($"FredDataDownloader:GetEnumerator: exception: {ex.Message}");
                yield break;
            }

            foreach (var item in rawData)
            {
                var dataPoint = ParseAuxiliaryData(item, dateIndex);
                yield return dataPoint;
            }
        }

        private static AuxiliaryData ParseAuxiliaryData(object[] data, int dateIndex)
        {
            IFormatProvider formatProvider = System.Globalization.CultureInfo.InvariantCulture;
            var date = DateTime.Parse((string)data[dateIndex], formatProvider);

            return new AuxiliaryData(date, data.Skip(1).Cast<double>());
        }
    }
}
