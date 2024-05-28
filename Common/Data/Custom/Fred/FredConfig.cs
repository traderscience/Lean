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
using QuantConnect.Util;

namespace QuantConnect.Data.Custom.Fred
{
    /// <summary>
    ///     Auxiliary class to access all Intrinio API data.
    /// </summary>
    public static class FredConfig
    {
        /// <summary>
        /// </summary>
        public static RateGate RateGate =
            new RateGate(1, TimeSpan.FromMilliseconds(5000));

        /// <summary>
        ///     Check if Intrinio API user and password are not empty or null.
        /// </summary>
        public static bool IsInitialized => !string.IsNullOrWhiteSpace(ApiKey);

        /// <summary>
        ///     FRED API Key
        /// </summary>
        public static string ApiKey = string.Empty;

        /// <summary>
        /// Sets the time interval between calls.
        /// For more information, please refer to: https://stlouisfed.org/documentation/api#limits
        /// </summary>
        /// <param name="timeSpan">Time interval between to consecutive calls.</param>
        /// <remarks>
        /// Paid subscription has limits of 1 call per second.
        /// Free subscription has limits of 1 call per minute.
        /// </remarks>
        public static void SetTimeIntervalBetweenCalls(TimeSpan timeSpan)
        {
            RateGate = new RateGate(100, timeSpan);
        }

        /// <summary>
        ///     Set the FRED API Key
        /// </summary>
        public static void SetApiKey(string apiKey)
        {
            ApiKey = apiKey;

            if (!IsInitialized)
            {
                throw new InvalidOperationException("Please set a valid FRED Api Key");
            }
        }
    }
}
