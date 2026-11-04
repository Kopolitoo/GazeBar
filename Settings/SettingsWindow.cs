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
    /// INTERFACE: the settings mode window, full screen, black background.
    ///
    /// Layout validated by the user:
    ///   - tabs on the left (SettingsCatalog), grouped in collapsible categories;
    ///     one tab shown at a time, five settings at most, nothing scrolls;
    ///   - each setting: its name, an explanation sentence, then "−", the value, "+" (or a
    ///     switch for a yes / no, see below); the default value is recalled below the value;
    ///   - on the right: "Défaut" at the top (restores the shown tab's values, after
    ///     confirmation) and "Retour" in the middle of the edge (back to movement mode).
    ///
    /// An "Icônes" tab, with a single big "Configurer les icônes" button that asks
    /// the mode to open the configuration screen (IconConfigMode).
    ///
    /// An "Outils" tab, with two big buttons "Calibration" and "Diagnostic".
    /// As for "Configurer les icônes", the window only
    /// NOTIFIES the mode (CalibrationRequested and DiagnosticRequested events): SettingsMode
    /// starts the tool and changes mode.
    ///
    /// A "Profils" tab (Système category), with two big buttons:
    ///   - "Enregistrer les réglages actuels": opens the NAME page: a text box, filled with the
    ///     dated name, where the user types the
    ///     profile name with the gaze keyboard (the mode shows it: NamingStarted /
    ///     NamingEnded), then "Enregistrer" or "Annuler". The page sits in the part of the
    ///     screen the keyboard leaves free (SetKeyboardZone). If the name already exists, a
    ///     "Oui / Non" question asks before replacing it. Then a message gives the name of
    ///     the saved profile;
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
    internal sealed partial class SettingsWindow : Window
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
        private const double TabIndent = 40;       // indent of tabs below their category
        private const double LeftContent = 460;
        private const double TopRow = 92;
        private const double StepRow = 184;           // from one setting row to the next
        private const double RowHeight = 160;
        private const double LabelWidth = 520;
        private const double Gap = 18;
        private const double SideButton = 120;         // "−", "+", button height
        private const double ValueWidth = 290;
        private const double ChoiceWidth = 260;       // the two buttons of a choice between two words

        // ── The switch of a yes / no setting (Bootstrap "switch" style).
        //    The gaze TARGET stays large (the room of two "Non" and "Oui"
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

        // The profile chosen in the list, awaiting confirmation ("Charger le
        // profil … ? Oui / Non"), or null.
        private FoundProfile _profileToConfirm;

        // Profile name page: open or not, its text box, the typed text (kept
        // when the page is rebuilt), "select all" at the first showing (the first letter typed
        // replaces the suggested name), the name waiting for "replace it?" (or null), and the
        // sentence under the box (error, or null for the help).
        private bool _naming;
        private TextBox _nameBox;
        private string _nameText;
        private bool _selectName;
        private string _nameToReplace;
        private string _nameError;

        // The part of the page left free by the gaze keyboard, in page units (whole page
        // below the title when the keyboard is not shown).
        private double _freeTop = TopRow;
        private double _freeBottom = PageHeight;

        // The keyboard input in progress (null = none).
        private Setting _editedSetting;
        private TextBox _input;

        // True when the mode closes the window (and not Alt+F4, for example).
        private bool _closeRequested;

        /// <summary>
        /// Creates the window, opened on tab number "tab" (0 = the first). When coming back
        /// from the icon configuration screen, it opens on the "Icônes" tab.
        /// When coming back from the profile list, "profileToConfirm" is the chosen
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

        /// <summary>"Configurer les icônes" button of the "Icônes" tab.</summary>
        public event Action IconConfigRequested;

        /// <summary>"Calibration" button of the "Outils" tab.</summary>
        public event Action CalibrationRequested;

        /// <summary>"Diagnostic" button of the "Outils" tab.</summary>
        public event Action DiagnosticRequested;

        /// <summary>"Mes mots" button of the "Général" tab.</summary>
        public event Action MyWordsRequested;

        /// <summary>"Charger un profil" button of the "Profils" tab.</summary>
        public event Action ProfileListRequested;

        /// <summary>"Oui" to the question "Charger le profil … ?": the profile to load.</summary>
        public event Action<FoundProfile> ProfileLoadRequested;

        /// <summary>The profile name page opens: the mode shows the gaze keyboard.</summary>
        public event Action NamingStarted;

        /// <summary>The profile name page closes: the mode hides the gaze keyboard.</summary>
        public event Action NamingEnded;

        /// <summary>
        /// The gaze keyboard is shown between "top" and "bottom" (page units, see ToPage), or
        /// not shown (top = bottom). The name page goes into the larger free part, above or
        /// below the keyboard, and is rebuilt.
        /// </summary>
        public void SetKeyboardZone(double top, double bottom)
        {
            top = Math.Max(0, Math.Min(PageHeight, top));
            bottom = Math.Max(0, Math.Min(PageHeight, bottom));
            if (bottom - top < 1)
            {
                _freeTop = TopRow;
                _freeBottom = PageHeight;
            }
            else if (top - TopRow >= PageHeight - bottom)
            {
                _freeTop = TopRow;          // keyboard at the bottom: the page above it
                _freeBottom = top;
            }
            else
            {
                _freeTop = bottom;          // keyboard at the top: the page below it
                _freeBottom = PageHeight;
            }
            if (_naming)
            {
                Build();
            }
        }

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
            // Profile name page: Escape answers "Non" to "replace it?", otherwise cancels.
            if (_naming)
            {
                if (_nameToReplace != null)
                {
                    CancelReplace();
                }
                else
                {
                    CancelNaming();
                }
                return true;
            }
            if (_editedSetting == null)
            {
                return false;
            }
            Setting setting = _editedSetting;
            _editedSetting = null;   // first, so that losing focus does not confirm the input
            RestoreDisplay(setting);
            App.LogDetail("Settings: input cancelled (" + setting.Key + ")");
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
        /// The screen changed while the window is open (called by the mode): it
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
}
