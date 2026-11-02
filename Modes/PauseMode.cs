namespace GazeBar
{
    /// <summary>
    /// PAUSE MODE (step 4 of the specification).
    ///
    /// Neither the circle nor the cursor follows the gaze: the overlay is empty, GazeBar
    /// does nothing. Handy for watching a video or reading without triggering anything.
    ///
    /// Only one thing stays active: looking down, below the screen, for [MenuDelay]
    /// opens the menu bar (the ModeManager handles it).
    /// From pause, the bar only shows the "Resume" button; when activated, it returns to
    /// movement mode (see MenuMode). The caregiver's Escape key also returns to movement mode.
    ///
    /// This mode replaces the temporary "idle mode" of steps 1 to 3, which was used by the
    /// toolbar (removed in step 4).
    /// </summary>
    internal sealed class PauseMode : Mode
    {
        public override string Name
        {
            get { return "Pause"; }
        }

        /// <summary>The menu opens from pause: it is the only way to leave it with the gaze.</summary>
        public override bool MenuAllowed
        {
            get { return true; }
        }

        /// <summary>
        /// The screen changed (step 11): this mode carries on, because it draws nothing, and the
        /// pause must not end on its own.
        /// </summary>
        public override bool AdaptToScreen()
        {
            return true;
        }

        public override void Enter(GazeState gaze)
        {
            Manager.Layer.HideAll();
        }
    }
}
