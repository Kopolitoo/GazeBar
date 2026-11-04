using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// MODEL: simulates pressing and releasing keyboard keys.
    ///
    /// It is used by the Ctrl, Shift and Alt modifier keys of the action choice (see
    /// Modifiers.cs) and by the gaze keyboard (TypeText, TypeKey).
    /// It works like MouseInjector, with SendInput.
    ///
    /// Safety: as for the mouse, the class remembers the keys it pressed, so it can release
    /// them all (ReleaseAll) on error, on Escape or when closing. A Ctrl key left pressed
    /// would turn every click into Ctrl + click.
    /// </summary>
    internal static class KeyboardInjector
    {
        private static readonly object LockState = new object();

        // The codes of the keys currently pressed by GazeBar.
        private static readonly List<ushort> Pressed = new List<ushort>();

        /// <summary>Presses the key (if GazeBar has not already pressed it).</summary>
        public static void PressDown(ushort keyCode)
        {
            lock (LockState)
            {
                if (Pressed.Contains(keyCode))
                {
                    return;
                }
                if (Send(keyCode, 0))
                {
                    Pressed.Add(keyCode);
                }
            }
        }

        /// <summary>Releases the key (if GazeBar pressed it).</summary>
        public static void Release(ushort keyCode)
        {
            lock (LockState)
            {
                if (!Pressed.Contains(keyCode))
                {
                    return;
                }
                // Even if Windows refuses, we forget it: we will not retry endlessly.
                Send(keyCode, NativeMethods.KEYEVENTF_KEYUP);
                Pressed.Remove(keyCode);
            }
        }

        /// <summary>Releases all keys pressed by GazeBar, in the reverse order of pressing.</summary>
        public static void ReleaseAll()
        {
            lock (LockState)
            {
                for (int i = Pressed.Count - 1; i >= 0; i--)
                {
                    Send(Pressed[i], NativeMethods.KEYEVENTF_KEYUP);
                }
                Pressed.Clear();
            }
        }

        // ───────────────────────── Gaze keyboard ─────────────────────────
        //
        // These methods send a complete key (press AND release) in a single SendInput call:
        // Windows handles them as one block, a real key cannot slip in between, and nothing
        // stays pressed afterwards. So they do not need the "Pressed" list (which is used by
        // the modifiers of the action choice).

        /// <summary>
        /// Types a text, character by character, in UNICODE: the application receives exactly
        /// "é", "œ" or "€", whether Windows is set to a French, English or other keyboard.
        /// This is what will allow changing language without trouble.
        /// </summary>
        public static void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            List<NativeMethods.INPUT> entries = new List<NativeMethods.INPUT>();
            foreach (char c in text)
            {
                entries.Add(Character(c, 0));
                entries.Add(Character(c, NativeMethods.KEYEVENTF_KEYUP));
            }
            SendAll(entries, "text");
        }

        /// <summary>
        /// Presses then releases a real key (Tab, Enter, arrows, F1...), with optional modifiers
        /// (Ctrl, Shift, Alt, Windows), pressed before and released after, in reverse order.
        /// extent: "extended" key (arrows, Home, End, Delete...).
        /// </summary>
        public static void TypeKey(ushort code, bool extent, IList<ushort> modifiers)
        {
            List<NativeMethods.INPUT> entries = new List<NativeMethods.INPUT>();
            if (modifiers != null)
            {
                foreach (ushort m in modifiers)
                {
                    entries.Add(Key(m, m == NativeMethods.VK_LWIN, false));
                }
            }
            entries.Add(Key(code, extent, false));
            entries.Add(Key(code, extent, true));
            if (modifiers != null)
            {
                for (int i = modifiers.Count - 1; i >= 0; i--)
                {
                    ushort m = modifiers[i];
                    entries.Add(Key(m, m == NativeMethods.VK_LWIN, true));
                }
            }
            SendAll(entries, "key " + code);
        }

        /// <summary>A "Unicode character" input (press or release).</summary>
        private static NativeMethods.INPUT Character(char c, uint flagsValue)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.u.ki.wVk = 0;
            input.u.ki.wScan = c;
            input.u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE | flagsValue;
            return input;
        }

        /// <summary>
        /// A "key" input (press or release), with its hardware code (wScan): some applications
        /// read this code rather than the key code.
        /// </summary>
        private static NativeMethods.INPUT Key(ushort code, bool extent, bool release)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.u.ki.wVk = code;
            input.u.ki.wScan = (ushort)NativeMethods.MapVirtualKeyW(code, 0);
            uint flagsValue = 0;
            if (extent)
            {
                flagsValue |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            }
            if (release)
            {
                flagsValue |= NativeMethods.KEYEVENTF_KEYUP;
            }
            input.u.ki.dwFlags = flagsValue;
            return input;
        }

        /// <summary>Sends all the inputs at once. what: for the log on failure.</summary>
        private static void SendAll(List<NativeMethods.INPUT> entries, string what)
        {
            NativeMethods.INPUT[] array = entries.ToArray();
            uint sentCount = NativeMethods.SendInput((uint)array.Length, array, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (sentCount != array.Length)
            {
                // Known case: a window started as administrator refuses the keys simulated by
                // a normal program. The "full installation" has UI access and is not refused
                // (see UiAccess.cs).
                App.Log("SendInput (" + what + ") : " + sentCount + " inputs out of " + array.Length +
                        " accepted (Windows error " + Marshal.GetLastWin32Error() + ")");
            }
        }

        /// <summary>Sends a press (flags = 0) or a release (KEYEVENTF_KEYUP) to Windows.</summary>
        private static bool Send(ushort keyCode, uint flagsValue)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.u.ki.wVk = keyCode;
            input.u.ki.dwFlags = flagsValue;

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            uint sentCount = NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (sentCount == 0)
            {
                App.Log("SendInput (keyboard) failed (Windows error " + Marshal.GetLastWin32Error() + ")");
                return false;
            }
            return true;
        }
    }
}
