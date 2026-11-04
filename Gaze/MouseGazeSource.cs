using System;
using System.Runtime.InteropServices;
using System.Threading;
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
    ///  - on the screen, by a low-level mouse hook (WH_MOUSE_LL). For each real movement,
    ///    Windows gives the new cursor position before applying it: new position − current
    ///    position = the movement of the hand, with the Windows pointer speed and acceleration.
    ///    So the virtual point moves exactly like the cursor would.
    ///  - outside the screen, the cursor is stuck at the edge: there, the movement comes from
    ///    "raw input" (WM_INPUT messages sent to a small invisible window), in mouse counts.
    ///    The virtual point can go a little outside the screen ([OffScreenMarginRatio]), like
    ///    a real gaze: this is how the menu is opened, by "looking" below the screen.
    /// The movements GazeBar injects itself (SendInput) are flagged by Windows and ignored;
    /// SetCursorPos produces no mouse event at all.
    ///
    /// A timer sends the point about 30 times per second, like the PCEye 5, even when the
    /// mouse does not move: a still mouse is a fixation.
    ///
    /// All this runs on its own thread, with its own message loop (a WPF Dispatcher). Why?
    /// Windows waits for the hook's answer before moving the cursor; if the hook were on the
    /// UI thread, busy drawing or taking the magnifier photo, the answer would come late, the
    /// cursor would already have moved, and the movement would be lost: the circle would drift
    /// away from the cursor little by little.
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

        // Keep a reference to the hook function: otherwise the .NET "garbage collector" could
        // destroy it while Windows still calls it (crash).
        private readonly NativeMethods.LowLevelMouseProc _hookFunction;
        private IntPtr _hook = IntPtr.Zero;

        private Thread _thread;
        private Dispatcher _dispatcher;
        private HwndSource _window;
        private DispatcherTimer _timer;
        private IntPtr _buffer = IntPtr.Zero;
        private uint _bufferSize;

        // The virtual gaze point and its limits, in screen pixels. Only used on the source's
        // own thread (after Start).
        private double _x;
        private double _y;
        private double _screenWidth;
        private double _screenHeight;

        public MouseGazeSource()
        {
            _hookFunction = OnMouseHook;
        }

        /// <summary>
        /// Starts the source's own thread and waits until it is ready (hook, invisible window,
        /// raw movements, timer). Never throws: on failure, IsRunning stays false.
        /// </summary>
        public override void Start()
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

            Exception error = null;
            using (ManualResetEvent ready = new ManualResetEvent(false))
            {
                _thread = new Thread(() =>
                {
                    try
                    {
                        SetUp();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                        TearDown();
                        ready.Set();
                        return;
                    }
                    ready.Set();
                    // The message loop of this thread: runs until Dispose.
                    Dispatcher.Run();
                });
                _thread.Name = "Mouse as gaze";
                _thread.IsBackground = true;           // never keeps GazeBar alive
                _thread.SetApartmentState(ApartmentState.STA);   // required by HwndSource
                _thread.Start();

                if (!ready.WaitOne(5000))
                {
                    error = new TimeoutException("Mouse as gaze: the thread did not start");
                }
            }

            if (error == null)
            {
                IsRunning = true;
                App.Log("Gaze: mouse as gaze (developer mode)");
            }
            else
            {
                IsRunning = false;
                ErrorMessage = error.Message;
                App.Log("Gaze: mouse as gaze failed: " + error);
            }
        }

        /// <summary>On the source's own thread: installs everything.</summary>
        private void SetUp()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            // For a low-level hook, Windows asks for the program's "module";
            // GetModuleHandle(null) gives the one of GazeBar.exe. Windows calls the hook on
            // this thread, through its message loop.
            _hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL, _hookFunction, NativeMethods.GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Mouse hook refused (Windows error " + Marshal.GetLastWin32Error() + ")");
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

            // A DispatcherTimer ticks on the thread that created it: this one.
            _timer = new DispatcherTimer(DispatcherPriority.Normal);
            _timer.Interval = TimeSpan.FromMilliseconds(MeasureIntervalMs);
            _timer.Tick += (sender, args) => OnGaze(_x, _y);
            _timer.Start();
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
            if ((flags & NativeMethods.MOUSE_MOVE_ABSOLUTE) != 0)
            {
                // Tablets and remote desktop: an absolute position, followed by the hook only.
                return;
            }
            int moveX = Marshal.ReadInt32(_buffer, headerSize + 12);
            int moveY = Marshal.ReadInt32(_buffer, headerSize + 16);

            double margin = _screenHeight * OffScreenMarginRatio;
            _x = MoveOutside(_x, moveX, _screenWidth - 1, margin);
            _y = MoveOutside(_y, moveY, _screenHeight - 1, margin);
        }

        /// <summary>
        /// One axis of a raw movement: only used when the virtual point is outside the screen,
        /// or on its edge and pushed outward (the cursor cannot follow there). The point stays
        /// on its side: when it comes back, it stops on the edge and the hook takes over.
        /// last: the last pixel of the screen on this axis.
        /// </summary>
        private static double MoveOutside(double position, int move, double last, double margin)
        {
            if (position < 0 || (position <= 0 && move < 0))
            {
                return Math.Max(-margin, Math.Min(0, position + move));
            }
            if (position > last || (position >= last && move > 0))
            {
                return Math.Min(last + margin, Math.Max(last, position + move));
            }
            return position;
        }

        /// <summary>
        /// Called by Windows for every mouse event, on the source's own thread. Must answer very quickly.
        /// For a real movement, moves the virtual point by the movement of the cursor, on each
        /// axis where the point is on the screen.
        /// </summary>
        private IntPtr OnMouseHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && wParam.ToInt32() == NativeMethods.WM_MOUSEMOVE)
            {
                int flags = Marshal.ReadInt32(lParam, NativeMethods.MouseHookFlagsOffset);
                NativeMethods.POINT cursor;
                if ((flags & NativeMethods.LLMHF_INJECTED) == 0 && NativeMethods.GetCursorPos(out cursor))
                {
                    // The new position (start of the structure) minus the current one.
                    int moveX = Marshal.ReadInt32(lParam, 0) - cursor.X;
                    int moveY = Marshal.ReadInt32(lParam, 4) - cursor.Y;
                    _x = MoveInside(_x, moveX, _screenWidth - 1);
                    _y = MoveInside(_y, moveY, _screenHeight - 1);
                }
            }

            // Always pass the event on: the mouse keeps working normally.
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        /// <summary>One axis of a hook movement: only when the point is on the screen, and it stays on it.</summary>
        private static double MoveInside(double position, int move, double last)
        {
            if (position < 0 || position > last)
            {
                return position;
            }
            return Math.Max(0, Math.Min(last, position + move));
        }

        /// <summary>Stops the source's thread (from any thread).</summary>
        public override void Dispose()
        {
            Dispatcher dispatcher = _dispatcher;
            if (dispatcher != null && !dispatcher.HasShutdownStarted)
            {
                dispatcher.Invoke(new Action(TearDown));
                dispatcher.InvokeShutdown();
            }
            _dispatcher = null;
        }

        /// <summary>On the source's own thread: removes everything SetUp installed.</summary>
        private void TearDown()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
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
