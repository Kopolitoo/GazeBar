using System.Diagnostics;

namespace GazeBar
{
    /// <summary>
    /// The part shared by all gaze sources (see IGazeSource): the clock, the latest position,
    /// the measure counter and the rate computation.
    ///
    /// "abstract": this class is never used alone. A real source (EyetrackerGazeSource...)
    /// inherits from it, writes its own Start and Dispose, and calls OnGaze for each new
    /// measure. Only the latest position is kept: nothing is saved.
    /// </summary>
    internal abstract class GazeSource : IGazeSource
    {
        private readonly object _lock = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private double _x;
        private double _y;
        private bool _hasData;
        private double _lastSampleMs = -1;
        private long _measureNumber;   // counter: +1 for each new measure received

        // Rate computation: count the measures received during a one-second window.
        private int _countInWindow;
        private double _windowStartMs;
        private double _samplesPerSecond;

        /// <summary>True if the source started correctly.</summary>
        public bool IsRunning { get; protected set; }

        /// <summary>Error message if the source could not start (empty otherwise).</summary>
        public string ErrorMessage { get; protected set; }

        /// <summary>True as soon as at least one measure has been received.</summary>
        public bool HasData
        {
            get { lock (_lock) { return _hasData; } }
        }

        /// <summary>Measure rate: number per second (0 if nothing arrives any more).</summary>
        public double SamplesPerSecond
        {
            get
            {
                lock (_lock)
                {
                    bool silent = _lastSampleMs < 0 || _clock.Elapsed.TotalMilliseconds - _lastSampleMs > 1500;
                    return silent ? 0 : _samplesPerSecond;
                }
            }
        }

        /// <summary>Time elapsed since the last measure, in ms (very large if none).</summary>
        public double MsSinceLastSample
        {
            get
            {
                lock (_lock)
                {
                    return _lastSampleMs < 0 ? double.MaxValue : _clock.Elapsed.TotalMilliseconds - _lastSampleMs;
                }
            }
        }

        /// <summary>Current time on the source's clock, in milliseconds (same scale as instantMs).</summary>
        public double NowMs
        {
            get { return _clock.Elapsed.TotalMilliseconds; }
        }

        /// <summary>
        /// Factor applied to the positions read (see ScreenMetrics.GazeFactor for the
        /// eyetracker). 1 by default: positions are already in screen pixels.
        /// </summary>
        protected virtual double ScaleFactor
        {
            get { return 1; }
        }

        public abstract void Start();

        public abstract void Dispose();

        /// <summary>
        /// To call for each new measure (from any thread): stores the position, counts the
        /// measure and updates the rate.
        /// </summary>
        protected void OnGaze(double x, double y)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            lock (_lock)
            {
                _x = x;
                _y = y;
                _hasData = true;
                _lastSampleMs = now;
                _measureNumber++;

                _countInWindow++;
                double elapsed = now - _windowStartMs;
                if (elapsed >= 1000)
                {
                    _samplesPerSecond = _countInWindow * 1000.0 / elapsed;
                    _countInWindow = 0;
                    _windowStartMs = now;
                }
            }
        }

        /// <summary>Latest known gaze position (screen pixels).</summary>
        public void GetLatest(out double x, out double y)
        {
            long num;
            double instantMs;
            GetLatest(out x, out y, out num, out instantMs);
        }

        /// <summary>
        /// Latest gaze position, with its number and the time it arrived.
        ///
        /// Why the number? The modes' clock runs at 60 ticks per second, but the PCEye 5 only
        /// sends about 30 measures per second: so one tick out of two sees the SAME measure as
        /// the previous tick. By comparing the numbers, a mode knows whether the measure is new,
        /// and counts it only once.
        /// The time (in milliseconds, on the source's clock) allows computing a speed or a
        /// duration in real time, whatever the rate.
        /// </summary>
        public void GetLatest(out double x, out double y, out long num, out double instantMs)
        {
            lock (_lock)
            {
                x = _x;
                y = _y;
                num = _measureNumber;
                instantMs = _lastSampleMs;
            }
            double factor = ScaleFactor;
            x *= factor;
            y *= factor;
        }
    }
}
