namespace GazeBar
{
    /// <summary>
    /// What a "target" that can be activated with the gaze must provide, so that ButtonAimer
    /// can decide whether it is being looked at and when it activates.
    ///
    /// ButtonAimer aims at GazeButton objects (the squares of the action choice and of the
    /// menu, drawn on the overlay), but settings mode has other targets: the
    /// tabs, the "−" and "+" buttons, the yes / no switches... (SettingTarget).
    /// Rather than writing a second aimer, we describe here what they have in common:
    /// this is an "interface". A class that "implements" the interface (written
    /// "class GazeButton : IGazeTarget") promises to provide these three members.
    ///
    /// Note: Contains receives the gaze point in the target's coordinate system. For a
    /// GazeButton these are screen pixels; for a SettingTarget these are the coordinates
    /// of the settings page (see SettingsMode).
    /// </summary>
    internal interface IGazeTarget
    {
        /// <summary>False for a target that never activates (empty slot, future feature).</summary>
        bool Active { get; }

        /// <summary>True if the gaze point is on the target.</summary>
        bool Contains(double x, double y);

        /// <summary>
        /// Updates the target's look: progress from 0 to 1 (ring or bar), hover,
        /// and whether the label is shown.
        /// </summary>
        void Draw(double progress, bool hover, bool showLabel);
    }
}
