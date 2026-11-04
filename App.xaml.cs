using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;

namespace GazeBar
{
    /// <summary>
    /// The application class: entry point of the program.
    ///
    /// Only one instance of GazeBar at a time, and the [StartWithWindows] option.
    ///
    /// It reads the settings, creates the shared "model" objects (cursor, gaze), opens the
    /// full-screen overlay and the mode manager, and watches the Escape key.
    /// There is no toolbar: everything goes through the gaze, and the
    /// menu bar opens by looking down, below the screen.
    /// It also makes sure, in all circumstances, that the mouse buttons are released when
    /// closing.
    ///
    /// Reminder: "partial" means the class is written in two pieces, this one and another
    /// one generated automatically from App.xaml.
    /// </summary>
    public partial class App : Application
    {
        private GazeSource _gaze;
        private OverlayWindow _layer;
        private ModeManager _modes;
        private KeyboardHook _keyboard;
        private DiagnosticWindow _diagnostic;

        /// <summary>
        /// The single-instance "lock". A Mutex is a named Windows object; only one
        /// program at a time can own it. The first GazeBar takes it; a second GazeBar sees it
        /// is already taken, and closes at once.
        /// "Local\": the name applies to the user's Windows session.
        /// </summary>
        private const string LockName = "Local\\GazeBar-InstanceUnique";
        private Mutex _singleInstanceMutex;

        // True for a second GazeBar, which closes without doing anything (see OnStartup / OnExit).
        private bool _extraInstance;

        /// <summary>
        /// Called by WPF when the program starts.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // A single instance (specification, "General operation"): two GazeBars at the same
            // time would draw two circles and send double clicks.
            // If GazeBar is already running, this one closes at once, without showing anything.
            bool isFirst;
            _singleInstanceMutex = new Mutex(true, LockName, out isFirst);
            if (!isFirst)
            {
                _extraInstance = true;
                Log("Second launch of GazeBar: an instance is already running, closing");
                Shutdown();
                return;
            }

            Log("GazeBar starting");

            // Safety nets: whatever the error, release the mouse buttons and the Ctrl, Shift,
            // Alt keys (a button or key left pressed would disturb the whole computer),
            // and write the error to the log.
            DispatcherUnhandledException += (sender, args) =>
            {
                MouseInjector.ReleaseAll();
                KeyboardInjector.ReleaseAll();
                Log("UNHANDLED ERROR (UI): " + args.Exception);
                args.Handled = true;   // prevents the program from closing abruptly
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                MouseInjector.ReleaseAll();
                KeyboardInjector.ReleaseAll();
                Log("UNHANDLED ERROR (.NET): " + args.ExceptionObject);
            };

            // The settings first: everything else uses them.
            AppSettings.Initialize();

            // The interface texts, in the chosen language.
            Texts.Load(AppSettings.Current.Language);

            // Start with Windows: the registry is made to match the settings file
            // (and the program path is updated if it was moved).
            WindowsStartup.Apply(AppSettings.Current.StartWithWindows);

            // Scrolling inactive windows: the Windows setting is made to match the
            // settings file (see WheelScroller).
            WheelScroller.Apply(AppSettings.Current.ScrollInactiveWindows);

            // The real gaze, provided by the eyetracker engine. If it is unavailable, the
            // program still starts (see GazeSource.ErrorMessage).
            // The eyetracker DLLs are not shipped with GazeBar: they are
            // first searched for on the PC (see EyetrackerLoader.cs), before any use of the gaze.
            EyetrackerLoader.Install();
            _gaze = new GazeSource();
            _gaze.Start();

            // The program only stops on request ("Quit GazeBar" button of the menu): closing
            // the Diagnostic window must not close everything.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // The full-screen overlay, transparent to clicks, and the mode manager.
            // We start in movement mode (the default mode of the specification).
            _layer = new OverlayWindow();
            _modes = new ModeManager(_gaze, _layer);
            _layer.Show();
            _layer.BringToFront();
            _modes.Start(new MovementMode());

            // If Windows hid the cursor (GazeBar started alone at startup,
            // without a physical mouse), make it reappear (see CursorWaker).
            CursorWaker.Check();

            // The caregiver's Escape key: back to movement mode, from any mode.
            _keyboard = new KeyboardHook();
            _keyboard.EscapePressed += OnEscape;
            _keyboard.Install();
        }

