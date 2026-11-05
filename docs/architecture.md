# GazeBar architecture

*Version française : [architecture.fr.md](architecture.fr.md).*

*For developers. Users: see the [user guide](user-guide.md). Rules for contributors:
[CONTRIBUTING.md](../CONTRIBUTING.md).*

The "step N" and step names below refer to the roadmap of the specification (the order in
which features were added). They help to find the matching pull request in the history. The
code comments do not use them: they describe the code as it is now.

## How it works

- **Gaze**: the modes read the gaze through the `IGazeSource` interface, which any gaze source
  provides (`GazeSource` holds the shared part: clock, latest position, rate). The normal source,
  `EyetrackerGazeSource`, reads the gaze from the eyetracker engine (NuGet package
  `Tobii.Interaction` 0.7.3), about 30 samples per second, in screen pixels, including slightly
  off the screen (negative coordinates or coordinates larger than the screen). The gaze software
  supplied with the eyetracker (TD Control) is paused while GazeBar runs.
- **Drawing**: `OverlayWindow` is a transparent window that covers the whole screen, always on
  top. Clicks go through it and it never takes the focus, so the window being used is never
  disturbed.
- **Acting**: GazeBar moves the Windows cursor and injects clicks, wheel and keys itself
  (`SendInput`: `MouseInjector`, `WheelScroller`, `KeyboardInjector`).
- **Modes**: `Modes/ModeManager` knows the current mode and gives it the gaze about 60 times
  per second. Each mode is one class of the `Modes` folder (list below).
- **Texts**: every text shown to the user is in `Languages/*.json`, read with `Texts.Get` /
  `Texts.Format`.
- **Privacy**: gaze data is never saved (eyetracker licence) and typed text is never written to
  the log.

## Folders

The source code is grouped by topic. Every class stays in the `GazeBar` namespace, whatever its
folder. The project file finds the `.cs` and `.xaml` files of every folder by itself.

The three biggest classes are "partial" classes: one class written in several files, named
`Class.Part.cs`. `AppSettings` (`.Icons`, `.Defaults`, `.File`), `SettingsWindow` (`.Build`,
`.Profiles`, `.Actions`) and `GazeKeyboard` (`.Actions`, `.InputBox`, `.Drawing`). The main file
(`AppSettings.cs`…) holds the fields and the explanation of the class.

| Folder | Contents |
|---|---|
| (root) | `App.xaml` (entry point: reads the settings, opens the overlay and the mode manager), `GazeBar.csproj`, `app.manifest`, `app.uiaccess.manifest` |
| `Gaze` | reading the gaze and processing it: `IGazeSource`, `GazeSource`, `EyetrackerGazeSource`, `EyetrackerLoader`, `Calibration`, `GazeState`, `Smoother`, `FixationDetector` |
| `Modes` | one class per mode, and `ModeManager` |
| `Actions` | what the action choice offers: `ActionCatalog`, `ActionChoiceLayout`, `Modifiers`, `DragGesture` |
| `Typing` | the gaze keyboard and word prediction: `GazeKeyboard`, `KeyboardKey`, `KeyboardLayout`, `WordPredictor` |
| `Settings` | the settings file, profiles and settings window: `AppSettings`, `SettingsCatalog`, `SettingsProfiles`, `SettingsWindow`, `SettingTarget` |
| `Ui` | windows and gaze buttons: `OverlayWindow`, `DiagnosticWindow`, `CursorSampler`, `GazeButton`, `ButtonAimer`, `IGazeTarget`, `Texts` |
| `Platform` | calls to Windows: `NativeMethods`, `MouseInjector`, `KeyboardInjector`, `KeyboardHook`, `WheelScroller`, `CursorWaker`, `ScreenCapture`, `ScreenMetrics`, `WindowsStartup`, `UiAccess`, `SoundFeedback` |
| `Languages`, `Keyboards`, `Dictionaries` | data files copied next to `GazeBar.exe` (texts, keyboard layouts, prediction dictionaries) |
| `Resources` | the program icon |
| `Install` | the installer of the full installation (UI access): `GazeBar.iss` (Inno Setup) and `setup-certificate.ps1` |

