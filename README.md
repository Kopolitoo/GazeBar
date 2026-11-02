# GazeBar

*Version française : [README.fr.md](README.fr.md).*

Software to control a Windows computer with eye gaze, using an eyetracker (developed and tested
with the Tobii Dynavox PCEye 5), organised in modes (movement, fixed gaze, action choice,
menu…). WPF application (.NET Framework 4.8, 32-bit); code and comments in English, interface in
English and French.
The gaze control software supplied with the eyetracker is paused while GazeBar is used.

> Since step 4, the old toolbar at the top of the screen is gone: everything is done with the
> eyes, and the menu bar opens by looking **at the bottom, off the screen**. The sections
> "Goal of this first version" and "Test protocol" below describe the first versions (with the
> toolbar) and are kept for reference.

> Working together, branches and GitHub: see [docs/GUIDE-GIT.md](docs/GUIDE-GIT.md) (in French).

> Contributing: see [CONTRIBUTING.md](CONTRIBUTING.md).

## Goal of this first version

1. **Check that the cursor driven by the eyetracker software is a good substitute for the gaze**:
   rate, regularity, coverage of the whole screen.
2. Offer three actions that this software does not have:
   - **Long press** (left button held for 1.5 s);
   - **Drag and drop with the left button**;
   - **Drag and drop with the right button**.

## Installation

**Prerequisite: a compatible eyetracker (Tobii); download and install its official driver from
the manufacturer's website.**

- Visual Studio 2026 with the ".NET desktop development" workload and the .NET Framework 4.8
  developer pack.
- The eyetracker engine must be running (tobii.service / Tobii interaction engine process), as
  for Mill Mouse.
- Open `GazeBar.slnx`, then press F5.

Parts of this code were written without being compiled: if Visual Studio reports an error,
send its text and it will be fixed.

### The eyetracker DLLs are not shipped with GazeBar

The gaze is read by three Tobii DLLs (`Tobii.Interaction.Net.dll`, `Tobii.Interaction.Model.dll`
and `Tobii.EyeX.Client.dll`). They are proprietary code of Tobii AB: GazeBar does not contain
them and does not distribute them. The NuGet package `Tobii.Interaction` is only used to
compile; its DLLs are not copied next to `GazeBar.exe` (and are deleted from the output folder
if they were there).

At startup, GazeBar looks for them on the PC itself (`EyetrackerLoader`), in this order:

1. the `Eyetracker` subfolder next to `GazeBar.exe`;
2. the installation folders of the manufacturer's software (`Program Files\Tobii`,
   `Program Files\Tobii Dynavox`);
3. the Visual Studio NuGet cache (`%USERPROFILE%\.nuget\packages\tobii.interaction\0.7.3`):
   on the PC where GazeBar is compiled, there is therefore nothing to do.

On another PC, if they cannot be found, the Diagnostic window says so. You then have to get them
yourself, accepting Tobii's licence: download the `Tobii.Interaction` package, version 0.7.3,
from nuget.org ("Download package"), rename the `.nupkg` file to `.zip`, open it, and copy into
GazeBar's `Eyetracker` subfolder the two DLLs of `lib\net45` and the one of `build\x86`. The log
(`gazebar.log`) notes where each DLL was found.

## Principle

The real gaze is read by the eyetracker engine (NuGet package Tobii.Interaction, class
GazeSource): about 30 samples per second, including off the screen (negative coordinates or
coordinates larger than the screen size). The bar and the Diagnostic window show it in green,
next to the cursor driven by the eyetracker software.

For the buttons: the eyetracker software moves the Windows cursor with the gaze. GazeBar reads
the position of this cursor (`GetCursorPos`) and only injects button presses and releases
(`SendInput`), never a movement. While a button is held, that software keeps moving the cursor:
this is what produces the drag.

The bar never takes the focus, so as not to disturb the window being used.

## Test protocol

1. Start GazeBar. The bar appears at the top of the screen with "Cursor: X … Y … | N changes/s".
   Look at different areas of the screen: the values must follow the gaze, corners included.
2. **Diagnostic** button ("Tools" tab of the settings): look over the corners, the edges and the
   centre; the thumbnail shows the cursor and the gaze live. GazeBar never saves gaze data to
   disk (the eyetracker licence forbids it): neither positions files nor coordinates in the log.
3. **Long press**: click the button (3 s countdown), look at the wanted place. The left button
   is pressed at the end of the countdown, then released 1.5 s later.
4. **Drag (right click)**: for example on a file in File Explorer, the move menu appears on
   release. **Drag (left)**: for example in Paint.
5. **Gaze mouse** (toggle button): the cursor follows the gaze directly, smoothed. Looking off
   the screen pauses it. Settings `Smoothing` and `CursorDeadZone` in `settings.json`. In this
   mode, drag and drops follow the real gaze.
