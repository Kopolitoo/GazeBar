using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GazeBar
{
    /// <summary>
    /// LOGIC: the texts shown to the user, in the chosen language (step "langues-socle").
    ///
    /// Every text of the interface lives in a language file of the Languages folder, next to
    /// GazeBar.exe: Languages/en.json, Languages/fr.json… A language file is a flat JSON
    /// object, one entry per text:
    ///     {
    ///       "language.name": "English",
    ///       "menu.settings": "Settings",
    ///       "profiles.loaded": "Profile \"{0}\" loaded."
    ///     }
    /// "{0}", "{1}"… are replaced by values (string.Format), see Format.
    ///
    /// In the code, a text is read by its key: Texts.Get("menu.settings").
    /// If the key is missing from the chosen language, the text of the FALLBACK language
    /// (English, the reference language) is used; if it is missing there too, the key itself is shown, so a
    /// forgotten text is visible instead of crashing. Each missing key is logged once.
    ///
    /// To add a language: copy en.json to xx.json (xx = ISO 639-1 code), translate the values
    /// (never the keys), and put it in the Languages folder. It then appears in the "Langue"
    /// choice of the settings (Général tab).
    /// </summary>
    internal static class Texts
    {
        /// <summary>Name of the language files folder, next to GazeBar.exe.</summary>
        public const string FolderName = "Languages";

        /// <summary>
        /// The reference language: used when a text is missing from the chosen one, and the
        /// default language of a new installation.
        /// </summary>
        public const string FallbackLanguage = "en";

        /// <summary>The key of a language file that holds the language's own name ("English").</summary>
        public const string NameKey = "language.name";

        private static Dictionary<string, string> _current = new Dictionary<string, string>();
        private static Dictionary<string, string> _fallback = new Dictionary<string, string>();
        private static readonly HashSet<string> _reportedMissing = new HashSet<string>();

        /// <summary>The code of the language in use ("fr", "en"…).</summary>
        public static string Language { get; private set; } = FallbackLanguage;

        /// <summary>Raised after the language changed (the interface redraws its texts).</summary>
        public static event Action Changed;

        /// <summary>
        /// Loads a language ("fr" → Languages/fr.json). A missing or unreadable file falls back
        /// to the fallback language. Raises Changed.
        /// </summary>
        public static void Load(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                language = FallbackLanguage;
            }
            language = language.Trim().ToLowerInvariant();

            _fallback = ReadFile(FallbackLanguage) ?? new Dictionary<string, string>();
            Dictionary<string, string> chosen = language == FallbackLanguage ? _fallback : ReadFile(language);
            if (chosen == null)
            {
                App.Log("Texts: language \"" + language + "\" not available, using " + FallbackLanguage);
                chosen = _fallback;
                language = FallbackLanguage;
            }
            _current = chosen;
            Language = language;
            _reportedMissing.Clear();
            App.Log("Texts: language " + language + " (" + _current.Count + " texts)");

            Changed?.Invoke();
        }

        /// <summary>The text of a key in the current language (see the class summary for fallbacks).</summary>
        public static string Get(string key)
        {
            string text;
            if (_current.TryGetValue(key, out text) || _fallback.TryGetValue(key, out text))
            {
                return text;
            }
            if (_reportedMissing.Add(key))
            {
                App.Log("Texts: missing key \"" + key + "\"");
            }
            return key;
        }

        /// <summary>The text of a key with its "{0}", "{1}"… replaced by the given values.</summary>
        public static string Format(string key, params object[] values)
        {
            string pattern = Get(key);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, pattern, values);
            }
            catch (FormatException)
            {
                // A badly written translation ("{1" without "}"…) must not crash GazeBar.
                App.Log("Texts: bad format in \"" + key + "\"");
                return pattern;
            }
        }

        /// <summary>
        /// The available languages: (code, name) for each file of the Languages folder, sorted
        /// by code. The name is the file's "language.name" entry (or the code if it has none).
        /// </summary>
        public static List<KeyValuePair<string, string>> Available()
        {
            List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
            try
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName);
                if (Directory.Exists(folder))
                {
                    List<string> files = new List<string>(Directory.GetFiles(folder, "*.json"));
                    files.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (string file in files)
                    {
                        string code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                        Dictionary<string, string> texts = ReadFile(code);
                        string name;
                        if (texts == null || !texts.TryGetValue(NameKey, out name))
                        {
                            name = code;
                        }
                        result.Add(new KeyValuePair<string, string>(code, name));
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Texts: cannot list languages: " + ex.Message);
            }
            return result;
        }

        /// <summary>The display name of the current language ("English").</summary>
        public static string LanguageName
        {
            get
            {
                string name;
                return _current.TryGetValue(NameKey, out name) ? name : Language;
            }
        }

        /// <summary>Reads Languages/&lt;code&gt;.json, or null if missing or unreadable.</summary>
        private static Dictionary<string, string> ReadFile(string code)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName, code + ".json");
            if (!File.Exists(path))
            {
                App.Log("Texts: file not found: " + path);
                return null;
            }
            try
            {
                // UseSimpleDictionaryFormat: a plain JSON object { "key": "text", … } is read as
                // a Dictionary (otherwise DataContractJsonSerializer expects a list of
                // { "Key": …, "Value": … }). Available since .NET Framework 4.5.
                DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings();
                settings.UseSimpleDictionaryFormat = true;
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(Dictionary<string, string>), settings);
                using (FileStream stream = File.OpenRead(path))
                {
                    Dictionary<string, string> texts = (Dictionary<string, string>)serializer.ReadObject(stream);
                    return texts ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                App.Log("Texts: unreadable file " + path + ": " + ex.Message);
                return null;
            }
        }
    }
}