## Eyetracker DLLs

The three Tobii DLLs (`Tobii.Interaction.Net.dll`, `Tobii.Interaction.Model.dll`,
`Tobii.EyeX.Client.dll`) are proprietary and are not shipped with GazeBar. The NuGet package is
only used to compile (`ExcludeAssets="runtime;build"` in `GazeBar.csproj`), and a build target
deletes any `Tobii.*.dll` from the output folder; the CI build fails if one is left.

At startup, `EyetrackerLoader` looks for them on the PC, in this order:

1. the `Eyetracker` subfolder next to `GazeBar.exe`;
2. the installation folders of the manufacturer's software (`Program Files\Tobii`,
   `Program Files\Tobii Dynavox`);
3. the NuGet cache (`%USERPROFILE%\.nuget\packages\tobii.interaction\0.7.3`): on the PC where
   GazeBar is compiled, there is nothing to do.

The .NET DLLs are given to .NET through the `AssemblyResolve` event; the native DLL is loaded
beforehand with `LoadLibrary`, in 32-bit. `EyetrackerGazeSource` uses the Tobii.Interaction types only in
`ConnectEyetracker`, so that GazeBar starts even without these DLLs. The log notes where each
DLL was found. How a user gets them: see the [user guide](user-guide.md#eyetracker-files).

## Classes and modes

- `OverlayWindow`: the "overlay", a transparent window that covers the whole screen, always on
  top, that clicks go through and that never takes the focus. It draws the circle, the progress
  ring and the options icon.
- `Modes/ModeManager`: knows the current mode and switches from one mode to another; about 60
  times per second, it gives the gaze position (`GazeState`) to the current mode.
- `Modes/Mode`: base shared by all modes (`Enter`, `Update`, `Quit`).
- `Modes/MovementMode` (step 2, default mode): a smoothed circle follows the gaze; with **Mouse
  tracking**, the cursor follows it too. When the gaze stays still for `FixationDelay`, the mode
  switches to fixed gaze (without a progress ring: the ring is kept for buttons).
- Each mode receives the gaze position in `Enter(gaze)` to start, and takes the circle where it
  is drawn: no jump from one mode to the next, no default place.
- `Gaze/FixationDetector`: recognises a fixation. A sample is still if its speed stays under
  `MaxFixationSpeed` AND it stays within `FixationRadius` of the anchor point (the mean of the
  fixation's samples). It takes `FixationDelay` seconds of stillness, measured in real time. A
  single outlier sample is ignored.
- `Modes/FixedGazeMode`: the circle freezes (white border) with an options icon stuck to the side
  facing the centre of the screen, and the cursor goes to the fixed point. If `DirectClick` is on
  (off by default, "Fixation" tab of the settings), looking inside the circle for
  `DirectClickDelay` makes a left click; looking at the icon for `OptionsIconDelay` (0.3 s,
  shorter than the other buttons; the icon lights up with a light green flash) opens the action choice (or first the magnifier if
  `MagnifierBeforeAction` is `true` in `settings.json`); leaving the circle and the icon goes
  back to movement mode. After a click, a new fixation at the same place is not recognised until
  the gaze has left it (no bursts of clicks).
- `Modes/ActionChoiceMode` (step 3): the area around the cursor (a square of 2 × `CircleRadius`)
  is captured (`ScreenCapture`), then shown enlarged ×2 in the middle of a black screen, with a
  cross in place of the cursor. Around it, a crown of eight slots, two per side of the picture:
  double click and left click (top), right click and left drag and drop (right), right drag and
  drop and magnifier (bottom), android drag and drop and scrolling (left, step 8). On the left
  edge of the screen, the three modifiers Ctrl, Shift, Alt (step 6), 60 px from the edge and
  60 px apart. An empty slot is drawn greyed with a "+". On the right, a "back" icon. A small
  circle (half of `CircleRadius`), above everything, shows where the gaze is. Looking at an icon
  for `ActivationDelay` activates it (progress ring). Before the click, the black screen is
  hidden and GazeBar waits 60 ms for it to disappear; then back to movement mode.
- `Modes/MagnifierMode` (step 5): refines the cursor position. The same area is captured, then
  shown enlarged ×`MagnifierZoom` (×4, i.e. 800 × 800 px) in the middle of a black screen; a
  small 50 px circle follows the gaze. When the gaze fixes on the picture (same detection as in
  movement mode), the point is brought back to real coordinates (offset divided by 4), the cursor
  is placed there and the action choice opens on that point. A 30 px gaze error is then only 7
  to 8 real pixels. The "back" icon (right) returns, doing nothing, to the mode it came from: the
  action choice, or movement mode when coming from fixed gaze. At opening, a fixation where the
  gaze already was is ignored until the gaze has left it.
- Modifiers (step 6, `Modifiers`, `KeyboardInjector`): in the action choice, looking at "Ctrl",
  "Shift" or "Alt" turns it on (green border), a second look turns it off. The keys that are on
  are pressed just before the action and released just after (after the drop for a drag). They
  turn off after each action, and at each return to movement mode.
- Drag and drop (step 6): from the crown of the action choice, in three stages.
  1. `Modes/DragTargetMode`: the circle follows the gaze, a dotted white line starts from the
     starting point; a fixation chooses the end point.
  2. `Modes/MagnifierMode`: the magnifier refines the end point.
  3. `Modes/DragExecutionMode`: button pressed at the start, cursor moved to the end point in
     `DragDuration` (0.5 s), button released. For the **android** drag and drop, a pause of
     `AndroidPauseDuration` (1 s) separates the press from the movement (it replaces the long
     press).
  During stages 1 and 2, a "back" button on the right edge of the screen (vertically centred)
  cancels without pressing anything. `Actions/DragGesture` describes the current drag (start,
  button, pause).
- `GazeButton`: a button activated by gaze (symbol of the Windows font, badge, ring, label on
  hover), drawn on the overlay. It can be empty (greyed "+"), unavailable (its symbol, greyed) or
  lit (green border, for a switch).
- `ButtonAimer` (step 4): decides which button is looked at and when it activates; shared by the
  action choice and the menu.
- `Modes/MenuMode` (step 4): looking at the bottom, off the screen, for `MenuDelay` (0.5 s)
  opens a bar centred at the bottom of the screen (buttons only, no background, 100 px apart
  on a 4K screen: issue #54), only from the movement, pause and game modes
  (the `ModeManager` watches for it). The magnifier, the action choice and the drag and drop
  have their own "back" button. Buttons: Settings, Game, Keyboard (the gaze keyboard, step 12),
  Pause (or Resume when coming from pause), Mouse tracking (switch, green border when on), Quit
  GazeBar (with a "Yes" / "No" confirmation). Looking outside the bar for `MenuCloseDelay` (1 s)
  closes it and goes back to the previous mode. A small circle shows where the gaze is.
- `Modes/SettingsMode` and `SettingsWindow` (step 7): "Settings" button of the menu. A
  full-screen window, black background, with four collapsible categories on the left: Gaze
  (Gaze, Fixation, Delays), Actions (Magnifier, drag, Scroll, Icons), Display (Display,
  Keyboard, Game) and System (General, Profiles, Tools). Only the open category shows its tabs,
  below it and indented; looking at another category opens it on its first tab (list in
  `SettingsCatalog`). Each setting: its name, an explanation, then "−", the value, "+" (or a
  rounded switch for a yes / no: looking at it toggles it). Keeping the gaze on "−" or "+"
  repeats the change every 0.4 s. "Default" (top right) resets the values of the shown tab, after
  confirmation; "Back" (middle of the right edge) or Escape goes back to movement mode. Numbers
  are shown with a point (0.16). The caregiver can click a value to type it (Enter confirms,
  Escape cancels; point or comma accepted). Each change is applied at once and saved. Unlike the
  overlay, this window accepts the mouse and the keyboard. `IGazeTarget` lets the `ButtonAimer`
  aim at its targets.
- Single instance (step 7, `App`): if GazeBar is already running, a second start closes at once,
  without showing anything, and notes it in the log.
- `WindowsStartup` (step 7): the `StartWithWindows` option (General tab) adds GazeBar to the
  Windows startup list (registry, `HKEY_CURRENT_USER\...\Run`), without administrator rights:
  administrator windows still ignore its clicks.
- `Modes/ScrollMode` (step 8): "Scroll" action of the crown. The white circle stays fixed on
  the cursor. Gaze inside the circle: nothing scrolls (dead zone). Gaze above, below, right or
  left: the window under the cursor scrolls in that direction (Windows mouse wheel, one direction
  at a time), faster as the gaze gets further from the circle, up to `MaxScrollSpeed` (5 notches/s
  by default) three `CircleRadius` beyond the edge. Gentle start (issue #52): the speed follows a
  curve with the distance (squared: slow near the circle) and each start begins at 20 % of it,
  reaching 100 % in 1.5 s. A small green arc on the circle shows the
  direction. Nothing scrolls during the first 0.4 s, unless the gaze has been inside the circle
  (on entry the gaze is still on the crown icon), nor when the gaze is lost, more than 200 px off the screen (a gaze slightly off the screen counts, to widen the
  speed range), or on the "back" button (right edge, centred), which goes back to movement mode.
  Ctrl on in the action choice: Ctrl + wheel (zoom). Fine scrolling (small wheel amounts, up to
  once per frame, and a smoothed speed that rises and falls gently); on entering, GazeBar
  activates the window under the cursor (without clicking), so there is no need to click before
  scrolling. "Scroll" tab: option "Scroll inactive windows" (yes by default), which sets the
  Windows option of the same name (`WheelScroller`, applied at startup and at each change).
- `Platform/SoundFeedback` (step sound-feedback): optional short sound when a button activates.
  `ButtonAimer` plays it at each activation (its `Sound` property: `Button` by default, `Key` for
  the gaze keyboard), `FixedGazeMode` for the options icon. Two settings in the "Sound" tab,
  `KeySound` and `ButtonSound` (no by default). The two sounds are small WAVs computed in memory
  and played with `System.Media.SoundPlayer` (in .NET Framework, no extra library).
- `Modes/IconConfigMode` (step 10): "Icons" tab of the settings, "Configure the icons" button.
  Same layout as the action choice (`ActionChoiceLayout`, shared by both modes), with a grey
  frame in place of the picture. The fourteen slots (eight of the crown, three modifiers on the
  left, Copy / Cut / Paste at the top) carry a badge: a red "−" on an assigned icon (looking at
  it removes the action), a green "+" on an empty slot (looking at it opens a list, in a
  rounded rectangle three quarters of the screen wide, with all the actions of the same group and
  "Cancel"; an action can be placed several times). Each change is saved in `settings.json`
  (lists `CrownIcons`, `ModifierIcons`, `UtilityIcons`, version 6; "" = empty slot); "Default"
  in the "Icons" tab restores the original layout. In the action choice, an empty slot is greyed
  and does nothing. "Back" or Escape goes back to the "Icons" tab. List of the actions:
  `ActionCatalog`.
- `Modes/GameMode` (step 9): "Game" button of the menu. Nothing is drawn; the user clicks
  with an external switch or voice recognition. `GameMouse` "direct" (default, for Mindball
  Play): the cursor follows the gaze like movement mode with mouse tracking, with its own
  `GameSmoothing`. `GameMouse` "relative" (untested): mouse movements for games that read raw
  input, either "tracking" or "gamepad" (`GameRelative`, `GameSensitivity`). The menu opens
  from this mode ("Quit the game").
- `Modes/PauseMode` (step 4): nothing follows the gaze any more; only the menu can still open
  ("Resume" button).
- `EyetrackerLoader`: finds and loads the eyetracker DLLs (Tobii.Interaction) at startup; they
  are not shipped with GazeBar (see "Installation"). The .NET DLLs are given to .NET through the
  `AssemblyResolve` event; the native DLL is loaded beforehand with `LoadLibrary`, in 32-bit.
  `EyetrackerGazeSource` uses the Tobii.Interaction types only in `ConnectEyetracker`, so that GazeBar
  starts even without these DLLs.
- `Modes/CalibrationMode` and `Calibration` (step 4): "Calibration" button of the "Tools" tab of
  the settings (with "Diagnostic"). They start the eyetracker calibration tool and wait for its
  window to close before going back to movement mode. The exact name of the command could not be
  checked: the log notes what was found and tried.
- `KeyboardHook` (step 4): the **Escape** key goes back to movement mode from any mode, for the
  caregiver. The key also reaches the active program normally. Since step 12, in movement mode,
  it closes the gaze keyboard; the "Esc" key of the gaze keyboard, simulated by GazeBar, is
  ignored by the hook (it only goes to the application).
- Gaze keyboard (step 12, `GazeKeyboard`, `KeyboardKey`, `KeyboardLayout`): "Keyboard" button of
  the menu, or "Keyboard" action of the action choice (opens / closes the keyboard; before opening it, a left
  click on P gives the focus to the aimed field and places the text cursor there; not placed by
  default: put it in place of another one in the icon configuration screen). A keyboard on the
  lower half of the screen (`KeyboardHeight`), above everything, without reserving space or
  taking the focus. Its five pages (letters, accents, 123.!, symbols and navigation, F1 to F12)
  come from a file of the `Keyboards` folder (`en-qwerty.json`, `fr-azerty.json`), read again at
  each opening: a key can be changed without recompiling (format explained in
  `KeyboardLayout.cs`). Gaze on the keyboard: the cursor does not move, the circle is hidden, the
  looked-at key lights up (with a thin light blue outline that fades in; a single gaze sample is
  enough, against two for the other buttons) then flashes green and acts after `KeyDelay`
  (0.6 s); keeping the gaze on it types it again (`KeyRepeat`). Gaze elsewhere: normal movement
  mode; the keyboard is hidden during the magnifier, the action choice, the settings, the
  pause… and comes back afterwards. Text sent as Unicode (accents work whatever the Windows
  keyboard layout), special keys and shortcuts as real keys. Shift (one letter), Caps Lock,
  Ctrl, Alt, Win (for the next key); automatic capital after ".", "?", "!" (`AutoCapitalize`).
  "⌫ ABC" deletes the current word (or Ctrl + Backspace). Keyboard menu bar (gaze below the
  screen, which then replaces the usual bar): Up / down (keyboard at the top of the screen,
  position remembered: `KeyboardAtTop`), Size (100, 75 or 50 %, width and height together,
  keyboard centred: `KeyboardSize`, also in the settings), Transparency (0, 25, 50 or 75 % of
  the backgrounds: keyboard, keys and input box; labels, hover and green flash stay opaque:
  `KeyboardTransparency`, `settings.json` version 15, issue #55), Pause. When it opens, the keyboard at
  the bottom slides up above it (and back down). Closing: button on the right above the keyboard
  (below it if the keyboard is at the top), 120 px away from it on a 4K screen (`CloseGap4K`,
  issue #56) so it is not triggered by mistake, keyboard bar, or Escape. Tab is a real tab: its width
  (2 or 4 spaces) is set in the software being typed in. Typed text is never written to the log.
  Settings: "Keyboard" tab of the settings (`settings.json` version 9).
- Word prediction (step 13, `WordPredictor`): the three boxes at the top of the keyboard suggest
  the three most likely words that start with the current word (letters typed since the last
  space, punctuation, Enter, Tab or arrow), ignoring accents and case ("ete" suggests "été"). If
  what is typed is already a known word, it is in the 1st box: looking at it just writes the
  space after it. Looking at a box writes the whole word followed by a space (no space after the
  French elided forms "l'", "qu'"…); the word keeps the starting capital. Ranking: frequency in
  the dictionary + 20 per use, divided by 1.6 for each letter still to type (short words first,
  issue #71). An unknown word of 2 characters or more shows in the 1st box with a
  "+" (amber): looking at it adds it to the personal dictionary. A number (digits and hyphens
  only, such as "2026") never gets a "+"; a word mixing letters and digits ("mp3") does (issue #61). Files: `Dictionaries/<language>.txt`
  (copied next to GazeBar.exe, read in the background at startup) and
  `%APPDATA%\GazeBar\personal-dictionary.json` (added words and usage counts; never in the
  repository), saved after each addition and, for the usage counts, once a minute and at
  closing. Next word: GazeBar remembers the pairs of words written one after the other ("I" →
  "am"); right after a space, the boxes suggest the words that most often followed the previous
  word, and while typing these words move ahead (+ 200 per time). The dictionaries have no
  pairs: these suggestions appear as sentences are written. Punctuation or a line end breaks
  the link.
- English keyboard (step clavier-anglais): `Keyboards/en-qwerty.json` ("English QWERTY",
  QWERTY letters, accents page É,Ü,Ñ) and the dictionary `Dictionaries/en.txt` (about 62,000
  words taken from FrequencyWords, OpenSubtitles 2018 subtitles, filtered by the en_US Hunspell
  spelling dictionary; the contractions "don't", "I'm"… have an estimated frequency). The French
  dictionary `Dictionaries/fr.txt` has about 125,000 words taken from Lexique 3.83. The
  dictionary follows the INTERFACE language ("Language" button of the settings), not the layout:
  the "Keyboard layout" button only changes the keys. A dictionary whose first line is
  `#apostropheInWord` (English) keeps an apostrophe after a letter inside the word ("don'"
  suggests "don't"); otherwise (French) the apostrophe ends the elided word ("l'"). A new
  installation starts with the English keyboard; an older settings file (before version 10)
  keeps the French keyboard.
- Keyboard options (step 14, `KeyboardListMode`): two more buttons in the keyboard bar, and a
  "My words" button in the General tab of the settings. "Input box" (switch, green border,
  `InputBox`): a band above the keys shows the last typed words (a plain preview: the text
  always goes to the software; it empties when the text cursor may have moved: arrow, Tab,
  shortcut, click elsewhere). "Keyboard layout": list of the layouts of the `Keyboards` folder
  (green border = the one in use); the choice is remembered (`KeyboardLayout`). "My words"
  (General tab of the settings): list of the words added with "+", in pages of 12; looking at a
  word removes it from the personal dictionary (with its usage counts and pairs).
  `settings.json` version 10.
- The overlay puts itself back above all windows twice per second.
- `app.manifest`: declares GazeBar "DPI aware", so that the gaze, the cursor and the drawing use
  the same pixels, even with a Windows scaling of 125 % or 150 %.
- `app.uiaccess.manifest` (step `ui-access`): same manifest with `uiAccess="true"`, used only by
  the "full installation" build (`dotnet build -p:UiAccess=true`). "UI access" puts the gaze
  circle above the Start menu and lets clicks and keys reach windows started as administrator.
  Windows starts such a program only if it is signed by a trusted certificate and installed in
  `C:\Program Files`, so Visual Studio and the simple .zip keep `app.manifest`. The automatic
  build checks that the two manifests differ only by `uiAccess`. `UiAccess.IsActive()` writes
  "UI access: yes" or "no" to the log at startup. The installer (step `installer`) ships this build:
  `Install/GazeBar.iss` makes `GazeBar-<version>-setup.exe` with
  [Inno Setup](https://jrsoftware.org/isinfo.php) 6, a build tool only (`make-setup.ps1` in
  `.github/scripts` installs it on the GitHub machine if needed). The installer copies the build
  to `C:\Program Files\GazeBar`, runs `setup-certificate.ps1`, which signs it with a certificate
  made on the user's computer (its private key deleted right after), and adds a Start menu
  shortcut and an entry in "Settings > Apps" of Windows. Its uninstaller removes the certificate
  and the "Start with Windows" entry. It starts GazeBar with ShellExecute (flag `shellexec`):
  CreateProcess refuses a program with UI access (error 740). Its texts are in the `.iss` file
  (French or English, from the Windows language), not in `Languages/`, because GazeBar is not
  running then.

## Settings file

All settings are in the `AppSettings` class (file `AppSettings.cs`) and saved in
`%APPDATA%\GazeBar\settings.json`, created at the first start with the default values of the
specification. It can be opened with Notepad (decimal numbers with a point: `0.3`), then GazeBar
restarted. An unreadable file is set aside (`settings.json.unreadable`) and replaced by the
default values.
`FileVersion` (currently 13) says which version wrote the file: an older file is converted at
startup (see the list of versions at the top of `AppSettings.cs`). The settings window lists its
tabs and settings in `SettingsCatalog.cs`.

Since step 11 (`ScreenMetrics.cs`), sizes are in **% of the screen height** and no longer in
pixels: `CircleRadius` 8.8 (190 px in 4K, 95 px in 1920 × 1080), `IconSize` 5.6,
`FixationRadius` 1.4, `CursorDeadZone` 0.4, `MaxFixationSpeed` 42 (%/s). An older file
(version 6) is converted once, by dividing the pixels by 21.6 (values tuned on a screen 2160 px
high). `InterfaceSize` (75, 100, 125 or 150 %, "Display" tab) enlarges or shrinks everything
that is drawn, except the gaze settings. The overlay texts also follow the Windows text size
(Accessibility). Changes of resolution, scaling and text size are detected while GazeBar runs.

### Settings profiles

Step 15, `SettingsProfiles.cs`. "Profiles" tab of the settings (System
category). "Save the current settings" opens a name page: a text box already holding a dated
name (`Settings YYYY-MM-DD HHhMM`, in the interface language) that the user can keep or replace
with the gaze keyboard, shown for this input; "Save" (or Enter) copies the settings to
`Documents\GazeBar\Profiles\<name>.json` (if the name exists, a question asks before replacing
it), "Cancel" saves nothing. The caregiver can rename or delete these files in File Explorer. "Load a profile"
shows the `.json` files of this folder as large buttons (the most recent first, 12 per page);
after "Yes", the current settings are kept in "Automatic backup" (file `auto-backup.json`,
replaced at each load, to go back with the eyes), then the profile is applied at once and saved
in `settings.json`. A profile from an older version is converted like `settings.json` at
startup. "Start with Windows" and "Detailed log" do not change (settings of the computer), nor
does the interface language or the personal dictionary.

### Interface language

Step langues-socle, `Texts.cs`. Every displayed text is in language
files, in the `Languages` folder copied next to GazeBar.exe (`Languages/en.json`,
`Languages/fr.json`). Each text has a key (`"menu.settings": "Settings"`); `{0}`, `{1}` are
replaced by values. English is the reference language (step langue-anglais): it is the language
of a new installation, and a text missing from the chosen language is taken in English (the
missing key is noted in the log); an older settings file (before version 12) keeps French. The
"Language" button (General tab) switches to the next language of the folder and redraws the page
at once; the choice is the `Language` setting (`settings.json` version 12). It also chooses the
word prediction dictionary (see "English keyboard" below).
To add a language: copy `en.json` to `xx.json` and translate the values (never the keys).

## Log and diagnostic

- Log: `%LOCALAPPDATA%\GazeBar\gazebar.log`. `App.Log` writes the important events and the
  errors (always); `App.LogDetail` writes the frequent events (modes, actions, keys, menu…)
  only when the "Detailed log" setting is on. At 5 MB the file becomes `gazebar.old.log` and a
  new one starts. Never write gaze coordinates or typed text to the log.
- `DiagnosticWindow` ("Tools" tab of the settings) shows the cursor and the gaze live;
  `CursorSampler` (1000 reads per second) only runs while this window is open.

## Build check (CI)

`.github/workflows/build.yml` compiles GazeBar on Windows for every pull request and every push
to `main`. `.github/scripts/check-data-files.ps1` checks that the keyboard and language JSON
files are valid and that every language has the same keys and `{n}` placeholders as `en.json`.

`.github/workflows/release.yml`, started by hand, builds a version with the number typed
(`-p:Version=…`, which replaces `<Version>` of `GazeBar.csproj`), checks that no eyetracker DLL
and no personal file is in the folder to ship, zips it with the licences and prepares a draft
GitHub release. `App.Version` reads the number back (assembly informational version) for the
log.
