using System;
using System.Collections.Generic;
using System.IO;

namespace GazeBar
{
    /// <summary>
    /// A profile found in the profiles folder: its displayed name (the file name without
    /// ".json"), its full path and the date it was last modified.
    /// </summary>
    internal sealed class FoundProfile
    {
        public string Name;
        public string Path;
        public DateTime Date;

        /// <summary>True for the "automatic backup" profile (made before each load).</summary>
        public bool IsBackup
        {
            get { return string.Equals(Name, SettingsProfiles.BackupName, StringComparison.OrdinalIgnoreCase); }
        }
    }

    /// <summary>
    /// SETTINGS PROFILES (step 15 of the specification, "profils-parametres").
    ///
    /// A PROFILE is simply a copy of the settings.json file, stored in the folder
    ///     Documents\GazeBar\Profiles
    /// (for example C:\Users\Name\Documents\GazeBar\Profiles\Réglages 2026-11-02 16h45.json).
    /// This way several sets of settings can be kept ("Evening", "Tired"...) and switched
    /// with the gaze, from the "Profiles" tab of the settings.
    ///
    /// What this class does (design validated by the user on 2026-11-02):
    ///   - List: the .json files of the folder, the most recent first;
    ///   - SaveCurrent: writes the current settings under an automatically dated name.
    ///     To give it a real name, the caregiver renames the file in Explorer;
    ///   - Load: reads a profile (a file from an older version is converted, like
    ///     settings.json at startup), first keeps the current settings in the "automatic
    ///     backup" profile (replaced on each load: this way one can go back with the gaze),
    ///     then applies and saves the profile.
    ///
    /// The "Start with Windows" setting is NOT taken from the profile: it concerns the
    /// computer rather than the person. The personal dictionary ("My words") is another
    /// file: it does not change either.
    ///
    /// No deleting or renaming with the gaze: the caregiver does it in Explorer.
    /// </summary>
    internal static class SettingsProfiles
    {
        /// <summary>The name of the profile written automatically before each load.</summary>
        public const string BackupName = "Sauvegarde automatique";

        /// <summary>The profiles folder: Documents\GazeBar\Profiles.</summary>
        public static string Folder
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, "GazeBar", "Profiles");
            }
        }

        /// <summary>
        /// The profiles of the folder, the most recently modified first (so the last one saved
        /// is always on the first page). Empty list if the folder does not exist yet or cannot
        /// be read.
        /// </summary>
        public static List<FoundProfile> List()
        {
            List<FoundProfile> profiles = new List<FoundProfile>();
            try
            {
                if (!Directory.Exists(Folder))
                {
                    return profiles;
                }
                foreach (string path in Directory.GetFiles(Folder, "*.json"))
                {
                    FoundProfile profile = new FoundProfile();
                    profile.Path = path;
                    profile.Name = Path.GetFileNameWithoutExtension(path);
                    profile.Date = File.GetLastWriteTime(path);
                    profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                App.Log("Profiles: unreadable folder: " + ex.Message);
            }
            // Sort: the most recent date first (b before a).
            profiles.Sort((a, b) => b.Date.CompareTo(a.Date));
            return profiles;
        }

        /// <summary>
        /// "Save the current settings" button: writes AppSettings.Current into a new profile
        /// named with the date and time, for example "Réglages 2026-11-02 16h45". If this name
        /// already exists (two saves in the same minute), "(2)", "(3)"... is added.
        /// Returns true on success; "message" is the sentence to show in the tab.
        /// </summary>
        public static bool SaveCurrent(out string message)
        {
            string name = "Réglages " + DateTime.Now.ToString("yyyy-MM-dd HH'h'mm");
            string path = Path.Combine(Folder, name + ".json");
            for (int num = 2; File.Exists(path); num++)
            {
                path = Path.Combine(Folder, name + " (" + num + ").json");
            }
            name = Path.GetFileNameWithoutExtension(path);

            if (AppSettings.Current.SaveTo(path))
            {
                App.Log("Profiles: current settings saved in \"" + name + "\"");
                message = "Réglages enregistrés dans le profil « " + name + " ».";
                return true;
            }
            message = "Impossible d'enregistrer le profil (voir le journal).";
            return false;
        }

        /// <summary>
        /// Loads the profile "path" after confirmation (see SettingsWindow).
        ///   1. The profile is READ first (and converted if it comes from an older version). If
        ///      it cannot be read, nothing changes and we say so.
        ///   2. The current settings are kept in the "automatic backup". The profile is read
        ///      BEFORE writing this backup: this way, reloading the backup itself works (the
        ///      settings before and after are simply swapped).
        ///   3. "Start with Windows" keeps its current value (computer setting).
        ///   4. The profile becomes the current settings, it is saved in settings.json and the
        ///      Windows setting "scroll inactive windows" is applied again.
        /// The keyboard, if it is open, reloads its layout: SettingsMode takes care of it (it
        /// has access to the mode manager).
        /// Returns true if the profile is loaded; "message" is the sentence to show.
        /// </summary>
        public static bool Load(string path, out string message)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            // 1. Read the profile.
            AppSettings newItems;
            bool updated;
            try
            {
                newItems = AppSettings.ReadFile(path, out updated);
            }
            catch (Exception ex)
            {
                App.Log("Profiles: \"" + name + "\" unreadable: " + ex.Message);
                message = "Le profil « " + name + " » est illisible : rien n'a changé.";
                return false;
            }
            if (updated)
            {
                App.Log("Profiles: \"" + name + "\" came from an older version, converted");
            }
            else if (newItems.FileVersion > AppSettings.CurrentVersion)
            {
                // Profile written by a newer version of GazeBar: the settings unknown to this
                // version are simply ignored.
                App.Log("Profiles: \"" + name + "\" comes from a newer version (" + newItems.FileVersion + ")");
            }

            // 2. Keep the current settings.
            AppSettings currentValues = AppSettings.Current;
            string backupPath = Path.Combine(Folder, BackupName + ".json");
            if (!currentValues.SaveTo(backupPath))
            {
                // Without a backup, we could not go back: we do not insist.
                message = "Impossible de garder les réglages actuels : le profil n'a pas été chargé.";
                return false;
            }

            // 3. The computer setting does not change.
            newItems.StartWithWindows = currentValues.StartWithWindows;

            // 4. Apply and save.
            AppSettings.Replace(newItems);
            newItems.Save();
            WheelScroller.Apply(newItems.ScrollInactiveWindows);

            App.Log("Profiles: profile \"" + name + "\" loaded");
            message = "Profil « " + name + " » chargé. Les réglages d'avant sont dans « " + BackupName + " ».";
            return true;
        }
    }
}
