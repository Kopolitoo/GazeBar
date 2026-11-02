# Contributing to GazeBar

Thank you for your interest in GazeBar, a Windows tool that controls the mouse with eye gaze.

## Language rules

- **Code, names and comments are written in English**: classes, methods, fields, files,
  folders, log messages and code comments.
- Texts shown to the user are NOT written in the code: they live in the language files of the
  `Languages` folder (`Languages/fr.json`…) and are read with `Texts.Get("key")` or
  `Texts.Format("key", values)` (see `Texts.cs`). Log messages stay in the code, in English.
- To add a language: copy `Languages/fr.json` to `Languages/xx.json` (ISO 639-1 code), translate
  the values (never the keys) and keep the `{0}`, `{1}` placeholders.
- The project owner discusses the project in French; issues and pull requests may be
  written in English or French.

## Technical rules

- WPF on .NET Framework 4.8, x86 only (the eyetracker libraries are 32-bit).
- No new dependency without discussing it first in an issue.
- One roadmap step per branch and per pull request.
- The personal dictionary (`%APPDATA%\GazeBar\personal-dictionary.json`) and any personal
  data stay out of the repository.
- Typed text is never written to the log.

## Testing

GazeBar is tested by its user on a Tobii PCEye 5. Please say clearly in your pull request
what was compiled and what was tested on real hardware.
