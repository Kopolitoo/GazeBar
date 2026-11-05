using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE: a key of the gaze keyboard, drawn on the overlay.
    ///
    /// It is a rectangle with slightly rounded corners, with its label in the center. As on
    /// the user's screenshots:
    ///   - the keys that type a letter have no background ("letter" style);
    ///   - the others have a dark background ("function" style);
    ///   - the digits, F1 to F12 and the arrows have a lighter background ("light" style).
    ///
    /// What one sees when looking at it:
    ///   - on hover, it gets slightly lighter: NO progress ring. The hover background is
    ///     only a little lighter, and a thin light-blue outline
    ///     fades in ([OutlineDurationMs]);
    ///   - when it activates, it "flashes" green for a short moment (Flash).
    /// An active modifier (Shift, Caps Lock, Ctrl, Alt, Win) has a green border, like the lit
    /// switches of the menu bar.
    /// With [KeyboardTransparency], the normal key background lets what is behind show
    /// through; the label, the hover background and the green flash stay opaque.
    ///
    /// Like GazeButton, it "implements" IGazeTarget: ButtonAimer decides whether it is looked
    /// at and when it activates, and tells it what to draw.
    /// Positions are in SCREEN PIXELS (like the gaze), converted by the overlay.
    /// </summary>
    internal sealed class KeyboardKey : IGazeTarget
    {
        /// <summary>Duration of the green flash on activation, in milliseconds.</summary>
        private const int FlashDurationMs = 150;

        /// <summary>Duration of the hover outline fade, in milliseconds.</summary>
        private const int OutlineDurationMs = 200;

        /// <summary>Final opacity of the hover outline ("slightly colored").</summary>
        private const double OutlineOpacity = 0.8;

        // Colors (frozen once and for all, WPF then draws them faster).
        private static readonly Brush LetterBackground = Freeze(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
        private static readonly Brush FunctionBackground = Freeze(Color.FromArgb(0xFF, 0x26, 0x27, 0x2A));
        private static readonly Brush LightBackground = Freeze(Color.FromArgb(0xFF, 0x3A, 0x3B, 0x3E));
        // Hover background: only a little lighter than the key.
        private static readonly Brush HoverBackground = Freeze(Color.FromArgb(0xFF, 0x46, 0x48, 0x4D));
        private static readonly Brush FlashBackground = Freeze(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53));
        private static readonly Brush White = Freeze(Colors.White);
        private static readonly Brush Gray = Freeze(Color.FromArgb(0xFF, 0x90, 0x90, 0x90));
        private static readonly Brush Green = Freeze(Color.FromArgb(0xFF, 0x00, 0xC8, 0x53));
        // Text color of the "+ myword" cell (unknown word to add), to tell it apart
        // from an ordinary suggestion (amber).
        private static readonly Brush Amber = Freeze(Color.FromArgb(0xFF, 0xFF, 0xC8, 0x57));

        private readonly Border _background;
        private readonly TextBlock _text;
        private readonly Brush _normalBackground;

        // The hover outline, light blue. Not frozen: its opacity changes during the fade.
        private readonly SolidColorBrush _outline = new SolidColorBrush(Color.FromRgb(0x7F, 0xB8, 0xF0));

        // True for an active modifier: its green border comes before the hover outline.
        private bool _lit;

        // Time (Environment.TickCount) when the hover started; null = not hovered.
        private int? _hoverStart;

        // Time (Environment.TickCount, in ms) when the flash ends; 0 = no flash.
        private int _flashEnd;

        // Rectangle of the ACTIVATION ZONE, in screen pixels: the whole grid cell, including
        // the space around the drawing. This way, there is no "hole" between two keys where
        // the gaze would aim at nothing.
        private double _left;
        private double _top;
        private double _right;
        private double _bottom;

        // Size of the key drawing, in WPF units (remembered by Place): used to choose the text
        // size of a suggestion, which changes with each typed letter.
        private double _widthWpf;
        private double _cellHeightWpf;

        /// <summary>
        /// Creates the key described by the layout file. backgroundOpacity: opacity of the
        /// normal key background, from 0 (invisible) to 1 (opaque), see [KeyboardTransparency].
        /// </summary>
        public KeyboardKey(LayoutKey description, double backgroundOpacity)
        {
            Description = description;

            // A suggestion cell is empty, so inactive, as long as it shows no word
            // (see ShowSuggestion).
            Active = !description.Prediction.HasValue;

            string style = (description.Style ?? "").Trim().ToLowerInvariant();
            if (style == "")
            {
                style = description.Text != null ? "letter" : "function";
            }
            if (description.Prediction.HasValue)
            {
                style = "function";
            }
            _normalBackground = Translucent(
                style == "light" ? LightBackground : style == "letter" ? LetterBackground : FunctionBackground,
                backgroundOpacity);

            _text = new TextBlock();
            _text.Foreground = Active ? White : Gray;
            // "Segoe UI Symbol" second: WPF looks there for the symbols missing from Segoe UI
            // (⌫, ⇧, ␣, ↺...).
            _text.FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol");
            _text.TextAlignment = TextAlignment.Center;
            // A suggestion stays on one line (a long word is written smaller, see
            // ShowSuggestion); the other labels may span two lines.
            _text.TextWrapping = description.Prediction.HasValue ? TextWrapping.NoWrap : TextWrapping.Wrap;
            _text.HorizontalAlignment = HorizontalAlignment.Center;
            _text.VerticalAlignment = VerticalAlignment.Center;

            _background = new Border();
            _background.Background = _normalBackground;
            _background.BorderBrush = Green;
            _background.BorderThickness = new Thickness(0);
            _background.CornerRadius = new CornerRadius(ScreenMetrics.Units(4));
            _background.Child = _text;

            ShowUppercase(false);
        }

        /// <summary>What the layout file says for this key.</summary>
        public LayoutKey Description { get; private set; }

        /// <summary>False for an empty suggestion cell (no word to suggest).</summary>
        public bool Active { get; private set; }

        /// <summary>
        /// Places the key on the keyboard surface.
        /// left, top, width, height: the grid CELL, in screen pixels;
        /// gap: the space left around the drawing, in screen pixels.
        /// </summary>
        public void Place(OverlayWindow layer, Canvas surface, double left, double top,
                           double width, double height, double gap)
        {
            _left = left;
            _top = top;
            _right = left + width;
            _bottom = top + height;

            Point corner = layer.ToWpf(left + gap / 2, top + gap / 2);
            double l = layer.LengthToWpf(Math.Max(1, width - gap));
            double h = layer.LengthToWpf(Math.Max(1, height - gap));
            _background.Width = l;
            _background.Height = h;
            Canvas.SetLeft(_background, corner.X);
            Canvas.SetTop(_background, corner.Y);

            // Text size: large for a single letter, smaller for a word ("Caps Lock",
            // "Enter"...), which can then span two lines.
            string label = _text.Text ?? "";
            double cellHeight = layer.LengthToWpf(height);
            _text.FontSize = Math.Max(1, cellHeight * (label.Length <= 2 ? 0.30 : 0.20));
            _text.Margin = new Thickness(ScreenMetrics.Units(4));
            _widthWpf = l;
            _cellHeightWpf = cellHeight;
            if (Description.Prediction.HasValue)
            {
                FitFontSize();
            }

            surface.Children.Add(_background);
        }

        /// <summary>
        /// Writes the label in uppercase (Shift or Caps Lock active) or as it is in the file.
        /// Only the keys that type text change (a → A, é → É).
        /// </summary>
        public void ShowUppercase(bool uppercaseLetters)
        {
            if (Description.Prediction.HasValue)
            {
                return;   // a suggestion cell has its own text (ShowSuggestion)
            }
            string label = Description.Label ?? "";
            if (uppercaseLetters && Description.Text != null)
            {
                label = Description.ShiftText ?? label.ToUpper(CultureInfo.CurrentCulture);
            }
            _text.Text = label;
        }

        /// <summary>
        /// The word shown in a suggestion cell. null = empty cell (it does not
        /// activate). newVal = true for the "+ myword" cell (unknown word to add to the personal
        /// dictionary), written in amber.
        /// </summary>
        public void ShowSuggestion(string text, bool newVal)
        {
            if (!Description.Prediction.HasValue)
            {
                return;
            }
            Active = !string.IsNullOrEmpty(text);
            _text.Text = text ?? "";
            _text.Foreground = newVal ? Amber : White;
            FitFontSize();
        }

        /// <summary>
        /// Text size of a suggestion: like a word on a key (20 % of the cell height... a bit
        /// more: 24 %), but smaller if the word is too long for the width. A Segoe UI letter is
        /// on average a bit more than half the text size wide: we count 0.55.
        /// </summary>
        private void FitFontSize()
        {
            if (_cellHeightWpf <= 0)
            {
                return;   // not placed yet: Place will take care of it
            }
            int number = Math.Max(1, (_text.Text ?? "").Length);
            double usableWidth = Math.Max(1, _widthWpf - 2 * ScreenMetrics.Units(8));
            double size = Math.Min(_cellHeightWpf * 0.24, usableWidth / (0.55 * number));
            _text.FontSize = Math.Max(1, size);
        }

        /// <summary>Green border for an active modifier (Shift, Caps Lock, Ctrl, Alt, Win).</summary>
        public void SetLit(bool lit)
        {
            _lit = lit;
            _hoverStart = null;
            ApplyBorder(false);
        }

        /// <summary>
        /// The key border: green (thick) for an active modifier; otherwise, on hover, the
        /// light-blue outline whose opacity rises from 0 to [OutlineOpacity] in [OutlineDurationMs].
        /// </summary>
        private void ApplyBorder(bool hover)
        {
            if (_lit)
            {
                _background.BorderBrush = Green;
                _background.BorderThickness = new Thickness(ScreenMetrics.Units(3));
                return;
            }
            if (!hover)
            {
                _hoverStart = null;
                _background.BorderThickness = new Thickness(0);
                return;
            }
            if (!_hoverStart.HasValue)
            {
                _hoverStart = Environment.TickCount;
            }
            double elapsedMs = Environment.TickCount - _hoverStart.Value;
            _outline.Opacity = OutlineOpacity * Math.Max(0, Math.Min(1, elapsedMs / OutlineDurationMs));
            _background.BorderBrush = _outline;
            _background.BorderThickness = new Thickness(ScreenMetrics.Units(2));
        }

        /// <summary>Starts the green activation flash.</summary>
        public void Flash()
        {
            _flashEnd = Environment.TickCount + FlashDurationMs;
            _background.Background = FlashBackground;
        }

        /// <summary>True if the gaze point (screen pixels) is in the key's cell.</summary>
        public bool Contains(double x, double y)
        {
            return x >= _left && x < _right && y >= _top && y < _bottom;
        }

        /// <summary>
        /// Updates the key's look. Progress is ignored: no ring on the keyboard (user's
        /// choice). Nor is the hover label: it is already written on the key.
        /// </summary>
        public void Draw(double progress, bool hover, bool showLabel)
        {
            // "Environment.TickCount - _flashEnd < 0": still flashing. This way of comparing
            // stays correct even when the Windows counter wraps around zero.
            if (_flashEnd != 0 && Environment.TickCount - _flashEnd < 0)
            {
                _background.Background = FlashBackground;
                return;
            }
            _flashEnd = 0;
            bool highlight = hover && Active;
            _background.Background = highlight ? HoverBackground : _normalBackground;
            ApplyBorder(highlight);
        }

        /// <summary>
        /// The brush with its opacity multiplied by "opacity" (1 = the brush itself). The text
        /// is a child of the Border, not of its background: it keeps its full opacity.
        /// </summary>
        private static Brush Translucent(Brush brush, double opacity)
        {
            if (opacity >= 1)
            {
                return brush;
            }
            Brush copy = brush.Clone();
            copy.Opacity = brush.Opacity * Math.Max(0, opacity);
            copy.Freeze();
            return copy;
        }

        private static Brush Freeze(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
