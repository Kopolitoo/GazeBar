# GazeBar user guide

*Version française : [user-guide.fr.md](user-guide.fr.md).*

GazeBar lets you use a Windows computer with your eyes only: you move a circle with your gaze,
stare at a place, then choose what to do there (click, double click, drag and drop, scroll,
type…). This guide explains how to use it. To install it, see the [README](../README.md).

## Before you start

- Install the official driver of your eyetracker (Tobii) from the manufacturer's website, and
  calibrate it.
- **Pause** the gaze software supplied with the eyetracker (TD Control) while GazeBar is used,
  so that the two programs do not both move the mouse.
- GazeBar needs three files of the eyetracker (see [Eyetracker files](#eyetracker-files)). On
  most computers it finds them by itself.

## Moving and staring

- **Movement mode** (at startup): a white circle follows your gaze. It moves smoothly, without
  jumps.
- **Fixed gaze**: when your gaze stays still for a moment, the circle freezes, the mouse cursor
  goes to the point you stared at, and an **options icon** appears next to the circle.
  - Look at the options icon: the **action choice** opens.
  - Look elsewhere: the circle follows your gaze again.
  - With "Direct click" on ("Fixation" tab of the settings), keeping your gaze inside the circle
    makes a left click.
- **Mouse tracking** (menu): the mouse cursor follows the circle all the time, instead of only
  when the gaze is fixed.

## Action choice

A black screen shows the area you stared at, enlarged twice, with a cross on the exact point.
Around it, icons: left click, double click, right click, magnifier, drag and drop (left button,
right button, android), scroll. Look at an icon until its ring is full to use it. On the left
edge: **Ctrl**, **Shift**, **Alt** (look once to turn on, the border turns green; they are
pressed during the next action). At the top: **Copy**, **Cut**, **Paste**. On the right:
**Back**. After an action, you are back in movement mode.

The icons can be moved or removed: Settings, "Icons" tab, "Configure the icons".

## Magnifier

For small targets (a check box, a small link). The area is shown enlarged four times; stare at
the exact point on the picture, and the action choice opens on that point. "Magnifier before
action" ("Magnifier, drag" tab) opens the magnifier every time, before the action choice.

## Drag and drop

Choose a drag and drop in the action choice, then look at the destination: a dotted line
follows your gaze. Stare at the destination, refine it in the magnifier, and the item is moved.
"Android" adds a pause before moving (for touch-style apps). The "back" button on the right
edge cancels.

## Scroll

Choose "Scroll" in the action choice. The circle stays in place. Look above, below, left or
right of it to scroll that way: the further away, the faster. Look inside the circle to stop.
"Back" (right edge) ends scrolling.

## Menu

Look **below the screen** for half a second: the menu bar opens at the bottom. It opens from
movement mode, pause and game mode. Buttons:

- **Settings**: see below.
- **Game**: game mode (see below).
- **Keyboard**: the gaze keyboard.
- **Pause** / **Resume**: nothing follows your gaze any more, until you open the menu again
  and choose "Resume".
- **Mouse tracking**: on / off (green border when on).
- **Quit GazeBar**: asks "Yes" or "No" first.

Look away from the bar for one second to close it.

## Gaze keyboard

Menu, "Keyboard". The keyboard fills the lower half of the screen. Look at a key: it lights up,
flashes green and types. Above the keyboard, the circle works as usual (you can click into
another place). Five pages: letters, accents, numbers and punctuation, symbols and navigation,
F1 to F12.

- **Word prediction**: the three boxes at the top suggest words. Look at one to write it with a
  space. An unknown word appears with an amber "+": look at it to add it to your personal
  dictionary. Short words come first, unless a longer word is much more common or used often. GazeBar also learns which word usually comes next.
- **Keyboard bar** (look below the screen while the keyboard is open): move the keyboard up or
  down, change its size, pause it, show the "Input box" (a preview of what you typed), choose
  the "Keyboard layout" (QWERTY, AZERTY…).
- **My words** (Settings, "General" tab): the words you added; look at one to remove it.
- Close the keyboard with the button above it, from the keyboard bar, or with Escape.

## Game mode

Menu, "Game". Nothing is drawn; the mouse cursor follows your gaze directly (made for games like
Mindball Play). Clicks are made with an external switch or voice recognition. Open the menu and
choose "Quit the game" to go back. The "Game" tab of the settings has the game options.

## Settings

Menu, "Settings". A black full-screen page with four categories on the left: **Gaze**,
**Actions**, **Interface**, **System**. Each setting has "−" and "+" (keep your gaze on them to
repeat) or a yes / no switch. Changes apply at once and are saved.

- **Default** (top right): puts back the original values of the shown tab.
- **Back** (right edge) or Escape: back to movement mode.
- The caregiver can also use the mouse and keyboard here: click a value to type it.
- **Language** ("General" tab): English or French. The prediction dictionary follows this
  language.
- **Profiles** ("Profiles" tab): save the current settings under a name, and load them later
  with your eyes (for example a "Morning" and an "Evening" profile).
- **Sound** tab: two yes / no options, both off at first. **Key sound**: a short "tick" for each
  key of the gaze keyboard. **Button sound**: a short sound for every other button (menu, action
  choice, options icon, settings). The sound plays at the Windows volume.
- **Tools** tab: **Calibration** (starts the eyetracker calibration) and **Diagnostic** (shows
  the gaze and the cursor live).

## For the caregiver

- **Escape** on the physical keyboard brings GazeBar back to movement mode from anywhere.
- Starting GazeBar a second time does nothing: it is already running.
- "Start with Windows" ("General" tab) starts GazeBar automatically.

## Files on the computer

| What | Where |
| --- | --- |
| Settings | `%APPDATA%\GazeBar\settings.json` (can be opened with Notepad) |
| Personal dictionary | `%APPDATA%\GazeBar\personal-dictionary.json` |
| Profiles | `Documents\GazeBar\Profiles` |
| Log | `%LOCALAPPDATA%\GazeBar\gazebar.log` |

GazeBar never saves where you look (the eyetracker licence forbids it) and never writes what you
type in the log. The log is short by default; "Detailed log" ("General" tab) notes more, to
track down a problem.

## Eyetracker files

The gaze is read by three Tobii files (`Tobii.Interaction.Net.dll`,
`Tobii.Interaction.Model.dll`, `Tobii.EyeX.Client.dll`). They belong to Tobii AB, so GazeBar
does not include them. GazeBar looks for them in the `Eyetracker` folder next to `GazeBar.exe`,
then in the folders of the Tobii software, then in the Visual Studio NuGet cache.

If the Diagnostic window says they are missing, get them yourself, accepting Tobii's licence:

1. On nuget.org, open the `Tobii.Interaction` package, version 0.7.3, and click "Download
   package".
2. Rename the downloaded `.nupkg` file to `.zip` and open it.
3. Copy the two DLLs of `lib\net45` and the one of `build\x86` into the `Eyetracker` folder next
   to `GazeBar.exe` (create it if needed).

## Full installation (UI access)

The simple zip runs from any folder, but Windows keeps two things out of its reach: the Start
menu, search and notifications are drawn above the gaze circle, and windows started "as
administrator" ignore its clicks and keys. The **full installation** removes both limits. Windows
allows this "UI access" only to a program that is signed and installed in `C:\Program Files`, so
it comes with its own installer.

To install or update:

1. On the [Releases](https://github.com/Kopolitoo/GazeBar/releases) page, download
   `GazeBar-x.y.z-setup.exe` (under "Assets") and double-click it. Keep another way to click
   (voice, mouse, TD Control) for this step: GazeBar closes during the installation.
2. The installer is not signed by a known publisher, so Windows may show "Windows protected
   your PC": click "More info", then "Run anyway". Windows then asks "Do you want to allow this
   app to make changes to your device?": answer "Yes".
3. The installer speaks the language of Windows. Click "Install", then "Finish" with "Launch
   GazeBar" ticked: GazeBar starts from `C:\Program Files\GazeBar`.

What the installer does: it copies GazeBar to `C:\Program Files\GazeBar`, creates a certificate
on this computer, makes the computer trust it, signs `GazeBar.exe` with it, then deletes the
certificate's private key (nothing else can ever be signed with it). A new certificate is made at
each update. It also adds GazeBar to the Start menu and to "Settings > Apps" of Windows. Your
settings are kept. If "Start with Windows" is on, GazeBar updates the startup entry itself. It
also updates a copy installed by the old `Install.cmd` script.

To check: the log (`%LOCALAPPDATA%\GazeBar\gazebar.log`) says "UI access: yes" at startup.

To remove it: Windows "Settings > Apps > Installed apps", GazeBar, "Uninstall". It deletes the
program folder, the shortcut and the certificate. Your settings are kept.

If the eyetracker files had to be copied by hand (see [Eyetracker files](#eyetracker-files)),
put the `Eyetracker` folder in `C:\Program Files\GazeBar` (Windows asks for administrator
rights). Updates keep it.

## Known limits

- One screen only.
- With the simple zip, a window started "as administrator" ignores GazeBar's clicks and keys,
  and the Windows Start menu can hide the circle. The
  [full installation](#full-installation-ui-access) removes these two limits.
- Tested with the Tobii Dynavox PCEye 5 only.
- The game mode with relative mouse movement has not been tested yet.
- The personal dictionary is shared between languages.