6. **Movement mode** (toggle button, on at startup): the circle follows the gaze, a fixation
   freezes the circle and puts the cursor on the fixed point (see below).
   **Mouse tracking** (toggle button, off by default): the cursor follows the circle too;
   otherwise, it only joins the gaze once the gaze is fixed. This setting is saved.
7. **Cancel** releases everything at once. **Quit** closes the program.

## Settings

All settings are in the `AppSettings` class (file `AppSettings.cs`) and saved in
`%APPDATA%\GazeBar\settings.json`, created at the first start with the default values of the
specification. It can be opened with Notepad (decimal numbers with a point: `0.3`), then GazeBar
restarted. An unreadable file is set aside (`settings.json.unreadable`) and replaced by the
default values. The old files `HoldSession.cs` (long press, 3 s countdown) and `GazeMouse.cs`
were removed at step 6, together with the settings that were no longer used.

Since step 11 (`ScreenMetrics.cs`), sizes are in **% of the screen height** and no longer in
pixels: `CircleRadius` 8.8 (190 px in 4K, 95 px in 1920 × 1080), `IconSize` 5.6,
`FixationRadius` 1.4, `CursorDeadZone` 0.4, `MaxFixationSpeed` 42 (%/s). An older file
(version 6) is converted once, by dividing the pixels by 21.6 (values tuned on a screen 2160 px
high). `InterfaceSize` (75, 100, 125 or 150 %, "Display" tab) enlarges or shrinks everything
that is drawn, except the gaze settings. The overlay texts also follow the Windows text size
(Accessibility). Changes of resolution, scaling and text size are detected while GazeBar runs.

**Settings profiles** (step 15, `SettingsProfiles.cs`): "Profiles" tab of the settings (System
category). "Save the current settings" copies the settings to
`Documents\GazeBar\Profiles\Settings YYYY-MM-DD HHhMM.json` (the file name is in the interface
language); the caregiver can rename or delete these files in File Explorer. "Load a profile"
shows the `.json` files of this folder as large buttons (the most recent first, 12 per page);
after "Yes", the current settings are kept in "Automatic backup" (file `auto-backup.json`,
replaced at each load, to go back with the eyes), then the profile is applied at once and saved
in `settings.json`. A profile from an older version is converted like `settings.json` at
startup. "Start with Windows" does not change (a setting of the computer), nor does the
interface language or the personal dictionary.

**Interface language** (step langues-socle, `Texts.cs`): every displayed text is in language
files, in the `Languages` folder copied next to GazeBar.exe (`Languages/en.json`,
`Languages/fr.json`). Each text has a key (`"menu.settings": "Settings"`); `{0}`, `{1}` are
replaced by values. English is the reference language (step langue-anglais): it is the language
of a new installation, and a text missing from the chosen language is taken in English (the
missing key is noted in the log); an older settings file (before version 12) keeps French. The
"Language" button (General tab) switches to the next language of the folder and redraws the page
at once; the choice is the `Language` setting (`settings.json` version 12). It also chooses the
word prediction dictionary (see "English keyboard" below).
To add a language: copy `en.json` to `xx.json` and translate the values (never the keys).

## Modes architecture

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
- `Modes/FixationDetector`: recognises a fixation. A sample is still if its speed stays under
  `MaxFixationSpeed` AND it stays within `FixationRadius` of the anchor point (the mean of the
  fixation's samples). It takes `FixationDelay` seconds of stillness, measured in real time. A
  single outlier sample is ignored.
- `Modes/FixedGazeMode`: the circle freezes (white border) with an options icon stuck to the side
  facing the centre of the screen, and the cursor goes to the fixed point. If `DirectClick` is on
  (off by default, set it to `true` in `settings.json`), looking inside the circle for
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
  cancels without pressing anything. `Modes/DragGesture` describes the current drag (start,
  button, pause).
- `GazeButton`: a button activated by gaze (symbol of the Windows font, badge, ring, label on
  hover), drawn on the overlay. It can be empty (greyed "+"), unavailable (its symbol, greyed) or
  lit (green border, for a switch).
- `ButtonAimer` (step 4): decides which button is looked at and when it activates; shared by the
  action choice and the menu.
- `Modes/MenuMode` (step 4): looking at the bottom, off the screen, for `MenuDelay` (0.5 s)
  opens a bar centred at the bottom of the screen, only from the movement, pause and game modes
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
  by default) three `CircleRadius` beyond the edge. A small green arc on the circle shows the
  direction. Nothing scrolls before the gaze has once been inside the circle, nor when the gaze
  is lost, more than 200 px off the screen (a gaze slightly off the screen counts, to widen the
  speed range), or on the "back" button (right edge, centred), which goes back to movement mode.
  Ctrl on in the action choice: Ctrl + wheel (zoom). Fine scrolling (small wheel amounts, up to
  once per frame, and a smoothed speed that rises and falls gently); on entering, GazeBar
  activates the window under the cursor (without clicking), so there is no need to click before
  scrolling. "Scroll" tab: option "Scroll inactive windows" (yes by default), which sets the
  Windows option of the same name (`WheelScroller`, applied at startup and at each change).
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
- `Modes/PauseMode` (step 4): nothing follows the gaze any more; only the menu can still open
  ("Resume" button).
