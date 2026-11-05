using System;

namespace Prunel
{
    /// <summary>
    /// GAZE SMOOTHING, shared by every mode that makes a circle follow the gaze.
    ///
    /// ─── Why a single-stage smoothing "stutters" ───
    ///
    /// The simplest smoothing does on each tick: position += (gaze − position) × [Smoothing].
    /// This is a "single-stage" smoothing. It has two flaws:
    ///
    ///  1. The PCEye 5 only sends about 30 measures per second, while the screen redraws 60
    ///     times per second. So the gaze moves in STAIR steps. On each new step, the circle
    ///     suddenly starts fast again, then slows down, then starts again...: its speed jerks
    ///     about 30 times per second. The eye sees these jerks.
    ///
    ///  2. The fraction was applied "per tick". If a tick arrives late (Windows is not
    ///     perfectly regular), the circle still moves by the same fraction: its movement
    ///     depends on the clock's irregularities.
    ///
    /// ─── What this smoother does ───
    ///
    ///  1. TWO STAGES in a row ("double smoothing"): the first stage follows the gaze, the
    ///     second follows the first. The second stage receives an already rounded curve: the
    ///     circle's speed does not jerk, it varies smoothly. The gaze jitter is also better
    ///     filtered. Each stage is twice as fast as a single smoothing: the average lag
    ///     behind the gaze stays the same, for the same [Smoothing] setting.
    ///
    ///  2. The smoothing depends on the REAL elapsed time, not on the number of ticks: a late
    ///     tick moves the circle a bit more, an early tick a bit less. The movement is regular.
    ///
    /// The [Smoothing] setting keeps the same meaning as before: "the fraction of the way
    /// covered in 16 milliseconds (one reference tick)". Smaller = calmer, larger = more
    /// responsive. At 1, the circle is directly on the gaze (no smoothing).
    ///
    /// ─── A bit of maths, to understand ───
    ///
    /// With a fraction L per 16 ms tick, the remaining distance is multiplied by (1 − L) on
    /// each tick. One can write (1 − L) = e^(−16 / τ), where τ ("tau") is the TIME CONSTANT:
    /// τ = −16 / ln(1 − L). Example: L = 0.16 → τ ≈ 92 ms. For any duration dt, the remaining
    /// distance is then multiplied by e^(−dt / τ): it is the same thing, but valid whatever dt.
    /// Each of the two stages uses τ / 2.
    ///
    /// Use in a mode:
    ///     private readonly Smoother _smoothed = new Smoother();
    ///     _smoothed.Place(x, y);                              // start, without sliding
    ///     _smoothed.Follow(gaze.X, gaze.Y, gaze.NowMs);       // on each tick
    ///     ... _smoothed.X, _smoothed.Y ...                    // the smoothed position
    /// </summary>
    internal sealed class Smoother
    {
        /// <summary>Reference duration of a tick, in ms: [Smoothing] is "the fraction covered in 16 ms".</summary>
        private const double ReferenceTickMs = 16;

        /// <summary>
        /// Maximum gap taken into account between two calls, in ms. After a long pause (gaze
        /// lost, mode suspended...), we do not want the circle to jump onto the gaze at once:
        /// it starts again sliding, as after a slightly long tick.
        /// </summary>
        private const double MaxGapMs = 50;

        // Where to read the smoothing setting: [Smoothing] usually, [GameSmoothing] in game mode.
        private readonly Func<double> _readSmoothing;

        /// <summary>Usual smoother, using the [Smoothing] setting.</summary>
        public Smoother()
            : this(() => AppSettings.Current.Smoothing)
        {
        }

        /// <summary>
        /// Smoother that reads its smoothing elsewhere (game mode uses [GameSmoothing]).
        /// readSmoothing is a function, called on each tick: a change of the setting applies
        /// immediately.
        /// </summary>
        public Smoother(Func<double> readSmoothing)
        {
            _readSmoothing = readSmoothing;
        }

        // First stage (follows the gaze).
        private double _x1;
        private double _y1;

        // Time of the last call to Follow, in ms (−1 = no call yet since Place).
        private double _lastMs = -1;

        /// <summary>Smoothed position (second stage), in screen pixels.</summary>
        public double X { get; private set; }

        /// <summary>Smoothed position (second stage), in screen pixels.</summary>
        public double Y { get; private set; }

        /// <summary>Places the circle directly at (x, y), without sliding (both stages).</summary>
        public void Place(double x, double y)
        {
            _x1 = x;
            _y1 = y;
            X = x;
            Y = y;
            _lastMs = -1;
        }

        /// <summary>
        /// Moves the smoothed position towards the target (targetX, targetY), according to the
        /// time elapsed since the previous call. nowMs = gaze.NowMs.
        /// </summary>
        public void Follow(double targetX, double targetY, double nowMs)
        {
            // 1. Time elapsed since the previous call (first call: one reference tick).
            double dt = _lastMs < 0 ? ReferenceTickMs : nowMs - _lastMs;
            _lastMs = nowMs;
            if (dt <= 0)
            {
                return; // same time: nothing to do
            }
            if (dt > MaxGapMs)
            {
                dt = MaxGapMs;
            }

            // 2. Smoothing 1 (or more): no smoothing, the circle is on the target.
            double smoothing = _readSmoothing();
            if (smoothing >= 0.999)
            {
                Place(targetX, targetY);
                _lastMs = nowMs;
                return;
            }

            // 3. Time constant of each stage (half of τ, see the explanation at the top).
            double tau = -ReferenceTickMs / Math.Log(1 - smoothing);
            double stageTau = tau / 2;

            // 4. Fraction of the way covered during dt: 1 − e^(−dt / τ).
            double fraction = 1 - Math.Exp(-dt / stageTau);

            // 5. First stage: towards the target. Second stage: towards the first.
            _x1 += (targetX - _x1) * fraction;
            _y1 += (targetY - _y1) * fraction;
            X += (_x1 - X) * fraction;
            Y += (_y1 - Y) * fraction;
        }
    }
}
