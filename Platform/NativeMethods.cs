using System;
using System.Runtime.InteropServices;

namespace GazeBar
{
    /// <summary>
    /// Windows functions (written in C/C++) called from C#.
    ///
    /// This mechanism is called "P/Invoke" (Platform Invoke). The function is declared with
    /// [DllImport("library_name.dll")] and the "extern" keyword: Windows then provides the
    /// code. The documentation of each function is on Microsoft's website (search for
    /// example "GetCursorPos Win32").
    ///
    /// "internal" = visible only in this project. "static" = no NativeMethods object needs to
    /// be created to use it.
    /// </summary>
    internal static class NativeMethods
    {
        // ───────────────────────── Cursor position ─────────────────────────

        /// <summary>A point on the screen, in pixels. Same memory layout as the Windows POINT structure.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>Reads the current mouse cursor position (here, the one the eyetracker software drives with the gaze).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT point);

        /// <summary>Moves the cursor to the given position (in screen pixels).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCursorPos(int x, int y);

        /// <summary>
        /// Cursor state (Windows CURSORINFO structure). : mostly "flags" is
        /// read, to know whether Windows hid the cursor.
        /// cbSize must contain the size of the structure before the call (Windows checks it).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        /// <summary>Reads the cursor state: visible, hidden, "suppressed" by Windows...</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorInfo(ref CURSORINFO info);

        /// <summary>
        /// CURSORINFO.flags flag: Windows "suppressed" the cursor (Windows 8 and later).
        /// This happens when Windows thinks there is no mouse: after a touch on a touch screen,
        /// or at startup if no physical mouse has moved yet. The cursor then stays invisible
        /// until the next movement of a "real" mouse.
        /// Not to be confused with a cursor hidden on purpose by a program (full-screen video,
        /// game): in that case, this flag is not set.
        /// </summary>
        internal const int CURSOR_SUPPRESSED = 0x00000002;

        /// <summary>Size of the primary screen (index 0 = width, 1 = height), in pixels.</summary>
        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int index);

        internal const int SM_CXSCREEN = 0;
        internal const int SM_CYSCREEN = 1;

