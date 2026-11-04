namespace GazeBar
{
    /// <summary>
    /// A "snapshot" of the gaze at a given moment, prepared by the ModeManager for the modes.
    /// This way, each mode does not have to redo the same checks (gaze lost? off screen?).
    ///
    /// "struct": a small bundle of values, copied as is (like a number), not a shared object.
    /// </summary>
    internal struct GazeState
    {
        /// <summary>True if a recent measure exists (less than [MaxMeasureAge] ms). Otherwise: gaze lost (eyes closed, head out of range...).</summary>
        public bool Available;

        /// <summary>Gaze position, in screen pixels. Can be negative or beyond the screen.</summary>
        public double X;
        public double Y;

        /// <summary>
        /// Gaze position BROUGHT BACK INTO THE SCREEN (each axis clamped separately at the edges).
        /// Example: gaze above the screen → YScreen = 0, but XScreen still follows the gaze from
        /// left to right. Used to slide the gaze circles along the edges when looking off
        /// screen.
        /// Also used by fixation and fixed gaze mode, but only when the gaze is very close
        /// to the screen (see NearScreen); never by the buttons.
        /// Only valid if Available is true.
        /// </summary>
        public double XScreen;
        public double YScreen;

        /// <summary>
        /// Measure number (goes up by 1 for each measure received from the PCEye). If the number
        /// has not changed since the previous tick, it is the same measure: nothing new.
        /// </summary>
        public long Number;

        /// <summary>Time when the measure arrived, in milliseconds.</summary>
        public double MeasureTimeMs;

        /// <summary>
        /// The time of this tick, in milliseconds, on the same clock as MeasureTimeMs.
        /// Always set, even when the gaze is not available. The modes use it to measure
        /// durations (fixation delay, activation delay...).
        /// </summary>
        public double NowMs;

        /// <summary>True if the gaze is available but outside the screen.</summary>
        public bool OffScreen;

        /// <summary>True if the gaze is available and BELOW the bottom of the screen (used to open the menu).</summary>
        public bool BelowScreen;

        /// <summary>
        /// True if the gaze is available and on the screen, OR right next to it: less than
        /// [EdgesMargin4K] pixels (measured on the 4K screen) from its edges.
        /// In the corners, the PCEye is less precise: when looking at a corner,
        /// some measures fall slightly off screen, which kept resetting the fixation. In this
        /// band, fixation and fixed gaze mode use the position brought back into the screen
        /// (XScreen, YScreen) instead of forgetting everything.
        /// </summary>
        public bool NearScreen;

        /// <summary>True if the gaze is available and on the screen: the "normal" case.</summary>
        public bool OnScreen
        {
            get { return Available && !OffScreen; }
        }
    }
}
