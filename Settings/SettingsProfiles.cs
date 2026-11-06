using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Prunel
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

        /// <summary>The name shown to the user: the file name, or "Sauvegarde automatique" (in the current language) for the backup.</summary>
        public string DisplayName
        {
            get { return IsBackup ? Texts.Get("profiles.backupName") : Name; }
        }
    }

    /// <summary>
    /// SETTINGS PROFILES (specification, "profils-parametres").
    ///
    /// A PROFILE is simply a copy of the settings.json file, stored in the folder
    ///     Documents\Prunel\Profiles
    /// (for example C:\Users\Name\Documents\Prunel\Profiles\Réglages 2026-11-02 16h45.json).
    /// This way several sets of settings can be kept ("Evening", "Tired"...) and switched
    /// with the gaze, from the "Profiles" tab of the settings.
    ///
    /// What this class does (design validated by the user):
    ///   - List: the .json files of the folder, the most recent first;
    ///   - Save: writes the current settings under the name typed with the gaze keyboard
    ///     (request; the dated name SuggestedName is offered by default);
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
    ///
    /// Profile names are typed by the user: they are not written to the log.
    /// </summary>
    internal static class SettingsProfiles
    {
        /// <summary>
        /// The FILE name of the profile written automatically before each load. It does not
        /// depend on the language (otherwise changing the language would lose the backup); the
        /// user sees "profiles.backupName" instead (FoundProfile.DisplayName).
        /// </summary>
        public const string BackupName = "auto-backup";

        /// <summary>The profiles folder: Documents\Prunel\Profiles.</summary>
        public static string Folder
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, "Prunel", "Profiles");
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

        /// <summary>Longest profile name that can be typed (characters).</summary>
        public const int NameMaxLength = 60;

        /// <summary>
        /// The name suggested when saving: the date and time, for example
        /// "Réglages 2026-11-02 16h45" (in the interface language). The user can keep it or
        /// type another one.
        /// </summary>
        public static string SuggestedName()
        {
            return Texts.Format("profiles.fileName", DateTime.Now.ToString("yyyy-MM-dd HH'h'mm"));
        }

        /// <summary>
        /// Turns the typed text into a usable file name: the characters Windows refuses in a
        /// file name (\ / : * ? " &lt; &gt; | and line ends) are removed, spaces are
        /// gathered, and spaces and dots at the ends are removed (Windows drops them).
        /// Returns "" if nothing is left. "error" is the sentence to show when the name cannot
        /// be used (empty, too long, reserved), otherwise null.
        /// </summary>
        public static string CleanName(string typed, out string error)
        {
            error = null;
            StringBuilder clean = new StringBuilder();
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in typed ?? "")
            {
                if (char.IsWhiteSpace(c))
                {
                    // One space only, never at the start.
                    if (clean.Length > 0 && clean[clean.Length - 1] != ' ')
                    {
                        clean.Append(' ');
                    }
                }
                else if (Array.IndexOf(invalid, c) < 0)
                {
                    clean.Append(c);
                }
            }
            string name = clean.ToString().Trim(' ', '.');

            if (name.Length == 0)
            {
                error = Texts.Get("profiles.nameEmpty");
            }
            else if (name.Length > NameMaxLength)
            {
                error = Texts.Format("profiles.nameTooLong", NameMaxLength);
            }
            else if (IsReserved(name))
            {
                error = Texts.Format("profiles.nameReserved", name);
            }
            return name;
        }

        /// <summary>
        /// True for a name that cannot be used: the automatic backup (it is replaced at each
        /// load) and the names Windows keeps for devices (CON, PRN, AUX, NUL, COM1…, LPT1…).
        /// </summary>
        private static bool IsReserved(string name)
        {
            string upper = name.ToUpperInvariant();
            if (upper == BackupName.ToUpperInvariant() ||
                upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL")
            {
                return true;
            }
            return upper.Length == 4 && (upper.StartsWith("COM") || upper.StartsWith("LPT")) &&
                   upper[3] >= '1' && upper[3] <= '9';
        }

        /// <summary>True if a profile with this name (already cleaned) exists.</summary>
        public static bool Exists(string name)
        {
            try
            {
                return File.Exists(Path.Combine(Folder, name + ".json"));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes AppSettings.Current into the profile "name" (already cleaned with CleanName).
        /// An existing profile with this name is replaced: the settings window asks first.
        /// Returns true on success; "message" is the sentence to show in the tab.
        /// The name is typed by the user: it is NOT written to the log (typed text is never
        /// logged).
        /// </summary>
        public static bool Save(string name, out string message)
        {
            string path = Path.Combine(Folder, name + ".json");
            if (AppSettings.Current.SaveTo(path))
            {
                App.Log("Profiles: current settings saved in a profile");
                message = Texts.Format("profiles.saved", name);
                return true;
            }
            message = Texts.Get("profiles.saveFailed");
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
                App.Log("Profiles: profile unreadable: " + ex.Message);
                message = Texts.Format("profiles.unreadable", name);
                return false;
            }
            if (updated)
            {
                App.Log("Profiles: profile from an older version, converted");
            }
            else if (newItems.FileVersion > AppSettings.CurrentVersion)
            {
                // Profile written by a newer version of Prunel: the settings unknown to this
                // version are simply ignored.
                App.Log("Profiles: profile from a newer version (" + newItems.FileVersion + ")");
            }

            // 2. Keep the current settings.
            AppSettings currentValues = AppSettings.Current;
            string backupPath = Path.Combine(Folder, BackupName + ".json");
            if (!currentValues.SaveTo(backupPath))
            {
                // Without a backup, we could not go back: we do not insist.
                message = Texts.Get("profiles.backupFailed");
                return false;
            }

            // 3. The computer settings (start with Windows, detailed log) do not change, nor the
            //    interface language (switching language by loading a profile would be a surprise).
            newItems.StartWithWindows = currentValues.StartWithWindows;
            newItems.DetailedLog = currentValues.DetailedLog;
            newItems.Language = currentValues.Language;

            // 4. Apply and save.
            AppSettings.Replace(newItems);
            newItems.Save();
            WheelScroller.Apply(newItems.ScrollInactiveWindows);

            App.Log(name == BackupName ? "Profiles: automatic backup loaded" : "Profiles: profile loaded");
            message = Texts.Format("profiles.loaded", name == BackupName ? Texts.Get("profiles.backupName") : name,
                                   Texts.Get("profiles.backupName"));
            return true;
        }
    }
}
