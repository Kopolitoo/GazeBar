using System;
using System.Collections.Generic;
using System.Globalization;

namespace GazeBar
{
    /// <summary>
    /// A setting as it is SHOWN in settings mode (step 7): its displayed name, an explanation
    /// sentence, its unit, the step of the "−" and "+" buttons, and the way to read and write
    /// it in AppSettings.
    ///
    /// Two kinds of settings:
    ///   - a NUMBER (Smoothing, FixationDelay...): "−" button, value, "+" button;
    ///   - a YES / NO (DirectClick, MouseTracking...): a rounded SWITCH, like Bootstrap's
    ///     (2026-11-02, replaces the two "No" and "Yes" buttons).
    ///     Variant (step 9): a choice between two words, for example "direct" or "relative"
    ///     for GameMouse. There, a switch would not say what each side means: the two
    ///     buttons are kept, one per word (see TwoChoices).
    ///
    /// Read and Write are "functions stored in a variable" (delegates). For example
    ///     p => p.Smoothing                 reads the smoothing of an AppSettings object p;
    ///     (p, v) => p.Smoothing = v        writes the value v into the smoothing of p.
    /// This way, a single class can show and change any setting.
    ///
    /// The limits (minimum, maximum) are NOT repeated here: after each change,
    /// AppSettings.Fix() brings the value back within its limits. They stay written in a
    /// single place.
    /// </summary>
    internal sealed class Setting
    {
        /// <summary>
        /// Numbers are shown with a POINT (0.16), as in settings.json (user request of
        /// 2026-10-26). CultureInfo.InvariantCulture is the "neutral" .NET formatting, which
        /// uses the point.
        /// </summary>
        private static readonly CultureInfo Format = CultureInfo.InvariantCulture;

        private Setting()
        {
        }

        /// <summary>
        /// The key of the setting's texts in the language files (for example
        /// "setting.fixationDelay"): "&lt;key&gt;.name" is the displayed name, "&lt;key&gt;.help" the
        /// explanation (see Texts.cs). Also used in the log.
        /// </summary>
        public string Key { get; private set; }

        /// <summary>The displayed name, in the current language (for example "Délai de fixation").</summary>
        public string Name
        {
            get { return Texts.Get(Key + ".name"); }
        }

        /// <summary>A short sentence, shown under the name, in the current language.</summary>
        public string Explanation
        {
            get { return Texts.Get(Key + ".help"); }
        }

        private string _unit;

        /// <summary>
        /// The unit, with its leading space (for example " s", " %") or "" for none. A unit
        /// with words ("unit.…" key, for example " crans/s") is read from the language file.
        /// </summary>
        public string Unit
        {
            get { return _unit.StartsWith("unit.", StringComparison.Ordinal) ? Texts.Get(_unit) : _unit; }
        }

        /// <summary>By how much "−" and "+" change the value.</summary>
        public double Step { get; private set; }

        /// <summary>Number of digits after the point, deduced from the step (0.05 → 2 digits).</summary>
        public int Decimals { get; private set; }

        /// <summary>True for a yes / no setting (or a choice between two words).</summary>
        public bool YesNo { get; private set; }

        /// <summary>
        /// True for a real yes / no (created by YesOrNo): it is shown as a switch.
        /// False for a choice between two other words (TwoChoices): two buttons.
        /// </summary>
        public bool Switch { get; private set; }

        private string _wrongKey;
        private string _rightKey;

        /// <summary>The word of the left button ("false" value): usually "Non".</summary>
        public string WrongWord
        {
            get { return Texts.Get(_wrongKey); }
        }

        /// <summary>The word of the right button ("true" value): usually "Oui".</summary>
        public string RightWord
        {
            get { return Texts.Get(_rightKey); }
        }

        private Func<AppSettings, double> _readNumber;
        private Action<AppSettings, double> _writeNumber;
        private Func<AppSettings, bool> _readYesNo;
        private Action<AppSettings, bool> _writeYesNo;

        /// <summary>Called after each change (null = nothing special to do).</summary>
        public Action<AppSettings> AfterChange { get; private set; }

        /// <summary>Creates a numeric setting.</summary>
        public static Setting Number(string key, string unit, double step,
                                     Func<AppSettings, double> read, Action<AppSettings, double> write)
        {
            Setting r = new Setting();
            r.Key = key;
            r._unit = unit;
            r.Step = step;
            r.Decimals = CountDecimals(step);
            r._readNumber = read;
            r._writeNumber = write;
            return r;
        }

