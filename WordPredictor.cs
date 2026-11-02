using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Xml;

namespace GazeBar
{
    /// <summary>
    /// LOGIC: word prediction for the gaze keyboard (step 13, "clavier-prediction").
    ///
    /// It answers one question: "the user typed these letters, which are the 3 most likely
    /// words that start this way?" (method Suggest).
    ///
    /// Two word sources:
    ///   1. the PRIMARY DICTIONARY of the keyboard layout's language, Dictionaries/<language>.txt:
    ///      fr.txt (taken from Lexique 3.83, CC BY-SA licence, see
    ///      Dictionaries/LICENSE-Lexique.txt, about 125,000 words) or en.txt (taken from
    ///      FrequencyWords / OpenSubtitles, CC BY-SA licence, see
    ///      Dictionaries/LICENSE-FrequencyWords.txt, about 62,000 words). Each word has its
    ///      frequency (occurrences per million words: films and books for French, film
    ///      subtitles for English). It is copied next to
    ///      GazeBar.exe and read once, at startup, in the background (about half a second,
    ///      without blocking the gaze);
    ///   2. the PERSONAL DICTIONARY, %APPDATA%\GazeBar\personal-dictionary.json, next to
    ///      settings.json. It is NEVER in the public repository: it holds the words added by
    ///      the user and how many times each word was used (the "learning").
    ///
    /// Ranking of a word (specification, validated on 2026-11-01):
    ///     score = frequency in Lexique + [UsageBonus] × number of uses
    /// Usual words therefore rise quickly. An added word (not in Lexique) starts at 0.
    ///
    /// NEXT WORD (user request of 2026-11-02): the prediction also remembers PAIRS of words
    /// written one after the other ("je" → "suis", "l'" → "homme"), in the personal
    /// dictionary. Right after a space, the 3 boxes show the words that most often followed
    /// the previous word; while typing, these words gain [FollowBonus] per occurrence.
    /// Lexique holds no pairs: at first there is nothing to suggest after a space, the
    /// prediction grows as sentences are written.
    ///
    /// Search IGNORES accents and case: each word has a "key" (lower case, no accents, see
    /// Key); "ete" finds "été". Keys are sorted alphabetically: the first one starting with
    /// what was typed is found by BINARY SEARCH (halve the list, then halve again…: 17 steps
    /// for 125,000 words), then the following ones are read while they share the prefix.
    ///
    /// Saving the personal dictionary: right after each added word; usage counts once a
    /// minute if they changed, and when GazeBar closes (to avoid writing to disk on every
    /// word).
    ///
    /// Typed text is never written to the log (privacy).
    /// </summary>
    internal sealed class WordPredictor
    {
        /// <summary>Name of the dictionaries folder, next to GazeBar.exe.</summary>
        public const string FolderName = "Dictionaries";

        /// <summary>
        /// Value of ONE use, in Lexique "occurrences per million". With 20, a rare word used
        /// 5 times (score 100) overtakes most common words with the same start, but not "de"
        /// (64,000) or "pas" (27,000).
        /// To tune after testing if usual words rise too fast or too slowly.
        /// </summary>
        private const double UsageBonus = 20;

        /// <summary>
        /// Value of ONE time the word followed the previous word, while typing: 10 times a
        /// plain use, because the previous word says a lot. Example: after "je", typing "s",
        /// "suis" already written 5 times after "je" gains 1,000.
        /// To tune after testing.
        /// </summary>
        private const double FollowBonus = 200;

        /// <summary>Number of suggestion boxes.</summary>
        public const int SuggestionCount = 3;

        /// <summary>Delay between two saves of the usage counts.</summary>
        private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(1);

        // ── The primary dictionary, sorted by key. The three arrays go together:
        //    Keys[i] is the key of word Words[i], whose frequency is Frequencies[i].
        //    "volatile": they are filled by another thread (the background load), then read
        //    by the UI thread. Everything is replaced at once (a single Tables object), so a
        //    mix of old and new is never read.
        private sealed class Tables
        {
            public string[] Keys;
            public string[] Words;
            public double[] Frequencies;
            public bool ApostropheInWord;
        }

        private volatile Tables _primary;

        // The language being loaded (so the same load is not started twice).
        private string _requestedLanguage;

