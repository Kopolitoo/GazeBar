using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GazeBar
{
    /// <summary>
    /// LOGIC: the gaze keyboard (step 12, "clavier-base").
    ///
    /// It is not a mode: the keyboard stays shown WHILE GazeBar works normally above it
    /// (user's choice of 2026-10-31):
    ///   - gaze OUTSIDE the keyboard: movement mode applies (fixed gaze, action choice,
    ///     magnifier…);
    ///   - gaze ON the keyboard: the mouse cursor does not move, the gaze circle is hidden,
    ///     and this keyboard reacts: the key looked at lights up, then flashes and performs
    ///     its action after [KeyDelay] (no progress ring).
    /// Movement mode passes it the gaze (MovementMode.Update).
    ///
    /// It is shown in movement and fixed gaze modes, and while the keyboard menu bar is
    /// open; it is hidden in all the others (magnifier, action choice, settings, pause…)
    /// and comes back on return (OnModeChange, called by the ModeManager).
    ///
    /// It is drawn on the overlay (KeyboardSurface), at the bottom of the screen, full
    /// width, above everything and WITHOUT reserving room: other windows keep their size.
    /// Since 2026-11-01 (user remarks), the keyboard menu bar also offers: moving it to the
    /// top of the screen (position remembered, [KeyboardAtTop]), and shrinking it to 75 % or
    /// 50 % ([KeyboardSize], centred). When this bar is open, the keyboard moves up above it
    /// (SetBottomReserve). The overlay never takes focus: the application being typed in
    /// keeps its text cursor, and the simulated keys do reach it.
    ///
    /// A "Fermer le clavier" button sits above the keyboard, on the right (below when the
    /// keyboard is at the top). It can also be closed with the Escape key of a real keyboard
    /// (for the caregiver), or with the "Clavier" button of the classic bar if it is hidden
    /// by game mode. (The Close button of the keyboard bar was removed in step 14: it was
    /// redundant.)
    ///
    /// Keys come from a layout file (KeyboardLayout, read again at each opening). Typed
    /// text is NEVER written to the log (privacy): only openings, closings and errors are.
    ///
    /// Step 13 ("clavier-prediction"): the 3 top boxes suggest the most likely words
    /// starting with the current word (WordPredictor); looking at one writes the whole word
    /// followed by a space. An unknown word (2 letters or more) shows in the 1st box with a
    /// "+": looking at it adds it to the personal dictionary.
    /// Right after a space, the boxes suggest the words that already followed the previous
    /// word ("next word", learnt as sentences are written, request of 2026-11-02).
    ///
    /// Step 14 ("clavier-options"): the layout is the one chosen with the "Langue" button
    /// ([KeyboardLayout], see ChangeLayout), and an optional INPUT BOX ([InputBox]) shows the
    /// last typed words in a band above the keys. It is a plain preview: text always goes
    /// straight to the application. The keyboard cannot read the application's text: the
    /// preview is therefore what THE KEYBOARD typed, and it is cleared when the text cursor
    /// may have moved (arrow, Tab, shortcut, click elsewhere…), like the current word.
    /// </summary>
    internal sealed class GazeKeyboard
    {
        // Distances in pixels of the 4K screen, converted for the current screen (ScreenMetrics.Px).
        private const double Margin4K = 16;          // between the keyboard edge and the keys
        private const double KeyGap4K = 12;   // gap between two keys
        // Between the Close button and the keyboard: 24 px at first, 60 px since 2026-11-01
        // (user request: button too close to the keys).
        private const double CloseGap4K = 60;

        /// <summary>Activation zone of the Close button: 20 % larger than its drawing.</summary>
        private const double CloseZoneFactor = 1.2;

        /// <summary>Height of the input box (step 14), relative to a row of keys.</summary>
        private const double InputBoxRatio = 0.7;

        /// <summary>Number of characters kept for the input box preview (only the end is shown).</summary>
        private const int PreviewMaxLength = 400;

        // Input box colours (frozen: WPF draws them faster).
        private static readonly Brush InputBoxBackground = Freeze(Color.FromArgb(0xFF, 0x26, 0x27, 0x2A));
        private static readonly Brush InputBoxText = Freeze(Colors.White);
        private static readonly Brush InputBoxEmptyText = Freeze(Color.FromArgb(0xFF, 0x80, 0x80, 0x80));

        private readonly OverlayWindow _layer;

        // The layout read at opening, the shown page and the previous page ("↺" key).
        private KeyboardLayout _layout;
        private string _page;
        private string _previousPage;

        // The targets aimed at by the gaze: the page's keys, then the Close button (the last one).
        private readonly List<IGazeTarget> _targets = new List<IGazeTarget>();
        private readonly List<KeyboardKey> _keys = new List<KeyboardKey>();
        private GazeButton _close;
        private readonly ButtonAimer _aimer;

        // True when the keyboard is really drawn on screen.
        private bool _visible;

        // Room kept free at the bottom of the screen, in pixels: the height of the keyboard
        // menu bar when it is open (see SetBottomReserve), 0 otherwise.
        private double _bottomReserve;

        // The keyboard rectangle, in screen pixels.
        private double _left;
        private double _top;
        private double _right;
        private double _bottom;

        // State of the keyboard modifiers. They are separate from the action choice ones
        // (Modifiers.cs), which are reset at every return to movement mode.
        private bool _shift;       // for the next key only
        private bool _capsLock;   // Caps Lock: stays on
        private bool _ctrl;
        private bool _alt;
        private bool _win;

        // The "current word": letters typed since the last space or sign (see Follow). Used
        // by "Effacer le mot" and by prediction (step 13).
        private readonly StringBuilder _currentWord = new StringBuilder();

        // Step 13: prediction, and what the suggestion boxes show (computed by RefreshState
        // after each key). _unknownWord: true when the 1st box shows "+ myword" (unknown word
        // to add).
        private readonly WordPredictor _predictor;
        private List<string> _suggestions = new List<string>();
        private bool _unknownWord;

        // The word written just before the current word (stored dictionary form), for
        // next-word prediction (2026-11-02); null at the start, after punctuation, or when the
        // text cursor may have moved (the previous word is then no longer certain).
        private string _previousWord;

        // The last two typed characters, for automatic capitalisation after ".".
        private string _end = "";

        // Step 14: the input box. _preview: what the keyboard typed since the text cursor
        // last stayed put (kept in memory only, never in the log); _previewText: its shown
        // text (null when the box is not shown); _previewWidthWpf: the width available for
        // this text, in WPF units.
        private readonly StringBuilder _preview = new StringBuilder();
        private TextBlock _previewText;
        private double _previewWidthWpf;

        public GazeKeyboard(OverlayWindow layer)
        {
            _layer = layer;
            // "1": a single gaze sample is enough to light a key (2 for GazeBar's other
            // buttons). Hover thus reacts one sample earlier (about 30 ms); user request of
            // 2026-11-02.
            _aimer = new ButtonAimer(_targets, 1);

            // The French dictionary is read as soon as GazeBar starts, in the background
            // (specification): it is ready when the keyboard opens.
            _predictor = new WordPredictor();
            _predictor.LoadLanguage("fr");
        }

        /// <summary>
        /// Call when GazeBar closes: saves the word usage counts not saved yet (otherwise
        /// they are saved once a minute).
        /// </summary>
        public void Stop()
        {
            _predictor.Stop();
        }

        /// <summary>True between Open and Close (even when the keyboard is hidden by another mode).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>True when the keyboard is drawn on screen.</summary>
        public bool Visible
        {
            get { return _visible; }
        }

        // ───────────────────────── Opening, closing, display ─────────────────────────

        /// <summary>
        /// Opens the keyboard: reads the layout file and starts from its start page.
        /// It will appear on return to movement mode (OnModeChange).
        /// Returns false if the file is missing or invalid (message for the menu bar).
        /// </summary>
        public bool Open(out string message)
        {
            // Step 14: the layout chosen with the "Langue" button. If its file disappeared or
            // contains an error, the default layout (fr-azerty) is tried rather than not
            // opening the keyboard at all; the saved choice does not change.
            string name = AppSettings.Current.KeyboardLayout;
            KeyboardLayout layout = KeyboardLayout.Load(name, out message);
            if (layout == null &&
                !string.Equals(name, KeyboardLayout.DefaultName, StringComparison.OrdinalIgnoreCase))
            {
                string defaultMessage;
                layout = KeyboardLayout.Load(KeyboardLayout.DefaultName, out defaultMessage);
                if (layout != null)
                {
                    App.Log("Keyboard: layout \"" + name + "\" unusable, using " + KeyboardLayout.DefaultName);
                    message = null;
                }
            }
            if (layout == null)
            {
                return false;
            }

            _layout = layout;
            // The dictionary of the layout's language ("fr" for fr-azerty.json): already
            // loaded at startup in the usual case.
            _predictor.LoadLanguage(layout.Language);
            _page = layout.StartPage;
            _previousPage = null;
            IsOpen = true;
            ForgetState();
            App.Log("Keyboard: opened");
            return true;
        }

        /// <summary>Closes the keyboard (Close button, keyboard menu bar, Escape key).</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            Hide();
            ForgetState();
            App.Log("Keyboard: closed");
        }

        /// <summary>
        /// Called by the ModeManager at each mode change, BEFORE entering the new mode: the
        /// keyboard is shown in movement and fixed gaze modes, and while the keyboard menu
        /// bar is open; it is hidden in all the others.
        /// (Before entering: the magnifier takes a screenshot as soon as it starts, the
        /// keyboard must not be on it.)
        /// </summary>
        public void OnModeChange(Mode newVal)
        {
            MenuMode menu = newVal as MenuMode;
            bool show = IsOpen &&
                            (newVal is MovementMode || newVal is FixedGazeMode ||
                             (menu != null && menu.KeyboardMenu));

            if (show && !_visible)
            {
                Build();
            }
            else if (!show && _visible)
            {
                Hide();
                // We went off to do something else (a click elsewhere, the settings…): the
                // text around the cursor may have changed, the current word is no longer certain.
                ForgetPosition();
            }
            else if (_visible)
            {
                // Still shown (movement ↔ fixed gaze, keyboard menu): no key is hovered any
                // more, and the one under the gaze waits for the gaze to leave it.
                _aimer.ResetAndBlock();
                RedrawWithoutHover();
            }
        }

        /// <summary>The screen changed (step 11): the keyboard is redrawn at the new sizes.</summary>
        public void AdaptToScreen()
        {
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// The keyboard menu bar opens (pixels = its height, from the bottom of the screen)
        /// or closes (0). If the keyboard is at the bottom, it moves up above the bar instead
        /// of being covered (user request of 2026-11-01). At the top, nothing changes.
        /// </summary>
        public void SetBottomReserve(double pixels)
        {
            pixels = Math.Max(0, pixels);
            if (Math.Abs(pixels - _bottomReserve) < 0.5)
            {
                return;
            }
            _bottomReserve = pixels;
            if (_visible && !AppSettings.Current.KeyboardAtTop)
            {
                // Redrawn at its new place, then slid from the old one (2026-11-02).
                double oldTop = _top;
                Build();
                if (_visible)
                {
                    _layer.AnimateKeyboardSurface(oldTop - _top);
                }
            }
        }

        /// <summary>
        /// "Haut / bas" button of the keyboard bar: the keyboard moves to the top of the
        /// screen (or back to the bottom). The position is saved: the keyboard reopens there.
        /// </summary>
        public void TogglePosition()
        {
            AppSettings p = AppSettings.Current;
            p.KeyboardAtTop = !p.KeyboardAtTop;
            p.Save();
            App.Log("Keyboard: " + (p.KeyboardAtTop ? "top" : "bottom") + " of the screen");
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// "Taille" button of the keyboard bar: 100 % → 75 % → 50 % → 100 %… (the same
        /// setting as "Taille du clavier" in the Clavier settings tab). Saved.
        /// </summary>
        public void ChangeSize()
        {
            AppSettings p = AppSettings.Current;
            p.KeyboardSize = p.KeyboardSize > 75 ? 75 : p.KeyboardSize > 50 ? 50 : 100;
            p.Save();
            App.Log("Keyboard: size " + p.KeyboardSize + " %");
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// "Zone de saisie" button of the keyboard bar (step 14): shows or hides the band of
        /// the last typed words. Saved. The keyboard is redrawn (it grows or shrinks by the
        /// band's height).
        /// </summary>
        public void ToggleInputBox()
        {
            AppSettings p = AppSettings.Current;
            p.InputBox = !p.InputBox;
            p.Save();
            App.Log("Keyboard: input box " + (p.InputBox ? "shown" : "hidden"));
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// The word prediction, for the "Mes mots" list (step 14, see KeyboardListMode).
        /// </summary>
        public WordPredictor Predictor
        {
            get { return _predictor; }
        }

        /// <summary>
        /// "Langue" button (step 14): switches to another layout of the Keyboards folder
        /// (file name without ".json"). It is read at once; if missing or invalid, nothing
        /// changes and "message" explains why. Otherwise the choice is saved, the dictionary
        /// of its language is loaded (in the background), and the keyboard restarts from the
        /// new layout's start page.
        /// </summary>
        public bool ChangeLayout(string name, out string message)
        {
            KeyboardLayout layout = KeyboardLayout.Load(name, out message);
            if (layout == null)
            {
                return false;
            }
            AppSettings p = AppSettings.Current;
            p.KeyboardLayout = name;
            p.Save();

            _layout = layout;
            _predictor.LoadLanguage(layout.Language);
            _page = layout.StartPage;
            _previousPage = null;
            ForgetState();
            App.Log("Keyboard: layout chosen: " + name);
            if (_visible)
            {
                Build();
            }
            return true;
        }

        /// <summary>
        /// Step 15: a settings profile has just been loaded (called by SettingsMode).
        /// Size, position and input box are read again at each drawing: nothing to do for
        /// them. But the layout (and therefore the dictionary language) may have changed: if
        /// the keyboard is open, it is reopened, which reads the layout saved in the
        /// settings again. If it cannot be (missing file), it closes.
        /// During settings, the keyboard is hidden: it will come back on return to movement
        /// mode.
        /// </summary>
        public void SettingsReplaced()
        {
            if (!IsOpen)
            {
                return;
            }
            string message;
            if (!Open(out message))
            {
                App.Log("Keyboard: profile layout unusable, keyboard closed (" + message + ")");
                Close();
            }
        }

        /// <summary>
        /// True if the gaze point (screen pixels, TRUE gaze and not clamped to the screen) is
        /// on the keyboard or on its Close button. False if the keyboard is not shown.
        /// </summary>
        public bool Contains(double x, double y)
        {
            if (!_visible)
            {
                return false;
            }
            if (x >= _left && x < _right && y >= _top && y < _bottom)
            {
                return true;
            }
            return _close != null && _close.Contains(x, y);
        }

        private void Hide()
        {
            _layer.HideKeyboardSurface();
            _visible = false;
        }

        /// <summary>Resets the modifiers, the current word and the end of the text.</summary>
        private void ForgetState()
        {
            _shift = false;
            _capsLock = false;
            _ctrl = false;
            _alt = false;
            _win = false;
            ForgetPosition();
        }

        /// <summary>
        /// The text cursor may have moved (arrow, Tab, shortcut, click elsewhere…): forget the
        /// current word, the previous word, the end of the text and the input box preview,
        /// which may no longer match the text around the cursor.
        /// </summary>
        private void ForgetPosition()
        {
            _currentWord.Clear();
            _previousWord = null;
            _end = "";
            _preview.Clear();
        }

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
                // Step 13: a suggestion box. No repetition: by keeping looking at it, the word
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
        ///    (space, punctuation, apostrophe, line end) ends the word;
        ///  - the last two characters, for automatic capitalisation: after ".", "?" or "!"
        ///    followed by a space or a line end, Shift turns on by itself for the next letter
        ///    ([AutoCapitalize]). Shift is shown on: it can be turned off.
        /// </summary>
        private void Follow(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || c == '-')
                {
                    _currentWord.Append(c);
                }
                else
                {
                    // The current word is finished (step 13): if it is known, the prediction
                    // remembers it was used once more, and that it followed the previous word
                    // (learning). With an apostrophe, the elided form counts: "l'", "qu'".
                    bool apostrophe = c == '\'' || c == '’';
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
        /// Step 13: the user looked at suggestion box number "num" (1, 2 or 3).
        ///  - "+ myword" box: the typed word is added to the personal dictionary, then a space
        ///    is written (the word is already written in full);
        ///  - otherwise: the suggested word replaces the typed letters, followed by a space. To
        ///    be fast and flicker-free, only the differing end is deleted: "bon" → "bonjour"
        ///    only writes "jour"; "ete" → "été" deletes "te" and writes "té".
        ///    After an elided word (m' t' n' s' c' l' qu' d' j'…), no space: the next word
        ///    sticks to it (user request of 2026-11-02).
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

        // ───────────────────────── Input box (step 14) ─────────────────────────

        /// <summary>
        /// Updates the input box preview as the application did: "deleted" Backspaces, then
        /// "addition" written. The preview is kept up to date even when the box is hidden: it
        /// is right as soon as it is shown. Only the end is kept.
        /// </summary>
        private void Preview(int deleted, string addition)
        {
            _preview.Length = Math.Max(0, _preview.Length - deleted);
            _preview.Append(addition);
            if (_preview.Length > PreviewMaxLength)
            {
                _preview.Remove(0, _preview.Length - PreviewMaxLength);
            }
        }

        /// <summary>
        /// Like Ctrl + Backspace in most applications: removes trailing spaces, then the
        /// letters of the last word.
        /// </summary>
        private void RemovePreviewWord()
        {
            int end = _preview.Length;
            while (end > 0 && char.IsWhiteSpace(_preview[end - 1]))
            {
                end--;
            }
            while (end > 0 && !char.IsWhiteSpace(_preview[end - 1]))
            {
                end--;
            }
            _preview.Length = end;
        }

        /// <summary>
        /// Writes the preview in the input box (if shown). A line end is shown as "↵". When
        /// the text is too long, only the end is shown, preceded by "…". Empty box: a small
        /// grey text explains what it is for.
        /// </summary>
        private void ShowPreview()
        {
            if (_previewText == null)
            {
                return;
            }
            if (_preview.Length == 0)
            {
                _previewText.Text = Texts.Get("keyboard.inputBoxEmpty");
                _previewText.Foreground = InputBoxEmptyText;
                return;
            }
            string text = _preview.ToString().Replace("\n", " ↵ ");
            _previewText.Foreground = InputBoxText;
            _previewText.Text = FittingEnd(text);
        }

        /// <summary>
        /// The longest end of "text" that fits in the box width (preceded by "…" if cut). The
        /// cut point is found by binary search: about ten measures instead of trying every
        /// character.
        /// </summary>
        private string FittingEnd(string text)
        {
            if (TextWidth(text) <= _previewWidthWpf)
            {
                return text;
            }
            // Find the smallest "start" for which "…" + the end from start fits.
            int bottom = 1;
            int top = text.Length;   // empty text: always fits
            while (bottom < top)
            {
                int middle = bottom + (top - bottom) / 2;
                if (TextWidth("…" + text.Substring(middle)) <= _previewWidthWpf)
                {
                    top = middle;     // it fits: maybe more text can be kept
                }
                else
                {
                    bottom = middle + 1;  // too long: cut further
                }
            }
            return "…" + text.Substring(bottom);
        }

        /// <summary>Width of a text in the input box font, in WPF units.</summary>
        private double TextWidth(string text)
        {
            FormattedText measure = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(_previewText.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                _previewText.FontSize,
                InputBoxText,
                VisualTreeHelper.GetDpi(_layer).PixelsPerDip);
            return measure.WidthIncludingTrailingWhitespace;
        }

        // ───────────────────────── Small helpers ─────────────────────────

        private static Brush Freeze(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>The codes of the modifiers that are on (Shift only if withShift).</summary>
        private List<ushort> ActiveModifiers(bool withShift)
        {
            List<ushort> mods = new List<ushort>();
            if (_ctrl)
            {
                mods.Add(NativeMethods.VK_CONTROL);
            }
            if (_alt)
            {
                mods.Add(NativeMethods.VK_MENU);
            }
            if (_win)
            {
                mods.Add(NativeMethods.VK_LWIN);
            }
            if (withShift && _shift)
            {
                mods.Add(NativeMethods.VK_SHIFT);
            }
            return mods;
        }

        private static bool ContainsLetter(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The Windows virtual-key code of a key named in the layout.
        /// extent: true for "extended" keys (arrows, Home, End, Delete, Page…).
        /// Also accepts a single letter (A to Z) or a single digit, for shortcuts.
        /// </summary>
        private static bool KeyCode(string name, out ushort code, out bool extent)
        {
            extent = false;
            code = 0;
            switch (name.Trim().ToLowerInvariant())
            {
                case "tab": code = 0x09; return true;
                case "enter": code = 0x0D; return true;
                case "escape": code = 0x1B; return true;
                case "space": code = 0x20; return true;
                case "backspace": code = NativeMethods.VK_BACK; return true;
                case "pageup": code = 0x21; extent = true; return true;
                case "pagedown": code = 0x22; extent = true; return true;
                case "end": code = 0x23; extent = true; return true;
                case "home": code = 0x24; extent = true; return true;
                case "left": code = 0x25; extent = true; return true;
                case "up": code = 0x26; extent = true; return true;
                case "right": code = 0x27; extent = true; return true;
                case "down": code = 0x28; extent = true; return true;
                case "insert": code = 0x2D; extent = true; return true;
                case "delete": code = 0x2E; extent = true; return true;
                // The Windows key alone (2026-11-02): opens or closes the Start menu.
                // It is an "extended" key for Windows.
                case "windows": code = NativeMethods.VK_LWIN; extent = true; return true;
            }

            string n = name.Trim().ToUpperInvariant();
            // F1 to F24: codes 0x70 to 0x87.
            int num;
            if (n.Length >= 2 && n[0] == 'F' && int.TryParse(n.Substring(1), out num) && num >= 1 && num <= 24)
            {
                code = (ushort)(0x70 + num - 1);
                return true;
            }
            // A single letter or digit: its code is that of the upper-case character.
            if (n.Length == 1 && ((n[0] >= 'A' && n[0] <= 'Z') || (n[0] >= '0' && n[0] <= '9')))
            {
                code = n[0];
                return true;
            }
            return false;
        }

        // ───────────────────────── Drawing ─────────────────────────

        /// <summary>
        /// (Re)draws the keyboard: background, keys of the shown page and Close button.
        /// All positions are in screen pixels, converted by the overlay.
        /// </summary>
        private void Build()
        {
            _targets.Clear();
            _keys.Clear();
            _close = null;
            if (!IsOpen || _layout == null || !_layer.Ready)
            {
                Hide();
                return;
            }
            KeyboardPage page = _layout.Page(_page) ?? _layout.Page(_layout.StartPage);

            AppSettings p = AppSettings.Current;
            double width = ScreenMetrics.WidthPixels;
            double height = ScreenMetrics.HeightPixels;

            // Size (2026-11-01): at 75 % or 50 %, width AND height shrink together, so keys
            // keep their shape; the keyboard is centred horizontally.
            double sizePercent = p.KeyboardSize / 100.0;
            double keyboardWidth = width * sizePercent;
            double keysHeight = height * p.KeyboardHeight / 100.0 * sizePercent;

            // The key grid.
            double margin = ScreenMetrics.Px(Margin4K);
            double cellWidth = (keyboardWidth - 2 * margin) / _layout.Columns;
            double cellHeight = (keysHeight - 2 * margin) / _layout.Rows;
            double gap = Math.Min(ScreenMetrics.Px(KeyGap4K), Math.Min(cellWidth, cellHeight) * 0.15);

            // Step 14: the input box adds a band above the keys. The keyboard grows by its
            // height: keys keep their size.
            double zoneHeight = p.InputBox ? Math.Round(cellHeight * InputBoxRatio) : 0;
            double keyboardHeight = keysHeight + zoneHeight;

            _left = (width - keyboardWidth) / 2;
            _right = _left + keyboardWidth;
            if (p.KeyboardAtTop)
            {
                // At the top of the screen ("Haut / bas" button of the keyboard bar).
                _top = 0;
                _bottom = keyboardHeight;
            }
            else
            {
                // At the bottom, above the keyboard menu bar if it is open.
                _bottom = Math.Max(keyboardHeight, height - _bottomReserve);
                _top = _bottom - keyboardHeight;
            }

            _layer.PrepareKeyboardSurface();
            Canvas surface = _layer.KeyboardSurface;

            // ── The background: an almost black rectangle, as on the user's screenshots.
            Border background = new Border();
            background.Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x12, 0x12, 0x12));
            background.Width = _layer.LengthToWpf(keyboardWidth);
            background.Height = _layer.LengthToWpf(keyboardHeight);
            Point corner = _layer.ToWpf(_left, _top);
            Canvas.SetLeft(background, corner.X);
            Canvas.SetTop(background, corner.Y);
            surface.Children.Add(background);

            // ── The input box (step 14): a dark band, the full width of the keys, with the
            //    text aligned left. It is not a gaze target: gaze on it does nothing (the
            //    mouse cursor stays still).
            _previewText = null;
            if (zoneHeight > 0)
            {
                Border zone = new Border();
                zone.Background = InputBoxBackground;
                zone.CornerRadius = new CornerRadius(ScreenMetrics.Units(4));
                double zoneWidth = keyboardWidth - 2 * margin - gap;
                zone.Width = _layer.LengthToWpf(zoneWidth);
                zone.Height = _layer.LengthToWpf(zoneHeight - gap);
                double indent = _layer.LengthToWpf(zoneHeight * 0.25);
                zone.Padding = new Thickness(indent, 0, indent, 0);

                _previewText = new TextBlock();
                _previewText.FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol");
                _previewText.FontSize = _layer.LengthToWpf(zoneHeight * 0.45);
                _previewText.TextWrapping = TextWrapping.NoWrap;
                _previewText.VerticalAlignment = VerticalAlignment.Center;
                _previewText.HorizontalAlignment = HorizontalAlignment.Left;
                zone.Child = _previewText;
                _previewWidthWpf = Math.Max(0, zone.Width - 2 * indent);

                Point zoneCorner = _layer.ToWpf(_left + margin + gap / 2, _top + margin + gap / 2);
                Canvas.SetLeft(zone, zoneCorner.X);
                Canvas.SetTop(zone, zoneCorner.Y);
                surface.Children.Add(zone);
            }

            // ── The keys, below the input box if it is shown.
            double keysTop = _top + zoneHeight;
            foreach (LayoutKey d in page.Keys)
            {
                KeyboardKey key = new KeyboardKey(d);
                key.Place(_layer, surface,
                              _left + margin + d.Col * cellWidth,
                              keysTop + margin + d.Row * cellHeight,
                              d.CellWidth * cellWidth,
                              cellHeight,
                              gap);
                _keys.Add(key);
                _targets.Add(key);
            }

            // ── The Close button: on the right, ABOVE the keyboard when it is at the bottom,
            //    BELOW when it is at the top (its label is then below the button). It stays
            //    on screen even if the keyboard is very tall. (E711 = "cancel" cross.)
            //    User request of 2026-11-02: it stays against the RIGHT EDGE OF THE SCREEN
            //    even when the keyboard is shrunk (75 %, 50 %, centred keyboard), and its size
            //    does not depend on the keyboard's: it is always [IconSize].
            double size = p.IconSizePx;
            double closeGap = ScreenMetrics.Px(CloseGap4K);
            double yClose = p.KeyboardAtTop
                ? _bottom + closeGap + size / 2
                : _top - closeGap - size / 2;
            yClose = Math.Max(size / 2 + margin, Math.Min(height - size / 2 - margin, yClose));
            _close = new GazeButton("", null, Texts.Get("keyboard.close"));
            _close.ZoneFactor = CloseZoneFactor;
            _close.Place(_layer, surface,
                           width - margin - size / 2,
                           yClose,
                           size, !p.KeyboardAtTop);
            _targets.Add(_close);

            // Keys changed: the one under the gaze is not activated until the gaze has left
            // it (for example after a page change).
            _aimer.ResetAndBlock();
            RefreshState();

            _layer.ShowKeyboardSurface();
            _visible = true;
        }

        /// <summary>
        /// Updates the labels (capitals), the green borders of the modifiers and, since step
        /// 13, the words of the suggestion boxes (recomputed from the current word: less than
        /// a millisecond).
        /// </summary>
        private void RefreshState()
        {
            string typed = _currentWord.ToString();
            _suggestions = _predictor.Suggest(typed, _previousWord, out _unknownWord);
            ShowPreview();

            bool uppercaseLetters = _shift ^ _capsLock;
            foreach (KeyboardKey key in _keys)
            {
                key.ShowUppercase(uppercaseLetters);
                string m = key.Description.Modifier;
                if (m != null)
                {
                    key.SetLit(IsLit(m));
                }

                int? num = key.Description.Prediction;
                if (num.HasValue)
                {
                    int idx = num.Value - 1;
                    if (_unknownWord && idx == 0)
                    {
                        key.ShowSuggestion("+ " + typed, true);
                    }
                    else if (!_unknownWord && idx >= 0 && idx < _suggestions.Count)
                    {
                        key.ShowSuggestion(ShownShape(_suggestions[idx], typed, _shift ^ _capsLock), false);
                    }
                    else
                    {
                        key.ShowSuggestion(null, false);
                    }
                }
            }
        }

        private bool IsLit(string modifier)
        {
            switch (modifier.Trim().ToLowerInvariant())
            {
                case "shift":
                    return _shift;
                case "capslock":
                    return _capsLock;
                case "ctrl":
                    return _ctrl;
                case "alt":
                    return _alt;
                case "win":
                    return _win;
                default:
                    return false;
            }
        }

        /// <summary>Draws every target without hover (for example when switching to fixed gaze).</summary>
        private void RedrawWithoutHover()
        {
            foreach (IGazeTarget target in _targets)
            {
                target.Draw(0, false, false);
            }
        }
    }
}
