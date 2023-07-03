using ProtoBuf;
using QuantConnect.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Third party reference data
    /// </summary>
    public class AuxiliaryData : BaseData
    {
        /// <summary>
        /// List of values for this time stamp
        /// </summary>
        public IEnumerable<double> Values { get; set; }

        /// <summary>
        /// The period of this bar, (daily, weekly, monthly, etc...)
        /// </summary>
        public virtual TimeSpan Period { get; set; }

        /// <summary>
        /// Default initializer to setup an empty tradebar.
        /// </summary>
        public AuxiliaryData()
        {
            Symbol = Symbol.Empty;
            DataType = MarketDataType.Auxiliary;
            Period = QuantConnect.Time.OneDay;
        }

        /// <summary>
        /// 
        /// </summary>
        public AuxiliaryData(DateTime dateTime,IEnumerable<double> values)
        {
            EndTime = dateTime;
            Values = values;
            Period = QuantConnect.Time.OneDay;
        }

        /// <summary>
        /// By default, we select the last Values column as the price.
        /// </summary>
        public override decimal Price
        {
            get => (decimal)Values.LastOrDefault();
        }

        /// <summary>
        /// By default, we select the last Values column as the value.
        /// </summary>
        public override decimal Value 
        {
            get => (decimal)Values.LastOrDefault();
        }

        /// <summary>
        /// Get the specified value by index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public decimal GetValue(int index)
        {
            return (decimal)Values.ElementAtOrDefault(index);
        }

        /// <summary>
        /// Get number of data values for each bar
        /// </summary>
        public int GetSize => Values != null ? Values.Count() : 0;

        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            //Handle end of file:
            if (line == null)
            {
                return null;
            }

            if (isLiveMode)
            {
                return new AuxiliaryData();
            }

            try
            {
                switch (config.SecurityType)
                {
                    //Equity File Data Format:
                    case SecurityType.Auxiliary:
                        return ParseAuxiliaryData(config, line, date);

                    default:
                        break;
                }
            }
            catch (Exception err)
            {
                Log.Error($"AuxiliaryData.Reader(): Error parsing line: '{line}', Symbol: {config.Symbol.Value}, SecurityType: {config.SecurityType}, Resolution: {config.Resolution}, Date: {date:yyyy-MM-dd}, Message: {err}");
            }

            // if we couldn't parse it above return a default instance
            return new AuxiliaryData { Symbol = config.Symbol, Period = config.Increment };
        }

        private static AuxiliaryData ParseAuxiliaryData(SubscriptionDataConfig config, string line, DateTime date)
        {
            var csv = line.Split(',');
            var values = new List<double>();
            for (int i = 1; i < csv.Length; i++)
            {
                values.Add(double.Parse(csv[i], System.Globalization.CultureInfo.InvariantCulture));
            }
            var timeStamp = DateTime.ParseExact(csv[0], "yyyyMMdd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            return new AuxiliaryData(timeStamp, values) { Symbol = config.Symbol, Period = config.Increment };
        }
    }
}