        /// <summary>
        /// REAL size of the primary screen, in pixels.
        ///
        /// Why not GetSystemMetrics? The user's test: GazeBar started in 1920 × 1080 at 100 %,
        /// then the screen switched to 3840 × 2160 at 200 %: GetSystemMetrics kept answering
        /// 1920 × 1080. Windows "freezes" its answers to the scaling at the program's startup:
        /// 3840 pixels at 200 %, seen with the startup scale (100 %), make 3840 × 100 ÷ 200 =
        /// 1920. So the change was not seen, and the overlay only covered the top-left quarter
        /// of the screen.
        ///
        /// EnumDisplaySettings, on the other hand, gives the current "display mode" of the
        /// screen: its real resolution, whatever the scaling. On failure, we fall back to
        /// GetSystemMetrics.
        /// </summary>
        internal static void PrimaryScreenSize(out double width, out double height)
        {
            DEVMODE mode = new DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            // null = the primary screen; ENUM_CURRENT_SETTINGS = the mode currently used.
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref mode) &&
                mode.dmPelsWidth > 0 && mode.dmPelsHeight > 0)
            {
                width = mode.dmPelsWidth;
                height = mode.dmPelsHeight;
                return;
            }
            width = GetSystemMetrics(SM_CXSCREEN);
            height = GetSystemMetrics(SM_CYSCREEN);
        }

        private const int ENUM_CURRENT_SETTINGS = -1;

        /// <summary>
        /// The program's "system" scaling, in dots per inch (96 = 100 %, 144 = 150 %). It is set
        /// when the program starts and no longer changes afterwards (see
        /// ScreenMetrics.StartupScale). Windows 10 version 1607 and later.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern uint GetDpiForSystem();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettings(string screenName, int modeNumber, ref DEVMODE mode);

        /// <summary>
        /// Description of a display mode (Windows DEVMODE structure). Only dmSize (to fill in
        /// before the call), dmPelsWidth and dmPelsHeight (the resolution) are used here; the
        /// other fields must still be there, in this order, so that the structure has exactly
        /// the size Windows expects.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        // ───────────────────────── Mouse and keyboard simulation ─────────────────────────

        /// <summary>Description of a mouse action to simulate (Windows MOUSEINPUT structure).</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int dx;             // position or movement in X (see MOUSEEVENTF_ABSOLUTE)
            public int dy;             // position or movement in Y
            public uint mouseData;     // extra data: the wheel amount, otherwise 0
            public uint dwFlags;       // WHICH action: button pressed, released, movement...
            public uint time;          // 0 = let Windows set the time
            public UIntPtr dwExtraInfo;
        }

        /// <summary>
        /// Description of a keyboard key to simulate (Windows KEYBDINPUT structure).
        /// Used for the Ctrl, Shift and Alt modifiers and the gaze keyboard.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            public ushort wVk;         // key code (VK_CONTROL, VK_SHIFT...)
            public ushort wScan;       // hardware code: unused here (0)
            public uint dwFlags;       // 0 = key pressed, KEYEVENTF_KEYUP = key released
            public uint time;          // 0 = let Windows set the time
            public UIntPtr dwExtraInfo;
        }

        /// <summary>
        /// A "union": the mouse AND the keyboard are stored at the SAME place in memory
        /// (FieldOffset(0) for both). Windows reads one or the other according to INPUT.type.
        /// This is how the INPUT structure is defined in C; reproducing it exactly lets Windows
        /// find the right size (SendInput checks it).
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        internal struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mid;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        /// <summary>
        /// Wrapper of a simulated input (Windows INPUT structure).
        /// type = INPUT_MOUSE (0) for the mouse, INPUT_KEYBOARD (1) for the keyboard.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        internal const uint INPUT_MOUSE = 0;
        internal const uint INPUT_KEYBOARD = 1;
        internal const uint MOUSEEVENTF_MOVE = 0x0001;
        internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
        internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        internal const uint MOUSEEVENTF_WHEEL = 0x0800;    // vertical wheel: amount in mouseData
        internal const uint MOUSEEVENTF_HWHEEL = 0x1000;   // horizontal wheel
        internal const uint MOUSEEVENTF_ABSOLUTE = 0x8000; // dx, dy = position on the screen (0 to 65535), not an offset
        internal const uint KEYEVENTF_KEYUP = 0x0002;

        // Codes of the modifier keys ("virtual-key codes").
        internal const ushort VK_SHIFT = 0x10;    // Shift
        internal const ushort VK_CONTROL = 0x11;  // Ctrl
        internal const ushort VK_MENU = 0x12;     // Alt (Windows calls it "menu")
        internal const ushort VK_C = 0x43;        // C key (copy, with Ctrl)
        internal const ushort VK_V = 0x56;        // V key (paste, with Ctrl)
        internal const ushort VK_X = 0x58;        // X key (cut, with Ctrl)

        // Gaze keyboard.
        //  KEYEVENTF_EXTENDEDKEY: "extended" key (arrows, Home, End, Delete, Page...,
        //                         Windows key): without this flag, some applications confuse
        //                         them with the numeric keypad keys;
        //  KEYEVENTF_UNICODE    : a CHARACTER is sent (in wScan) and not a key.
        //                         Windows delivers it as is to the application, whatever the
        //                         keyboard layout set in Windows.
        internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        internal const uint KEYEVENTF_UNICODE = 0x0004;
        internal const ushort VK_BACK = 0x08;     // Backspace (delete)
        internal const ushort VK_LWIN = 0x5B;     // left Windows key

        /// <summary>
        /// Gives the key (and modifiers) producing a character with the Windows keyboard
        /// layout. Low byte = key code; high byte = 1 Shift, 2 Ctrl, 4 Alt. Returns -1 if no key
        /// produces this character.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern short VkKeyScanW(char character);

        /// <summary>Converts a key code into a "hardware code" (mode 0 = MAPVK_VK_TO_VSC).</summary>
        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKeyW(uint code, uint mode);

        /// <summary>Simulates mouse or keyboard actions, as if they came from the hardware.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint inputCount, INPUT[] inputs, int sizeOfInput);

        // ───────────────────────── Window that does not take the focus ─────────────────────────

        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_NOACTIVATE = 0x08000000; // the window never becomes "active"
        internal const int WS_EX_TOOLWINDOW = 0x00000080; // tool window: not in Alt+Tab
        internal const int WM_MOUSEACTIVATE = 0x0021;     // message: "a click is going to activate the window"
        internal const int MA_NOACTIVATE = 3;             // answer: "no, do not activate it"

        // Messages sent by Windows to all windows (see OverlayWindow.Hook):
        internal const int WM_SETTINGCHANGE = 0x001A;     // a Windows setting changed
        internal const int WM_DISPLAYCHANGE = 0x007E;     // the screen resolution changed

        // For the full-screen window (OverlayWindow) that must let clicks through:
        internal const int WS_EX_TRANSPARENT = 0x00000020; // the mouse "goes through" the window
        internal const int WS_EX_LAYERED = 0x00080000;     // layered window (already set by WPF, we make sure)

        /// <summary>
        /// Changes the position, the size or the "above / below" place of a window.
        /// Here it is only used to put the overlay back at the very top of the window stack.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1); // "above everything"
        internal const uint SWP_NOSIZE = 0x0001;      // do not change the size
        internal const uint SWP_NOMOVE = 0x0002;      // do not move
        internal const uint SWP_NOACTIVATE = 0x0010;  // do not give the focus

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SetWindowLong(IntPtr window, int index, int newValue);

        // ───────────────────────── Screen capture ─────────────────────────
        //
        // To take a picture of part of the screen, we use GDI, the old Windows drawing library
        // (always present). The principle, in 5 steps (see ScreenCapture.cs):
        //   1. GetDC(0)                       : get the "drawing context" of the whole screen;
        //   2. CreateCompatibleDC/Bitmap      : prepare an empty image in memory;
        //   3. SelectObject                   : "put" this image into an in-memory context;
        //   4. BitBlt                         : copy the wanted rectangle of the screen into the image;
        //   5. DeleteObject/DeleteDC/ReleaseDC: give everything back to Windows (otherwise, memory leak).

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height,
                                           IntPtr source, int sourceX, int sourceY, uint operation);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr gdiObject);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr dc);

        internal const uint SRCCOPY = 0x00CC0020;   // BitBlt: plain pixel copy

        // ───────────────────────── Windows of other programs ─────────────────────────
        //
        // During calibration, GazeBar watches the window of the eyetracker tool to know when it
        // closes (see CalibrationMode).

        /// <summary>The active window (the one receiving the keyboard), or IntPtr.Zero.</summary>
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        /// <summary>True if this window still exists.</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        /// <summary>True if this window is visible (neither hidden nor closed).</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        /// <summary>Gives the number of the program (process) a window belongs to.</summary>
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        /// <summary>Reads the title of a window (for the log).</summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int maxCount);

        // ───────────────────────── Window under the cursor (scrolling) ─────────────────────────
        //
        // When entering scroll mode, GazeBar makes the window under the aimed point active:
        // some software ignores the wheel as long as its window is not active (the user had to
        // click before being able to scroll).

        /// <summary>
        /// The window (or part of a window) at this point of the screen. Windows that the mouse
        /// "goes through" (like GazeBar's overlay) are ignored.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(POINT point);

        /// <summary>Goes up from a part of a window to the main window containing it.</summary>
        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr window, uint flags);

        internal const uint GA_ROOT = 2;   // GetAncestor: the main window (at the very top)

        /// <summary>Makes a window active (it receives the keyboard and, depending on the settings, the wheel).</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr window);

        /// <summary>
        /// Temporarily "attaches" two threads: they then share the keyboard state, which allows
        /// GazeBar to change the active window. Otherwise Windows often refuses
        /// SetForegroundWindow to a program that is not itself in the foreground.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachThreadInput(uint threadAttach, uint threadAttachTo, [MarshalAs(UnmanagedType.Bool)] bool attach);

        /// <summary>The number of the calling thread (here, the one of GazeBar's display).</summary>
        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        /// <summary>Reads a Windows setting (here: which window the wheel goes to).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint winIni);

        /// <summary>
        /// Windows 10/11 setting "Scroll inactive windows when I hover over them": value 2 = the
        /// wheel goes to the window under the cursor (on), 0 = to the active window (off).
        /// </summary>
        internal const uint SPI_GETMOUSEWHEELROUTING = 0x201C;

        /// <summary>Changes this same setting ("Scroll inactive windows" option, see WheelScroller).</summary>
        internal const uint SPI_SETMOUSEWHEELROUTING = 0x201D;

        /// <summary>Same function, the value being passed directly instead of its address (see WheelScroller).</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SystemParametersInfo(uint action, uint param, IntPtr value, uint winIni);

        internal const uint SPIF_UPDATEINIFILE = 0x01;   // Windows saves the change
        internal const uint SPIF_SENDCHANGE = 0x02;      // and notifies the open programs

        // ───────────────────────── Caregiver's Escape key ─────────────────────────
        //
        // A "keyboard hook" lets us be notified of every key pressed, in any program. GazeBar
        // only OBSERVES: the key goes on its way normally to the active program (see
        // KeyboardHook.cs).

        /// <summary>The function called by Windows for every key (signature imposed by Windows).</summary>
        internal delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string moduleName);

        internal const int WH_KEYBOARD_LL = 13;     // "low-level" keyboard hook, for the whole system
        internal const int WM_KEYDOWN = 0x0100;     // a key is pressed
        internal const int WM_SYSKEYDOWN = 0x0104;  // same, with Alt
        internal const int VK_ESCAPE = 0x1B;        // code of the Escape key

        /// <summary>
        /// In the structure received by the keyboard hook, the "flags" field (3rd number, so 8
        /// bytes after the start): this flag says the key was SIMULATED by a program
        /// (SendInput), and not typed on a real keyboard.
        /// </summary>
        internal const int HookFlagsOffset = 8;
        internal const int LLKHF_INJECTED = 0x10;

        // ───────────────────────── Clock precision ─────────────────────────

        /// <summary>Asks Windows for a more precise clock (1 ms instead of about ~15 ms).</summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeBeginPeriod(uint milliseconds);

        /// <summary>To call when done, to cancel timeBeginPeriod.</summary>
        [DllImport("winmm.dll")]
        internal static extern uint timeEndPeriod(uint milliseconds);

        // ───────────────────────── Loading a native DLL ─────────────────────────

        /// <summary>
        /// Loads a native DLL (written in C) from its full path. Used to load in advance the
        /// eyetracker's native DLL, which is not shipped with GazeBar (see EyetrackerLoader.cs).
        /// Returns IntPtr.Zero on failure (see GetLastWin32Error).
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibrary(string dllPath);

        // ───────────────────────── Rights of the GazeBar process ─────────────────────────

        /// <summary>A "pseudo handle" to the GazeBar process itself (no need to close it).</summary>
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        /// <summary>Asks for read access to TOKEN_QUERY: reading the process token.</summary>
        internal const uint TOKEN_QUERY = 0x0008;

        /// <summary>
        /// Opens the "access token" of a process: the Windows object that holds its rights.
        /// The handle must then be closed with CloseHandle.
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        /// <summary>TokenUIAccess: the information "does this process have UI access" (a number, 0 or 1).</summary>
        internal const int TokenUIAccess = 26;

        /// <summary>Reads one piece of information from an access token (here a 4-byte number).</summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass,
            out uint tokenInformation, uint tokenInformationLength, out uint returnLength);

        /// <summary>Closes a handle opened by Windows (here the token).</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
