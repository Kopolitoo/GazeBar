using System;

namespace GazeBar
{
    /// <summary>
    /// MODEL: the [ScrollInactiveWindows] option (user request of 2026-11-02, "Scroll" tab
    /// of the settings).
    ///
    /// It is the Windows 10/11 setting "Scroll inactive windows when I hover over them"
    /// (Windows Settings → Bluetooth & devices → Mouse). It decides which window Windows
    /// sends the wheel to:
    ///   - on: to the window UNDER THE CURSOR, even if it is not active. This is what
    ///     GazeBar's scroll mode needs, since it puts the cursor on the aimed point;
    ///   - off: always to the ACTIVE window, even if the cursor is elsewhere. A window must
    ///     then be clicked before it can be scrolled.
    ///
    /// GazeBar only changes this Windows setting, with the SystemParametersInfo function
    /// (provided by Windows: no new dependency). Windows saves the change: it stays valid
    /// even when GazeBar is closed.
    ///
    /// Values of the setting in Windows: 0 = active window, 1 = hybrid (active window for
    /// classic programs, window under the cursor for Store apps), 2 = window under the cursor.
    /// </summary>
    internal static class WheelScroller
    {
        private const uint ActiveWindow = 0;          // MOUSEWHEEL_ROUTING_FOCUS
        private const uint UnderCursor = 2;          // MOUSEWHEEL_ROUTING_MOUSE_POS

        /// <summary>
        /// Reads the current Windows setting. Returns null if it cannot be read (Windows too
        /// old, for example).
        /// </summary>
        public static uint? Read()
        {
            try
            {
                uint val = 0;
                if (NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETMOUSEWHEELROUTING, 0, ref val, 0))
                {
                    return val;
                }
            }
            catch (Exception ex)
            {
                App.Log("Wheel: cannot read the Windows setting: " + ex.Message);
            }
            return null;
        }

        /// <summary>Describes the current Windows setting, for the log.</summary>
        public static string Description()
        {
            uint? val = Read();
            if (val == null)
            {
                return "unknown";
            }
            if (val.Value == UnderCursor)
            {
                return "on";
            }
            return "off (" + val.Value + ")";
        }

        /// <summary>
        /// Makes the Windows setting match GazeBar's option (active = true: the wheel goes to
        /// the window under the cursor). Does nothing if it already matches. Never crashes
        /// the program: an error is only written to the log.
        ///
        /// Called when GazeBar starts and each time the option is changed in settings mode
        /// (like WindowsStartup.Apply).
        /// </summary>
        public static void Apply(bool active)
        {
            try
            {
                uint wanted = active ? UnderCursor : ActiveWindow;
                uint? current = Read();
                if (current == wanted)
                {
                    return;
                }

                // SPIF_UPDATEINIFILE: Windows saves the change (it survives a restart).
                // SPIF_SENDCHANGE: open programs are notified.
                uint flagsValue = NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE;

                // Microsoft's documentation says the value is passed by its ADDRESS
                // ("pointer"). Some programs pass it directly instead of the address: if the
                // first way did not work, we try the second one. In both cases, we read the
                // setting back to check.
                uint val = wanted;
                NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETMOUSEWHEELROUTING, 0, ref val, flagsValue);
                if (Read() != wanted)
                {
                    NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETMOUSEWHEELROUTING, 0, new IntPtr(wanted), flagsValue);
                }

                App.Log("Wheel: Windows setting \"scroll inactive windows\" " +
                        (active ? "turned on" : "turned off") + " by GazeBar, result: " + Description());
            }
            catch (Exception ex)
            {
                App.Log("Wheel: cannot change the Windows setting: " + ex.Message);
            }
        }
    }
}
