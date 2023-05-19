using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace QuantConnect.Util
{
    /// <summary>
    /// Brokerage support functions
    /// </summary>
    public static class BrokerageUtil
    {
        /// <summary>
        /// Convert MultiCharts order reference string to dictionary
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static Dictionary<string, string> TryParseExternalOrderReference(string input)
        {
            // MultiCharts: ID=[84], Symbol:  (FUT, USD, 20200619), Exchange: GLOBEX, 			   Order: SELL-LMT, Lots: 3, StopPrice: 1.79769e+308, LimitPrice: 9538
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();

            if (input != null)
            {
                if (input.StartsWith("MultiCharts"))
                {
                    input = input.Replace(" ", null);
                    int s = input.IndexOf("Symbol:");
                    int ex = input.IndexOf("Exchange:");
                    if (ex > 0)
                    {
                        keyValuePairs = input.Substring(ex).Split(',')
                            .Select(value => value.Split(':'))
                            .ToDictionary(pair => pair[0], pair => pair[1]);
                        keyValuePairs.Add("Symbol", input.Substring(s + 7, ex - s + 7));
                        keyValuePairs.Add("Source", "MultiCharts");
                        return keyValuePairs;
                    }
                }
                else
                {
                    try
                    {
                        string[] parts = input.Split(';');
                        var kvp = JsonConvert.DeserializeObject<Dictionary<string, string>>(parts[0], new JsonSerializerSettings() {NullValueHandling=NullValueHandling.Ignore});
                        return kvp;
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
            return null;
        }
    }
}
