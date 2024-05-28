/*
 * ZigZag Indicator with Support and Resistance Levels
 * Jeff Ellestad, TraderScience
 */
using QuantConnect.Data;
using QuantConnect.Data.Market;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Indicators
{
    public class ZigZagPoint
    {
        public DateTime Time;
        public decimal Value;
        public int trend;

        public ZigZagPoint(DateTime time, decimal value, int trend)
        {
            this.Time = time;
            this.Value = value;
            this.trend = trend;
        }
    }

    public class ZigZag : TradeBarIndicator, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary> 
        public int WarmUpPeriod { get; }

        /// <summary>
        /// Handler called when a new pivot is added
        /// </summary>
        public Action<ZigZagPoint> PivotAddedHandler;


        private bool isReady()
        {
            return pivots.Count >= 2;
        }

        public override bool IsReady => isReady();

        private const int PEAK = 1;
        private const int VALLEY = -1;
        private const int UPTREND = -1;
        private const int DOWNTREND = 1;
        private decimal upThreshold;
        private decimal downThreshold;

        // Pivot history
        private RollingWindow<ZigZagPoint> pivots;

        private int currentTrend;
        private ZigZagPoint initialPivot;
        private ZigZagPoint lastPivot;
        private TradeBar max;
        private TradeBar min;

        public ZigZag(string name, int warmupPeriod = 5, int maxPivots = 100, decimal upThreshold = .05m, decimal downThreshold = -.05m) : base(name)
        {
            this.WarmUpPeriod = warmupPeriod;
            this.upThreshold = upThreshold;
            // ensure downThreshold is negative
            this.downThreshold = -Math.Abs(downThreshold);
            if (this.upThreshold == 0 || this.downThreshold == 0)
                throw new Exception("ZigZag thresholds cannot be 0");
            pivots = new RollingWindow<ZigZagPoint>(maxPivots);
        }

        public override void Reset()
        {
            currentTrend = 0;
            max = null;
            min = null;
            lastPivot = null;
            initialPivot = null;
            pivots.Reset();
        }

        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>
        /// A new value for this indicator
        /// </returns>
        protected override decimal ComputeNextValue(TradeBar input)
        {
            if (input == null) return 0.0m;
            var avgPrice = (input.High + input.Low + input.Close) / 3.0m;

            if (initialPivot == null) // still looking for initial pivot
            {
                if (min == null)
                    min = input;
                if (max == null)
                    max = input;
                if (avgPrice / min.Value >= upThreshold+1)
                {
                    initialPivot = new ZigZagPoint(input.EndTime, avgPrice, PEAK);
                    lastPivot = initialPivot;
                    currentTrend = DOWNTREND;
                    return currentTrend;
                }
                if (avgPrice / max.Value <= downThreshold+1)
                {
                    initialPivot = new ZigZagPoint(input.EndTime, avgPrice, VALLEY);
                    lastPivot = initialPivot;
                    currentTrend = UPTREND;
                    return currentTrend;
                }
                if (avgPrice > max.Value)
                    max = input;
                if  (avgPrice < min.Value)
                    min = input;
                return 0m;  // no initial pivot found yet
            }

            // Process current data point after establishing the initial pivot 
            // r = price change ratio since last pivot
            var r = avgPrice / lastPivot.Value;

            if (currentTrend == UPTREND)
            {
                if (r >= upThreshold + 1)
                {
                    // reversed from downtrend, make final update to last pivot and add a new one
                    addPivot(lastPivot);
                    lastPivot = new ZigZagPoint(input.Time, avgPrice, PEAK);
                    currentTrend = DOWNTREND;
                }
                else
                if (avgPrice < lastPivot.Value)
                {
                    // downtrend is extending, update current pivot
                    lastPivot.Time = input.Time;
                    lastPivot.Value = avgPrice;
                }
            }
            else
            {
                if (r <= downThreshold + 1)
                {
                    // reversed from uptrend, update the current pivot and add a new one
                    addPivot(lastPivot);
                    lastPivot = new ZigZagPoint(input.Time, avgPrice, VALLEY);
                    currentTrend = UPTREND;
                }
                else
                if (avgPrice > lastPivot.Value)
                {
                    // uptrend is extending, update the current pivot
                    lastPivot.Time = input.Time;
                    lastPivot.Value = avgPrice;
                }
            }
            return currentTrend;
        }

        /// <summary>
        /// Return list of all pivots
        /// </summary>
        /// <returns></returns>
        public List<ZigZagPoint> Pivots()
        {
            return pivots.ToList();
        }


        private void addPivot(ZigZagPoint pivot)
        {
            pivots.Add(pivot);
            if (PivotAddedHandler != null)
                PivotAddedHandler(pivot);
        }

        /// <summary>
        /// Get Current Trend
        ///   -1 = down trend
        ///   1 = up trend
        /// </summary>
        /// <returns></returns>
        public int CurrentTrend()
        {
            return currentTrend * -1;  // converted trend: -1 = down trend, 1 = up trend 
        }

        /// <summary>
        /// Get current price position in last pivot high-low range
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public double PositionInRange(decimal value)
        {
            var lowPivot = pivots.FirstOrDefault(p => p.trend == -1);
            var highPivot = pivots.FirstOrDefault(p => p.trend == 1);
            if (lowPivot == null || highPivot == null)
                return .5;
            var range = highPivot.Value - lowPivot.Value;
            var pir = range > 0 ? (value - lowPivot.Value) / range : .5m;

            return (double)pir;
        }

        /// <summary>
        /// Get current price support level
        /// </summary>
        /// <returns></returns>
        public decimal CurrentSupport()
        {
            var lowPivot = pivots.FirstOrDefault(p => p.trend == -1);
            if (lowPivot != null)
                return lowPivot.Value;
            else
                return 0;
        }

        /// <summary>
        /// Get current price resistance level
        /// </summary>
        /// <returns></returns>
        public decimal CurrentResistance()
        {
            var highPivot = pivots.FirstOrDefault(p => p.trend == 1);
            if (highPivot != null)
                return highPivot.Value;
            else
                return 0;
        }
    }
}
