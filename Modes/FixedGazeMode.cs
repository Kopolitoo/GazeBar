using System;

namespace GazeBar
{
    /// <summary>
    /// FIXED GAZE MODE (step 2 of the specification).
    ///
    /// We get here from movement mode, when the gaze has fixated on a point P.
    ///  - The circle freezes around P, with a white border. It starts from where the circle of
    ///    movement mode was and slides to P in a few ticks: no visual jump.
    ///  - An options icon is stuck to the circle, on the side facing the center of the screen
    ///    (cursor top left → icon bottom right, etc.; in the center: bottom right).
    ///  - Cursor: it is placed on P as soon as this mode starts. This is the only time it joins
    ///    the gaze when [MouseTracking] is off (user's choice of 2026-10-22; the specification
    ///    planned to place it when the icon is activated).
    ///
    /// Then, depending on where one looks:
    ///  - in the circle: if [DirectClick] is on (off by default), the ring fills up during
    ///    [DirectClickDelay]; when full, a left click is made on P, then back to movement
    ///    mode. Looking at the icon resets this delay;
    ///  - on the icon: its ring fills up during [OptionsIconDelay] (shorter than
    ///    [ActivationDelay]); when full, the icon activates and opens action choice mode
    ///    (step 3), or first magnifier mode (step 5) if [MagnifierBeforeAction] is on;
    ///  - outside the circle and the icon: circle and icon disappear, back to movement mode.
    ///
    /// A single isolated measure in another zone is ignored (same tolerance as for the
    /// fixation): two measures in a row are needed to change zone.
    ///
    /// Gaze right next to the screen (GazeState.NearScreen, 2026-11-02): it is brought back
    /// to the nearest edge instead of leaving this mode. Further off screen: back to movement
    /// mode, as before.
    /// </summary>
    internal sealed class FixedGazeMode : Mode
    {
        /// <summary>The zones where the gaze can be.</summary>
        private enum Zone
        {
            Circle,
            Icon,
            Outside
        }

        // Number of measures in a row needed in a new zone to switch to it.
        private const int MeasuresToChange = 2;

        /// <summary>
        /// Activation zone of the options icon, relative to the drawn icon: 50 % larger (user
        /// requests of 2026-10-28, 1.2, then 2026-11-02, 1.5).
        /// The drawing itself does not change. The zone overlaps the circle a bit more, on the
        /// icon's side: there, the icon wins (see ZoneOf).
        /// </summary>
        private const double IconZoneFactor = 1.5;

        // The fixated point, in screen pixels.
        private readonly double _px;
        private readonly double _py;

        // Center of the circle as drawn: it slides towards P (see Enter).
        private readonly Smoother _smoothed = new Smoother();

        // Center of the options icon, in screen pixels (computed in Enter).
        private double _ix;
        private double _iy;

        // Chosen zone, and "candidate" zone seen on the last measures.
        private Zone _zone = Zone.Circle;
        private Zone _candidateShape = Zone.Circle;
        private int _candidateShapeMeasures;
        private long _lastNumber = -1;

        // Times (ms) when the gaze entered the circle or the icon; -1 = timer stopped.
        private double _circleStartMs = -1;
        private double _iconStartMs = -1;

        public FixedGazeMode(double x, double y)
        {
            _px = x;
            _py = y;
        }

        public override string Name
        {
            get { return "Fixed gaze"; }
        }

