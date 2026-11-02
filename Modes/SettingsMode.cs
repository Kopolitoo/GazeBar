using System;
using System.Windows;

namespace GazeBar
{
    /// <summary>
    /// SETTINGS MODE (step 7 of the specification).
    ///
    /// Entry: "Settings" button of the menu bar.
    ///
    /// Display: the SettingsWindow window, full screen, black background (see that file for
    /// the layout). The overlay is emptied: nothing is drawn on it during this mode.
    ///
    /// The gaze: this mode links the gaze to the window.
    ///   - on each tick, the gaze position (screen pixels) is converted to a position on the
    ///     1920 × 1080 page of the window, then given to a ButtonAimer, the same as for the
    ///     menu and the action choice;
    ///   - a target looked at for [ActivationDelay] activates;
    ///   - REPEAT (user's choice of 2026-10-26): if the gaze stays on "−" or "+", the value
    ///     keeps changing, by one step every [RepeatDelay] seconds. The other targets (tabs,
    ///     No / Yes...) only activate once: the gaze must leave the target to activate it again;
    ///   - a small gray circle (50 px) shows where the gaze is.
    ///
    /// Step 10: the "Configure icons" button of the "Icons" tab opens the icon configuration
    /// screen (IconConfigMode), which comes back here, on this tab.
    ///
    /// Step 14: the "My words" button of the "General" tab opens the list of the words added
    /// to the keyboard (KeyboardListMode), which comes back here, on this tab.
    ///
    /// "Tools" tab (2026-10-29, buttons moved from the menu bar):
    ///   - "Calibration" starts the eyetracker tool then switches to calibration mode (which
    ///     returns to movement mode when the tool closes). If starting fails, we stay here and
    ///     the message is shown in the tab;
    ///   - "Diagnostic" returns to movement mode and opens the Diagnostic window.
    ///
    /// Step 15 (profiles): "Profiles" tab. "Load a profile" opens the list of profiles
    /// (KeyboardListMode, kind Profiles), which comes back here with the chosen profile: the
    /// window then asks for confirmation. "Yes": LoadProfile (below) applies the profile, then
    /// the keyboard reloads its layout (the language may have changed).
    /// "Save the current settings" opens the profile NAME page (request of 2026-11-02): this
    /// mode then shows the gaze keyboard (GazeKeyboard.ShowForInput) and gives it the gaze when
    /// the gaze is on it; elsewhere, the window's targets react as usual. The keys go to the
    /// window's text box, which has the keyboard focus.
    ///
    /// Exit: "Back" button (in the middle of the right edge) or the caregiver's Escape key,
    /// to movement mode. The bottom menu does not open here (Mode.MenuAllowed stays "no",
    /// rule of 2026-10-25).
    ///
    /// Each change is applied at once (AppSettings.Current) and saved in settings.json.
    /// </summary>
    internal sealed class SettingsMode : Mode
    {
        /// <summary>Repeat interval on "−" and "+", in seconds.</summary>
        private const double RepeatDelay = 0.4;

        // The tab shown when opening (0 = the first).
        private readonly int _startTab;

        // Step 15: the profile chosen in the list, to confirm when opening (or null).
        private readonly FoundProfile _profileToConfirm;

        private SettingsWindow _window;
        private ButtonAimer _aimer;

        // Version of the page seen at the previous tick: if it changes, the targets changed.
        private int _version = -1;

        // The target being repeated ("−" or "+"), or -1.
        private int _repeatedTarget = -1;

        // The small gaze circle: smoothed position, in screen pixels.
        private readonly Smoother _smoothed = new Smoother();
        private bool _circlePlaced;

        /// <summary>
        /// Opens the settings on tab number "tab" (the first one if not given). Step 15:
        /// "profileToConfirm" = profile chosen in the list of profiles; the page then opens on
        /// the question "Load profile ...?".
        /// </summary>
        public SettingsMode(int tab = 0, FoundProfile profileToConfirm = null)
        {
            _startTab = tab;
            _profileToConfirm = profileToConfirm;
        }

        public override string Name
        {
            get { return "Settings"; }
        }

        public override void Enter(GazeState gaze)
        {
            // Nothing on the overlay during this mode: everything is in the window.
            Manager.Layer.HideAll();

            // The window is stored in _window ONLY when everything is ready (aimer included):
            // Update does nothing as long as _window is empty (bug of 2026-11-02: a tick
            // arriving during Show found an empty aimer).
            SettingsWindow window = new SettingsWindow(_startTab, _profileToConfirm);
            window.BackRequested += Back;
            window.IconConfigRequested += OpenIconConfig;
            window.CalibrationRequested += LaunchCalibration;
            window.DiagnosticRequested += OpenDiagnostic;
            window.MyWordsRequested += OpenMyWords;
            window.ProfileListRequested += OpenProfileList;
            window.ProfileLoadRequested += LoadProfile;
            window.NamingStarted += ShowKeyboard;
            window.NamingEnded += HideKeyboard;
            _aimer = new ButtonAimer(window.Targets);
            _version = -1;
            _repeatedTarget = -1;

            window.Show();
            // Activate: the window takes the keyboard, so that the caregiver can type a value.
            // (Windows may refuse it if another program is in the foreground; a click of the
            // caregiver in the window is then enough.)
            window.Activate();
            _window = window;

            _circlePlaced = gaze.OnScreen;
            if (_circlePlaced)
            {
                _smoothed.Place(gaze.X, gaze.Y);
            }
        }

