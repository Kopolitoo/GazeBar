using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Prunel
{
    /// <summary>
    /// SCROLL MODE (specification, user's description).
    ///
    /// We get here from the action choice, with the "Scroll" action of the crown (slot 0).
    /// The cursor is on the aimed point P; it does not move.
    ///
    /// Display, on top of the real screen:
    ///   - the dead zone circle, FIXED on P (the cursor). Its radius is half of [CircleRadius]
    ///     (see DeadZoneFactor);
    ///   - a small colored arc, laid on the circle, on the side where the gaze is: it shows the
    ///     scroll direction. It is shown ALL THE TIME, even
    ///     when the gaze is in the dead zone (nothing scrolls then): it always shows where
    ///     Prunel sees the gaze. Gaze lost or on the back button: it stays at its last place;
    ///   - circle and arc are more transparent than in the other modes,
    ///     to see better what scrolls underneath;
    ///   - a "back" button on the right edge of the screen, vertically centered: it returns to
    ///     movement mode.
    ///
    /// Scrolling:
    ///   - DEAD ZONE = THE DRAWN CIRCLE, of radius [CircleRadius] × [DeadZoneFactor] (half):
    ///     gaze inside → nothing scrolls;
    ///   - DIRECTION: gaze above the circle → up, below → down, on the right → right, on the
    ///     left → left. Only one direction at a time: the one with the largest gap from the
    ///     center (no diagonal);
    ///   - SPEED: 0 at the edge of the dead zone, then it increases with the distance, up to
    ///     [MaxScrollSpeed] notches per second at [MaxSpeedRadii] × [CircleRadius] beyond the
    ///     edge (three radii). This speed-up distance is always
    ///     counted in WHOLE radii of [CircleRadius]: shrinking the dead zone only brought the
    ///     start of scrolling closer, without making the speed-up more abrupt.
    ///     Further away, it no longer increases;
    ///   - GENTLE START (issue #52: "scrolling starts a bit too fast"), two curves together:
    ///       · distance: the speed follows a curve, not a straight line ([SpeedCurvePower]):
    ///         very slow near the circle, faster further away. Halfway: a quarter of the
    ///         maximum speed (instead of half);
    ///       · time: each start (leaving the dead zone, or a new direction) begins at
    ///         [RampStartFraction] of this speed, which then reaches 100 % in [RampMs];
    ///   - Windows "wheel events" are sent: the window under the cursor scrolls (Windows setting
    ///     "Scroll inactive windows when I hover over them", on by default).
    ///
    /// FINE SCROLLING (user request: "scrolling is very jerky"):
    ///   - quarter notches (30 units) would make 3 or 4 small jumps per second at low speed.
    ///     So the wheel goes in very small amounts, up to once per frame (about 60 times
    ///     per second), as soon as there are [MinStep] units. Recent software (browsers, Word,
    ///     Explorer) adds up these small amounts and scrolls almost continuously. Very old
    ///     software, which waits for a whole notch (120), stays notch by notch: nothing can be
    ///     done about that on our side;
    ///   - the SPEED itself is smoothed ([SpeedTimeConstantMs]): it rises and falls smoothly
    ///     instead of changing at once, when entering and leaving the dead zone as on every
    ///     small gaze jitter.
    ///
    /// ACTIVE WINDOW (same request: "I often have to click before I can scroll"): when entering
    /// this mode, Prunel makes the window under P active, without clicking (so without the risk
    /// of opening a link). Some software ignores the wheel as long as its window is not active,
    /// and if the Windows setting above is off, the wheel always goes to the active window. The
    /// log notes the state of this setting, which can be changed in the "Scroll" tab of the
    /// settings (see WheelScroller).
    ///
    /// The gaze used is smoothed ([Smoothing]), like the circle of movement mode: otherwise, the
    /// small natural jitter of the eye would make the speed and the direction hesitate.
    ///
    /// Precautions:
    ///   - on entry, the gaze is still on the crown icon, often outside the circle: nothing
    ///     scrolls until the gaze has been inside the circle once, or for at most
    ///     [ArmingDelayMs] (the time for the eye to leave the icon). After that, scrolling
    ///     follows the gaze even if it never entered the circle (issue #50: looking straight
    ///     above or below the circle did nothing until the gaze moved);
    ///   - nothing scrolls when the gaze is lost, too far off screen or on the back button
    ///     (otherwise, while going to look at the button, it would scroll to the right).
    ///
    /// Gaze slightly off screen: it still counts, up to [OffScreenMargin4K] pixels beyond the
    /// edges. This enlarges the area where the speed is set:
    /// upwards and downwards, the screen alone is too short to reach the maximum speed. The
    /// PCEye 5 measures the gaze off screen, but less precisely; beyond the margin, the
    /// measure is considered unusable.
    ///
    /// The Ctrl, Shift, Alt modifiers chosen in the action choice are pressed during each wheel
    /// movement, then released: Ctrl + wheel zooms in most software, Shift + wheel often
    /// scrolls sideways.
    ///
    /// The menu does not open during this mode. Ways out: the "back" button, looking off the
    /// screen ON THE RIGHT (the "back" side) for [RightExitMs] (issue #51: the button is
    /// sometimes hard to aim at), or the caregiver's Escape key. Off the screen on the right,
    /// nothing scrolls; above, below and on the left, a gaze slightly off screen still scrolls
    /// (see above).
    /// </summary>
    internal sealed class ScrollMode : Mode
    {
        /// <summary>
        /// Smallest wheel send, in Windows units (120 = one whole notch): 8 units = 1/15 of a
        /// notch. As soon as the reserve reaches this amount, EVERYTHING it contains is sent (in
        /// whole units), on every frame if needed. (Quarter notches, 30, make visible jumps at
        /// low speed.)
        /// If a program scrolls much too fast (some old programs count each send as a whole
        /// notch), go back to 120 here.
        /// </summary>
        private const int MinStep = 8;

        /// <summary>
        /// Time constant of the speed smoothing, in milliseconds: in 150 ms, the speed covers
        /// about two thirds of the way to the wanted speed (same principle as Smoother.cs, single
        /// stage). Larger = softer but slower starts and stops; smaller = livelier.
        /// </summary>
        private const double SpeedTimeConstantMs = 150;

        /// <summary>
        /// Longest wait, in milliseconds after entering the mode, before scrolling may start
        /// when the gaze has not been inside the circle (see the precautions).
        /// </summary>
        private const double ArmingDelayMs = 400;

        /// <summary>Below this speed (notches per second), it is considered zero.</summary>
        private const double NegligibleSpeed = 0.05;

        /// <summary>
        /// Shape of the speed curve with the distance (see the class comment): 1 = straight line,
        /// 2 = slow near the circle then faster. Larger = even slower near the circle.
        /// </summary>
        private const double SpeedCurvePower = 2;

        /// <summary>
        /// Gentle start in time (see the class comment): each start begins at this fraction of
        /// the speed (0.2 = 20 %)...
        /// </summary>
        private const double RampStartFraction = 0.2;

        /// <summary>...and reaches 100 % after this time, in milliseconds.</summary>
        private const double RampMs = 1500;

        /// <summary>
        /// How long, in milliseconds, the gaze must stay off the screen on the right to leave
        /// this mode (see the class comment).
        /// </summary>
        private const double RightExitMs = 1000;

        /// <summary>One wheel notch, in Windows units (WHEEL_DELTA).</summary>
        private const int Notch = 120;

        /// <summary>Half opening of the arc showing the direction, in degrees (50° arc).</summary>
        private const double ArcHalfAngle = 25;

        /// <summary>
        /// Distance, counted in circle radii from the edge, where the speed reaches
        /// [MaxScrollSpeed]. First 1, tripled at the user's request for a more
        /// gradual speed-up.
        /// </summary>
        private const double MaxSpeedRadii = 3;

        /// <summary>
        /// How far, in 4K screen pixels beyond the screen edges, the gaze still counts for
        /// scrolling. Further away, nothing scrolls. Converted for the current screen
        /// with ScreenMetrics.GazePx (200 px in 4K → 100 px in 1920 × 1080).
        /// </summary>
        private const double OffScreenMargin4K = 200;

        /// <summary>
        /// Radius of the dead zone (and of the drawn circle), relative to [CircleRadius]: half.
        /// Only changes this mode.
        /// </summary>
        private const double DeadZoneFactor = 0.5;

        /// <summary>
        /// Opacity of the circle and the arc, relative to their usual opacity: 0.5 = twice as
        /// transparent (request: "increase the transparency").
        /// </summary>
        private const double Opacity = 0.5;

        // The aimed point P (the cursor and the center of the circle), in screen pixels.
        private readonly double _px;
        private readonly double _py;

        // The "back" button (the only button of this mode) and what decides whether it is activated.
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly ButtonAimer _aimer;

        // The smoothed gaze, in screen pixels; _placed = false as long as it has no position.
        private readonly Smoother _smoothed = new Smoother();
        private bool _placed;

        // True once the gaze has been inside the circle or [ArmingDelayMs] has passed (see the precautions).
        private bool _armed;
        private double _enteredMs;

        // Amount of wheel not sent yet (in Windows units). The speed gives, on each tick, a small
        // amount (for example 10 notches/s × 120 × 0.016 s = 19 units); it is added here, and
        // sent as soon as there are at least [MinStep] units.
        private double _reserve;
        private bool _horizontalReserve;
        private int _reserveDirection;

        // The smoothed speed, in notches per second (always positive; the direction is _reserveDirection).
        private double _speed;

        // Time when the gaze went off the screen on the right; -1 = it is not there.
        private double _rightSinceMs = -1;

        // Time when the current start began (gentle start in time); -1 = not scrolling.
        private double _rampStartMs = -1;

        private double _lastTickMs = -1;

        // The direction arc: a black line underneath and a green line on top (readable on every background).
        private Path _arcBackground;
        private Path _arc;

        public ScrollMode(double x, double y)
        {
            _px = x;
            _py = y;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Scroll"; }
        }

        public override void Enter(GazeState gaze)
        {
            _aimer.Reset();

            OverlayWindow layer = Manager.Layer;
            layer.HideAll();
            if (!layer.Ready)
            {
                Manager.ChangeMode(new MovementMode());
                return;
            }

            // The cursor is put back on P for safety: the window under it is the one that will scroll.
            NativeMethods.SetCursorPos((int)Math.Round(_px), (int)Math.Round(_py));

            // This window becomes the active window (see the class comment).
            ActivateWindowUnderP();

            _placed = Usable(gaze);
            _smoothed.Place(gaze.X, gaze.Y);
            _armed = false;
            _enteredMs = gaze.NowMs;
            _reserve = 0;
            _speed = 0;
            _rampStartMs = -1;
            _rightSinceMs = -1;
            _lastTickMs = gaze.NowMs;

            BuildSurface();
            layer.ShowCircle(_px, _py, AppSettings.Current.CircleRadiusPx * DeadZoneFactor, true, Opacity);

            App.LogDetail("Scroll: start, modifiers: " + Modifiers.Description());
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // Time elapsed since the previous tick, in seconds (bounded: after Windows freezes,
            // we do not want to send a whole second of scrolling at once).
            double now = gaze.NowMs;
            double dt = _lastTickMs < 0 ? 0 : (now - _lastTickMs) / 1000.0;
            dt = Math.Max(0, Math.Min(dt, 0.1));
            _lastTickMs = now;

            // 1. Has the "back" button just been activated?
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                App.LogDetail("Scroll: end (back)");
                Manager.ChangeMode(new MovementMode());
                return;
            }

            // 2. Gaze off the screen on the right (any distance, any height): after [RightExitMs],
            //    back to movement mode, like the "back" button. Meanwhile, nothing scrolls.
            //    A lost gaze, or a gaze back on the screen, starts the count again.
            bool offRight = gaze.Available && gaze.X >= Manager.ScreenWidth;
            if (offRight)
            {
                if (_rightSinceMs < 0)
                {
                    _rightSinceMs = now;
                }
                if (now - _rightSinceMs >= RightExitMs)
                {
                    App.LogDetail("Scroll: end (gaze off the screen on the right)");
                    Manager.ChangeMode(new MovementMode());
                    return;
                }
            }
            else
            {
                _rightSinceMs = -1;
            }

            // 3. No usable gaze (lost, or too far off screen), gaze off the screen on the right,
            //    or gaze on the button: nothing scrolls. A gaze slightly off screen above, below
            //    or on the left is usable.
            bool onBack = gaze.OnScreen && _buttons.Count > 0 && _buttons[0].Contains(gaze.X, gaze.Y);
            //    The arc stays shown at its last place.
            //    Immediate stop (no smooth slowdown): it is a safety measure.
            if (!Usable(gaze) || offRight || onBack)
            {
                _reserve = 0;
                _speed = 0;
                _rampStartMs = -1;
                return;
            }

            // 4. The smoothed gaze moves smoothly towards the gaze (see Smoother.cs).
            if (!_placed)
            {
                _smoothed.Place(gaze.X, gaze.Y);
                _placed = true;
            }
            _smoothed.Follow(gaze.X, gaze.Y, gaze.NowMs);

            // 5. Where is the gaze relative to the circle?
            double dx = _smoothed.X - _px;
            double dy = _smoothed.Y - _py;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double radius = p.CircleRadiusPx * DeadZoneFactor;

            // The arc is drawn in all cases, even in the dead zone. Gaze exactly in the center:
            // no direction, the arc stays where it was.
            if (distance >= 1)
            {
                DrawArc(dx, dy, radius);
            }

            if (distance <= radius || now - _enteredMs >= ArmingDelayMs)
            {
                // In the circle (the dead zone, where the wanted speed is zero), or the eye has
                // had time to leave the crown icon: scrolling is now allowed (see the precautions).
                _armed = true;
            }

            if (!_armed)
            {
                return;
            }

            // 6. The wanted speed and, outside the dead zone, the direction (one axis only: the
            //    one with the largest gap).
            //    Speed: from 0 at the edge of the dead zone to [MaxScrollSpeed] three whole
            //    [CircleRadius] further, along a curve, and lowered during the first moments of
            //    each start (see the class comment).
            //    In the dead zone, the previous direction is kept: the speed goes smoothly back
            //    down to 0 there, in the same direction.
            double wantedSpeed = 0;
            if (distance > radius)
            {
                bool horizontal = Math.Abs(dx) > Math.Abs(dy);
                // Wheel: up and right = positive (see MouseInjector.Wheel).
                int direction = horizontal ? (dx > 0 ? 1 : -1) : (dy < 0 ? 1 : -1);
                double fraction = Math.Min(1.0, (distance - radius) / (MaxSpeedRadii * p.CircleRadiusPx));
                fraction = Math.Pow(fraction, SpeedCurvePower);

                // Change of direction: start from zero (speed and reserve), so as not to carry the
                // momentum of the old direction into the new one. It is a new start.
                if (horizontal != _horizontalReserve || direction != _reserveDirection)
                {
                    _reserve = 0;
                    _speed = 0;
                    _horizontalReserve = horizontal;
                    _reserveDirection = direction;
                    _rampStartMs = now;
                }
                // Leaving the dead zone: a new start too.
                if (_rampStartMs < 0)
                {
                    _rampStartMs = now;
                }
                double ramp = Math.Min(1.0, (now - _rampStartMs) / RampMs);
                double timeFactor = RampStartFraction + (1 - RampStartFraction) * ramp;

                wantedSpeed = fraction * timeFactor * p.MaxScrollSpeed;
            }
            else
            {
                // In the dead zone: the next exit will be a new start.
                _rampStartMs = -1;
            }

            // 7. The smoothed speed moves towards the wanted speed, according to the elapsed time
            //    (same computation as Smoother.cs: the fraction covered is 1 − e^(−dt / τ)).
            _speed += (wantedSpeed - _speed) * (1 - Math.Exp(-dt * 1000 / SpeedTimeConstantMs));
            if (wantedSpeed == 0 && _speed < NegligibleSpeed)
            {
                _speed = 0;
                _reserve = 0;
                return;
            }

            // 8. The wheel reserve: add this tick's amount, and send everything it contains (in
            //    whole units) as soon as it reaches [MinStep].
            _reserve += _speed * Notch * dt;
            if (_reserve >= MinStep)
            {
                int amount = (int)_reserve;
                _reserve -= amount;
                Send(_reserveDirection * amount, _horizontalReserve);
            }
        }

        public override void Quit()
        {
            // The surface is hidden and cleared. The circle stays shown: movement mode takes it
            // over where it is (on P) and slides it towards the gaze, without a jump.
            Manager.Layer.HideDragSurface();
            Manager.Layer.DragSurface.Children.Clear();
            _buttons.Clear();
            _arc = null;
            _arcBackground = null;
        }

        /// <summary>
        /// True if the gaze measure can be used for scrolling: available, and on the screen or
        /// less than [OffScreenMargin4K] pixels from its edges.
        /// </summary>
        private bool Usable(GazeState gaze)
        {
            if (!gaze.Available)
            {
                return false;
            }
            double margin = ScreenMetrics.GazePx(OffScreenMargin4K);
            return gaze.X >= -margin && gaze.X < Manager.ScreenWidth + margin &&
                   gaze.Y >= -margin && gaze.Y < Manager.ScreenHeight + margin;
        }

        /// <summary>
        /// Makes the window under P active (without clicking). If it already is, or if it is a
        /// Prunel window, nothing is done. The result is written to the log.
        ///
        /// Windows does not allow any program to change the active window (to prevent a window
        /// from "stealing" the keyboard while one is typing). Prunel is not in the foreground
        /// (its overlay never takes the focus): so Prunel is "attached" for a moment to the
        /// current active window (AttachThreadInput), which gives it this right.
        /// </summary>
        private void ActivateWindowUnderP()
        {
            try
            {
                // Which window does the wheel go to? (Windows setting, see WheelScroller)
                App.LogDetail("Scroll: Windows setting \"scroll inactive windows\" = " + WheelScroller.Description());

                NativeMethods.POINT point;
                point.X = (int)Math.Round(_px);
                point.Y = (int)Math.Round(_py);
                IntPtr underP = NativeMethods.WindowFromPoint(point);
                IntPtr window = underP == IntPtr.Zero ? IntPtr.Zero : NativeMethods.GetAncestor(underP, NativeMethods.GA_ROOT);
                if (window == IntPtr.Zero)
                {
                    App.LogDetail("Scroll: no window under the target point");
                    return;
                }

                // A window of Prunel itself (for example the keyboard): do not touch it.
                uint process;
                NativeMethods.GetWindowThreadProcessId(window, out process);
                if (process == (uint)Process.GetCurrentProcess().Id)
                {
                    return;
                }

                IntPtr active = NativeMethods.GetForegroundWindow();
                if (active == window)
                {
                    return;   // already active: nothing to do
                }

                uint activeThread = active == IntPtr.Zero ? 0 : NativeMethods.GetWindowThreadProcessId(active, out process);
                uint prunelThread = NativeMethods.GetCurrentThreadId();
                bool attached = activeThread != 0 && activeThread != prunelThread &&
                               NativeMethods.AttachThreadInput(prunelThread, activeThread, true);
                bool succeeded = NativeMethods.SetForegroundWindow(window);
                if (attached)
                {
                    NativeMethods.AttachThreadInput(prunelThread, activeThread, false);
                }

                System.Text.StringBuilder title = new System.Text.StringBuilder(128);
                NativeMethods.GetWindowText(window, title, title.Capacity);
                App.LogDetail("Scroll: window \"" + title + "\" activated: " + (succeeded ? "yes" : "no (refused by Windows)"));
            }
            catch (Exception ex)
            {
                // A failure here does not prevent scrolling: it is only logged.
                App.Log("Scroll: cannot activate the window under the target point: " + ex.Message);
            }
        }

        /// <summary>Sends the wheel, with the active modifiers pressed around it.</summary>
        private static void Send(int amount, bool horizontal)
        {
            bool modifiers = Modifiers.AnyActive;
            if (modifiers)
            {
                Modifiers.PressActive();
            }

            MouseInjector.Wheel(amount, horizontal);

            if (modifiers)
            {
                Modifiers.ReleaseAll();
            }
        }

        // ───────────────────────── Drawing ─────────────────────────

        /// <summary>
        /// Fills the surface: the direction arc (hidden for now) and the "back" button.
        /// The drag and drop surface (DragSurface) is reused: it is a transparent surface on top
        /// of the real screen, exactly what is needed here too.
        /// </summary>
        private void BuildSurface()
        {
            OverlayWindow layer = Manager.Layer;
            layer.PrepareDragSurface();
            Canvas surface = layer.DragSurface;
            double size = AppSettings.Current.IconSizePx;

            _arcBackground = new Path();
            _arcBackground.Stroke = Brushes.Black;
            _arcBackground.StrokeThickness = ScreenMetrics.Units(10);
            _arcBackground.Opacity = 0.5 * Opacity;
            _arcBackground.StrokeStartLineCap = PenLineCap.Round;
            _arcBackground.StrokeEndLineCap = PenLineCap.Round;
            _arcBackground.Visibility = Visibility.Collapsed;
            _arc = new Path();
            _arc.Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53));   // the green of the rings
            _arc.StrokeThickness = ScreenMetrics.Units(6);
            _arc.Opacity = Opacity;
            _arc.StrokeStartLineCap = PenLineCap.Round;
            _arc.StrokeEndLineCap = PenLineCap.Round;
            _arc.Visibility = Visibility.Collapsed;
            surface.Children.Add(_arcBackground);
            surface.Children.Add(_arc);

            // The "back" button, on the right edge of the screen, vertically centered.
            // Symbol E72B = "back" arrow of the Windows font.
            double xBack = Manager.ScreenWidth - size / 2 - ScreenMetrics.Px(10);
            double yBack = Manager.ScreenHeight / 2;
            GazeButton back = new GazeButton("", null, Texts.Get("scroll.quit"));
            back.Place(layer, surface, xBack, yBack, size);
            _buttons.Add(back);

            layer.ShowDragSurface();
        }

        /// <summary>
        /// Places the arc on the circle, centered on the gaze direction (dx, dy: gap between the
        /// gaze and the center, in screen pixels).
        ///
        /// Atan2 gives the angle of this direction. On the screen, the Y axis goes down: an
        /// increasing angle therefore turns clockwise, hence "Clockwise".
        /// </summary>
        private void DrawArc(double dx, double dy, double radiusPixels)
        {
            if (_arc == null)
            {
                return;
            }

            OverlayWindow layer = Manager.Layer;
            Point center = layer.ToWpf(_px, _py);
            double radius = layer.LengthToWpf(radiusPixels);

            double angle = Math.Atan2(dy, dx);
            double half = ArcHalfAngle * Math.PI / 180;
            Point startPoint = new Point(center.X + radius * Math.Cos(angle - half), center.Y + radius * Math.Sin(angle - half));
            Point arrival = new Point(center.X + radius * Math.Cos(angle + half), center.Y + radius * Math.Sin(angle + half));

            PathFigure figure = new PathFigure();
            figure.StartPoint = startPoint;
            figure.IsClosed = false;
            figure.IsFilled = false;
            figure.Segments.Add(new ArcSegment(arrival, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            PathGeometry shape = new PathGeometry();
            shape.Figures.Add(figure);
            shape.Freeze();

            _arcBackground.Data = shape;
            _arc.Data = shape;
            _arcBackground.Visibility = Visibility.Visible;
            _arc.Visibility = Visibility.Visible;
        }
    }
}