- `EyetrackerLoader`: finds and loads the eyetracker DLLs (Tobii.Interaction) at startup; they
  are not shipped with GazeBar (see "Installation"). The .NET DLLs are given to .NET through the
  `AssemblyResolve` event; the native DLL is loaded beforehand with `LoadLibrary`, in 32-bit.
  `GazeSource` uses the Tobii.Interaction types only in `ConnectEyetracker`, so that GazeBar
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
  the menu, or "Keyboard" action of the action choice (opens / closes the keyboard; not placed by
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
  keyboard centred: `KeyboardSize`, also in the settings), Pause. When it opens, the keyboard at
  the bottom slides up above it (and back down). Closing: button on the right above the keyboard
  (below it if the keyboard is at the top), keyboard bar, or Escape. Tab is a real tab: its width
  (2 or 4 spaces) is set in the software being typed in. Typed text is never written to the log.
  Settings: "Keyboard" tab of the settings (`settings.json` version 9).
- Word prediction (step 13, `WordPredictor`): the three boxes at the top of the keyboard suggest
  the three most likely words that start with the current word (letters typed since the last
  space, punctuation, Enter, Tab or arrow), ignoring accents and case ("ete" suggests "été"). If
  what is typed is already a known word, it is in the 1st box: looking at it just writes the
  space after it. Looking at a box writes the whole word followed by a space (no space after the
  French elided forms "l'", "qu'"…); the word keeps the starting capital. Ranking: frequency in
  the dictionary + 20 per use. An unknown word of 2 letters or more shows in the 1st box with a
  "+" (amber): looking at it adds it to the personal dictionary. Files: `Dictionaries/<language>.txt`
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

Step 2 test: stare at a button, the circle freezes (white border, options icon) and the cursor
goes to the button; look elsewhere, it moves again. With `DirectClick` set to `true`, keeping the
gaze on the button fills a ring, then a left click is made. The log notes each switch to fixed
gaze and each direct click.

Step 3 test: stare at a file in File Explorer, look at the options icon, the black screen shows
the enlarged area; look at "Double click": the black screen disappears and the file opens,
without touching the mouse.

Step 4 test: look at the bottom, off the screen: the menu bar appears. Look at "Pause":
everything stops. Look at the bottom again, then "Resume": the circle comes back. All with the
eyes only. Escape also goes back to movement mode.

Step 5 test: stare at a small check box, look at the options icon, then "Magnifier" in the
crown: the area appears enlarged ×4. Stare at the box on the picture: the action choice opens,
the cross is on the box; "Left click" checks it. With `"MagnifierBeforeAction": true` in
`settings.json`, the magnifier opens right after the options icon.

Step 6 test: in File Explorer, stare at a file, open the action choice, look at "Ctrl" (green
border) then "Left click"; do it again on another file: both are selected. Then stare at a file,
choose "Drag left" (four arrows, badge L), look at the destination folder (the dotted
line follows), stare at it, refine in the magnifier: the file is moved. The "back" button on the
right cancels.

Step 7 test: menu, "Settings": the black screen opens on the Gaze tab. Keep the gaze on the "+"
of the smoothing: the value goes up by one step every 0.4 s. Look at the Fixation tab, then the
direct click switch: it turns blue, knob on the right, "Yes". "Default", then "Yes": the values
of the tab come back. The caregiver clicks a value, types 0.5 then Enter. "Back" goes back to
movement mode. Starting GazeBar a second time does nothing (see the log).

Step 8 test: open a long web page, stare at the text, open the action choice then "Scroll"
(left, top). The white circle stays in place. Look inside the circle: nothing moves. Look a
little below the circle: the page goes down slowly; lower: faster. Above: it goes up. "Back" on
the right edge goes back to movement mode. At low speed, the page must slide without small
jumps, and start and stop gently; without having clicked in the page first, it must scroll
anyway (the log notes the window made active and the state of the Windows wheel setting).

Step 10 test: menu, "Settings", "Icons" tab, "Configure the icons". Look at the "−" of the left
click (top left of the crown) then the one of the right click: both slots become empty ("+").
Look at the first one: the list offers all the actions of the crown; choose "Right click". Do
the same with the other slot and "Left click": the two actions have swapped places. "Back", then
in the action choice, the two icons are in their new place. Removing "Cut" leaves a greyed slot
at the top of the action choice. "Default" in the "Icons" tab puts everything back.

