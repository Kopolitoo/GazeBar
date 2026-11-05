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
    /// Part of SettingsWindow: what the gaze targets do (change a value, defaults, tabs)
    /// and the keyboard input of a value by the caregiver.
    /// </summary>
    internal sealed partial class SettingsWindow : Window
    {
        // ───────────────────────── Actions ─────────────────────────

        /// <summary>
        /// Gaze on a category: if it is not open yet, it opens on its first tab. If it is
        /// already open, nothing changes (the current tab is not left by mistake).
        /// </summary>
        private void ChooseCategory(int category)
        {
            if (category == SettingsCatalog.CategoryOf(_tab))
            {
                return;
            }
            List<int> tabs = SettingsCatalog.TabsOf(category);
            if (tabs.Count > 0)
            {
                App.LogDetail("Settings: category " + SettingsCatalog.Categories[category].Name);
                ChooseTab(tabs[0]);
            }
        }

        /// <summary>
        /// "Langue" button of the Général tab: switches to the next
        /// language of the Languages folder (in code order, back to the first after the last).
        /// The choice is saved and the page is redrawn at once in the new language. With a
        /// single language file, a message says so.
        /// </summary>
        private void NextLanguage()
        {
            ConfirmInput();
            List<KeyValuePair<string, string>> languages = Texts.Available();
            if (languages.Count < 2)
            {
                _message = Texts.Get("settings.language.onlyOne");
                Build();
                return;
            }
            int index = languages.FindIndex(l => l.Key == Texts.Language);
            string next = languages[(index + 1) % languages.Count].Key;
            P.Language = next;
            P.Save();
            Texts.Load(next);
            App.Log("Settings: Language = " + next);
            _message = null;
            Title = Texts.Get("settings.windowTitle");
            Build();
        }

        private void ChooseTab(int num)
        {
            if (_naming)
            {
                // Another tab (with the mouse, the categories being under the keyboard): the
                // name page is left without saving.
                _naming = false;
                _nameBox = null;
                _nameToReplace = null;
                Notify(NamingEnded);
            }
            _tab = num;
            _confirmation = false;
            _profileToConfirm = null;
            _message = null;
            App.LogDetail("Settings: tab " + SettingsCatalog.Tabs[num].Name);
            Build();
        }

        /// <summary>"−" or "+": only the value text changes (no rebuild).</summary>
        private void ChangeNumber(Setting setting, int direction)
        {
            ConfirmInput();
            setting.Change(P, direction);
            Save(setting);
            ValueFrame frame;
            if (_frames.TryGetValue(setting, out frame))
            {
                frame.Value.Text = setting.Text(P);
            }
        }

        private void ChangeYesNo(Setting setting, bool val)
        {
            if (setting.ReadYesNo(P) == val)
            {
                return;   // already chosen: nothing to do
            }
            setting.WriteYesNo(P, val);
            Save(setting);
            Build();   // to redraw the switch (or frame the new choice)
        }

        private void RequestDefaults()
        {
            _confirmation = true;
            Build();
        }

        private void CancelDefaults()
        {
            _confirmation = false;
            Build();
        }

        /// <summary>Restores the tab's settings to their initial value (read from a new object).</summary>
        private void RestoreDefaults(SettingsTab tab)
        {
            AppSettings defaults = new AppSettings();
            if (tab.Name == SettingsCatalog.IconsTabName)
            {
                P.DefaultIcons();   // the "Icônes" tab has no setting, but a layout
            }
            foreach (Setting setting in tab.Settings)
            {
                setting.Copy(defaults, P);
                if (setting.AfterChange != null)
                {
                    setting.AfterChange(P);
                }
            }
            P.Save();
            App.Log("Settings: default values of tab " + tab.Name);
            _confirmation = false;
            Build();
        }

        /// <summary>Saves the file after a change, and logs it.</summary>
        private static void Save(Setting setting)
        {
            if (setting.AfterChange != null)
            {
                setting.AfterChange(P);
            }
            P.Save();
            App.Log("Settings: " + setting.Key + " = " + setting.Text(P));
        }

        // ───────────────────────── Keyboard input (for the caregiver) ─────────────────────────

        /// <summary>
        /// Replaces the value display with a text box where the caregiver types (or dictates)
        /// a value. Enter confirms; clicking elsewhere also confirms; Escape cancels (see
        /// CancelInput, called by App when the Escape key is pressed).
        /// </summary>
        private void StartInput(Setting setting)
        {
            if (_editedSetting == setting)
            {
                return;
            }
            ConfirmInput();

            ValueFrame frame;
            if (!_frames.TryGetValue(setting, out frame))
            {
                return;
            }

            _input = new TextBox();
            _input.Text = setting.NumberText(setting.ReadNumber(P));
            _input.FontSize = 46;
            _input.Width = ValueWidth - 30;
            _input.HorizontalContentAlignment = HorizontalAlignment.Center;
            _input.VerticalAlignment = VerticalAlignment.Center;
            _input.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ConfirmInput();
                    e.Handled = true;
                }
            };
            _input.LostKeyboardFocus += (s, e) => ConfirmInput();

            _editedSetting = setting;
            frame.Border.Child = _input;

            // The window must have the keyboard for typing to reach the text box.
            Activate();
            _input.Focus();
            Keyboard.Focus(_input);
            _input.SelectAll();
        }

        /// <summary>Confirms the input in progress, if any.</summary>
        private void ConfirmInput()
        {
            if (_editedSetting == null)
            {
                return;
            }
            Setting setting = _editedSetting;
            string text = _input.Text;
            _editedSetting = null;   // first, so it is not confirmed twice (focus loss)

            double val;
            if (Setting.ReadInput(text, out val))
            {
                setting.WriteNumber(P, val);   // brought back within its limits by Fix
                Save(setting);
            }
            else
            {
                App.LogDetail("Settings: unreadable input for " + setting.Key);
            }
            RestoreDisplay(setting);
        }

        /// <summary>Puts the normal value display back in place of the text box.</summary>
        private void RestoreDisplay(Setting setting)
        {
            ValueFrame frame;
            if (_frames.TryGetValue(setting, out frame))
            {
                frame.Value.Text = setting.Text(P);
                frame.Border.Child = frame.Display;
            }
            _input = null;
        }
    }
}
