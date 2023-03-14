/*
 * Copyright TraderScience Corporation 2020-2023
 */

using System;
using QuantConnect.Data.Market;
using Python.Runtime;

namespace QuantConnect.Data.Consolidators
{
    /// <summary>
    /// A data consolidator that can make bigger bars from smaller ones
    /// until a user specified event occurs
    ///
    /// Use this Event Based consolidator to aggregate data until a user specified event is triggered
    /// </summary>

    // User's event checker accepts a user defined context object and returns a bool (true=trigger consolidation)
    public delegate bool EventBasedConsolidatorTrigger(object context);

    /// <summary>
    /// Interface for a user provided class that will determine when to trigger an event for bar consolidation.
    /// </summary>
    public interface IEventBasedConsolidatorChecker
    {
        public bool EventTrigger(object context);

        public object CreateContext(Symbol symbol, object indicator);
    }

    /// <summary>
    /// TradeBarEventConsolidator
    /// Used to consolidate bars based on a user provided trigger function.
    /// Examples: moving average crossing, volume threshold, volatility, etc.
    /// </summary>
    public class TradeBarEventConsolidator : IDataConsolidator
    {
        private EventBasedConsolidatorTrigger _checkEvent;
        private DataConsolidatedHandler _dataConsolidatedHandler;
        private IBaseData _consolidated;

        private PyObject _pyCheckEvent = null;
        private Symbol _symbol = null;
        private int? _maxCount = null;
        private int _currentCount = 0;
        private TradeBar _workingBar = null;
        private object _workingContext = null;
        private DateTime? _lastEmit = null;
        private TimeSpan? _period = null;


        /// <summary>
        /// Event handler that fires when a new piece of data is produced
        /// </summary>
        public event EventHandler<TradeBar> DataConsolidated;

        /// <summary>
        /// Event handler that fires when a new piece of data is produced
        /// </summary>
        event DataConsolidatedHandler IDataConsolidator.DataConsolidated
        {
            add { _dataConsolidatedHandler += value; }
            remove { _dataConsolidatedHandler -= value; }
        }

        /// <summary>
        /// Gets the most recently consolidated piece of data. This will be null if this consolidator
        /// has not produced any data yet.
        /// </summary>
        public IBaseData Consolidated
        {
            get { return _consolidated; }
            private set { _consolidated = value; }
        }

        /// <summary>
        /// Gets a clone of the data being currently consolidated
        /// </summary>
        public IBaseData WorkingData => _workingBar?.Clone();

        /// <summary>
        /// Gets the type consumed by this consolidator
        /// </summary>
        public Type InputType => typeof(TradeBar);

        /// <summary>
        /// Gets <see cref="RenkoBar"/> which is the type emitted in the <see cref="IDataConsolidator.DataConsolidated"/> event.
        /// </summary>
        public Type OutputType => typeof(TradeBar);

        /// <summary>
        /// Dispose of consolidator
        /// </summary>
        ~TradeBarEventConsolidator()
        {
            DataConsolidated = null;
            _dataConsolidatedHandler = null;
        }

