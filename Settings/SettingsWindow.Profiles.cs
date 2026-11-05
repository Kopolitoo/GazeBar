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
    /// Part of SettingsWindow: the "Profils" tab (save, name and load a profile).
    /// </summary>
    internal sealed partial class SettingsWindow : Window
    {
        /// <summary>
        /// "Profils" tab: an explanation with the profiles folder, then two big
        /// buttons one above the other (same height as those of "Outils", but wider for the
        /// long text, with the same wide gap). E74E = floppy disk (Enregistrer), E8E5 = open
        /// folder (Charger). Below the buttons: the message ("Réglages enregistrés dans…").
        /// </summary>
        private void BuildProfilesTab()
        {
            StackPanel texts = new StackPanel();
            texts.Children.Add(Text(Texts.Get("settings.profiles.title"), 36, White, true));
            TextBlock explanation = Text(
                Texts.Get("settings.profiles.help"),
                23, Soft, false);
            explanation.TextWrapping = TextWrapping.Wrap;
            explanation.Width = 1140;
            explanation.Margin = new Thickness(0, 6, 0, 0);
            texts.Children.Add(explanation);
            TextBlock folder = Text(SettingsProfiles.Folder, 22, Soft, false);
            folder.FontFamily = new FontFamily("Consolas");
            folder.TextWrapping = TextWrapping.Wrap;
            folder.Width = 1140;
            folder.Margin = new Thickness(0, 4, 0, 0);
            texts.Children.Add(folder);
            Place(texts, LeftContent, TopRow + 20);

            AddTarget(LeftContent + 70, 320, 1000, 180, BigButton("\uE74E", Texts.Get("settings.profiles.save")),
                         SaveProfile, false, false);
            AddTarget(LeftContent + 70, 620, 1000, 180, BigButton("\uE8E5", Texts.Get("settings.profiles.load")),
                         () => Notify(ProfileListRequested), false, false);

            BuildMessage();
        }

        /// <summary>
        /// The question "Charger le profil … ?", like the "Défaut" confirmation:
        /// "Oui" and "Non" in the middle of the page. For the automatic backup, the question
        /// rather says the previous settings come back.
        /// </summary>
        private void BuildProfileConfirmation()
        {
            string questionText = _profileToConfirm.IsBackup
                ? Texts.Get("settings.profiles.restoreQuestion")
                : Texts.Format("settings.profiles.loadQuestion", _profileToConfirm.DisplayName);
            TextBlock question = Text(questionText, 40, White, true);
            question.TextWrapping = TextWrapping.Wrap;
            question.Width = 1140;
            Place(question, LeftContent, 260);

            TextBlock precision = Text(
                Texts.Format("settings.profiles.loadHelp", Texts.Get("profiles.backupName")), 23, Soft, false);
            precision.TextWrapping = TextWrapping.Wrap;
            precision.Width = 1140;
            Place(precision, LeftContent, 380);

            AddTarget(640, 480, 300, SideButton, Word(Texts.Get("common.yes"), 42), ConfirmProfile, false, false);
            AddTarget(1040, 480, 300, SideButton, Word(Texts.Get("common.no"), 42), CancelProfile, false, false);
        }

        /// <summary>
        /// "Enregistrer les réglages actuels" button: opens the name page, with the dated
        /// name already written (it can be kept as it is). The mode shows the keyboard.
        /// </summary>
        private void SaveProfile()
        {
            ConfirmInput();
            App.LogDetail("Profiles: name page opened");
            _naming = true;
            _nameText = SettingsProfiles.SuggestedName();
            _selectName = true;
            _nameToReplace = null;
            _nameError = null;
            _message = null;
            Build();
            Notify(NamingStarted);
        }

        /// <summary>
        /// The profile name page, in the part of the page left free by the keyboard:
        ///   - its title;
        ///   - the text box, then "Enregistrer" (E74E = floppy disk) and "Annuler" (E711 =
        ///     cross) on the same row, so that everything fits even above a tall keyboard;
        ///   - below: the help, or the error (empty name…).
        /// Or, when the name already exists, the question "Le remplacer ?" with "Oui / Non".
        /// The box has the keyboard focus: the keys of the gaze keyboard (and of a real
        /// keyboard) reach it. Enter saves.
        /// </summary>
        private void BuildNamingPage()
        {
            _nameBox = null;
            if (_nameToReplace != null)
            {
                const double QuestionHeight = 270;
                double yq = NamingTop(QuestionHeight);
                TextBlock question = Text(Texts.Format("settings.profiles.replaceQuestion", _nameToReplace), 40, White, true);
                question.TextWrapping = TextWrapping.Wrap;
                question.Width = 1140;
                Place(question, LeftContent, yq);
                AddTarget(640, yq + 140, 300, SideButton, Word(Texts.Get("common.yes"), 42), ConfirmReplace, false, false);
                AddTarget(1040, yq + 140, 300, SideButton, Word(Texts.Get("common.no"), 42), CancelReplace, false, false);
                return;
            }

            const double ContentHeight = 260;
            const double RowHeight = 130;
            const double BoxWidth = 680;
            const double ButtonWidth = 240;
            double y = NamingTop(ContentHeight);

            Place(Text(Texts.Get("settings.profiles.nameTitle"), 36, White, true), LeftContent, y);

            TextBox box = new TextBox();
            box.Text = _nameText ?? "";
            box.MaxLength = SettingsProfiles.NameMaxLength;
            box.FontSize = 46;
            box.FontFamily = new FontFamily("Segoe UI");
            box.Width = BoxWidth;
            box.Height = RowHeight;
            box.VerticalContentAlignment = VerticalAlignment.Center;
            box.Padding = new Thickness(16, 0, 16, 0);
            box.TextChanged += (s, e) => _nameText = box.Text;
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    ConfirmName();
                }
            };
            // The focus once the box is really shown (and again if Windows gave it back late).
            box.Loaded += (s, e) => FocusNameBox(box);
            Place(box, LeftContent, y + 60);
            _nameBox = box;

            AddTarget(LeftContent + BoxWidth + 20, y + 60, ButtonWidth, RowHeight,
                         SymbolAndWord("\uE74E", Texts.Get("settings.profiles.nameSave")), ConfirmName, false, false);
            AddTarget(LeftContent + BoxWidth + ButtonWidth + 40, y + 60, ButtonWidth, RowHeight,
                         SymbolAndWord("\uE711", Texts.Get("common.cancel")), CancelNaming, false, false);

            TextBlock below = _nameError == null
                ? Text(Texts.Get("settings.profiles.nameHelp"), 23, Soft, false)
                : Text(_nameError, 28, SettingTarget.Accent, true);
            below.TextWrapping = TextWrapping.Wrap;
            below.Width = 1180;
            Place(below, LeftContent, y + 60 + RowHeight + 14);
        }

        /// <summary>
        /// Top of the name page: centred in the free part of the page, but never above it
        /// (if the keyboard is very tall, the page starts at the top of the free part).
        /// </summary>
        private double NamingTop(double height)
        {
            double free = _freeBottom - _freeTop;
            return _freeTop + Math.Max(10, (free - height) / 2);
        }

        /// <summary>
        /// Gives the keyboard focus to the name box: the window is activated first, otherwise
        /// the simulated keys would go to another window. At the first showing, the whole
        /// suggested name is selected (the first letter typed replaces it); otherwise the text
        /// cursor goes to the end.
        /// </summary>
        private void FocusNameBox(TextBox box)
        {
            if (box != _nameBox)
            {
                return;   // the page was rebuilt in the meantime
            }
            Activate();
            box.Focus();
            Keyboard.Focus(box);
            if (_selectName)
            {
                box.SelectAll();
                _selectName = false;
            }
            else
            {
                box.CaretIndex = box.Text.Length;
            }
        }

        /// <summary>
        /// "Enregistrer" (or Enter): the typed name is cleaned (characters Windows refuses
        /// are removed). Empty or reserved name: the error is shown under the box. Name already
        /// used: the question "Le remplacer ?". Otherwise the profile is saved.
        /// </summary>
        private void ConfirmName()
        {
            if (!_naming || _nameToReplace != null)
            {
                return;
            }
            string error;
            string name = SettingsProfiles.CleanName(_nameText, out error);
            if (error != null)
            {
                _nameError = error;
                Build();
                return;
            }
            if (SettingsProfiles.Exists(name))
            {
                App.LogDetail("Profiles: name already used, asking before replacing");
                _nameToReplace = name;
                Build();
                return;
            }
            SaveNamedProfile(name);
        }

        /// <summary>"Oui" to "Le remplacer ?": the existing profile is replaced.</summary>
        private void ConfirmReplace()
        {
            string name = _nameToReplace;
            _nameToReplace = null;
            if (name != null)
            {
                SaveNamedProfile(name);
            }
        }

        /// <summary>"Non" to "Le remplacer ?": back to the name box, the typed text is kept.</summary>
        private void CancelReplace()
        {
            _nameToReplace = null;
            _nameError = null;
            Build();
        }

        /// <summary>Saves the profile, closes the name page and shows the result.</summary>
        private void SaveNamedProfile(string name)
        {
            string message;
            SettingsProfiles.Save(name, out message);
            EndNaming(message);
        }

        /// <summary>"Annuler": nothing is saved, back to the "Profils" tab.</summary>
        private void CancelNaming()
        {
            App.LogDetail("Profiles: save cancelled");
            EndNaming(null);
        }

        /// <summary>Closes the name page: the mode hides the keyboard, the tab is shown again.</summary>
        private void EndNaming(string message)
        {
            bool wasNaming = _naming;
            _naming = false;
            _nameBox = null;
            _nameText = null;
            _nameToReplace = null;
            _nameError = null;
            _message = message;
            if (wasNaming)
            {
                Notify(NamingEnded);
            }
            Build();
        }

        /// <summary>"Oui": the mode loads the profile (then shows the message here).</summary>
        private void ConfirmProfile()
        {
            FoundProfile profile = _profileToConfirm;
            _profileToConfirm = null;
            Action<FoundProfile> subscribers = ProfileLoadRequested;
            if (subscribers != null && profile != null)
            {
                subscribers(profile);
            }
            else
            {
                Build();
            }
        }

        /// <summary>"Non": back to the "Profils" tab, nothing changes.</summary>
        private void CancelProfile()
        {
            App.LogDetail("Profiles: load cancelled");
            _profileToConfirm = null;
            Build();
        }
    }
}
