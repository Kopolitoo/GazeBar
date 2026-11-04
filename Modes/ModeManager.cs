using System;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// LOGIC: the conductor of the modes.
    ///
    /// It knows the current mode and the previous mode, and it alone switches from one mode
    /// to another (ChangeMode method). Having a single crossing point makes it possible to
    /// always tidy up the old mode properly, to log everything, and later to add transition
    /// animations in one place.
    ///
    /// It also owns the shared clock: on each frame drawn by the screen (usually 60 times per
    /// second), it reads the gaze, prepares a GazeState and gives it to the current mode (the
    /// mode's Update method).
    ///
    /// Why "on each frame"? Rather than a timer (DispatcherTimer) every 16 ms: such a timer
    /// is not precise on Windows (it often fires after 15 or 31 ms), and it is not synchronized
    /// with the screen: some frames would show two circle moves, others none, and the movement
    /// would "catch". WPF's CompositionTarget.Rendering event is called just before WPF draws
    /// each frame: the circle moves exactly once per frame, regularly.
    ///
    /// The modes find everything they need there: the gaze (Gaze), the full-screen overlay
    /// (Layer) and the screen size.
    ///
    /// The first mode is usually movement mode; it hands over to fixed gaze mode when a point
    /// is fixated, and fixed gaze mode hands it back.
    ///
    /// It also opens the menu: when the gaze stays down, below the screen, for
    /// [MenuDelay], we switch to menu mode. Only movement, pause and game
    /// modes allow it (Mode.MenuAllowed property).
    /// </summary>
    internal sealed class ModeManager
    {
        /// <summary>
        /// Usual interval between two updates, in milliseconds (16 ms ≈ 60 frames per second,
        /// the rate of most screens). It is only an order of magnitude: the clock follows the
        /// screen, and the modes always measure real time (NowMs).
        /// </summary>
        public const int IntervalMs = 16;

        // True between Start and Stop (the clock is hooked to CompositionTarget.Rendering).
        private bool _running;

        // Time of the frame already handled: WPF sometimes calls Rendering twice for the same
        // frame; it is handled only once.
        private TimeSpan _lastImage = TimeSpan.Zero;

        // Time (ms) when the gaze went down below the screen; -1 = it is not there.
        private double _belowStartMs = -1;

        // For the log: the gaze situation at the previous tick (see NoteGazePosition).
        // Only the situation is logged, never the gaze coordinates.
        private string _gazeSituation = "";

        public ModeManager(GazeSource gaze, OverlayWindow layer)
        {
            Gaze = gaze;
            Layer = layer;
            Keyboard = new GazeKeyboard(layer);

            // The screen may change while GazeBar runs.
            ScreenMetrics.Change += OnScreenChanged;
        }

        /// <summary>
        /// The Windows resolution, scaling or text size changed. The current mode
        /// adapts if it can (Mode.AdaptToScreen); otherwise we return to movement mode, as with
        /// the Escape key: mouse buttons and Ctrl, Shift, Alt keys released for safety (a drag
        /// and drop is interrupted).
        /// </summary>
        private void OnScreenChanged()
        {
            if (CurrentMode == null)
            {
                return;
            }
            // The gaze keyboard redraws itself at the new sizes.
            Keyboard.AdaptToScreen();
            if (CurrentMode.AdaptToScreen())
            {
                App.Log("Screen changed: mode " + CurrentMode.Name + " goes on with the new sizes");
                return;
            }
            App.Log("Screen changed: back to movement mode (mode " + CurrentMode.Name + " was laid out for the old screen)");
            MouseInjector.ReleaseAll();
            KeyboardInjector.ReleaseAll();
            ChangeMode(new MovementMode());
        }

        /// <summary>The gaze source (eyetracker).</summary>
        public GazeSource Gaze { get; private set; }

        /// <summary>The transparent full-screen window where the modes draw.</summary>
        public OverlayWindow Layer { get; private set; }

        /// <summary>
        /// The gaze keyboard. It is not a mode: it stays shown during movement and
        /// fixed gaze modes (see GazeKeyboard).
        /// </summary>
        public GazeKeyboard Keyboard { get; private set; }

        /// <summary>The current mode (null before Start).</summary>
        public Mode CurrentMode { get; private set; }

        /// <summary>The previous mode, to be able to go back to it (for example when closing the menu).</summary>
        public Mode PreviousMode { get; private set; }

        /// <summary>Raised after each mode change. The interface can use it to show itself up to date.</summary>
        public event Action<Mode> ModeChanged;

        /// <summary>
        /// Width of the primary screen, in real pixels, measured by ScreenMetrics
        /// (GetSystemMetrics does not see a resolution change made while GazeBar runs, see
        /// NativeMethods.PrimaryScreenSize).
        /// </summary>
        public double ScreenWidth
        {
            get { return ScreenMetrics.WidthPixels; }
        }

        /// <summary>Height of the primary screen, in real pixels, measured by ScreenMetrics.</summary>
        public double ScreenHeight
        {
            get { return ScreenMetrics.HeightPixels; }
        }

        // ───────────────────────── Commands ─────────────────────────

        /// <summary>Enters the first mode and starts the clock.</summary>
        public void Start(Mode firstMode)
        {
            ChangeMode(firstMode);
            if (!_running)
            {
                CompositionTarget.Rendering += OnImage;
                _running = true;
            }
        }

        /// <summary>Stops the clock and cleanly quits the current mode (when the program closes).</summary>
        public void Stop()
        {
            if (_running)
            {
                CompositionTarget.Rendering -= OnImage;
                _running = false;
            }
            if (CurrentMode != null)
            {
                CurrentMode.Quit();
                CurrentMode = null;
            }
        }

        /// <summary>
        /// Switches to the given mode: the old mode is tidied up (Quit), then the new one starts
        /// (Enter). Can be called by a mode itself, from its Update.
        /// </summary>
        public void ChangeMode(Mode newVal)
        {
            if (newVal == null || newVal == CurrentMode)
            {
                return;
            }

            Mode old = CurrentMode;
            if (old != null)
            {
                old.Quit();
            }

            PreviousMode = old;
            CurrentMode = newVal;
            newVal.Attach(this);

            // The gaze keyboard is shown or hidden according to the new mode.
            // BEFORE Enter: the magnifier takes its picture of the screen as soon as it enters, without the keyboard.
            Keyboard.OnModeChange(newVal);

            App.LogDetail("Mode: " + (old == null ? "(none)" : old.Name) + " → " + newVal.Name);
            // The new mode receives the current gaze position to initialize itself.
            newVal.Enter(ReadGaze());

            Action<Mode> subscribers = ModeChanged;
            if (subscribers != null)
            {
                subscribers(newVal);
            }
        }

        /// <summary>Goes back to the previous mode, if there is one.</summary>
        public void ReturnToPreviousMode()
        {
            if (PreviousMode != null)
            {
                ChangeMode(PreviousMode);
            }
        }

        // ───────────────────────── Clock ─────────────────────────

        /// <summary>
        /// Called by WPF just before drawing each frame. Each frame is handled only once (see
        /// _lastImage), then a tick is made.
        /// </summary>
        private void OnImage(object sender, EventArgs e)
        {
            RenderingEventArgs image = e as RenderingEventArgs;
            if (image != null)
            {
                if (image.RenderingTime == _lastImage)
                {
                    return;
                }
                _lastImage = image.RenderingTime;
            }
            OnTick();
        }

        private void OnTick()
        {
            if (CurrentMode == null)
            {
                return;
            }

            // No tick INSIDE another tick. Some operations done during a
            // tick, like opening the settings window (Show, Activate), let WPF process other
            // messages meanwhile: the next frame could then arrive, and update the new mode
            // before the end of its Enter (SettingsMode: aimer still empty →
            // NullReferenceException).
            if (_inTick)
            {
                return;
            }
            _inTick = true;
            try
            {
                Tick();
            }
            finally
            {
                _inTick = false;
            }
        }

        // True during a tick (see OnTick).
        private bool _inTick;

        private void Tick()
        {

            GazeState state = ReadGaze();

            try
            {
                // Does the menu open? If so, the current mode is not updated on this tick.
                if (WatchMenu(state))
                {
                    return;
                }

                CurrentMode.Update(state);
            }
            catch (Exception ex)
            {
                // An error in a mode must not block everything: log it, release the mouse
                // buttons and the keyboard keys for safety, and the clock goes on.
                MouseInjector.ReleaseAll();
                KeyboardInjector.ReleaseAll();
                App.Log("ERROR in mode " + CurrentMode.Name + ": " + ex);
            }
        }

        /// <summary>
        /// Opens the menu when the gaze stays down, below the screen, for [MenuDelay].
        /// The timer starts again from zero as soon as the gaze is elsewhere, or lost: only
        /// what is really seen counts. Returns true if the menu just opened.
        /// </summary>
        private bool WatchMenu(GazeState state)
        {
            NoteGazePosition(state);

            if (!state.Available || !state.BelowScreen)
            {
                _belowStartMs = -1;
                return false;
            }

            if (_belowStartMs < 0)
            {
                _belowStartMs = state.NowMs;
                return false;
            }

            if (state.NowMs - _belowStartMs >= AppSettings.Current.MenuDelay * 1000.0 &&
                CurrentMode.MenuAllowed)
            {
                _belowStartMs = -1;
                // Keyboard shown (so movement mode) → the keyboard's menu bar, not the
                // classic bar.
                bool keyboardMenu = Keyboard.IsOpen && CurrentMode is MovementMode;
                ChangeMode(new MenuMode(keyboardMenu));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Menu diagnosis: writes to the log each time the gaze changes situation (on the
        /// screen, below the screen, elsewhere off screen, lost), with its last known position.
        /// Used to check that the PCEye 5 really measures the gaze BELOW the screen (the
        /// condition to open the menu), instead of losing it.
        /// </summary>
        private void NoteGazePosition(GazeState state)
        {
            string situation;
            if (!state.Available)
            {
                situation = "lost";
            }
            else if (state.BelowScreen)
            {
                situation = "below the screen";
            }
            else if (state.OffScreen)
            {
                situation = "off screen (not below)";
            }
            else
            {
                situation = "on screen";
            }

            if (situation != _gazeSituation)
            {
                _gazeSituation = situation;
                App.LogDetail("Gaze: " + situation + " (screen " + ScreenWidth + " × " + ScreenHeight + ")");
            }
        }

        /// <summary>
        /// Width of the band, around the screen, where a gaze "right next to it" still counts
        /// for the fixation (see GazeState.NearScreen). In 4K screen pixels (2160 px high):
        /// 120 px on the 4K screen, i.e. 60 px on a 1080 px screen.
        /// Wide enough for the measures overflowing in the corners, narrow enough so that a
        /// gaze clearly off screen (to open the menu, for example) does not trigger fixed gaze.
        /// </summary>
        private const double EdgesMargin4K = 120;

        /// <summary>Prepares the gaze "snapshot" for the current mode.</summary>
        private GazeState ReadGaze()
        {
            GazeState state = new GazeState();
            state.NowMs = Gaze.NowMs;

            if (!Gaze.IsRunning || !Gaze.HasData ||
                Gaze.MsSinceLastSample > AppSettings.Current.MaxMeasureAge)
            {
                state.Available = false;
                return state;
            }

            double x;
            double y;
            long num;
            double instantMs;
            Gaze.GetLatest(out x, out y, out num, out instantMs);

            double width = ScreenWidth;
            double height = ScreenHeight;

            state.Available = true;
            state.X = x;
            state.Y = y;
            state.Number = num;
            state.MeasureTimeMs = instantMs;
            state.OffScreen = GazeSource.IsOffScreen(x, y, width, height);
            state.BelowScreen = y >= height;

            // The same position, but brought back into the screen (from 0 to width − 1, from 0
            // to height − 1). Math.Max / Math.Min "clamp" the value between two bounds.
            state.XScreen = Math.Max(0, Math.Min(width - 1, x));
            state.YScreen = Math.Max(0, Math.Min(height - 1, y));

            // Very close to the screen? (On the screen, or off screen by less than the margin.)
            double margin = ScreenMetrics.GazePx(EdgesMargin4K);
            state.NearScreen = x >= -margin && y >= -margin &&
                                x < width + margin && y < height + margin;
            return state;
        }
    }
}
