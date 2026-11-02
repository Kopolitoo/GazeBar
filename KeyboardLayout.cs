using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace GazeBar
{
    /// <summary>
    /// MODEL: a gaze keyboard layout (step 12, "clavier-base"), read from a JSON file of the
    /// Keyboards folder (for example Keyboards/en-qwerty.json, Keyboards/fr-azerty.json).
    ///
    /// Why a file and not code? To be able to add a language or change a key later WITHOUT
    /// recompiling: it is enough to edit or add a file (user request of 2026-10-31). The file
    /// is read again each time the keyboard opens: a change shows at the next opening.
    ///
    /// The keyboard is a GRID: [Columns] columns by [Rows] rows (12 × 4 for French). Row 0 is
    /// the top one (Tab, word suggestions, delete...).
    /// Each page (letters, accents, numbers, symbols, functions) lists its keys; each key says
    /// where it is (row, col, width in cells) and what it does.
    ///
    /// What a key does (only one of these lines per key):
    ///   "text": "é"            types this text (in Unicode: see KeyboardInjector.TypeText);
    ///   "key": "Tab"           presses a real key (Tab, Enter, Escape, Delete, Home, End,
    ///                          PageUp, PageDown, Left, Right, Up, Down, F1 to F12,
    ///                          Windows = the Windows key alone, which opens the Start menu);
    ///   "shortcut": "Ctrl+F"   a shortcut (Ctrl, Alt, Shift or Win + a key);
    ///   "page": "accents"      shows another page ("back" = the previous page);
    ///   "modifier": "Shift"    Shift, CapsLock, Ctrl, Alt or Win;
    ///   "prediction": 1        word suggestion cell no. 1, 2 or 3 (filled by the prediction, step 13);
    ///   "command": "DeleteWord"  DeleteWord or DeleteLetter.
    /// And for the look:
    ///   "label"      the text shown on the key;
    ///   "shiftText"  optional: the text typed with Shift or Caps Lock (otherwise: the text in
    ///                uppercase, é → É);
    ///   "style"      optional: "light" (light gray background, like the digits), "function"
    ///                (dark background) or "letter" (no background). Default: "letter" for a
    ///                key that types text, "function" for the others.
    ///
    /// The file is read with DataContractJsonSerializer, like settings.json (shipped with
    /// .NET Framework: no extra dependency). Note: JSON does not allow comments; that is why
    /// the format is explained here.
    /// </summary>
    [DataContract(Name = "Layout", Namespace = "")]
    internal sealed class KeyboardLayout
    {
        /// <summary>The name of the layouts folder, next to GazeBar.exe.</summary>
        public const string FolderName = "Keyboards";

        /// <summary>
        /// The default layout (English since step clavier-anglais; fr-azerty before). Since
        /// step 14, another one is chosen with the "Language" button of the keyboard bar
        /// ([KeyboardLayout] in settings.json).
        /// </summary>
        public const string DefaultName = "en-qwerty";

        /// <summary>The displayed name, for example "Français AZERTY".</summary>
        [DataMember(Name = "name", Order = 0)]
        public string Name { get; set; }

        /// <summary>The language of the word dictionary ("fr"), for the prediction (step 13).</summary>
        [DataMember(Name = "language", Order = 1)]
        public string Language { get; set; }

        /// <summary>Number of columns of the grid (12 for French).</summary>
        [DataMember(Name = "columns", Order = 2)]
        public int Columns { get; set; }

        /// <summary>Number of rows of the grid, top row included (4 for French).</summary>
        [DataMember(Name = "rows", Order = 3)]
        public int Rows { get; set; }

        /// <summary>The name of the page shown when the keyboard opens.</summary>
        [DataMember(Name = "startPage", Order = 4)]
        public string StartPage { get; set; }

        [DataMember(Name = "pages", Order = 5)]
        public List<KeyboardPage> Pages { get; set; }

        /// <summary>
        /// Optional ("apostropheInWord": true), step clavier-anglais: an apostrophe typed after
        /// a letter stays INSIDE the word, for English contractions ("don't", "I'm"): the
        /// prediction keeps suggesting "don't" after "don'". Absent or false (French): the
        /// apostrophe ends an elided word ("l'", "qu'") and the next word starts.
        /// </summary>
        [DataMember(Name = "apostropheInWord", Order = 6)]
        public bool ApostropheInWord { get; set; }

        /// <summary>The page with this name, or null.</summary>
        public KeyboardPage Page(string name)
        {
            if (Pages == null)
            {
                return null;
            }
            foreach (KeyboardPage page in Pages)
            {
                if (string.Equals(page.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return page;
                }
            }
            return null;
        }

        /// <summary>The full path of a layout file (next to GazeBar.exe).</summary>
        public static string PathOf(string name)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName, name + ".json");
        }

        /// <summary>
        /// Reads a layout. Returns null if the file is missing or unreadable: error explained in
        /// "message" (shown in the menu bar) and written to the log.
        /// </summary>
        public static KeyboardLayout Load(string name, out string message)
        {
            string path = PathOf(name);
            message = null;
            if (!File.Exists(path))
            {
                message = Texts.Format("keyboard.layoutNotFound", name + ".json");
                App.Log("Keyboard: file not found: " + path);
                return null;
            }

            KeyboardLayout layout;
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(KeyboardLayout));
                using (FileStream stream = File.OpenRead(path))
                {
                    layout = (KeyboardLayout)serializer.ReadObject(stream);
                }
            }
            catch (Exception ex)
            {
                // A typo in the JSON (forgotten comma, quote...): say so.
                message = Texts.Format("keyboard.layoutUnreadable", name + ".json");
                App.Log("Keyboard: unreadable file " + path + ": " + ex.Message);
                return null;
            }

            string error = layout.Check();
            if (error != null)
            {
                message = Texts.Get("keyboard.layoutInvalid");
                App.Log("Keyboard: " + name + ".json: " + error);
                return null;
            }

            App.Log("Keyboard: layout \"" + layout.Name + "\" read (" + layout.Pages.Count + " pages)");
            return layout;
        }

        /// <summary>
        /// Step 14 ("Language" button): all the layouts of the Keyboards folder that read
        /// correctly, sorted by displayed name. An unreadable or incorrect layout is not offered
        /// (the reason is written to the log by Load).
        /// The file name (without ".json") is the one saved in settings.json.
        /// </summary>
        public static List<FoundLayout> List()
        {
            List<FoundLayout> result = new List<FoundLayout>();
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName);
            string[] files;
            try
            {
                files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json") : new string[0];
            }
            catch (Exception ex)
            {
                App.Log("Keyboard: layouts folder unreadable: " + ex.Message);
                return result;
            }

            foreach (string path in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                string message;
                KeyboardLayout layout = Load(fileName, out message);
                if (layout != null)
                {
                    result.Add(new FoundLayout
                    {
                        File = fileName,
                        // No name in the file: show the file name.
                        Name = string.IsNullOrWhiteSpace(layout.Name) ? fileName : layout.Name,
                        Language = string.IsNullOrWhiteSpace(layout.Language) ? "?" : layout.Language.Trim()
                    });
                }
            }
            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// <summary>
        /// Checks that the layout is usable. Returns null if all is well, otherwise a sentence
        /// explaining the problem (for the log). A key outside the grid is only reported in
        /// the log and ignored: the rest of the keyboard stays usable.
        /// </summary>
        private string Check()
        {
            if (Columns < 1 || Columns > 30 || Rows < 1 || Rows > 10)
            {
                return "impossible grid size (" + Columns + " × " + Rows + ")";
            }
            if (Pages == null || Pages.Count == 0)
            {
                return "no page";
            }
            if (Page(StartPage) == null)
            {
                return "start page \"" + StartPage + "\" does not exist";
            }
            foreach (KeyboardPage page in Pages)
            {
                if (page.Keys == null)
                {
                    page.Keys = new List<LayoutKey>();
                }
                page.Keys.RemoveAll(t =>
                {
                    bool outside = t == null || t.Row < 0 || t.Row >= Rows || t.Col < 0 ||
                                  t.Col + t.CellWidth > Columns;
                    if (outside && t != null)
                    {
                        App.Log("Keyboard: key \"" + t.Label + "\" of page " + page.Name +
                                " outside the grid (row " + t.Row + ", col " + t.Col + "): ignored");
                    }
                    return outside;
                });
            }
            return null;
        }
    }

    /// <summary>A layout found in the Keyboards folder (step 14, "Language" list).</summary>
    internal sealed class FoundLayout
    {
        /// <summary>The file name without ".json" ("fr-azerty").</summary>
        public string File { get; set; }

        /// <summary>The displayed name ("Français AZERTY").</summary>
        public string Name { get; set; }

        /// <summary>The dictionary language ("fr").</summary>
        public string Language { get; set; }
    }

    /// <summary>A keyboard page: its name and its keys.</summary>
    [DataContract(Name = "Page", Namespace = "")]
    internal sealed class KeyboardPage
    {
        [DataMember(Name = "name", Order = 0)]
        public string Name { get; set; }

        [DataMember(Name = "keys", Order = 1)]
        public List<LayoutKey> Keys { get; set; }
    }

    /// <summary>
    /// A key as written in the file (see KeyboardLayout for the meaning of each field).
    /// Fields missing from the file are null.
    /// "int?": a number that may be missing (null).
    /// </summary>
    [DataContract(Name = "Key", Namespace = "")]
    internal sealed class LayoutKey
    {
        [DataMember(Name = "row", Order = 0)]
        public int Row { get; set; }

        [DataMember(Name = "col", Order = 1)]
        public int Col { get; set; }

        [DataMember(Name = "width", Order = 2, EmitDefaultValue = false)]
        public int? Width { get; set; }

        [DataMember(Name = "label", Order = 3, EmitDefaultValue = false)]
        public string Label { get; set; }

        [DataMember(Name = "text", Order = 4, EmitDefaultValue = false)]
        public string Text { get; set; }

        [DataMember(Name = "shiftText", Order = 5, EmitDefaultValue = false)]
        public string ShiftText { get; set; }

        [DataMember(Name = "key", Order = 6, EmitDefaultValue = false)]
        public string Key { get; set; }

        [DataMember(Name = "shortcut", Order = 7, EmitDefaultValue = false)]
        public string Shortcut { get; set; }

        [DataMember(Name = "page", Order = 8, EmitDefaultValue = false)]
        public string Page { get; set; }

        [DataMember(Name = "modifier", Order = 9, EmitDefaultValue = false)]
        public string Modifier { get; set; }

        [DataMember(Name = "prediction", Order = 10, EmitDefaultValue = false)]
        public int? Prediction { get; set; }

        [DataMember(Name = "command", Order = 11, EmitDefaultValue = false)]
        public string Command { get; set; }

        [DataMember(Name = "style", Order = 12, EmitDefaultValue = false)]
        public string Style { get; set; }

        /// <summary>The width in cells (1 if missing or incorrect).</summary>
        public int CellWidth
        {
            get { return Width.HasValue && Width.Value >= 1 ? Width.Value : 1; }
        }
    }
}
