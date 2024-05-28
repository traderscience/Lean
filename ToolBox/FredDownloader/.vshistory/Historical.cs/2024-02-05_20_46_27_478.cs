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
using System.Diagnostics;
using System.Net;
using QuantConnect.Logging;
using Xaye.Fred;

namespace QuantConnect.ToolBox.FredDownloader
{
    /// <summary>
    /// Class for fetching alternative/economic historical data from FRED (stlouisfed.com/api)
    /// </summary>
    public static class Historical
    {
        /// <summary>
        /// Get stock historical price from Yahoo Finance
        /// </summary>
        /// <param name="symbol">Stock ticker symbol</param>
        /// <param name="start">Starting datetime</param>
        /// <param name="end">Ending datetime</param>
        /// <returns>List of history price</returns>
        public static Xaye.Fred.Series Get(string seriesCode, DateTime start, DateTime end, string eventCode, string apiKey)
        {
            try
            {
                var client = new Xaye.Fred.Fred(apiKey, true);
                var result = client.GetSeries(seriesCode, start, end);
                return result;
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
            }
            return null;
        }
    }
}