        /// <summary>Creates a yes / no setting. after: optional, called after each change.</summary>
        public static Setting YesOrNo(string key,
                                       Func<AppSettings, bool> read, Action<AppSettings, bool> write,
                                       Action<AppSettings> after = null)
        {
            Setting r = TwoChoices(key, "common.no", "common.yes", read, write, after);
            r.Switch = true;   // shown as a switch (2026-11-02)
            return r;
        }

        /// <summary>
        /// Creates a choice between two words (step 9). The setting is stored like a yes / no:
        /// "false" = wrongKey (left button), "true" = rightKey (right button). The words are
        /// keys of the language files.
        /// Example: TwoChoices("setting.relativeMouse", "setting.relativeMouse.left",
        /// "setting.relativeMouse.right", p => p.RelativeMouse, ...).
        /// </summary>
        public static Setting TwoChoices(string key, string wrongKey, string rightKey,
                                        Func<AppSettings, bool> read, Action<AppSettings, bool> write,
                                        Action<AppSettings> after = null)
        {
            Setting r = new Setting();
            r.Key = key;
            r._unit = "";
            r.YesNo = true;
            r._wrongKey = wrongKey;
            r._rightKey = rightKey;
            r._readYesNo = read;
            r._writeYesNo = write;
            r.AfterChange = after;
            return r;
        }

        public double ReadNumber(AppSettings p)
        {
            return _readNumber(p);
        }

        public bool ReadYesNo(AppSettings p)
        {
            return _readYesNo(p);
        }

        /// <summary>
        /// Writes a new value, brings it back within its limits (Fix) and rounds it to the
        /// number of decimals shown (to avoid 0.30000000000000004 after several "+").
        /// </summary>
        public void WriteNumber(AppSettings p, double val)
        {
            _writeNumber(p, Math.Round(val, Decimals));
            p.Fix();
        }

        public void WriteYesNo(AppSettings p, bool val)
        {
            _writeYesNo(p, val);
        }

        /// <summary>"−" (direction = -1) or "+" (direction = +1): one step more or less.</summary>
        public void Change(AppSettings p, int direction)
        {
            WriteNumber(p, ReadNumber(p) + direction * Step);
        }

        /// <summary>Copies the value of "source" into "target" (for the "Default" button of a tab).</summary>
        public void Copy(AppSettings source, AppSettings target)
        {
            if (YesNo)
            {
                WriteYesNo(target, ReadYesNo(source));
            }
            else
            {
                WriteNumber(target, ReadNumber(source));
            }
        }

        /// <summary>The value ready to show, with its unit: "0.16", "0.8 s", "oui", "directe".</summary>
        public string Text(AppSettings p)
        {
            if (YesNo)
            {
                return (ReadYesNo(p) ? RightWord : WrongWord).ToLowerInvariant();
            }
            return NumberText(ReadNumber(p)) + Unit;
        }

        /// <summary>The number alone, with a point and the right number of decimals (for input).</summary>
        public string NumberText(double val)
        {
            return val.ToString("F" + Decimals, Format);
        }

        /// <summary>
        /// Reads a number typed by the caregiver. Accepts the point AND the comma (0.16 or 0,16),
        /// and ignores the unit if it was typed ("0.8 s"). Returns false if it is not a number.
        /// </summary>
        public static bool ReadInput(string text, out double val)
        {
            string own = (text ?? "").Trim().Replace(',', '.');
            // Keep only the beginning: digits, minus sign and point.
            int end = 0;
            while (end < own.Length && ("0123456789.-".IndexOf(own[end]) >= 0))
            {
                end++;
            }
            return double.TryParse(own.Substring(0, end), NumberStyles.Float, Format, out val);
        }

        private static int CountDecimals(double step)
        {
            string text = step.ToString("0.########", Format);
            int point = text.IndexOf('.');
            return point < 0 ? 0 : text.Length - point - 1;
        }
    }

    /// <summary>A tab of settings mode: its name, its symbol and its settings (five at most).</summary>
    internal sealed class SettingsTab
    {
        public SettingsTab(string name, string symbol, params Setting[] settingsList)
        {
            Name = name;
            Symbol = symbol;
            Settings = new List<Setting>(settingsList);
        }

        /// <summary>The key of the tab ("tab.general"…): its identity, and its text key in the language files.</summary>
        public string Name { get; private set; }

        /// <summary>The displayed name, in the current language.</summary>
        public string Label
        {
            get { return Texts.Get(Name); }
        }

