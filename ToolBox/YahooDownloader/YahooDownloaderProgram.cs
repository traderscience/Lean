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
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.ToolBox.YahooDownloader
{
    public static class YahooDownloaderProgram
    {
        /// <summary>
        /// Yahoo Downloader Toolbox Project For LEAN Algorithmic Trading Engine.
        /// Original by @chrisdk2015, tidied by @jaredbroad
        /// </summary>
        public static void YahooDownloader(IList<string> tickers, string resolution, DateTime startDate, DateTime endDate)
        {
            if (resolution.IsNullOrEmpty() || tickers.IsNullOrEmpty())
            {
                Console.WriteLine("YahooDownloader ERROR: '--tickers=' or '--resolution=' parameter is missing");
                Console.WriteLine("--tickers=eg SPY,AAPL");
                Console.WriteLine("--resolution=Daily");
                Environment.Exit(1);
            }
            try
            {
                // Load settings from command line
                var castResolution = (Resolution)Enum.Parse(typeof(Resolution), resolution);

                // Load settings from config.json
                var dataDirectory = Config.Get("data-folder", "../../../Data");

                // Create an instance of the downloader
                const string market = Market.USA;
                var downloader = new YahooDataDownloader();

                DateTime splitStart = new DateTime(1980, 1, 1);  // always request the entire split/dividend history
                DateTime splitEnd = DateTime.Today.AddDays(1);

                foreach (var ticker in tickers)
                {
                    try
                    {
                        // Download the data
                        var symbolObject = Symbol.Create(ticker, SecurityType.Equity, market);
                        var data = downloader.Get(new DataDownloaderGetParameters(symbolObject, castResolution, startDate, endDate));

                        // Save the data
                        var writer = new LeanDataWriter(castResolution, symbolObject, dataDirectory);
                        writer.Write(data);

                        // Get the entire split and dividend history
                        var splitDivData = downloader.DownloadSplitAndDividendData(symbolObject, splitStart, splitEnd);
                        // Create/overwrite existing factor files for this ticker
                        string dataDir = $"{dataDirectory}\\Equity\\{symbolObject.ID.Market}\\daily\\{symbolObject.Value}.zip";
                        var factorGen = new FactorFileGenerator(symbolObject, dataDir);
                        factorGen.CreateFactorFile(splitDivData.ToList()).WriteToFile(symbolObject);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"YahooDownloaderProgram: ticker {ticker} error {ex.ToString()}");
                    }
                }
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }
    }
}
