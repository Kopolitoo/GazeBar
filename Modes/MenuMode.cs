using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// MENU MODE (step 4 of the specification).
    ///
    /// Entry: looking down, below the screen, for [MenuDelay]. The ModeManager watches for
    /// this, only from movement, pause and game modes: see Mode.MenuAllowed (step 6).
    ///
    /// Display: a bar centered at the bottom of the screen, with buttons activated by the gaze
    /// (GazeButton): one looks at a button for [ActivationDelay], its ring fills up, then it
    /// activates. From left to right:
    ///   Settings       opens settings mode (step 7);
    ///   Game           switches to game mode (step 9). Coming from game mode, this button
    ///                  becomes "Quit game" and returns to movement mode;
    ///   Keyboard       opens GazeBar's gaze keyboard (step 12, see GazeKeyboard), then closes
    ///                  the menu. If it is already open (hidden by game mode), closes it;
    ///   Pause          switches to pause mode;
    ///   Mouse tracking switch: green border when the option is on. The menu stays open; the
    ///                  gaze must leave the button before it can be used again;
    ///   Quit           asks for confirmation ("Yes, quit" / "No"), then closes GazeBar.
    ///
    /// Calibration and Diagnostic are no longer in the bar (user request of 2026-10-29): they
    /// are in the "Tools" tab of the settings (see SettingsWindow).
    ///
    /// From game mode, the "Quit game" button has a green border, like a lit switch: it shows
    /// that game mode is active (user request of 2026-10-28).
    ///
    /// From PAUSE mode (user request of 2026-10-28): the bar first shows ONLY the "Resume"
    /// button (play). This way, during the pause, a downward gaze cannot trigger anything else.
    /// When activated, it ends the pause and returns directly to movement mode. For the other
    /// buttons, one then opens the menu again.
    ///
    /// The gaze circle keeps its normal size ([CircleRadius], as in movement mode: user request
    /// of 2026-10-28; before, it was halved). It is hidden while the gaze is in the bar zone.
    ///
    /// KEYBOARD MENU BAR (step 12, user's choice of 2026-10-31): when the gaze keyboard is
    /// shown, looking below the screen does NOT open the classic bar, but the keyboard's one
    /// (KeyboardMenu = true). Since 2026-11-01 (user remarks), from left to right:
    ///   Up / down     puts the keyboard at the top of the screen, or brings it back down
    ///                 (remembered);
    ///   Size          100 % → 75 % → 50 % → 100 %... (the button shows the current size);
    ///   Input box     switch (green border): the band of the last typed words (step 14);
    ///   Language      opens the list of the layouts of the Keyboards folder (step 14);
    ///   Pause         switches to pause mode (the keyboard is hidden, and comes back on resume).
    /// No "Close" button: it duplicated the one placed above the keyboard (removed at the
    /// user's request of 2026-11-02).
    /// Up / down, Size and Input box leave the menu open, like "Mouse tracking".
    /// Language opens a full-screen list (KeyboardListMode). "My words" is in the General tab
    /// of the settings (user request of 2026-11-02). The keyboard stays shown, and if it is at
    /// the bottom it MOVES UP above the bar instead of being covered
    /// (GazeKeyboard.SetBottomReserve); it moves back down when the menu closes.
    ///
    /// Exit: when the gaze leaves the bar for [MenuCloseDelay], it disappears and we return to
    /// the previous mode. The gaze down below the screen counts as "in the bar": that is where
    /// it is when the menu opens. A lost gaze does not count (neither in nor out).
    /// </summary>
    internal sealed class MenuMode : Mode
    {
        /// <summary>The actions of the bar's buttons.</summary>
        private enum MenuAction
        {
            Settings,
            Game,
            QuitGame,
            Keyboard,
            KeyboardPosition,
            KeyboardSize,
            InputBox,
            Language,
            Pause,
            Resume,
            MouseTracking,
            Quit,
            ConfirmQuit,
            CancelQuit
        }

        /// <summary>
        /// Space between two buttons, and between the buttons and the edge of the bar, in pixels.
        /// Large enough so that the progress ring of a button does not touch its neighbour.
        /// Step 11: these three distances are pixels of the 4K screen, converted for the current
        /// screen (ScreenMetrics.Px, or ScreenMetrics.GazePx for the gaze margin).
        /// </summary>
        private const double Gap4K = 60;

        private static double Gap
        {
            get { return ScreenMetrics.Px(Gap4K); }
        }

        /// <summary>Space between the bar and the bottom of the screen, in 4K screen pixels.</summary>
        private const double BottomMargin4K = 30;

        /// <summary>
        /// Margin around the bar, in 4K screen pixels: the gaze is still "in the bar" there.
        /// Avoids closing the menu when the gaze brushes the edge.
        /// </summary>
        private const double ZoneMargin4K = 60;

        // The buttons shown, and the action of each one (same order; null = unavailable button).
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly List<MenuAction?> _actions = new List<MenuAction?>();
        private readonly ButtonAimer _aimer;

        // The mode we came from, to return to it when closing.
        private Mode _back;

        // True during the "Quit" confirmation.
        private bool _confirmation;

        // A message shown above the bar (null = none).
        private string _message;

        // True if the bar could be built (the overlay is shown).
        private bool _built;

        // True once the bar has been shown since entering the menu: only the first appearance
        // is animated (not the "Yes / No" bar of the confirmation).
        private bool _alreadyShown;

        // The bar zone (with its margin), in screen pixels.
        private double _leftZone;
        private double _rightZone;
        private double _topZone;

        // Time (ms) when the gaze left the bar; -1 = it is inside.
        private double _outsideBarStartMs = -1;

        // The gaze circle: smoothed position in screen pixels.
        private readonly Smoother _smoothed = new Smoother();
        private bool _circlePlaced;

        /// <summary>The classic menu bar.</summary>
        public MenuMode() : this(false)
        {
        }

        /// <summary>
        /// keyboardMenu = true: the menu bar of the gaze keyboard (step 12) instead of the
        /// classic bar.
        /// </summary>
        public MenuMode(bool keyboardMenu)
        {
            KeyboardMenu = keyboardMenu;
            _aimer = new ButtonAimer(_buttons);
        }

        /// <summary>True for the keyboard's menu bar (the keyboard then stays shown underneath).</summary>
        public bool KeyboardMenu { get; private set; }

        public override string Name
        {
            get { return "Menu"; }
        }

        public override void Enter(GazeState gaze)
        {
            // The ModeManager has already noted the previous mode: that is where we will return.
            _back = Manager.PreviousMode;
            _confirmation = false;
            _message = null;
            _outsideBarStartMs = -1;
            _alreadyShown = false;

            OverlayWindow layer = Manager.Layer;
            layer.HideRing();
            layer.HideIcon();
            layer.HideChoiceScreen();

            // The circle starts from where the current circle is (no jump), otherwise it will
            // appear at the first on-screen gaze measure.
            _circlePlaced = layer.CircleShown;
            if (_circlePlaced)
            {
                _smoothed.Place(layer.CircleX, layer.CircleY);
            }

            Build();
        }

        public override void Update(GazeState gaze)
        {
            if (!_built)
            {
                // Overlay not shown yet (unlikely case): do not stay in an invisible menu.
                Return();
                return;
            }

            AppSettings p = AppSettings.Current;
            double now = gaze.NowMs;

            // 1. The buttons: has one of them just activated?
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                Activate(active);
                return;
            }

            // 2. Closing: gaze outside the bar for [MenuCloseDelay].
            if (gaze.Available)
            {
                if (InBar(gaze))
                {
                    _outsideBarStartMs = -1;
                }
                else if (_outsideBarStartMs < 0)
                {
                    _outsideBarStartMs = now;
                }
                else if (now - _outsideBarStartMs >= p.MenuCloseDelay * 1000.0)
                {
                    Return();
                    return;
                }
            }

            // 3. The gaze circle. It follows the position brought back into the screen: gaze
            //    off screen → it slides along the edge (user request of 2026-10-29).
            //    It is hidden when the gaze is in the bar zone (with its margin), or down below
            //    the screen: it would hinder reading the buttons (request of 2026-10-28).
            //    Its position keeps being tracked: it reappears without a jump when leaving the
            //    zone. Gaze lost: it stays where it is.
            if (gaze.Available)
            {
                if (!_circlePlaced)
                {
                    _smoothed.Place(gaze.XScreen, gaze.YScreen);
                    _circlePlaced = true;
                }
                _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);
                // Keyboard bar (step 12): the circle is also hidden above the keyboard.
                if (InBar(gaze) || (KeyboardMenu && Manager.Keyboard.Contains(_smoothed.X, _smoothed.Y)))
                {
                    Manager.Layer.HideCircle();
                }
                else
                {
                    Manager.Layer.ShowCircle(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, false);
                }
            }
        }

        public override void Quit()
        {
            // The bar is hidden. The circle stays: the next mode takes it over where it is.
            // Animated closing (see OverlayWindow.HideMenuBar).
            Manager.Layer.HideMenuBar(true);
            _buttons.Clear();
            _actions.Clear();

            // Keyboard bar: the keyboard may move back down (it had moved up above it).
            if (KeyboardMenu)
            {
                Manager.Keyboard.SetBottomReserve(0);
            }
        }

        // ───────────────────────── Actions ─────────────────────────

        private void Activate(int num)
        {
            MenuAction action = _actions[num].Value;
            App.Log("Menu: " + action);

            switch (action)
            {
                case MenuAction.Settings:
                    Manager.ChangeMode(new SettingsMode());
                    break;

                case MenuAction.Game:
                    Manager.ChangeMode(new GameMode());
                    break;

                case MenuAction.QuitGame:
                    Manager.ChangeMode(new MovementMode());
                    break;

                case MenuAction.Keyboard:
                    // The gaze keyboard (step 12). Already open (hidden during the game): the
                    // button closes it. Otherwise, it opens and appears when back in movement
                    // mode. Layout file missing or incorrect: message.
                    if (Manager.Keyboard.IsOpen)
                    {
                        Manager.Keyboard.Close();
                        Return();
                        break;
                    }
                    string keyboardMessage;
                    if (Manager.Keyboard.Open(out keyboardMessage))
                    {
                        // From the game, we return to movement mode: that is where the keyboard shows.
                        Manager.ChangeMode(new MovementMode());
                    }
                    else
                    {
                        ShowMessage(keyboardMessage);
                    }
                    break;

                case MenuAction.KeyboardPosition:
                    // The menu stays open; the bar is rebuilt (the symbol changes) and the button
                    // waits for the gaze to leave it before it can be used again.
                    Manager.Keyboard.TogglePosition();
                    Build();
                    break;

                case MenuAction.KeyboardSize:
                    Manager.Keyboard.ChangeSize();
                    Build();
                    break;

                case MenuAction.InputBox:
                    // Switch, like "Mouse tracking": the menu stays open. The bar is rebuilt
                    // (green border); the button waits for the gaze to leave it.
                    Manager.Keyboard.ToggleInputBox();
                    Build();
                    break;

                case MenuAction.Language:
                    Manager.ChangeMode(new KeyboardListMode(KeyboardListMode.ListKind.Language));
                    break;

                case MenuAction.Pause:
                    Manager.ChangeMode(new PauseMode());
                    break;

                case MenuAction.Resume:
                    // End of the pause: straight back to movement mode, without showing the
                    // other buttons (user's choice of 2026-10-28).
                    Manager.ChangeMode(new MovementMode());
                    break;

                case MenuAction.MouseTracking:
                    // Switch: toggle the option, save it, and stay in the menu.
                    AppSettings.Current.MouseTracking = !AppSettings.Current.MouseTracking;
                    AppSettings.Current.Save();
                    _buttons[num].SetLit(AppSettings.Current.MouseTracking);
                    App.Log("Mouse tracking: " + (AppSettings.Current.MouseTracking ? "yes" : "no"));
                    _aimer.BlockUntilExit();
                    break;

                case MenuAction.Quit:
                    _confirmation = true;
                    _message = Texts.Get("menu.quitQuestion");
                    Build();
                    break;

                case MenuAction.ConfirmQuit:
                    App.Log("Menu: GazeBar exit requested");
                    Application.Current.Shutdown();
                    break;

                case MenuAction.CancelQuit:
                    _confirmation = false;
                    _message = null;
                    Build();
                    break;
            }
        }

        /// <summary>Closes the menu and returns to the previous mode (movement mode by default).</summary>
        private void Return()
        {
            Mode back = _back;
            if (back == null || back is MenuMode)
            {
                back = new MovementMode();
            }
            Manager.ChangeMode(back);
        }

        /// <summary>Shows a message above the bar (for example an error).</summary>
        private void ShowMessage(string message)
        {
            _message = message;
            Build();
        }

        // ───────────────────────── The bar ─────────────────────────

        /// <summary>
        /// (Re)builds the bar: the menu buttons, or those of the "Quit" confirmation. All
        /// positions are in screen pixels, converted by the overlay.
        /// </summary>
        private void Build()
        {
            OverlayWindow layer = Manager.Layer;
            _built = false;
            _buttons.Clear();
            _actions.Clear();
            if (!layer.Ready)
            {
                return;
            }

            AppSettings p = AppSettings.Current;
            double width = Manager.ScreenWidth;
            double height = Manager.ScreenHeight;

            layer.PrepareMenuBar();
            Canvas surface = layer.MenuBar;

            // ── The buttons to show.
            List<GazeButton> buttons = new List<GazeButton>();
            List<MenuAction?> actions = new List<MenuAction?>();
            if (_back is PauseMode && !_confirmation)
            {
                // During the pause: only the "Resume" button (E768 = play).
                Add(buttons, actions, new GazeButton("", null, Texts.Get("menu.resume")), MenuAction.Resume);
            }
            else if (KeyboardMenu && !_confirmation)
            {
                // Keyboard menu bar (step 12, see the class comment).
                //   E70E / E70D = chevron up / down, E769 = pause,
                //   E70F = pencil (input box), E774 = globe (language).
                if (p.KeyboardAtTop)
                {
                    Add(buttons, actions, new GazeButton("\uE70D", null, Texts.Get("menu.keyboardBottom")), MenuAction.KeyboardPosition);
                }
                else
                {
                    Add(buttons, actions, new GazeButton("\uE70E", null, Texts.Get("menu.keyboardTop")), MenuAction.KeyboardPosition);
                }
                Add(buttons, actions,
                        GazeButton.Text(p.KeyboardSize.ToString("F0") + " %", Texts.Get("menu.keyboardSize")),
                        MenuAction.KeyboardSize);
                // Step 14: input box (switch) and language ("My words" is in the settings).
                GazeButton zone = new GazeButton("\uE70F", null, Texts.Get("menu.inputBox"));
                zone.SetLit(p.InputBox);
                Add(buttons, actions, zone, MenuAction.InputBox);
                Add(buttons, actions, new GazeButton("\uE774", null, Texts.Get("menu.keyboardLanguage")), MenuAction.Language);
                Add(buttons, actions, new GazeButton("\uE769", null, Texts.Get("menu.pause")), MenuAction.Pause);
            }
            else if (_confirmation)
            {
                // "No" is on the right, "Yes" on the left. The bar is shorter and centered:
                // neither of them is at the place of the old "Quit" button (far right), so that
                // one never confirms by simply keeping looking at the same place.
                Add(buttons, actions, new GazeButton("", null, Texts.Get("menu.confirmQuit")), MenuAction.ConfirmQuit);
                Add(buttons, actions, new GazeButton("", null, Texts.Get("menu.cancelQuit")), MenuAction.CancelQuit);
            }
            else
            {
                // Symbols of the Windows font (Segoe MDL2 Assets / Segoe Fluent Icons):
                //   E713 gear, E7FC gamepad, E765 keyboard, E769 pause,
                //   E768 play, E962 mouse, E7E8 power button.
                Add(buttons, actions, new GazeButton("", null, Texts.Get("menu.settings")), MenuAction.Settings);
                if (_back is GameMode)
                {
                    // Green border: game mode is active.
                    GazeButton quitGame = new GazeButton("\uE7FC", null, Texts.Get("menu.quitGame"));
                    quitGame.SetLit(true);
                    Add(buttons, actions, quitGame, MenuAction.QuitGame);
                }
                else
                {
                    Add(buttons, actions, new GazeButton("\uE7FC", null, Texts.Get("menu.game")), MenuAction.Game);
                }
                Add(buttons, actions, new GazeButton("", null, Texts.Get("menu.keyboard")), MenuAction.Keyboard);
                // (From the pause, only "Resume" is shown: so here, we are never in pause.)
                Add(buttons, actions, new GazeButton("", null, Texts.Get("menu.pause")), MenuAction.Pause);
                GazeButton tracking = new GazeButton("", null, Texts.Get("menu.mouseTracking"));
                tracking.SetLit(p.MouseTracking);
                Add(buttons, actions, tracking, MenuAction.MouseTracking);
                Add(buttons, actions, new GazeButton("", null, Texts.Get("menu.quit")), MenuAction.Quit);
            }

            // ── Button size: [IconSize], reduced if the bar does not fit in the screen
            //    width.
            int n = buttons.Count;
            // The menu buttons are twice as big as the other icons
            // (user request of 2026-10-22).
            double size = 2 * p.IconSizePx;
            double maxSize = (width - ScreenMetrics.Px(20) - (n + 1) * Gap) / n;
            size = Math.Max(ScreenMetrics.Px(20), Math.Min(size, maxSize));

            // ── The bar background: a dark rectangle with rounded corners, centered at the bottom.
            double barWidth = n * size + (n + 1) * Gap;
            double barHeight = size + 2 * Gap;
            double left = (width - barWidth) / 2;
            double top = height - ScreenMetrics.Px(BottomMargin4K) - barHeight;

            Border background = new Border();
            background.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24));
            background.BorderBrush = Brushes.Gray;
            background.BorderThickness = new Thickness(ScreenMetrics.Units(1));
            background.CornerRadius = new CornerRadius(ScreenMetrics.Units(16));
            background.Width = layer.LengthToWpf(barWidth);
            background.Height = layer.LengthToWpf(barHeight);
            Point corner = layer.ToWpf(left, top);
            Canvas.SetLeft(background, corner.X);
            Canvas.SetTop(background, corner.Y);
            surface.Children.Add(background);

            // ── The buttons, from left to right (label above: below, it would be off the
            //    screen).
            for (int i = 0; i < n; i++)
            {
                double x = left + Gap + size / 2 + i * (size + Gap);
                double y = top + Gap + size / 2;
                buttons[i].Place(layer, surface, x, y, size, true);
                _buttons.Add(buttons[i]);
                _actions.Add(actions[i]);
            }

            // ── The optional message, centered above the bar.
            double zoneTop = top;
            if (!string.IsNullOrEmpty(_message))
            {
                TextBlock text = new TextBlock();
                text.Text = _message;
                text.Foreground = Brushes.White;
                // The message follows the Windows text size (step 11).
                text.FontSize = ScreenMetrics.Font(28);
                Border frame = new Border();
                frame.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24));
                frame.CornerRadius = new CornerRadius(ScreenMetrics.Units(8));
                frame.Padding = new Thickness(ScreenMetrics.Units(16), ScreenMetrics.Units(6), ScreenMetrics.Units(16), ScreenMetrics.Units(6));
                frame.Child = text;
                frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Point topCenter = layer.ToWpf(width / 2, top - ScreenMetrics.Px(20));
                Canvas.SetLeft(frame, topCenter.X - frame.DesiredSize.Width / 2);
                Canvas.SetTop(frame, topCenter.Y - frame.DesiredSize.Height);
                surface.Children.Add(frame);

                // The message is part of the bar for closing. Its measured height is in WPF
                // units: it is converted to screen pixels (step 11; it was a fixed estimate of
                // 80 px before).
                double messageHeight = frame.DesiredSize.Height / layer.LengthToWpf(1);
                zoneTop = top - ScreenMetrics.Px(20) - messageHeight;
            }

            // ── The "in the bar" zone, with its margin.
            double zoneMargin = ScreenMetrics.GazePx(ZoneMargin4K);
            _leftZone = left - zoneMargin;
            _rightZone = left + barWidth + zoneMargin;
            _topZone = zoneTop - zoneMargin;

            // Keyboard bar: the keyboard (if at the bottom) moves up above the bar and its
            // optional message, with a small gap (request of 2026-11-01).
            if (KeyboardMenu)
            {
                Manager.Keyboard.SetBottomReserve(height - zoneTop + ScreenMetrics.Px(20));
            }

            // The buttons changed: start from zero, and the button that may be under the gaze
            // does not activate until the gaze has left it.
            _aimer.ResetAndBlock();

            // First appearance since entering the menu: animated (see ShowMenuBar).
            layer.ShowMenuBar(!_alreadyShown);
            _alreadyShown = true;
            _built = true;
        }

        private static void Add(List<GazeButton> buttons, List<MenuAction?> actions,
                                    GazeButton button, MenuAction? action)
        {
            buttons.Add(button);
            actions.Add(action);
        }

        /// <summary>True if the gaze is on the bar (with its margin), or down below the screen.</summary>
        private bool InBar(GazeState gaze)
        {
            if (gaze.BelowScreen)
            {
                return true;
            }
            return gaze.X >= _leftZone && gaze.X <= _rightZone && gaze.Y >= _topZone;
        }
    }
}
