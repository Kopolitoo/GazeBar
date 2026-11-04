using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// Part of GazeKeyboard: drawing the keyboard (background, input box, keys, Close button).
    /// </summary>
    internal sealed partial class GazeKeyboard
    {
        // ───────────────────────── Drawing ─────────────────────────

        /// <summary>
        /// (Re)draws the keyboard: background, keys of the shown page and Close button.
        /// All positions are in screen pixels, converted by the overlay.
        /// </summary>
        private void Build()
        {
            _targets.Clear();
            _keys.Clear();
            _close = null;
            if (!IsOpen || _layout == null || !_layer.Ready)
            {
                Hide();
                return;
            }
            KeyboardPage page = _layout.Page(_page) ?? _layout.Page(_layout.StartPage);

            AppSettings p = AppSettings.Current;
            double width = ScreenMetrics.WidthPixels;
            double height = ScreenMetrics.HeightPixels;

            // Size: at 75 % or 50 %, width AND height shrink together, so keys
            // keep their shape; the keyboard is centred horizontally.
            double sizePercent = p.KeyboardSize / 100.0;
            double keyboardWidth = width * sizePercent;
            double keysHeight = height * p.KeyboardHeight / 100.0 * sizePercent;

            // The key grid.
            double margin = ScreenMetrics.Px(Margin4K);
            double cellWidth = (keyboardWidth - 2 * margin) / _layout.Columns;
            double cellHeight = (keysHeight - 2 * margin) / _layout.Rows;
            double gap = Math.Min(ScreenMetrics.Px(KeyGap4K), Math.Min(cellWidth, cellHeight) * 0.15);

            // The input box adds a band above the keys. The keyboard grows by its
            // height: keys keep their size.
            double zoneHeight = p.InputBox ? Math.Round(cellHeight * InputBoxRatio) : 0;
            double keyboardHeight = keysHeight + zoneHeight;

            _left = (width - keyboardWidth) / 2;
            _right = _left + keyboardWidth;
            if (p.KeyboardAtTop)
            {
                // At the top of the screen ("Haut / bas" button of the keyboard bar).
                _top = 0;
                _bottom = keyboardHeight;
            }
            else
            {
                // At the bottom, above the keyboard menu bar if it is open.
                _bottom = Math.Max(keyboardHeight, height - _bottomReserve);
                _top = _bottom - keyboardHeight;
            }

            _layer.PrepareKeyboardSurface();
            Canvas surface = _layer.KeyboardSurface;

            // ── The background: an almost black rectangle, as on the user's screenshots.
            Border background = new Border();
            background.Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x12, 0x12, 0x12));
            background.Width = _layer.LengthToWpf(keyboardWidth);
            background.Height = _layer.LengthToWpf(keyboardHeight);
            Point corner = _layer.ToWpf(_left, _top);
            Canvas.SetLeft(background, corner.X);
            Canvas.SetTop(background, corner.Y);
            surface.Children.Add(background);

            // ── The input box: a dark band, the full width of the keys, with the
            //    text aligned left. It is not a gaze target: gaze on it does nothing (the
            //    mouse cursor stays still).
            _previewText = null;
            if (zoneHeight > 0)
            {
                Border zone = new Border();
                zone.Background = InputBoxBackground;
                zone.CornerRadius = new CornerRadius(ScreenMetrics.Units(4));
                double zoneWidth = keyboardWidth - 2 * margin - gap;
                zone.Width = _layer.LengthToWpf(zoneWidth);
                zone.Height = _layer.LengthToWpf(zoneHeight - gap);
                double indent = _layer.LengthToWpf(zoneHeight * 0.25);
                zone.Padding = new Thickness(indent, 0, indent, 0);

                _previewText = new TextBlock();
                _previewText.FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol");
                _previewText.FontSize = _layer.LengthToWpf(zoneHeight * 0.45);
                _previewText.TextWrapping = TextWrapping.NoWrap;
                _previewText.VerticalAlignment = VerticalAlignment.Center;
                _previewText.HorizontalAlignment = HorizontalAlignment.Left;
                zone.Child = _previewText;
                _previewWidthWpf = Math.Max(0, zone.Width - 2 * indent);

                Point zoneCorner = _layer.ToWpf(_left + margin + gap / 2, _top + margin + gap / 2);
                Canvas.SetLeft(zone, zoneCorner.X);
                Canvas.SetTop(zone, zoneCorner.Y);
                surface.Children.Add(zone);
            }

            // ── The keys, below the input box if it is shown.
            double keysTop = _top + zoneHeight;
            foreach (LayoutKey d in page.Keys)
            {
                KeyboardKey key = new KeyboardKey(d);
                key.Place(_layer, surface,
                              _left + margin + d.Col * cellWidth,
                              keysTop + margin + d.Row * cellHeight,
                              d.CellWidth * cellWidth,
                              cellHeight,
                              gap);
                _keys.Add(key);
                _targets.Add(key);
            }

            // ── The Close button: on the right, ABOVE the keyboard when it is at the bottom,
            //    BELOW when it is at the top (its label is then below the button). It stays
            //    on screen even if the keyboard is very tall. (E711 = "cancel" cross.)
            //    User request: it stays against the RIGHT EDGE OF THE SCREEN
            //    even when the keyboard is shrunk (75 %, 50 %, centred keyboard), and its size
            //    does not depend on the keyboard's: it is always [IconSize].
            double size = p.IconSizePx;
            double closeGap = ScreenMetrics.Px(CloseGap4K);
            double yClose = p.KeyboardAtTop
                ? _bottom + closeGap + size / 2
                : _top - closeGap - size / 2;
            yClose = Math.Max(size / 2 + margin, Math.Min(height - size / 2 - margin, yClose));
            _close = new GazeButton("", null, Texts.Get("keyboard.close"));
            _close.ZoneFactor = CloseZoneFactor;
            _close.Place(_layer, surface,
                           width - margin - size / 2,
                           yClose,
                           size, !p.KeyboardAtTop);
            _targets.Add(_close);

            // Keys changed: the one under the gaze is not activated until the gaze has left
            // it (for example after a page change).
            _aimer.ResetAndBlock();
            RefreshState();

            _layer.ShowKeyboardSurface();
            _visible = true;
        }

        /// <summary>
        /// Updates the labels (capitals), the green borders of the modifiers and, since step
        /// 13, the words of the suggestion boxes (recomputed from the current word: less than
        /// a millisecond).
        /// </summary>
        private void RefreshState()
        {
            string typed = _currentWord.ToString();
            _suggestions = _predictor.Suggest(typed, _previousWord, out _unknownWord);
            ShowPreview();

            bool uppercaseLetters = _shift ^ _capsLock;
            foreach (KeyboardKey key in _keys)
            {
                key.ShowUppercase(uppercaseLetters);
                string m = key.Description.Modifier;
                if (m != null)
                {
                    key.SetLit(IsLit(m));
                }

                int? num = key.Description.Prediction;
                if (num.HasValue)
                {
                    int idx = num.Value - 1;
                    if (_unknownWord && idx == 0)
                    {
                        key.ShowSuggestion("+ " + typed, true);
                    }
                    else if (!_unknownWord && idx >= 0 && idx < _suggestions.Count)
                    {
                        key.ShowSuggestion(ShownShape(_suggestions[idx], typed, _shift ^ _capsLock), false);
                    }
                    else
                    {
                        key.ShowSuggestion(null, false);
                    }
                }
            }
        }

        private bool IsLit(string modifier)
        {
            switch (modifier.Trim().ToLowerInvariant())
            {
                case "shift":
                    return _shift;
                case "capslock":
                    return _capsLock;
                case "ctrl":
                    return _ctrl;
                case "alt":
                    return _alt;
                case "win":
                    return _win;
                default:
                    return false;
            }
        }

        /// <summary>Draws every target without hover (for example when switching to fixed gaze).</summary>
        private void RedrawWithoutHover()
        {
            foreach (IGazeTarget target in _targets)
            {
                target.Draw(0, false, false);
            }
        }
    }
}
