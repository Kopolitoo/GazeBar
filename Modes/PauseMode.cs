namespace Prunel
{
    /// <summary>
    /// PAUSE MODE.
    ///
    /// Neither the circle nor the cursor follows the gaze: the overlay is empty, Prunel
    /// does nothing. Handy for watching a video or reading without triggering anything.
    ///
    /// Only one thing stays active: looking down, below the screen, for [MenuDelay]
    /// opens the menu bar (the ModeManager handles it).
    /// From pause, the bar only shows the "Resume" button; when activated, it returns to
    /// movement mode (see MenuMode). The caregiver's Escape key also returns to movement mode.
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
        /// The screen changed: this mode carries on, because it draws nothing, and the
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
