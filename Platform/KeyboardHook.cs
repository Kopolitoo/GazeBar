using System;
using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// MODEL: watches the Escape key, in all programs.
    ///
    /// The specification asks for "always a way out": a keyboard key (Escape) returns to
    /// movement mode, for the caregiver. It is all the more useful since GazeBar has
    /// no toolbar that could be clicked with the mouse.
    ///
    /// How: we install a "low-level keyboard hook" (WH_KEYBOARD_LL). Windows then calls our
    /// OnKey function on every key press, wherever the focus is.
    /// We only observe: the key is always passed on to the active program
    /// (CallNextHookEx). Escape therefore still closes a dialog box, for example.
    ///
    /// Windows calls OnKey on the UI thread (the one that installed the hook), while it
    /// processes its messages: so we could change mode directly. But we must answer quickly
    /// (otherwise Windows removes the hook): the event is raised afterwards, with BeginInvoke.
    /// </summary>
    internal sealed class KeyboardHook : IDisposable
    {
        // Keep a reference to the function: otherwise the .NET "garbage collector" could
        // destroy it while Windows still calls it (crash).
        private readonly NativeMethods.LowLevelKeyboardProc _function;
        private IntPtr _hook = IntPtr.Zero;

        public KeyboardHook()
        {
            _function = OnKey;
        }

        /// <summary>
        /// Raised (on the UI thread) when the Escape key is pressed. The argument is true when
        /// the key was simulated by other software (for example Dragon, by voice), false for a
        /// real keyboard.
        /// </summary>
        public event Action<bool> EscapePressed;

        /// <summary>Installs the hook. Returns false if Windows refuses it (written to the log).</summary>
        public bool Install()
        {
            if (_hook != IntPtr.Zero)
            {
                return true;
            }

            // For a low-level hook, Windows asks for the program's "module";
            // GetModuleHandle(null) gives the one of GazeBar.exe.
            _hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL, _function, NativeMethods.GetModuleHandle(null), 0);

            if (_hook == IntPtr.Zero)
            {
                App.Log("Escape key: keyboard hook refused (error " + Marshal.GetLastWin32Error() + ")");
                return false;
            }
            App.Log("Escape key: watched");
            return true;
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        /// <summary>Called by Windows for every key. Must answer very quickly.</summary>
        private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
        {
            // code >= 0: the message must be handled. wParam says "pressed" or "released".
            // lParam points to a structure whose first field is the key code.
            if (code >= 0)
            {
                int message = wParam.ToInt32();
                if (message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN)
                {
                    int key = Marshal.ReadInt32(lParam);
                    // The Escape key of the gaze keyboard is SIMULATED by GazeBar.
                    // It must reach the application, but must neither return to movement mode
                    // nor close the keyboard: GazeBar's own keys carry a mark (dwExtraInfo, see
                    // KeyboardInjector) and are ignored. A key simulated by OTHER software
                    // (Dragon, when the user says "Escape") counts (issue #51).
                    int flagsValue = Marshal.ReadInt32(lParam, NativeMethods.HookFlagsOffset);
                    bool simulated = (flagsValue & NativeMethods.LLKHF_INJECTED) != 0;
                    long extraInfo = Marshal.ReadIntPtr(lParam, NativeMethods.HookExtraInfoOffset).ToInt64();
                    bool fromGazeBar = simulated && extraInfo == NativeMethods.GazeBarKeyMarker;
                    if (key == NativeMethods.VK_ESCAPE && !fromGazeBar)
                    {
                        Action<bool> subscribers = EscapePressed;
                        if (subscribers != null)
                        {
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(subscribers, simulated);
                        }
                    }
                }
            }

            // Always pass the key on to the next program.
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }
    }
}
