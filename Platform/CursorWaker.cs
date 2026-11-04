using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// MODEL: makes the cursor reappear if Windows hid it (fix of 2026-10-29).
    ///
    /// The observed problem: when GazeBar is started alone at Windows startup, the cursor
    /// stays invisible; it only appears after a drag and drop.
    ///
    /// The explanation: when Windows thinks there is no mouse (no physical mouse has moved
    /// since startup), it "suppresses" the cursor. It only shows it again on the next mouse
    /// movement. But movement mode places the cursor with SetCursorPos: for Windows, this is
    /// NOT a mouse movement, only a change of position. Drag and drop, on the other hand,
    /// goes through SendInput (MouseInjector.MoveTo), which imitates a real mouse: that is
    /// why it made the cursor reappear.
    ///
    /// The solution: when GazeBar starts, we ask Windows for the cursor state
    /// (GetCursorInfo). If it is "suppressed", we simulate a tiny mouse nudge through
    /// SendInput (1 pixel right then 1 pixel left), then put the cursor back exactly in place
    /// with SetCursorPos. The cursor reappears without moving.
    /// A single check, at startup (the user's choice after testing, 2026-10-29).
    /// </summary>
    internal static class CursorWaker
    {
        /// <summary>If Windows suppressed the cursor, simulates a small mouse nudge.</summary>
        public static void Check()
        {
            NativeMethods.CURSORINFO info = new NativeMethods.CURSORINFO();
            info.cbSize = Marshal.SizeOf(typeof(NativeMethods.CURSORINFO));
            if (!NativeMethods.GetCursorInfo(ref info))
            {
                return;   // cannot read it: do nothing
            }

            // "&" tests whether the CURSOR_SUPPRESSED flag is part of flags.
            bool removed = (info.flags & NativeMethods.CURSOR_SUPPRESSED) != 0;
            if (!removed)
            {
                return;
            }

            App.Log("Cursor hidden by Windows: small simulated mouse nudge to show it");

            // The current cursor position, to put it back exactly after the nudge.
            NativeMethods.POINT before;
            bool knownPosition = NativeMethods.GetCursorPos(out before);

            // "Relative" nudge (like a real mouse): 1 pixel right, then back.
            MouseInjector.MoveBy(1, 0);
            MouseInjector.MoveBy(-1, 0);

            // Windows mouse acceleration can distort the one-pixel round trip:
            // so we put the cursor back at its exact position.
            if (knownPosition)
            {
                NativeMethods.SetCursorPos(before.X, before.Y);
            }
        }
    }
}
