using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Prunel
{
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
