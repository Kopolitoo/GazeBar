using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Prunel
{
    /// <summary>
    /// INTERFACE: a button activated with the gaze, drawn on the overlay.
    ///
    /// It is a dark square with rounded corners, with:
    ///   - a symbol of the Windows font (Segoe Fluent Icons or Segoe MDL2 Assets);
    ///   - optionally a small text "badge" at the bottom right ("G", "D", "2×") to tell apart
    ///     actions sharing the same symbol;
    ///   - a progress ring that fills up while it is looked at;
    ///   - a text label, shown on hover if [ShowLabels] is on.
    ///
    /// A button can be "empty" (Empty method): a slot with no action assigned yet. It is drawn
    /// grayed, with a pale "+", to show the user that an action can be put there (icon
    /// configuration screen). It never activates; only its label appears on hover.
    ///
    /// A button can also be "unavailable" (Unavailable method): its function exists in
    /// the specification but is not programmed yet (for example "Settings" in the menu bar).
    /// It keeps its real symbol, but grayed, and never activates either.
    ///
    /// A button can be "lit" (SetLit method): a switch in the "yes" position, like
    /// "Mouse tracking" in the menu bar. Its border becomes green.
    ///
    /// A button can show a word instead of a symbol (Text method): this is the case of
    /// the "Ctrl", "Maj" and "Alt" modifiers, which have no meaningful symbol in the Windows
    /// icon font.
    ///
    /// For the icon configuration screen:
    ///   - an empty slot can be "configurable" (ConfigurableEmpty method): grayed like an empty
    ///     slot, but it activates (to place an action there);
    ///   - a round badge at the top right (SetConfigBadge method): green "+" on an empty slot,
    ///     red "−" on an assigned slot.
    ///
    /// It does not know itself whether it is looked at nor for how long: the mode
    /// (ActionChoiceMode, MenuMode) decides, with the help of a ButtonAimer, and tells it what
    /// to draw (Draw method).
    ///
    /// Positions are given in SCREEN PIXELS, like the gaze; the conversion to WPF units is
    /// done here, with the help of the overlay.
    ///
    /// Thicknesses, rounded corners, margins and the label font are constants in
    /// units of the reference screen, converted with ScreenMetrics.Units and
    /// ScreenMetrics.Font at Place time (see ScreenMetrics.cs).
    ///
    /// It "implements" IGazeTarget: this way ButtonAimer can aim at a GazeButton
    /// as well as at a target of settings mode (SettingTarget).
    /// </summary>
    internal sealed class GazeButton : IGazeTarget
    {
        // The WPF elements making up the button.
        private readonly Border _background;
        private readonly TextBlock _symbol;
        private readonly TextBlock _badge;
        private readonly Path _ring;
        private readonly Border _label;

        // The round "+" / "−" badge of the configuration screen (null = none).
        private Border _configBadge;
        private TextBlock _configSign;

        // True for a button drawn grayed (empty slot, unavailable function).
        private bool _grayed;

        // Size of the symbol relative to the button side (smaller for a word, see Text).
        private double _symbolFactor = 0.45;

        // Colors (frozen once and for all, WPF then draws them faster).
        private static readonly Brush NormalBackground = Freeze(new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x21, 0x24)));
        private static readonly Brush HoverBackground = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x3C, 0x40, 0x43)));
        private static readonly Brush White = Freeze(new SolidColorBrush(Colors.White));
        private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53)));
        private static readonly Brush EmptyBackground = Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0x50, 0x50, 0x50)));
        private static readonly Brush EmptyHoverBackground = Freeze(new SolidColorBrush(Color.FromArgb(0xA0, 0x60, 0x60, 0x60)));
        private static readonly Brush Gray = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x90, 0x90, 0x90)));
        private static readonly Brush Red = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0x39, 0x35)));

        /// <summary>
        /// Creates an empty slot (grayed, never activated), with this label on hover.
        /// Symbol E710 = "+" (add) of the Windows font.
        /// </summary>
        public static GazeButton Empty(string label)
        {
            return Unavailable("\uE710", label);
        }

        /// <summary>
        /// Creates a button showing a word (for example "Ctrl") instead of a symbol: the
        /// ordinary Windows font (Segoe UI), in bold, smaller to fit in the square.
        /// </summary>
        public static GazeButton Text(string word, string label)
        {
            GazeButton button = new GazeButton(word, null, label);
            button._symbol.FontFamily = new FontFamily("Segoe UI");
            button._symbol.FontWeight = FontWeights.Bold;
            button._symbolFactor = 0.28;
            return button;
        }

        /// <summary>
        /// Creates a grayed button that never activates, with its real symbol: for a function
        /// planned but not programmed yet.
        /// </summary>
        public static GazeButton Unavailable(string symbol, string label)
        {
            GazeButton button = new GazeButton(symbol, null, label);
            button.Active = false;
            button._grayed = true;
            button._background.Background = EmptyBackground;
            button._background.BorderBrush = Gray;
            button._symbol.Foreground = Gray;
            button._symbol.Opacity = 0.6;
            return button;
        }

        /// <summary>
        /// Creates an empty slot of the configuration screen: grayed, without a
        /// symbol (the "+" badge is enough), but it activates, to place an action there.
        /// </summary>
        public static GazeButton ConfigurableEmpty(string label)
        {
            GazeButton button = Unavailable("", label);
            button.Active = true;
            return button;
        }

        /// <summary>
        /// Adds the round badge of the configuration screen, at the top right:
        /// plus = true → "+" on a green background (empty slot, an action can be placed);
        /// plus = false → "−" on a red background (assigned action, it can be removed).
        /// To call BEFORE Place. ("−" is the real minus sign, U+2212.)
        /// </summary>
        public void SetConfigBadge(bool plus)
        {
            _configSign = new TextBlock();
            _configSign.Text = plus ? "+" : "\u2212";
            _configSign.Foreground = White;
            _configSign.FontFamily = new FontFamily("Segoe UI");
            _configSign.FontWeight = FontWeights.Bold;
            _configSign.HorizontalAlignment = HorizontalAlignment.Center;
            _configSign.VerticalAlignment = VerticalAlignment.Center;
            // The sign is a bit too low in its text line: lift it slightly.
            _configSign.Margin = new Thickness(0, 0, 0, ScreenMetrics.Units(4));

            _configBadge = new Border();
            _configBadge.Background = plus ? Green : Red;
            _configBadge.BorderBrush = White;
            _configBadge.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            _configBadge.Child = _configSign;
        }

        /// <summary>
        /// Creates the button.
        /// symbol: the character of the icon font (for example "" for "back");
        /// badge: a short text, or null for none;
        /// label: the text shown on hover.
        /// </summary>
        public GazeButton(string symbol, string badge, string label)
        {
            Label = label;
            Active = true;

            _symbol = new TextBlock();
            _symbol.Text = symbol;
            _symbol.Foreground = White;
            _symbol.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            _symbol.HorizontalAlignment = HorizontalAlignment.Center;
            _symbol.VerticalAlignment = VerticalAlignment.Center;

            _badge = new TextBlock();
            _badge.Text = badge ?? "";
            _badge.Foreground = White;
            _badge.FontWeight = FontWeights.Bold;
            _badge.HorizontalAlignment = HorizontalAlignment.Right;
            _badge.VerticalAlignment = VerticalAlignment.Bottom;
            _badge.Margin = new Thickness(0, 0, ScreenMetrics.Units(8), ScreenMetrics.Units(4));
            _badge.Visibility = badge == null ? Visibility.Collapsed : Visibility.Visible;

            // A grid lets the symbol (in the center) and the badge (in the corner) overlap.
            Grid content = new Grid();
            content.Children.Add(_symbol);
            content.Children.Add(_badge);

            _background = new Border();
            _background.Background = NormalBackground;
            _background.BorderBrush = White;
            _background.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            _background.CornerRadius = new CornerRadius(ScreenMetrics.Units(12));
            _background.Child = content;

            _ring = new Path();
            _ring.Stroke = Green;
            _ring.StrokeThickness = ScreenMetrics.Units(6);
            _ring.StrokeStartLineCap = PenLineCap.Round;
            _ring.StrokeEndLineCap = PenLineCap.Round;
            _ring.Visibility = Visibility.Collapsed;

            TextBlock text = new TextBlock();
            text.Text = label;
            text.Foreground = White;
            // The label font also follows the Windows text size.
            text.FontSize = ScreenMetrics.Font(20);
            _label = new Border();
            _label.Background = NormalBackground;
            _label.CornerRadius = new CornerRadius(ScreenMetrics.Units(6));
            _label.Padding = new Thickness(ScreenMetrics.Units(8), ScreenMetrics.Units(2), ScreenMetrics.Units(8), ScreenMetrics.Units(2));
            _label.Child = text;
            _label.Visibility = Visibility.Collapsed;
        }

        /// <summary>False for an empty or unavailable slot: it never activates (no ring).</summary>
        public bool Active { get; private set; }

        /// <summary>True for a switch in the "yes" position (green border). See SetLit.</summary>
        public bool Lit { get; private set; }

        /// <summary>
        /// Lights the button on or off: a lit button has a thick green border, to show that an
        /// option is on (for example "Mouse tracking").
        /// </summary>
        public void SetLit(bool lit)
        {
            Lit = lit;
            if (Active)
            {
                _background.BorderBrush = lit ? Green : White;
                _background.BorderThickness = new Thickness(ScreenMetrics.Units(lit ? 5 : 2));
            }
        }

        /// <summary>The text shown on hover (and written to the log on activation).</summary>
        public string Label { get; private set; }

        /// <summary>Center of the button, in screen pixels.</summary>
        public double X { get; private set; }

        public double Y { get; private set; }

        /// <summary>Side of the button, in screen pixels.</summary>
        public double Size { get; private set; }

        /// <summary>
        /// Adds the button to a surface of the overlay (for example the black screen) and
        /// places it, centered on (x, y) in screen pixels, with a side of [size] pixels.
        /// labelAbove: the label is shown above the button instead of below (for the menu bar,
        /// stuck to the bottom of the screen: below, it would be hidden).
        /// "= false": optional parameter; if it is not given, it is false.
        /// </summary>
        public void Place(OverlayWindow layer, Canvas surface, double x, double y, double size,
                           bool labelAbove = false)
        {
            X = x;
            Y = y;
            Size = size;

            Point center = layer.ToWpf(x, y);
            double side = layer.LengthToWpf(size);

            _background.Width = side;
            _background.Height = side;
            _symbol.FontSize = side * _symbolFactor;
            _badge.FontSize = side * 0.2;
            Canvas.SetLeft(_background, center.X - side / 2);
            Canvas.SetTop(_background, center.Y - side / 2);

            // The ring surrounds the square: radius = half diagonal of the square, plus a small
            // gap. Its shape changes on each tick (Draw); it is placed at (0, 0) of the surface.
            Canvas.SetLeft(_ring, 0);
            Canvas.SetTop(_ring, 0);
            _ringRadius = side / 2 * Math.Sqrt(2) + ScreenMetrics.Units(4);
            _centerWpf = center;

            // The label is centered under the button (or above, if requested).
            _label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(_label, center.X - _label.DesiredSize.Width / 2);
            if (labelAbove)
            {
                Canvas.SetTop(_label, center.Y - side / 2 - ScreenMetrics.Units(12) - _label.DesiredSize.Height);
            }
            else
            {
                Canvas.SetTop(_label, center.Y + side / 2 + ScreenMetrics.Units(12));
            }

            surface.Children.Add(_background);

            // The "+" / "−" badge: a circle of about a third of the side, whose center
            // is a bit inside the top-right corner (it overflows the square).
            if (_configBadge != null)
            {
                double diameter = side * 0.34;
                _configBadge.Width = diameter;
                _configBadge.Height = diameter;
                _configBadge.CornerRadius = new CornerRadius(diameter / 2);
                _configSign.FontSize = diameter * 0.8;
                double xBadge = center.X + side / 2 - diameter * 0.2;
                double yBadge = center.Y - side / 2 + diameter * 0.2;
                Canvas.SetLeft(_configBadge, xBadge - diameter / 2);
                Canvas.SetTop(_configBadge, yBadge - diameter / 2);
                surface.Children.Add(_configBadge);
            }

            surface.Children.Add(_ring);
            surface.Children.Add(_label);
        }

        private Point _centerWpf;
        private double _ringRadius;

        /// <summary>
        /// Size of the ACTIVATION ZONE (the zone where the gaze "touches" the button), relative
        /// to the drawn square. 1 = exactly the square (default value); 1.3 = a square 30 %
        /// larger, centered on the button. The drawing does not change: only the sensitive zone
        /// grows (user request for the action choice, the magnifier...), which
        /// forgives small gaze errors better.
        /// Note: the zone must not overlap a neighbouring button.
        /// </summary>
        public double ZoneFactor { get; set; } = 1.0;

        /// <summary>
        /// True if the gaze point (screen pixels) is in the activation zone of the button: a
        /// square of side [Size] × [ZoneFactor], centered on the button.
        /// </summary>
        public bool Contains(double x, double y)
        {
            double halfSide = Size * ZoneFactor / 2;
            return Math.Abs(x - X) <= halfSide && Math.Abs(y - Y) <= halfSide;
        }

        /// <summary>
        /// Updates the button's look.
        /// progress: from 0 (empty ring, hidden) to 1 (full ring);
        /// hover: true if it is looked at (lighter background);
        /// showLabel: shows the text under the button.
        /// </summary>
        public void Draw(double progress, bool hover, bool showLabel)
        {
            if (!_grayed)
            {
                _background.Background = hover ? HoverBackground : NormalBackground;
            }
            else
            {
                _background.Background = hover ? EmptyHoverBackground : EmptyBackground;
            }

            if (Active && progress > 0)
            {
                _ring.Data = OverlayWindow.ArcShape(_centerWpf, _ringRadius, Math.Min(progress, 1));
                _ring.Visibility = Visibility.Visible;
            }
            else
            {
                _ring.Visibility = Visibility.Collapsed;
            }

            _label.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
