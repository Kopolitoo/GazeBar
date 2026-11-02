using System;

namespace GazeBar
{
    /// <summary>
    /// MOVEMENT MODE (default mode, step 2 of the specification).
    ///
    /// A [CircleRadius] circle follows the gaze on the overlay, smoothed ([Smoothing], see
    /// Smoother.cs). The mouse cursor does not move, unless [MouseTracking] is on: it then
    /// follows the circle. So as not to jitter, it only gets on the way when the circle has
    /// moved more than [CursorDeadZone] pixels away; then it follows continuously until it
    /// catches up.
    ///
    /// At the same time, a FixationDetector watches the gaze. When the gaze has stayed still
    /// for [FixationDelay], we switch to fixed gaze mode on the fixated point.
    /// (No progress ring during this wait.)
    ///
    /// Gaze off screen: the circle keeps following the gaze, but clamped to the screen edges
    /// (each axis separately, see GazeState.XScreen). Gaze above the screen: the circle slides
    /// from left to right along the top edge; gaze on the right: it slides up and down along
    /// the right edge. The fixation, however, is forgotten as soon as the gaze moves away
    /// from the screen; but a gaze JUST next to the screen (narrow band, see
    /// GazeState.NearScreen) still counts, brought back to the edge: otherwise, in the
    /// corners, the measures overflowing a little prevented fixed gaze (2026-11-02).
    /// Gaze lost (no measure): the circle and the cursor stay where they are.
    ///
    /// It is one of the only modes from which the menu can open (with pause, step 6).
    ///
    /// Step 12, gaze keyboard (see GazeKeyboard): when it is shown and the gaze is ON it, this
    /// mode hands over to it: the cursor does not move, the circle is hidden, no fixation
    /// starts, and the keys react. The circle keeps following the gaze secretly: it reappears
    /// without a jump when leaving the keyboard.
    /// </summary>
    internal sealed class MovementMode : Mode
    {
        private readonly FixationDetector _fixation = new FixationDetector();

        // Position (decimal) of the smoothed circle, in screen pixels (see Smoother.cs).
        private readonly Smoother _smoothed = new Smoother();

        // True when the cursor is "on the way" behind the circle ([MouseTracking]).
        private bool _cursorOnTheWay;

        // True as soon as the circle has a position (otherwise it is not drawn yet).
        private bool _placed;

        // "Already handled" zone: after a direct click, no fixation is started again at the
        // same place until the gaze has left it. Otherwise, by keeping looking at the button
        // just clicked, it would be clicked again and again.
        private bool _ignoredZone;
        private readonly double _zoneX;
        private readonly double _zoneY;

        /// <summary>Ordinary movement mode.</summary>
        public MovementMode()
        {
        }

        /// <summary>
        /// Movement mode that ignores a new fixation around (x, y), within a radius of
        /// [CircleRadius], until the gaze has left this zone.
        /// Used after a direct click, or an action done at this place.
        /// </summary>
        public MovementMode(double xIgnored, double yIgnored)
        {
            _ignoredZone = true;
            _zoneX = xIgnored;
            _zoneY = yIgnored;
        }

        public override string Name
        {
            get { return "Mouvement"; }
        }

        /// <summary>The menu opens from movement mode (gaze down, below the screen).</summary>
        public override bool MenuAllowed
        {
            get { return true; }
        }

        /// <summary>
        /// The screen changed (step 11): this mode carries on, because it redraws the circle on
        /// each tick, with the sizes of the new screen.
        /// </summary>
        public override bool AdaptToScreen()
        {
            return true;
        }

