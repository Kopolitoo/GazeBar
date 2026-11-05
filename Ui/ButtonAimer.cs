using System.Collections.Generic;

namespace GazeBar
{
    /// <summary>
    /// LOGIC: decides which button (GazeButton) is looked at, and when it activates.
    /// It aims at any target implementing IGazeTarget (also the targets of
    /// settings mode).
    ///
    /// The action choice, the menu bar and the other screens all need it: so it is this
    /// small class, shared by the modes.
    ///
    /// The rules:
    ///  - on each NEW gaze measure, we look for the button underneath;
    ///  - to switch to another button (or to "none"), [_measuresToChange] measures in a row at
    ///    the same place are needed: an isolated measure (a small jump of the gaze) is ignored;
    ///  - the gaze resting on an active button for the activation delay activates it; its
    ///    progress ring fills up meanwhile;
    ///  - an empty or unavailable button never activates (only its label appears);
    ///  - gaze lost: the timer starts again from zero (a button that is no longer looked at
    ///    is never activated);
    ///  - after BlockUntilExit, the looked-at button cannot activate again until the gaze has
    ///    left it. Useful for a switch ("Mouse tracking"): otherwise, by keeping looking at
    ///    it, it would be turned on and off endlessly;
    ///  - after ResetAndBlock (the buttons were just replaced), the button under the gaze at
    ///    the first measure is blocked the same way. This way, a new button appearing under
    ///    the gaze does not activate on its own;
    ///  - on activation, the optional sound [Sound] is played (see SoundFeedback).
    /// </summary>
    internal sealed class ButtonAimer
    {
        /// <summary>
        /// Number of measures in a row needed on a new button to aim at it: 2 by default;
        /// 1 for the gaze keyboard (faster hover).
        /// </summary>
        private readonly int _measuresToChange;

        // The list of buttons, shared with the mode (which may fill it afterwards).
        // IReadOnlyList<IGazeTarget>: a list that can be read but not modified, of any
        // targets (GazeButton, SettingTarget...). Thanks to a C# rule called "covariance", a
        // List<GazeButton> can be given as is.
        private readonly IReadOnlyList<IGazeTarget> _buttons;

        // The "candidate" button seen on the last measures, and how many times in a row.
        private int _candidate;
        private int _candidateMeasures;
        private long _lastNumber;

        // Time (ms) when the gaze arrived on the aimed button; -1 = timer stopped.
        private double _aimStartMs;

        // True after BlockUntilExit, until the gaze changes button.
        private bool _blocked;

        // True after ResetAndBlock, until the first new measure.
        private bool _blockFirst;

        /// <summary>
        /// The sound played when a button activates: Button by default, Key for the gaze
        /// keyboard. It only plays if its setting is on (see SoundFeedback).
        /// </summary>
        public SoundKind Sound { get; set; } = SoundKind.Button;

        public ButtonAimer(IReadOnlyList<IGazeTarget> buttons) : this(buttons, 2)
        {
        }

        /// <summary>measuresToChange: see _measuresToChange (at least 1).</summary>
        public ButtonAimer(IReadOnlyList<IGazeTarget> buttons, int measuresToChange)
        {
            _buttons = buttons;
            _measuresToChange = measuresToChange < 1 ? 1 : measuresToChange;
            Reset();
        }

        /// <summary>The index of the aimed button in the list, or -1 if none.</summary>
        public int Aimed { get; private set; }

        /// <summary>Forgets everything (when entering a mode).</summary>
        public void Reset()
        {
            Aimed = -1;
            _candidate = -1;
            _candidateMeasures = 0;
            _lastNumber = -1;
            _aimStartMs = -1;
            _blocked = false;
            _blockFirst = false;
        }

        /// <summary>
        /// Forgets everything (the buttons were just replaced), and blocks the button that will
        /// be under the gaze at the next measure, until the gaze leaves it.
        /// </summary>
        public void ResetAndBlock()
        {
            Reset();
            _blockFirst = true;
        }

        /// <summary>
        /// Prevents the aimed button from activating again until the gaze has left it.
        /// </summary>
        public void BlockUntilExit()
        {
            _blocked = true;
            _aimStartMs = -1;
        }

        /// <summary>
        /// To call on each tick. Updates the aimed button and its timer, then draws the buttons
        /// (ring on the aimed button only).
        /// delaySeconds: the activation delay ([ActivationDelay] in general);
        /// showLabels: shows the label of the aimed button ([ShowLabels]).
        /// Returns the index of the button that just activated, or -1. When a button activates,
        /// the buttons are not redrawn: the mode is going to change something anyway.
        /// </summary>
        public int Update(GazeState gaze, double delaySeconds, bool showLabels)
        {
            double now = gaze.NowMs;

            if (!gaze.Available)
            {
                // Gaze lost: the timer starts again from zero.
                _aimStartMs = -1;
            }
            else if (gaze.Number != _lastNumber)
            {
                // New measure: which button is it on? (off screen → none)
                _lastNumber = gaze.Number;
                int seen = gaze.OffScreen ? -1 : ButtonUnder(gaze.X, gaze.Y);
                if (_blockFirst)
                {
                    // First measure after the buttons were replaced: the one under the gaze is
                    // aimed at once, but blocked.
                    _blockFirst = false;
                    Aimed = seen;
                    _blocked = seen >= 0;
                }
                else
                {
                    ChangeAimedIfNeeded(seen);
                }
            }

            // The timer of the aimed button (not while blocked).
            if (gaze.Available && Aimed >= 0 && !_blocked && _aimStartMs < 0)
            {
                _aimStartMs = now;
            }
            if (Aimed < 0)
            {
                _aimStartMs = -1;
            }

            // An empty or unavailable button never activates: no progress.
            double progress = 0;
            if (Aimed >= 0 && _aimStartMs >= 0 && _buttons[Aimed].Active)
            {
                progress = (now - _aimStartMs) / (delaySeconds * 1000.0);
            }

            if (progress >= 1)
            {
                int active = Aimed;
                _aimStartMs = -1;
                SoundFeedback.Play(Sound);
                return active;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool hover = i == Aimed;
                _buttons[i].Draw(hover ? progress : 0, hover, hover && showLabels);
            }
            return -1;
        }

        /// <summary>
        /// Changes the aimed button only if the new one is confirmed by [_measuresToChange]
        /// measures in a row. Changing button resets the timer and lifts the block.
        /// </summary>
        private void ChangeAimedIfNeeded(int seen)
        {
            if (seen == Aimed)
            {
                _candidateMeasures = 0;
                return;
            }

            if (seen == _candidate && _candidateMeasures > 0)
            {
                _candidateMeasures++;
            }
            else
            {
                _candidate = seen;
                _candidateMeasures = 1;
            }

            if (_candidateMeasures >= _measuresToChange)
            {
                Aimed = seen;
                _candidateMeasures = 0;
                _aimStartMs = -1;
                _blocked = false;
            }
        }

        /// <summary>The index of the button under this gaze point, or -1.</summary>
        private int ButtonUnder(double x, double y)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Contains(x, y))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
