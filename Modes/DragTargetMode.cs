using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// DRAG AND DROP MODE, part 1: choosing the drop point (step 6).
    ///
    /// We get here from the action choice, with one of the three "drag and drop" actions of
    /// the crown. The start point is the aimed point of the action choice (the cross).
    ///
    /// Display, on top of the real screen (the black screen of the action choice is gone):
    ///   - the [CircleRadius] circle follows the gaze, smoothed, as in movement mode;
    ///   - a dotted white line joins the start point to the circle: it is the future path of
    ///     the mouse;
    ///   - a small white dot marks the start point;
    ///   - a "back" button, on the right edge of the screen and vertically centered (user's
    ///     choice of 2026-10-25), cancels the drag and drop without pressing anything and
    ///     returns to movement mode.
    ///
    /// When the gaze fixates (same detection as in movement mode, [FixationDelay]), we move
    /// to part 2: the magnifier opens around the fixated point to refine the drop point
    /// (MagnifierMode, with this drag and drop in progress). The cursor has not moved: it is
    /// still on the start point.
    ///
    /// Two precautions, as in the magnifier:
    ///  - on entry, the gaze is still where the crown icon just activated was. A fixation is
    ///    only recognized once the gaze has left that place (radius [CircleRadius]);
    ///  - a fixation on the "back" button does not count as a drop point.
    ///
    /// The menu does not open during this mode (choice of 2026-10-25): the way out is the
    /// "back" button (or the caregiver's Escape key).
    /// </summary>
    internal sealed class DragTargetMode : Mode
    {
        /// <summary>Radius of the dot marking the start, in 4K screen pixels (converted with ScreenMetrics.Px, step 11).</summary>
        private const double StartDotRadius = 6;

        private readonly DragGesture _drag;

        private readonly FixationDetector _fixation = new FixationDetector();

        // The "back" button (the only button of this mode) and what decides whether it is activated.
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly ButtonAimer _aimer;

        // Position (decimal) of the smoothed circle, in screen pixels; _placed = false as long
        // as it has no position.
        private readonly Smoother _smoothed = new Smoother();
        private bool _placed;

        // Ignored zone: where the gaze was on entry (see the class comment).
        private bool _ignoredZone;
        private bool _ignoredZoneKnown;
        private double _zoneX;
        private double _zoneY;

        // The path line: a solid black line underneath and a dotted white line on top, so
        // that it can be seen on every background.
        private Line _rowBackground;
        private Line _row;

        public DragTargetMode(DragGesture drag)
        {
            _drag = drag;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Glisser-déposer (arrivée)"; }
        }

        public override void Enter(GazeState gaze)
        {
            _aimer.Reset();
            _fixation.Reset();

            OverlayWindow layer = Manager.Layer;
            layer.HideAll();
            if (!layer.Ready)
            {
                Manager.ChangeMode(new MovementMode());
                return;
            }

            // The ignored zone is centered on the current gaze (if it is on the screen),
            // otherwise on the first on-screen measure to come.
            _ignoredZone = true;
            _ignoredZoneKnown = gaze.OnScreen;
            _zoneX = gaze.X;
            _zoneY = gaze.Y;

            // The circle starts from the gaze (the action choice had hidden it: no visible
            // jump), brought back into the screen if it is outside.
            _placed = gaze.Available;
            _smoothed.Place(gaze.XScreen, gaze.YScreen);

            BuildSurface();
            App.Log("Drag: choosing the end point (" + _drag.Name + ", start at (" +
                    _drag.StartX.ToString("F0") + ", " + _drag.StartY.ToString("F0") +
                    "), modifiers: " + Modifiers.Description() + ")");
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // 1. Has the "back" button just been activated?
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                App.Log("Drag: cancelled (back)");
                Manager.ChangeMode(new MovementMode());
                return;
            }

            if (gaze.Available)
            {
                // 2. The circle moves smoothly towards the gaze (see Smoother.cs). Gaze off
                //    screen: it stays at the edge and slides along it (GazeState.XScreen).
                if (!_placed)
                {
                    _smoothed.Place(gaze.XScreen, gaze.YScreen);
                    _placed = true;
                }
                _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);
            }

            if (gaze.OnScreen)
            {
                // 3. The ignored zone: has the gaze left it? (Real on-screen gaze only.)
                if (!_ignoredZoneKnown)
                {
                    _zoneX = gaze.X;
                    _zoneY = gaze.Y;
                    _ignoredZoneKnown = true;
                }
                else if (_ignoredZone && Distance(gaze.X, gaze.Y, _zoneX, _zoneY) > p.CircleRadiusPx)
                {
                    _ignoredZone = false;
                }
            }

            // 4. The fixation, outside the ignored zone and outside the "back" button.
            //    No progress ring, as in movement mode.
            _fixation.Add(gaze);
            bool onBack = gaze.OnScreen && _buttons.Count > 0 && _buttons[0].Contains(gaze.X, gaze.Y);
            if (_ignoredZone || onBack)
            {
                _fixation.Reset();
            }

            if (_fixation.Reached)
            {
                App.Log("Drag: end point fixed at (" + _fixation.X.ToString("F0") + ", " +
                        _fixation.Y.ToString("F0") + "), opening the magnifier");
                Manager.ChangeMode(new MagnifierMode(_fixation.X, _fixation.Y, _drag));
                return;
            }

            // 5. Drawing: the circle and the path line.
            if (_placed)
            {
                Manager.Layer.ShowCircle(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, false);
                PlaceRow(_smoothed.X, _smoothed.Y);
            }
        }

        public override void Quit()
        {
            // The surface is hidden and cleared. The circle stays visible: if cancelled,
            // movement mode takes it over where it is (the magnifier hides everything).
            Manager.Layer.HideDragSurface();
            Manager.Layer.DragSurface.Children.Clear();
            _buttons.Clear();
            _row = null;
            _rowBackground = null;
        }

        // ───────────────────────── Drawing ─────────────────────────

        /// <summary>Fills the drag surface: path line, start point, "back" button.</summary>
        private void BuildSurface()
        {
            OverlayWindow layer = Manager.Layer;
            layer.PrepareDragSurface();
            Canvas surface = layer.DragSurface;
            double size = AppSettings.Current.IconSizePx;

            // The line: it will start from the start point; its other end follows the circle.
            _rowBackground = new Line();
            _rowBackground.Stroke = Brushes.Black;
            _rowBackground.StrokeThickness = ScreenMetrics.Units(5);
            _rowBackground.Opacity = 0.5;
            _rowBackground.Visibility = Visibility.Collapsed;
            _row = new Line();
            _row.Stroke = Brushes.White;
            _row.StrokeThickness = ScreenMetrics.Units(3);
            // Dots: 3 line thicknesses, then 2 thicknesses of gap, and so on.
            _row.StrokeDashArray = new DoubleCollection(new double[] { 3, 2 });
            _row.Visibility = Visibility.Collapsed;
            surface.Children.Add(_rowBackground);
            surface.Children.Add(_row);

            // The start point: a small white disc with a black border.
            Point startPoint = layer.ToWpf(_drag.StartX, _drag.StartY);
            double r = layer.LengthToWpf(ScreenMetrics.Px(StartDotRadius));
            Ellipse point = new Ellipse();
            point.Fill = Brushes.White;
            point.Stroke = Brushes.Black;
            point.StrokeThickness = ScreenMetrics.Units(2);
            point.Width = r * 2;
            point.Height = r * 2;
            Canvas.SetLeft(point, startPoint.X - r);
            Canvas.SetTop(point, startPoint.Y - r);
            surface.Children.Add(point);

            // The "back" button, on the right edge of the screen, vertically centered.
            // Symbol E72B = "back" arrow of the Windows font.
            double xBack = Manager.ScreenWidth - size / 2 - ScreenMetrics.Px(10);
            double yBack = Manager.ScreenHeight / 2;
            GazeButton back = new GazeButton("", null, "Annuler le glisser-déposer");
            back.Place(layer, surface, xBack, yBack, size);
            _buttons.Add(back);

            layer.ShowDragSurface();
        }

        /// <summary>Places the path line between the start point and (x, y), in screen pixels.</summary>
        private void PlaceRow(double x, double y)
        {
            if (_row == null)
            {
                return;
            }

            OverlayWindow layer = Manager.Layer;
            Point startPoint = layer.ToWpf(_drag.StartX, _drag.StartY);
            Point arrival = layer.ToWpf(x, y);

            foreach (Line stroke in new Line[] { _rowBackground, _row })
            {
                stroke.X1 = startPoint.X;
                stroke.Y1 = startPoint.Y;
                stroke.X2 = arrival.X;
                stroke.Y2 = arrival.Y;
                stroke.Visibility = Visibility.Visible;
            }
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
