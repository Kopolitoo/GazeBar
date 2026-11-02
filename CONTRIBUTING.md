# Contributing to GazeBar

Thank you for your interest in GazeBar, a Windows tool that controls the mouse with eye gaze.

## Language rules

- **Code, names and comments are written in English**: classes, methods, fields, files,
  folders, log messages and code comments.
- The user interface is still in French for now. Translating the interface (language files
  and a language setting) is planned as a separate step.
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
