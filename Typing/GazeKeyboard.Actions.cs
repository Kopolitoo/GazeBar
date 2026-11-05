using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Prunel
{
    /// <summary>
    /// Part of GazeKeyboard: following the gaze over the keys and what each key does
    /// (letters, special keys, shortcuts, modifiers, pages, word suggestions).
    /// </summary>
    internal sealed partial class GazeKeyboard
    {
        // ───────────────────────── Gaze ─────────────────────────

        /// <summary>
        /// Call at every movement mode tick, while the keyboard is shown: updates the hovered
        /// key and activates it if the gaze stayed on it for [KeyDelay].
        /// </summary>
        public void Update(GazeState gaze)
        {
            if (!_visible)
            {
                return;
            }

            AppSettings p = AppSettings.Current;
            int num = _aimer.Update(gaze, p.KeyDelay, p.ShowLabels);
            if (num < 0)
            {
                return;
            }

            if (num >= _keys.Count)
            {
                // The last one in the list: the Close button.
                Close();
                return;
            }
            Activate(_keys[num]);
        }

        /// <summary>Performs a key's action.</summary>
        private void Activate(KeyboardKey key)
        {
            LayoutKey d = key.Description;
            key.Flash();

            // True for a key that can repeat while the gaze stays on it ([KeyRepeat]):
            // letters, delete, arrows… Not for a modifier or a shortcut: by keeping looking,
            // it would be toggled on and off endlessly.
            bool repeatable = true;

            if (d.Prediction.HasValue)
            {
                // A suggestion box. No repetition: by keeping looking at it, the word
                // would not be written twice (the box is empty right after anyway, the current
                // word starting from scratch).
                ChooseSuggestion(d.Prediction.Value);
                repeatable = false;
            }
            else if (d.Text != null)
            {
                Write(d);
            }
            else if (d.Key != null)
            {
                PressKey(d.Key);
            }
            else if (d.Shortcut != null)
            {
                Shortcut(d.Shortcut);
                repeatable = false;
            }
            else if (d.Page != null)
            {
                // The page changes: the keys are replaced (see Build).
                ChangePage(d.Page);
                return;
            }
            else if (d.Modifier != null)
            {
                ToggleModifier(d.Modifier);
                repeatable = false;
            }
            else if (d.Command == "DeleteLetter")
            {
                DeleteLetter();
            }
            else if (d.Command == "DeleteWord")
            {
                DeleteWord();
            }
            else
            {
                App.Log("Keyboard: key \"" + d.Label + "\" has no known action");
            }

            if (!repeatable || !AppSettings.Current.KeyRepeat)
            {
                _aimer.BlockUntilExit();
            }
            RefreshState();
        }

        // ───────────────────────── Actions ─────────────────────────

        /// <summary>Writes a key's text, in upper case if Shift or Caps Lock (but not both).</summary>
        private void Write(LayoutKey d)
        {
            // Shift and Caps Lock together give lower case, as on a real keyboard.
            bool uppercaseLetters = _shift ^ _capsLock;
            string text = uppercaseLetters ? (d.ShiftText ?? d.Text.ToUpper(CultureInfo.CurrentCulture)) : d.Text;

            if ((_ctrl || _alt || _win) && text.Length == 1)
            {
                // With Ctrl, Alt or Win, the REAL key is needed (Ctrl + c = copy): Windows is
                // asked which key produces this character. High byte: 1 = Shift is needed too.
                short result = NativeMethods.VkKeyScanW(text[0]);
                if (result != -1)
                {
                    ushort code = (ushort)(result & 0xFF);
                    List<ushort> mods = ActiveModifiers(false);
                    if (((result >> 8) & 1) != 0)
                    {
                        mods.Add(NativeMethods.VK_SHIFT);
                    }
                    KeyboardInjector.TypeKey(code, false, mods);
                    _ctrl = false;
                    _alt = false;
                    _win = false;
                    _shift = false;
                    // A shortcut writes no text: the current word is no longer certain.
                    ForgetPosition();
                    return;
                }
            }

            KeyboardInjector.TypeText(text);
            Preview(0, text);
            _ctrl = false;
            _alt = false;
            _win = false;
            if (ContainsLetter(text))
            {
                _shift = false;   // Shift only counts for a letter
            }
            Follow(text);
        }

        /// <summary>Presses a real key (Tab, Enter, arrows…), with the active modifiers.</summary>
        private void PressKey(string name)
        {
            ushort code;
            bool extent;
            if (!KeyCode(name, out code, out extent))
            {
                App.Log("Keyboard: unknown key \"" + name + "\" in the layout");
                return;
            }

            // Here, Shift counts (Shift + Tab, Shift + arrow to select…).
            List<ushort> mods = ActiveModifiers(true);
            // Ctrl, Alt or Win + Enter is not a plain line end (Shift + Enter is).
            bool withModifier = _ctrl || _alt || _win;
            KeyboardInjector.TypeKey(code, extent, mods);
            _ctrl = false;
            _alt = false;
            _win = false;
            _shift = false;

            if (code == 0x0D && !withModifier)
            {
                Follow("\n");   // Enter: line end (counts for automatic capitalisation)
                Preview(0, "\n");
            }
            else
            {
                // Tab, arrows, Home, Ctrl + Enter…: the text cursor may have moved, the
                // current word is no longer certain.
                ForgetPosition();
            }
        }

        /// <summary>
        /// A shortcut written in the layout, for example "Ctrl+F": the words before the last
        /// "+" are the modifiers (Ctrl, Alt, Shift, Win), the last one is the key.
        /// </summary>
        private void Shortcut(string shortcut)
        {
            string[] pieces = shortcut.Split('+');
            List<ushort> mods = new List<ushort>();
            for (int i = 0; i < pieces.Length - 1; i++)
            {
                switch (pieces[i].Trim().ToLowerInvariant())
                {
                    case "ctrl":
                        mods.Add(NativeMethods.VK_CONTROL);
                        break;
                    case "alt":
                        mods.Add(NativeMethods.VK_MENU);
                        break;
                    case "shift":
                        mods.Add(NativeMethods.VK_SHIFT);
                        break;
                    case "win":
                        mods.Add(NativeMethods.VK_LWIN);
                        break;
                    default:
                        App.Log("Keyboard: unknown modifier in shortcut \"" + shortcut + "\"");
                        return;
                }
            }

            ushort code;
            bool extent;
            if (!KeyCode(pieces[pieces.Length - 1].Trim(), out code, out extent))
            {
                App.Log("Keyboard: unknown key in shortcut \"" + shortcut + "\"");
                return;
            }
            KeyboardInjector.TypeKey(code, extent, mods);
            ForgetPosition();
        }

        /// <summary>Delete a letter: Backspace (with Ctrl, Alt, Shift if they are on).</summary>
        private void DeleteLetter()
        {
            // With Ctrl, Backspace deletes a whole word in most applications: the preview does
            // the same; with Alt or Win, the result is unknown: the preview is cleared.
            if (_alt || _win)
            {
                _preview.Clear();
            }
            else if (_ctrl)
            {
                RemovePreviewWord();
            }
            else
            {
                Preview(1, "");
            }
            KeyboardInjector.TypeKey(NativeMethods.VK_BACK, false, ActiveModifiers(true));
            _ctrl = false;
            _alt = false;
            _win = false;
            _shift = false;
            if (_currentWord.Length > 0)
            {
                _currentWord.Length--;
            }
            else
            {
                // Deleting the space or the end of the previous word: it is no longer certain.
                _previousWord = null;
            }
            _end = _end.Length > 0 ? _end.Substring(0, _end.Length - 1) : "";
        }

        /// <summary>
        /// Delete the word: as many Backspaces as letters in the current word. If the
        /// keyboard does not know the word (right after a space, or after a click elsewhere),
        /// it sends Ctrl + Backspace, which most applications understand as "delete the
        /// previous word".
        /// </summary>
        private void DeleteWord()
        {
            if (_currentWord.Length > 0)
            {
                for (int i = 0; i < _currentWord.Length; i++)
                {
                    KeyboardInjector.TypeKey(NativeMethods.VK_BACK, false, null);
                }
                Preview(_currentWord.Length, "");
                _currentWord.Clear();
            }
            else
            {
                KeyboardInjector.TypeKey(NativeMethods.VK_BACK, false, new List<ushort> { NativeMethods.VK_CONTROL });
                _previousWord = null;   // the previous word has just been deleted
                RemovePreviewWord();
            }
            _end = "";
        }

        /// <summary>Turns a modifier on or off (Shift, CapsLock, Ctrl, Alt, Win).</summary>
        private void ToggleModifier(string name)
        {
            switch (name.Trim().ToLowerInvariant())
            {
                case "shift":
                    _shift = !_shift;
                    break;
                case "capslock":
                    _capsLock = !_capsLock;
                    break;
                case "ctrl":
                    _ctrl = !_ctrl;
                    break;
                case "alt":
                    _alt = !_alt;
                    break;
                case "win":
                    _win = !_win;
                    break;
                default:
                    App.Log("Keyboard: unknown modifier \"" + name + "\" in the layout");
                    break;
            }
        }

        /// <summary>Shows another keyboard page ("back" = the previous page).</summary>
        private void ChangePage(string name)
        {
            if (string.Equals(name, "back", StringComparison.OrdinalIgnoreCase))
            {
                name = _previousPage ?? _layout.StartPage;
            }
            if (_layout.Page(name) == null)
            {
                App.Log("Keyboard: page \"" + name + "\" not found in the layout");
                return;
            }
            _previousPage = _page;
            _page = name;
            Build();
        }

        /// <summary>
        /// Keeps track of what has just been written:
        ///  - the current word: letters, digits and hyphen ("peut-être"); any other character
        ///    (space, punctuation, apostrophe, line end) ends the word. Except when the
        ///    dictionary says so (English, see WordPredictor.ApostropheInWord): an apostrophe
        ///    after a letter stays in the word ("don't");
        ///  - the last two characters, for automatic capitalisation: after ".", "?" or "!"
        ///    followed by a space or a line end, Shift turns on by itself for the next letter
        ///    ([AutoCapitalize]). Shift is shown on: it can be turned off.
        /// </summary>
        private void Follow(string text)
        {
            foreach (char c in text)
            {
                bool apostrophe = c == '\'' || c == '’';
                if (char.IsLetterOrDigit(c) || c == '-' ||
                    (apostrophe && _currentWord.Length > 0 && _predictor.ApostropheInWord))
                {
                    _currentWord.Append(c);
                }
                else
                {
                    // The current word is finished: if it is known, the prediction
                    // remembers it was used once more, and that it followed the previous word
                    // (learning). With an apostrophe, the elided form counts: "l'", "qu'".
                    if (_currentWord.Length > 0)
                    {
                        string word = _currentWord.ToString();
                        _previousWord = _predictor.RecordUsage(apostrophe ? word + "'" : word, _previousWord);
                    }
                    // Next word: only a space or an apostrophe keep the link with the previous
                    // word; punctuation (. , ? ! …) or a line end break it.
                    if (c != ' ' && !apostrophe)
                    {
                        _previousWord = null;
                    }
                    _currentWord.Clear();
                }
            }
            KeepEnd(text);
        }

        /// <summary>
        /// Remembers the last two written characters, and turns Shift on by itself after ".",
        /// "?" or "!" followed by a space or a line end ([AutoCapitalize]).
        /// </summary>
        private void KeepEnd(string text)
        {
            _end += text;
            if (_end.Length > 2)
            {
                _end = _end.Substring(_end.Length - 2);
            }

            if (AppSettings.Current.AutoCapitalize && _end.Length == 2 &&
                (_end[1] == ' ' || _end[1] == '\n') && ".?!".IndexOf(_end[0]) >= 0)
            {
                _shift = true;
            }
        }

        /// <summary>
        /// The user looked at suggestion box number "num" (1, 2 or 3).
        ///  - "+ myword" box: the typed word is added to the personal dictionary, then a space
        ///    is written (the word is already written in full);
        ///  - otherwise: the suggested word replaces the typed letters, followed by a space. To
        ///    be fast and flicker-free, only the differing end is deleted: "bon" → "bonjour"
        ///    only writes "jour"; "ete" → "été" deletes "te" and writes "té".
        ///    After an elided word (m' t' n' s' c' l' qu' d' j'…), no space: the next word
        ///    sticks to it.
        /// </summary>
        private void ChooseSuggestion(int num)
        {
            string typed = _currentWord.ToString();
            // Shift or Caps Lock on before a next word: its 1st letter in upper case.
            bool uppercaseRequested = _shift ^ _capsLock;
            // The other modifiers do not apply to a whole word: they are turned off.
            _shift = false;
            _ctrl = false;
            _alt = false;
            _win = false;

            if (num == 1 && _unknownWord)
            {
                _predictor.Add(typed);
                KeyboardInjector.TypeText(" ");
                Preview(0, " ");
                Follow(" ");   // ends the word: it already counts one use
                return;
            }

            int idx = num - 1;
            if (idx < 0 || idx >= _suggestions.Count)
            {
                return;   // empty box (should not happen: it is inactive)
            }

            // (With an empty "typed", it is a next-word suggestion: nothing to delete.)
            string word = ShownShape(_suggestions[idx], typed, uppercaseRequested);

            // The identical first letters stay in place.
            int common = 0;
            while (common < typed.Length && common < word.Length && typed[common] == word[common])
            {
                common++;
            }
            for (int i = common; i < typed.Length; i++)
            {
                KeyboardInjector.TypeKey(NativeMethods.VK_BACK, false, null);
            }

            bool elision = word.EndsWith("'") || word.EndsWith("’");
            string addition = word.Substring(common) + (elision ? "" : " ");
            KeyboardInjector.TypeText(addition);
            Preview(typed.Length - common, addition);

            _currentWord.Clear();
            _previousWord = _predictor.RecordUsage(word, _previousWord);
            KeepEnd(addition);
        }

        /// <summary>
        /// The word as shown in its box and written: with the capitals of what was typed
        /// (WithUppercase); for a next word (nothing typed), with a capital at the start if
        /// Shift or Caps Lock is on.
        /// </summary>
        private static string ShownShape(string word, string typed, bool uppercase)
        {
            if (string.IsNullOrEmpty(typed) && uppercase && !string.IsNullOrEmpty(word))
            {
                return char.ToUpper(word[0], CultureInfo.CurrentCulture) + word.Substring(1);
            }
            return WithUppercase(word, typed);
        }

        /// <summary>
        /// The suggested word with the capitals of what was typed: "Bo" → "Bonjour" (the word
        /// keeps its initial capital, specification), "BON" (Caps Lock) → "BONJOUR".
        /// Otherwise, the word as stored ("Paris" stays "Paris").
        /// </summary>
        private static string WithUppercase(string word, string typed)
        {
            if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(typed))
            {
                return word;
            }
            int letters = 0;
            int uppercaseLetters = 0;
            foreach (char c in typed)
            {
                if (char.IsLetter(c))
                {
                    letters++;
                    if (char.IsUpper(c))
                    {
                        uppercaseLetters++;
                    }
                }
            }
            if (letters >= 2 && uppercaseLetters == letters)
            {
                return word.ToUpper(CultureInfo.CurrentCulture);
            }
            if (char.IsUpper(typed[0]))
            {
                return char.ToUpper(word[0], CultureInfo.CurrentCulture) + word.Substring(1);
            }
            return word;
        }
    }
}
