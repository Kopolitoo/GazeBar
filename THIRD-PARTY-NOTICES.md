# Third-party notices

GazeBar itself is under the MIT licence ([LICENSE.txt](LICENSE.txt)). This file lists the parts
that come from others, and what GazeBar uses without shipping it.

## Shipped with GazeBar

### French word list: `Dictionaries/fr.txt`

- Source: [Lexique 3.83](http://www.lexique.org), by Boris New, Christophe Pallier and
  contributors.
- Licence: Creative Commons Attribution-ShareAlike 4.0 International
  ([CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)).
- The file is a derived work (only the written word and its frequency are kept) and stays under
  CC BY-SA 4.0. Details of the changes and the references to cite:
  [Dictionaries/LICENSE-Lexique.txt](Dictionaries/LICENSE-Lexique.txt) (in French).

### English word list: `Dictionaries/en.txt`

- Source: [FrequencyWords](https://github.com/hermitdave/FrequencyWords) (2018 version), by
  Hermit Dave, built from the OpenSubtitles 2018 corpus (P. Lison and J. Tiedemann, 2016).
- Licence: [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
- The file is a derived work and stays under CC BY-SA 4.0. The en_US Hunspell spelling
  dictionary ([SCOWL](http://wordlist.aspell.net), version 2020.12.07) was only used as a filter
  when building it; none of its words were added. Details:
  [Dictionaries/LICENSE-FrequencyWords.txt](Dictionaries/LICENSE-FrequencyWords.txt).

### Application icon: `Resources/GazeBar.ico`

Image first generated with ChatGPT (OpenAI), then edited by the project owner with Photocraft.
OpenAI's terms of use give the rights on generated images to the person who made them; the icon
is distributed under the MIT licence like the rest of the project.

## Used but not shipped

### Tobii.Interaction (eyetracker library)

- NuGet package `Tobii.Interaction` 0.7.3 (Tobii Core SDK), © Tobii AB.
- Proprietary licence: "Tobii Core SDK and Gaming SDK License Agreement", accepted by whoever
  downloads it.
- GazeBar only uses the package to **compile**. Its DLLs (`Tobii.Interaction.Net.dll`,
  `Tobii.Interaction.Model.dll`, `Tobii.EyeX.Client.dll`) are not in this repository and are
  not distributed with GazeBar: GazeBar finds them on the computer at startup (see
  [docs/architecture.md](docs/architecture.md#eyetracker-dlls)).
- The licence forbids saving gaze data: GazeBar never writes gaze positions to disk.

### Microsoft .NET Framework 4.8, WPF and Windows fonts

Part of Windows. GazeBar uses the icon symbols of the Windows fonts (Segoe MDL2 Assets / Segoe
Fluent Icons) without copying the fonts.

## Trademarks

Tobii, Dynavox and TD Control are registered trademarks of Tobii AB. GazeBar is independent and
is not affiliated with, endorsed by or supported by Tobii Dynavox. Windows is a trademark of
Microsoft Corporation.
