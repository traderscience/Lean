using Newtonsoft.Json;
using QLNet;
using QuantConnect;
using QuantConnect.Api;
using QuantConnect.Brokerages;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds.Enumerators;
using QuantConnect.Logging;
using QuantConnect.ToolBox;
using QuantConnect.Util;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.ToolBox.TiingoDownloader
{
    public class TiingoDataDownloader : IDataDownloader
    {
        private readonly string _apiKey;
        private IFactorFileProvider _factorFileProvider;
        private IMapFileProvider _mapFileProvider;
        private IDataProvider _dataProvider;

        public TiingoDataDownloader(string apiKey)
        {
            _apiKey = apiKey;
            var composer = Composer.Instance;

            _mapFileProvider = new LocalDiskMapFileProvider();

            var dataProviderTypeName = Config.Get("data-provider", "DefaultDataProvider");
            _dataProvider = composer.GetExportedValueByTypeName<IDataProvider>(dataProviderTypeName);

            var factorFileProviderTypeName = Config.Get("factor-file-provider", "LocalDiskFactorFileProvider");
            _factorFileProvider = new LocalDiskFactorFileProvider();
            _factorFileProvider.Initialize(_mapFileProvider, _dataProvider);
        }

        public IEnumerable<BaseData> Get(DataDownloaderGetParameters dataDownloaderGetParameters)
        {
            // unpack parameters
            var symbol = dataDownloaderGetParameters.Symbol;
            var resolution = dataDownloaderGetParameters.Resolution;
            var startUtc = dataDownloaderGetParameters.StartUtc;
            var endUtc = dataDownloaderGetParameters.EndUtc;
            var tickType = dataDownloaderGetParameters.TickType;

            return Get(symbol, resolution, startUtc, endUtc);
        }

        public IEnumerable<BaseData> Get(Symbol symbol, Resolution resolution, DateTime startUtc, DateTime endUtc)
        {
            string interval = GetInterval(resolution);
            TimeSpan span = ResolutionSpan(resolution);
            decimal factor = 1.0m;

            using (var client = new RestClient("https://api.tiingo.com"))
            {
                client.AddDefaultHeader("Authorization", $"Token {_apiKey}");
                RestRequest request;
                if (resolution == Resolution.Daily)
                {
                    // for daily requests, Tiingo returns both the adjusted and unadjusted prices
                    // we want the unadjusted prices.
                    // We ask for the adjClose price as a doublecheck for our internal split calcuations 
                    request = new RestRequest($"tiingo/daily/{symbol.Value}/prices", Method.Get);
                    request.AddParameter("columns", "date,open,high,low,close,volume,adjClose");
                }
                else
                {
                    // for intraday prices, Tiingo is return adjusted prices only,
                    // which means we have to undo the factors to get the unadjusted prices before saving
                    request = new RestRequest($"iex/{symbol.Value}/prices", Method.Get);
                    request.AddParameter("resampleFreq", interval);
                    request.AddParameter("columns", "date,open,high,low,close,volume");
                }
                request.AddParameter("startDate", startUtc.ToString("yyyy-MM-dd"));
                request.AddParameter("endDate", endUtc.ToString("yyyy-MM-dd"));

                // send the request and save the response
                var response = client.Execute(request);

                if (response.IsSuccessful)
                {
                    if (resolution == Resolution.Daily)
                        return ParseCsvResponse(symbol, span, response.Content);
                    else
                    {
                        var adjustedBars = ParseCsvResponse(symbol, span, response.Content);
                        var factorFile = _factorFileProvider.Get(symbol);
                        decimal priceScaleFactor = 1.0m;
                        decimal sumOfDividends = 0.0m;
                        DateTime priceFrontier = startUtc;
                        DateTime nextTradableDate = DateTime.MinValue;

                        foreach (var bar in adjustedBars)
                        {
                            priceFrontier = bar.GetUpdatePriceScaleFrontier();
                            if (priceFrontier > nextTradableDate)
                            {
                                priceScaleFactor = factorFile.GetPriceScale(priceFrontier.Date, DataNormalizationMode.SplitAdjusted, contractOffset:0, dataMappingMode:null, endDateTime:endUtc);

                                // update factor files every day
                                nextTradableDate = priceFrontier.Date.AddDays(1);
                                nextTradableDate = nextTradableDate.Add(Time.LiveAuxiliaryDataOffset);
                            }
                            if (priceScaleFactor != 1.0m)
                                bar.Normalize(priceScaleFactor, DataNormalizationMode.BackwardsRatio, sumOfDividends);
                        }
                        return adjustedBars;
                    }
                }
                else
                {
                    // Handle error response
                    throw new Exception($"Error fetching data from Tiingo: {response.StatusCode}");
                }
            }
        }

        /// <summary>
        /// Gets the security type for the specified Lean symbol
        /// </summary>
        /// <param name="symbol">The Lean symbol</param>
        /// <returns>The security type</returns>
        public SecurityType GetSecurityType(string symbol)
        {
            return SecurityType.Equity;
        }



        public IEnumerable<string> GetColumnNames()
        {
            return null;
        }

        private static TimeSpan ResolutionSpan(Resolution resolution)
        {
            switch (resolution)
            {
                case Resolution.Minute:
                    return TimeSpan.FromMinutes(1);
                case Resolution.Hour:
                    return TimeSpan.FromHours(1);
                case Resolution.Daily:
                    return TimeSpan.FromDays(1);
                // Add other resolutions as needed
                default:
                    throw new ArgumentException("Unsupported resolution", nameof(resolution));
            }
        }

        private static string GetInterval(Resolution resolution)
        {
            switch (resolution)
            {
                case Resolution.Minute:
                    return "1min";
                case Resolution.Hour:
                    return "1hour";
                case Resolution.Daily:
                    return "1day";
                // Add other resolutions as needed
                default:
                    throw new ArgumentException("Unsupported resolution", nameof(resolution));
            }
        }


        /// <summary>
        /// Parse raw historical price data into list
        /// </summary>
        /// <param name="csvData"></param>
        /// <returns></returns>
        private IEnumerable<TradeBar> ParseCsvResponse(Symbol symbol, TimeSpan period, string csvData)
        {
            var hps = new List<HistoryPrice>();

            try
            {
                // deserialize the incoming json data
                var rows = JsonConvert.DeserializeObject<IEnumerable<HistoryPrice>>(csvData);
                var bars = rows.Select(x => new TradeBar(x.Date, symbol, x.Open, x.High, x.Low, x.Close, (long)x.Volume, period));
                return bars;
            }
            catch (Exception ex)
            {
                Log.Debug(ex.Message);
            }
            return null;
        }
    }

    public class HistoryPrice
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public decimal AdjClose { get; set; }
    }
}
