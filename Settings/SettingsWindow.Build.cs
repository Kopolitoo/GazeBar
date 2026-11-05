using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Prunel
{
    /// <summary>
    /// Part of SettingsWindow: building the page (title, categories, tabs, setting rows,
    /// confirmation pages).
    /// </summary>
    internal sealed partial class SettingsWindow : Window
    {
        // ───────────────────────── Building the page ─────────────────────────

        private static AppSettings P
        {
            get { return AppSettings.Current; }
        }

        /// <summary>(Re)builds the whole page for the chosen tab.</summary>
        private void Build()
        {
            // An input in progress is confirmed before rebuilding everything.
            ConfirmInput();

            _content.Children.Clear();
            _targets.Clear();
            _frames.Clear();

            SettingsTab tab = SettingsCatalog.Tabs[_tab];

            // ── The title, at the top.
            TextBlock title = Text(Texts.Format("settings.title", tab.Label), 30, Soft, false);
            Place(title, LeftContent, 26);

            // ── The left column: the "collapsible" CATEGORIES.
            //    All four categories are visible; only the one holding the open tab shows its
            //    tabs, right below it and indented.
            //    At most 4 + 4 = 8 buttons (instead of 12 if all tabs were shown), hence taller
            //    buttons.
            BuildLeftColumn();

            // ── The content: the settings, or the "Défaut" confirmation, or the profile
            //    load confirmation.
            if (_naming)
            {
                BuildNamingPage();
            }
            else if (_confirmation)
            {
                BuildConfirmation(tab);
            }
            else if (_profileToConfirm != null)
            {
                BuildProfileConfirmation();
            }
            else
            {
                for (int i = 0; i < tab.Settings.Count; i++)
                {
                    BuildRow(tab.Settings[i], TopRow + i * StepRow);
                }
                if (tab.Name == SettingsCatalog.GeneralTabName)
                {
                    BuildFileInfo(TopRow + tab.Settings.Count * StepRow);
                    BuildMyWordsButton();
                }
                if (tab.Name == DisplayTabName)
                {
                    BuildScreenInfo(TopRow + tab.Settings.Count * StepRow);
                }
                if (tab.Name == SettingsCatalog.IconsTabName)
                {
                    BuildIconsTab();
                }
                if (tab.Name == SettingsCatalog.ToolsTabName)
                {
                    BuildToolsTab();
                }
                if (tab.Name == SettingsCatalog.ProfilesTabName)
                {
                    BuildProfilesTab();
                }
            }

            // ── The right column: "Défaut" at the top, "Retour" in the middle of the edge.
            //    E72C = circular arrows (restore), E72B = back arrow.
            //    No "Défaut" for a tab with nothing to restore ("Outils", "Profils"): a button
            //    that does nothing would be confusing.
            bool hasDefaults = tab.Settings.Count > 0 || tab.Name == SettingsCatalog.IconsTabName;
            if (hasDefaults)
            {
                AddTarget(RightLeft, 60, RightSide, RightSide, SymbolAndWord("", Texts.Get("settings.defaults")),
                             RequestDefaults, false, false);
            }
            AddTarget(RightLeft, (PageHeight - RightSide) / 2, RightSide, RightSide,
                         SymbolAndWord("", Texts.Get("common.back")), RequestBack, false, false);

            Version++;
        }

        /// <summary>
        /// The left column: the categories, and below the open category its tabs.
        ///
        /// Looking at another category opens it directly on its first tab (a single look, as
        /// before to change tab). Looking at the already open category does nothing. The open
        /// tab is framed as before; the open category has its symbol in colour.
        /// </summary>
        private void BuildLeftColumn()
        {
            int openCategory = SettingsCatalog.CategoryOf(_tab);
            List<int> openTabs = SettingsCatalog.TabsOf(openCategory);

            // Number of rows in the column: all categories + the tabs of the open category.
            // Rows share the page height, without exceeding the maximum height (140): with 8
            // rows (category "Interface" open), each one is about 105 high.
            int rowCount = SettingsCatalog.Categories.Count + openTabs.Count;
            double step = Math.Min(TabMaxHeight + TabGap,
                                  (BottomTabs - TopTabs + TabGap) / Math.Max(1, rowCount));

            double y = TopTabs;
            for (int c = 0; c < SettingsCatalog.Categories.Count; c++)
            {
                SettingsCategory category = SettingsCatalog.Categories[c];
                bool open = c == openCategory;

                // The category: symbol (in colour if open) and name.
                UIElement categoryRow = RowColumn(category.Symbol, category.Label, open, 46, 36, 24);
                int categoryIndex = c;   // copy for the lambda below
                AddTarget(LeftTab, y, TabWidth, step - TabGap, categoryRow,
                             () => ChooseCategory(categoryIndex), false, false);
                y += step;

                if (!open)
                {
                    continue;
                }

                // The tabs of the open category, indented to the right (the indent shows they
                // "belong" to the category above).
                foreach (int tabIndex in openTabs)
                {
                    SettingsTab o = SettingsCatalog.Tabs[tabIndex];
                    UIElement tabRow = RowColumn(o.Symbol, o.Label, tabIndex == _tab, 34, 30, 14);
                    int num = tabIndex;   // copy for the lambda below
                    AddTarget(LeftTab + TabIndent, y, TabWidth - TabIndent, step - TabGap,
                                 tabRow, () => ChooseTab(num), false, tabIndex == _tab);
                    y += step;
                }
            }
        }

        /// <summary>
        /// The content of a left column button: a symbol then a name. Tabs have slightly
        /// smaller text and a shorter margin than categories, so that "Loupe, glisser" fits
        /// in the indented button.
        /// </summary>
        private static UIElement RowColumn(string symbol, string name, bool inColor,
                                              double symbolSize, double nameSize, double margin)
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Margin = new Thickness(margin, 0, 0, 0);
            TextBlock symbolBlock = Text(symbol, symbolSize, inColor ? SettingTarget.Accent : Soft, false);
            symbolBlock.FontFamily = new FontFamily(IconFont);
            symbolBlock.Width = symbolSize + 22;
            symbolBlock.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(symbolBlock);
            TextBlock nameBlock = Text(name, nameSize, White, true);
            nameBlock.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(nameBlock);
            return row;
        }

        /// <summary>A setting row: name and explanation, then the buttons.</summary>
        private void BuildRow(Setting setting, double top)
        {
            // Name and explanation, vertically centred in the row height.
            StackPanel texts = new StackPanel();
            texts.Width = LabelWidth;
            texts.Height = RowHeight;
            StackPanel center = new StackPanel();
            center.VerticalAlignment = VerticalAlignment.Center;
            center.Children.Add(Text(setting.Name, 36, White, true));
            TextBlock explanation = Text(setting.Explanation, 23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            center.Children.Add(explanation);
            Grid textFrame = new Grid();
            textFrame.Width = LabelWidth;
            textFrame.Height = RowHeight;
            textFrame.Children.Add(center);
            Place(textFrame, LeftContent, top);

            double x = LeftContent + LabelWidth + Gap;
            double y = top + (RowHeight - SideButton) / 2;

            if (setting.Switch)
            {
                // A yes / no: a single big target; looking at it toggles the value.
                // SettingsMode blocks the target until the gaze leaves it: staying on it
                // therefore does not toggle the switch over and over.
                bool current = setting.ReadYesNo(P);
                AddTarget(x, y, 2 * ChoiceWidth + Gap, SideButton, SwitchDrawing(current, current ? setting.RightWord : setting.WrongWord),
                             () => ChangeYesNo(setting, !current), false, false);
                return;
            }

            if (setting.YesNo)
            {
                // A choice between two words ("Directe" / "Relative"…): two buttons.
                bool val = setting.ReadYesNo(P);
                AddTarget(x, y, ChoiceWidth, SideButton, Word(setting.WrongWord, 42), () => ChangeYesNo(setting, false), false, !val);
                AddTarget(x + ChoiceWidth + Gap, y, ChoiceWidth, SideButton, Word(setting.RightWord, 42), () => ChangeYesNo(setting, true), false, val);
                return;
            }

            // "−" (U+2212, the true minus sign), the value, "+". Both buttons repeat.
            AddTarget(x, y, SideButton, SideButton, Word("−", 72), () => ChangeNumber(setting, -1), true, false);
            AddTarget(x + SideButton + Gap + ValueWidth + Gap, y, SideButton, SideButton, Word("+", 72),
                         () => ChangeNumber(setting, +1), true, false);

            // The value: a frame that is NOT a gaze target. The caregiver clicks on it to type
            // a value on the keyboard.
            ValueFrame frame = new ValueFrame();
            frame.Value = Text(setting.Text(P), 50, White, true);
            frame.Value.HorizontalAlignment = HorizontalAlignment.Center;
            AppSettings defaults = new AppSettings();
            TextBlock callbackFn = Text(Texts.Format("settings.defaultValue", setting.Text(defaults)), 20, Soft, false);
            callbackFn.HorizontalAlignment = HorizontalAlignment.Center;
            StackPanel display = new StackPanel();
            display.VerticalAlignment = VerticalAlignment.Center;
            display.Children.Add(frame.Value);
            display.Children.Add(callbackFn);
            frame.Display = display;

            frame.Border = new Border();
            frame.Border.Width = ValueWidth;
            frame.Border.Height = SideButton;
            frame.Border.CornerRadius = new CornerRadius(14);
            frame.Border.BorderThickness = new Thickness(2);
            frame.Border.BorderBrush = Border;
            frame.Border.Background = ValueBackground;
            frame.Border.Cursor = Cursors.IBeam;
            frame.Border.ToolTip = Texts.Get("settings.typeValueTooltip");
            frame.Border.Child = display;
            frame.Border.MouseLeftButtonUp += (s, e) =>
            {
                StartInput(setting);
                e.Handled = true;
            };
            Place(frame.Border, x + SideButton + Gap, y);
            _frames[setting] = frame;
        }

        /// <summary>Name of the "Affichage" tab, below which the detected screen is recalled.</summary>
        private const string DisplayTabName = "tab.display";

        /// <summary>
        /// "Affichage" tab: the screen detected by Prunel (Windows resolution,
        /// scaling and text size), to check that the change was seen.
        /// </summary>
        private void BuildScreenInfo(double top)
        {
            StackPanel info = new StackPanel();
            info.Children.Add(Text(Texts.Get("settings.screenDetected"), 36, White, true));
            TextBlock description = Text(ScreenMetrics.Description(), 24, Soft, false);
            description.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(description);
            info.Children.Add(Text(Texts.Get("settings.sizesInPercent"), 23, Soft, false));
            Place(info, LeftContent, top + 30);
        }

        /// <summary>Général tab: reminder of where the settings are saved.</summary>
        private void BuildFileInfo(double top)
        {
            StackPanel info = new StackPanel();
            info.Children.Add(Text(Texts.Get("settings.file"), 36, White, true));
            TextBlock path = Text(AppSettings.FilePath, 24, Soft, false);
            path.FontFamily = new FontFamily("Consolas");
            path.TextWrapping = TextWrapping.Wrap;
            path.Width = 1140;
            path.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(path);
            info.Children.Add(Text(Texts.Get("settings.savedOnChange"), 23, Soft, false));
            Place(info, LeftContent, top + 30);
        }

        /// <summary>
        /// "Général" tab: a big "Mes mots" button, below
        /// the settings file reminder. It opens the list of words added to the keyboard's
        /// personal dictionary (KeyboardListMode), to remove some. The Clavier tab had no room
        /// left (five settings). E82D = dictionary.
        /// </summary>
        private void BuildMyWordsButton()
        {
            AddTarget(LeftContent, 640, 570, 180, BigButton("\uE82D", Texts.Get("settings.myWords")),
                         () => Notify(MyWordsRequested), false, false);
            // The interface language, next to "Mes mots". E774 = globe.
            AddTarget(LeftContent + 630, 640, 570, 180, BigButton("\uE774", Texts.LanguageName),
                         NextLanguage, false, false);
            // Below the buttons: the message of the language button if there is one, otherwise
            // the explanation.
            TextBlock explanation = string.IsNullOrEmpty(_message)
                ? Text(Texts.Get("settings.myWordsHelp"), 23, Soft, false)
                : Text(_message, 30, SettingTarget.Accent, true);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            Place(explanation, LeftContent, 840);
        }

        /// <summary>
        /// "Icônes" tab: an explanation, then a big button that opens the action
        /// choice icon configuration screen. E71D = grid of squares.
        /// </summary>
        private void BuildIconsTab()
        {
            StackPanel texts = new StackPanel();
            texts.Children.Add(Text(Texts.Get("settings.icons.title"), 36, White, true));
            TextBlock explanation = Text(
                Texts.Get("settings.icons.help"), 23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            texts.Children.Add(explanation);
            Place(texts, LeftContent, TopRow + 20);

            StackPanel button = new StackPanel();
            button.Orientation = Orientation.Horizontal;
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.VerticalAlignment = VerticalAlignment.Center;
            TextBlock symbol = Text("\uE71D", 60, White, false);
            symbol.FontFamily = new FontFamily(IconFont);
            symbol.VerticalAlignment = VerticalAlignment.Center;
            symbol.Margin = new Thickness(0, 0, 30, 0);
            button.Children.Add(symbol);
            TextBlock word = Text(Texts.Get("settings.icons.configure"), 42, White, true);
            word.VerticalAlignment = VerticalAlignment.Center;
            button.Children.Add(word);

            AddTarget(LeftContent + 170, 420, 800, 180, button, RequestIconConfig, false, false);
        }

        /// <summary>
        /// "Outils" tab: two big buttons one above the other, with a wide gap
        /// between them so that aiming at one does not activate the other.
        ///   E890 = eye (Calibration), E9D9 = diagnostic (curve).
        /// Below the buttons: the possible message (calibration impossible…).
        /// </summary>
        private void BuildToolsTab()
        {
            StackPanel texts = new StackPanel();
            texts.Children.Add(Text(Texts.Get("tab.tools"), 36, White, true));
            TextBlock explanation = Text(
                Texts.Get("settings.tools.help"),
                23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            texts.Children.Add(explanation);
            Place(texts, LeftContent, TopRow + 20);

            AddTarget(LeftContent + 170, 320, 800, 180, BigButton("\uE890", Texts.Get("settings.tools.calibration")),
                         () => Notify(CalibrationRequested), false, false);
            AddTarget(LeftContent + 170, 620, 800, 180, BigButton("\uE9D9", Texts.Get("settings.tools.diagnostic")),
                         () => Notify(DiagnosticRequested), false, false);

            BuildMessage();
        }

        /// <summary>The tab's possible message (below the big buttons), in blue.</summary>
        private void BuildMessage()
        {
            if (!string.IsNullOrEmpty(_message))
            {
                TextBlock message = Text(_message, 30, SettingTarget.Accent, true);
                message.TextWrapping = TextWrapping.Wrap;
                message.Width = 1140;
                Place(message, LeftContent, 870);
            }
        }

        /// <summary>The content of a big button: a symbol on the left, then a word.</summary>
        private static UIElement BigButton(string symbol, string word)
        {
            StackPanel button = new StackPanel();
            button.Orientation = Orientation.Horizontal;
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.VerticalAlignment = VerticalAlignment.Center;
            TextBlock icon = Text(symbol, 60, White, false);
            icon.FontFamily = new FontFamily(IconFont);
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 30, 0);
            button.Children.Add(icon);
            TextBlock text = Text(word, 42, White, true);
            text.VerticalAlignment = VerticalAlignment.Center;
            button.Children.Add(text);
            return button;
        }

        /// <summary>
        /// Notifies the mode (raises the given event, if it has subscribers). An input in
        /// progress is confirmed first.
        /// </summary>
        private void Notify(Action evt)
        {
            ConfirmInput();
            if (evt != null)
            {
                evt();
            }
        }

        private void RequestIconConfig()
        {
            ConfirmInput();
            Action subscribers = IconConfigRequested;
            if (subscribers != null)
            {
                subscribers();
            }
        }

        /// <summary>
        /// The "Défaut" confirmation. "Oui" and "Non" are in the middle of the page, far from
        /// the "Défaut" button (top right): confirmation never happens by simply keeping the
        /// gaze at the same place.
        /// </summary>
        private void BuildConfirmation(SettingsTab tab)
        {
            string questionText = tab.Name == SettingsCatalog.IconsTabName
                ? Texts.Get("settings.icons.defaultsQuestion")
                : Texts.Format("settings.defaultsQuestion", tab.Label);
            TextBlock question = Text(questionText, 40, White, true);
            question.TextWrapping = TextWrapping.Wrap;
            question.Width = 1140;
            Place(question, LeftContent, 300);

            AddTarget(640, 480, 300, SideButton, Word(Texts.Get("common.yes"), 42), () => RestoreDefaults(tab), false, false);
            AddTarget(1040, 480, 300, SideButton, Word(Texts.Get("common.no"), 42), CancelDefaults, false, false);
        }
    }
}
