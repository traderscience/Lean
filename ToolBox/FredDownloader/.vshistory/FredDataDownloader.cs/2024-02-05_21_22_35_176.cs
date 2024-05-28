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
using Xaye.Fred;

namespace QuantConnect.ToolBox.FredDownloader
{
    /// <summary>
    /// Quandl Data Downloader class
    /// </summary>
    public class FredDataDownloader : IDataDownloader, IDisposable
    {
        private string _apiKey;
        private Xaye.Fred.Fred _client;
        private bool disposedValue;

        public FredDataDownloader(string apiKey)
        {
            _apiKey = apiKey;
            _client = new Xaye.Fred.Fred(apiKey, true);
        }

        /// <summary>
        /// Get historical data enumerable for a single symbol, type and resolution given this start and end time (in UTC).
        /// </summary>
        /// <param name="dataDownloaderGetParameters">model class for passing in parameters for historical data</param>
        /// <returns>Enumerable of base data for this symbol</returns>
        public IEnumerable<BaseData> Get(DataDownloaderGetParameters dataDownloaderGetParameters)
        {
            if (dataDownloaderGetParameters == null)
                return null;
            var symbol = dataDownloaderGetParameters.Symbol;
            string seriesCode = symbol.Value;
            var resolution = dataDownloaderGetParameters.Resolution;
            var startUtc = dataDownloaderGetParameters.StartUtc;
            var endUtc = dataDownloaderGetParameters.EndUtc;
            var tickType = dataDownloaderGetParameters.TickType;

            switch (resolution)
            {
                case Resolution.Daily:
                case Resolution.Weekly:
                case Resolution.Monthly:
                case Resolution.Quarterly:
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
            return GetEnumerator(seriesCode, startUtc, endUtc, _apiKey);
        }

        public IEnumerable<string> GetColumnNames()
        {
            return null;
        }

        private static IEnumerable<BaseData> GetEnumerator(string seriesCode, DateTime start, DateTime end, string apiKey)
        {
            IEnumerable<BaseData> data = new List<AuxiliaryData>();
            int dateIndex = 0;
            Frequency? frequency = null;
            Xaye.Fred.Series rawData = null;

            try
            {
                var client = new Xaye.Fred.Fred(apiKey, true);
                rawData = client.GetSeries(seriesCode, start, end);
                if (rawData != null && rawData.Any())
                {
                    frequency = rawData.Frequency;
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

        private static AuxiliaryData ParseAuxiliaryData(Observation data, int dateIndex)
        {
            return new AuxiliaryData(data.Date, new double[] { data.Value.Value });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~FredDataDownloader()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