Step 11 test: screen at 1920 × 1080, 100 %. At the first start, the log notes "updated to
version 7" and the detected screen; the circle, the icons and the menu bar look the same size as
before in 4K. "Display" tab: "Detected screen" gives the resolution, the scaling and the text
size; "Interface size" at 150 % enlarges the circle, the icons and the menu. While GazeBar runs,
change the resolution, then the scaling (125 %), then the Windows text size: each time, the log
notes the change and the sizes adapt (an open action choice or magnifier closes, back to
movement mode; the pause, the game and the settings stay open).

Step 12 test: in Notepad, open the menu, "Keyboard": the keyboard appears at the bottom. Look at
keys: they light up, flash and the text is written ("é" from the accents page). Look above the
keyboard: the circle comes back and fixed gaze works. Look below the screen: the keyboard bar
opens and the keyboard slides up above it; "Up / down" puts it at the top (it reopens there next
time), "Size" goes to 75 % then 50 %, "Pause" hides it until resuming. In the icon configuration,
place "Keyboard" on the crown: in the action choice, this button opens the keyboard.

Step 13 test (interface in French): in Notepad, open the keyboard and type "bo": the boxes
suggest "bon", "bonne"…; look at "bonjour" (after "bonj"): "bonjour" and a space are written.
"ete" suggests "été". Type an invented word ("zorglub"): the 1st box shows "+ zorglub" in amber;
look at it: a space is written, and typing "zo" suggests the word. Write "je suis" twice (typing
"suis" in full then a space): after "je" and a space, the 1st box suggests "suis". After closing
GazeBar, `%APPDATA%\GazeBar\personal-dictionary.json` contains the word.

Step 14 test: keyboard open, look below the screen: the keyboard bar also shows "Input box" and
"Keyboard layout". "Input box": the border turns green, a band appears above the keys; type
"hello everyone": the band shows it; an arrow empties it. "Keyboard layout": the list shows the
layouts, the one in use with a green border; "Back to the keyboard" goes back to the keyboard.
Settings, General tab, "My words": "zorglub" (added in step 13) is in the list; look at it: it
disappears; "Back to the settings" goes back to the General tab. Typing "zo", it is no longer
suggested.

English keyboard test: keyboard open, "Keyboard layout": the list shows "English QWERTY" and
"Français AZERTY"; choose "English QWERTY": QWERTY letters, and with the interface in French,
"bonj" still suggests "bonjour". Settings, General, "Language" → English; back to the keyboard:
"th" suggests "the", "that", "this" (or close), "don" and "don'" suggest "don't".

Step 15 test: Settings, System, Profiles, "Save the current settings": the message gives the
name of the profile, and the file appears in `Documents\GazeBar\Profiles`. Change the circle
radius (Display). Profiles, "Load a profile": the list shows the profile; look at it: "Load the
profile …?"; "No" changes nothing; "Yes" restores the old radius and creates "Automatic backup".
Loading "Automatic backup" restores the changed radius.

## Known limits

- A window started "as administrator" ignores the injected clicks and the keys of the gaze
  keyboard, unless GazeBar is also started as administrator.
- The game mode with relative mouse movement (gaze to mouse speed) has not been studied yet.
- The personal dictionary (added words, word pairs) is shared between languages.
- Log: `%LOCALAPPDATA%\GazeBar\gazebar.log`.

## Licence

GazeBar is distributed under the MIT licence: anyone can use, modify and redistribute it freely,
provided the copyright notice is kept. The software is provided without warranty. Full text:
[LICENSE.txt](LICENSE.txt).

Exceptions: the dictionary `Dictionaries/fr.txt` is taken from
[Lexique 3.83](http://www.lexique.org) (B. New, C. Pallier et al.) and stays under the
CC BY-SA 4.0 licence: see [Dictionaries/LICENSE-Lexique.txt](Dictionaries/LICENSE-Lexique.txt)
(in French). The dictionary `Dictionaries/en.txt` is taken from
[FrequencyWords](https://github.com/hermitdave/FrequencyWords) (H. Dave, from OpenSubtitles
2018) and stays under the CC BY-SA 4.0 licence: see
[Dictionaries/LICENSE-FrequencyWords.txt](Dictionaries/LICENSE-FrequencyWords.txt).

## Legal notice

This project is independent, open source software. It is not affiliated with, endorsed by or
supported by Tobii Dynavox. TD Control, Tobii and Dynavox are registered trademarks of Tobii AB.

The Tobii DLLs (Tobii Core SDK) are not part of GazeBar and are not distributed with it; they
remain the property of Tobii AB and are subject to their own licence ("Tobii Core SDK and Gaming
SDK License Agreement"), which everyone accepts by downloading them.
