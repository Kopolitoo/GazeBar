namespace GazeBar
{
    /// <summary>
    /// What every mode can do. This is an "abstract" class: we never create a plain "Mode",
    /// but a specific mode (MovementMode, FixedGazeMode, MenuMode...) that inherits from it
    /// and fills in the methods it cares about.
    ///
    /// The ModeManager calls these methods at the right time:
    ///   Enter(gaze)     once, when the mode becomes the current mode. It receives the
    ///                   current gaze position, to start at the right place (and not at a
    ///                   default place such as the center of the screen);
    ///   Update()        about 60 times per second, with the latest gaze position;
    ///   Quit()          once, when switching to another mode (to tidy everything up).
    ///
    /// To avoid a visual jump from one mode to the next, the circle is NOT hidden in Quit:
    /// the next mode takes it over where it is (Layer.CircleX, Layer.CircleY) and moves it
    /// smoothly. A mode that does not want a circle hides it in its Enter.
    ///
    /// "virtual": the method has a default behaviour (do nothing), which a mode can
    /// replace with "override".
    /// </summary>
    internal abstract class Mode
    {
        /// <summary>Readable name of the mode, for the log and the display.</summary>
        public abstract string Name { get; }

        /// <summary>
        /// True if looking down below the screen can open the menu during this mode.
        ///
        /// The menu ONLY opens from movement,
        /// pause and game modes. The other modes have their own way out: a "back" button in
        /// the magnifier, the action choice and the drag and drop.
        /// The default answer is therefore "no"; only MovementMode, PauseMode and GameMode
        /// replace it with "yes" (with override).
        /// </summary>
        public virtual bool MenuAllowed
        {
            get { return false; }
        }

        /// <summary>The manager that drives this mode. Given by the ModeManager before Enter(gaze).</summary>
        protected ModeManager Manager { get; private set; }

        /// <summary>Called by the ModeManager only (hence "internal").</summary>
        internal void Attach(ModeManager manager)
        {
            Manager = manager;
        }

        public virtual void Enter(GazeState gaze)
        {
        }

        public virtual void Update(GazeState gaze)
        {
        }

        public virtual void Quit()
        {
        }

        /// <summary>
        /// Called when the Windows resolution, scaling or text size changed during this mode
        /// (see ScreenMetrics.cs).
        /// Returns true if the mode adapted itself (for example movement mode, which redraws
        /// everything on each tick with the new sizes). Returns false (default) if the mode
        /// cannot carry on: its buttons and its photo were placed for the old screen. The
        /// manager then returns to movement mode.
        /// </summary>
        public virtual bool AdaptToScreen()
        {
            return false;
        }
    }
}
