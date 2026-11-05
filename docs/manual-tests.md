# Manual tests

*Version française : [manual-tests.fr.md](manual-tests.fr.md).*

GazeBar cannot be tested automatically: these tests are done by hand, with an eyetracker. Run
the ones that match what a pull request changes, and say in the pull request which ones passed.

Before testing, turn on "Detailed log" (Settings, System, "General" tab): the log lines
mentioned below are only written with this option. Log: `%LOCALAPPDATA%\GazeBar\gazebar.log`.

## 1. Movement and fixed gaze

Stare at a button, the circle freezes (white border, options icon) and the cursor
goes to the button; look elsewhere, it moves again. With "Direct click" on ("Fixation" tab), keeping the
gaze on the button fills a ring, then a left click is made. The log notes each switch to fixed
gaze and each direct click.

## 2. Action choice

Stare at a file in File Explorer, look at the options icon, the black screen shows
the enlarged area; look at "Double click": the black screen disappears and the file opens,
without touching the mouse.

## 3. Menu and pause

Look at the bottom, off the screen: the menu bar appears: buttons only, with no
dark background behind them, and clearly spaced (also in the keyboard's menu bar). Look at "Pause":
everything stops. Look at the bottom again: only "Resume" shows, and the circle
stays hidden, even when looking quickly away (issue #49). After "Resume", the circle comes back. All with the
eyes only. Escape also goes back to movement mode.

## 4. Magnifier

Stare at a small check box, look at the options icon, then "Magnifier" in the
crown: the area appears enlarged ×4. Stare at the box on the picture: the action choice opens,
the cross is on the box; "Left click" checks it. With "Magnifier before action" on ("Magnifier, drag" tab), the magnifier opens right after the options icon.

## 5. Modifiers and drag and drop

In File Explorer, stare at a file, open the action choice, look at "Ctrl" (green
border) then "Left click"; do it again on another file: both are selected. Then stare at a file,
choose "Drag left" (four arrows, badge L), look at the destination folder (the dotted
line follows), stare at it, refine in the magnifier: the file is moved. The "back" button on the
right cancels.

## 6. Settings

Menu, "Settings": the black screen opens on the Gaze tab. Keep the gaze on the "+"
of the smoothing: the value goes up by one step every 0.4 s. Look at the Fixation tab, then the
direct click switch: it turns blue, knob on the right, "Yes". "Default", then "Yes": the values
of the tab come back. The caregiver clicks a value, types 0.5 then Enter. "Back" goes back to
movement mode. Starting GazeBar a second time does nothing (see the log).

## 7. Scrolling

Open a long web page, stare at the text, open the action choice then "Scroll"
(left, top). The white circle stays in place. Look inside the circle: nothing moves. Look a
little below the circle: the page goes down slowly; lower: faster. Above: it goes up. "Back" on
the right edge goes back to movement mode. At low speed, the page must slide without small
jumps, and start and stop gently; without having clicked in the page first, it must scroll
anyway (the log notes the window made active and the state of the Windows wheel setting).

## 8. Icon configuration

Menu, "Settings", "Icons" tab, "Configure the icons". Look at the "−" of the left
click (top left of the crown) then the one of the right click: both slots become empty ("+").
Look at the first one: the list offers all the actions of the crown; choose "Right click". Do
the same with the other slot and "Left click": the two actions have swapped places. "Back", then
in the action choice, the two icons are in their new place. Removing "Cut" leaves a greyed slot
at the top of the action choice. "Default" in the "Icons" tab puts everything back.

## 9. Screen sizes

Screen at 1920 × 1080, 100 %. At the first start, the log notes "updated to
version 7" and the detected screen; the circle, the icons and the menu bar look the same size as
before in 4K. "Display" tab: "Detected screen" gives the resolution, the scaling and the text
size; "Interface size" at 150 % enlarges the circle, the icons and the menu. While GazeBar runs,
change the resolution, then the scaling (125 %), then the Windows text size: each time, the log
notes the change and the sizes adapt (an open action choice or magnifier closes, back to
movement mode; the pause, the game and the settings stay open).

## 10. Gaze keyboard

In Notepad, open the menu, "Keyboard": the keyboard appears at the bottom. Look at
keys: they light up, flash and the text is written ("é" from the accents page). Look above the
keyboard: the circle comes back and fixed gaze works. Type for a while on the top
right keys: the Close button, clearly apart from the keyboard, is never triggered by mistake
(same below the keyboard when it is at the top). Look below the screen: the keyboard bar
opens and the keyboard slides up above it; "Up / down" puts it at the top (it reopens there next
time), "Size" goes to 75 % then 50 %, "Pause" hides it until resuming. In the icon configuration,
place "Keyboard" on the crown: in the action choice, this button opens the keyboard. In Notepad with several lines of text,
fix your gaze in the middle of a line (Notepad not active, or another window in front), choose
"Keyboard": the text cursor goes to that place and typed keys are written there. With the
keyboard open, the same button closes it, without a click.

## 11. Word prediction

Interface in French. In Notepad, open the keyboard and type "bo": the boxes
suggest "bon", "bonne"…; look at "bonjour" (after "bonj"): "bonjour" and a space are written.
"ete" suggests "été". "pr" suggests short words ("pris", "près"…) before "prendre". Type an invented word ("zorglub"): the 1st box shows "+ zorglub" in amber;
look at it: a space is written, and typing "zo" suggests the word. Type "2026": no "+" box
appears; type "mp3x": "+ mp3x" appears. Write "je suis" twice (typing
"suis" in full then a space): after "je" and a space, the 1st box suggests "suis". After closing
GazeBar, `%APPDATA%\GazeBar\personal-dictionary.json` contains the word.

## 12. Keyboard options

Keyboard open, look below the screen: the keyboard bar also shows "Input box" and
"Keyboard layout". "Input box": the border turns green, a band appears above the keys; type
"hello everyone": the band shows it; an arrow empties it. "Keyboard layout": the list shows the
layouts, the one in use with a green border; "Back to the keyboard" goes back to the keyboard.
Settings, General tab, "My words": "zorglub" (added in step 13) is in the list; look at it: it
disappears; "Back to the settings" goes back to the General tab. Typing "zo", it is no longer
suggested.
"Transparency" (shows "0 %"): look at it: "25 %", the keyboard backgrounds let the window behind
show through; again: "50 %", "75 %", then "0 %" (opaque as before). At 75 %, the letters,
the green flash and the hover stay clearly visible. Close and reopen GazeBar: the value is kept.

## 13. English keyboard

Keyboard open, "Keyboard layout": the list shows "English QWERTY" and
"Français AZERTY"; choose "English QWERTY": QWERTY letters, and with the interface in French,
"bonj" still suggests "bonjour". Settings, General, "Language" → English; back to the keyboard:
"th" suggests "the", "that", "this" (or close), "don" and "don'" suggest "don't".

## 14. Settings profiles

Settings, System, Profiles, "Save the current settings": the gaze keyboard
appears with the name page; type "Evening", look at "Save": the message gives the name of the
profile, and the file appears in `Documents\GazeBar\Profiles`. Change the circle
radius (Display). Profiles, "Load a profile": the list shows the profile; look at it: "Load the
profile …?"; "No" changes nothing; "Yes" restores the old radius and creates "Automatic backup".
Loading "Automatic backup" restores the changed radius.

## 15. Sound feedback

Settings, "Interface", "Sound" tab: both switches are off; nothing makes a sound. Turn on
"Key sound": open the keyboard and type: a short high "tick" for each key (also with key
repeat), but no sound for the menu or the action choice. Turn it off and turn on "Button sound":
the keys are silent; the options icon, the action choice, the menu buttons and the settings
buttons make a short two-note sound. Turn both off: silence again. Restart GazeBar: the choices
are kept.

## Full installation (Setup.exe, UI access)

Use the `GazeBar-setup-…` zip of the automatic build (Actions page of the pull request,
"Artifacts") or the `GazeBar-x.y.z-setup.exe` of a draft release. Keep another way to click
(voice, mouse) during the installation.

1. Double-click `GazeBar-…-setup.exe`. Windows may say "unknown publisher" ("More info", "Run
   anyway"), then asks "Yes" for administrator. The installer speaks the language of Windows;
   click "Install", then "Finish" with "Launch GazeBar" ticked: GazeBar starts and the log says
   "UI access: yes".
2. `C:\Program Files\GazeBar` exists, GazeBar is in the Start menu and in "Settings > Apps"
   of Windows.
3. Open the Windows Start menu: the gaze circle stays visible above it, and a fixed gaze then a
   left click opens an item of the menu.
4. Start Notepad as administrator (right click, "Run as administrator"): a click by gaze puts
   the text cursor in it, and the gaze keyboard types in it.
5. Run the installer again (update): no question about closing GazeBar (it is closed), same
   result, settings unchanged.
6. Uninstall it from "Settings > Apps": the folder, the shortcut and the certificate "GazeBar
   local signature" (`certlm.msc`, "Trusted Root Certification Authorities") are gone; the
   settings are still in `%APPDATA%\GazeBar`.
7. The simple zip and Visual Studio still work as before, with "UI access: no" in the log.

## Before publishing a version

Download the zip of the draft release and unzip it in a new folder (not the Visual Studio
folder). It contains `GazeBar.exe`, the `Languages`, `Keyboards` and `Dictionaries` folders, the
licences, and no `Tobii.*.dll`. Start `GazeBar.exe` (Windows may ask "Run anyway"): the gaze
works, the gaze keyboard suggests words, and the first lines of
`%LOCALAPPDATA%\GazeBar\gazebar.log` say "GazeBar 0.1.0 starting" (the number of the version).
The draft also has `GazeBar-0.1.0-setup.exe`: run the "Full installation" test above with it.