        /// <summary>Symbol of the Windows icon font (Segoe Fluent Icons / Segoe MDL2 Assets).</summary>
        public string Symbol { get; private set; }

        public List<Setting> Settings { get; private set; }
    }

    /// <summary>
    /// A CATEGORY of the left column of the settings (user request of 2026-11-02: "fewer
    /// buttons on the left"). A category groups a few tabs; only the open category shows its
    /// tabs, below it and indented ("collapsible categories", user's choice of 2026-11-02).
    /// </summary>
    internal sealed class SettingsCategory
    {
        public SettingsCategory(string name, string symbol, params string[] tabNames)
        {
            Name = name;
            Symbol = symbol;
            TabNames = new List<string>(tabNames);
        }

        /// <summary>The key of the category ("category.gaze"…), also its text key in the language files.</summary>
        public string Name { get; private set; }

        /// <summary>The displayed name, in the current language.</summary>
        public string Label
        {
            get { return Texts.Get(Name); }
        }

        /// <summary>Symbol of the Windows icon font (Segoe Fluent Icons / Segoe MDL2 Assets).</summary>
        public string Symbol { get; private set; }

        /// <summary>The keys of the tabs of the category, in display order.</summary>
        public List<string> TabNames { get; private set; }
    }

    /// <summary>
    /// THE LIST OF TABS AND SETTINGS of settings mode (step 7).
    ///
    /// Since 2026-11-02, the display order on the left is the one of the CATEGORIES (see
    /// "Categories" below): the "Tabs" list is only used to find a tab by its index. The first
    /// tab of the list ("Regard") is the one that opens by default.
    ///
    /// Step 11: the sizes (anti-jitter, fixation radius, max speed, circle radius, icon size)
    /// are in % OF THE SCREEN HEIGHT (see ScreenMetrics.cs), and the "Display" tab gets
    /// "Interface size".
    ///
    /// Grouping validated by the user on 2026-10-26 (six tabs; "Scroll" added in step 8,
    /// "Game" in step 9).
    ///
    /// To add a setting later: add it to AppSettings.cs (with its default value and its limits
    /// in Fix), then here, in the wanted tab (five at most per tab, so that nothing scrolls).
    /// The "Scroll" tab was added in step 8, the "Game" tab in step 9, the "Icons" tab in
    /// step 10, the "Keyboard" tab in step 12.
    ///
    /// The "Icons" tab is special: it has no "− / +" setting, but a big button that opens the
    /// icon configuration screen of the action choice (IconConfigMode). Its "Default" button
    /// restores the original icon layout.
    ///
    /// The "Tools" tab (last) has no setting either: two big buttons, "Calibration" and
    /// "Diagnostic", which used to be in the menu bar. It has no "Default" button (there is
    /// nothing to restore).
    ///
    /// The "Profiles" tab (step 15, before "Tools") has no setting nor "Default": two big
    /// buttons to save the current settings and load a profile.
    /// </summary>
    internal static class SettingsCatalog
    {
        /// <summary>The name of the "Icons" tab (step 10), recognized by SettingsWindow.</summary>
        public const string IconsTabName = "tab.icons";

        /// <summary>The index of the "Icons" tab in the list (to come back to it after the configuration).</summary>
        public static int IconsTabIndex
        {
            get { return Math.Max(0, Tabs.FindIndex(o => o.Name == IconsTabName)); }
        }

        /// <summary>
        /// The name of the "General" tab, recognized by SettingsWindow (which adds the "My
        /// words" button to it, step 14, user request of 2026-11-02).
        /// </summary>
        public const string GeneralTabName = "tab.general";

        /// <summary>The index of the "General" tab (to come back to it after the "My words" list).</summary>
        public static int GeneralTabIndex
        {
            get { return Math.Max(0, Tabs.FindIndex(o => o.Name == GeneralTabName)); }
        }

        /// <summary>
        /// The name of the "Tools" tab (calibration and diagnostic, moved from the menu bar on
        /// 2026-10-29), recognized by SettingsWindow.
        /// </summary>
        public const string ToolsTabName = "tab.tools";

        /// <summary>
        /// The name of the "Profiles" tab (step 15), recognized by SettingsWindow, which puts two
        /// big buttons there: "Save the current settings" and "Load a profile".
        /// </summary>
        public const string ProfilesTabName = "tab.profiles";

        /// <summary>The index of the "Profiles" tab (to come back to it after the list of profiles).</summary>
        public static int ProfilesTabIndex
        {
            get { return Math.Max(0, Tabs.FindIndex(o => o.Name == ProfilesTabName)); }
        }