        public override void Update(GazeState gaze)
        {
            if (_window == null || _aimer == null)
            {
                return;
            }
            AppSettings p = AppSettings.Current;

            // 1. Has the page been rebuilt (new tab, confirmation...)? The aimer starts again
            //    from zero, and the target under the gaze does not activate until the gaze has
            //    left it (as in the menu).
            if (_window.Version != _version)
            {
                _version = _window.Version;
                _aimer.ResetAndBlock();
                _repeatedTarget = -1;
            }

            // 2. The gaze, converted to page coordinates. GazeState is a "struct": onPage is a
            //    copy, X and Y can be changed without touching the original.
            //    Profile name page: when the gaze is on the gaze keyboard, the keys react and
            //    the window's targets do not (the gaze is sent far away from them).
            GazeKeyboard keyboard = Manager.Keyboard;
            bool onKeyboard = gaze.OnScreen && keyboard.Contains(gaze.X, gaze.Y);
            bool keyboardShown = keyboard.Visible;
            keyboard.Update(gaze);
            if (keyboardShown && !keyboard.Visible)
            {
                // Its "Close" button was looked at: the name page takes the whole height
                // again (the name can still be typed with a real keyboard).
                _window.SetKeyboardZone(0, 0);
                return;
            }
            GazeState onPage = gaze;
            if (onKeyboard)
            {
                onPage.X = -10000;
                onPage.Y = -10000;
            }
            else if (gaze.Available)
            {
                Point point = _window.ToPage(gaze.X, gaze.Y);
                onPage.X = point.X;
                onPage.Y = point.Y;
            }

            // 3. The targets. During a repeat, the delay is shorter.
            double delay = (_repeatedTarget >= 0 && _aimer.Aimed == _repeatedTarget) ? RepeatDelay : p.ActivationDelay;
            int active = _aimer.Update(onPage, delay, false);
            if (_aimer.Aimed != _repeatedTarget)
            {
                _repeatedTarget = -1;   // the gaze left "−" or "+": end of the repeat
            }

            if (active >= 0)
            {
                if (_window.RepeatableTarget(active))
                {
                    _repeatedTarget = active;
                }
                else
                {
                    _aimer.BlockUntilExit();
                }

                _window.Activate(active);
                if (Manager.CurrentMode != this)
                {
                    return;   // "Back": the mode has already changed
                }
            }

            // 4. The small gaze circle. Gaze off screen: it stays at the edge and slides along
            //    it (GazeState.XScreen). Gaze lost: it stays where it is.
            if (gaze.Available)
            {
                if (!_circlePlaced)
                {
                    _smoothed.Place(gaze.XScreen, gaze.YScreen);
                    _circlePlaced = true;
                }
                _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);
                _window.PlaceGazeDot(_window.ToPage(_smoothed.X, _smoothed.Y));
            }
        }

        public override void Quit()
        {
            if (_window != null)
            {
                SettingsWindow window = _window;
                _window = null;
                window.BackRequested -= Back;
                window.IconConfigRequested -= OpenIconConfig;
                window.CalibrationRequested -= LaunchCalibration;
                window.DiagnosticRequested -= OpenDiagnostic;
                window.MyWordsRequested -= OpenMyWords;
                window.ProfileListRequested -= OpenProfileList;
                window.ProfileLoadRequested -= LoadProfile;
                window.NamingStarted -= ShowKeyboard;
                window.NamingEnded -= HideKeyboard;
                window.CloseSettings();
                // A profile name being typed: the keyboard is hidden (and closed again if it
                // was opened only for the name).
                Manager.Keyboard.EndInput();
            }
        }

        /// <summary>
        /// The screen changed (step 11): we stay in the settings. The window takes the screen
        /// size again and the page is redrawn (the "Display" tab shows the detected screen).
        /// The 1920 × 1080 unit page adapts by itself (Viewbox).
        /// </summary>
        public override bool AdaptToScreen()
        {
            if (_window != null)
            {
                _window.AdaptToScreen();
                // The keyboard has been redrawn at the new sizes (ModeManager): the name page
                // is placed again beside it.
                if (Manager.Keyboard.Visible)
                {
                    SendKeyboardZone();
                }
            }
            return true;
        }

