using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace Prunel
{
    /// <summary>
    /// MODEL: all of Prunel's settings, in one place.
    ///
    /// They are read at startup and
    /// saved in the file:
    ///     %APPDATA%\Prunel\settings.json
    /// (for example C:\Users\Name\AppData\Roaming\Prunel\settings.json).
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
    internal sealed partial class AppSettings
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
        ///      At 0.7, the cursor jerked in direct mouse);
        ///   5: MaxScrollSpeed goes from 10 to 15 notches/s by default (maximum scroll speed
        ///      raised by half);
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
        ///   9: step 12 (user remarks) — KeyboardSize and KeyboardAtTop. Nothing
        ///      to fix: missing from an older file, they keep their default value;
        ///  10: step 14 ("clavier-options") — KeyboardLayout and InputBox. Missing from an
        ///      older file: InputBox keeps its default value; KeyboardLayout gets "fr-azerty"
        ///      (since step clavier-anglais the default is "en-qwerty", for new installations);
        ///  11: MaxScrollSpeed goes from 15 to 5 notches/s by default (user request of
        ///      2026-11-02, after the switch to fine scrolling), and new setting
        ///      ScrollInactiveWindows (yes by default; missing from an older file, it keeps
        ///      its default value).
        /// Since the "code-anglais" step, the file is settings.json with English
        /// keys: an old parametres.json is no longer read.
        ///  12: step langues-socle — Language (interface language). Since step langue-anglais
        ///      the default is "en" (new installations); an older file belongs to an existing
        ///      French user, so it gets "fr".
        ///  13: step logging — DetailedLog (no by default). Nothing to fix: missing from an
        ///      older file, it keeps its default value.
        ///  14: step sound-feedback — KeySound and ButtonSound (no by default). Nothing to fix:
        ///      missing from an older file, they keep their default value.
        ///  15: issue #55 — KeyboardTransparency (0 % by default). Nothing to fix: missing from
        ///      an older file, it keeps its default value.
        /// </summary>
        public const int CurrentVersion = 15;

        /// <summary>Format number of the file, so it can evolve one day without breaking everything.</summary>
        [DataMember(Order = 0)]
        public int FileVersion { get; set; }

        // ───────────────────────── Gaze ─────────────────────────

        /// <summary>Fraction of the way covered in 16 ms (one tick), between 0 and 1 (0 = still, 1 = raw, shaky gaze). Double smoothing, in real time: see Modes/Smoother.cs.</summary>
        [DataMember(Order = 10)]
        public double Smoothing { get; set; }

        /// <summary>
        /// Cursor anti-jitter, in % OF THE SCREEN HEIGHT (in pixels before): below
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
        /// The gaze is "still" while it stays within this radius, in % of the screen height. 1.4 %
        /// = 15 px on a 1080 px screen. In code: FixationRadiusPx.
        /// </summary>
        [DataMember(Order = 20)]
        public double FixationRadius { get; set; }

        /// <summary>Stillness duration before switching to fixed gaze mode, in seconds.</summary>
        [DataMember(Order = 21)]
        public double FixationDelay { get; set; }

        /// <summary>
        /// Maximum gaze speed during a fixation, in % OF THE SCREEN HEIGHT PER SECOND.
        /// In code: MaxFixationSpeedPx.
        /// Between two samples, the gaze speed is computed (distance ÷ elapsed time): above
        /// this value, the eye is jumping to another point (a "saccade"), it is not a
        /// fixation.
        /// (This setting is not in the first version of the specification.)
        /// 42 %/s = 907 px/s on the 4K screen, i.e. 30 pixels
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
        /// height. 8.8 % = 190 px in 4K, 95 px in 1080p.
        /// In code: CircleRadiusPx (which also takes [InterfaceSize] into account).
        /// </summary>
        [DataMember(Order = 30)]
        public double CircleRadius { get; set; }

        /// <summary>
        /// Side of the gaze-activated icons, in % of the screen height.
        /// 5.6 % = 121 px in 4K, 60 px in 1080p. In code: IconSizePx.
        /// </summary>
        [DataMember(Order = 31)]
        public double IconSize { get; set; }

        /// <summary>
        /// Interface size, in %: 75 (small), 100 (normal, default), 125 (large) or
        /// 150 (very large). Scales everything drawn on the overlay (circle, icons, menu bar,
        /// texts…), but not the gaze settings (fixation radius, maximum speed, anti-jitter).
        /// See ScreenMetrics.cs.
        /// </summary>
        [DataMember(Order = 33)]
        public double InterfaceSize { get; set; }

        // ── The same settings in PIXELS of the current screen. Computed on every
        //    read (the screen may change while Prunel runs) and not saved in the file (no
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
        /// Much shorter than [ActivationDelay]: it is the icon
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
        /// </summary>
        [DataMember(Order = 60)]
        public double AndroidPauseDuration { get; set; }

        /// <summary>
        /// Duration of the cursor move during a drag and drop, in seconds: the cursor goes
        /// from the start point to the end point in that time, button held down.
        /// The end point is fixed with [FixationDelay].
        /// </summary>
        [DataMember(Order = 61)]
        public double DragDuration { get; set; }

        // ───────────────────────── Scrolling ─────────────────────────

        // (The zone where nothing scrolls is the circle itself, of radius [CircleRadius]: it
        //  has no setting of its own. An old "neutral zone" setting still in the file is
        //  simply ignored when reading.)

        /// <summary>
        /// Maximum speed of scroll mode, in wheel notches per second.
        /// The speed is 0 at the edge of the dead zone (half of [CircleRadius]) and reaches
        /// 5 by default.
        /// </summary>
        [DataMember(Order = 71)]
        public double MaxScrollSpeed { get; set; }

        /// <summary>
        /// Windows setting "Scroll inactive windows when I hover over them". Yes
        /// by default: the wheel goes to the window under the cursor, without having to click
        /// in it first. The settings file keeps the choice; WheelScroller really applies it
        /// to Windows (at startup and on each change).
        /// </summary>
        [DataMember(Order = 72)]
        public bool ScrollInactiveWindows { get; set; }

        // ───────────────────────── Game ─────────────────────────

        /// <summary>
        /// Game mode smoothing, same meaning and same computation as [Smoothing] (see
        /// Modes/Smoother.cs), but set separately. 0.16 by
        /// default, like movement mode (at 0.7, the cursor jerks far too much).
        /// </summary>
        [DataMember(Order = 80)]
        public double GameSmoothing { get; set; }

        /// <summary>Multiplier of relative moves in game mode.</summary>
        [DataMember(Order = 81)]
        public double GameSensitivity { get; set; }

        /// <summary>
        /// Game mode variant:
        ///   "direct":   the cursor follows the gaze (default, for Mindball Play);
        ///   "relative": moves are sent to the game (for other games).
        /// Written in full in settings.json, so it is easy to read.
        /// </summary>
        [DataMember(Order = 82)]
        public string GameMouse { get; set; }

        /// <summary>
        /// Game mode relative mouse: how the gaze is turned into movement.
        ///   "tracking": the movement follows the gaze's (default);
        ///   "gamepad":  the gap between the gaze and the screen centre gives the speed.
        /// The specification proposes both ways "to test".
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
        /// Start Prunel when the Windows session opens. No by default.
        /// The settings file keeps the choice; WindowsStartup really applies it, in the
        /// Windows registry.
        /// </summary>
        [DataMember(Order = 91)]
        public bool StartWithWindows { get; set; }

        // ───────────────────────── Action choice icons ─────────────────────────

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

        // ───────────────────────── Gaze keyboard ─────────────────────────

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
        /// Size of the gaze keyboard, in %: 100, 75 or 50. Width
        /// AND height shrink together: at 50 %, the keyboard is half the screen width and half
        /// of [KeyboardHeight], centred.
        /// Also set with the "Taille" button of the keyboard menu bar.
        /// </summary>
        [DataMember(Order = 114)]
        public double KeyboardSize { get; set; }

        /// <summary>
        /// True: the keyboard is at the top of the screen; false: at the bottom. Changed by
        /// the "Haut / bas" button of the keyboard menu bar, and remembered for the next
        /// opening.
        /// </summary>
        [DataMember(Order = 115)]
        public bool KeyboardAtTop { get; set; }

        /// <summary>
        /// The gaze keyboard layout: the name of a file of the Keyboards folder, without
        /// ".json" (for example "en-qwerty", "fr-azerty"). Chosen with the "Langue" button of the keyboard
        /// menu bar.
        /// </summary>
        [DataMember(Order = 116)]
        public string KeyboardLayout { get; set; }

        /// <summary>
        /// True: a band above the keys shows the last typed words (the "input box", a plain
        /// preview: text always goes straight to the application).
        /// "Zone de saisie" button of the keyboard bar. Not in the Clavier settings
        /// tab: it already has five rows, the maximum that fits on the page.
        /// </summary>
        [DataMember(Order = 117)]
        public bool InputBox { get; set; }

        /// <summary>
        /// Transparency of the keyboard BACKGROUNDS, in %: 0, 25, 50 or 75 (issue #55). The
        /// keyboard background, the key backgrounds and the input box let what is behind them
        /// show through; the labels, the hover and the green flash stay fully visible.
        /// "Transparence" button of the keyboard menu bar (0 → 25 → 50 → 75 → 0 %). Not in the
        /// Clavier settings tab: it already has five rows, the maximum that fits on the page.
        /// </summary>
        [DataMember(Order = 118)]
        public double KeyboardTransparency { get; set; }

        // ───────────────────────── Language ─────────────────────────

        /// <summary>
        /// The interface language: the code of a file of the Languages folder, without
        /// ".json" ("fr", later "en"…). See Texts.cs. Chosen with the "Langue" button of the
        /// Général settings tab.
        /// </summary>
        [DataMember(Order = 130)]
        public string Language { get; set; }

        // ───────────────────────── Log ─────────────────────────

        /// <summary>
        /// Detailed log: no by default. Off, prunel.log only gets the important events and the
        /// errors; on, it also gets every frequent event (modes, actions, keys...), useful to
        /// track down a bug. See App.Log and App.LogDetail.
        /// Like StartWithWindows, it belongs to the computer: loading a profile does not change it.
        /// </summary>
        [DataMember(Order = 131)]
        public bool DetailedLog { get; set; }

        // ───────────────────────── Sound ─────────────────────────

        /// <summary>
        /// A short sound when a key of the gaze keyboard activates (yes / no). No by default.
        /// See SoundFeedback.
        /// </summary>
        [DataMember(Order = 140)]
        public bool KeySound { get; set; }

        /// <summary>
        /// A short sound when any other button activates: menu, action choice, options icon,
        /// settings... (yes / no). No by default. See SoundFeedback.
        /// </summary>
        [DataMember(Order = 141)]
        public bool ButtonSound { get; set; }
    }
}
