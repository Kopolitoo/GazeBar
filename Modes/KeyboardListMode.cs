using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// KEYBOARD LIST MODE ("clavier-options").
    ///
    /// Two lists: "Language" opens from the menu bar of the gaze keyboard, "My words" from
    /// the General tab of the settings:
    ///   - LANGUAGE: the layouts found in the Keyboards folder (one per JSON file). The one in
    ///     use has a green border. Looking at another one chooses it (saved in settings.json),
    ///     then we go back to the keyboard;
    ///   - MY WORDS: the words added to the personal dictionary with the "+" cell, in
    ///     alphabetical order. Each word has a red "−" badge: looking at it REMOVES it at once
    ///     (as for removing an icon in the icon configuration), and we stay in the list to be able
    ///     to remove
    ///     others. Lexique words are never affected.
    ///
    /// A third list, which has nothing to do with the
    /// keyboard but reuses the same layout:
    ///   - PROFILES: the settings profiles of the Documents\GazeBar\Profiles folder, the most
    ///     recent first (see SettingsProfiles). Looking at one returns to the settings,
    ///     Profiles tab, with the question "Load profile ...?": nothing changes without this
    ///     "Yes". "Back to settings" returns to the same tab without changing anything.
    ///
    /// Layout: the same as the list of the icon configuration: black screen,
    /// rounded rectangle over three quarters of the screen, title at the top, grid of at most
    /// 4 columns × 3 rows (12 per page). "Back" (to the keyboard for Language, to the settings
    /// for My words) is on the right of the screen, vertically centered, outside the rectangle. At
    /// the bottom of the rectangle: "Previous page" and "Next page" when the
    /// list does not fit on one page.
    ///
    /// The keyboard is hidden during this mode (it is neither movement mode nor fixed gaze)
    /// and reappears when back in movement mode. The bottom menu does not open here; the
    /// caregiver's Escape key acts like "Back" (App.OnEscape).
    ///
    /// The words are shown on the screen but never written to the log (privacy).
    /// </summary>
    internal sealed class KeyboardListMode : Mode
    {
        /// <summary>The possible lists.</summary>
        public enum ListKind
        {
            Language,
            MyWords,
            Profiles
        }

        /// <summary>Proportion of the screen taken by the list (as in the icon configuration).</summary>
        private const double ListRatio = 0.75;

        /// <summary>Grid of a page: 4 columns × 3 rows.</summary>
        private const int Columns = 4;
        private const int Rows = 3;
        private const int PerPage = Columns * Rows;

        // Colors (the same as the list of the icon configuration).
        private static readonly Brush ListBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x26)));
        private static readonly Brush Soft = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD)));

        private readonly ListKind _kind;

        // The buttons shown and what each one does (same order).
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly List<Action> _actions = new List<Action>();
        private readonly ButtonAimer _aimer;

        // The content of the list: the layouts (Language) or the words (My words).
        private List<FoundLayout> _layouts = new List<FoundLayout>();
        private List<string> _words = new List<string>();
        private List<FoundProfile> _profiles = new List<FoundProfile>();

        // The page shown (0 = the first).
        private int _page;

        // A message shown under the title (error), or null.
        private string _message;

        // True as long as the screen could not be built (overlay not ready yet).
        private bool _toBuild;

        // The small circle showing the gaze (as in the action choice).
        private Ellipse _gazeCircleBackground;
        private Ellipse _gazeCircle;
        private readonly Smoother _smoothed = new Smoother();
        private bool _gazeCirclePlaced;

        public KeyboardListMode(ListKind kind)
        {
            _kind = kind;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get
            {
                switch (_kind)
                {
                    case ListKind.Language: return Texts.Get("menu.keyboardLayout");
                    case ListKind.Profiles: return Texts.Get("tab.profiles");
                    default: return Texts.Get("settings.myWords");
                }
            }
        }

        public override void Enter(GazeState gaze)
        {
            OverlayWindow layer = Manager.Layer;
            layer.HideAll();
            layer.BringToFront();
            _page = 0;
            _message = null;

            if (_kind == ListKind.Language)
            {
                _layouts = KeyboardLayout.List();
                // Start at the page of the layout in use.
                int currentOne = CurrentLayoutIndex();
                _page = currentOne < 0 ? 0 : currentOne / PerPage;
            }
            else if (_kind == ListKind.Profiles)
            {
                _profiles = SettingsProfiles.List();
            }
            else
            {
                _words = Manager.Keyboard.Predictor.AddedWords();
            }

            _toBuild = true;
            Build();
        }

        public override void Update(GazeState gaze)
        {
            if (_toBuild)
            {
                Build();
                return;
            }

            AppSettings p = AppSettings.Current;
            // Each item already has its name written underneath: no hover label, it would
            // overlap this name (as in the icon configuration).
            int active = _aimer.Update(gaze, p.ActivationDelay, false);
            if (active >= 0)
            {
                _actions[active]();
                return;
            }

            MoveGazeCircle(gaze, p);
        }

        /// <summary>The screen changed: we stay here, everything is redrawn on the next tick.</summary>
        public override bool AdaptToScreen()
        {
            _toBuild = true;
            return true;
        }

        public override void Quit()
        {
            Manager.Layer.HideChoiceScreen();
            Manager.Layer.ChoiceScreen.Children.Clear();
            _buttons.Clear();
            _actions.Clear();
            _gazeCircle = null;
            _gazeCircleBackground = null;
        }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>
        /// "Back" button or Escape key (also called by App): Language → movement mode, where
        /// the keyboard reappears; My words → the settings, General tab; Profiles → the
        /// settings, Profiles tab.
        /// </summary>
        public void Back()
        {
            App.LogDetail(Name + ": back");
            if (_kind == ListKind.MyWords)
            {
                Manager.ChangeMode(new SettingsMode(SettingsCatalog.GeneralTabIndex));
            }
            else if (_kind == ListKind.Profiles)
            {
                Manager.ChangeMode(new SettingsMode(SettingsCatalog.ProfilesTabIndex));
            }
            else
            {
                Manager.ChangeMode(new MovementMode());
            }
        }

        private void ChangePage(int offset)
        {
            _page += offset;
            Build();
        }

        /// <summary>Language: a layout is chosen; we go back to the keyboard if it reads correctly.</summary>
        private void ChooseLayout(FoundLayout layout)
        {
            string message;
            if (Manager.Keyboard.ChangeLayout(layout.File, out message))
            {
                Manager.ChangeMode(new MovementMode());
            }
            else
            {
                // The file may have changed since the list was opened: say so.
                _message = message;
                Build();
            }
        }

        /// <summary>
        /// Profiles: a profile is chosen. We go back to the settings, Profiles tab, which asks
        /// for confirmation before loading it.
        /// </summary>
        private void ChooseProfile(FoundProfile profile)
        {
            App.LogDetail("Profiles: profile chosen, confirmation requested");
            Manager.ChangeMode(new SettingsMode(SettingsCatalog.ProfilesTabIndex, profile));
        }

        /// <summary>My words: the word is removed from the personal dictionary, the list is redrawn.</summary>
        private void RemoveWord(string word)
        {
            Manager.Keyboard.Predictor.Delete(word);
            _words.Remove(word);
            Build();
        }

        private int CurrentLayoutIndex()
        {
            string currentOne = AppSettings.Current.KeyboardLayout;
            for (int i = 0; i < _layouts.Count; i++)
            {
                if (string.Equals(_layouts[i].File, currentOne, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        // ───────────────────────── Building the screen ─────────────────────────

        /// <summary>
        /// (Re)draws the whole screen. All positions are computed in screen pixels, then
        /// converted by the overlay.
        /// </summary>
        private void Build()
        {
            OverlayWindow layer = Manager.Layer;
            if (!layer.Ready)
            {
                _toBuild = true;   // we will try again on the next tick
                return;
            }
            _toBuild = false;

            AppSettings p = AppSettings.Current;
            double width = Manager.ScreenWidth;
            double height = Manager.ScreenHeight;
            double size = p.IconSizePx;
            double space = ActionChoiceLayout.Space;

            layer.PrepareChoiceScreen();
            Canvas screen = layer.ChoiceScreen;
            _buttons.Clear();
            _actions.Clear();

            // ── The rounded rectangle, centered.
            double l = width * ListRatio;
            double h = height * ListRatio;
            double left = (width - l) / 2;
            double top = (height - h) / 2;
            double centerX = width / 2;
            Point corner = layer.ToWpf(left, top);
            Border frame = new Border();
            frame.Width = layer.LengthToWpf(l);
            frame.Height = layer.LengthToWpf(h);
            frame.CornerRadius = new CornerRadius(layer.LengthToWpf(size * 0.3));
            frame.Background = ListBackground;
            frame.BorderBrush = Brushes.White;
            frame.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            Canvas.SetLeft(frame, corner.X);
            Canvas.SetTop(frame, corner.Y);
            screen.Children.Add(frame);

            // ── The number of items and pages.
            int number = _kind == ListKind.Language ? _layouts.Count
                       : _kind == ListKind.Profiles ? _profiles.Count
                       : _words.Count;
            int pages = Math.Max(1, (number + PerPage - 1) / PerPage);
            // After a removal, the last page may be empty: go back one.
            _page = Math.Max(0, Math.Min(_page, pages - 1));

            // ── The title, and under it an explanation (or the error message).
            //    The texts follow the Windows text size.
            double titleFont = size * 0.3 * ScreenMetrics.WindowsText;
            double nameFont = size * 0.2 * ScreenMetrics.WindowsText;
            string title = _kind == ListKind.Profiles ? Texts.Get("settings.profiles.load") : Name;
            if (pages > 1)
            {
                title += Texts.Format("list.pageOf", _page + 1, pages);
            }
            double yTitle = top + size * 0.35;
            AddCenteredText(layer, screen, title, centerX, yTitle, l, titleFont, Brushes.White, true);
            string explanation = _message ?? (_kind == ListKind.Language
                ? Texts.Get("list.layouts.help")
                : _kind == ListKind.Profiles
                ? Texts.Get("list.profiles.help")
                : Texts.Get("list.words.help"));
            double yExplanation = yTitle + titleFont * 1.5;
            AddCenteredText(layer, screen, explanation, centerX, yExplanation, l, nameFont,
                               _message != null ? Brushes.Orange : Soft, false);

            // ── "Back" (cross E711): on the right of the screen, vertically centered, OUTSIDE
            //    the list rectangle (in the middle of the right margin), so as not to aim at it
            //    while looking at a word.
            string backName = _kind == ListKind.Language ? Texts.Get("list.backToKeyboard") : Texts.Get("list.backToSettings");
            double rightMargin = width - (left + l);
            double xBack = left + l + rightMargin / 2;
            double yBack = height / 2;
            AddButton(layer, screen, new GazeButton("", null, backName), Back,
                          xBack, yBack, size);
            AddCenteredText(layer, screen, backName, xBack, yBack + size / 2 + nameFont * 0.4,
                               rightMargin - space / 2, nameFont, Soft, false);

            // ── The bottom row: Previous page · Next page (E72B / E72A = arrows).
            double yBottom = top + h - size / 2 - nameFont * 2.5;
            double bottomStep = size + 2 * space;
            if (_page > 0)
            {
                AddButton(layer, screen, new GazeButton("", null, Texts.Get("list.previousPage")), () => ChangePage(-1),
                              centerX - bottomStep * 1.5, yBottom, size);
                AddCenteredText(layer, screen, Texts.Get("list.previousPage"), centerX - bottomStep * 1.5,
                                   yBottom + size / 2 + nameFont * 0.4, bottomStep, nameFont, Soft, false);
            }
            if (_page < pages - 1)
            {
                AddButton(layer, screen, new GazeButton("", null, Texts.Get("list.nextPage")), () => ChangePage(1),
                              centerX + bottomStep * 1.5, yBottom, size);
                AddCenteredText(layer, screen, Texts.Get("list.nextPage"), centerX + bottomStep * 1.5,
                                   yBottom + size / 2 + nameFont * 0.4, bottomStep, nameFont, Soft, false);
            }

            // ── The grid of the page: between the explanation and the bottom row.
            double gridTop = yExplanation + nameFont * 2.5;
            double gridBottom = yBottom - size / 2 - space / 2;
            int isFirst = _page * PerPage;
            int inPage = Math.Max(0, Math.Min(PerPage, number - isFirst));

            if (inPage == 0)
            {
                string empty = _kind == ListKind.Language
                    ? Texts.Get("list.layouts.empty")
                    : _kind == ListKind.Profiles
                    ? Texts.Get("list.profiles.empty")
                    : Texts.Get("list.words.empty");
                AddCenteredText(layer, screen, empty, centerX, (gridTop + gridBottom) / 2 - nameFont,
                                   l * 0.8, nameFont * 1.3, Soft, false);
            }
            else
            {
                int columns = Math.Min(inPage, Columns);
                int rows = (inPage + columns - 1) / columns;
                // As in the icon configuration: each item gets a cell, and the icon with its name
                // underneath
                // are centered in the cell.
                double stepX = l / columns;
                double stepY = Math.Max((gridBottom - gridTop) / rows, size + nameFont * 2 + space * 0.5);
                double yFirst = (gridTop + gridBottom) / 2 - (rows - 1) * stepY / 2 - nameFont;

                for (int i = 0; i < inPage; i++)
                {
                    int row = i / columns;
                    int column = i % columns;
                    int inRow = Math.Min(columns, inPage - row * columns);
                    double x = centerX + (column - (inRow - 1) / 2.0) * stepX;
                    double y = yFirst + row * stepY;

                    GazeButton button;
                    Action action;
                    string name;
                    if (_kind == ListKind.Language)
                    {
                        FoundLayout d = _layouts[isFirst + i];   // copy for the function below
                        // The button shows the language code ("FR"); the layout in use has a green border.
                        button = GazeButton.Text(d.Language.ToUpperInvariant(), d.Name);
                        button.SetLit(isFirst + i == CurrentLayoutIndex());
                        action = () => ChooseLayout(d);
                        name = d.Name;
                    }
                    else if (_kind == ListKind.Profiles)
                    {
                        FoundProfile profile = _profiles[isFirst + i];   // copy for the function below
                        // E77B = person; E7A7 = "undo" arrow for the automatic backup (going
                        // back to the settings from before the last load).
                        button = new GazeButton(profile.IsBackup ? "\uE7A7" : "\uE77B", null, Texts.Get("list.loadProfile"));
                        action = () => ChooseProfile(profile);
                        name = profile.DisplayName;
                    }
                    else
                    {
                        string word = _words[isFirst + i];   // copy for the function below
                        // E74D = trash can, with the red "−" badge of the icon configuration.
                        button = new GazeButton("", null, Texts.Get("list.removeWord"));
                        button.SetConfigBadge(false);
                        action = () => RemoveWord(word);
                        name = word;
                    }
                    AddButton(layer, screen, button, action, x, y, size);
                    AddCenteredText(layer, screen, name, x, y + size / 2 + nameFont * 0.4,
                                       stepX - space, nameFont, Brushes.White, false);
                }
            }

            // The small gaze circle, added LAST to be on top of everything.
            _gazeCircleBackground = new Ellipse();
            _gazeCircleBackground.Stroke = Brushes.Black;
            _gazeCircleBackground.StrokeThickness = ScreenMetrics.Units(4);
            _gazeCircleBackground.Opacity = 0.6;
            _gazeCircleBackground.Visibility = Visibility.Collapsed;
            _gazeCircle = new Ellipse();
            _gazeCircle.Stroke = Brushes.LimeGreen;
            _gazeCircle.StrokeThickness = ScreenMetrics.Units(2);
            _gazeCircle.Visibility = Visibility.Collapsed;
            screen.Children.Add(_gazeCircleBackground);
            screen.Children.Add(_gazeCircle);

            layer.ShowChoiceScreen();

            // The buttons were just replaced: the one under the gaze does not activate until
            // the gaze has left it (otherwise, after removing a word, the one taking its place
            // would be removed at once).
            _aimer.ResetAndBlock();
        }

        // ───────────────────────── Small tools ─────────────────────────

        /// <summary>Places a button (centered on x, y in screen pixels) that the gaze can activate.</summary>
        private void AddButton(OverlayWindow layer, Canvas screen, GazeButton button, Action action,
                                   double x, double y, double size)
        {
            button.ZoneFactor = ActionChoiceMode.ZoneFactor;
            button.Place(layer, screen, x, y, size);
            _buttons.Add(button);
            _actions.Add(action);
        }

        /// <summary>
        /// Writes a text horizontally centered on x, its top at y (screen pixels), over a width
        /// of "width" pixels (the text wraps beyond).
        /// </summary>
        private static void AddCenteredText(OverlayWindow layer, Canvas screen, string text, double x, double y,
                                               double width, double fontPixels, Brush color, bool bold)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = layer.LengthToWpf(fontPixels);
            block.Foreground = color;
            block.FontFamily = new FontFamily("Segoe UI");
            block.TextWrapping = TextWrapping.Wrap;
            if (bold)
            {
                block.FontWeight = FontWeights.Bold;
            }
            block.Width = layer.LengthToWpf(width);
            block.TextAlignment = TextAlignment.Center;
            Point point = layer.ToWpf(x, y);
            Canvas.SetLeft(block, point.X - block.Width / 2);
            Canvas.SetTop(block, point.Y);
            screen.Children.Add(block);
        }

        /// <summary>
        /// Moves the small gaze circle, as in the icon configuration: it follows the gaze
        /// smoothly, its radius is half of [CircleRadius]. Gaze off screen: it slides along the
        /// edge. Gaze lost: it stays where it is.
        /// </summary>
        private void MoveGazeCircle(GazeState gaze, AppSettings p)
        {
            if (_gazeCircle == null || !gaze.Available)
            {
                return;
            }
            if (!_gazeCirclePlaced)
            {
                _smoothed.Place(gaze.XScreen, gaze.YScreen);
                _gazeCirclePlaced = true;
            }
            _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);

            OverlayWindow layer = Manager.Layer;
            Point center = layer.ToWpf(_smoothed.X, _smoothed.Y);
            double radius = layer.LengthToWpf(p.CircleRadiusPx / 2);
            PlaceEllipse(_gazeCircleBackground, center, radius);
            PlaceEllipse(_gazeCircle, center, radius);
        }

        private static void PlaceEllipse(Ellipse ellipse, Point center, double radius)
        {
            ellipse.Width = radius * 2;
            ellipse.Height = radius * 2;
            Canvas.SetLeft(ellipse, center.X - radius);
            Canvas.SetTop(ellipse, center.Y - radius);
            ellipse.Visibility = Visibility.Visible;
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