        // ── The personal dictionary (used only by the UI thread).
        //    Key of these dictionaries: the exact word as stored ("bonjour", "Paris").
        private readonly Dictionary<string, int> _usages = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _addedWords = new List<string>();
        private readonly List<string> _addedKeys = new List<string>();

        // Word pairs: for each word (stored form), the words written right after it and how
        // many times. Example: _followers["je"]["suis"] = 12.
        private readonly Dictionary<string, Dictionary<string, int>> _followers =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        // True when usage counts changed since the last save.
        private bool _toSave;

        private readonly DispatcherTimer _saveTimer;

        /// <summary>
        /// Create from the UI thread (the save timer depends on it).
        /// Reads the personal dictionary right away (small file).
        /// </summary>
        public WordPredictor()
        {
            LoadPersonal();

            _saveTimer = new DispatcherTimer();
            _saveTimer.Interval = SaveInterval;
            _saveTimer.Tick += (s, e) =>
            {
                if (_toSave)
                {
                    SavePersonal();
                }
            };
            _saveTimer.Start();
        }

        /// <summary>True when the primary dictionary is loaded (otherwise, no suggestion).</summary>
        public bool Ready
        {
            get { return _primary != null; }
        }

        /// <summary>
        /// The line written in a dictionary file to say that, in its language, an apostrophe
        /// after a letter stays INSIDE the word (English contractions: "don't", "I'm"); the
        /// prediction then keeps suggesting "don't" after "don'". Without it (French), the
        /// apostrophe ends an elided word ("l'", "qu'") and the next word starts.
        /// </summary>
        public const string ApostropheInWordLine = "#apostropheInWord";

        /// <summary>True when the loaded dictionary has the [ApostropheInWordLine] line.</summary>
        public bool ApostropheInWord
        {
            get
            {
                Tables t = _primary;
                return t != null && t.ApostropheInWord;
            }
        }

        // ───────────────────────── Loading the primary dictionary ─────────────────────────

        /// <summary>
        /// Starts reading a language's dictionary in the background ("en" →
        /// Dictionaries/en.txt, "fr" → Dictionaries/fr.txt; English if no language is given). Does nothing if this language is already loaded or loading.
        /// Meanwhile, Suggest suggests nothing.
        /// </summary>
        public void LoadLanguage(string language)
        {
            language = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
            if (language == _requestedLanguage)
            {
                return;
            }
            // Language change (step 14, "Langue" button): the old language's dictionary is
            // forgotten at once. Otherwise, if the new language's file were missing, French
            // words would still be suggested.
            bool changement = _requestedLanguage != null;
            _requestedLanguage = language;
            if (changement)
            {
                _primary = null;
            }
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName, language + ".txt");

            // Task.Run: reading happens on another thread, gaze tracking goes on in the
            // meantime.
            Task.Run(() =>
            {
                Tables t = ReadFile(path);
                // The result is kept only if this language is still the requested one (the
                // language may have changed again during the read).
                if (t != null && _requestedLanguage == language)
                {
                    _primary = t;
                }
            });
        }

