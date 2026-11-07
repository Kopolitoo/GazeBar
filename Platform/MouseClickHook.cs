using System;
using System.Runtime.InteropServices;

namespace Prunel
{
    /// <summary>
    /// MODEL: tells when a mouse button is pressed, in all programs (issue #96).
    ///
    /// The gaze keyboard cannot read the text of the application it types in: it only
    /// remembers what it typed itself (the "current word"). A click may put the text cursor
    /// somewhere else (another field of a form…), and then the current word is wrong. The
    /// keyboard therefore needs to know about EVERY click: a gaze click made by Prunel (direct
    /// click, action choice…), a click said to voice recognition (Dragon), or a click with a
    /// real mouse.
    ///
    /// How: like KeyboardHook, a "low-level mouse hook" (WH_MOUSE_LL). Windows calls OnMouse
    /// for every mouse event, wherever the cursor is. We only OBSERVE: the event always goes on
    /// to the programs (CallNextHookEx). Only button presses count (left, right, middle);
    /// moves and the wheel are ignored.
    ///
    /// Windows calls OnMouse on the thread that installed the hook (the UI thread) and every
    /// mouse move waits for the answer: it must be very quick. The event is raised afterwards,
    /// with BeginInvoke. The hook is installed only while the gaze keyboard is open.
    /// </summary>
    internal sealed class MouseClickHook : IDisposable
    {
        // Keep a reference to the function: otherwise the .NET "garbage collector" could
        // destroy it while Windows still calls it (crash).
        private readonly NativeMethods.LowLevelMouseProc _function;
        private IntPtr _hook = IntPtr.Zero;

        public MouseClickHook()
        {
            _function = OnMouse;
        }

        /// <summary>Raised (on the UI thread) when a mouse button is pressed.</summary>
        public event Action ButtonPressed;

        /// <summary>Installs the hook. Returns false if Windows refuses it (written to the log).</summary>
        public bool Install()
        {
            if (_hook != IntPtr.Zero)
            {
                return true;
            }

            _hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL, _function, NativeMethods.GetModuleHandle(null), 0);

            if (_hook == IntPtr.Zero)
            {
                App.Log("Mouse clicks: mouse hook refused (error " + Marshal.GetLastWin32Error() + ")");
                return false;
            }
            return true;
        }

        /// <summary>Removes the hook (nothing happens if it is not installed).</summary>
        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        /// <summary>Called by Windows for every mouse event. Must answer very quickly.</summary>
        private IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int message = wParam.ToInt32();
                if (message == NativeMethods.WM_LBUTTONDOWN ||
                    message == NativeMethods.WM_RBUTTONDOWN ||
                    message == NativeMethods.WM_MBUTTONDOWN)
                {
                    Action subscribers = ButtonPressed;
                    if (subscribers != null)
                    {
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(subscribers);
                    }
                }
            }

            // Always pass the event on to the next program.
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }
    }
}
