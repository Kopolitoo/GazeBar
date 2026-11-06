using System;
using Microsoft.Win32;

namespace Prunel
{
    /// <summary>
    /// MODEL: the screen measurements, and the conversion of Prunel's sizes to pixels
    /// ("mesures-ecran").
    ///
    /// THE PROBLEM: sizes written in pixels (190 px circle, 120 px
    /// icons...), tuned on the user's 4K screen (3840 × 2160). On a 1920 × 1080 screen, the
    /// same 190 px circle looks twice as big.
    ///
    /// THE SOLUTION (specification): sizes are expressed as a PERCENTAGE OF THE
    /// SCREEN HEIGHT. An 8.8 % circle is 190 px on a 2160 px high screen, and 95 px on a
    /// 1080 px screen: on the same physical screen, it keeps the same real size. This is what
    /// matters for the gaze, whose precision does not depend on the resolution chosen in
    /// Windows.
    ///
    /// Three kinds of values go through here:
    ///   1. the SETTINGS of settings.json, in % of the height (CircleRadius, IconSize...):
    ///      Percent and InterfacePercent methods;
    ///   2. the CONSTANTS written in pixels in the code (for example the 60 px space between
    ///      the buttons of the action choice). They were tuned on the user's 4K screen: they
    ///      are kept as they are, as "pixels of a 2160 px high screen", and converted: Px and
    ///      GazePx methods;
    ///   3. the CONSTANTS written in WPF units (line thickness, text size...). On the 4K
    ///      screen at 200 %, the screen is 1080 WPF units high: they are kept as "units of a
    ///      1080-unit high screen": Units and Font methods.
    ///
    /// Two Windows settings are added:
    ///   - the SCALING (100 %, 125 %, 200 %...): it only changes the conversion between pixels
    ///     and WPF units, the real size of Prunel's elements stays the same;
    ///   - the TEXT SIZE (Windows Settings › Accessibility › Text size, from 100 to 225 %): it
    ///     enlarges the texts of the overlay (labels, menu bar...), Font method.
    /// And one Prunel setting: [InterfaceSize] (75, 100, 125 or 150 %), which enlarges or
    /// shrinks everything drawn, but not the gaze settings (fixation radius, maximum speed,
    /// anti-jitter): the eye's jitter does not depend on one's taste for large icons.
    ///
    /// CHANGES WHILE PRUNEL RUNS: the overlay (OverlayWindow) calls Measure when Windows
    /// announces a display change, and also every two seconds as a precaution. If something
    /// changed, the Change event is raised: the mode manager redraws what is needed (see
    /// ModeManager.OnScreenChanged).
    ///
    /// "static": there is only one primary screen, these values are shared by the whole program.
    /// </summary>
    internal static class ScreenMetrics
    {
        /// <summary>Height, in pixels, of the screen on which the pixel constants were tuned (4K).</summary>
        public const double ReferenceHeightPixels = 2160;

        /// <summary>Height, in WPF units, of that same screen (4K at 200 %).</summary>
        public const double ReferenceHeightUnits = 1080;

        /// <summary>Width of the primary screen, in real pixels.</summary>
        public static double WidthPixels { get; private set; } = 1920;

        /// <summary>Height of the primary screen, in real pixels.</summary>
        public static double HeightPixels { get; private set; } = 1080;

        /// <summary>Windows scaling: 1 = 100 %, 1.25 = 125 %, 2 = 200 %.</summary>
        public static double WindowsScale { get; private set; } = 1;

        /// <summary>Windows text size (Accessibility): 1 = 100 %, 2.25 = 225 %.</summary>
        public static double WindowsText { get; private set; } = 1;

        /// <summary>
        /// The Windows scaling WHEN Prunel STARTED (1 = 100 %). Windows "freezes" it for the
        /// program: it no longer changes afterwards, even if the scaling is changed in the
        /// Windows settings. Read with GetDpiForSystem.
        /// </summary>
        public static double StartupScale { get; } = ReadStartupScale();

        /// <summary>
        /// Correction of the eyetracker gaze.
        ///
        /// The user's test: Prunel started in 1920 × 1080 at 100 %, then the screen switched
        /// to 3840 × 2160 at 150 %. The log shows that the gaze received from the eyetracker no
        /// longer went beyond about X 1830 (instead of 3840); back at 100 %, it reached 3802
        /// again. So the eyetracker gives the gaze in the units of the STARTUP scale: it is
        /// reduced by 150 ÷ 100 = 1.5. We multiply it by this ratio to get the real pixels back.
        /// When the scale has not changed since startup (the usual case), the ratio is 1 and
        /// nothing changes (that is why everything worked in 4K at 200 % from the start).
        /// </summary>
        public static double GazeFactor
        {
            // Before the first measurement (at the very beginning), no correction.
            get { return _measure ? WindowsScale / StartupScale : 1; }
        }

        /// <summary>Screen height in WPF units (1080 for a 4K screen at 200 % or 1080p at 100 %).</summary>
        public static double HeightUnits
        {
            get { return HeightPixels / WindowsScale; }
        }

        /// <summary>Raised when the resolution, the scaling or the text size changed.</summary>
        public static event Action Change;

        // True after the first measurement (at startup, nothing has "changed": no event).
        private static bool _measure;

        /// <summary>[InterfaceSize] as a factor: 100 % → 1, 125 % → 1.25.</summary>
        private static double InterfaceFactor
        {
            get { return AppSettings.Current.InterfaceSize / 100.0; }
        }

