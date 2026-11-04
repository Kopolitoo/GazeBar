using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GazeBar
{
    /// <summary>
    /// INTERFACE: the full-screen "overlay", click-through, always on top.
    ///
    /// Modes (the …Mode classes) do not draw on it directly: they call simple methods such
    /// as ShowCircle or HideCircle, in SCREEN PIXELS (the same units as the eyetracker gaze
    /// and the cursor). This window converts them to WPF units.
    ///
    /// Why a conversion? When Windows scales the display (125 %, 150 %…), WPF no longer
    /// counts in pixels but in "device-independent units" (1 unit = 1/96 inch).
    /// At 150 %, 100 real pixels are only 66.7 WPF units. Without conversion, the circle
    /// would be offset from the gaze, more and more away from the top-left corner.
    /// </summary>
    internal partial class OverlayWindow : Window
    {
        private readonly DispatcherTimer _foreground;

        /// <summary>
        /// Opacity of the green flash of the options icon when its delay is over (see
        /// ShowIcon): 0.6 = a light flash, the dark icon stays seen through it.
        /// </summary>
        private const double FlashMaxOpacity = 0.6;

        public OverlayWindow()
        {
            InitializeComponent();

            // Several windows can be "always on top" (the taskbar, the Diagnostic window,
            // other software…). Among them, the last one shown or activated wins. To keep the
            // overlay ABOVE EVERYTHING, it is put back at the top of the stack twice a second.
            //
            // Known limit (2026-10-29): the Windows Start menu, search and notification
            // centre live in a "band" reserved for the system, higher than every "always on
            // top" window. The overlay therefore goes below them despite this timer. Only a
            // program declared "uiAccess" (manifest), digitally signed and installed in
            // C:\Program Files can be shown above them.
            _foreground = new DispatcherTimer();
            _foreground.Interval = TimeSpan.FromMilliseconds(500);
            _foreground.Tick += (sender, e) => BringToFront();
            _foreground.Start();

            // Step 11: screen watch (resolution, scaling, text size). Windows notifies the
            // overlay with messages (see Hook), but it is also checked every two seconds: it
            // is not yet known whether Windows really sends a message when the text size
            // changes (to check in testing).
            _screenWatch = new DispatcherTimer();
            _screenWatch.Interval = TimeSpan.FromSeconds(2);
            _screenWatch.Tick += (sender, e) => CoverScreen();
            _screenWatch.Start();

            // After a Windows message, wait half a second before measuring: a resolution
            // change often sends several messages in a row, and the screen takes a moment to
            // settle. A single measure is enough.
            _deferredMeasure = new DispatcherTimer();
            _deferredMeasure.Interval = TimeSpan.FromMilliseconds(500);
            _deferredMeasure.Tick += (sender, e) =>
            {
                _deferredMeasure.Stop();
                CoverScreen();
            };
        }

        private readonly DispatcherTimer _screenWatch;
        private readonly DispatcherTimer _deferredMeasure;

        /// <summary>
        /// Requests a new screen measure in half a second. If a request is already pending,
        /// the countdown restarts from zero (Stop then Start).
        /// </summary>
        private void ScheduleMeasure()
        {
            _deferredMeasure.Stop();
            _deferredMeasure.Start();
        }

        /// <summary>
        /// The "hook" for Windows messages sent to the overlay (step 11). Only two messages,
        /// sent to every window, are kept:
        ///   WM_DISPLAYCHANGE: the screen resolution changed;
        ///   WM_SETTINGCHANGE: a Windows setting changed (maybe the text size).
        /// handled stays false: the message goes on its normal way.
        /// </summary>
        private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == NativeMethods.WM_DISPLAYCHANGE || message == NativeMethods.WM_SETTINGCHANGE)
            {
                ScheduleMeasure();
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Puts the overlay at the very top of the "always on top" windows, without moving
        /// it, resizing it or giving it focus.
        /// </summary>
        public void BringToFront()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;   // window not created yet
            }
            NativeMethods.SetWindowPos(
                handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        // ───────────────────────── Click-through window ─────────────────────────

        /// <summary>
        /// Called when Windows has created the "real" window (its handle, HWND).
        /// Adds the styles that make it click-through and non-activatable, then gives it the
        /// exact size of the screen.
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr handle = new WindowInteropHelper(this).Handle;

            // Added styles (the "|" combines the options):
            //  - TRANSPARENT: clicks and the wheel go through the window and reach the
            //    application below;
            //  - LAYERED: needed for TRANSPARENT to work (WPF already sets it);
            //  - NOACTIVATE: the window never takes focus;
            //  - TOOLWINDOW: it does not show in Alt+Tab.
            int style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(
                handle,
                NativeMethods.GWL_EXSTYLE,
                style
                | NativeMethods.WS_EX_TRANSPARENT
                | NativeMethods.WS_EX_LAYERED
                | NativeMethods.WS_EX_NOACTIVATE
                | NativeMethods.WS_EX_TOOLWINDOW);

            // Step 11: listen to Windows messages (see Hook).
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null)
            {
                source.AddHook(Hook);
            }

            CoverScreen();
        }

        /// <summary>
        /// Called by WPF if the Windows scaling changes while GazeBar runs (for example from
        /// 100 % to 125 % in the display settings). The size is recomputed at once, then a
        /// second time half a second later (step 11): the resolution often changes at the
        /// same time.
        /// </summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            CoverScreen();
            ScheduleMeasure();
        }

        /// <summary>
        /// Places the window over the whole primary screen.
        ///
        /// PrimaryScreenSize gives the screen size in real pixels (GazeBar is declared
        /// "DPI aware" in app.manifest). TransformFromDevice is the "real pixels → WPF units"
        /// conversion provided by WPF for this window.
        ///
        /// Step 11: the measure is also given to ScreenMetrics, which notifies the rest of
        /// the program if the resolution, scaling or text size changed. The window size is
        /// only rewritten (and logged) if it changed, because this method is now called
        /// every two seconds.
        /// </summary>
        private void CoverScreen()
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null || source.CompositionTarget == null)
            {
                return;
            }

            // The true resolution, even after a change (see NativeMethods.PrimaryScreenSize).
            double widthPixels;
            double heightPixels;
            NativeMethods.PrimaryScreenSize(out widthPixels, out heightPixels);

            if (widthPixels <= 0 || heightPixels <= 0)
            {
                return;   // screen changing: try again later
            }

            Matrix toWpf = source.CompositionTarget.TransformFromDevice;
            Point size = toWpf.Transform(new Point(widthPixels, heightPixels));

            // (A size not set yet is NaN, "not a number": it is set then.)
            bool sizeChanged = double.IsNaN(Width) || double.IsNaN(Height) || Left != 0 || Top != 0 ||
                                 Math.Abs(Width - size.X) > 0.5 || Math.Abs(Height - size.Y) > 0.5;
            if (sizeChanged)
            {
                Left = 0;
                Top = 0;
                Width = size.X;
                Height = size.Y;

                App.Log("Overlay: screen " + widthPixels + " × " + heightPixels + " pixels = " +
                        size.X.ToString("F0") + " × " + size.Y.ToString("F0") + " WPF units (scale " +
                        (widthPixels / size.X * 100).ToString("F0") + " %)");
            }

            // TransformToDevice: WPF units → real pixels. M22 = vertical factor (1.25 at 125 %).
            ScreenMetrics.Measure(source.CompositionTarget.TransformToDevice.M22);
        }

        // ───────────────────────── Drawing ─────────────────────────

        // Colours of the circle's top stroke: discreet in movement, lighter in fixed gaze.
        // "Freeze" locks the brush: WPF then draws it faster.
        private static readonly SolidColorBrush MovementBrush = Freeze(new SolidColorBrush(Colors.DarkGray));
        private static readonly SolidColorBrush FixedBrush = Freeze(new SolidColorBrush(Colors.LightGray));

        private static SolidColorBrush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Shows the movement mode circle (discreet) centred on (x, y).
        /// Everything is in SCREEN PIXELS, like the gaze and the cursor.
        /// </summary>
        public void ShowCircle(double xPixels, double yPixels, double radiusPixels)
        {
            ShowCircle(xPixels, yPixels, radiusPixels, false);
        }

        /// <summary>
        /// Shows the circle centred on (x, y), with the given radius.
        /// fixedValue = true: fixed gaze mode circle (lighter, more opaque stroke).
        /// </summary>
        public void ShowCircle(double xPixels, double yPixels, double radiusPixels, bool fixedValue)
        {
            ShowCircle(xPixels, yPixels, radiusPixels, fixedValue, 1.0);
        }

        /// <summary>
        /// Same as above, with reduced opacity: damping = 1 → usual opacity, 0.5 → twice as
        /// transparent (scroll mode, request of 2026-10-28).
        /// </summary>
        public void ShowCircle(double xPixels, double yPixels, double radiusPixels, bool fixedValue,
                                   double damping)
        {
            CircleX = xPixels;
            CircleY = yPixels;
            CircleShown = true;

            Circle.Stroke = fixedValue ? FixedBrush : MovementBrush;
            // Thicknesses in reference-screen units, converted (step 11, see ScreenMetrics.cs).
            Circle.StrokeThickness = ScreenMetrics.Units(fixedValue ? 1.5 : 1);
            Circle.Opacity = (fixedValue ? 0.6 : 0.3) * damping;
            CircleBackground.StrokeThickness = ScreenMetrics.Units(fixedValue ? 2.5 : 2);
            CircleBackground.Opacity = (fixedValue ? 0.7 : 0.5) * damping;

            // Not shown on screen yet: nothing to draw (PointFromScreen would fail).
            if (PresentationSource.FromVisual(this) == null)
            {
                return;
            }

            // PointFromScreen converts a screen point (real pixels) to a position in this
            // window (WPF units). It takes scaling into account.
            Point center = PointFromScreen(new Point(xPixels, yPixels));
            double radius = PixelsToWpf(radiusPixels);

            PlaceEllipse(CircleBackground, center, PixelsToWpf(radiusPixels + ScreenMetrics.Px(2)));
            PlaceEllipse(Circle, center, radius);
        }

        /// <summary>Centre of the circle as drawn, in screen pixels (valid if CircleShown).</summary>
        public double CircleX { get; private set; }

        public double CircleY { get; private set; }

        /// <summary>
        /// True if the circle is visible. A starting mode uses it to take the circle where
        /// it is, instead of making it jump elsewhere.
        /// </summary>
        public bool CircleShown { get; private set; }

        /// <summary>Hides the circle.</summary>
        public void HideCircle()
        {
            CircleShown = false;
            CircleBackground.Visibility = Visibility.Collapsed;
            Circle.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Shows the progress ring on the edge of a circle centred on (x, y).
        /// progress goes from 0 (nothing) to 1 (full turn).
        /// </summary>
        public void ShowRing(double xPixels, double yPixels, double radiusPixels, double progress)
        {
            Ring.StrokeThickness = ScreenMetrics.Units(6);
            DrawArc(Ring, xPixels, yPixels, radiusPixels, progress);
        }

        public void HideRing()
        {
            Ring.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Shows the options icon: a square of side [size] centred on (x, y), in screen
        /// pixels. progress (0 to 1) lights up the icon with a light green flash; at 0, no
        /// flash. A ring around the icon was used before; with a very short delay (0.1 s) it
        /// had no time to be seen, the flash is easier to notice (user request of 2026-11-02).
        /// showLabel: shows the "Options" text below the icon.
        /// </summary>
        public void ShowIcon(double xPixels, double yPixels, double sizePixels,
                                  double progress, bool showLabel)
        {
            if (PresentationSource.FromVisual(this) == null)
            {
                return;
            }

            Point center = PointFromScreen(new Point(xPixels, yPixels));
            double side = PixelsToWpf(sizePixels);

            OptionsIcon.Width = side;
            OptionsIcon.Height = side;
            OptionsIcon.BorderThickness = new Thickness(ScreenMetrics.Units(2));
            OptionsIcon.CornerRadius = new CornerRadius(ScreenMetrics.Units(12));
            OptionsIconSymbol.FontSize = side * 0.5;
            Canvas.SetLeft(OptionsIcon, center.X - side / 2);
            Canvas.SetTop(OptionsIcon, center.Y - side / 2);
            OptionsIcon.Visibility = Visibility.Visible;

            // The flash: its corners follow the inside of the icon's border; its opacity
            // grows with the progress, up to FlashMaxOpacity (a light flash, the white symbol
            // stays readable on it).
            double inner = Math.Max(0, ScreenMetrics.Units(12) - ScreenMetrics.Units(2));
            OptionsIconFlash.CornerRadius = new CornerRadius(inner);
            OptionsIconFlash.Opacity = FlashMaxOpacity * Math.Max(0, Math.Min(1, progress));

            if (showLabel)
            {
                // The label follows the Windows text size (step 11, see ScreenMetrics.Font) and
                // the interface language.
                OptionsLabelText.Text = Texts.Get("overlay.options");
                OptionsLabelText.FontSize = ScreenMetrics.Font(20);
                OptionsLabel.CornerRadius = new CornerRadius(ScreenMetrics.Units(6));
                OptionsLabel.Padding = new Thickness(ScreenMetrics.Units(8), ScreenMetrics.Units(2), ScreenMetrics.Units(8), ScreenMetrics.Units(2));
                // Measure the label to centre it below the icon.
                OptionsLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(OptionsLabel, center.X - OptionsLabel.DesiredSize.Width / 2);
                Canvas.SetTop(OptionsLabel, center.Y + side / 2 + ScreenMetrics.Units(12));
                OptionsLabel.Visibility = Visibility.Visible;
            }
            else
            {
                OptionsLabel.Visibility = Visibility.Collapsed;
            }
        }

        public void HideIcon()
        {
            OptionsIcon.Visibility = Visibility.Collapsed;
            OptionsIconFlash.Opacity = 0;
            OptionsLabel.Visibility = Visibility.Collapsed;
        }

        /// <summary>Hides everything drawn on the overlay (black screen included).</summary>
        public void HideAll()
        {
            HideCircle();
            HideRing();
            HideIcon();
            HideChoiceScreen();
            HideMenuBar();
            HideDragSurface();
            HideKeyboardSurface();
        }

        // ───────────────────────── Action choice black screen (step 3) ─────────────────────────

        /// <summary>
        /// True when the overlay is really shown on screen. Before that, no coordinate
        /// conversion is possible (PointFromScreen would fail).
        /// </summary>
        public bool Ready
        {
            get { return PresentationSource.FromVisual(this) != null; }
        }

        /// <summary>
        /// Clears the black screen's previous content and gives it the overlay's size.
        /// It stays hidden: the mode fills it, then calls ShowChoiceScreen.
        /// </summary>
        public void PrepareChoiceScreen()
        {
            ChoiceScreen.Children.Clear();
            ChoiceScreen.Width = ActualWidth;
            ChoiceScreen.Height = ActualHeight;
            Canvas.SetLeft(ChoiceScreen, 0);
            Canvas.SetTop(ChoiceScreen, 0);
        }

        public void ShowChoiceScreen()
        {
            ChoiceScreen.Visibility = Visibility.Visible;
        }

        /// <summary>Hides the black screen (its content is kept until the next PrepareChoiceScreen).</summary>
        public void HideChoiceScreen()
        {
            ChoiceScreen.Visibility = Visibility.Collapsed;
        }

        // ───────────────────────── Menu bar (step 4) ─────────────────────────

        /// <summary>
        /// Clears the menu bar surface and gives it the overlay's size.
        /// It stays hidden: menu mode fills it, then calls ShowMenuBar.
        /// </summary>
        public void PrepareMenuBar()
        {
            MenuBar.Children.Clear();
            MenuBar.Width = ActualWidth;
            MenuBar.Height = ActualHeight;
            Canvas.SetLeft(MenuBar, 0);
            Canvas.SetTop(MenuBar, 0);
        }

        /// <summary>Duration of the menu bar appear animation, in milliseconds.</summary>
        private const double MenuAppearDurationMs = 250;

        /// <summary>
        /// Distance travelled by the bar while appearing, in WPF units: it rises from a
        /// little below its final place.
        /// </summary>
        private const double MenuAppearRise = 80;

        /// <summary>
        /// Shows the menu bar.
        /// animate = true: small appear animation (user request of 2026-10-28): the bar
        /// rises [MenuAppearRise] units to its place, going from transparent to opaque, in
        /// [MenuAppearDurationMs] ms. It slows down at the end ("EaseOut"), which looks more
        /// natural than a constant speed.
        /// These are WPF's built-in animations: no extra dependency.
        /// Button activation zones are at their final place at once: during this quarter
        /// second, only the drawing moves (the activation delay is much longer).
        /// </summary>
        public void ShowMenuBar(bool animate = false)
        {
            // A closing animation still running is cancelled: its end must not hide the bar
            // being shown.
            _menuClosing = null;
            MenuBar.Visibility = Visibility.Visible;

            // A TranslateTransform shifts the surface's drawing without changing its position.
            TranslateTransform offset = MenuBar.RenderTransform as TranslateTransform;
            if (offset == null)
            {
                offset = new TranslateTransform();
                MenuBar.RenderTransform = offset;
            }

            if (!animate)
            {
                // No animation (for example the bar rebuilt for "Oui / Non"): if an appear
                // animation is still running, let it finish.
                return;
            }

            Duration duration = new Duration(TimeSpan.FromMilliseconds(MenuAppearDurationMs));
            CubicEase slowed = new CubicEase();
            slowed.EasingMode = EasingMode.EaseOut;

            DoubleAnimation rise = new DoubleAnimation(ScreenMetrics.Units(MenuAppearRise), 0, duration);
            rise.EasingFunction = slowed;
            DoubleAnimation fade = new DoubleAnimation(0, 1, duration);
            fade.EasingFunction = slowed;

            // BeginAnimation replaces an animation already running on the same property.
            offset.BeginAnimation(TranslateTransform.YProperty, rise);
            MenuBar.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>Duration of the menu bar closing animation, in milliseconds.</summary>
        private const double MenuCloseDurationMs = 200;

        // The closing animation in progress, or null. Used to tell, when an animation ends,
        // whether it is really the last requested closing (and not one cancelled because the
        // menu was reopened in the meantime).
        private DoubleAnimation _menuClosing;

        /// <summary>
        /// Hides the menu bar (its content is kept until the next PrepareMenuBar).
        /// animate = true: small closing animation (user request of 2026-10-28), the reverse
        /// of appearing: the bar goes down [MenuAppearRise] units while becoming transparent,
        /// in [MenuCloseDurationMs] ms, speeding up ("EaseIn").
        /// Buttons already ignore the gaze: only the drawing fades.
        /// animate = false (default): the bar disappears at once. This is the case of
        /// HideAll, called for example before a screenshot: nothing must wait there.
        /// </summary>
        public void HideMenuBar(bool animate = false)
        {
            if (!animate || MenuBar.Visibility != Visibility.Visible)
            {
                _menuClosing = null;
                MenuBar.Visibility = Visibility.Collapsed;
                RestoreMenuBar();
                return;
            }

            TranslateTransform offset = MenuBar.RenderTransform as TranslateTransform;
            if (offset == null)
            {
                offset = new TranslateTransform();
                MenuBar.RenderTransform = offset;
            }

            Duration duration = new Duration(TimeSpan.FromMilliseconds(MenuCloseDurationMs));
            CubicEase accelerated = new CubicEase();
            accelerated.EasingMode = EasingMode.EaseIn;

            // "null" as start value: the animation starts from the current value (useful if
            // the appear animation was not finished).
            DoubleAnimation descent = new DoubleAnimation();
            descent.To = ScreenMetrics.Units(MenuAppearRise);
            descent.Duration = duration;
            descent.EasingFunction = accelerated;
            DoubleAnimation fade = new DoubleAnimation();
            fade.To = 0;
            fade.Duration = duration;
            fade.EasingFunction = accelerated;

            // At the end of the fade: the bar is hidden for good, unless the menu was
            // reopened in the meantime (then _menuClosing changed).
            _menuClosing = fade;
            fade.Completed += (s, e) =>
            {
                if (_menuClosing == fade)
                {
                    _menuClosing = null;
                    MenuBar.Visibility = Visibility.Collapsed;
                    RestoreMenuBar();
                }
            };

            offset.BeginAnimation(TranslateTransform.YProperty, descent);
            MenuBar.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>
        /// Stops the bar's animations and puts it back in place, opaque. Otherwise, the end
        /// of an animation "holds" its last value (transparent, shifted bar).
        /// BeginAnimation(…, null) removes the animation: the property gets its true value back.
        /// </summary>
        private void RestoreMenuBar()
        {
            MenuBar.BeginAnimation(OpacityProperty, null);
            TranslateTransform offset = MenuBar.RenderTransform as TranslateTransform;
            if (offset != null)
            {
                offset.BeginAnimation(TranslateTransform.YProperty, null);
                offset.Y = 0;
            }
            MenuBar.Opacity = 1;
        }

        // ───────────────────────── Drag-and-drop surface (step 6) ─────────────────────────

        /// <summary>
        /// Clears the drag-and-drop surface and gives it the overlay's size.
        /// It stays hidden: the mode fills it, then calls ShowDragSurface.
        /// </summary>
        public void PrepareDragSurface()
        {
            DragSurface.Children.Clear();
            DragSurface.Width = ActualWidth;
            DragSurface.Height = ActualHeight;
            Canvas.SetLeft(DragSurface, 0);
            Canvas.SetTop(DragSurface, 0);
        }

        public void ShowDragSurface()
        {
            DragSurface.Visibility = Visibility.Visible;
        }

        /// <summary>Hides the drag-and-drop surface (its content is kept until the next PrepareDragSurface).</summary>
        public void HideDragSurface()
        {
            DragSurface.Visibility = Visibility.Collapsed;
        }

        // ───────────────────────── Gaze keyboard surface (step 12) ─────────────────────────

        /// <summary>
        /// Clears the keyboard surface and gives it the overlay's size.
        /// It stays hidden: GazeKeyboard fills it, then calls ShowKeyboardSurface.
        /// </summary>
        public void PrepareKeyboardSurface()
        {
            KeyboardSurface.Children.Clear();
            KeyboardSurface.Width = ActualWidth;
            KeyboardSurface.Height = ActualHeight;
            Canvas.SetLeft(KeyboardSurface, 0);
            Canvas.SetTop(KeyboardSurface, 0);
        }

        public void ShowKeyboardSurface()
        {
            KeyboardSurface.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Slides the keyboard to its new place (user request of 2026-11-02): it has just been
        /// redrawn at its final place, it is first shifted by "fromPixels" (screen pixels,
        /// positive = lower), then this shift goes back to zero. Moving up: at the menu bar's
        /// appear speed, slowing down at the end. Moving down: at its closing speed, speeding
        /// up. As for the bar, a TranslateTransform shifts the drawing without changing the
        /// computed positions.
        /// </summary>
        public void AnimateKeyboardSurface(double fromPixels)
        {
            TranslateTransform offset = KeyboardSurface.RenderTransform as TranslateTransform;
            if (offset == null)
            {
                offset = new TranslateTransform();
                KeyboardSurface.RenderTransform = offset;
            }

            bool rising = fromPixels > 0;
            Duration duration = new Duration(TimeSpan.FromMilliseconds(rising ? MenuAppearDurationMs : MenuCloseDurationMs));
            CubicEase curve = new CubicEase();
            curve.EasingMode = rising ? EasingMode.EaseOut : EasingMode.EaseIn;

            DoubleAnimation drag = new DoubleAnimation(LengthToWpf(fromPixels), 0, duration);
            drag.EasingFunction = curve;
            // BeginAnimation replaces an animation already running (bar reopened very fast).
            offset.BeginAnimation(TranslateTransform.YProperty, drag);
        }

        /// <summary>Hides the keyboard (its content is kept until the next PrepareKeyboardSurface).</summary>
        public void HideKeyboardSurface()
        {
            KeyboardSurface.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a screen point (real pixels) to a position in the overlay (WPF units).
        /// Use only if Ready is true.
        /// </summary>
        public Point ToWpf(double xPixels, double yPixels)
        {
            return PointFromScreen(new Point(xPixels, yPixels));
        }

        /// <summary>Converts a length in real pixels to a length in WPF units.</summary>
        public double LengthToWpf(double pixels)
        {
            return PixelsToWpf(pixels);
        }

        /// <summary>
        /// Draws an arc in "path": it starts at the top and turns clockwise, over a
        /// "progress" fraction of the turn.
        ///
        /// In WPF, an arc is described by a start point and an ArcSegment (end point, radii,
        /// sweep direction, and "large arc" when beyond half a turn).
        /// A full turn cannot be written as a single arc (start = end): a whole circle is
        /// drawn instead.
        /// </summary>
        private void DrawArc(Path path, double xPixels, double yPixels, double radiusPixels, double progress)
        {
            if (progress <= 0 || PresentationSource.FromVisual(this) == null)
            {
                path.Visibility = Visibility.Collapsed;
                return;
            }

            Point center = PointFromScreen(new Point(xPixels, yPixels));
            double radius = PixelsToWpf(radiusPixels);

            path.Data = ArcShape(center, radius, progress);
            path.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// The shape of an arc that starts at the top and turns clockwise, over a "progress"
        /// fraction of the turn (centre and radius in WPF units).
        /// "static" and "internal": the action choice buttons (GazeButton) also use it for
        /// their progress ring.
        /// </summary>
        internal static Geometry ArcShape(Point center, double radius, double progress)
        {
            Geometry shape;
            if (progress >= 0.999)
            {
                shape = new EllipseGeometry(center, radius, radius);
            }
            else
            {
                double angle = progress * 2 * Math.PI;   // in radians, 0 = top
                Point startPoint = new Point(center.X, center.Y - radius);
                Point arrival = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));

                PathFigure figure = new PathFigure();
                figure.StartPoint = startPoint;
                figure.IsClosed = false;
                figure.IsFilled = false;
                figure.Segments.Add(new ArcSegment(
                    arrival, new Size(radius, radius), 0, angle > Math.PI, SweepDirection.Clockwise, true));

                PathGeometry paths = new PathGeometry();
                paths.Figures.Add(figure);
                shape = paths;
            }

            shape.Freeze();
            return shape;
        }

        /// <summary>Places an ellipse: its top-left corner is at (centre − radius).</summary>
        private static void PlaceEllipse(Ellipse ellipse, Point center, double radius)
        {
            ellipse.Width = radius * 2;
            ellipse.Height = radius * 2;
            Canvas.SetLeft(ellipse, center.X - radius);
            Canvas.SetTop(ellipse, center.Y - radius);
            ellipse.Visibility = Visibility.Visible;
        }

        /// <summary>Converts a length in real pixels to a length in WPF units.</summary>
        private double PixelsToWpf(double pixels)
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null || source.CompositionTarget == null)
            {
                return pixels;
            }
            // M11 is the horizontal conversion factor (0.8 at 125 %, 0.667 at 150 %…).
            return pixels * source.CompositionTarget.TransformFromDevice.M11;
        }
    }
}
