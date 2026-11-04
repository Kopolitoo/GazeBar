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
    /// ACTION CHOICE MODE.
    ///
    /// We get here from fixed gaze mode, by looking at the options icon. The cursor is already
    /// on the fixated point P (placed when entering fixed gaze).
    ///
    /// The mode runs in three phases ("steps"):
    ///
    ///  1. CAPTURE. The circle and the icon of the overlay are hidden, we wait [WaitMs] for
    ///     Windows to finish erasing them, then we take a picture of the square enclosing the
    ///     circle (side 2 × [CircleRadius], i.e. 200 × 200 px). Without this wait, the photo
    ///     would contain the circle.
    ///
    ///  2. CHOICE. A black screen covers the whole screen. In the center: the photo enlarged
    ///     ×2 (400 × 400 px) and a cross in place of the cursor. Around it: the crown of eight
    ///     action slots. On the left: the three modifier slots. On the right: the "back" icon.
    ///     A slot without an action is still drawn, grayed, to show that an action can be put
    ///     there. On top of everything, a small circle (half of
    ///     [CircleRadius]) shows where the gaze is.
    ///     Looking at an icon for [ActivationDelay] activates it (its ring fills up).
    ///
    ///  3. EXECUTION. The black screen is hidden, we wait [WaitMs] for it to disappear, then
    ///     the click is injected at the cursor. This way, the target application is visible
    ///     (and up to date) at the moment of the click. We then return to movement mode.
    ///
    /// The "back" icon returns to movement mode without doing anything.
    ///
    /// The "Magnifier" action (crown slot 4) opens magnifier mode on the same point P,
    /// to refine the cursor position. The magnifier then comes back here, on the refined point
    /// (or on P if "back" is chosen there).
    ///
    /// Drag and drop and modifiers:
    ///   - the three "drag and drop" actions (left, right, android) open the choice of the drop
    ///     point (DragTargetMode), with P as the start point;
    ///   - the three slots on the left are the Ctrl, Shift and Alt modifiers. A gaze activates
    ///     one (green border), a second one deactivates it; the action choice stays open.
    ///     The active keys are pressed just before the action and released just after (see
    ///     Modifiers.cs).
    ///
    /// Special actions: Copy, Cut, Paste, three buttons stuck to
    /// the top of the screen, outside the crown, as the modifiers are on the left edge (see
    /// ActionChoiceLayout). They send Ctrl + C, Ctrl + X and Ctrl + V to the active window;
    /// "Paste" first makes a left click on P, to put the insertion point where one was looking.
    ///
    /// The "Keyboard" action (of the crown, to place oneself in the icon configuration
    /// screen) opens the gaze keyboard, or closes it if it is already
    /// open.
    ///
    /// The "Scroll" action (slot 0) opens scroll mode, centered on P.
    ///
    /// The icon layout (crown, modifiers, utility buttons) is not written here;
    /// it is read from the settings (AppSettings.Layout) and set in the icon configuration
    /// screen (IconConfigMode), opened from the "Icons" tab of settings mode. A slot can be
    /// empty: it is drawn grayed and does not activate. The layout itself is computed by
    /// ActionChoiceLayout, shared with the configuration screen.
    /// </summary>
    internal sealed class ActionChoiceMode : Mode
    {
        /// <summary>The three phases of the mode (see the class comment).</summary>
        private enum Step
        {
            Capture,
            Choice,
            Execution
        }

        /// <summary>
        /// Wait before the photo, and before the click, in milliseconds: the time for Windows to
        /// really erase what was just hidden (the specification says "about 50 ms").
        /// </summary>
        private const double WaitMs = 60;

        /// <summary>
        /// Activation zone of ALL the buttons of this mode (crown, modifiers, back), relative to
        /// the drawn square: 30 % larger. With 120 px buttons 60 px
        /// apart (ActionChoiceLayout.Space), each zone overflows by 18 px on each side: 24 px
        /// remain between two neighbouring zones.
        /// </summary>
        public const double ZoneFactor = 1.3;

        // The aimed point P (cursor position), in screen pixels.
        private readonly double _px;
        private readonly double _py;

        private Step _step;
        private double _stepStartMs;

        /// <summary>Label of an empty slot, shown on hover.</summary>
        private static string EmptyLabel { get { return Texts.Get("actionChoice.emptySlot"); } }

        // The buttons shown, and the action of each one (same order; null = empty slot).
        private readonly List<GazeButton> _buttons = new List<GazeButton>();
        private readonly List<ChoiceAction?> _actions = new List<ChoiceAction?>();

        // The small circle showing the gaze: two ellipses (black line underneath, green on top),
        // added last on the black screen to be on top of everything. Smoothed position in
        // screen pixels; _gazeCirclePlaced = false as long as it has no position.
        private Ellipse _gazeCircleBackground;
        private Ellipse _gazeCircle;
        private readonly Smoother _smoothed = new Smoother();
        private bool _gazeCirclePlaced;

        // Decides which button is looked at and when it activates (class shared with the menu).
        private readonly ButtonAimer _aimer;

        // The chosen action, waiting for execution (Execution step).
        private ChoiceAction _chosenAction;

        public ActionChoiceMode(double x, double y)
        {
            _px = x;
            _py = y;
            _aimer = new ButtonAimer(_buttons);
        }

        public override string Name
        {
            get { return "Action choice"; }
        }

        public override void Enter(GazeState gaze)
        {
            _aimer.Reset();

            // 1. CAPTURE: hide everything GazeBar draws, and wait a bit before the photo.
            Manager.Layer.HideAll();
            _step = Step.Capture;
            _stepStartMs = gaze.NowMs;
        }

        public override void Update(GazeState gaze)
        {
            double now = gaze.NowMs;

            switch (_step)
            {
                case Step.Capture:
                    if (now - _stepStartMs >= WaitMs)
                    {
                        bool succeeded;
                        try
                        {
                            succeeded = BuildScreen();
                        }
                        catch (Exception ex)
                        {
                            App.Log("Action choice: error while building the screen: " + ex);
                            succeeded = false;
                        }

                        if (!succeeded)
                        {
                            // Photo impossible: do not stay stuck in front of a black screen.
                            Manager.ChangeMode(new MovementMode());
                        }
                    }
                    break;

                case Step.Choice:
                    UpdateChoice(gaze);
                    break;

                case Step.Execution:
                    if (now - _stepStartMs >= WaitMs)
                    {
                        Execute();
                    }
                    break;
            }
        }

        public override void Quit()
        {
            // The black screen is hidden and cleared (the photo is no longer needed).
            Manager.Layer.HideChoiceScreen();
            Manager.Layer.ChoiceScreen.Children.Clear();
            _buttons.Clear();
            _actions.Clear();
            _gazeCircle = null;
            _gazeCircleBackground = null;
        }

        // ───────────────────────── 2. The choice ─────────────────────────

        private void UpdateChoice(GazeState gaze)
        {
            AppSettings p = AppSettings.Current;

            // Which button is looked at? Has one of them just activated?
            // (An empty slot never activates: see ButtonAimer.)
            int active = _aimer.Update(gaze, p.ActivationDelay, p.ShowLabels);
            if (active >= 0)
            {
                Activate(_actions[active].Value);
                return;
            }

            // The small gaze circle (only when the gaze is on the screen).
            MoveGazeCircle(gaze, p);
        }

        /// <summary>A button has just been activated.</summary>
        private void Activate(ChoiceAction action)
        {
            App.LogDetail("Action choice: " + action);

            if (action == ChoiceAction.Back)
            {
                // Back without doing anything. Movement mode starts again from the gaze (the
                // circle was hidden during the choice, so there is no visible jump).
                Manager.ChangeMode(new MovementMode());
                return;
            }

            if (ActionCatalog.IsModifier(action))
            {
                // Ctrl, Shift or Alt: light it on or off, and stay in the choice.
                // The button is blocked until the gaze has left it: otherwise, by keeping looking
                // at it, it would be turned on and off endlessly.
                bool active = Modifiers.Toggle(ActionCatalog.ToModifier(action));
                // The same modifier can be placed on several slots: all its icons
                // light up or go off together.
                for (int i = 0; i < _actions.Count; i++)
                {
                    if (_actions[i] == action)
                    {
                        _buttons[i].SetLit(active);
                    }
                }
                _aimer.BlockUntilExit();
                return;
            }

            if (action == ChoiceAction.DragLeft || action == ChoiceAction.DragRight ||
                action == ChoiceAction.DragAndroid)
            {
                // The drag and drop starts from the aimed point P. The black screen will be
                // hidden by Quit, just before the next mode shows the real screen.
                MouseSide button = action == ChoiceAction.DragRight ? MouseSide.Right : MouseSide.Left;
                bool withPause = action == ChoiceAction.DragAndroid;
                Manager.ChangeMode(new DragTargetMode(new DragGesture(_px, _py, button, withPause)));
                return;
            }

            if (action == ChoiceAction.Scroll)
            {
                // Scrolling happens around P, on the real screen (the black screen will be
                // hidden by Quit, just before).
                Manager.ChangeMode(new ScrollMode(_px, _py));
                return;
            }

            if (action == ChoiceAction.Keyboard)
            {
                // The gaze keyboard: closed, it opens;
                // already open (hidden during this choice), it closes. It appears when back in
                // movement mode (GazeKeyboard.OnModeChange).
                // Layout file missing or incorrect: nothing opens, the error is written to the
                // log (and shown if going through the menu bar).
                if (Manager.Keyboard.IsOpen)
                {
                    Manager.Keyboard.Close();
                }
                else
                {
                    string message;
                    Manager.Keyboard.Open(out message);
                }
                Manager.ChangeMode(new MovementMode());
                return;
            }

            if (action == ChoiceAction.Magnifier)
            {
                // The magnifier takes the picture around P itself (the black screen will be
                // hidden by Quit, just before). "true": its "back" icon will return here.
                Manager.ChangeMode(new MagnifierMode(_px, _py, true));
                return;
            }

            // 3. EXECUTION: hide the black screen, and wait for it to disappear before the click.
            _chosenAction = action;
            Manager.Layer.HideChoiceScreen();
            _step = Step.Execution;
            _stepStartMs = Manager.Gaze.NowMs;
        }

        // ───────────────────────── 3. Execution ─────────────────────────

        private void Execute()
        {
            // The cursor is put back on P for safety (it may have been moved in the meantime).
            NativeMethods.SetCursorPos((int)Math.Round(_px), (int)Math.Round(_py));

            // The active modifiers are pressed just before the click...
            string modifiers = Modifiers.Description();
            Modifiers.PressActive();

            switch (_chosenAction)
            {
                case ChoiceAction.LeftClick:
                    MouseInjector.Click(MouseSide.Left);
                    break;
                case ChoiceAction.RightClick:
                    MouseInjector.Click(MouseSide.Right);
                    break;
                case ChoiceAction.DoubleClick:
                    MouseInjector.DoubleClick();
                    break;
                case ChoiceAction.Copy:
                    CtrlShortcut(NativeMethods.VK_C);
                    break;
                case ChoiceAction.Cut:
                    CtrlShortcut(NativeMethods.VK_X);
                    break;
                case ChoiceAction.Paste:
                    // Click first: the insertion point goes where one was looking.
                    MouseInjector.Click(MouseSide.Left);
                    CtrlShortcut(NativeMethods.VK_V);
                    break;
            }

            // ... and released just after. (Movement mode will then deactivate them.)
            Modifiers.ReleaseAll();

            App.LogDetail(_chosenAction + ", modifiers: " + modifiers);

            // Back to movement mode. A new fixation at the same place is ignored
            // until the gaze has left it, as after a direct click.
            Manager.ChangeMode(new MovementMode(_px, _py));
        }

        /// <summary>
        /// Sends Ctrl + key (copy, cut, paste) to the active window. No click for copy and cut:
        /// a click would clear the selection. If Ctrl was already pressed by the modifiers,
        /// PressDown does nothing; active Shift or Alt stay added (for example Ctrl + Shift + V
        /// pastes without formatting in many programs).
        /// </summary>
        private static void CtrlShortcut(ushort key)
        {
            KeyboardInjector.PressDown(NativeMethods.VK_CONTROL);
            KeyboardInjector.PressDown(key);
            KeyboardInjector.Release(key);
            KeyboardInjector.Release(NativeMethods.VK_CONTROL);
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

            // ── The photo: the square enclosing the circle, centered on P. Near an edge of the
            //    screen, the square is shifted to stay fully on the screen (P is then no longer
            //    in the center of the photo, nor is the cross).
            int side = (int)Math.Round(2 * p.CircleRadiusPx);
            side = Math.Min(side, (int)Math.Min(width, height));
            int ox = (int)Clamp(Math.Round(_px - side / 2.0), 0, width - side);
            int oy = (int)Clamp(Math.Round(_py - side / 2.0), 0, height - side);

            BitmapSource photo = ScreenCapture.Capture(ox, oy, side, side);
            if (photo == null)
            {
                return false;
            }

            layer.PrepareChoiceScreen();
            Canvas screen = layer.ChoiceScreen;

            // ── The layout (the same as the one of the icon configuration screen).
            ActionChoiceLayout placed = new ActionChoiceLayout(width, height, size, side);
            double zoom = ActionChoiceLayout.Zoom;

            // ── The enlarged photo, in the center of the screen.
            double enlargedSide = placed.EnlargedPhotoSide;
            double left = placed.CenterX - enlargedSide / 2;
            double top = placed.CenterY - enlargedSide / 2;

            Image image = new Image();
            image.Source = photo;
            image.Stretch = Stretch.Fill;
            // "Nearest neighbour": each pixel becomes a sharp 2 × 2 square, instead of being
            // softened (blurred). The enlarged text thus stays readable.
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Point corner = layer.ToWpf(left, top);
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

            // ── The cross, in place of the cursor in the enlarged photo.
            DrawCross(layer, screen, left + (_px - ox) * zoom, top + (_py - oy) * zoom);

            // ── The three groups of slots (crown, utility buttons at the top, modifiers on the
            //    left), with the layout chosen in the settings.
            //    An empty slot is drawn grayed. A modifier already active (for example Ctrl
            //    chosen before going through the magnifier) is drawn lit.
            foreach (IconGroup group in new[] { IconGroup.Crown, IconGroup.Utilities, IconGroup.Modifiers })
            {
                ChoiceAction?[] layout = p.Layout(group);
                Point[] slots = placed.Slots(group);
                for (int i = 0; i < layout.Length; i++)
                {
                    ChoiceAction? action = layout[i];
                    GazeButton button = AddButton(layer, screen, action, slots[i].X, slots[i].Y, size);
                    if (action.HasValue && ActionCatalog.IsModifier(action.Value))
                    {
                        button.SetLit(Modifiers.IsActive(ActionCatalog.ToModifier(action.Value)));
                    }
                }
            }

            // ── The "back" icon, on the right
            AddButton(layer, screen, ChoiceAction.Back, placed.Back.X, placed.Back.Y, size);

            // ── The small gaze circle, added LAST: it is thus drawn on top of the photo and the
            //    buttons. It will appear at the first on-screen measure.
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
            _step = Step.Choice;
            App.LogDetail("Action choice: photo of " + side + " × " + side + " px");
            return true;
        }

        /// <summary>
        /// Moves the small gaze circle: it follows the gaze smoothly ([Smoothing], see
        /// Smoother.cs), like the circle of movement mode. Its radius is half of [CircleRadius].
        /// Gaze off screen: it slides along the edge. Gaze lost: it stays where it is.
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
            double radius = layer.LengthToWpf(p.CircleRadiusPx / 2);
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

        /// <summary>
        /// Creates the button of an action (or an empty slot if action is null), places it on the
        /// black screen, and returns it.
        /// </summary>
        private GazeButton AddButton(OverlayWindow layer, Canvas screen, ChoiceAction? action,
                                           double x, double y, double size)
        {
            GazeButton button = action.HasValue ? ActionCatalog.CreateButton(action.Value) : GazeButton.Empty(EmptyLabel);
            button.ZoneFactor = ZoneFactor;
            button.Place(layer, screen, x, y, size);
            _buttons.Add(button);
            _actions.Add(action);
            return button;
        }

        /// <summary>
        /// Draws a cross centered on (x, y) in screen pixels: a thick black line underneath, a
        /// thin white line on top, so that it can be seen on every background.
        /// </summary>
        private static void DrawCross(OverlayWindow layer, Canvas screen, double x, double y)
        {
            Point center = layer.ToWpf(x, y);
            // Half length: 20 px of the 4K screen; thicknesses: units of the reference screen
            // (see ScreenMetrics.cs).
            double half = layer.LengthToWpf(ScreenMetrics.Px(20));
            double thick = ScreenMetrics.Units(5);
            double end = ScreenMetrics.Units(2);

            AddLine(screen, center.X - half, center.Y, center.X + half, center.Y, Brushes.Black, thick);
            AddLine(screen, center.X, center.Y - half, center.X, center.Y + half, Brushes.Black, thick);
            AddLine(screen, center.X - half, center.Y, center.X + half, center.Y, Brushes.White, end);
            AddLine(screen, center.X, center.Y - half, center.X, center.Y + half, Brushes.White, end);
        }

        private static void AddLine(Canvas screen, double x1, double y1, double x2, double y2,
                                         Brush color, double thickness)
        {
            Line stroke = new Line();
            stroke.X1 = x1;
            stroke.Y1 = y1;
            stroke.X2 = x2;
            stroke.Y2 = y2;
            stroke.Stroke = color;
            stroke.StrokeThickness = thickness;
            screen.Children.Add(stroke);
        }

        private static double Clamp(double val, double min, double max)
        {
            return Math.Max(min, Math.Min(max, val));
        }
    }
}