        public override void Enter(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // The drawn circle starts from where it already is (circle of movement mode), otherwise from P.
            if (Manager.Layer.CircleShown)
            {
                _smoothed.Place(Manager.Layer.CircleX, Manager.Layer.CircleY);
            }
            else
            {
                _smoothed.Place(_px, _py);
            }

            PlaceIcon(p.CircleRadiusPx, p.IconSizePx);

            // The gaze is fixated: the cursor joins it now.
            PlaceCursor();

            App.Log("Fixed gaze at (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;
            double now = gaze.NowMs;

            // Gaze clearly off screen: we have necessarily left the circle and the icon. Right
            // next to the screen (GazeState.NearScreen), we stay: in a corner, a few
            // overflowing measures must not cancel everything (2026-11-02).
            if (gaze.Available && !gaze.NearScreen)
            {
                ReturnToMovement(false);
                return;
            }

            if (!gaze.Available)
            {
                // Gaze lost (eyes closed...): we stay in fixed gaze, but the timers start again
                // from zero, so as never to click on a gaze we no longer see.
                _circleStartMs = -1;
                _iconStartMs = -1;
            }
            else if (gaze.Number != _lastNumber)
            {
                // New measure: which zone is it in?
                _lastNumber = gaze.Number;
                // Position brought back into the screen: identical to the real gaze on the
                // screen; right next to it, it is the nearest edge point (same rule as the
                // fixation, see FixationDetector). This way, an icon stuck to the screen edge
                // can also be aimed at by looking slightly beyond the edge.
                ChangeZoneIfNeeded(ZoneOf(gaze.XScreen, gaze.YScreen, p));

                if (_zone == Zone.Outside)
                {
                    ReturnToMovement(false);
                    return;
                }
            }

            // The timers of the chosen zone (only if the gaze is available).
            if (gaze.Available)
            {
                if (_zone == Zone.Circle)
                {
                    _iconStartMs = -1;
                    if (_circleStartMs < 0)
                    {
                        _circleStartMs = now;
                    }
                }
                else
                {
                    _circleStartMs = -1;
                    if (_iconStartMs < 0)
                    {
                        _iconStartMs = now;
                    }
                }
            }

            double clickProgress = 0;
            if (p.DirectClick && _circleStartMs >= 0)
            {
                clickProgress = (now - _circleStartMs) / (p.DirectClickDelay * 1000.0);
            }

            double iconProgress = 0;
            if (_iconStartMs >= 0)
            {
                // Delay specific to the options icon, shorter than the one of the other buttons.
                iconProgress = (now - _iconStartMs) / (p.OptionsIconDelay * 1000.0);
            }

            // Has an action reached its end?
            if (clickProgress >= 1)
            {
                DirectClick();
                return;
            }
            if (iconProgress >= 1)
            {
                ActivateIcon();
                return;
            }

            // Drawing.
            // The circle slides smoothly towards P (see Smoother.cs; a few ticks are enough).
            _smoothed.Follow(_px, _py, gaze.NowMs);
            Manager.Layer.ShowCircle(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, true);
            if (clickProgress > 0)
            {
                Manager.Layer.ShowRing(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, clickProgress);
            }
            else
            {
                Manager.Layer.HideRing();
            }
            Manager.Layer.ShowIcon(_ix, _iy, p.IconSizePx, iconProgress,
                                              p.ShowLabels && _zone == Zone.Icon);
        }

        public override void Quit()
        {
            // The circle stays visible: movement mode takes it over where it is.
            Manager.Layer.HideRing();
            Manager.Layer.HideIcon();
        }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>
        /// Direct left click on the fixated point. The overlay lets clicks through: no need to
        /// hide it first. The cursor is put back on P for safety (it may have been moved).
        /// </summary>
        private void DirectClick()
        {
            PlaceCursor();
            MouseInjector.Press(MouseSide.Left);
            MouseInjector.Release(MouseSide.Left);
            App.Log("Direct click at (" + _px.ToString("F0") + ", " + _py.ToString("F0") + ")");
            ReturnToMovement(true);
        }

        /// <summary>
        /// Activation of the options icon: we switch to action choice mode (step 3), which
        /// takes a picture of the zone around P and offers the actions.
        /// If the [MagnifierBeforeAction] option is on, we first go through magnifier mode
        /// (step 5), which refines the position then opens the action choice itself.
        /// This option is set in the "Magnifier, drag" tab of settings mode (step 7).
        /// </summary>
        private void ActivateIcon()
        {
            PlaceCursor();
            if (AppSettings.Current.MagnifierBeforeAction)
            {
                // "false": the magnifier's "back" icon will return to movement mode.
                Manager.ChangeMode(new MagnifierMode(_px, _py, false));
            }
            else
            {
                Manager.ChangeMode(new ActionChoiceMode(_px, _py));
            }
        }

        /// <summary>
        /// Back to movement mode. ignoreHere = true after an action: a new fixation at the same
        /// place will not be recognized until the gaze has left it.
        /// </summary>
        private void ReturnToMovement(bool ignoreHere)
        {
            Manager.ChangeMode(ignoreHere ? new MovementMode(_px, _py) : new MovementMode());
        }

        // ───────────────────────── Computations ─────────────────────────

        /// <summary>
        /// Places the icon diagonally, on the side of the circle facing the screen center.
        /// The nearest corner of the icon touches the circle: at 45°, a point of the circle is
        /// offset by radius ÷ √2 on each axis, to which half the icon side is added.
        /// The icon is then kept fully on the screen.
        /// </summary>
        private void PlaceIcon(double radius, double size)
        {
            double width = Manager.ScreenWidth;
            double height = Manager.ScreenHeight;

            // Direction: +1 = to the right / down, −1 = to the left / up.
            // "<=": at the exact center of the screen, the icon goes bottom right.
            double directionX = _px <= width / 2 ? 1 : -1;
            double directionY = _py <= height / 2 ? 1 : -1;

            double offset = radius / Math.Sqrt(2) + size / 2;
            _ix = Clamp(_px + directionX * offset, size / 2, width - size / 2);
            _iy = Clamp(_py + directionY * offset, size / 2, height - size / 2);
        }

        /// <summary>Which zone is this gaze point in?</summary>
        private Zone ZoneOf(double x, double y, AppSettings p)
        {
            // The activation zone of the icon is a square [IconZoneFactor] times larger than
            // the drawn icon. It overlaps the circle a bit, near the corner touching it: there,
            // the icon wins (it is tested first).
            double halfSide = p.IconSizePx * IconZoneFactor / 2;
            if (Math.Abs(x - _ix) <= halfSide && Math.Abs(y - _iy) <= halfSide)
            {
                return Zone.Icon;
            }

            double dx = x - _px;
            double dy = y - _py;
            if (Math.Sqrt(dx * dx + dy * dy) <= p.CircleRadiusPx)
            {
                return Zone.Circle;
            }

            return Zone.Outside;
        }

        /// <summary>
        /// Changes zone only if the new zone is confirmed by [MeasuresToChange] measures in a
        /// row. An isolated measure is ignored.
        /// </summary>
        private void ChangeZoneIfNeeded(Zone view)
        {
            if (view == _zone)
            {
                _candidateShapeMeasures = 0;
                return;
            }

            if (view == _candidateShape && _candidateShapeMeasures > 0)
            {
                _candidateShapeMeasures++;
            }
            else
            {
                _candidateShape = view;
                _candidateShapeMeasures = 1;
            }

            if (_candidateShapeMeasures >= MeasuresToChange)
            {
                _zone = view;
                _candidateShapeMeasures = 0;
            }
        }

        private void PlaceCursor()
        {
            NativeMethods.SetCursorPos((int)Math.Round(_px), (int)Math.Round(_py));
        }

        private static double Clamp(double val, double min, double max)
        {
            return Math.Max(min, Math.Min(max, val));
        }
    }
}
