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
    /// LOGIC: the gaze keyboard ("clavier-base").
    ///
    /// It is not a mode: the keyboard stays shown WHILE GazeBar works normally above it:
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
    /// The keyboard menu bar also offers: moving it to the
    /// top of the screen (position remembered, [KeyboardAtTop]), and shrinking it to 75 % or
    /// 50 % ([KeyboardSize], centred). When this bar is open, the keyboard moves up above it
    /// (SetBottomReserve). The overlay never takes focus: the application being typed in
    /// keeps its text cursor, and the simulated keys do reach it.
    ///
    /// A "Fermer le clavier" button sits above the keyboard, on the right (below when the
    /// keyboard is at the top). It can also be closed with the Escape key of a real keyboard
    /// (for the caregiver), or with the "Clavier" button of the classic bar if it is hidden
    /// by game mode.
    ///
    /// Keys come from a layout file (KeyboardLayout, read again at each opening). Typed
    /// text is NEVER written to the log (privacy): only openings, closings and errors are.
    ///
    /// The 3 top boxes suggest the most likely words
    /// starting with the current word (WordPredictor); looking at one writes the whole word
    /// followed by a space. An unknown word (2 letters or more) shows in the 1st box with a
    /// "+": looking at it adds it to the personal dictionary.
    /// Right after a space, the boxes suggest the words that already followed the previous
    /// word ("next word", learnt as sentences are written).
    ///
    /// The layout is the one chosen with the "Langue" button
    /// ([KeyboardLayout], see ChangeLayout), and an optional INPUT BOX ([InputBox]) shows the
    /// last typed words in a band above the keys. It is a plain preview: text always goes
    /// straight to the application. The keyboard cannot read the application's text: the
    /// preview is therefore what THE KEYBOARD typed, and it is cleared when the text cursor
    /// may have moved (arrow, Tab, shortcut, click elsewhere…), like the current word.
    /// </summary>
    internal sealed partial class GazeKeyboard
    {
        // Distances in pixels of the 4K screen, converted for the current screen (ScreenMetrics.Px).
        private const double Margin4K = 16;          // between the keyboard edge and the keys
        private const double KeyGap4K = 12;   // gap between two keys
        // Between the Close button and the keyboard (60 px: closer, the button is too near the
        // keys).
        private const double CloseGap4K = 60;

        /// <summary>Activation zone of the Close button: 20 % larger than its drawing.</summary>
        private const double CloseZoneFactor = 1.2;

        /// <summary>Height of the input box, relative to a row of keys.</summary>
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

        // Profile name: true while the keyboard is shown in the settings to type
        // a profile name (ShowForInput); _openedForInput: it was closed before and opened only
        // for that, so it closes again afterwards (EndInput).
        private bool _shownForInput;
        private bool _openedForInput;

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
        // by "Effacer le mot" and by prediction.
        private readonly StringBuilder _currentWord = new StringBuilder();

        // Prediction, and what the suggestion boxes show (computed by RefreshState
        // after each key). _unknownWord: true when the 1st box shows "+ myword" (unknown word
        // to add).
        private readonly WordPredictor _predictor;
        private List<string> _suggestions = new List<string>();
        private bool _unknownWord;

        // The word written just before the current word (stored dictionary form), for
        // next-word prediction; null at the start, after punctuation, or when the
        // text cursor may have moved (the previous word is then no longer certain).
        private string _previousWord;

        // The last two typed characters, for automatic capitalisation after ".".
        private string _end = "";

        // The input box. _preview: what the keyboard typed since the text cursor
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
            // buttons). Hover thus reacts one sample earlier (about 30 ms).
            _aimer = new ButtonAimer(_targets, 1);

            // The dictionary is the one of the INTERFACE language (Language setting):
            // the keyboard layout does not change it. It is read as
            // soon as GazeBar starts, in the background (specification): it is ready when the
            // keyboard opens. When the interface language changes ("Langue" button of the
            // settings), the dictionary of the new language is loaded.
            _predictor = new WordPredictor();
            _predictor.LoadLanguage(Texts.Language);
            Texts.Changed += () => _predictor.LoadLanguage(Texts.Language);
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
            // The layout chosen with the "Langue" button. If its file disappeared or
            // contains an error, the default layout (en-qwerty) is tried rather than not
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
            _page = layout.StartPage;
            _previousPage = null;
            IsOpen = true;
            ForgetState();
            App.LogDetail("Keyboard: opened");
            return true;
        }

        /// <summary>Closes the keyboard (Close button, keyboard menu bar, Escape key).</summary>
        public void Close()
        {
            _shownForInput = false;
            _openedForInput = false;
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            Hide();
            ForgetState();
            App.LogDetail("Keyboard: closed");
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
            // A profile name input does not outlive the settings (SettingsMode.Quit already
            // ends it: this is only a safety net).
            EndInput();

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

        /// <summary>
        /// Profile name: shows the keyboard in the settings, so that
        /// the user can type the name of the profile to save. The keys go, as always, to the
        /// active window: here the settings window, whose text box has the focus.
        /// If the keyboard was closed, it is opened for this input only (EndInput closes it
        /// again). Returns false if it cannot be shown (missing layout…): the caregiver can
        /// still type the name with a real keyboard.
        /// The settings mode passes it the gaze (Update) while it is shown.
        /// </summary>
        public bool ShowForInput()
        {
            if (!_shownForInput)
            {
                _openedForInput = false;
                if (!IsOpen)
                {
                    string message;
                    if (!Open(out message))
                    {
                        App.Log("Keyboard: cannot open for the profile name (" + message + ")");
                        return false;
                    }
                    _openedForInput = true;
                }
                _shownForInput = true;
                ForgetState();   // a new text box: nothing typed yet, no modifier
            }
            Build();
            // The settings window is "always on top" too: the overlay is put back above it.
            _layer.BringToFront();
            return _visible;
        }

        /// <summary>
        /// End of the profile name input (saved, cancelled, or the settings are left): the
        /// keyboard is hidden, and closed again if it was opened only for this input.
        /// </summary>
        public void EndInput()
        {
            if (!_shownForInput)
            {
                return;
            }
            bool close = _openedForInput;
            _shownForInput = false;
            _openedForInput = false;
            Hide();
            ForgetPosition();
            if (close)
            {
                Close();
            }
        }

        /// <summary>
        /// The keyboard rectangle in screen pixels (without its Close button), or Rect.Empty
        /// when it is not shown. Used by the settings window to place the profile name page
        /// in the part of the screen the keyboard leaves free.
        /// </summary>
        public Rect ScreenBounds
        {
            get
            {
                return _visible ? new Rect(_left, _top, _right - _left, _bottom - _top) : Rect.Empty;
            }
        }

        /// <summary>The screen changed: the keyboard is redrawn at the new sizes.</summary>
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
        /// of being covered. At the top, nothing changes.
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
                // Redrawn at its new place, then slid from the old one.
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
            App.LogDetail("Keyboard: " + (p.KeyboardAtTop ? "top" : "bottom") + " of the screen");
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
            App.LogDetail("Keyboard: size " + p.KeyboardSize + " %");
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// "Zone de saisie" button of the keyboard bar: shows or hides the band of
        /// the last typed words. Saved. The keyboard is redrawn (it grows or shrinks by the
        /// band's height).
        /// </summary>
        public void ToggleInputBox()
        {
            AppSettings p = AppSettings.Current;
            p.InputBox = !p.InputBox;
            p.Save();
            App.LogDetail("Keyboard: input box " + (p.InputBox ? "shown" : "hidden"));
            if (_visible)
            {
                Build();
            }
        }

        /// <summary>
        /// The word prediction, for the "Mes mots" list (see KeyboardListMode).
        /// </summary>
        public WordPredictor Predictor
        {
            get { return _predictor; }
        }

        /// <summary>
        /// "Langue" button: switches to another layout of the Keyboards folder
        /// (file name without ".json"). It is read at once; if missing or invalid, nothing
        /// changes and "message" explains why. Otherwise the choice is saved and the keyboard
        /// restarts from the new layout's start page. The dictionary does not change: it
        /// follows the interface language.
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
            _page = layout.StartPage;
            _previousPage = null;
            ForgetState();
            App.LogDetail("Keyboard: layout chosen: " + name);
            if (_visible)
            {
                Build();
            }
            return true;
        }

        /// <summary>
        /// A settings profile has just been loaded (called by SettingsMode).
        /// Size, position and input box are read again at each drawing: nothing to do for
        /// them. But the layout may have changed: if
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
                // The Windows key alone: opens or closes the Start menu.
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
    }
}
