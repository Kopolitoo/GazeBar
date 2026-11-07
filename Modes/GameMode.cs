using System;

namespace Prunel
{
    /// <summary>
    /// GAME MODE (section "10. Game mode" of the specification).
    ///
    /// Entry: "Game" button of the menu. Exit: looking down, below the screen, for
    /// [MenuDelay] opens the menu (MenuAllowed = yes), where the button becomes "Quit game".
    /// The caregiver's Escape key also returns to movement mode.
    ///
    /// In this mode, nothing is drawn: no circle, no fixation, no fixed gaze, no gaze click.
    /// The user clicks with his switch; no keyboard key is sent.
    ///
    /// Two variants, chosen by the [GameMouse] setting:
    ///
    ///   "direct" (default, for Mindball Play): the cursor moves EXACTLY as in movement mode
    ///   with mouse tracking, but without a circle (user request: with
    ///   [GameSmoothing] = 0.7 then 0.3, the cursor stuttered too much). So: the same double
    ///   smoothing as movement mode, but with its own [GameSmoothing] setting (0.16 by
    ///   default, like [Smoothing]: the user wants two separate settings), and the
    ///   [CursorDeadZone] anti-jitter:
    ///   at rest, the cursor does not move as long as the smoothed gaze is less than
    ///   [CursorDeadZone] pixels away; beyond that, it follows continuously until it has
    ///   caught up. Gaze lost or off screen: the cursor stays where it is.
    ///
    ///   "relative" (for other games): many games ignore the cursor position and only read
    ///   the mouse MOVEMENTS ("raw input"). So movements are sent with SendInput. Two ways of
    ///   translating the gaze, chosen by [GameRelative] (the specification says "to test"):
    ///     "tracking": the movement sent follows the movement of the smoothed gaze
    ///                 (gaze moves 50 px → mouse moves 50 px × [GameSensitivity]);
    ///     "gamepad" : the gap between the gaze and the screen CENTER gives a speed, like the
    ///                 small stick of a gamepad. In the center, within a circle of radius
    ///                 [CircleRadius], nothing moves; the speed then increases up to the
    ///                 screen edge.
    ///   Gaze lost or off screen: no more movement is sent.
    ///
    /// Note, known limits: a game in "exclusive full screen" may hide Prunel's overlay (the
    /// bottom menu might then not be visible), and nothing is guaranteed against the
    /// anti-cheat systems of some online games, which may refuse simulated input.
    /// </summary>
    internal sealed class GameMode : Mode
    {
        /// <summary>
        /// Gamepad mode: maximum cursor speed, in 4K screen pixels per second (converted with
        /// ScreenMetrics.GazePx), reached when the gaze is at the screen edge (before
        /// multiplying by [GameSensitivity]).
        /// Starting value, to adjust after testing.
        /// </summary>
        private const double MaxGamepadSpeed = 1500;

        // The gaze smoothed with [GameSmoothing] (setting separate from [Smoothing], the one of movement mode).
        private readonly Smoother _smoothed = new Smoother(() => AppSettings.Current.GameSmoothing);

        // True when _smoothed has a valid position (otherwise it is placed on the gaze).
        private bool _placed;

        // Direct mouse: true when the cursor is "on the way" behind the smoothed gaze
        // (same principle as the mouse tracking of movement mode).
        private bool _cursorOnTheWay;

        // Relative mouse, tracking mode: smoothed position at the previous tick.
        private double _previousX;
        private double _previousY;

        // Relative mouse: the pixel fractions not sent yet. SendInput only accepts whole
        // pixels; without these remainders, small slow movements would be lost.
        private double _remainderX;
        private double _remainderY;

        // Relative mouse, gamepad mode: time of the previous tick (−1 = none).
        private double _lastMs = -1;

        public override string Name
        {
            get { return "Game (" + AppSettings.Current.GameMouse + ")"; }
        }

        /// <summary>The menu opens from the game: it is the gaze way out.</summary>
        public override bool MenuAllowed
        {
            get { return true; }
        }

        /// <summary>
        /// The screen changed: this mode carries on, because a game must not be
        /// interrupted, and the cursor position is recomputed on each tick.
        /// </summary>
        public override bool AdaptToScreen()
        {
            return true;
        }

        public override void Enter(GazeState gaze)
        {
            // No drawing in this mode: the game must stay fully visible.
            Manager.Layer.HideAll();
            Modifiers.DisableAll();

            // Start from zero on each entry (also when coming back from the menu): the
            // smoothing starts again from the current gaze, without sliding from an old position.
            _placed = false;
            _cursorOnTheWay = false;
            _remainderX = 0;
            _remainderY = 0;
            _lastMs = -1;

            App.LogDetail("Game mode: mouse " + AppSettings.Current.GameMouse +
                    (AppSettings.Current.RelativeMouse ? " (" + AppSettings.Current.GameRelative + ")" : ""));
        }

