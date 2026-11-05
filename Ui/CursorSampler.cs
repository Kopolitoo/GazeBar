using System;
using System.Diagnostics;
using System.Threading;

namespace Prunel
{
    /// <summary>
    /// One measure: where the cursor was, and when.
    ///
    /// "struct" = a small bundle of values (unlike a "class", it is copied when passed from
    /// one method to another).
    /// </summary>
    internal struct CursorSample
    {
        /// <summary>Milliseconds elapsed since the sampler started.</summary>
        public double Ms;

        /// <summary>Horizontal position, in pixels (0 = left edge of the screen).</summary>
        public int X;

        /// <summary>Vertical position, in pixels (0 = top edge of the screen).</summary>
        public int Y;
    }

    /// <summary>Statistics computed over the last elapsed second.</summary>
    internal struct CursorStats
    {
        /// <summary>How many times per second the cursor was READ (must be well above the eye tracking rate).</summary>
        public double PollsPerSecond;

        /// <summary>How many times per second the position CHANGED: the real rate of the "gaze" obtained through the cursor.</summary>
        public double ChangesPerSecond;
    }

    /// <summary>
    /// MODEL: continuously reads the mouse cursor position.
    ///
    /// Reminder of the principle: the eyetracker software moves the Windows cursor to where
    /// you look. So reading the cursor amounts to reading a "gaze" filtered by that software,
    /// on the whole screen (unlike the UWP API, limited to its own window).
    ///
    /// How it works: a dedicated "thread" queries Windows about 500 to 1000 times per
    /// second. The user interface thread is not slowed down, and each position change is
    /// seen with its precise time.
    /// It only runs while the Diagnostic window is open: that window creates it
    /// and stops it.
    /// </summary>
    internal sealed class CursorSampler : IDisposable
    {
        /// <summary>Number of positions kept in memory to draw the gaze trail.</summary>
        private const int TrailLength = 150;

        // The clock: measures elapsed time precisely and steadily.
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        // The data below is read by the interface AND written by the sampling thread.
        // The "lock" guarantees that only one of them touches it at a time, so that a
        // half-written value is never read.
        private readonly object _lock = new object();
        private readonly CursorSample[] _trail = new CursorSample[TrailLength];

        private Thread _thread;
        private volatile bool _running;

        private int _x;
        private int _y;
        private bool _hasPosition;
        private double _lastChangeMs = -1;
        private int _trailCount;
        private int _trailNext;
        private int _polls;
        private int _changes;
        private double _windowStartMs;
        private CursorStats _stats;


        /// <summary>Current time of the sampler's clock, in milliseconds.</summary>
        public double NowMs
        {
            get { return _clock.Elapsed.TotalMilliseconds; }
        }

        /// <summary>Statistics of the last complete second.</summary>
        public CursorStats Stats
        {
            get { lock (_lock) { return _stats; } }
        }

        /// <summary>Time elapsed since the last position change, in ms (-1 if no position has changed yet).</summary>
        public double MsSinceLastChange
        {
            get
            {
                lock (_lock)
                {
                    return _lastChangeMs < 0 ? -1 : NowMs - _lastChangeMs;
                }
            }
        }

        /// <summary>Starts watching the cursor.</summary>
        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _thread = new Thread(Loop);
            _thread.Name = "CursorSampler";
            _thread.IsBackground = true;                   // does not block closing the program
            _thread.Priority = ThreadPriority.AboveNormal; // regular reads even if the computer is busy
            _thread.Start();
        }

        /// <summary>Most recent position. "out": the method returns its two results through these parameters.</summary>
        public void GetLatest(out int x, out int y)
        {
            lock (_lock)
            {
                x = _x;
                y = _y;
            }
        }

        /// <summary>Copy of the latest positions, from oldest to newest (to draw the trail).</summary>
        public CursorSample[] GetTrail()
        {
            lock (_lock)
            {
                CursorSample[] copy = new CursorSample[_trailCount];
                int start = (_trailNext - _trailCount + TrailLength) % TrailLength;
                for (int i = 0; i < _trailCount; i++)
                {
                    copy[i] = _trail[(start + i) % TrailLength];
                }
                return copy;
            }
        }

        /// <summary>Thread loop: read the cursor, wait 1 ms, start again.</summary>
        private void Loop()
        {
            // Without this call, Thread.Sleep(1) would sleep ~15 ms. Here Windows grants us 1 ms.
            NativeMethods.timeBeginPeriod(1);
            try
            {
                while (_running)
                {
                    PollOnce();
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                App.Log("CursorSampler: stopped on error: " + ex);
            }
            finally
            {
                NativeMethods.timeEndPeriod(1);
            }
        }

        /// <summary>One cursor read: updates the position, the trail and the statistics.</summary>
        private void PollOnce()
        {
            NativeMethods.POINT point;
            if (!NativeMethods.GetCursorPos(out point))
            {
                return; // cannot read at this moment (rare): we will try again
            }

            double now = NowMs;
            bool changed;
            CursorSample sample = new CursorSample();

            lock (_lock)
            {
                _polls++;
                changed = !_hasPosition || point.X != _x || point.Y != _y;

                if (changed)
                {
                    _hasPosition = true;
                    _x = point.X;
                    _y = point.Y;
                    _lastChangeMs = now;
                    _changes++;

                    sample.Ms = now;
                    sample.X = point.X;
                    sample.Y = point.Y;

                    // "Circular buffer": when it is full, the new position replaces the oldest one.
                    _trail[_trailNext] = sample;
                    _trailNext = (_trailNext + 1) % TrailLength;
                    if (_trailCount < TrailLength)
                    {
                        _trailCount++;
                    }
                }

                // Every second: freeze the statistics and start again from zero.
                if (now - _windowStartMs >= 1000.0)
                {
                    double seconds = (now - _windowStartMs) / 1000.0;
                    _stats.PollsPerSecond = _polls / seconds;
                    _stats.ChangesPerSecond = _changes / seconds;
                    _polls = 0;
                    _changes = 0;
                    _windowStartMs = now;
                }
            }
        }

        /// <summary>Stops the thread.</summary>
        public void Dispose()
        {
            _running = false;
            if (_thread != null)
            {
                _thread.Join(500);
                _thread = null;
            }
        }
    }
}