        public override void Enter(GazeState gaze)
        {
            // Back to movement mode = end of the action (or giving up): the Ctrl, Shift, Alt
            // modifiers of the action choice are deactivated (see Modifiers.cs).
            Modifiers.DisableAll();

            // Starting point of the circle, never placed "by default" in the center:
            //  - if a circle is already shown (coming back from fixed gaze mode), start from
            //    where it is: it will slide smoothly towards the gaze, without a jump;
            //  - otherwise, start directly from the gaze (brought back into the screen if
            //    outside);
            //  - if the gaze is not available yet, nothing is drawn: the circle will appear at
            //    the first measure.
            if (Manager.Layer.CircleShown)
            {
                Place(Manager.Layer.CircleX, Manager.Layer.CircleY);
            }
            else if (gaze.Available)
            {
                Place(gaze.XScreen, gaze.YScreen);
            }
            else
            {
                _placed = false;
            }

            _fixation.Reset();
            Manager.Layer.HideRing();
            Manager.Layer.HideIcon();
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // Is the gaze on the gaze keyboard (step 12)? Real gaze, on the screen.
            GazeKeyboard keyboard = Manager.Keyboard;
            bool onKeyboard = gaze.OnScreen && keyboard.Contains(gaze.X, gaze.Y);

            if (gaze.Available)
            {
                // 1. The circle moves smoothly towards the gaze (see Smoother.cs).
                //    (First measure received: it appears directly on the gaze.)
                //    We aim at the position BROUGHT BACK INTO THE SCREEN: if the gaze leaves
                //    the screen, the circle stays at the edge and slides along it.
                if (!_placed)
                {
                    Place(gaze.XScreen, gaze.YScreen);
                }
                _smoothed.Follow(gaze.XScreen, gaze.YScreen, gaze.NowMs);

                // 2. The cursor follows the circle, only if the [MouseTracking] option is on.
                //    Otherwise, it does not move: it will join the gaze once fixated.
                if (p.MouseTracking && !onKeyboard)
                {
                    MoveCursor(p.CursorDeadZonePx);
                }
            }

            // 3. Has the gaze left the ignored zone? (Only with a gaze on the screen, as
            //    before: a gaze off screen does not count.)
            if (gaze.OnScreen && _ignoredZone &&
                Distance(gaze.X, gaze.Y, _zoneX, _zoneY) > p.CircleRadiusPx)
            {
                _ignoredZone = false;
            }

            // 4. The fixation. In the ignored zone, it is not allowed to progress.
            //    A gaze clearly off screen resets it; right next to the screen, it is brought
            //    back to the edge (see FixationDetector.Add).
            //    No progress ring here (user's choice): the ring is reserved for buttons
            //    (options icon, action choice, menu bar).
            _fixation.Add(gaze);
            if (_ignoredZone || onKeyboard)
            {
                // On the keyboard, no fixation: the keys react.
                _fixation.Reset();
            }

            // 4b. The gaze keyboard (step 12): key hovered, activated... If it is not shown,
            //     it does nothing.
            keyboard.Update(gaze);

            if (_fixation.Reached)
            {
                Manager.ChangeMode(new FixedGazeMode(_fixation.X, _fixation.Y));
                return;
            }

            // 5. Drawing the circle (as soon as there is a position).
            //    It is hidden above the keyboard (step 12), including when the gaze is below
            //    the screen: the circle, clamped to the bottom edge, would then be on the keyboard.
            if (_placed)
            {
                if (onKeyboard || keyboard.Contains(_smoothed.X, _smoothed.Y))
                {
                    Manager.Layer.HideCircle();
                }
                else
                {
                    Manager.Layer.ShowCircle(_smoothed.X, _smoothed.Y, p.CircleRadiusPx, false);
                }
            }
        }

        public override void Quit()
        {
            // The circle stays visible: the next mode takes it over where it is.
            Manager.Layer.HideRing();
        }

        /// <summary>Places the circle directly at (x, y), without smoothing.</summary>
        private void Place(double x, double y)
        {
            _smoothed.Place(x, y);
            _placed = true;
        }

        /// <summary>
        /// Makes the cursor follow the circle, without jitter:
        ///  - at rest, the cursor does not move as long as the circle is less than deadZone
        ///    pixels away (the small gaze jitter is ignored);
        ///  - as soon as the circle moves further away, the cursor gets "on the way" and
        ///    follows the circle on EVERY tick, continuously, until it has caught up (less than
        ///    one pixel).
        /// Before, the cursor stayed still then jumped deadZone pixels at once, again and
        /// again: it moved in small leaps, which gave a jerky movement.
        /// The real cursor position is read: if it was moved by the mouse in the meantime,
        /// this is taken into account.
        /// </summary>
        private void MoveCursor(double deadZone)
        {
            NativeMethods.POINT cursor;
            if (!NativeMethods.GetCursorPos(out cursor))
            {
                return;
            }

            double distance = Distance(cursor.X, cursor.Y, _smoothed.X, _smoothed.Y);
            if (!_cursorOnTheWay && distance >= deadZone)
            {
                _cursorOnTheWay = true;
            }
            if (!_cursorOnTheWay)
            {
                return;
            }

            NativeMethods.SetCursorPos((int)Math.Round(_smoothed.X), (int)Math.Round(_smoothed.Y));
            if (distance < 1)
            {
                _cursorOnTheWay = false; // caught up: back to rest
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