        public override void Update(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // Gaze lost or off screen: nothing moves any more. When the gaze comes back, the
            // smoothing will start again from the gaze (no sudden move in relative mode).
            if (!gaze.OnScreen)
            {
                _placed = false;
                _lastMs = -1;
                _remainderX = 0;
                _remainderY = 0;
                return;
            }

            if (!_placed)
            {
                // Starting point of the smoothing. With the direct mouse, we start from the
                // CURSOR (as the circle of movement mode starts from its place): it slides
                // towards the gaze instead of jumping. With the relative mouse, we start from
                // the gaze (no movement sent).
                NativeMethods.POINT cursor;
                if (!p.RelativeMouse && NativeMethods.GetCursorPos(out cursor))
                {
                    _smoothed.Place(cursor.X, cursor.Y);
                }
                else
                {
                    _smoothed.Place(gaze.X, gaze.Y);
                }
                _previousX = _smoothed.X;
                _previousY = _smoothed.Y;
                _placed = true;
            }
            _smoothed.Follow(gaze.X, gaze.Y, gaze.NowMs);

            if (!p.RelativeMouse)
            {
                DirectMouse();
            }
            else if (p.GamepadRelative)
            {
                GamepadMouse(gaze.NowMs);
            }
            else
            {
                TrackingMouse();
            }
        }

        /// <summary>
        /// Direct mouse: the cursor follows the smoothed gaze, like the mouse tracking of
        /// movement mode (MovementMode.MoveCursor):
        ///  - at rest, the cursor does not move as long as the smoothed gaze is less than
        ///    [CursorDeadZone] pixels away (the small gaze jitter is ignored);
        ///  - beyond that, it gets "on the way" and follows on each frame, continuously, until
        ///    it has caught up (less than one pixel), then goes back to rest.
        /// The real cursor position is read: if the game moved it, this is taken into account.
        /// </summary>
        private void DirectMouse()
        {
            NativeMethods.POINT cursor;
            if (!NativeMethods.GetCursorPos(out cursor))
            {
                return;
            }

            double gapX = cursor.X - _smoothed.X;
            double gapY = cursor.Y - _smoothed.Y;
            double distance = Math.Sqrt(gapX * gapX + gapY * gapY);
            if (!_cursorOnTheWay && distance >= AppSettings.Current.CursorDeadZonePx)
            {
                _cursorOnTheWay = true;
            }
            if (!_cursorOnTheWay)
            {
                return;
            }

            MouseInjector.PointTo(_smoothed.X, _smoothed.Y);
            if (distance < 1)
            {
                _cursorOnTheWay = false; // caught up: back to rest
            }
        }

        /// <summary>Relative mouse "tracking": send the movement of the smoothed gaze since the previous tick.</summary>
        private void TrackingMouse()
        {
            double dx = (_smoothed.X - _previousX) * AppSettings.Current.GameSensitivity;
            double dy = (_smoothed.Y - _previousY) * AppSettings.Current.GameSensitivity;
            _previousX = _smoothed.X;
            _previousY = _smoothed.Y;
            SendMove(dx, dy);
        }

        /// <summary>
        /// Relative mouse "gamepad": the gap between the smoothed gaze and the screen center
        /// gives a speed. The movement sent = speed × time elapsed since the previous tick.
        /// </summary>
        private void GamepadMouse(double nowMs)
        {
            double dt = _lastMs < 0 ? 0 : (nowMs - _lastMs) / 1000.0;
            _lastMs = nowMs;
            if (dt <= 0)
            {
                return;
            }
            if (dt > 0.05)
            {
                dt = 0.05; // after a very late tick, no big leap
            }

            double centerX = Manager.ScreenWidth / 2;
            double centerY = Manager.ScreenHeight / 2;
            double gapX = _smoothed.X - centerX;
            double gapY = _smoothed.Y - centerY;
            double distance = Math.Sqrt(gapX * gapX + gapY * gapY);

            // Neutral zone in the center: the circle of radius [CircleRadius], as for scrolling.
            double neutralZone = AppSettings.Current.CircleRadiusPx;
            // Distance where the speed is at its maximum: the nearest edge (half height).
            double maxDistance = Math.Min(centerX, centerY);
            if (distance <= neutralZone || maxDistance <= neutralZone)
            {
                return;
            }

            // From 0 (edge of the neutral zone) to 1 (screen edge), without going above 1.
            double force = Math.Min(1.0, (distance - neutralZone) / (maxDistance - neutralZone));
            // MaxGamepadSpeed is in 4K screen pixels per second: converted.
            double speed = force * ScreenMetrics.GazePx(MaxGamepadSpeed) * AppSettings.Current.GameSensitivity;

            // Same direction as the gap (gap / distance = direction of length 1).
            SendMove(gapX / distance * speed * dt, gapY / distance * speed * dt);
        }

        /// <summary>Adds the movement to the remainders and sends the whole part (in pixels).</summary>
        private void SendMove(double dx, double dy)
        {
            _remainderX += dx;
            _remainderY += dy;
            int wholeX = (int)Math.Truncate(_remainderX);
            int wholeY = (int)Math.Truncate(_remainderY);
            if (wholeX == 0 && wholeY == 0)
            {
                return;
            }
            _remainderX -= wholeX;
            _remainderY -= wholeY;
            MouseInjector.MoveBy(wholeX, wholeY);
        }
    }
}
