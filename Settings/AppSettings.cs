using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace GazeBar
{
    /// <summary>
    /// MODEL: all of GazeBar's settings, in one place.
    ///
    /// They replace the constants that were written at the top of the first files
    /// (GazeMouse.cs and HoldSession.cs, removed in step 6). They are read at startup and
    /// saved in the file:
    ///     %APPDATA%\GazeBar\settings.json
    /// (for example C:\Users\Name\AppData\Roaming\GazeBar\settings.json).
    ///
    /// JSON is readable text, which can be opened with Notepad. Beware: in this file,
    /// decimal numbers are written with a POINT (0.3), not a comma.
    ///
    /// JSON is read and written with DataContractJsonSerializer, provided with
    /// .NET Framework 4.8: no extra library is needed.
    /// The principle: the class is marked with [DataContract] and each setting to save with
    /// [DataMember]. The serializer does the rest. The JSON keys are the property names.
    ///
    /// Usage in code: AppSettings.Current.Smoothing, AppSettings.Current.CircleRadius, etc.
    /// </summary>
    [DataContract(Name = "Settings", Namespace = "")]
    internal sealed class AppSettings
    {
        // ───────────────────────── Global access ─────────────────────────

        /// <summary>
        /// The settings in use, shared by the whole program.
        /// "static": there is only one copy. Replaced at startup by Initialize(), which
        /// reads the file.
        /// </summary>
        public static AppSettings Current { get; private set; } = new AppSettings();

        /// <summary>
        /// Current version of the settings file.
        ///   1: step 1;
        ///   2: step 2 — DirectClick and MouseTracking become "no" by default (user's choice of
        ///      2026-10-22). A version 1 file is updated once (see Refresh);
        ///   3: step 6 — new drag and drop: AndroidPauseDuration and DragDuration replace the
        ///      former long-press duration, drop delay, minimum drag distance and maximum
        ///      press duration, removed (user rule: a setting no longer used is removed);
        ///   4: step 9 — GameSmoothing goes from 0.7 to 0.16 by default (user test of
        ///      2026-10-28: at 0.7, the cursor jerked in direct mouse);
        ///   5: MaxScrollSpeed goes from 10 to 15 notches/s by default (maximum scroll speed
        ///      raised by half, user request of 2026-10-28);
        ///   6: step 10 — action choice icon layout (CrownIcons, ModifierIcons, UtilityIcons).
        ///      Nothing to fix: an older file lacks these lists, so they keep the default
        ///      layout;
        ///   7: step 11 — sizes go from pixels to PERCENT OF THE SCREEN HEIGHT
        ///      (CursorDeadZone, FixationRadius, MaxFixationSpeed, CircleRadius, IconSize), and
        ///      new setting InterfaceSize. The old values, tuned on the user's 4K screen
        ///      (2160 px high), are converted once;
        ///   8: step 12 — gaze keyboard: KeyDelay, KeyRepeat, AutoCapitalize and
        ///      KeyboardHeight. Nothing to fix: missing from an older file, they keep their
        ///      default value;
        ///   9: step 12 (user remarks of 2026-11-01) — KeyboardSize and KeyboardAtTop. Nothing
        ///      to fix: missing from an older file, they keep their default value;
        ///  10: step 14 ("clavier-options") — KeyboardLayout and InputBox. Missing from an
        ///      older file: InputBox keeps its default value; KeyboardLayout gets "fr-azerty"
        ///      (since step clavier-anglais the default is "en-qwerty", for new installations);
        ///  11: MaxScrollSpeed goes from 15 to 5 notches/s by default (user request of
        ///      2026-11-02, after the switch to fine scrolling), and new setting
        ///      ScrollInactiveWindows (yes by default; missing from an older file, it keeps
        ///      its default value).
        /// Since the "code-anglais" step (2026-11-02), the file is settings.json with English
        /// keys: an old parametres.json is no longer read.
        ///  12: step langues-socle — Language (interface language). Since step langue-anglais
        ///      the default is "en" (new installations); an older file belongs to an existing
        ///      French user, so it gets "fr".
        ///  13: step logging — DetailedLog (no by default). Nothing to fix: missing from an
        ///      older file, it keeps its default value.
        /// </summary>
        public const int CurrentVersion = 13;

        /// <summary>Format number of the file, so it can evolve one day without breaking everything.</summary>
        [DataMember(Order = 0)]
        public int FileVersion { get; set; }

        // ───────────────────────── Gaze ─────────────────────────

        /// <summary>Fraction of the way covered in 16 ms (one tick), between 0 and 1 (0 = still, 1 = raw, shaky gaze). Double smoothing, in real time: see Modes/Smoother.cs.</summary>
        [DataMember(Order = 10)]
        public double Smoothing { get; set; }

        /// <summary>
        /// Cursor anti-jitter, in % OF THE SCREEN HEIGHT (step 11; in pixels before): below
        /// it, the cursor does not move. 0.4 % = 4 px on a 1080 px screen.
        /// In code, use CursorDeadZonePx (the value in pixels of the current screen).
        /// </summary>
        [DataMember(Order = 11)]
        public double CursorDeadZone { get; set; }

        /// <summary>Maximum age of a gaze sample, in MILLISECONDS. Beyond it, the gaze is considered lost.</summary>
        [DataMember(Order = 12)]
        public double MaxMeasureAge { get; set; }

        // ───────────────────────── Fixation ─────────────────────────

        /// <summary>
        /// The gaze is "still" while it stays within this radius, in % of the screen height
        /// (step 11). 1.4 % = 15 px on a 1080 px screen. In code: FixationRadiusPx.
        /// </summary>
        [DataMember(Order = 20)]
        public double FixationRadius { get; set; }

        /// <summary>Stillness duration before switching to fixed gaze mode, in seconds.</summary>
        [DataMember(Order = 21)]
        public double FixationDelay { get; set; }

        /// <summary>
        /// Maximum gaze speed during a fixation, in % OF THE SCREEN HEIGHT PER SECOND (step 11;
        /// in pixels per second before). In code: MaxFixationSpeedPx.
        /// Between two samples, the gaze speed is computed (distance ÷ elapsed time): above
        /// this value, the eye is jumping to another point (a "saccade"), it is not a
        /// fixation.
        /// Setting added in step 2 (it is not in the first version of the specification).
        /// 42 %/s = 907 px/s on the 4K screen (the old value, 900 px/s), i.e. 30 pixels
        /// between two PCEye samples (≈ 30 per second).
        /// </summary>
        [DataMember(Order = 24)]
        public double MaxFixationSpeed { get; set; }

        /// <summary>Automatic left click if the fixation goes on (yes / no). No by default.</summary>
        [DataMember(Order = 22)]
        public bool DirectClick { get; set; }

        /// <summary>Extra delay after entering fixed gaze before the direct click, in seconds.</summary>
        [DataMember(Order = 23)]
        public double DirectClickDelay { get; set; }

        // ───────────────────────── Display ─────────────────────────

        /// <summary>
        /// Radius of the movement mode circle (and fixed gaze exit zone), in % of the screen
        /// height (step 11). 8.8 % = 190 px in 4K, 95 px in 1080p.
        /// In code: CircleRadiusPx (which also takes [InterfaceSize] into account).
        /// </summary>
        [DataMember(Order = 30)]
        public double CircleRadius { get; set; }

        /// <summary>
        /// Side of the gaze-activated icons, in % of the screen height (step 11).
        /// 5.6 % = 121 px in 4K, 60 px in 1080p. In code: IconSizePx.
        /// </summary>
        [DataMember(Order = 31)]
        public double IconSize { get; set; }

        /// <summary>
        /// Interface size, in % (step 11): 75 (small), 100 (normal, default), 125 (large) or
        /// 150 (very large). Scales everything drawn on the overlay (circle, icons, menu bar,
        /// texts…), but not the gaze settings (fixation radius, maximum speed, anti-jitter).
        /// See ScreenMetrics.cs.
        /// </summary>
        [DataMember(Order = 33)]
        public double InterfaceSize { get; set; }

        // ── The same settings in PIXELS of the current screen (step 11). Computed on every
        //    read (the screen may change while GazeBar runs) and not saved in the file (no
        //    [DataMember]). These are what the modes use.

        /// <summary>[CursorDeadZone] in pixels of the current screen.</summary>
        public double CursorDeadZonePx
        {
            get { return ScreenMetrics.Percent(CursorDeadZone); }
        }

        /// <summary>[FixationRadius] in pixels of the current screen.</summary>
        public double FixationRadiusPx
        {
            get { return ScreenMetrics.Percent(FixationRadius); }
        }

        /// <summary>[MaxFixationSpeed] in pixels per second on the current screen.</summary>
        public double MaxFixationSpeedPx
        {
            get { return ScreenMetrics.Percent(MaxFixationSpeed); }
        }

        /// <summary>[CircleRadius] in pixels of the current screen, multiplied by [InterfaceSize].</summary>
        public double CircleRadiusPx
        {
            get { return ScreenMetrics.InterfacePercent(CircleRadius); }
        }

        /// <summary>[IconSize] in pixels of the current screen, multiplied by [InterfaceSize].</summary>
        public double IconSizePx
        {
            get { return ScreenMetrics.InterfacePercent(IconSize); }
        }

        /// <summary>Show an action's label when its icon is hovered (learning mode).</summary>
        [DataMember(Order = 32)]
        public bool ShowLabels { get; set; }

        // ───────────────────────── Delays ─────────────────────────

        /// <summary>Gaze duration on an icon before it activates, in seconds.</summary>
        [DataMember(Order = 40)]
        public double ActivationDelay { get; set; }

        /// <summary>
        /// Gaze duration on the fixed gaze mode options icon before it activates, in seconds.
        /// Much shorter than [ActivationDelay] (user request of 2026-10-22): it is the icon
        /// looked at most often, and looking at it by mistake has no consequence (the action
        /// choice always offers "retour").
        /// </summary>
        [DataMember(Order = 43)]
        public double OptionsIconDelay { get; set; }

        /// <summary>Gaze duration below the screen before the menu opens, in seconds.</summary>
        [DataMember(Order = 41)]
        public double MenuDelay { get; set; }

        /// <summary>Gaze duration outside the menu bar before it closes, in seconds.</summary>
        [DataMember(Order = 42)]
        public double MenuCloseDelay { get; set; }

        // ───────────────────────── Magnifier ─────────────────────────

        /// <summary>Go through the magnifier before the action choice.</summary>
        [DataMember(Order = 50)]
        public bool MagnifierBeforeAction { get; set; }

        /// <summary>Magnification of the capture in magnifier mode (4 = four times bigger).</summary>
        [DataMember(Order = 51)]
        public double MagnifierZoom { get; set; }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>
        /// "Android" drag and drop: pause between the button press and the start of the move,
        /// in seconds (as when moving an icon on a phone).
        /// Replaces the former long-press duration (step 6).
        /// </summary>
        [DataMember(Order = 60)]
        public double AndroidPauseDuration { get; set; }

        /// <summary>
        /// Duration of the cursor move during a drag and drop, in seconds: the cursor goes
        /// from the start point to the end point in that time, button held down.
        /// Replaces the former drop delay (step 6): the end point is now fixed with [FixationDelay].
        /// </summary>
        [DataMember(Order = 61)]
        public double DragDuration { get; set; }

        // ───────────────────────── Scrolling ─────────────────────────

        // (Step 8: the former neutral scroll zone setting is removed, the zone where nothing
        //  scrolls is now the circle itself, of radius [CircleRadius]. If still in the file,
        //  it is simply ignored when reading.)

        /// <summary>
        /// Maximum speed of scroll mode, in wheel notches per second (step 8).
        /// The speed is 0 at the edge of the dead zone (half of [CircleRadius]) and reaches
        /// 5 by default since 2026-11-02 (15 since 2026-10-28, 10 at first).
        /// </summary>
        [DataMember(Order = 71)]
        public double MaxScrollSpeed { get; set; }

        /// <summary>
        /// Windows setting "Scroll inactive windows when I hover over them" (2026-11-02). Yes
        /// by default: the wheel goes to the window under the cursor, without having to click
        /// in it first. The settings file keeps the choice; WheelScroller really applies it
        /// to Windows (at startup and on each change).
        /// </summary>
        [DataMember(Order = 72)]
        public bool ScrollInactiveWindows { get; set; }

        // ───────────────────────── Game ─────────────────────────

        /// <summary>
        /// Game mode smoothing, same meaning and same computation as [Smoothing] (see
        /// Modes/Smoother.cs), but set separately (user request of 2026-10-28). 0.16 by
        /// default, like movement mode (0.7 at first: the cursor jerked far too much).
        /// </summary>
        [DataMember(Order = 80)]
        public double GameSmoothing { get; set; }

        /// <summary>Multiplier of relative moves in game mode.</summary>
        [DataMember(Order = 81)]
        public double GameSensitivity { get; set; }

        /// <summary>
        /// Game mode variant (step 9, user's choice of 2026-10-27):
        ///   "direct":   the cursor follows the gaze (default, for Mindball Play);
        ///   "relative": moves are sent to the game (for other games).
        /// Written in full in settings.json, so it is easy to read.
        /// </summary>
        [DataMember(Order = 82)]
        public string GameMouse { get; set; }

        /// <summary>
        /// Game mode relative mouse: how the gaze is turned into movement (step 9).
        ///   "tracking": the movement follows the gaze's (default);
        ///   "gamepad":  the gap between the gaze and the screen centre gives the speed.
        /// Setting added in step 9: the specification proposes both ways "to test".
        /// </summary>
        [DataMember(Order = 83)]
        public string GameRelative { get; set; }

        // The possible values of GameMouse and GameRelative, written only once.
        public const string DirectMouseText = "direct";
        public const string RelativeMouseText = "relative";
        public const string TrackingRelativeText = "tracking";
        public const string GamepadRelativeText = "gamepad";

        /// <summary>True if game mode sends moves (GameMouse = "relative"). Not saved in the file.</summary>
        public bool RelativeMouse
        {
            get { return GameMouse == RelativeMouseText; }
            set { GameMouse = value ? RelativeMouseText : DirectMouseText; }
        }

        /// <summary>True if the relative mouse works like a gamepad (GameRelative = "gamepad"). Not saved.</summary>
        public bool GamepadRelative
        {
            get { return GameRelative == GamepadRelativeText; }
            set { GameRelative = value ? GamepadRelativeText : TrackingRelativeText; }
        }

        // ───────────────────────── General ─────────────────────────

        /// <summary>
        /// In movement mode, the mouse cursor also follows the gaze. No by default: the cursor
        /// then joins the gaze only once it is fixed (fixed gaze mode).
        /// </summary>
        [DataMember(Order = 90)]
        public bool MouseTracking { get; set; }

        /// <summary>
        /// Start GazeBar when the Windows session opens (step 7). No by default.
        /// The settings file keeps the choice; WindowsStartup really applies it, in the
        /// Windows registry.
        /// </summary>
        [DataMember(Order = 91)]
        public bool StartWithWindows { get; set; }

        // ───────────────────────── Action choice icons (step 10) ─────────────────────────

        /// <summary>
        /// The actions placed on the eight slots of the action choice crown, starting top
        /// right, clockwise (see ActionChoiceLayout). Each action is written by its name (for
        /// example "LeftClick", see ChoiceAction); "" = empty slot. Set in the icon
        /// configuration screen ("Icônes" tab of settings mode).
        /// </summary>
        [DataMember(Order = 100)]
        public string[] CrownIcons { get; set; }

        /// <summary>The three slots of the left edge, top to bottom ("Ctrl", "Shift", "Alt" or "").</summary>
        [DataMember(Order = 101)]
        public string[] ModifierIcons { get; set; }

        /// <summary>The three slots of the top edge, left to right ("Copy", "Cut", "Paste" or "").</summary>
        [DataMember(Order = 102)]
        public string[] UtilityIcons { get; set; }

        // ───────────────────────── Gaze keyboard (step 12) ─────────────────────────

        /// <summary>
        /// Gaze duration on a keyboard key before it activates, in seconds.
        /// Separate from [ActivationDelay]: typing is often faster than clicking.
        /// </summary>
        [DataMember(Order = 110)]
        public double KeyDelay { get; set; }

        /// <summary>
        /// While the gaze stays on a key, it is typed again after another [KeyDelay] (for
        /// "ll", "ss"…). No: the key must be left to type it again.
        /// </summary>
        [DataMember(Order = 111)]
        public bool KeyRepeat { get; set; }

        /// <summary>Automatic capital after ".", "?" or "!" followed by a space.</summary>
        [DataMember(Order = 112)]
        public bool AutoCapitalize { get; set; }

        /// <summary>Height of the gaze keyboard, in % of the screen height.</summary>
        [DataMember(Order = 113)]
        public double KeyboardHeight { get; set; }

        /// <summary>
        /// Size of the gaze keyboard, in %: 100, 75 or 50 (user request of 2026-11-01). Width
        /// AND height shrink together: at 50 %, the keyboard is half the screen width and half
        /// of [KeyboardHeight], centred.
        /// Also set with the "Taille" button of the keyboard menu bar.
        /// </summary>
        [DataMember(Order = 114)]
        public double KeyboardSize { get; set; }

        /// <summary>
        /// True: the keyboard is at the top of the screen; false: at the bottom. Changed by
        /// the "Haut / bas" button of the keyboard menu bar, and remembered for the next
        /// opening (user request of 2026-11-01).
        /// </summary>
        [DataMember(Order = 115)]
        public bool KeyboardAtTop { get; set; }

        /// <summary>
        /// The gaze keyboard layout: the name of a file of the Keyboards folder, without
        /// ".json" (for example "en-qwerty", "fr-azerty"). Chosen with the "Langue" button of the keyboard
        /// menu bar (step 14).
        /// </summary>
        [DataMember(Order = 116)]
        public string KeyboardLayout { get; set; }

        /// <summary>
        /// True: a band above the keys shows the last typed words (the "input box", a plain
        /// preview: text always goes straight to the application).
        /// "Zone de saisie" button of the keyboard bar (step 14). Not in the Clavier settings
        /// tab: it already has five rows, the maximum that fits on the page.
        /// </summary>
        [DataMember(Order = 117)]
        public bool InputBox { get; set; }

        // ───────────────────────── Language (step langues-socle) ─────────────────────────

        /// <summary>
        /// The interface language: the code of a file of the Languages folder, without
        /// ".json" ("fr", later "en"…). See Texts.cs. Chosen with the "Langue" button of the
        /// Général settings tab.
        /// </summary>
        [DataMember(Order = 130)]
        public string Language { get; set; }

        // ───────────────────────── Log (step logging) ─────────────────────────

        /// <summary>
        /// Detailed log: no by default. Off, gazebar.log only gets the important events and the
        /// errors; on, it also gets every frequent event (modes, actions, keys...), useful to
        /// track down a bug. See App.Log and App.LogDetail.
        /// Like StartWithWindows, it belongs to the computer: loading a profile does not change it.
        /// </summary>
        [DataMember(Order = 131)]
        public bool DetailedLog { get; set; }

        /// <summary>
        /// A group's layout, read from the lists above: an array with one cell per slot, null
        /// for an empty slot. Not saved as such.
        /// </summary>
        public ChoiceAction?[] Layout(IconGroup group)
        {
            string[] names = Names(group) ?? new string[0];
            ChoiceAction?[] result = new ChoiceAction?[ActionCatalog.SlotCount(group)];
            for (int i = 0; i < result.Length && i < names.Length; i++)
            {
                result[i] = ActionCatalog.Read(names[i], group);
            }
            return result;
        }

        /// <summary>
        /// Places an action on a slot (null = empty it). The same action may be placed on
        /// several slots of the group (user request of 2026-10-28). Does not save the file
        /// (see Save).
        /// </summary>
        public void SetSlot(IconGroup group, int num, ChoiceAction? action)
        {
            ChoiceAction?[] layout = Layout(group);
            if (num < 0 || num >= layout.Length)
            {
                return;
            }
            layout[num] = action;
            WriteLayout(group, layout);
        }

        /// <summary>Restores the original layout of the three groups ("Défaut" button of the "Icônes" tab).</summary>
        public void DefaultIcons()
        {
            CrownIcons = DefaultNames(IconGroup.Crown);
            ModifierIcons = DefaultNames(IconGroup.Modifiers);
            UtilityIcons = DefaultNames(IconGroup.Utilities);
        }

        /// <summary>A group's list of names, as saved in the file.</summary>
        private string[] Names(IconGroup group)
        {
            switch (group)
            {
                case IconGroup.Crown:
                    return CrownIcons;
                case IconGroup.Modifiers:
                    return ModifierIcons;
                default:
                    return UtilityIcons;
            }
        }

        /// <summary>Writes a layout (one cell per slot) into the group's list of names.</summary>
        private void WriteLayout(IconGroup group, ChoiceAction?[] layout)
        {
            string[] names = new string[layout.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = ActionCatalog.Write(layout[i]);
            }
            switch (group)
            {
                case IconGroup.Crown:
                    CrownIcons = names;
                    break;
                case IconGroup.Modifiers:
                    ModifierIcons = names;
                    break;
                default:
                    UtilityIcons = names;
                    break;
            }
        }

        private static string[] DefaultNames(IconGroup group)
        {
            ChoiceAction[] actions = ActionCatalog.DefaultLayout(group);
            string[] names = new string[actions.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = actions[i].ToString();
            }
            return names;
        }

        /// <summary>
        /// Tidies a group's list read from the file: the right number of cells, and unknown
        /// names or names of another group emptied (an action may appear several times). A
        /// missing list takes the default layout.
        /// </summary>
        private void FixIcons(IconGroup group)
        {
            if (Names(group) == null)
            {
                WriteLayout(group, Convert(ActionCatalog.DefaultLayout(group)));
                return;
            }
            WriteLayout(group, Layout(group));
        }

        private static ChoiceAction?[] Convert(ChoiceAction[] actions)
        {
            ChoiceAction?[] result = new ChoiceAction?[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                result[i] = actions[i];
            }
            return result;
        }

        // ───────────────────────── Default values ─────────────────────────

        /// <summary>The constructor: a new object holds the default values.</summary>
        public AppSettings()
        {
            DefaultValues();
        }

        /// <summary>
        /// Resets every setting to its initial value (those of the specification).
        /// The "Défaut" button of each settings mode tab (step 7) only resets the settings of
        /// its tab: it reads these values from a new object (new AppSettings()).
        /// </summary>
        public void DefaultValues()
        {
            FileVersion = CurrentVersion;

            Smoothing = 0.16;
            // Sizes in % of the screen height (step 11): the old values in pixels of the 4K
            // screen (8, 30, 900, 190, 120) divided by 21.6, rounded.
            CursorDeadZone = 0.4;
            MaxMeasureAge = 300;

            FixationRadius = 1.4;
            FixationDelay = 0.8;
            MaxFixationSpeed = 42;
            DirectClick = false;
            DirectClickDelay = 1.5;

            CircleRadius = 8.8;
            IconSize = 5.6;
            InterfaceSize = 100;
            ShowLabels = true;

            ActivationDelay = 0.6;
            OptionsIconDelay = 0.3;
            MenuDelay = 0.5;
            MenuCloseDelay = 1.0;

            MagnifierBeforeAction = true;
            MagnifierZoom = 4;

            AndroidPauseDuration = 1.0;
            DragDuration = 0.5;

            MaxScrollSpeed = 5;
            ScrollInactiveWindows = true;

            GameSmoothing = 0.16;
            GameSensitivity = 1.0;
            GameMouse = DirectMouseText;
            GameRelative = TrackingRelativeText;

            MouseTracking = false;
            StartWithWindows = false;
            DetailedLog = false;

            KeyDelay = 0.6;
            KeyRepeat = true;
            AutoCapitalize = true;
            KeyboardHeight = 50;
            KeyboardSize = 100;
            KeyboardAtTop = false;
            KeyboardLayout = GazeBar.KeyboardLayout.DefaultName;
            InputBox = false;
            Language = Texts.FallbackLanguage;

            DefaultIcons();
        }

        /// <summary>
        /// Called by the serializer JUST BEFORE reading the file.
        ///
        /// Pitfall to know: when reading, DataContractJsonSerializer does NOT call the
        /// constructor. A setting missing from the file (for example one added in a new
        /// version of GazeBar) would then be 0 or "no". By filling in the default values here
        /// first, a missing setting keeps its default value.
        /// </summary>
        [OnDeserializing]
        private void BeforeReading(StreamingContext context)
        {
            DefaultValues();
        }

        /// <summary>
        /// Brings each value back within reasonable limits. Protects against a typo in the
        /// file (a smoothing of 30 instead of 0.30, a negative delay…).
        /// </summary>
        public void Fix()
        {
            Smoothing = Clamp(Smoothing, 0.01, 1);
            // Size limits in % of the screen height (step 11).
            CursorDeadZone = Clamp(CursorDeadZone, 0, 5);
            MaxMeasureAge = Clamp(MaxMeasureAge, 50, 5000);

            FixationRadius = Clamp(FixationRadius, 0.2, 14);
            FixationDelay = Clamp(FixationDelay, 0.1, 10);
            MaxFixationSpeed = Clamp(MaxFixationSpeed, 2, 900);
            DirectClickDelay = Clamp(DirectClickDelay, 0.1, 10);

            CircleRadius = Clamp(CircleRadius, 1, 23);
            IconSize = Clamp(IconSize, 2, 19);
            InterfaceSize = Clamp(InterfaceSize, 75, 150);

            ActivationDelay = Clamp(ActivationDelay, 0.1, 10);
            OptionsIconDelay = Clamp(OptionsIconDelay, 0.1, 10);
            MenuDelay = Clamp(MenuDelay, 0.1, 10);
            MenuCloseDelay = Clamp(MenuCloseDelay, 0.1, 10);

            MagnifierZoom = Clamp(MagnifierZoom, 1, 10);

            AndroidPauseDuration = Clamp(AndroidPauseDuration, 0, 10);
            DragDuration = Clamp(DragDuration, 0.05, 10);

            MaxScrollSpeed = Clamp(MaxScrollSpeed, 1, 100);

            GameSmoothing = Clamp(GameSmoothing, 0.01, 1);
            GameSensitivity = Clamp(GameSensitivity, 0.1, 10);

            // An unknown word (typo, capitals…) becomes the default value again.
            string mouse = (GameMouse ?? "").Trim().ToLowerInvariant();
            GameMouse = mouse == RelativeMouseText ? RelativeMouseText : DirectMouseText;
            string relative = (GameRelative ?? "").Trim().ToLowerInvariant();
            GameRelative = relative == GamepadRelativeText ? GamepadRelativeText : TrackingRelativeText;

            KeyDelay = Clamp(KeyDelay, 0.1, 5);
            KeyboardHeight = Clamp(KeyboardHeight, 25, 75);
            // Only 50, 75 or 100: another value goes to the nearest (60 → 50, 90 → 100).
            KeyboardSize = Math.Round(Clamp(KeyboardSize, 50, 100) / 25) * 25;
            // An empty file name or one containing a path ("..\", "C:\"…) becomes the default
            // layout again: only files of the Keyboards folder are read.
            if (string.IsNullOrWhiteSpace(KeyboardLayout) ||
                KeyboardLayout.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                KeyboardLayout.Contains(".."))
            {
                KeyboardLayout = GazeBar.KeyboardLayout.DefaultName;
            }
            KeyboardLayout = KeyboardLayout.Trim();

            // Same rule for the language: a plain file name of the Languages folder.
            if (string.IsNullOrWhiteSpace(Language) ||
                Language.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                Language.Contains(".."))
            {
                Language = Texts.FallbackLanguage;
            }
            Language = Language.Trim().ToLowerInvariant();

            FixIcons(IconGroup.Crown);
            FixIcons(IconGroup.Modifiers);
            FixIcons(IconGroup.Utilities);
        }

        /// <summary>
        /// Updates a file written by an older version of GazeBar.
        /// Returns true if something changed (the file must then be saved again).
        ///
        /// Why? Changing a default value is not enough: the file already on the computer holds
        /// the old value (for example DirectClick = true), and that is what would be read. It
        /// is therefore fixed only once, then the version number is raised: afterwards, a
        /// choice made by the user in the file is respected.
        /// </summary>
        public bool Refresh()
        {
            if (FileVersion >= CurrentVersion)
            {
                return false;
            }

            if (FileVersion < 2)
            {
                DirectClick = false;
                MouseTracking = false;
                App.Log("Settings: updated to version 2 (DirectClick and MouseTracking turned off)");
            }

            if (FileVersion < 3)
            {
                // Nothing to fix: the old drag settings are simply ignored when reading, and
                // the new ones already have their default value (see BeforeReading). Saving
                // the file again makes the old ones disappear.
                App.Log("Settings: updated to version 3 (drag and drop settings replaced)");
            }

            if (FileVersion < 4)
            {
                // Only the old default is replaced: a value chosen by the user stays.
                if (Math.Abs(GameSmoothing - 0.7) < 0.001)
                {
                    GameSmoothing = 0.16;
                }
                App.Log("Settings: updated to version 4 (GameSmoothing 0.7 → 0.16)");
            }

            if (FileVersion < 5)
            {
                // Only the old default is replaced: a value chosen by the user stays.
                if (Math.Abs(MaxScrollSpeed - 10) < 0.001)
                {
                    MaxScrollSpeed = 15;
                }
                App.Log("Settings: updated to version 5 (MaxScrollSpeed 10 → 15)");
            }

            if (FileVersion < 6)
            {
                // Nothing to fix: icon lists missing from the file kept the default layout
                // (see BeforeReading). Saving the file again adds them.
                App.Log("Settings: updated to version 6 (action choice icon layout)");
            }

            if (FileVersion < 7)
            {
                // Sizes were in pixels, tuned on the user's 4K screen (2160 px high). They
                // become % of the screen height: percent = pixels ÷ 2160 × 100 = pixels ÷ 21.6.
                // ALWAYS divide by 2160, not by the current screen height: the values were
                // tuned in 4K, even if the screen is now 1920 × 1080.
                // Example: CircleRadius 190 px → 8.8 %.
                CursorDeadZone = PixelsToPercent(CursorDeadZone, 1);
                FixationRadius = PixelsToPercent(FixationRadius, 1);
                MaxFixationSpeed = PixelsToPercent(MaxFixationSpeed, 0);
                CircleRadius = PixelsToPercent(CircleRadius, 1);
                IconSize = PixelsToPercent(IconSize, 1);
                App.Log("Settings: updated to version 7 (sizes in % of the screen height)");
            }

            if (FileVersion < 8)
            {
                // Nothing to fix: keyboard settings missing from the file kept their default
                // value (see BeforeReading). Saving the file again adds them.
                App.Log("Settings: updated to version 8 (gaze keyboard settings)");
            }

            if (FileVersion < 9)
            {
                // Nothing to fix: KeyboardSize and KeyboardAtTop, missing from the file, kept
                // their default value (see BeforeReading).
                App.Log("Settings: updated to version 9 (keyboard size and position)");
            }

            if (FileVersion < 10)
            {
                // KeyboardLayout is missing from the file: it belongs to an existing French
                // user, who keeps the French keyboard (the default is English since step
                // clavier-anglais). InputBox kept its default value (see BeforeReading).
                KeyboardLayout = "fr-azerty";
                App.Log("Settings: updated to version 10 (keyboard language and input box)");
            }

            if (FileVersion < 11)
            {
                // Only the old default is replaced: a value chosen by the user stays.
                // (A version 4 or older file has just gone from 10 to 15 right above: so it
                // also ends at 5.)
                if (Math.Abs(MaxScrollSpeed - 15) < 0.001)
                {
                    MaxScrollSpeed = 5;
                }
                App.Log("Settings: updated to version 11 (MaxScrollSpeed 15 → 5)");
            }

            if (FileVersion < 12)
            {
                // Language is missing from the file: it belongs to an existing French user, who
                // keeps French (a new installation, with no file, starts in English).
                Language = "fr";
                App.Log("Settings: updated to version 12 (interface language)");
            }

            // Version 13 (DetailedLog): nothing to fix, the setting keeps its default value (no).
            FileVersion = CurrentVersion;
            return true;
        }

        /// <summary>
        /// Version 7 conversion: from pixels of the 4K screen to % of its height, rounded to
        /// "decimals" digits after the point (as in settings mode).
        /// </summary>
        private static double PixelsToPercent(double pixels, int decimals)
        {
            return Math.Round(pixels / ScreenMetrics.ReferenceHeightPixels * 100, decimals);
        }

        /// <summary>Returns the value, brought between min and max. An unreadable value (NaN) becomes min.</summary>
        private static double Clamp(double val, double min, double max)
        {
            if (double.IsNaN(val) || val < min)
            {
                return min;
            }
            if (val > max)
            {
                return max;
            }
            return val;
        }

        // ───────────────────────── Reading and saving ─────────────────────────

        /// <summary>Full path of the settings file.</summary>
        public static string FilePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GazeBar");
                return Path.Combine(folder, "settings.json");
            }
        }

        /// <summary>Call once at startup: reads the file and fills AppSettings.Current.</summary>
        public static void Initialize()
        {
            Current = Load();
        }

        /// <summary>
        /// Step 15 (profiles): replaces all current settings at once with "newItems" (a
        /// profile just read). Nothing in the program keeps a copy of the old object: each
        /// mode reads AppSettings.Current again at every tick, so everything follows at once.
        /// Saves nothing: the caller must do it.
        /// </summary>
        public static void Replace(AppSettings newItems)
        {
            if (newItems != null)
            {
                Current = newItems;
            }
        }

        /// <summary>
        /// Reads a settings file, updates a file of an older version (Refresh) then brings
        /// the values back within their limits (Fix).
        ///
        /// Used at startup (settings.json) and in step 15 to load a profile.
        /// "updated" tells whether the file came from an older version.
        /// If the file is missing or unreadable, an exception is THROWN: the caller decides
        /// what to do (start from default values, show a message…).
        /// </summary>
        public static AppSettings ReadFile(string path, out bool updated)
        {
            AppSettings result;
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
            using (FileStream file = File.OpenRead(path))
            {
                result = (AppSettings)serializer.ReadObject(file);
            }
            if (result == null)
            {
                // A file holding only "null": treated as unreadable.
                throw new InvalidDataException("the file contains no settings");
            }
            // Updating an old file comes BEFORE Fix (step 11): the old sizes in pixels (190…)
            // would exceed the new limits in % (23 at most) and would be damaged before being
            // converted.
            updated = result.Refresh();
            result.Fix();
            return result;
        }

        /// <summary>
        /// Reads the settings file.
        ///  - If it does not exist yet (first launch), it is created with the default values.
        ///  - If it is unreadable (damaged file, syntax error), it is set aside under the name
        ///    settings.json.unreadable, and the default values are used.
        /// In every case, the program starts.
        /// </summary>
        public static AppSettings Load()
        {
            string path = FilePath;
            AppSettings result;

            if (!File.Exists(path))
            {
                App.Log("Settings: first launch, creating " + path);
                result = new AppSettings();
                result.Save();
                return result;
            }

            try
            {
                bool updated;
                result = ReadFile(path, out updated);
                App.Log("Settings: read from " + path);
                if (updated)
                {
                    result.Save();
                }
                return result;
            }
            catch (Exception ex)
            {
                App.Log("Settings: unreadable file, default values used. " + ex.Message);
                try
                {
                    File.Copy(path, path + ".unreadable", true);
                }
                catch
                {
                    // Never mind the backup copy: what matters is to start.
                }
                result = new AppSettings();
                result.Save();
                return result;
            }
        }

        /// <summary>
        /// Saves the settings to the file, nicely laid out (one setting per line).
        ///
        /// It is written first to a temporary file, then moved over the old one: if the
        /// computer shuts down mid-write, the old file stays intact.
        /// </summary>
        public void Save()
        {
            SaveTo(FilePath);
        }

        /// <summary>
        /// Saves the settings to the file "path" (step 15: also used to write a profile in
        /// Documents\GazeBar\Profiles). The folder is created if it does not exist.
        /// Returns false if writing failed (disk full, protected folder…).
        /// </summary>
        public bool SaveTo(string path)
        {
            string temp = path + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                using (FileStream file = File.Create(temp))
                using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(
                           file, new UTF8Encoding(false), false, true, "  "))
                {
                    // CreateJsonWriter parameters: the file, the encoding (UTF-8 without BOM),
                    // "do not close the file ourselves", "indent", and the indentation (2 spaces).
                    serializer.WriteObject(writer, this);
                    writer.Flush();
                }

                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
                return true;
            }
            catch (Exception ex)
            {
                // Never crash the program because of a settings file.
                App.Log("Settings: failed to save " + path + ": " + ex.Message);
                return false;
            }
        }
    }
}
