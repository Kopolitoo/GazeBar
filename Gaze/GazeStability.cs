using System;

namespace Prunel
{
    /// <summary>
    /// LOGIC: measures how STEADY the gaze is, in three bands of the screen (top quarter,
    /// middle, bottom quarter). Used by the Diagnostic window to understand why the circle
    /// jitters at the bottom of the screen (issue #86).
    ///
    /// Only counters are kept in memory (sums, numbers of measures, times): no gaze position
    /// is stored, nothing is written to disk (eyetracker licence). Everything is forgotten
    /// when the Diagnostic window closes, or with Reset.
    ///
    /// For each band, it computes:
    ///  - AGITATION: the typical distance between two measures in a row (RMS, in screen
    ///    pixels), when the gaze holds still. Big jumps (the gaze moving elsewhere) are left
    ///    out: they are real moves, not jitter;
    ///  - TOO FAST: the share of these steps that fail the fixation speed test
    ///    ([MaxFixationSpeed], see FixationDetector). Many → fixed gaze is hard to reach;
    ///  - LOSSES: how many times per minute no measure arrives for more than
    ///    [MaxMeasureAge] (the circle then freezes, and moves again when the gaze comes back);
    ///  - OFF SCREEN: the share of measures outside the screen.
    ///
    /// A measure belongs to the band of its height on the screen. Above the screen counts as
    /// top, below the screen as bottom. A loss belongs to the band of the last measure before it.
    /// </summary>
    internal sealed class GazeStability
    {
        /// <summary>The three bands, from top to bottom.</summary>
        public const int BandCount = 3;
        public const int Top = 0;
        public const int Middle = 1;
        public const int Bottom = 2;

        /// <summary>
        /// A step bigger than this (in 4K screen pixels) is a real move of the gaze, not
        /// jitter: it is left out of the agitation and of the "too fast" share.
        /// 200 px on the 4K screen = about 10 % of its height.
        /// </summary>
        private const double JumpLimit4K = 200;

        /// <summary>
        /// Gap between two measures longer than this: the measures in between were missed by
        /// the reading timer, so the step is not "two measures in a row". In ms.
        /// (The PCEye 5 sends a measure about every 30 ms.)
        /// </summary>
        private const double MaxStepGapMs = 80;

        /// <summary>Counters of one band.</summary>
        private sealed class Band
        {
            public long Measures;          // number of measures seen
            public long OffScreen;         // of which outside the screen
            public long Steps;             // steps between two measures in a row (jumps excluded)
            public double SquaredSum;      // sum of the squared step lengths
            public long TooFast;           // steps failing the fixation speed test
            public long Losses;            // gaze losses (no measure for more than MaxMeasureAge)
            public double TimeMs;          // time spent in this band (measures + losses)
        }

        private readonly Band[] _bands = new Band[BandCount];

        // Previous measure (only for the step being computed; replaced at each new measure).
        private bool _hasPrevious;
        private long _previousNumber;
        private double _previousX;
        private double _previousY;
        private double _previousMs;
        private int _previousBand;

        // True while the gaze is lost (the current loss is counted only once).
        private bool _inLoss;

        public GazeStability()
        {
            Reset();
        }

        /// <summary>Forgets every counter.</summary>
        public void Reset()
        {
            for (int i = 0; i < BandCount; i++)
            {
                _bands[i] = new Band();
            }
            _hasPrevious = false;
            _inLoss = false;
        }

        /// <summary>
        /// To call often (every 10 ms or so), with the gaze source. Each new measure is
        /// analyzed once; between two measures, only the gaze losses are watched.
        /// </summary>
        public void Update(IGazeSource gaze, double screenWidth, double screenHeight)
        {
            if (!gaze.IsRunning || !gaze.HasData)
            {
                return;
            }

            // 1. Gaze lost? (Same rule as the modes: no measure for more than MaxMeasureAge.)
            if (gaze.MsSinceLastSample > AppSettings.Current.MaxMeasureAge)
            {
                if (_hasPrevious && !_inLoss)
                {
                    _bands[_previousBand].Losses++;
                }
                _inLoss = true;
                return;
            }

            double x;
            double y;
            long number;
            double instantMs;
            gaze.GetLatest(out x, out y, out number, out instantMs);
            if (_hasPrevious && number == _previousNumber)
            {
                return; // same measure as before: nothing new
            }

            int band = BandOf(y, screenHeight);
            Band b = _bands[band];
            b.Measures++;
            if (GazeState.IsOffScreen(x, y, screenWidth, screenHeight))
            {
                b.OffScreen++;
            }

            if (_hasPrevious)
            {
                double gapMs = instantMs - _previousMs;

                // Time spent: given to the band of the previous measure (including a loss).
                // Capped, so that a long pause (eyetracker unplugged...) does not count.
                if (gapMs > 0)
                {
                    _bands[_previousBand].TimeMs += Math.Min(gapMs, 5000);
                }

                // Step between two measures in a row (none missed, no loss in between).
                if (!_inLoss && number == _previousNumber + 1 && gapMs > 0 && gapMs <= MaxStepGapMs)
                {
                    double dx = x - _previousX;
                    double dy = y - _previousY;
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    if (length <= ScreenMetrics.GazePx(JumpLimit4K))
                    {
                        b.Steps++;
                        b.SquaredSum += length * length;
                        if (length / gapMs * 1000.0 > AppSettings.Current.MaxFixationSpeedPx)
                        {
                            b.TooFast++;
                        }
                    }
                }
            }

            _hasPrevious = true;
            _inLoss = false;
            _previousNumber = number;
            _previousX = x;
            _previousY = y;
            _previousMs = instantMs;
            _previousBand = band;
        }

        /// <summary>Number of measures seen in this band.</summary>
        public long Measures(int band)
        {
            return _bands[band].Measures;
        }

        /// <summary>Agitation: RMS step length, in screen pixels (0 if no step yet).</summary>
        public double Agitation(int band)
        {
            Band b = _bands[band];
            return b.Steps > 0 ? Math.Sqrt(b.SquaredSum / b.Steps) : 0;
        }

        /// <summary>Share of the steps failing the fixation speed test, in % (0 if no step).</summary>
        public double TooFastPercent(int band)
        {
            Band b = _bands[band];
            return b.Steps > 0 ? 100.0 * b.TooFast / b.Steps : 0;
        }

        /// <summary>Gaze losses per minute spent in this band (0 if no time yet).</summary>
        public double LossesPerMinute(int band)
        {
            Band b = _bands[band];
            return b.TimeMs > 0 ? b.Losses / (b.TimeMs / 60000.0) : 0;
        }

        /// <summary>Share of the measures outside the screen, in % (0 if no measure).</summary>
        public double OffScreenPercent(int band)
        {
            Band b = _bands[band];
            return b.Measures > 0 ? 100.0 * b.OffScreen / b.Measures : 0;
        }

        /// <summary>Band of a height on the screen: top quarter, middle half, bottom quarter.</summary>
        private static int BandOf(double y, double screenHeight)
        {
            if (y < screenHeight / 4)
            {
                return Top;
            }
            if (y >= screenHeight * 3 / 4)
            {
                return Bottom;
            }
            return Middle;
        }
    }
}
