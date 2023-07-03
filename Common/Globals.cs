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

using System.IO;
using System.Reflection;
using QuantConnect.Configuration;

namespace QuantConnect
{
    /// <summary>
    /// Provides application level constant values
    /// </summary>
    public static class Globals
    {
        static Globals()
        {
            Reset();
        }

        /// <summary>
        /// The root directory of the data folder for this application
        /// </summary>
        public static string DataFolder { get; private set; }

        /// <summary>
        /// Resets global values with the Config data.
        /// </summary>
        public static void Reset ()
        {
            CacheDataFolder = DataFolder = Config.Get("data-folder", @"../../../Data/");

            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var versionid = Config.Get("version-id");
            if (!string.IsNullOrWhiteSpace(versionid))
            {
                Version += "." + versionid;
            }

            var cacheLocation = Config.Get("cache-location");
            if (!string.IsNullOrEmpty(cacheLocation) && !cacheLocation.IsDirectoryEmpty())
            {
                CacheDataFolder = cacheLocation;
            }
        }

        /// <summary>
        /// The directory used for storing downloaded remote files
        /// </summary>
        public const string Cache = "./cache/data";

        /// <summary>
        /// The version of lean
        /// </summary>
        public static string Version { get; private set; }

        /// <summary>
        /// Data path to cache folder location
        /// </summary>
        public static string CacheDataFolder { get; private set; }

        /// <summary>
        /// Helper method that will build a data folder path checking if it exists on the cache folder else will return data folder
        /// </summary>
        public static string GetDataFolderPath(string relativePath)
        {
            var result = Path.Combine(CacheDataFolder, relativePath);
            if (result.IsDirectoryEmpty())
            {
                result = Path.Combine(DataFolder, relativePath);
            }

            return result;
        }
    }
}
