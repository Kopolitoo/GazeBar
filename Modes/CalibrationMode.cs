using System;
using System.Diagnostics;
using System.Text;

namespace GazeBar
{
    /// <summary>
    /// CALIBRATION MODE (step 4 of the specification).
    ///
    /// We get here from the "Tools" tab of the settings (before 2026-10-29: from the menu
    /// bar), once the eyetracker calibration tool has been started (see Calibration.cs).
    /// During calibration, GazeBar must do nothing: the overlay is empty, nothing follows
    /// the gaze (otherwise the circle would get in the way and a fixation on a calibration
    /// point could trigger an action).
    ///
    /// "Back to movement mode when the tool closes": the eyetracker does not tell us when it
    /// closes. We guess it this way:
    ///  1. when started, the eyetracker tool opens its window, which becomes the active
    ///     window. So we note the active window at the start (the one of the application we
    ///     were using), then wait for ANOTHER window to become active, not belonging to
    ///     GazeBar, and remember it (its title is written to the log);
    ///  2. when that window no longer exists or is hidden, calibration is over: back to
    ///     movement mode.
    /// Safety nets: if no window appears within [WindowWaitMs], or if calibration lasts more
    /// than [MaxDurationMs], we return to movement mode. Looking down below the screen
    /// always opens the menu, and the Escape key returns to movement mode.
    ///
    /// TO CHECK on the PCEye 5: that the calibration window really becomes the active
    /// window (the log will tell).
    /// </summary>
    internal sealed class CalibrationMode : Mode
    {
        /// <summary>Maximum wait for the tool's window, in milliseconds.</summary>
        private const double WindowWaitMs = 15000;

        /// <summary>Maximum calibration duration, in milliseconds (5 minutes).</summary>
        private const double MaxDurationMs = 5 * 60 * 1000;

        private double _startMs;
        private IntPtr _previousWindow = IntPtr.Zero;
        private IntPtr _toolWindow = IntPtr.Zero;
        private uint _myProcess;

        /// <summary>
        /// previousWindow: the active window just BEFORE the tool was started (noted by
        /// SettingsMode). So it is not the tool's window.
        /// </summary>
        public CalibrationMode(IntPtr previousWindow)
        {
            _previousWindow = previousWindow;
        }

        public override string Name
        {
            get { return "Calibration"; }
        }

        /// <summary>
        /// The screen changed (step 11): this mode carries on, because the eyetracker
        /// calibration must not be interrupted.
        /// </summary>
        public override bool AdaptToScreen()
        {
            return true;
        }

        public override void Enter(GazeState gaze)
        {
            Manager.Layer.HideAll();
            _startMs = gaze.NowMs;
            _toolWindow = IntPtr.Zero;
            _myProcess = (uint)Process.GetCurrentProcess().Id;
        }

        public override void Update(GazeState gaze)
        {
            double elapsedMs = gaze.NowMs - _startMs;

            if (_toolWindow == IntPtr.Zero)
            {
                // 1. Wait for the tool's window.
                IntPtr active = NativeMethods.GetForegroundWindow();
                if (active != IntPtr.Zero && active != _previousWindow && !BelongsToGazeBar(active))
                {
                    _toolWindow = active;
                    App.Log("Calibration: tool window \"" + Title(active) + "\"");
                }
                else if (elapsedMs > WindowWaitMs)
                {
                    App.Log("Calibration: no tool window seen, back to movement mode");
                    Manager.ChangeMode(new MovementMode());
                }
                return;
            }

            // 2. Wait for it to close.
            if (!NativeMethods.IsWindow(_toolWindow) || !NativeMethods.IsWindowVisible(_toolWindow))
            {
                App.Log("Calibration: tool closed, back to movement mode");
                Manager.ChangeMode(new MovementMode());
            }
            else if (elapsedMs > MaxDurationMs)
            {
                App.Log("Calibration: maximum duration exceeded, back to movement mode");
                Manager.ChangeMode(new MovementMode());
            }
        }

        /// <summary>True if the window belongs to GazeBar itself.</summary>
        private bool BelongsToGazeBar(IntPtr window)
        {
            uint process;
            NativeMethods.GetWindowThreadProcessId(window, out process);
            return process == _myProcess;
        }

        private static string Title(IntPtr window)
        {
            StringBuilder text = new StringBuilder(256);
            NativeMethods.GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }
    }
}
