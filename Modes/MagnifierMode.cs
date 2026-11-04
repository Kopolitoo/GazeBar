using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GazeBar
{
    /// <summary>
    /// MAGNIFIER MODE: refining the cursor position.
    ///
    /// Why? The gaze is often wrong by about thirty pixels. That is too much to aim at a small
    /// checkbox. In the magnifier, the zone around the aimed point is enlarged [MagnifierZoom]
    /// times (×4 by default): a 30 px error on the screen is then only worth 30 ÷ 4 ≈ 7 to 8
    /// real px.
    ///
    /// We get here in three ways:
    ///  - from action choice mode, with the "Magnifier" action of the crown (slot 4 by default);
    ///  - from fixed gaze mode, directly, if the [MagnifierBeforeAction] option is on
    ///    ("Magnifier, drag" tab of settings mode);
    ///  - during a drag and drop, to refine the drop point (part 2, see
    ///    DragGesture.cs). The fixation then starts the execution of the drag
    ///    (DragExecutionMode) instead of the action choice, and the "back" icon, placed on the
    ///    right edge of the screen, cancels the drag and drop (back to movement mode).
    ///
    /// Like the action choice, the mode runs in two phases ("steps"):
    ///
    ///  1. CAPTURE. Everything GazeBar draws is hidden, we wait [WaitMs] for Windows to erase
    ///     it, then we take a picture of the square enclosing the circle around the aimed
    ///     point P (side 2 × [CircleRadius], i.e. 200 × 200 px).
    ///
    ///  2. AIMING. A black screen covers the whole screen. In the center: the photo enlarged
    ///     ×[MagnifierZoom] (800 × 800 px). A small 50 px diameter circle follows the gaze. On
    ///     the right: the "back" icon.
    ///     When the gaze fixates on the photo (same detection as in movement mode), the
    ///     fixated point is converted to real coordinates: the corner of the enlarged photo is
    ///     subtracted, the result is divided by the zoom, and the corner of the photographed
    ///     zone is added. The cursor is placed there, then we switch to action choice mode on
    ///     this precise point.
    ///
    /// The "back" icon returns to the mode we came from, without doing anything (user request
    /// in the specification): the action choice (on the same point P) if we came from the
    /// crown, otherwise movement mode.
    ///
    /// Two precautions:
    ///  - when the magnifier opens, the gaze is still on the icon just activated, which may be
    ///    above the photo. So a fixation is only recognized once the gaze has left that place
    ///    (same principle as the "ignored zone" of movement mode after a click);
    ///    EXCEPTION: if, on opening, the gaze is already on the
    ///    place of the photo where P appears (the center of the screen, except near an edge),
    ///    there is no ignored zone. This is the case when the options icon of fixed gaze was
    ///    near the center of the screen: one is already looking at the right place, it would
    ///    be absurd to force a gaze move to validate the position;
    ///  - only a fixation ON the photo counts. The gaze resting on the "back" icon or on the
    ///    black background never places the cursor.
    /// </summary>
    internal sealed class MagnifierMode : Mode
    {
        /// <summary>The two phases of the mode (see the class comment).</summary>
        private enum Step
        {
            Capture,
            Aim
        }

        /// <summary>
        /// Wait before the photo, in milliseconds: the time for Windows to really erase what was
        /// just hidden (same value as in the action choice).
        /// </summary>
        private const double WaitMs = 60;

        /// <summary>
        /// Diameter of the small circle following the gaze, in 4K screen pixels (specification:
        /// 50 px), converted with ScreenMetrics.Px.
        /// </summary>
        private const double GazeCircleDiameter = 50;

        // The aimed point P (cursor position on entry), in screen pixels.
        private readonly double _px;
        private readonly double _py;

        // True if we came from the action choice: the "back" icon returns there.
        private readonly bool _backToActionChoice;

        // The drag and drop whose drop point is being refined, or null for an ordinary magnifier.
        private readonly DragGesture _drag;

        private Step _step;
        private double _stepStartMs;

        // The photographed zone (screen pixels): top-left corner and side.
        private int _ox;
        private int _oy;
        private int _side;

        // The enlarged photo on the black screen (screen pixels): top-left corner and zoom.
        private double _left;
        private double _top;
        private double _zoom;

        // The "back" button (the only button of this mode) and what decides whether it is activated.
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly ButtonAimer _aimer;

        // The fixation detection, the same as in movement mode.
        private readonly FixationDetector _fixation = new FixationDetector();

        // Ignored zone: where the gaze was when the magnifier opened (see the class comment).
        // _ignoredZoneKnown = false as long as there has been no on-screen measure.
        private bool _ignoredZone;
        private bool _ignoredZoneKnown;
        private double _zoneX;
        private double _zoneY;

        // Where point P appears on the enlarged photo (screen pixels), computed with the photo.
        // _knownTarget = false as long as the photo is not built.
        private bool _knownTarget;
        private double _targetX;
        private double _targetY;

        /// <summary>
        /// Activation zone of the "back" button, relative to the drawn button: 30 % larger.
        /// </summary>
        private const double BackZoneFactor = 1.3;

        // The small circle following the gaze: two ellipses (black line underneath, green on top).
        private Ellipse _gazeCircleBackground;
        private Ellipse _gazeCircle;
        private readonly Smoother _smoothed = new Smoother();
        private bool _gazeCirclePlaced;

        /// <summary>
        /// Opens the magnifier around the point (x, y), in screen pixels.
        /// backToActionChoice: true if we came from the action choice (the "back" icon returns
        /// there), false if we came from fixed gaze mode (back to movement mode).
        /// </summary>
        public MagnifierMode(double x, double y, bool backToActionChoice)
        {
            _px = x;
            _py = y;
            _backToActionChoice = backToActionChoice;
            _aimer = new ButtonAimer(_buttons);
        }

        /// <summary>
        /// Opens the magnifier around the drop point (x, y) of a drag and drop.
        /// The fixation starts the execution of the drag; the "back" icon cancels it.
        /// </summary>
        public MagnifierMode(double x, double y, DragGesture drag)
        {
            _px = x;
            _py = y;
            _drag = drag;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Magnifier"; }
        }

        public override void Enter(GazeState gaze)
        {
            // Start from zero: new photo, no fixation in progress.
            _aimer.Reset();
            _fixation.Reset();

            // The ignored zone is centered on the current gaze (if it is on the screen),
            // otherwise on the first on-screen measure to come.
            _ignoredZone = true;
            _ignoredZoneKnown = gaze.OnScreen;
            _zoneX = gaze.X;
            _zoneY = gaze.Y;
            _knownTarget = false;

            // 1. CAPTURE: hide everything GazeBar draws, and wait a bit before the photo.
            Manager.Layer.HideAll();
            _step = Step.Capture;
            _stepStartMs = gaze.NowMs;
        }

        public override void Update(GazeState gaze)
        {
            if (_step == Step.Capture)
            {
                if (gaze.NowMs - _stepStartMs >= WaitMs)
                {
                    bool succeeded;
                    try
                    {
                        succeeded = BuildScreen();
                    }
                    catch (Exception ex)
                    {
                        App.Log("Magnifier: error while building the screen: " + ex);
                        succeeded = false;
                    }

                    if (!succeeded)
                    {
                        // Photo impossible: do not stay stuck in front of a black screen.
                        Manager.ChangeMode(new MovementMode());
                    }
                }
                return;
            }

            UpdateAim(gaze);
        }

        public override void Quit()
        {
            // The black screen is hidden and cleared (the photo is no longer needed).
            Manager.Layer.HideChoiceScreen();
            Manager.Layer.ChoiceScreen.Children.Clear();
            _buttons.Clear();
            _gazeCircle = null;
            _gazeCircleBackground = null;
        }

        // ───────────────────────── 2. Aiming ─────────────────────────

        private void UpdateAim(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // a. Has the "back" icon just been activated?
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                Back();
                return;
            }

            // b. The small gaze circle.
            MoveGazeCircle(gaze, p);

            // c. The ignored zone: has the gaze left it?
            if (gaze.OnScreen)
            {
                if (!_ignoredZoneKnown)
                {
                    _zoneX = gaze.X;
                    _zoneY = gaze.Y;
                    _ignoredZoneKnown = true;
                    LiftZoneIfAlreadyOnTarget(p);
                }
                else if (_ignoredZone && Distance(gaze.X, gaze.Y, _zoneX, _zoneY) > p.CircleRadiusPx)
                {
                    _ignoredZone = false;
                }
            }

            // d. The fixation. It only counts on the photo, and outside the ignored zone.
            //    No progress ring (as in movement mode): the small circle is enough.
            //
            //    Note on the radius: the specification speaks of a "radius adapted to the
            //    enlargement". Here [FixationRadius] is kept as is, in screen pixels, because it
            //    measures the gaze jitter, and the jitter does not change with the zoom: the eye
            //    trembles the same way in front of an enlarged photo. It is the final conversion
            //    (division by the zoom) that brings the precision.
            _fixation.Add(gaze);
            if (_ignoredZone || (gaze.OnScreen && !OnPhoto(gaze.X, gaze.Y)))
            {
                _fixation.Reset();
            }

            if (_fixation.Reached && OnPhoto(_fixation.X, _fixation.Y))
            {
                PlaceAndContinue(_fixation.X, _fixation.Y);
            }
        }

        /// <summary>
        /// The gaze fixated at (fx, fy) on the enlarged photo: compute the matching real point,
        /// place the cursor there, and switch to the action choice.
        /// </summary>
        private void PlaceAndContinue(double fx, double fy)
        {
            // Example with a zoom of 4: a point 100 px to the right of the left edge of the
            // enlarged photo is 100 ÷ 4 = 25 px to the right of the left edge of the photographed zone.
            double x = _ox + (fx - _left) / _zoom;
            double y = _oy + (fy - _top) / _zoom;

            // For safety, stay within the photographed zone (last pixel included).
            x = Clamp(x, _ox, _ox + _side - 1);
            y = Clamp(y, _oy, _oy + _side - 1);

            if (_drag != null)
            {
                // Drag and drop: (x, y) is the drop point. The cursor stays on the start point;
                // the execution will take it there, button pressed.
                App.LogDetail("Magnifier: drag end point refined");
                Manager.ChangeMode(new DragExecutionMode(_drag, x, y));
                return;
            }

            NativeMethods.SetCursorPos((int)Math.Round(x), (int)Math.Round(y));
            App.LogDetail("Magnifier: fixation on the photo, cursor placed");

            Manager.ChangeMode(new ActionChoiceMode(x, y));
        }

        /// <summary>
        /// "Back" icon: go back to where we came from, without doing anything. The cursor has not
        /// moved (it is still on P). During a drag and drop, "back" cancels it.
        /// </summary>
        private void Back()
        {
            App.LogDetail(_drag != null ? "Magnifier: drag and drop cancelled (back)" : "Magnifier: back");
            if (_drag != null)
            {
                Manager.ChangeMode(new MovementMode());
            }
            else if (_backToActionChoice)
            {
                Manager.ChangeMode(new ActionChoiceMode(_px, _py));
            }
            else
            {
                Manager.ChangeMode(new MovementMode());
            }
        }

        /// <summary>
        /// No ignored zone if, on opening, the gaze was already on the place of the photo where P
        /// appears (within [CircleRadius]): see the exception in the class comment. Does nothing
        /// as long as the photo or the gaze position are not known.
        /// </summary>
        private void LiftZoneIfAlreadyOnTarget(AppSettings p)
        {
            if (!_ignoredZone || !_ignoredZoneKnown || !_knownTarget)
            {
                return;
            }
            if (Distance(_zoneX, _zoneY, _targetX, _targetY) <= p.CircleRadiusPx)
            {
                _ignoredZone = false;
                App.LogDetail("Magnifier: gaze already on the target point, no need to move the gaze");
            }
        }

        /// <summary>True if this point (screen pixels) is on the enlarged photo.</summary>
        private bool OnPhoto(double x, double y)
        {
            double enlargedSide = _side * _zoom;
            return x >= _left && x <= _left + enlargedSide &&
                   y >= _top && y <= _top + enlargedSide;
        }

        // ───────────────────────── 1. The photo and the black screen ─────────────────────────

        /// <summary>
        /// Takes the photo and fills the black screen. Returns false if it is impossible.
        /// All positions are computed in screen pixels, then converted by the overlay.
        /// </summary>
        private bool BuildScreen()
        {
            OverlayWindow layer = Manager.Layer;
            if (!layer.Ready)
            {
                return false;
            }

            AppSettings p = AppSettings.Current;
            double width = Manager.ScreenWidth;
            double height = Manager.ScreenHeight;
            double size = p.IconSizePx;
            _zoom = p.MagnifierZoom;

            // ── The photographed zone: the square enclosing the circle, centered on P.
            //    The enlarged photo must fit on the screen (with a margin of 40 px of the 4K
            //    screen, converted with ScreenMetrics.Px): on a small screen, or with
            //    a large zoom, a smaller square is therefore photographed.
            //    Example: 1080 px high screen, zoom 4 → at most (1080 − 40) ÷ 4 = 260 px.
            int side = (int)Math.Round(2 * p.CircleRadiusPx);
            int maxSide = (int)Math.Floor((Math.Min(width, height) - ScreenMetrics.Px(40)) / _zoom);
            side = Math.Max(10, Math.Min(side, maxSide));
            //    Near an edge of the screen, the square is shifted to stay fully on the screen
            //    (P is then no longer in the center of the photo).
            _ox = (int)Clamp(Math.Round(_px - side / 2.0), 0, width - side);
            _oy = (int)Clamp(Math.Round(_py - side / 2.0), 0, height - side);
            _side = side;

            BitmapSource photo = ScreenCapture.Capture(_ox, _oy, side, side);
            if (photo == null)
            {
                return false;
            }

            layer.PrepareChoiceScreen();
            Canvas screen = layer.ChoiceScreen;

            // ── The enlarged photo, in the center of the screen.
            double cxScreen = width / 2;
            double cyScreen = height / 2;
            double enlargedSide = side * _zoom;
            _left = cxScreen - enlargedSide / 2;
            _top = cyScreen - enlargedSide / 2;

            // Where P appears on the enlarged photo (the reverse of the PlaceAndContinue computation).
            _targetX = _left + (_px - _ox) * _zoom;
            _targetY = _top + (_py - _oy) * _zoom;
            _knownTarget = true;
            LiftZoneIfAlreadyOnTarget(p);

            Image image = new Image();
            image.Source = photo;
            image.Stretch = Stretch.Fill;
            // "Nearest neighbour": each pixel becomes a sharp 4 × 4 square, instead of being
            // softened (blurred). This way one sees exactly where each real pixel falls.
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Point corner = layer.ToWpf(_left, _top);
            image.Width = layer.LengthToWpf(enlargedSide);
            image.Height = image.Width;
            Canvas.SetLeft(image, corner.X);
            Canvas.SetTop(image, corner.Y);
            screen.Children.Add(image);

            // A thin gray frame around the photo.
            Rectangle frame = new Rectangle();
            frame.Stroke = Brushes.Gray;
            // Thickness in units of the reference screen, converted (see ScreenMetrics.cs).
            double stroke = ScreenMetrics.Units(2);
            frame.StrokeThickness = stroke;
            frame.Width = image.Width + 2 * stroke;
            frame.Height = image.Height + 2 * stroke;
            Canvas.SetLeft(frame, corner.X - stroke);
            Canvas.SetTop(frame, corner.Y - stroke);
            screen.Children.Add(frame);

            // ── The "back" icon, on the right,
            //    at the same height as in the action choice (middle of the screen).
            //    During a drag and drop, it is stuck to the right edge of the screen, at the same
            //    place as while choosing the drop point.
            //    Symbol E72B = "back" arrow of the Windows font.
            double xBack = width - size;
            string backLabel = _drag != null ? Texts.Get("drag.cancel") : Texts.Get("common.back");
            GazeButton back = new GazeButton("", null, backLabel);
            back.ZoneFactor = BackZoneFactor;
            back.Place(layer, screen, xBack, cyScreen, size);
            _buttons.Add(back);

            // ── The small gaze circle, added LAST to be drawn on top.
            //    It will appear at the first on-screen measure.
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
            _gazeCirclePlaced = false;

            layer.ShowChoiceScreen();
            _step = Step.Aim;
            App.LogDetail("Magnifier: photo of " + side + " × " + side + " px, magnified ×" + _zoom.ToString("0.##"));
            return true;
        }

        /// <summary>
        /// Moves the small gaze circle: it follows the gaze smoothly ([Smoothing], see
        /// Smoother.cs). Gaze off screen: it slides along the edge. Gaze lost: it stays where it is.
        /// </summary>
        private void MoveGazeCircle(GazeState gaze, AppSettings p)
        {
            if (_gazeCircle == null || !gaze.Available)
            {
                return;
            }

            // Position brought back into the screen: gaze off screen → the circle slides along
            // the edge (see GazeState.XScreen).
            if (!_gazeCirclePlaced)
            {
                _smoothed.Place(gaze.XScreen, gaze.YScreen);
                _gazeCirclePlaced = true;
            }
            _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);

            OverlayWindow layer = Manager.Layer;
            Point center = layer.ToWpf(_smoothed.X, _smoothed.Y);
            double radius = layer.LengthToWpf(ScreenMetrics.Px(GazeCircleDiameter) / 2);
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

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double Clamp(double val, double min, double max)
        {
            return Math.Max(min, Math.Min(max, val));
        }
    }
}