        // ───────────────────────── Conversions ─────────────────────────

        /// <summary>
        /// A percentage of the screen height, in pixels. For the GAZE settings (fixation
        /// radius, maximum speed, anti-jitter): [InterfaceSize] does not apply.
        /// Example: 1.4 % on a 1080 px screen → 15 px.
        /// </summary>
        public static double Percent(double percent)
        {
            return percent / 100.0 * HeightPixels;
        }

        /// <summary>
        /// A percentage of the screen height, in pixels, multiplied by [InterfaceSize].
        /// For what is DRAWN (circle radius, icon size).
        /// Example: 8.8 % on a 1080 px screen, interface at 100 % → 95 px.
        /// </summary>
        public static double InterfacePercent(double percent)
        {
            return Percent(percent) * InterfaceFactor;
        }

        /// <summary>
        /// A code constant written in pixels of the 4K screen (2160 px high), converted to
        /// pixels of the current screen, multiplied by [InterfaceSize]. For what is drawn.
        /// Example: 60 px on the 4K screen → 30 px on a 1080 px screen.
        /// </summary>
        public static double Px(double pixels4K)
        {
            return GazePx(pixels4K) * InterfaceFactor;
        }

        /// <summary>
        /// Like Px, but without [InterfaceSize]: for a GAZE distance (for example the off-screen
        /// margin of scroll mode).
        /// </summary>
        public static double GazePx(double pixels4K)
        {
            return pixels4K * HeightPixels / ReferenceHeightPixels;
        }

        /// <summary>
        /// A code constant written in WPF units of the 4K screen at 200 % (1080 units high),
        /// converted to WPF units of the current screen, multiplied by [InterfaceSize].
        /// For line thicknesses, rounded corners, text margins...
        /// On a 1080p screen at 100 % as on a 4K screen at 200 %, it does not change.
        /// </summary>
        public static double Units(double units)
        {
            return units * HeightUnits / ReferenceHeightUnits * InterfaceFactor;
        }

        /// <summary>
        /// A font size, like Units, additionally multiplied by the Windows text size. For the
        /// texts of the overlay (hover labels, menu bar...).
        /// </summary>
        public static double Font(double units)
        {
            return Units(units) * WindowsText;
        }

        // ───────────────────────── Measurement ─────────────────────────

        /// <summary>
        /// Reads the size of the primary screen and the Windows text size.
        /// scale: the Windows scaling, given by the overlay (1 = 100 %).
        /// The first time, raises nothing; afterwards, raises Change if a value changed.
        /// Returns true if something changed.
        /// </summary>
        public static bool Measure(double scale)
        {
            // The real resolution (GetSystemMetrics does not always see the change: see
            // NativeMethods.PrimaryScreenSize).
            double width;
            double height;
            NativeMethods.PrimaryScreenSize(out width, out height);
            if (width <= 0 || height <= 0)
            {
                return false;   // cannot measure for now (screen changing)
            }
            if (double.IsNaN(scale) || scale <= 0)
            {
                scale = WindowsScale;
            }
            double text = ReadTextSize();

            bool change = width != WidthPixels || height != HeightPixels ||
                          Math.Abs(scale - WindowsScale) > 0.001 ||
                          Math.Abs(text - WindowsText) > 0.001;
            bool firstOne = !_measure;
            _measure = true;
            if (!change && !firstOne)
            {
                return false;
            }

            WidthPixels = width;
            HeightPixels = height;
            WindowsScale = scale;
            WindowsText = text;
            App.Log("Screen: " + Description() + (firstOne ? "" : " (change detected)") +
                    "; startup scale " + (StartupScale * 100).ToString("F0") +
                    " %, gaze × " + GazeFactor.ToString("0.###"));

            if (firstOne)
            {
                return false;
            }
            Action subscribers = Change;
            if (subscribers != null)
            {
                subscribers();
            }
            return true;
        }

        /// <summary>Readable summary, for the log and the "Display" tab: "1920 × 1080 px, scale 100 %, text 100 %".</summary>
        public static string Description()
        {
            return Texts.Format("screen.description", WidthPixels.ToString("F0"), HeightPixels.ToString("F0"),
                                (WindowsScale * 100).ToString("F0"), (WindowsText * 100).ToString("F0"));
        }

        /// <summary>The scaling frozen at startup (see StartupScale). 1 if it cannot be read.</summary>
        private static double ReadStartupScale()
        {
            try
            {
                uint dpi = NativeMethods.GetDpiForSystem();
                if (dpi > 0)
                {
                    return dpi / 96.0;   // 96 dots per inch = 100 %
                }
            }
            catch (EntryPointNotFoundException)
            {
                // Windows 10 too old (before version 1607): no GetDpiForSystem.
            }
            return 1;
        }

        /// <summary>
        /// The Windows text size (Accessibility › Text size). Windows stores it in the
        /// registry, under HKEY_CURRENT_USER\Software\Microsoft\Accessibility, value
        /// "TextScaleFactor", from 100 to 225 (in %). Missing = never changed = 100 %.
        /// WPF does not apply it by itself: Prunel reads it (Font method).
        /// </summary>
        private static double ReadTextSize()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility"))
                {
                    object val = key == null ? null : key.GetValue("TextScaleFactor");
                    if (val is int)
                    {
                        int percent = (int)val;
                        return Math.Max(100, Math.Min(225, percent)) / 100.0;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Screen: unreadable text size (" + ex.Message + "), 100 % used");
            }
            return 1;
        }
    }
}
