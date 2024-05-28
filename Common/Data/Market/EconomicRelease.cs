using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Economic Release Item
    /// </summary>
    public class EconomicRelease
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
        public string Currency { get; set; }
        public string Symbol { get; set; }
        public string Source { get; set; }
        public string Url { get; set; }
        public DateTime ReleaseTime { get; set; }
        public string Frequency { get; set; }
        public string Unit { get; set; }
        public string SeasonalAdjustment { get; set; }
        public int Impact { get; set; }
        public double Previous { get; set; }
        public double Estimate { get; set; }
        public double Actual { get; set; }

    }
}