        /// <summary>
        /// The caregiver's Escape key (called by App): if a value is being typed, the input is
        /// cancelled and we stay here. Returns true in that case.
        /// </summary>
        public bool CancelInput()
        {
            return _window != null && _window.CancelInput();
        }

        /// <summary>"Configure icons" button: opens the icon configuration screen.</summary>
        private void OpenIconConfig()
        {
            App.Log("Settings: icon configuration");
            Manager.ChangeMode(new IconConfigMode());
        }

        /// <summary>
        /// "My words" button of the "General" tab (step 14): the list of the words added to the
        /// keyboard, which comes back here, on this tab.
        /// </summary>
        private void OpenMyWords()
        {
            App.Log("Settings: my words");
            Manager.ChangeMode(new KeyboardListMode(KeyboardListMode.ListKind.MyWords));
        }

        /// <summary>
        /// "Load a profile" button of the "Profiles" tab (step 15): the full-screen list of
        /// profiles, which comes back here (Profiles tab) with or without a choice.
        /// </summary>
        private void OpenProfileList()
        {
            App.Log("Settings: profile list");
            Manager.ChangeMode(new KeyboardListMode(KeyboardListMode.ListKind.Profiles));
        }

        /// <summary>
        /// "Yes" to "Load profile ...?" (step 15): the profile becomes the current settings
        /// (SettingsProfiles.Load). The keyboard, if open, reloads its layout, because the
        /// profile may have another language. We stay in the settings: the page is rebuilt
        /// with the new values and the message.
        /// </summary>
        private void LoadProfile(FoundProfile profile)
        {
            string message;
            if (SettingsProfiles.Load(profile.Path, out message))
            {
                Manager.Keyboard.SettingsReplaced();
            }
            if (_window != null)
            {
                _window.ShowMessage(message);
            }
        }

        /// <summary>
        /// The profile name page opens (2026-11-02): the gaze keyboard is shown at its usual
        /// place (bottom or top, usual size), and the window places the page in the free part.
        /// If it cannot be shown, the page uses the whole height (real keyboard only).
        /// </summary>
        private void ShowKeyboard()
        {
            if (_window == null)
            {
                return;
            }
            if (Manager.Keyboard.ShowForInput())
            {
                SendKeyboardZone();
            }
            else
            {
                _window.SetKeyboardZone(0, 0);
            }
        }

        /// <summary>Tells the window where the keyboard is, in page units.</summary>
        private void SendKeyboardZone()
        {
            Rect bounds = Manager.Keyboard.ScreenBounds;
            if (bounds.IsEmpty)
            {
                _window.SetKeyboardZone(0, 0);
                return;
            }
            Point top = _window.ToPage(bounds.Left, bounds.Top);
            Point bottom = _window.ToPage(bounds.Left, bounds.Bottom);
            _window.SetKeyboardZone(top.Y, bottom.Y);
        }

        /// <summary>The profile name page closes: the keyboard is hidden again.</summary>
        private void HideKeyboard()
        {
            Manager.Keyboard.EndInput();
        }

        /// <summary>
        /// "Calibration" button of the "Tools" tab.
        ///
        /// Difficulty: the settings window is full screen and "always on top" (Topmost). If the
        /// eyetracker tool were started without doing anything, its window would be hidden
        /// behind. So we first HIDE the settings window (Hide: it still exists, it is simply
        /// no longer shown).
        ///
        /// Then, as from the menu bar, we note the active window BEFORE starting the tool:
        /// calibration mode will recognize the tool's window, which will be the next one to
        /// become active (see CalibrationMode). By hiding ours, Windows made another window
        /// active (the previous application): that is the one we note.
        ///
        /// If starting fails, the window is shown again with the message.
        /// </summary>
        private void LaunchCalibration()
        {
            if (_window == null)
            {
                return;
            }
            App.Log("Settings: calibration");
            _window.Hide();
            IntPtr previousWindow = NativeMethods.GetForegroundWindow();

            string message;
            if (Calibration.Launch(Manager.Gaze, out message))
            {
                // Quit() will close the settings window (already hidden).
                Manager.ChangeMode(new CalibrationMode(previousWindow));
            }
            else
            {
                _window.Show();
                _window.Activate();
                _window.ShowMessage(message);
            }
        }

        /// <summary>
        /// "Diagnostic" button of the "Tools" tab: back to movement mode (the settings window,
        /// always on top, would hide the Diagnostic window), then the Diagnostic window opens.
        /// </summary>
        private void OpenDiagnostic()
        {
            App.Log("Settings: diagnostic");
            Manager.ChangeMode(new MovementMode());
            ((App)Application.Current).OpenDiagnostic();
        }

        /// <summary>"Back" button: back to movement mode.</summary>
        private void Back()
        {
            App.Log("Settings: back to movement mode");
            Manager.ChangeMode(new MovementMode());
        }
    }
}