        /// <summary>
        /// Create a new TradeBarConsolidator for the desired resolution
        /// </summary>
        /// <param name="checkEvent">function that determines if an event has occurred</param>
        /// <param name="resolution">The resolution desired</param>
        /// <returns>A consolidator that produces data on the resolution interval</returns>
        public static TradeBarEventConsolidator FromResolution(EventBasedConsolidatorTrigger checkEvent, object context, Resolution resolution)
        {
            return new TradeBarEventConsolidator(checkEvent, context, resolution.ToTimeSpan());
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'TradeBar' representing the period
        /// </summary>
        /// <param name="checkEvent">function that determines if an event has occurred</param>
        /// <param name="context">user function context</param>
        /// <param name="period">The minimum span of time before emitting a consolidated bar</param>
        public TradeBarEventConsolidator(EventBasedConsolidatorTrigger checkEvent, object context, TimeSpan period)
        {
            _checkEvent = checkEvent;
            _workingContext = context;
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'TradeBar' representing the last count pieces of data
        /// </summary>
        /// <param name="checkEvent">function that determines if an event has occurred</param>
        /// <param name="maxCount">The number of pieces to accept before emitting a consolidated bar</param>
        public TradeBarEventConsolidator(EventBasedConsolidatorTrigger checkEvent, object context, int maxCount)
        {
            _checkEvent = checkEvent;
            _workingContext = context;
            _maxCount = maxCount;
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'TradeBar' representing the last count pieces of data or the period, whichever comes first
        /// </summary>
        /// <param name="checkEvent">function that determines if an event has occurred</param>
        /// <param name="context">user function context</param>
        /// <param name="maxCount">The number of pieces to accept before emitting a consolidated bar</param>
        /// <param name="period">The minimum span of time before emitting a consolidated bar</param>
        public TradeBarEventConsolidator(EventBasedConsolidatorTrigger checkEvent, object context, int maxCount, TimeSpan period)
        {
            _checkEvent = checkEvent;
            _workingContext = context;
            _maxCount = maxCount;
            _period = period;
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'TradeBar' representing the last count pieces of data or the period, whichever comes first
        /// </summary>
        /// <param name="checkEvent"></param>
        /// <param name="context">user function context</param>
        /// <param name="func">Func that defines the start time of a consolidated data</param>
        public TradeBarEventConsolidator(EventBasedConsolidatorTrigger checkEvent, object context, Func<DateTime, CalendarInfo> func)
        {
            _checkEvent = checkEvent;
            _workingContext = context;
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'TradeBar' representing the last count pieces of data or the period, whichever comes first
        /// </summary>
        /// <param name="checkEvent"></param>
        /// <param name="pyfuncobj">Python function object that defines the start time of a consolidated data</param>
        public TradeBarEventConsolidator(PyObject checkEvent, PyObject context, PyObject pyfuncobj)
        {
            _pyCheckEvent = checkEvent;
            _workingContext = context;
        }

        /// <summary>
        /// Event invocator for the DataConsolidated event. This should be invoked
        /// by derived classes when they have consolidated a new piece of data.
        /// </summary>
        /// <param name="consolidated">The newly consolidated data</param>
        protected void OnDataConsolidated(TradeBar consolidated)
        {
            _dataConsolidatedHandler?.Invoke(this, consolidated);
            DataConsolidated?.Invoke(this, consolidated);
            _workingBar = consolidated;
            Consolidated = consolidated;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public void Update(IBaseData inputData)
        {
            if (inputData is not TradeBar)
                throw new InvalidOperationException($"Event Consolidators can only be used with a TradeBar input");

            var data = inputData as TradeBar;
            if (_symbol == null)
            {
                _symbol = data.Symbol;
            }
            else if (_symbol != data.Symbol)
            {
                throw new InvalidOperationException($"Consolidators can only be used with a single symbol. The previous consolidated symbol ({_symbol}) is not the same as in the current data ({data.Symbol}).");
            }

            //Decide to fire the event
            var fireDataConsolidated = false;


            if (!_lastEmit.HasValue)
            {
                // initialize this value for period computations
                _lastEmit = data.Time;
            }

            if (_workingBar == null)
            {
                _workingBar = new TradeBar()
                {
                    Time = data.Time,
                    Period = data.Period,
                    Symbol = data.Symbol,
                    Open = data.Open,
                    High = data.High,
                    Low = data.Low,
                    Close = data.Close,
                    Volume = data.Volume,
                    DataType = MarketDataType.TradeBar
                };
            }
            else
            if (data.Time >= _workingBar.EndTime)
            {
                AggregateBar(data);
            }

            if (_checkEvent != null)
            {
                fireDataConsolidated = _checkEvent(_workingContext);

                //Fire the event
                if (fireDataConsolidated)
                {
                    var workingTradeBar = _workingBar as TradeBar;
                    if (workingTradeBar != null)
                    {
                        OnDataConsolidated(workingTradeBar);
                        _lastEmit = _workingBar.EndTime;
                    }
                    _workingBar = null;
                }
            }
        }

        /// <summary>
        /// Aggregates the new 'data' into the 'workingBar'. The 'workingBar' will be
        /// null following the event firing
        /// </summary>
        /// <param name="workingBar">The bar we're building, null if the event was just fired and we're starting a new trade bar</param>
        /// <param name="data">The new data</param>
        protected void AggregateBar(TradeBar data)
        {
            if (_workingBar != null)
            {
                //Aggregate the working bar
                // Extend the workingBar period by the length of the new bar
                _workingBar.Period = data.EndTime.Subtract(_workingBar.Time);
                // update the new Close price
                _workingBar.Close = data.Close;
                // accumulate Volume
                _workingBar.Volume += data.Volume;
                // check if workingBars' Low and High have been exceeded
                if (data.Low < _workingBar.Low) _workingBar.Low = data.Low;
                if (data.High > _workingBar.High) _workingBar.High = data.High;
            }
        }

        /// <summary>
        /// Scans this consolidator to see if it should emit a bar due to time passing
        /// </summary>
        /// <param name="currentLocalTime">The current time in the local time zone (same as <see cref="BaseData.Time"/>)</param>
        public void Scan(DateTime currentLocalTime)
        {
        }

        public void Dispose()
        {
            DataConsolidated = null;
            _dataConsolidatedHandler = null;
        }
    }
}
