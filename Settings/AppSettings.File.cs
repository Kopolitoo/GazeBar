using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace Prunel
{
    /// <summary>
    /// Part of AppSettings: reading and saving the settings file (settings.json, profiles).
    /// </summary>
    internal sealed partial class AppSettings
    {
        // ───────────────────────── Reading and saving ─────────────────────────

        /// <summary>Full path of the settings file.</summary>
        public static string FilePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Prunel");
                return Path.Combine(folder, "settings.json");
            }
        }

        /// <summary>Call once at startup: reads the file and fills AppSettings.Current.</summary>
        public static void Initialize()
        {
            Current = Load();
        }

        /// <summary>
        /// Replaces all current settings at once with "newItems" (a
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
        /// Used at startup (settings.json) and to load a profile.
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
            // Updating an old file comes BEFORE Fix: the old sizes in pixels (190…)
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
        /// Saves the settings to the file "path" (also used to write a profile in
        /// Documents\Prunel\Profiles). The folder is created if it does not exist.
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