        /// <summary>
        /// THE CATEGORIES of the left column (2026-11-02), in display order, with the names of
        /// their tabs. Each tab of "Tabs" must be in exactly one category (otherwise it could no
        /// longer be reached with the gaze).
        /// Symbols: E890 = open eye ("view"), E7C9 = touching hand ("actions"),
        /// E7F4 = screen, E770 = computer ("system").
        /// </summary>
        public static readonly List<SettingsCategory> Categories = new List<SettingsCategory>
        {
            new SettingsCategory("category.gaze", "\uE890", "tab.gaze", "tab.fixation", "tab.delays"),
            new SettingsCategory("category.actions", "\uE7C9", "tab.magnifierDrag", "tab.scroll", IconsTabName),
            new SettingsCategory("category.display", "\uE7F4", "tab.display", "tab.keyboard", "tab.game"),
            new SettingsCategory("category.system", "\uE770", GeneralTabName, ProfilesTabName, ToolsTabName)
        };

        /// <summary>
        /// The index of the category containing tab number "tab" (0 if none, which should not
        /// happen).
        /// </summary>
        public static int CategoryOf(int tab)
        {
            if (tab < 0 || tab >= Tabs.Count)
            {
                return 0;
            }
            string name = Tabs[tab].Name;
            return Math.Max(0, Categories.FindIndex(c => c.TabNames.Contains(name)));
        }

        /// <summary>
        /// The indexes (in "Tabs") of the tabs of category "category", in display order. A name
        /// that cannot be found is ignored.
        /// </summary>
        public static List<int> TabsOf(int category)
        {
            List<int> numbers = new List<int>();
            foreach (string name in Categories[category].TabNames)
            {
                int num = Tabs.FindIndex(o => o.Name == name);
                if (num >= 0)
                {
                    numbers.Add(num);
                }
            }
            return numbers;
        }

