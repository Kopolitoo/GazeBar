using System;

namespace GazeBar
{
    /// <summary>
    /// LOGIC: recognizes a FIXATION, that is, a gaze that stays still on a point.
    ///
    /// It is given the gaze on each tick (Add method). It answers:
    ///   - Progress : from 0 to 1, how far the fixation is (used to fill the ring);
    ///   - Reached  : true when the gaze has stayed still for [FixationDelay];
    ///   - X, Y     : the fixated point (the average of the fixation's measures).
    ///
    /// A measure is "still" if it passes TWO tests:
    ///  1. the SPEED test (the user's method, branch socle-mode-adjust): the gaze speed since
    ///     the previous measure, in pixels per second, stays below [MaxFixationSpeed]. The
    ///     distance is divided by the REAL time elapsed between the two measures, and only NEW
    ///     measures are counted: so the result depends neither on the clock rate (60 ticks/s)
    ///     nor on the PCEye's (≈ 30 measures/s);
    ///  2. the DISPERSION test: the measure stays within [FixationRadius] of the anchor
    ///     point. Without this test, a gaze sliding slowly (reading a line...) would pass the
    ///     speed test and end up triggering a fixation off target.
    ///
    /// The anchor point is the AVERAGE of the measures accepted since the fixation began: it
    /// is more accurate than the first measure alone, which may fall slightly off.
    ///
    /// Tolerance: the PCEye sometimes returns ONE aberrant measure in the middle of a fixation
    /// (a blink, a reflection). So a failing measure is set aside once, without resetting
    /// anything. If the next measure fails too, it is a real move: the fixation starts again
    /// from this new measure.
    ///
    /// Time is measured in real milliseconds (FixationDelay), not in number of ticks.
    ///
    /// Screen edges and corners: a measure just outside the screen (see
    /// GazeState.NearScreen) does not interrupt the fixation; it is brought back to the edge.
    /// </summary>
    internal sealed class FixationDetector
    {
        // The anchor point = sum of the accepted measures ÷ their number.
        private double _sumX;
        private double _sumY;
        private int _number;

        // Time of the first measure of the current fixation, in milliseconds.
        private double _startMs;

        // Last ACCEPTED measure (used to compute the speed).
        private double _prevX;
        private double _prevY;
        private double _prevMs;

        // Number of the last processed measure (to process each measure only once).
        private long _lastNumber = -1;

        // True if the last measure was set aside (only one tolerated in a row).
        private bool _setAsideMeasure;

        /// <summary>Fixation progress, between 0 (nothing) and 1 (fixation reached).</summary>
        public double Progress { get; private set; }

        /// <summary>True when the gaze has stayed still for [FixationDelay].</summary>
        public bool Reached
        {
            get { return Progress >= 1; }
        }

        /// <summary>The fixated point (anchor point), in screen pixels.</summary>
        public double X
        {
            get { return _number > 0 ? _sumX / _number : 0; }
        }

        public double Y
        {
            get { return _number > 0 ? _sumY / _number : 0; }
        }

        /// <summary>
        /// Forgets everything: the next measure will start a new fixation.
        /// To call when the gaze is lost or off screen.
        /// </summary>
        public void Reset()
        {
            _number = 0;
            _sumX = 0;
            _sumY = 0;
            _setAsideMeasure = false;
            Progress = 0;
        }

        /// <summary>
        /// To call on each tick with the gaze "snapshot".
        /// Only new on-screen measures are analyzed; between two measures, only the progress is
        /// updated (so that the ring fills up smoothly).
        /// </summary>
        public void Add(GazeState gaze)
        {
            // Gaze lost, or clearly off screen: forget the fixation.
            // Gaze right next to the screen (in the NearScreen band): keep it.
            if (!gaze.NearScreen)
            {
                Reset();
                return;
            }

            if (gaze.Number != _lastNumber)
            {
                _lastNumber = gaze.Number;

                // Analyze the position BROUGHT BACK INTO THE SCREEN (XScreen, YScreen). On the
                // screen, it is exactly the real gaze. Right next to it, it is the nearest edge
                // point: in a corner, the overflowing measures are brought back into the corner,
                // they stay grouped and the fixation can succeed. So the fixated point is always
                // on the screen (one can click right at the edge, or in the corner).
                AnalyzeMeasure(gaze.XScreen, gaze.YScreen, gaze.MeasureTimeMs);
            }

            ComputeProgress(gaze.NowMs);
        }

        // ───────────────────────── Computation details ─────────────────────────

        private void AnalyzeMeasure(double x, double y, double instantMs)
        {
            // No fixation in progress: this measure becomes its starting point.
            if (_number == 0)
            {
                Restart(x, y, instantMs);
                return;
            }

            AppSettings p = AppSettings.Current;

            // Test 1: the speed since the last accepted measure, in pixels per second.
            double duration = instantMs - _prevMs;
            bool speedOk = true;
            if (duration > 0)
            {
                double speed = Distance(x, y, _prevX, _prevY) / duration * 1000.0;
                speedOk = speed <= p.MaxFixationSpeedPx;
            }

            // Test 2: the distance to the anchor point.
            bool dispersionOk = Distance(x, y, X, Y) <= p.FixationRadiusPx;

            if (speedOk && dispersionOk)
            {
                // Still measure: it joins the fixation and refines the anchor point.
                _sumX += x;
                _sumY += y;
                _number++;
                _prevX = x;
                _prevY = y;
                _prevMs = instantMs;
                _setAsideMeasure = false;
            }
            else if (!_setAsideMeasure)
            {
                // First measure moving away: maybe a simple accident. Ignore it.
                _setAsideMeasure = true;
            }
            else
            {
                // Second in a row: the gaze really went elsewhere.
                Restart(x, y, instantMs);
            }
        }

        /// <summary>Starts a new fixation from this measure.</summary>
        private void Restart(double x, double y, double instantMs)
        {
            _sumX = x;
            _sumY = y;
            _number = 1;
            _prevX = x;
            _prevY = y;
            _prevMs = instantMs;
            _startMs = instantMs;
            _setAsideMeasure = false;
        }

        private void ComputeProgress(double nowMs)
        {
            if (_number == 0)
            {
                Progress = 0;
                return;
            }

            double delayMs = AppSettings.Current.FixationDelay * 1000.0;
            Progress = Math.Min(1.0, Math.Max(0.0, (nowMs - _startMs) / delayMs));
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
