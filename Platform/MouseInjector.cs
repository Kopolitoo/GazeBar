using System;
using System.Runtime.InteropServices;

namespace Prunel
{
    /// <summary>Which mouse button.</summary>
    internal enum MouseSide
    {
        Left,
        Right
    }

    /// <summary>
    /// MODEL: simulates pressing and releasing the mouse buttons, and moving
    /// the cursor during a drag and drop.
    ///
    /// Clicks happen where the cursor already is (the modes place it beforehand with
    /// SetCursorPos).
    ///
    /// For drag and drop, the cursor is moved by MoveTo, which goes through
    /// SendInput like a real mouse: applications then receive the same "the mouse moves"
    /// messages as with a physical mouse, which is needed for Explorer or Paint to
    /// recognize a drag.
    ///
    /// Safety: the class remembers the buttons it pressed, so it can release them all
    /// (ReleaseAll) when closing or on error. A stuck button would be very annoying.
    ///
    /// Known Windows limit: a normal program cannot simulate the mouse over a window started
    /// "as administrator". To act on such a window, Prunel would also have to be started as
    /// administrator.
    /// </summary>
    internal static class MouseInjector
    {
        private static readonly object Lock = new object();
        private static bool _leftDown;
        private static bool _rightDown;

        /// <summary>Presses the button (if it is not already pressed).</summary>
        public static void Press(MouseSide side)
        {
            lock (Lock)
            {
                if (IsDown(side))
                {
                    return;
                }

                uint flag = side == MouseSide.Left
                    ? NativeMethods.MOUSEEVENTF_LEFTDOWN
                    : NativeMethods.MOUSEEVENTF_RIGHTDOWN;

                if (Send(flag))
                {
                    SetDown(side, true);
                }
            }
        }

        /// <summary>Releases the button (if it is pressed).</summary>
        public static void Release(MouseSide side)
        {
            lock (Lock)
            {
                if (!IsDown(side))
                {
                    return;
                }

                uint flag = side == MouseSide.Left
                    ? NativeMethods.MOUSEEVENTF_LEFTUP
                    : NativeMethods.MOUSEEVENTF_RIGHTUP;

                if (Send(flag))
                {
                    SetDown(side, false);
                }
            }
        }

        /// <summary>A simple click: press then release, where the cursor is.</summary>
        public static void Click(MouseSide side)
        {
            Press(side);
            Release(side);
        }

        /// <summary>
        /// A left double click: two clicks sent right after each other.
        /// Windows recognizes them as a double click if they arrive within the "double-click
        /// time" set in Windows (500 ms by default) and at the same place: this is the case
        /// here, since they are a few microseconds apart without moving the cursor.
        /// </summary>
        public static void DoubleClick()
        {
            Click(MouseSide.Left);
            Click(MouseSide.Left);
        }

        /// <summary>Releases all the buttons we pressed.</summary>
        public static void ReleaseAll()
        {
            Release(MouseSide.Left);
            Release(MouseSide.Right);
        }

        /// <summary>True if one of the two buttons is pressed by Prunel.</summary>
        public static bool AnyPressed
        {
            get
            {
                lock (Lock)
                {
                    return _leftDown || _rightDown;
                }
            }
        }

        /// <summary>
        /// Moves the cursor to the point (x, y), in screen pixels, as a real mouse would
        /// (drag and drop).
        ///
        /// With MOUSEEVENTF_ABSOLUTE, Windows does not expect pixels but a position between 0
        /// and 65535 on each axis (0 = left or top edge, 65535 = right or bottom edge of the
        /// primary screen). So we convert: position = x × 65535 ÷ (width − 1).
        /// Rounding can shift the cursor by one pixel: at the end of a drag, the mode puts the
        /// cursor back exactly in place with SetCursorPos before releasing.
        /// </summary>
        public static void MoveTo(double x, double y)
        {
            // Size measured by ScreenMetrics (the real resolution, even after a change).
            double width = ScreenMetrics.WidthPixels;
            double height = ScreenMetrics.HeightPixels;
            if (width < 2 || height < 2)
            {
                return;
            }

            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_MOUSE;
            input.u.mid.dx = (int)Math.Round(x * 65535.0 / (width - 1));
            input.u.mid.dy = (int)Math.Round(y * 65535.0 / (height - 1));
            input.u.mid.dwFlags = NativeMethods.MOUSEEVENTF_MOVE | NativeMethods.MOUSEEVENTF_ABSOLUTE;
            SendOne(input);
        }

        /// <summary>
        /// Moves the cursor by (dx, dy) pixels from its current position, like a real mouse
        /// being pushed ("relative" game mode).
        ///
        /// Without MOUSEEVENTF_ABSOLUTE, dx and dy are an OFFSET and not a position. This is
        /// what games read as "raw input". For other software, Windows may apply mouse
        /// acceleration (the "Enhance pointer precision" option): the cursor may then go a
        /// little further than the requested offset.
        /// </summary>
        public static void MoveBy(int dx, int dy)
        {
            if (dx == 0 && dy == 0)
            {
                return;
            }

            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_MOUSE;
            input.u.mid.dx = dx;
            input.u.mid.dy = dy;
            input.u.mid.dwFlags = NativeMethods.MOUSEEVENTF_MOVE;
            SendOne(input);
        }

        /// <summary>
        /// A wheel movement (scroll mode), where the cursor is.
        ///
        /// amount: in Windows "wheel units". One notch of a real wheel is 120 (WHEEL_DELTA);
        /// less can be sent (scroll mode sends from 8 units, see ScrollMode.MinStep) for a
        /// smoother scroll: most recent software (browsers, Word, Explorer) adds up these
        /// small amounts.
        /// Direction, like a real wheel:
        ///   - vertical: positive = up, negative = down;
        ///   - horizontal: positive = right, negative = left.
        ///
        /// mouseData is declared "uint" (unsigned number) but Windows reads it as a signed
        /// number: "unchecked" lets us store a negative number in it as is.
        /// </summary>
        public static void Wheel(int amount, bool horizontal)
        {
            if (amount == 0)
            {
                return;
            }

            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_MOUSE;
            input.u.mid.mouseData = unchecked((uint)amount);
            input.u.mid.dwFlags = horizontal ? NativeMethods.MOUSEEVENTF_HWHEEL : NativeMethods.MOUSEEVENTF_WHEEL;
            SendOne(input);
        }

        private static bool IsDown(MouseSide side)
        {
            return side == MouseSide.Left ? _leftDown : _rightDown;
        }

        private static void SetDown(MouseSide side, bool value)
        {
            if (side == MouseSide.Left)
            {
                _leftDown = value;
            }
            else
            {
                _rightDown = value;
            }
        }

        /// <summary>Sends a button action to Windows. Returns true if Windows accepted it.</summary>
        private static bool Send(uint buttonFlag)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_MOUSE;
            input.u.mid.dwFlags = buttonFlag;   // the other fields stay at 0: no movement
            return SendOne(input);
        }

        /// <summary>Sends a simulated input to Windows. Returns true if Windows accepted it.</summary>
        private static bool SendOne(NativeMethods.INPUT input)
        {
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            uint sent = NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));

            if (sent == 0)
            {
                App.Log("SendInput failed (Windows error " + Marshal.GetLastWin32Error() + ")");
                return false;
            }
            return true;
        }
    }
}