        public static readonly List<SettingsTab> Tabs = new List<SettingsTab>
        {
            // E7B3 = eye ("Gaze")
            new SettingsTab("tab.gaze", "",
                Setting.Number("setting.smoothing", "", 0.01,
                               p => p.Smoothing, (p, v) => p.Smoothing = v),
                Setting.Number("setting.cursorDeadZone", " %", 0.1,
                               p => p.CursorDeadZone, (p, v) => p.CursorDeadZone = v),
                Setting.Number("setting.maxMeasureAge", " ms", 50,
                               p => p.MaxMeasureAge, (p, v) => p.MaxMeasureAge = v),
                Setting.YesOrNo("setting.mouseTracking",
                                 p => p.MouseTracking, (p, v) => p.MouseTracking = v)),

            // E707 = pin ("Fixation": a fixated point)
            new SettingsTab("tab.fixation", "",
                Setting.Number("setting.fixationRadius", " %", 0.1,
                               p => p.FixationRadius, (p, v) => p.FixationRadius = v),
                Setting.Number("setting.fixationDelay", " s", 0.1,
                               p => p.FixationDelay, (p, v) => p.FixationDelay = v),
                Setting.Number("setting.maxFixationSpeed", " %/s", 2,
                               p => p.MaxFixationSpeed, (p, v) => p.MaxFixationSpeed = v),
                Setting.YesOrNo("setting.directClick",
                                 p => p.DirectClick, (p, v) => p.DirectClick = v),
                Setting.Number("setting.directClickDelay", " s", 0.1,
                               p => p.DirectClickDelay, (p, v) => p.DirectClickDelay = v)),

            // E916 = stopwatch ("Delays")
            new SettingsTab("tab.delays", "",
                Setting.Number("setting.activationDelay", " s", 0.1,
                               p => p.ActivationDelay, (p, v) => p.ActivationDelay = v),
                Setting.Number("setting.optionsIconDelay", " s", 0.1,
                               p => p.OptionsIconDelay, (p, v) => p.OptionsIconDelay = v),
                Setting.Number("setting.menuDelay", " s", 0.1,
                               p => p.MenuDelay, (p, v) => p.MenuDelay = v),
                Setting.Number("setting.menuCloseDelay", " s", 0.1,
                               p => p.MenuCloseDelay, (p, v) => p.MenuCloseDelay = v)),

            // E7F4 = screen ("Display")
            new SettingsTab("tab.display", "",
                Setting.Number("setting.circleRadius", " %", 0.2,
                               p => p.CircleRadius, (p, v) => p.CircleRadius = v),
                Setting.Number("setting.iconSize", " %", 0.2,
                               p => p.IconSize, (p, v) => p.IconSize = v),
                Setting.Number("setting.interfaceSize", " %", 25,
                               p => p.InterfaceSize, (p, v) => p.InterfaceSize = v),
                Setting.YesOrNo("setting.showLabels",
                                 p => p.ShowLabels, (p, v) => p.ShowLabels = v)),

            // E71E = magnifier ("Magnifier, drag")
            new SettingsTab("tab.magnifierDrag", "",
                Setting.YesOrNo("setting.magnifierBeforeAction",
                                 p => p.MagnifierBeforeAction, (p, v) => p.MagnifierBeforeAction = v),
                Setting.Number("setting.magnifierZoom", " ×", 0.5,
                               p => p.MagnifierZoom, (p, v) => p.MagnifierZoom = v),
                Setting.Number("setting.androidPauseDuration", " s", 0.1,
                               p => p.AndroidPauseDuration, (p, v) => p.AndroidPauseDuration = v),
                Setting.Number("setting.dragDuration", " s", 0.05,
                               p => p.DragDuration, (p, v) => p.DragDuration = v)),

            // EC8F = up and down arrows ("Scroll", step 8)
            new SettingsTab("tab.scroll", "\uEC8F",
                Setting.Number("setting.maxScrollSpeed", "unit.notchesPerSecond", 1,
                               p => p.MaxScrollSpeed, (p, v) => p.MaxScrollSpeed = v),
                // Windows setting, applied by WheelScroller (2026-11-02).
                Setting.YesOrNo("setting.scrollInactiveWindows",
                                 p => p.ScrollInactiveWindows, (p, v) => p.ScrollInactiveWindows = v,
                                 p => WheelScroller.Apply(p.ScrollInactiveWindows))),

            // E7FC = gamepad ("Game", step 9)
            new SettingsTab("tab.game", "\uE7FC",
                Setting.TwoChoices("setting.relativeMouse",
                                  "setting.relativeMouse.left", "setting.relativeMouse.right",
                                  p => p.RelativeMouse, (p, v) => p.RelativeMouse = v),
                Setting.TwoChoices("setting.gamepadRelative",
                                  "setting.gamepadRelative.left", "setting.gamepadRelative.right",
                                  p => p.GamepadRelative, (p, v) => p.GamepadRelative = v),
                Setting.Number("setting.gameSmoothing", "", 0.01,
                               p => p.GameSmoothing, (p, v) => p.GameSmoothing = v),
                Setting.Number("setting.gameSensitivity", " ×", 0.1,
                               p => p.GameSensitivity, (p, v) => p.GameSensitivity = v)),

            // E71D = grid of squares ("Icons", step 10): no setting, a "Configure icons" button
            // (see SettingsWindow.BuildIconsTab).
            new SettingsTab(IconsTabName, "\uE71D"),

            // E765 = keyboard ("Keyboard", step 12)
            new SettingsTab("tab.keyboard", "\uE765",
                Setting.Number("setting.keyDelay", " s", 0.1,
                               p => p.KeyDelay, (p, v) => p.KeyDelay = v),
                Setting.YesOrNo("setting.keyRepeat",
                                 p => p.KeyRepeat, (p, v) => p.KeyRepeat = v),
                Setting.YesOrNo("setting.autoCapitalize",
                                 p => p.AutoCapitalize, (p, v) => p.AutoCapitalize = v),
                Setting.Number("setting.keyboardHeight", " %", 5,
                               p => p.KeyboardHeight, (p, v) => p.KeyboardHeight = v),
                // 50, 75 or 100 % (Fix rounds to the quarter); also in the keyboard bar.
                Setting.Number("setting.keyboardSize", " %", 25,
                               p => p.KeyboardSize, (p, v) => p.KeyboardSize = v)),

            // E713 = gear ("General")
            new SettingsTab(GeneralTabName, "",
                Setting.YesOrNo("setting.startWithWindows",
                                 p => p.StartWithWindows, (p, v) => p.StartWithWindows = v,
                                 p => WindowsStartup.Apply(p.StartWithWindows))),

            // E77B = person ("Profiles", step 15): no setting, two big buttons "Save the current
            // settings" and "Load a profile" (see SettingsWindow.BuildProfilesTab).
            new SettingsTab(ProfilesTabName, "\uE77B"),

            // E90F = wrench ("Tools", 2026-10-29): no setting, two big buttons "Calibration" and
            // "Diagnostic" (see SettingsWindow.BuildToolsTab).
            new SettingsTab(ToolsTabName, "\uE90F")
        };
    }
}
