using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Tobii.Interaction;

namespace GazeBar
{
    /// <summary>
    /// MODEL: the real gaze position, provided by the eyetracker through the Tobii.Interaction
    /// library (the same as Mill Mouse). It does not depend on the cursor driven by the
    /// vendor's software.
    ///
    /// Coordinates are in screen pixels. They can be NEGATIVE or beyond the screen size when
    /// looking outside it: this is useful (off-screen menu, etc.).
    ///
    /// If the eyetracker engine is not available, the program keeps working: IsRunning stays
    /// false and ErrorMessage explains why.
    ///
    /// The eyetracker DLLs are not shipped with GazeBar: they are
    /// searched for on the PC at startup (see EyetrackerLoader.cs). So that GazeBar starts
    /// even when they cannot be found, the Tobii.Interaction types (Host, GazePointDataStream)
    /// only appear in the ConnectEyetracker method: .NET only looks for a DLL when it prepares
    /// ("just-in-time compiles") a method that uses it. If the DLL is missing, the error
    /// therefore happens when ConnectEyetracker is called, inside the try of Start, instead of
    /// crashing the whole program.
    /// </summary>
    internal sealed class GazeSource : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        // The Host object and the gaze stream (Tobii.Interaction types). Deliberately stored
        // under "neutral" types (IDisposable, object): see the comment at the top of the class.
        private IDisposable _host;
        private object _stream;

        private double _x;
        private double _y;
        private bool _hasData;
        private double _lastSampleMs = -1;
        private long _measureNumber;   // counter: +1 for each new measure received

        // Rate computation: count the measures received during a one-second window.
        private int _countInWindow;
        private double _windowStartMs;
        private double _samplesPerSecond;

        /// <summary>True if the connection to the eyetracker engine succeeded.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Error message if the connection failed (empty otherwise).</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// The Host object (Tobii.Interaction) (null if the connection failed). Only used to start
        /// the calibration tool (see Calibration.cs). Its type is "object" so that the
        /// rest of the program does not need to know Tobii.Interaction.
        /// </summary>
        public object Host
        {
            get { return _host; }
        }

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

        /// <summary>Connects to the eyetracker engine and starts receiving the gaze.</summary>
        public void Start()
        {
            // Were the eyetracker DLLs found on the PC? (see EyetrackerLoader.cs)
            if (!EyetrackerLoader.Available)
            {
                IsRunning = false;
                ErrorMessage = EyetrackerLoader.Message;
                App.Log("Gaze: no connection: " + ErrorMessage);
                return;
            }

            try
            {
                ConnectEyetracker();
                IsRunning = true;
                App.Log("Gaze: connected");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                ErrorMessage = ex.Message;
                App.Log("Gaze: connection failed: " + ex);
            }
        }

        /// <summary>
        /// The only place that uses the Tobii.Interaction types directly. "NoInlining" forbids
        /// the compiler from copying this method inside Start: otherwise, a missing DLL would
        /// make Start itself fail, outside its try.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ConnectEyetracker()
        {
            Host host = new Host();
            _host = host;
            GazePointDataStream stream = host.Streams.CreateGazePointDataStream();
            _stream = stream;

            // Called by Tobii.Interaction, on one of its threads, for each new measure.
            stream.Next += (sender, measure) => OnGaze(measure.Data.X, measure.Data.Y);
        }

        private void OnGaze(double x, double y)
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
            lock (_lock)
            {
                x = _x;
                y = _y;
            }
            // Scale correction (see ScreenMetrics.GazeFactor): usually 1.
            double factor = ScreenMetrics.GazeFactor;
            x *= factor;
            y *= factor;
        }

        /// <summary>
        /// Latest gaze position, with its number and the time it arrived.
        ///
        /// Why the number? The modes' clock runs at 60 ticks per second, but the PCEye 5 only
        /// sends about 30 measures per second: so one tick out of two sees the SAME measure as
        /// the previous tick. By comparing the numbers, a mode knows whether the measure is new,
        /// and counts it only once.
        /// The time (in milliseconds, on GazeSource's clock) allows computing a speed or a
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
            // Scale correction (see ScreenMetrics.GazeFactor): usually 1.
            double factor = ScreenMetrics.GazeFactor;
            x *= factor;
            y *= factor;
        }

        /// <summary>Current time on GazeSource's clock, in milliseconds (same scale as instantMs).</summary>
        public double NowMs
        {
            get { return _clock.Elapsed.TotalMilliseconds; }
        }

        /// <summary>True if the position is outside the primary screen.</summary>
        public static bool IsOffScreen(double x, double y, double screenWidth, double screenHeight)
        {
            return x < 0 || y < 0 || x >= screenWidth || y >= screenHeight;
        }

        public void Dispose()
        {
            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }
        }
    }
}