        /// <summary>
        /// Escape key (for the caregiver): the mouse buttons and modifier keys are released for
        /// safety (a drag and drop in progress is interrupted) and we return to movement mode,
        /// unless we are already there. In movement mode, it closes the gaze keyboard.
        /// The key also reaches the active program normally (it is not "eaten" by GazeBar).
        /// </summary>
        private void OnEscape()
        {
            if (_modes == null)
            {
                return;
            }

            // In movement mode, Escape closes the gaze keyboard if it is open.
            // (The Escape key of the gaze keyboard is simulated: it does not arrive here, see
            // KeyboardHook.)
            if (_modes.CurrentMode is MovementMode)
            {
                if (_modes.Keyboard.IsOpen)
                {
                    App.LogDetail("Escape key: closing the gaze keyboard");
                    _modes.Keyboard.Close();
                }
                return;
            }

            // Settings mode: during keyboard input, Escape only cancels the input.
            SettingsMode settings = _modes.CurrentMode as SettingsMode;
            if (settings != null && settings.CancelInput())
            {
                return;
            }

            // Icon configuration screen: Escape returns to the settings.
            IconConfigMode configuration = _modes.CurrentMode as IconConfigMode;
            if (configuration != null)
            {
                configuration.BackToSettings();
                return;
            }
            // Keyboard lists: Escape acts like "Back" (My words → settings).
            KeyboardListMode list = _modes.CurrentMode as KeyboardListMode;
            if (list != null)
            {
                list.Back();
                return;
            }
            App.LogDetail("Escape key: back to movement mode");
            MouseInjector.ReleaseAll();
            KeyboardInjector.ReleaseAll();
            _modes.ChangeMode(new MovementMode());
        }

        /// <summary>
        /// Opens the Diagnostic window (menu button). Only one at a time: if it is already open,
        /// it is simply brought to the front.
        /// </summary>
        internal void OpenDiagnostic()
        {
            if (_diagnostic == null)
            {
                _diagnostic = new DiagnosticWindow(_gaze);
                _diagnostic.Closed += (sender, args) => { _diagnostic = null; };
            }
            _diagnostic.Show();
            _diagnostic.Activate();
        }

        /// <summary>
        /// Called by WPF when the program closes.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            if (_extraInstance)
            {
                // Second GazeBar: it started nothing, it touches nothing (above all not the
                // mouse buttons, which the first GazeBar may be holding).
                _singleInstanceMutex.Dispose();
                base.OnExit(e);
                return;
            }

            MouseInjector.ReleaseAll();
            KeyboardInjector.ReleaseAll();
            if (_keyboard != null)
            {
                _keyboard.Dispose();
            }
            if (_modes != null)
            {
                // The pending word usage counts of the keyboard are saved in the
                // personal dictionary.
                _modes.Keyboard.Stop();
                _modes.Stop();
            }
            if (_gaze != null)
            {
                _gaze.Dispose();
            }
            Log("GazeBar stopping");
            if (_singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();
            }
            base.OnExit(e);
        }

        /// <summary>Maximum size of gazebar.log (5 MB). Beyond it, the file becomes gazebar.old.log.</summary>
        private const long MaxLogBytes = 5L * 1024 * 1024;

        // Several threads may write to the log at the same time: the lock makes them wait
        // for each other, so that two lines are never mixed up.
        private static readonly object LogLock = new object();

        // Current size of gazebar.log in bytes (-1 = not measured yet).
        private static long _logBytes = -1;

        /// <summary>
        /// Writes an IMPORTANT message to the log: start and stop, errors, settings read or
        /// changed, eyetracker found or not... Always written.
        /// For frequent events (each mode, each action, each key...), use LogDetail.
        /// </summary>
        internal static void Log(string message)
        {
            Write(message);
        }

        /// <summary>
        /// Writes a DETAILED message to the log (frequent events, useful to track down a bug).
        /// Only written when the "Detailed log" setting is on: otherwise the log stays short.
        /// </summary>
        internal static void LogDetail(string message)
        {
            AppSettings settings = AppSettings.Current;
            if (settings != null && settings.DetailedLog)
            {
                Write(message);
            }
        }

        /// <summary>
        /// Writes one line, in two places:
        ///  1. the Visual Studio "Output" window (prefix [GazeBar]);
        ///  2. the gazebar.log file in %LOCALAPPDATA%\GazeBar
        ///     (useful when the program is started without the debugger).
        /// When gazebar.log reaches 5 MB, it is renamed gazebar.old.log (replacing the previous
        /// one) and a new gazebar.log starts: the log never fills the disk.
        /// Reminder: no gaze coordinates and no typed text in the log (see CONTRIBUTING.md).
        /// </summary>
        private static void Write(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;
            Debug.WriteLine("[GazeBar] " + line);

            lock (LogLock)
            {
                try
                {
                    string folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GazeBar");
                    Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, "gazebar.log");
                    string text = line + Environment.NewLine;

                    if (_logBytes < 0)
                    {
                        _logBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                    }
                    if (_logBytes > 0 && _logBytes + text.Length > MaxLogBytes)
                    {
                        string oldPath = Path.Combine(folder, "gazebar.old.log");
                        File.Delete(oldPath);   // does nothing if the file does not exist
                        File.Move(path, oldPath);
                        _logBytes = 0;
                    }

                    File.AppendAllText(path, text);
                    _logBytes += Encoding.UTF8.GetByteCount(text);
                }
                catch
                {
                    // The log must never crash the program itself:
                    // if writing fails, ignore it silently (the size is measured again next time).
                    _logBytes = -1;
                }
            }
        }
    }
}
