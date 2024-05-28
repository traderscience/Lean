using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System;
using static QLNet.Callability;

namespace QuantConnect.Indicators
{
    public class EntropyIndicator : IndicatorBase<TradeBar>
    {
        private readonly KaufmanAdaptiveMovingAverage _kama;
        private RollingWindow<decimal> _priceWindow;
        private RollingWindow<decimal> _movingAvgWindow;
        private RollingWindow<double> _entropyWindow;

        /// <summary>
        /// Values provides access to prior bar's entropy value
        /// </summary>
        public RollingWindow<double> Values => _entropyWindow;

        public EntropyIndicator(string name, int period, int fastEmaPeriod=2, int slowEmaPeriod=30, int window=100)
            : base(name)
        {
            // Initialize KAMA with desired period
            _kama = new KaufmanAdaptiveMovingAverage(name + "_KAMA", period, fastEmaPeriod, slowEmaPeriod);

            // Rolling window to store historical prices for variance calculation
            _priceWindow = new RollingWindow<decimal>(period);
            _movingAvgWindow = new RollingWindow<decimal>(period);
            _entropyWindow = new RollingWindow<double>(window);
        }

        protected override decimal ComputeNextValue(TradeBar input)
        {
            // Update KAMA with the new data point
            _kama.Update(input.EndTime, input.Close);


            if (!_kama.IsReady)
            {
                return 0m; // Return 0 if the indicator isn't ready
            }
            // Add the latest price to the rolling window
            _priceWindow.Add(input.Close);

            decimal kamaValue = _kama.Current.Value;
            _movingAvgWindow.Add(kamaValue);

            // Calculate variance
            decimal variance = 0m;

            for (int i=0; i < _priceWindow.Count; i++)
            {
                var diff = _priceWindow[i] - _movingAvgWindow[i];
                variance += (diff * diff);
            }
            variance /= _priceWindow.Count;

            // Entropy could be derived from variance in different ways,
            // this is a placeholder for your entropy calculation logic
            decimal entropy = CalculateEntropyFromVariance(variance);
            _entropyWindow.Add((double)entropy);
            return entropy;
        }

        private decimal CalculateEntropyFromVariance(decimal variance)
        {
            // Implement your entropy calculation logic based on variance here
            // This is a simple example, you might want to use a more complex approach
            return (decimal)Math.Log((double)variance);
        }

        public override bool IsReady => _priceWindow.IsReady && _kama.IsReady;
    }
}