        /// <summary>
        /// Reads a "word&lt;Tab&gt;frequency" file (one line per word) and sorts it by key.
        /// Returns null (and logs it) if the file is missing or unreadable.
        /// </summary>
        private static Tables ReadFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    App.Log("Prediction: dictionary not found: " + path + " (no suggestions)");
                    return null;
                }

                DateTime start = DateTime.Now;
                List<string> words = new List<string>(130000);
                List<double> frequencies = new List<double>(130000);
                bool apostropheInWord = false;
                foreach (string row in File.ReadLines(path, Encoding.UTF8))
                {
                    if (row.Trim() == ApostropheInWordLine)
                    {
                        apostropheInWord = true;
                        continue;
                    }
                    if (row.Length == 0 || row[0] == '#')
                    {
                        continue;   // empty line or comment
                    }
                    int tab = row.IndexOf('\t');
                    string word = (tab < 0 ? row : row.Substring(0, tab)).Trim();
                    if (word.Length == 0)
                    {
                        continue;
                    }
                    double frequency = 0;
                    if (tab >= 0)
                    {
                        // InvariantCulture: the file writes "12.5" with a point, whatever the
                        // Windows language (in French, Windows would expect "12,5").
                        double.TryParse(row.Substring(tab + 1).Trim(), NumberStyles.Float,
                                        CultureInfo.InvariantCulture, out frequency);
                    }
                    words.Add(word);
                    frequencies.Add(frequency);
                }

                // Compute the keys, then sort the three arrays together by key.
                Tables t = new Tables();
                t.ApostropheInWord = apostropheInWord;
                t.Words = words.ToArray();
                t.Frequencies = frequencies.ToArray();
                t.Keys = new string[t.Words.Length];
                for (int i = 0; i < t.Words.Length; i++)
                {
                    t.Keys[i] = Key(t.Words[i]);
                }
                int[] order = new int[t.Words.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    order[i] = i;
                }
                string[] sortedKeys = (string[])t.Keys.Clone();
                Array.Sort(sortedKeys, order, StringComparer.Ordinal);
                string[] sortedWords = new string[order.Length];
                double[] sortedFrequencies = new double[order.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    sortedWords[i] = t.Words[order[i]];
                    sortedFrequencies[i] = t.Frequencies[order[i]];
                }
                t.Keys = sortedKeys;
                t.Words = sortedWords;
                t.Frequencies = sortedFrequencies;

                App.Log("Prediction: " + t.Words.Length + " words read from " + path + " in " +
                        (int)(DateTime.Now - start).TotalMilliseconds + " ms");
                return t;
            }
            catch (Exception ex)
            {
                App.Log("Prediction: unreadable dictionary " + path + ": " + ex.Message);
                return null;
            }
        }

        // ───────────────────────── Suggestions ─────────────────────────

        /// <summary>
        /// The (at most) 3 most likely words starting with "typed" (ignoring accents and
        /// case), from most to least likely.
        /// Only words at least as long as what was typed are suggested ("ete" suggests "été",
        /// rule validated on 2026-11-02). If what was typed is itself a known word, it goes in
        /// the 1st box (so looking at it writes the space after it). Words are returned as
        /// stored: the keyboard adds the capital letter if needed.
        ///
        /// unknownWord: true if "typed" (2 letters or more) matches no known word, neither
        /// whole nor as a word start: the keyboard then offers to add it. Always false while
        /// the primary dictionary is not loaded.
        ///
        /// previous: the word written just before (stored form, returned by RecordUsage), or
        /// null (start of text, after punctuation or a cursor move). With an empty "typed",
        /// the suggestions are the words that most often followed it.
        /// </summary>
        public List<string> Suggest(string typed, string previous, out bool unknownWord)
        {
            List<string> result = new List<string>(SuggestionCount);
            unknownWord = false;
            Tables t = _primary;
            Dictionary<string, int> followers = null;
            if (previous != null)
            {
                _followers.TryGetValue(previous, out followers);
            }

            if (string.IsNullOrEmpty(typed))
            {
                // Next word: nothing typed, suggest what already followed the previous word.
                if (followers != null)
                {
                    List<string> bestNext = new List<string>(SuggestionCount + 1);
                    List<double> nextScores = new List<double>(SuggestionCount + 1);
                    foreach (KeyValuePair<string, int> follower in followers)
                    {
                        Remember(follower.Key, FollowBonus * follower.Value, "", null, bestNext, nextScores);
                    }
                    result.AddRange(bestNext);
                }
                return result;
            }
            if (t == null)
            {
                return result;
            }

            string prefix = Key(typed);
            if (prefix.Length == 0)
            {
                return result;
            }

            // The best candidates found, sorted from best to worst.
            List<string> best = new List<string>(SuggestionCount + 1);
            List<double> scores = new List<double>(SuggestionCount + 1);
            bool known = false;

            // 1. The primary dictionary: the first key starting with the prefix, then all the
            //    following ones while they share it.
            for (int i = FirstKey(t.Keys, prefix); i < t.Keys.Length; i++)
            {
                if (!t.Keys[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    break;
                }
                known = true;
                Remember(t.Words[i], t.Frequencies[i], typed, followers, best, scores);
            }

            // 2. Words added by the user (a few dozen at most: read them all).
            for (int i = 0; i < _addedWords.Count; i++)
            {
                if (_addedKeys[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    known = true;
                    Remember(_addedWords[i], 0, typed, followers, best, scores);
                }
            }

            unknownWord = !known && prefix.Length >= 2;

            // User request of 2026-11-02: if what was typed is already a known word, it comes
            // FIRST. Looking at it simply writes the space after it (faster than looking for
            // the Space key). The other boxes keep the best ones.
            string exactMatch = KnownShape(typed);
            if (exactMatch != null)
            {
                result.Add(exactMatch);
                for (int i = 0; i < best.Count && result.Count < SuggestionCount; i++)
                {
                    result.Add(best[i]);
                }
                return result;
            }

            result.AddRange(best);
            return result;
        }

        /// <summary>
        /// Adds a candidate to the best list if it fits (score = frequency + usage bonus +
        /// follow bonus if this word already followed the previous word). The list stays
        /// sorted and holds at most 3 words. followers: the words that followed the previous
        /// word (or null).
        /// </summary>
        private void Remember(string word, double frequency, string typed, Dictionary<string, int> followers,
                             List<string> best, List<double> scores)
        {
            // Rule validated by the user on 2026-11-02: a word AT LEAST AS LONG as what was
            // typed ("été" for "ete"). The fully typed word is skipped here: Suggest puts it
            // back in the 1st box itself.
            if (word.Length < typed.Length || string.Equals(word, typed, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int usages;
            _usages.TryGetValue(word, out usages);
            double score = frequency + UsageBonus * usages;
            int times;
            if (followers != null && followers.TryGetValue(word, out times))
            {
                score += FollowBonus * times;
            }

            // Find its place: before the first one with a lower score. On equal scores, the
            // shorter word comes first.
            int slot = scores.Count;
            for (int i = 0; i < scores.Count; i++)
            {
                if (score > scores[i] || (score == scores[i] && word.Length < best[i].Length))
                {
                    slot = i;
                    break;
                }
            }
            if (slot >= SuggestionCount)
            {
                return;
            }
            best.Insert(slot, word);
            scores.Insert(slot, score);
            if (best.Count > SuggestionCount)
            {
                best.RemoveAt(SuggestionCount);
                scores.RemoveAt(SuggestionCount);
            }
        }

        /// <summary>
        /// Binary search: index of the first key that is not before "prefix" in alphabetical
        /// order (so the first one that may start with it).
        /// </summary>
        private static int FirstKey(string[] keys, string prefix)
        {
            int bottom = 0;
            int top = keys.Length;   // the answer is between low and high
            while (bottom < top)
            {
                int middle = bottom + (top - bottom) / 2;
                if (string.CompareOrdinal(keys[middle], prefix) < 0)
                {
                    bottom = middle + 1;   // too early: the answer is after the middle
                }
                else
                {
                    top = middle;      // the middle may fit: search before
                }
            }
            return bottom;
        }

        /// <summary>
        /// The search "key" of a word: lower case, no accents (é → e, ç → c, œ → oe,
        /// ’ → '). "Été" and "ete" share the key "ete".
        /// </summary>
        public static string Key(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return "";
            }
            // FormD splits each accented letter into letter + accent (é → e + ´); only the
            // letters are kept.
            string decomposed = word.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(decomposed.Length + 2);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                switch (c)
                {
                    case 'œ': sb.Append("oe"); break;
                    case 'æ': sb.Append("ae"); break;
                    case '’': sb.Append('\''); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ───────────────────────── Learning and added words ─────────────────────────

        /// <summary>
        /// A word has just been written in full (picked in a box, or typed then followed by a
        /// space, punctuation or Enter). If known, its usage count goes up by 1 (saved within
        /// the minute). An unknown word is NOT kept: only the "+" box adds a word
        /// (specification).
        ///
        /// previous: the word written just before (what the previous call returned), or null.
        /// If both are known, the pair "previous → word" is remembered (next word).
        /// Returns the stored form of the word ("Bonjour" → "bonjour"), or null if unknown:
        /// it is the "previous" of the next call.
        /// </summary>
        public string RecordUsage(string word, string previous)
        {
            string shape = KnownShape(word);
            if (shape == null)
            {
                return null;
            }
            int n;
            _usages.TryGetValue(shape, out n);
            _usages[shape] = n + 1;

            if (previous != null)
            {
                Dictionary<string, int> followers;
                if (!_followers.TryGetValue(previous, out followers))
                {
                    followers = new Dictionary<string, int>(StringComparer.Ordinal);
                    _followers[previous] = followers;
                }
                int times;
                followers.TryGetValue(shape, out times);
                followers[shape] = times + 1;
            }
            _toSave = true;
            return shape;
        }

        /// <summary>
        /// Adds a word to the personal dictionary ("+" box), as typed, and saves the file
        /// right away. Does nothing if it is already known.
        /// </summary>
        public void Add(string word)
        {
            if (string.IsNullOrWhiteSpace(word) || KnownShape(word) != null)
            {
                return;
            }
            _addedWords.Add(word);
            _addedKeys.Add(Key(word));
            App.Log("Prediction: one word added to the personal dictionary (" + _addedWords.Count + " in total)");
            SavePersonal();
        }

        /// <summary>
        /// Step 14 ("Mes mots" button): a copy of the list of words added with the "+" box,
        /// in alphabetical order (ignoring accents and case).
        /// </summary>
        public List<string> AddedWords()
        {
            List<string> copyItem = new List<string>(_addedWords);
            copyItem.Sort((a, b) => string.Compare(a, b, CultureInfo.CurrentCulture,
                                                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
            return copyItem;
        }

        /// <summary>
        /// Step 14 ("Mes mots" button): removes a word added by mistake. Its usage count and
        /// the pairs it appears in (next word) are forgotten too, so it is never suggested
        /// again. The file is saved right away. Lexique words are untouched. The word is not
        /// written to the log (privacy).
        /// </summary>
        public void Delete(string word)
        {
            int idx = _addedWords.IndexOf(word);
            if (idx < 0)
            {
                return;
            }
            _addedWords.RemoveAt(idx);
            _addedKeys.RemoveAt(idx);
            _usages.Remove(word);
            _followers.Remove(word);
            foreach (Dictionary<string, int> followers in _followers.Values)
            {
                followers.Remove(word);
            }
            App.Log("Prediction: one word removed from the personal dictionary (" + _addedWords.Count + " left)");
            SavePersonal();
        }

        /// <summary>
        /// The stored form of a typed word ("Bonjour" → "bonjour"), compared ignoring case but
        /// WITH accents; null if the word is in neither dictionary.
        /// </summary>
        private string KnownShape(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return null;
            }
            foreach (string added in _addedWords)
            {
                if (string.Equals(added, word, StringComparison.OrdinalIgnoreCase))
                {
                    return added;
                }
            }
            Tables t = _primary;
            if (t == null)
            {
                return null;
            }
            string key = Key(word);
            for (int i = FirstKey(t.Keys, key); i < t.Keys.Length && t.Keys[i] == key; i++)
            {
                if (string.Equals(t.Words[i], word, StringComparison.OrdinalIgnoreCase))
                {
                    return t.Words[i];
                }
            }
            return null;
        }

        // ───────────────────────── The personal dictionary file ─────────────────────────

        /// <summary>Full path of the personal dictionary, next to settings.json.</summary>
        public static string PersonalPath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(AppSettings.FilePath), "personal-dictionary.json");
            }
        }

        /// <summary>Call when GazeBar closes: saves pending usages.</summary>
        public void Stop()
        {
            _saveTimer.Stop();
            if (_toSave)
            {
                SavePersonal();
            }
        }

        /// <summary>
        /// Reads the personal dictionary. Missing: start from scratch. Unreadable: set it
        /// aside (personal-dictionary.json.unreadable) and start from scratch, as for settings.
        /// </summary>
        private void LoadPersonal()
        {
            string path = PersonalPath;
            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PersonalFile));
                PersonalFile file;
                using (FileStream stream = File.OpenRead(path))
                {
                    file = (PersonalFile)serializer.ReadObject(stream);
                }
                if (file != null && file.Words != null)
                {
                    foreach (PersonalWord m in file.Words)
                    {
                        if (m == null || string.IsNullOrWhiteSpace(m.Word))
                        {
                            continue;
                        }
                        if (m.Added && !_addedWords.Contains(m.Word))
                        {
                            _addedWords.Add(m.Word);
                            _addedKeys.Add(Key(m.Word));
                        }
                        if (m.Usages > 0)
                        {
                            _usages[m.Word] = m.Usages;
                        }
                    }
                }
                if (file != null && file.Followers != null)
                {
                    foreach (WordPair pair in file.Followers)
                    {
                        if (pair == null || string.IsNullOrEmpty(pair.Before) || string.IsNullOrEmpty(pair.After) ||
                            pair.Number <= 0)
                        {
                            continue;
                        }
                        Dictionary<string, int> followers;
                        if (!_followers.TryGetValue(pair.Before, out followers))
                        {
                            followers = new Dictionary<string, int>(StringComparer.Ordinal);
                            _followers[pair.Before] = followers;
                        }
                        followers[pair.After] = pair.Number;
                    }
                }
                App.Log("Prediction: personal dictionary read (" + _addedWords.Count + " added words, " +
                        _usages.Count + " words with usages, " + _followers.Count +
                        " words with followers)");
            }
            catch (Exception ex)
            {
                App.Log("Prediction: unreadable personal dictionary, starting from scratch. " + ex.Message);
                try
                {
                    File.Copy(path, path + ".unreadable", true);
                }
                catch
                {
                    // Never mind the backup copy.
                }
            }
        }

        /// <summary>
        /// Saves the personal dictionary: first to a temporary file, then moved over the old
        /// one (if the computer shuts down mid-write, the old one stays).
        /// </summary>
        private void SavePersonal()
        {
            _toSave = false;
            PersonalFile file = new PersonalFile();
            // Version 2 (2026-11-02): word pairs added (Followers). A version 1 file is read
            // as is: it simply has no pairs.
            file.FileVersion = 2;
            file.Words = new List<PersonalWord>();
            foreach (string word in _addedWords)
            {
                int n;
                _usages.TryGetValue(word, out n);
                file.Words.Add(new PersonalWord { Word = word, Added = true, Usages = n });
            }
            foreach (KeyValuePair<string, int> u in _usages)
            {
                if (!_addedWords.Contains(u.Key))
                {
                    file.Words.Add(new PersonalWord { Word = u.Key, Added = false, Usages = u.Value });
                }
            }

            file.Followers = new List<WordPair>();
            foreach (KeyValuePair<string, Dictionary<string, int>> before in _followers)
            {
                foreach (KeyValuePair<string, int> after in before.Value)
                {
                    file.Followers.Add(new WordPair { Before = before.Key, After = after.Key, Number = after.Value });
                }
            }

            string path = PersonalPath;
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PersonalFile));
                using (FileStream stream = File.Create(temp))
                using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(
                           stream, new UTF8Encoding(false), false, true, "  "))
                {
                    serializer.WriteObject(writer, file);
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
            }
            catch (Exception ex)
            {
                // Never crash the program because of this file.
                App.Log("Prediction: failed to save the personal dictionary: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Content of personal-dictionary.json. Example:
    /// {
    ///   "FileVersion": 2,
    ///   "Words": [
    ///     { "Word": "Kopolito", "Added": true, "Usages": 4 },
    ///     { "Word": "bonjour", "Added": false, "Usages": 12 }
    ///   ],
    ///   "Followers": [
    ///     { "Before": "je", "After": "suis", "Number": 7 }
    ///   ]
    /// }
    /// "Added": true for a word added with the "+" box (not in Lexique).
    /// </summary>
    [DataContract]
    internal sealed class PersonalFile
    {
        [DataMember(Order = 1)]
        public int FileVersion { get; set; }

        [DataMember(Order = 2)]
        public List<PersonalWord> Words { get; set; }

        /// <summary>Word pairs (next word), since version 2.</summary>
        [DataMember(Order = 3)]
        public List<WordPair> Followers { get; set; }
    }

    /// <summary>A word pair: "After" was written right after "Before", "Number" times.</summary>
    [DataContract]
    internal sealed class WordPair
    {
        [DataMember(Order = 1)]
        public string Before { get; set; }

        [DataMember(Order = 2)]
        public string After { get; set; }

        [DataMember(Order = 3)]
        public int Number { get; set; }
    }

    /// <summary>A word of the personal dictionary.</summary>
    [DataContract]
    internal sealed class PersonalWord
    {
        [DataMember(Order = 1)]
        public string Word { get; set; }

        [DataMember(Order = 2)]
        public bool Added { get; set; }

        [DataMember(Order = 3)]
        public int Usages { get; set; }
    }
}
