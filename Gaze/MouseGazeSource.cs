using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>
    /// Developer mode: the PHYSICAL mouse plays the gaze, to test GazeBar without an
    /// eyetracker. Only used when GazeBar is started with the "--mouse" option (see App).
    ///
    /// Why not simply read the cursor position? Because GazeBar moves the cursor itself
    /// (movement mode, magnifier...): it would read back its own moves, and the cursor would
    /// fight the hand. So this source keeps its own "virtual gaze" point, moved only by the
    /// movements of the real mouse:
    ///  - Windows sends them as "raw input" (WM_INPUT messages) to a small invisible window;
    ///  - the movements GazeBar injects itself (SendInput) come with no device: they are
    ///    ignored. SetCursorPos produces no raw input at all.
    ///
    /// The virtual point can go a little outside the screen ([OffScreenMarginRatio]), like a
    /// real gaze: this is how the menu is opened, by "looking" below the screen.
    ///
    /// A timer sends the point about 30 times per second, like the PCEye 5, even when the
    /// mouse does not move: a still mouse is a fixation.
    ///
    /// Only the latest point is kept in memory: nothing is saved.
    /// </summary>
    internal sealed class MouseGazeSource : GazeSource
    {
        /// <summary>How far the virtual point can go outside the screen, as a share of the screen height.</summary>
        private const double OffScreenMarginRatio = 0.15;

        /// <summary>Time between two measures, in ms (about 30 per second, like the PCEye 5).</summary>
        private const int MeasureIntervalMs = 33;

        /// <summary>Message-only window (no display): HWND_MESSAGE.</summary>
        private static readonly IntPtr MessageOnlyParent = new IntPtr(-3);

        private HwndSource _window;
        private DispatcherTimer _timer;
        private IntPtr _buffer = IntPtr.Zero;
        private uint _bufferSize;

        // The virtual gaze point and its limits, in screen pixels. Only used on the UI thread.
        private double _x;
        private double _y;
        private double _screenWidth;
        private double _screenHeight;

        /// <summary>
        /// Opens the invisible window, asks Windows for the raw mouse movements and starts the
        /// timer. Must be called on the UI thread.
        /// </summary>
        public override void Start()
        {
            try
            {
                NativeMethods.PrimaryScreenSize(out _screenWidth, out _screenHeight);

                // The virtual point starts where the cursor is.
                NativeMethods.POINT cursor;
                if (NativeMethods.GetCursorPos(out cursor))
                {
                    _x = cursor.X;
                    _y = cursor.Y;
                }
                else
                {
                    _x = _screenWidth / 2;
                    _y = _screenHeight / 2;
                }

                HwndSourceParameters parameters = new HwndSourceParameters("GazeBar mouse gaze");
                parameters.ParentWindow = MessageOnlyParent;
                parameters.WindowStyle = 0;
                _window = new HwndSource(parameters);
                _window.AddHook(WindowProc);

                NativeMethods.RAWINPUTDEVICE[] devices = new NativeMethods.RAWINPUTDEVICE[1];
                devices[0].UsagePage = 0x01;   // generic desktop controls
                devices[0].Usage = 0x02;       // mouse
                devices[0].Flags = NativeMethods.RIDEV_INPUTSINK;
                devices[0].TargetWindow = _window.Handle;
                if (!NativeMethods.RegisterRawInputDevices(devices, 1,
                        (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTDEVICE))))
                {
                    throw new InvalidOperationException(
                        "RegisterRawInputDevices failed (Windows error " + Marshal.GetLastWin32Error() + ")");
                }

                _timer = new DispatcherTimer(DispatcherPriority.Normal);
                _timer.Interval = TimeSpan.FromMilliseconds(MeasureIntervalMs);
                _timer.Tick += (sender, args) => OnGaze(_x, _y);
                _timer.Start();

                IsRunning = true;
                App.Log("Gaze: mouse as gaze (developer mode)");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                ErrorMessage = ex.Message;
                App.Log("Gaze: mouse as gaze failed: " + ex);
            }
        }

        /// <summary>Receives the messages of the invisible window; only WM_INPUT matters.</summary>
        private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == NativeMethods.WM_INPUT)
            {
                ReadRawInput(lParam);
            }
            // Not "handled": Windows still does its own cleanup of the message.
            return IntPtr.Zero;
        }

        /// <summary>
        /// Reads one raw mouse movement and moves the virtual point.
        ///
        /// Memory layout (Windows RAWINPUT structure): a header (type, size, device, wParam),
        /// then the RAWMOUSE part: flags (2 bytes + 2 of padding), buttons (4), raw buttons
        /// (4), then the movement X (4) and Y (4).
        /// </summary>
        private void ReadRawInput(IntPtr rawInput)
        {
            int headerSize = 8 + 2 * IntPtr.Size;

            uint size = 0;
            NativeMethods.GetRawInputData(rawInput, NativeMethods.RID_INPUT, IntPtr.Zero, ref size, (uint)headerSize);
            if (size == 0)
            {
                return;
            }
            if (size > _bufferSize)
            {
                if (_buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_buffer);
                }
                _buffer = Marshal.AllocHGlobal((int)size);
                _bufferSize = size;
            }
            if (NativeMethods.GetRawInputData(rawInput, NativeMethods.RID_INPUT, _buffer, ref size, (uint)headerSize) != size)
            {
                return;
            }

            uint type = (uint)Marshal.ReadInt32(_buffer, 0);
            IntPtr device = Marshal.ReadIntPtr(_buffer, 8);
            // No device = a movement injected by a program (GazeBar itself): ignored.
            if (type != NativeMethods.RIM_TYPEMOUSE || device == IntPtr.Zero)
            {
                return;
            }

            ushort flags = (ushort)Marshal.ReadInt16(_buffer, headerSize);
            int moveX = Marshal.ReadInt32(_buffer, headerSize + 12);
            int moveY = Marshal.ReadInt32(_buffer, headerSize + 16);

            if ((flags & NativeMethods.MOUSE_MOVE_ABSOLUTE) != 0)
            {
                // Tablets and remote desktop: a position from 0 to 65535 across the screen.
                _x = moveX / 65535.0 * _screenWidth;
                _y = moveY / 65535.0 * _screenHeight;
            }
            else
            {
                // A normal mouse: a movement in "counts" (Windows pointer speed not applied).
                _x += moveX;
                _y += moveY;
            }

            double margin = _screenHeight * OffScreenMarginRatio;
            _x = Math.Max(-margin, Math.Min(_screenWidth + margin, _x));
            _y = Math.Max(-margin, Math.Min(_screenHeight + margin, _y));
        }

        public override void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
            if (_window != null)
            {
                _window.RemoveHook(WindowProc);
                _window.Dispose();
                _window = null;
            }
            if (_buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
                _bufferSize = 0;
            }
        }
    }
}
