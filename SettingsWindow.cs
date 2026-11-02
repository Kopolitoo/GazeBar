using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE: the settings mode window (step 7), full screen, black background.
    ///
    /// Layout validated by the user on 2026-10-26:
    ///   - tabs on the left (SettingsCatalog), grouped in collapsible categories since
    ///     2026-11-02; one tab shown at a time, five settings at most, nothing scrolls;
    ///   - each setting: its name, an explanation sentence, then "−", the value, "+" (or a
    ///     switch for a yes / no, see below); the default value is recalled below the value;
    ///   - on the right: "Défaut" at the top (restores the shown tab's values, after
    ///     confirmation) and "Retour" in the middle of the edge (back to movement mode).
    ///
    /// Step 10: an "Icônes" tab, with a single big "Configurer les icônes" button that asks
    /// the mode to open the configuration screen (IconConfigMode).
    ///
    /// 2026-10-29: an "Outils" tab, with two big buttons "Calibration" and "Diagnostic"
    /// (they used to be in the menu bar). As for "Configurer les icônes", the window only
    /// NOTIFIES the mode (CalibrationRequested and DiagnosticRequested events): SettingsMode
    /// starts the tool and changes mode.
    ///
    /// Step 15: a "Profils" tab (Système category), with two big buttons:
    ///   - "Enregistrer les réglages actuels": done right here (SettingsProfiles), a
    ///     message gives the name of the created profile;
    ///   - "Charger un profil": notifies the mode (ProfileListRequested), which opens the
    ///     full-screen profile list. The chosen profile comes back here as a "Oui / Non"
    ///     confirmation (constructor, "profileToConfirm"); "Oui" notifies the mode
    ///     (ProfileLoadRequested), which loads the profile.
    ///
    /// WHY A REAL WINDOW, and not a drawing on the overlay like the other modes?
    /// The overlay lets clicks through and never takes the keyboard. But the caregiver must
    /// be able to click with the mouse and type a value on the keyboard (or dictate it).
    /// This window therefore accepts mouse and keyboard. The gaze is handled by
    /// SettingsMode, which asks the window where its targets are.
    ///
    /// THE PAGE: everything is drawn on a 1920 × 1080 unit surface (_page), placed in a
    /// "Viewbox" that scales it to the real screen size. Positions written here are thus
    /// those of a 1920 × 1080 screen, whatever the screen. Buttons keep a fixed size:
    /// changing [IconSize] does not move what is being looked at.
    /// </summary>
    internal sealed class SettingsWindow : Window
    {
        public const double PageWidth = 1920;
        public const double PageHeight = 1080;

        // ── Layout (in units of the 1920 × 1080 page).
        private const double LeftTab = 60;
        private const double TabWidth = 340;
        private const double TabMaxHeight = 140;   // height of a tab when there is room
        private const double TabGap = 20;         // gap between two tabs
        private const double TopTabs = 60;         // top of the first tab
        private const double BottomTabs = 1040;        // the last tab stops before this line
        private const double TabIndent = 40;       // indent of tabs below their category (2026-11-02)
        private const double LeftContent = 460;
        private const double TopRow = 92;
        private const double StepRow = 184;           // from one setting row to the next
        private const double RowHeight = 160;
        private const double LabelWidth = 520;
        private const double Gap = 18;
        private const double SideButton = 120;         // "−", "+", button height
        private const double ValueWidth = 290;
        private const double ChoiceWidth = 260;       // the two buttons of a choice between two words

        // ── The switch of a yes / no setting (2026-11-02, Bootstrap "switch" style).
        //    The gaze TARGET stays large (the whole room of the former "Non" and "Oui"
        //    buttons: 2 × 260 + 18 = 538 × 120); the drawn switch sits inside it.
        private const double SwitchWidth = 150;   // the track (the "pill")
        private const double SwitchHeight = 76;
        private const double SwitchButton = 60;     // the sliding knob
        private static readonly Brush ActiveTrack = Freeze(new SolidColorBrush(Color.FromRgb(0x0D, 0x6E, 0xFD)));   // Bootstrap blue
        private const double RightLeft = 1700;      // right column (Défaut, Retour)
        private const double RightSide = 160;
        private const double GazeDotDiameter = 50;

        // ── Colours.
        private static readonly Brush White = Freeze(new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF4)));
        private static readonly Brush Soft = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAD)));
        private static readonly Brush Border = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x52, 0x5C)));
        private static readonly Brush ValueBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x1D, 0x22)));

        private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";

        // The page, its content (rebuilt on every tab change) and the gaze dot.
        private readonly Canvas _page;
        private readonly Canvas _content;
        private readonly Ellipse _gazeDot;

        // The gaze targets, in creation order.
        private readonly List<SettingTarget> _targets = new List<SettingTarget>();

        // For each numeric setting shown: its value frame (for keyboard input).
        private readonly Dictionary<Setting, ValueFrame> _frames = new Dictionary<Setting, ValueFrame>();

        private int _tab;
        private bool _confirmation;

        // A message shown in the "Outils" tab (for example "Calibration impossible…") or
        // "Profils" ("Profil chargé…"), or null. Cleared when the tab changes.
        private string _message;

        // Step 15: the profile chosen in the list, awaiting confirmation ("Charger le
        // profil … ? Oui / Non"), or null.
        private FoundProfile _profileToConfirm;

        // The keyboard input in progress (null = none).
        private Setting _editedSetting;
        private TextBox _input;

        // True when the mode closes the window (and not Alt+F4, for example).
        private bool _closeRequested;

        /// <summary>
        /// Creates the window, opened on tab number "tab" (0 = the first). When coming back
        /// from the icon configuration screen, it opens on the "Icônes" tab.
        /// Step 15: when coming back from the profile list, "profileToConfirm" is the chosen
        /// profile; the page then shows the question "Charger le profil … ?".
        /// </summary>
        public SettingsWindow(int tab = 0, FoundProfile profileToConfirm = null)
        {
            _tab = Math.Max(0, Math.Min(tab, SettingsCatalog.Tabs.Count - 1));
            _profileToConfirm = profileToConfirm;

            Title = Texts.Get("settings.windowTitle");
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            Background = Brushes.Black;

            _page = new Canvas();
            _page.Width = PageWidth;
            _page.Height = PageHeight;
            _page.Background = Brushes.Black;

            _content = new Canvas();
            _page.Children.Add(_content);

            // The small grey gaze dot, above everything, and transparent to the mouse.
            _gazeDot = new Ellipse();
            _gazeDot.Width = GazeDotDiameter;
            _gazeDot.Height = GazeDotDiameter;
            _gazeDot.Stroke = Brushes.LightGray;
            _gazeDot.StrokeThickness = 4;
            _gazeDot.IsHitTestVisible = false;
            _gazeDot.Visibility = Visibility.Collapsed;
            _page.Children.Add(_gazeDot);

            // The Viewbox scales the 1920 × 1080 page to the screen, without distorting it.
            Viewbox scale = new Viewbox();
            scale.Stretch = Stretch.Uniform;
            scale.Child = _page;
            Content = scale;

            Build();
        }

        // ───────────────────────── What the mode uses ─────────────────────────

        /// <summary>Request to go back to movement mode ("Retour" button, or closing by Windows).</summary>
        public event Action BackRequested;

        /// <summary>"Configurer les icônes" button of the "Icônes" tab (step 10).</summary>
        public event Action IconConfigRequested;

        /// <summary>"Calibration" button of the "Outils" tab (2026-10-29).</summary>
        public event Action CalibrationRequested;

        /// <summary>"Diagnostic" button of the "Outils" tab (2026-10-29).</summary>
        public event Action DiagnosticRequested;

        /// <summary>"Mes mots" button of the "Général" tab (step 14).</summary>
        public event Action MyWordsRequested;

        /// <summary>"Charger un profil" button of the "Profils" tab (step 15).</summary>
        public event Action ProfileListRequested;

        /// <summary>"Oui" to the question "Charger le profil … ?" (step 15): the profile to load.</summary>
        public event Action<FoundProfile> ProfileLoadRequested;

        /// <summary>
        /// Shows a message in the open tab ("Outils": calibration could not be started;
        /// "Profils": the profile is loaded…). The page is rebuilt to show it.
        /// </summary>
        public void ShowMessage(string message)
        {
            _message = message;
            Build();
        }

        /// <summary>
        /// The gaze targets. The same list is kept throughout: it is only cleared and filled
        /// again when the page is rebuilt.
        /// </summary>
        public IReadOnlyList<IGazeTarget> Targets
        {
            get { return _targets; }
        }

        /// <summary>
        /// Increases on every page rebuild (new tab, confirmation…). The mode uses it to know
        /// the targets changed. "−" and "+" do not rebuild the page (only the value text
        /// changes): repetition can go on.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>True if activating this target can repeat ("−" and "+").</summary>
        public bool RepeatableTarget(int num)
        {
            return _targets[num].Repeat;
        }

        /// <summary>Activates the given target (the gaze rested on it long enough).</summary>
        public void Activate(int num)
        {
            _targets[num].Action();
        }

        /// <summary>
        /// Converts a screen point (in real pixels, like the gaze) to a point of the
        /// 1920 × 1080 page. PointFromScreen takes into account the Windows scaling AND the
        /// Viewbox scaling. Before the window is shown, returns a point very far away.
        /// </summary>
        public Point ToPage(double xScreen, double yScreen)
        {
            if (PresentationSource.FromVisual(_page) == null)
            {
                return new Point(-10000, -10000);
            }
            return _page.PointFromScreen(new Point(xScreen, yScreen));
        }

        /// <summary>Places the small gaze dot (page coordinates).</summary>
        public void PlaceGazeDot(Point point)
        {
            Canvas.SetLeft(_gazeDot, point.X - GazeDotDiameter / 2);
            Canvas.SetTop(_gazeDot, point.Y - GazeDotDiameter / 2);
            _gazeDot.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Escape key during keyboard input: the input is cancelled (the value does not
        /// change) and the settings stay open. Returns true if an input was in progress;
        /// otherwise Escape keeps its usual role (back to movement mode).
        /// </summary>
        public bool CancelInput()
        {
            if (_editedSetting == null)
            {
                return false;
            }
            Setting setting = _editedSetting;
            _editedSetting = null;   // first, so that losing focus does not confirm the input
            RestoreDisplay(setting);
            App.Log("Settings: input cancelled (" + setting.Key + ")");
            return true;
        }

        /// <summary>Closes the window (called by the mode when it is left).</summary>
        public void CloseSettings()
        {
            // An input in progress is confirmed, as if the caregiver had pressed Enter.
            ConfirmInput();
            _closeRequested = true;
            Close();
        }

        // ───────────────────────── Window ─────────────────────────

        /// <summary>
        /// Once Windows has created the window, give it the exact size of the primary screen,
        /// as for the overlay (see OverlayWindow.CoverScreen).
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            CoverScreen();
        }

        /// <summary>
        /// The screen changed while the window is open (step 11, called by the mode): it
        /// takes the screen size again, and the page is rebuilt (an input in progress is
        /// confirmed).
        /// </summary>
        public void AdaptToScreen()
        {
            CoverScreen();
            Build();
        }

        /// <summary>Gives the window the exact size of the primary screen.</summary>
        private void CoverScreen()
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null || source.CompositionTarget == null)
            {
                return;
            }
            double widthPixels;
            double heightPixels;
            NativeMethods.PrimaryScreenSize(out widthPixels, out heightPixels);
            Point size = source.CompositionTarget.TransformFromDevice.Transform(new Point(widthPixels, heightPixels));
            Left = 0;
            Top = 0;
            Width = size.X;
            Height = size.Y;
        }

        /// <summary>
        /// Closing requested by Windows (Alt+F4…) and not by the mode: it is cancelled, and a
        /// return to movement mode is requested instead, which will close the window cleanly.
        /// "BeginInvoke": a little later, because a window cannot be closed while it is
        /// closing.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_closeRequested)
            {
                e.Cancel = true;
                Dispatcher.BeginInvoke(new Action(RequestBack));
            }
            base.OnClosing(e);
        }

        private void RequestBack()
        {
            Action subscribers = BackRequested;
            if (subscribers != null)
            {
                subscribers();
            }
        }

        // ───────────────────────── Building the page ─────────────────────────

        private static AppSettings P
        {
            get { return AppSettings.Current; }
        }

        /// <summary>(Re)builds the whole page for the chosen tab.</summary>
        private void Build()
        {
            // An input in progress is confirmed before rebuilding everything.
            ConfirmInput();

            _content.Children.Clear();
            _targets.Clear();
            _frames.Clear();

            SettingsTab tab = SettingsCatalog.Tabs[_tab];

            // ── The title, at the top.
            TextBlock title = Text(Texts.Format("settings.title", tab.Label), 30, Soft, false);
            Place(title, LeftContent, 26);

            // ── The left column: the "collapsible" CATEGORIES (2026-11-02, user's choice).
            //    All four categories are visible; only the one holding the open tab shows its
            //    tabs, right below it and indented.
            //    At most 4 + 3 = 7 buttons (instead of 11 before), hence taller buttons.
            BuildLeftColumn();

            // ── The content: the settings, or the "Défaut" confirmation, or the profile
            //    load confirmation (step 15).
            if (_confirmation)
            {
                BuildConfirmation(tab);
            }
            else if (_profileToConfirm != null)
            {
                BuildProfileConfirmation();
            }
            else
            {
                for (int i = 0; i < tab.Settings.Count; i++)
                {
                    BuildRow(tab.Settings[i], TopRow + i * StepRow);
                }
                if (tab.Name == SettingsCatalog.GeneralTabName)
                {
                    BuildFileInfo(TopRow + tab.Settings.Count * StepRow);
                    BuildMyWordsButton();
                }
                if (tab.Name == DisplayTabName)
                {
                    BuildScreenInfo(TopRow + tab.Settings.Count * StepRow);
                }
                if (tab.Name == SettingsCatalog.IconsTabName)
                {
                    BuildIconsTab();
                }
                if (tab.Name == SettingsCatalog.ToolsTabName)
                {
                    BuildToolsTab();
                }
                if (tab.Name == SettingsCatalog.ProfilesTabName)
                {
                    BuildProfilesTab();
                }
            }

            // ── The right column: "Défaut" at the top, "Retour" in the middle of the edge.
            //    E72C = circular arrows (restore), E72B = back arrow.
            //    No "Défaut" for a tab with nothing to restore ("Outils", "Profils"): a button
            //    that does nothing would be confusing.
            bool hasDefaults = tab.Settings.Count > 0 || tab.Name == SettingsCatalog.IconsTabName;
            if (hasDefaults)
            {
                AddTarget(RightLeft, 60, RightSide, RightSide, SymbolAndWord("", Texts.Get("settings.defaults")),
                             RequestDefaults, false, false);
            }
            AddTarget(RightLeft, (PageHeight - RightSide) / 2, RightSide, RightSide,
                         SymbolAndWord("", Texts.Get("common.back")), RequestBack, false, false);

            Version++;
        }

        /// <summary>
        /// The left column: the categories, and below the open category its tabs.
        ///
        /// Looking at another category opens it directly on its first tab (a single look, as
        /// before to change tab). Looking at the already open category does nothing. The open
        /// tab is framed as before; the open category has its symbol in colour.
        /// </summary>
        private void BuildLeftColumn()
        {
            int openCategory = SettingsCatalog.CategoryOf(_tab);
            List<int> openTabs = SettingsCatalog.TabsOf(openCategory);

            // Number of rows in the column: all categories + the tabs of the open category.
            // Rows share the page height, without exceeding the maximum height (140): with 7
            // rows, each one is about 120 high.
            int rowCount = SettingsCatalog.Categories.Count + openTabs.Count;
            double step = Math.Min(TabMaxHeight + TabGap,
                                  (BottomTabs - TopTabs + TabGap) / Math.Max(1, rowCount));

            double y = TopTabs;
            for (int c = 0; c < SettingsCatalog.Categories.Count; c++)
            {
                SettingsCategory category = SettingsCatalog.Categories[c];
                bool open = c == openCategory;

                // The category: symbol (in colour if open) and name.
                UIElement categoryRow = RowColumn(category.Symbol, category.Label, open, 46, 36, 24);
                int categoryIndex = c;   // copy for the lambda below
                AddTarget(LeftTab, y, TabWidth, step - TabGap, categoryRow,
                             () => ChooseCategory(categoryIndex), false, false);
                y += step;

                if (!open)
                {
                    continue;
                }

                // The tabs of the open category, indented to the right (the indent shows they
                // "belong" to the category above).
                foreach (int tabIndex in openTabs)
                {
                    SettingsTab o = SettingsCatalog.Tabs[tabIndex];
                    UIElement tabRow = RowColumn(o.Symbol, o.Label, tabIndex == _tab, 34, 30, 14);
                    int num = tabIndex;   // copy for the lambda below
                    AddTarget(LeftTab + TabIndent, y, TabWidth - TabIndent, step - TabGap,
                                 tabRow, () => ChooseTab(num), false, tabIndex == _tab);
                    y += step;
                }
            }
        }

        /// <summary>
        /// The content of a left column button: a symbol then a name. Tabs have slightly
        /// smaller text and a shorter margin than categories, so that "Loupe, glisser" fits
        /// in the indented button.
        /// </summary>
        private static UIElement RowColumn(string symbol, string name, bool inColor,
                                              double symbolSize, double nameSize, double margin)
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Margin = new Thickness(margin, 0, 0, 0);
            TextBlock symbolBlock = Text(symbol, symbolSize, inColor ? SettingTarget.Accent : Soft, false);
            symbolBlock.FontFamily = new FontFamily(IconFont);
            symbolBlock.Width = symbolSize + 22;
            symbolBlock.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(symbolBlock);
            TextBlock nameBlock = Text(name, nameSize, White, true);
            nameBlock.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(nameBlock);
            return row;
        }

        /// <summary>A setting row: name and explanation, then the buttons.</summary>
        private void BuildRow(Setting setting, double top)
        {
            // Name and explanation, vertically centred in the row height.
            StackPanel texts = new StackPanel();
            texts.Width = LabelWidth;
            texts.Height = RowHeight;
            StackPanel center = new StackPanel();
            center.VerticalAlignment = VerticalAlignment.Center;
            center.Children.Add(Text(setting.Name, 36, White, true));
            TextBlock explanation = Text(setting.Explanation, 23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            center.Children.Add(explanation);
            Grid textFrame = new Grid();
            textFrame.Width = LabelWidth;
            textFrame.Height = RowHeight;
            textFrame.Children.Add(center);
            Place(textFrame, LeftContent, top);

            double x = LeftContent + LabelWidth + Gap;
            double y = top + (RowHeight - SideButton) / 2;

            if (setting.Switch)
            {
                // A yes / no: a single big target; looking at it toggles the value.
                // SettingsMode blocks the target until the gaze leaves it: staying on it
                // therefore does not toggle the switch over and over.
                bool current = setting.ReadYesNo(P);
                AddTarget(x, y, 2 * ChoiceWidth + Gap, SideButton, SwitchDrawing(current, current ? setting.RightWord : setting.WrongWord),
                             () => ChangeYesNo(setting, !current), false, false);
                return;
            }

            if (setting.YesNo)
            {
                // A choice between two words ("Directe" / "Relative"…): two buttons.
                bool val = setting.ReadYesNo(P);
                AddTarget(x, y, ChoiceWidth, SideButton, Word(setting.WrongWord, 42), () => ChangeYesNo(setting, false), false, !val);
                AddTarget(x + ChoiceWidth + Gap, y, ChoiceWidth, SideButton, Word(setting.RightWord, 42), () => ChangeYesNo(setting, true), false, val);
                return;
            }

            // "−" (U+2212, the true minus sign), the value, "+". Both buttons repeat.
            AddTarget(x, y, SideButton, SideButton, Word("−", 72), () => ChangeNumber(setting, -1), true, false);
            AddTarget(x + SideButton + Gap + ValueWidth + Gap, y, SideButton, SideButton, Word("+", 72),
                         () => ChangeNumber(setting, +1), true, false);

            // The value: a frame that is NOT a gaze target. The caregiver clicks on it to type
            // a value on the keyboard.
            ValueFrame frame = new ValueFrame();
            frame.Value = Text(setting.Text(P), 50, White, true);
            frame.Value.HorizontalAlignment = HorizontalAlignment.Center;
            AppSettings defaults = new AppSettings();
            TextBlock callbackFn = Text(Texts.Format("settings.defaultValue", setting.Text(defaults)), 20, Soft, false);
            callbackFn.HorizontalAlignment = HorizontalAlignment.Center;
            StackPanel display = new StackPanel();
            display.VerticalAlignment = VerticalAlignment.Center;
            display.Children.Add(frame.Value);
            display.Children.Add(callbackFn);
            frame.Display = display;

            frame.Border = new Border();
            frame.Border.Width = ValueWidth;
            frame.Border.Height = SideButton;
            frame.Border.CornerRadius = new CornerRadius(14);
            frame.Border.BorderThickness = new Thickness(2);
            frame.Border.BorderBrush = Border;
            frame.Border.Background = ValueBackground;
            frame.Border.Cursor = Cursors.IBeam;
            frame.Border.ToolTip = Texts.Get("settings.typeValueTooltip");
            frame.Border.Child = display;
            frame.Border.MouseLeftButtonUp += (s, e) =>
            {
                StartInput(setting);
                e.Handled = true;
            };
            Place(frame.Border, x + SideButton + Gap, y);
            _frames[setting] = frame;
        }

        /// <summary>Name of the "Affichage" tab, below which the detected screen is recalled (step 11).</summary>
        private const string DisplayTabName = "tab.display";

        /// <summary>
        /// "Affichage" tab (step 11): the screen detected by GazeBar (Windows resolution,
        /// scaling and text size), to check that the change was seen.
        /// </summary>
        private void BuildScreenInfo(double top)
        {
            StackPanel info = new StackPanel();
            info.Children.Add(Text(Texts.Get("settings.screenDetected"), 36, White, true));
            TextBlock description = Text(ScreenMetrics.Description(), 24, Soft, false);
            description.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(description);
            info.Children.Add(Text(Texts.Get("settings.sizesInPercent"), 23, Soft, false));
            Place(info, LeftContent, top + 30);
        }

        /// <summary>Général tab: reminder of where the settings are saved.</summary>
        private void BuildFileInfo(double top)
        {
            StackPanel info = new StackPanel();
            info.Children.Add(Text(Texts.Get("settings.file"), 36, White, true));
            TextBlock path = Text(AppSettings.FilePath, 24, Soft, false);
            path.FontFamily = new FontFamily("Consolas");
            path.TextWrapping = TextWrapping.Wrap;
            path.Width = 1140;
            path.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(path);
            info.Children.Add(Text(Texts.Get("settings.savedOnChange"), 23, Soft, false));
            Place(info, LeftContent, top + 30);
        }

        /// <summary>
        /// "Général" tab (step 14, user request of 2026-11-02): a big "Mes mots" button, below
        /// the settings file reminder. It opens the list of words added to the keyboard's
        /// personal dictionary (KeyboardListMode), to remove some. The Clavier tab had no room
        /// left (five settings). E82D = dictionary.
        /// </summary>
        private void BuildMyWordsButton()
        {
            AddTarget(LeftContent, 640, 570, 180, BigButton("\uE82D", Texts.Get("settings.myWords")),
                         () => Notify(MyWordsRequested), false, false);
            // Step langues-socle: the interface language, next to "Mes mots". E774 = globe.
            AddTarget(LeftContent + 630, 640, 570, 180, BigButton("\uE774", Texts.LanguageName),
                         NextLanguage, false, false);
            // Below the buttons: the message of the language button if there is one, otherwise
            // the explanation.
            TextBlock explanation = string.IsNullOrEmpty(_message)
                ? Text(Texts.Get("settings.myWordsHelp"), 23, Soft, false)
                : Text(_message, 30, SettingTarget.Accent, true);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            Place(explanation, LeftContent, 840);
        }

        /// <summary>
        /// "Icônes" tab (step 10): an explanation, then a big button that opens the action
        /// choice icon configuration screen. E71D = grid of squares.
        /// </summary>
        private void BuildIconsTab()
        {
            StackPanel texts = new StackPanel();
            texts.Children.Add(Text(Texts.Get("settings.icons.title"), 36, White, true));
            TextBlock explanation = Text(
                Texts.Get("settings.icons.help"), 23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            texts.Children.Add(explanation);
            Place(texts, LeftContent, TopRow + 20);

            StackPanel button = new StackPanel();
            button.Orientation = Orientation.Horizontal;
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.VerticalAlignment = VerticalAlignment.Center;
            TextBlock symbol = Text("\uE71D", 60, White, false);
            symbol.FontFamily = new FontFamily(IconFont);
            symbol.VerticalAlignment = VerticalAlignment.Center;
            symbol.Margin = new Thickness(0, 0, 30, 0);
            button.Children.Add(symbol);
            TextBlock word = Text(Texts.Get("settings.icons.configure"), 42, White, true);
            word.VerticalAlignment = VerticalAlignment.Center;
            button.Children.Add(word);

            AddTarget(LeftContent + 170, 420, 800, 180, button, RequestIconConfig, false, false);
        }

        /// <summary>
        /// "Outils" tab (2026-10-29): two big buttons one above the other, with a wide gap
        /// between them so that aiming at one does not activate the other.
        ///   E890 = eye (Calibration), E9D9 = diagnostic (curve).
        /// Below the buttons: the possible message (calibration impossible…).
        /// </summary>
        private void BuildToolsTab()
        {
            StackPanel texts = new StackPanel();
            texts.Children.Add(Text(Texts.Get("tab.tools"), 36, White, true));
            TextBlock explanation = Text(
                Texts.Get("settings.tools.help"),
                23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            texts.Children.Add(explanation);
            Place(texts, LeftContent, TopRow + 20);

            AddTarget(LeftContent + 170, 320, 800, 180, BigButton("\uE890", Texts.Get("settings.tools.calibration")),
                         () => Notify(CalibrationRequested), false, false);
            AddTarget(LeftContent + 170, 620, 800, 180, BigButton("\uE9D9", Texts.Get("settings.tools.diagnostic")),
                         () => Notify(DiagnosticRequested), false, false);

            BuildMessage();
        }

        /// <summary>The tab's possible message (below the big buttons), in blue.</summary>
        private void BuildMessage()
        {
            if (!string.IsNullOrEmpty(_message))
            {
                TextBlock message = Text(_message, 30, SettingTarget.Accent, true);
                message.TextWrapping = TextWrapping.Wrap;
                message.Width = 1140;
                Place(message, LeftContent, 870);
            }
        }

        /// <summary>
        /// "Profils" tab (step 15): an explanation with the profiles folder, then two big
        /// buttons one above the other (same height as those of "Outils", but wider for the
        /// long text, with the same wide gap). E74E = floppy disk (Enregistrer), E8E5 = open
        /// folder (Charger). Below the buttons: the message ("Réglages enregistrés dans…").
        /// </summary>
        private void BuildProfilesTab()
        {
            StackPanel texts = new StackPanel();
            texts.Children.Add(Text(Texts.Get("settings.profiles.title"), 36, White, true));
            TextBlock explanation = Text(
                Texts.Get("settings.profiles.help"),
                23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            texts.Children.Add(explanation);
            TextBlock folder = Text(SettingsProfiles.Folder, 22, Soft, false);
            folder.FontFamily = new FontFamily("Consolas");
            folder.TextWrapping = TextWrapping.Wrap;
            folder.Width = 1140;
            folder.Margin = new Thickness(0, 4, 0, 0);
            texts.Children.Add(folder);
            Place(texts, LeftContent, TopRow + 20);

            AddTarget(LeftContent + 70, 320, 1000, 180, BigButton("\uE74E", Texts.Get("settings.profiles.save")),
                         SaveProfile, false, false);
            AddTarget(LeftContent + 70, 620, 1000, 180, BigButton("\uE8E5", Texts.Get("settings.profiles.load")),
                         () => Notify(ProfileListRequested), false, false);

            BuildMessage();
        }

        /// <summary>
        /// The question "Charger le profil … ?" (step 15), like the "Défaut" confirmation:
        /// "Oui" and "Non" in the middle of the page. For the automatic backup, the question
        /// rather says the previous settings come back.
        /// </summary>
        private void BuildProfileConfirmation()
        {
            string questionText = _profileToConfirm.IsBackup
                ? Texts.Get("settings.profiles.restoreQuestion")
                : Texts.Format("settings.profiles.loadQuestion", _profileToConfirm.DisplayName);
            TextBlock question = Text(questionText, 40, White, true);
            question.TextWrapping = TextWrapping.Wrap;
            question.Width = 1140;
            Place(question, LeftContent, 260);

            TextBlock precision = Text(
                Texts.Format("settings.profiles.loadHelp", Texts.Get("profiles.backupName")), 23, Soft, false);
            precision.TextWrapping = TextWrapping.Wrap;
            precision.Width = 1140;
            Place(precision, LeftContent, 380);

            AddTarget(640, 480, 300, SideButton, Word(Texts.Get("common.yes"), 42), ConfirmProfile, false, false);
            AddTarget(1040, 480, 300, SideButton, Word(Texts.Get("common.no"), 42), CancelProfile, false, false);
        }

        /// <summary>"Enregistrer les réglages actuels" button: a new dated profile.</summary>
        private void SaveProfile()
        {
            ConfirmInput();
            string message;
            SettingsProfiles.SaveCurrent(out message);
            ShowMessage(message);
        }

        /// <summary>"Oui": the mode loads the profile (then shows the message here).</summary>
        private void ConfirmProfile()
        {
            FoundProfile profile = _profileToConfirm;
            _profileToConfirm = null;
            Action<FoundProfile> subscribers = ProfileLoadRequested;
            if (subscribers != null && profile != null)
            {
                subscribers(profile);
            }
            else
            {
                Build();
            }
        }

        /// <summary>"Non": back to the "Profils" tab, nothing changes.</summary>
        private void CancelProfile()
        {
            App.Log("Profiles: load cancelled");
            _profileToConfirm = null;
            Build();
        }

        /// <summary>The content of a big button: a symbol on the left, then a word.</summary>
        private static UIElement BigButton(string symbol, string word)
        {
            StackPanel button = new StackPanel();
            button.Orientation = Orientation.Horizontal;
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.VerticalAlignment = VerticalAlignment.Center;
            TextBlock icon = Text(symbol, 60, White, false);
            icon.FontFamily = new FontFamily(IconFont);
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 30, 0);
            button.Children.Add(icon);
            TextBlock text = Text(word, 42, White, true);
            text.VerticalAlignment = VerticalAlignment.Center;
            button.Children.Add(text);
            return button;
        }

        /// <summary>
        /// Notifies the mode (raises the given event, if it has subscribers). An input in
        /// progress is confirmed first.
        /// </summary>
        private void Notify(Action evt)
        {
            ConfirmInput();
            if (evt != null)
            {
                evt();
            }
        }

        private void RequestIconConfig()
        {
            ConfirmInput();
            Action subscribers = IconConfigRequested;
            if (subscribers != null)
            {
                subscribers();
            }
        }

        /// <summary>
        /// The "Défaut" confirmation. "Oui" and "Non" are in the middle of the page, far from
        /// the "Défaut" button (top right): confirmation never happens by simply keeping the
        /// gaze at the same place.
        /// </summary>
        private void BuildConfirmation(SettingsTab tab)
        {
            string questionText = tab.Name == SettingsCatalog.IconsTabName
                ? Texts.Get("settings.icons.defaultsQuestion")
                : Texts.Format("settings.defaultsQuestion", tab.Label);
            TextBlock question = Text(questionText, 40, White, true);
            question.TextWrapping = TextWrapping.Wrap;
            question.Width = 1140;
            Place(question, LeftContent, 300);

            AddTarget(640, 480, 300, SideButton, Word(Texts.Get("common.yes"), 42), () => RestoreDefaults(tab), false, false);
            AddTarget(1040, 480, 300, SideButton, Word(Texts.Get("common.no"), 42), CancelDefaults, false, false);
        }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>
        /// Gaze on a category: if it is not open yet, it opens on its first tab. If it is
        /// already open, nothing changes (the current tab is not left by mistake).
        /// </summary>
        private void ChooseCategory(int category)
        {
            if (category == SettingsCatalog.CategoryOf(_tab))
            {
                return;
            }
            List<int> tabs = SettingsCatalog.TabsOf(category);
            if (tabs.Count > 0)
            {
                App.Log("Settings: category " + SettingsCatalog.Categories[category].Name);
                ChooseTab(tabs[0]);
            }
        }

        /// <summary>
        /// "Langue" button of the Général tab (step langues-socle): switches to the next
        /// language of the Languages folder (in code order, back to the first after the last).
        /// The choice is saved and the page is redrawn at once in the new language. With a
        /// single language file, a message says so.
        /// </summary>
        private void NextLanguage()
        {
            ConfirmInput();
            List<KeyValuePair<string, string>> languages = Texts.Available();
            if (languages.Count < 2)
            {
                _message = Texts.Get("settings.language.onlyOne");
                Build();
                return;
            }
            int index = languages.FindIndex(l => l.Key == Texts.Language);
            string next = languages[(index + 1) % languages.Count].Key;
            P.Language = next;
            P.Save();
            Texts.Load(next);
            App.Log("Settings: Language = " + next);
            _message = null;
            Title = Texts.Get("settings.windowTitle");
            Build();
        }

        private void ChooseTab(int num)
        {
            _tab = num;
            _confirmation = false;
            _profileToConfirm = null;
            _message = null;
            App.Log("Settings: tab " + SettingsCatalog.Tabs[num].Name);
            Build();
        }

        /// <summary>"−" or "+": only the value text changes (no rebuild).</summary>
        private void ChangeNumber(Setting setting, int direction)
        {
            ConfirmInput();
            setting.Change(P, direction);
            Save(setting);
            ValueFrame frame;
            if (_frames.TryGetValue(setting, out frame))
            {
                frame.Value.Text = setting.Text(P);
            }
        }

        private void ChangeYesNo(Setting setting, bool val)
        {
            if (setting.ReadYesNo(P) == val)
            {
                return;   // already chosen: nothing to do
            }
            setting.WriteYesNo(P, val);
            Save(setting);
            Build();   // to redraw the switch (or frame the new choice)
        }

        private void RequestDefaults()
        {
            _confirmation = true;
            Build();
        }

        private void CancelDefaults()
        {
            _confirmation = false;
            Build();
        }

        /// <summary>Restores the tab's settings to their initial value (read from a new object).</summary>
        private void RestoreDefaults(SettingsTab tab)
        {
            AppSettings defaults = new AppSettings();
            if (tab.Name == SettingsCatalog.IconsTabName)
            {
                P.DefaultIcons();   // the "Icônes" tab has no setting, but a layout
            }
            foreach (Setting setting in tab.Settings)
            {
                setting.Copy(defaults, P);
                if (setting.AfterChange != null)
                {
                    setting.AfterChange(P);
                }
            }
            P.Save();
            App.Log("Settings: default values of tab " + tab.Name);
            _confirmation = false;
            Build();
        }

        /// <summary>Saves the file after a change, and logs it.</summary>
        private static void Save(Setting setting)
        {
            if (setting.AfterChange != null)
            {
                setting.AfterChange(P);
            }
            P.Save();
            App.Log("Settings: " + setting.Key + " = " + setting.Text(P));
        }

        // ───────────────────────── Keyboard input (for the caregiver) ─────────────────────────

        /// <summary>
        /// Replaces the value display with a text box where the caregiver types (or dictates)
        /// a value. Enter confirms; clicking elsewhere also confirms; Escape cancels (see
        /// CancelInput, called by App when the Escape key is pressed).
        /// </summary>
        private void StartInput(Setting setting)
        {
            if (_editedSetting == setting)
            {
                return;
            }
            ConfirmInput();

            ValueFrame frame;
            if (!_frames.TryGetValue(setting, out frame))
            {
                return;
            }

            _input = new TextBox();
            _input.Text = setting.NumberText(setting.ReadNumber(P));
            _input.FontSize = 46;
            _input.Width = ValueWidth - 30;
            _input.HorizontalContentAlignment = HorizontalAlignment.Center;
            _input.VerticalAlignment = VerticalAlignment.Center;
            _input.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ConfirmInput();
                    e.Handled = true;
                }
            };
            _input.LostKeyboardFocus += (s, e) => ConfirmInput();

            _editedSetting = setting;
            frame.Border.Child = _input;

            // The window must have the keyboard for typing to reach the text box.
            Activate();
            _input.Focus();
            Keyboard.Focus(_input);
            _input.SelectAll();
        }

        /// <summary>Confirms the input in progress, if any.</summary>
        private void ConfirmInput()
        {
            if (_editedSetting == null)
            {
                return;
            }
            Setting setting = _editedSetting;
            string text = _input.Text;
            _editedSetting = null;   // first, so it is not confirmed twice (focus loss)

            double val;
            if (Setting.ReadInput(text, out val))
            {
                setting.WriteNumber(P, val);   // brought back within its limits by Fix
                Save(setting);
            }
            else
            {
                App.Log("Settings: unreadable input for " + setting.Key + ": \"" + text + "\"");
            }
            RestoreDisplay(setting);
        }

        /// <summary>Puts the normal value display back in place of the text box.</summary>
        private void RestoreDisplay(Setting setting)
        {
            ValueFrame frame;
            if (_frames.TryGetValue(setting, out frame))
            {
                frame.Value.Text = setting.Text(P);
                frame.Border.Child = frame.Display;
            }
            _input = null;
        }

        // ───────────────────────── Small helpers ─────────────────────────

        /// <summary>
        /// Creates a gaze target at this place, with this content. It also responds to mouse
        /// clicks (for the caregiver). repeat: true for "−" and "+"; chosen: true for the
        /// shown tab or the current choice (Non / Oui), framed in blue.
        /// </summary>
        private void AddTarget(double x, double y, double width, double height, UIElement content,
                                  Action action, bool repeat, bool chosen)
        {
            SettingTarget target = new SettingTarget(new Rect(x, y, width, height), content, action, repeat, chosen);
            target.Background.MouseLeftButtonUp += (s, e) =>
            {
                action();
                e.Handled = true;
            };
            Place(target.Background, x, y);
            _targets.Add(target);
        }

        private void Place(UIElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            _content.Children.Add(element);
        }

        private static TextBlock Text(string text, double size, Brush color, bool bold)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = size;
            block.Foreground = color;
            block.FontFamily = new FontFamily("Segoe UI");
            if (bold)
            {
                block.FontWeight = FontWeights.Bold;
            }
            return block;
        }

        /// <summary>A centred word (or sign), for a button's content.</summary>
        private static UIElement Word(string word, double size)
        {
            TextBlock block = Text(word, size, White, true);
            block.HorizontalAlignment = HorizontalAlignment.Center;
            block.VerticalAlignment = VerticalAlignment.Center;
            return block;
        }

        /// <summary>
        /// The drawing of a switch (like the Bootstrap "switch"), followed by the word "Oui" or
        /// "Non". The track is a Border with fully round ends (CornerRadius = half the
        /// height), the knob is an Ellipse placed left or right:
        ///   - on: solid blue track, white knob on the RIGHT;
        ///   - off: dark track with grey border, grey knob on the LEFT.
        /// It is only a drawing: the target that contains it reacts to the gaze.
        /// </summary>
        private static UIElement SwitchDrawing(bool active, string word)
        {
            Border track = new Border();
            track.Width = SwitchWidth;
            track.Height = SwitchHeight;
            track.CornerRadius = new CornerRadius(SwitchHeight / 2);
            track.BorderThickness = new Thickness(3);
            track.BorderBrush = active ? ActiveTrack : Soft;
            track.Background = active ? ActiveTrack : ValueBackground;
            track.VerticalAlignment = VerticalAlignment.Center;

            // The knob: margin so it does not touch the edge of the track.
            double margin = (SwitchHeight - 6 - SwitchButton) / 2;
            Ellipse round = new Ellipse();
            round.Width = SwitchButton;
            round.Height = SwitchButton;
            round.Fill = active ? White : Soft;
            round.HorizontalAlignment = active ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            round.VerticalAlignment = VerticalAlignment.Center;
            round.Margin = new Thickness(margin);
            track.Child = round;

            TextBlock text = Text(word, 42, active ? White : Soft, true);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(30, 0, 0, 0);
            text.MinWidth = 90;   // "Oui" and "Non": the switch does not move

            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.HorizontalAlignment = HorizontalAlignment.Center;
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(track);
            row.Children.Add(text);
            return row;
        }

        /// <summary>An icon-font symbol above a word (Défaut and Retour buttons).</summary>
        private static UIElement SymbolAndWord(string symbol, string word)
        {
            StackPanel stack = new StackPanel();
            stack.HorizontalAlignment = HorizontalAlignment.Center;
            stack.VerticalAlignment = VerticalAlignment.Center;
            TextBlock icon = Text(symbol, 60, White, false);
            icon.FontFamily = new FontFamily(IconFont);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(icon);
            TextBlock text = Text(word, 24, White, false);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(text);
            return stack;
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        /// <summary>The value frame of a numeric setting, and what it contains.</summary>
        private sealed class ValueFrame
        {
            public Border Border;
            public UIElement Display;
            public TextBlock Value;
        }
    }

    /// <summary>
    /// INTERFACE: a gaze target of settings mode (tab, "−", "+", "Non", "Oui", "Défaut",
    /// "Retour"). A dark rectangle with rounded corners; while it is looked at, its
    /// background lightens and a green bar fills at the bottom, from left to right (the
    /// equivalent of the round buttons' ring, more readable on a rectangle as wide as a
    /// tab).
    ///
    /// Its zone is in coordinates of the 1920 × 1080 page (see SettingsWindow).
    /// </summary>
    internal sealed class SettingTarget : IGazeTarget
    {
        public static readonly Brush Accent = Freeze(new SolidColorBrush(Color.FromRgb(0x6F, 0xB4, 0xFF)));
        private static readonly Brush NormalBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x36)));
        private static readonly Brush HoverBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x45, 0x4B, 0x54)));
        private static readonly Brush ChosenBackground = Freeze(new SolidColorBrush(Color.FromRgb(0x12, 0x26, 0x3D)));
        private static readonly Brush Border = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x52, 0x5C)));
        private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53)));

        private const double BarHeight = 10;

        private readonly Rect _zone;
        private readonly bool _chosen;
        private readonly Rectangle _bar;

        public SettingTarget(Rect zone, UIElement content, Action action, bool repeat, bool chosen)
        {
            _zone = zone;
            _chosen = chosen;
            Action = action;
            Repeat = repeat;

            _bar = new Rectangle();
            _bar.Fill = Green;
            _bar.Height = BarHeight;
            _bar.HorizontalAlignment = HorizontalAlignment.Left;
            _bar.VerticalAlignment = VerticalAlignment.Bottom;
            _bar.Width = 0;
            _bar.Visibility = Visibility.Collapsed;
            _bar.IsHitTestVisible = false;

            // A grid stacks the content (centre) and the bar (bottom).
            Grid grid = new Grid();
            grid.Children.Add(content);
            grid.Children.Add(_bar);

            Background = new Border();
            Background.Width = zone.Width;
            Background.Height = zone.Height;
            Background.CornerRadius = new CornerRadius(14);
            Background.BorderThickness = new Thickness(chosen ? 4 : 2);
            Background.BorderBrush = chosen ? Accent : Border;
            Background.Background = chosen ? ChosenBackground : NormalBackground;
            Background.ClipToBounds = true;
            Background.Cursor = System.Windows.Input.Cursors.Hand;
            Background.Child = grid;
        }

        /// <summary>The displayed element (to place on the page).</summary>
        public Border Background { get; private set; }

        /// <summary>What happens when the target activates.</summary>
        public Action Action { get; private set; }

        /// <summary>True if activation repeats while the gaze stays on it ("−" and "+").</summary>
        public bool Repeat { get; private set; }

        public bool Active
        {
            get { return true; }
        }

        public bool Contains(double x, double y)
        {
            return _zone.Contains(x, y);
        }

        public void Draw(double progress, bool hover, bool showLabel)
        {
            if (hover)
            {
                Background.Background = HoverBackground;
            }
            else
            {
                Background.Background = _chosen ? ChosenBackground : NormalBackground;
            }

            if (progress > 0)
            {
                // Inner width: the target width minus its two borders.
                double inner = _zone.Width - 2 * Background.BorderThickness.Left;
                _bar.Width = Math.Max(0, inner * Math.Min(progress, 1));
                _bar.Visibility = Visibility.Visible;
            }
            else
            {
                _bar.Visibility = Visibility.Collapsed;
            }
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
