# Contributing to GazeBar

Thank you for your interest in GazeBar, a Windows tool that controls the mouse with eye gaze.
Start with the [architecture](docs/architecture.md) to find your way in the code.

## Language rules

- **Code, names and comments are written in English**: classes, methods, fields, files,
  folders, log messages and code comments.
- Texts shown to the user are NOT written in the code: they live in the language files of the
  `Languages` folder (`Languages/en.json`, `Languages/fr.json`…) and are read with `Texts.Get("key")` or
  `Texts.Format("key", values)` (see `Texts.cs`). Log messages stay in the code, in English.
- To add a language: copy `Languages/en.json` (the reference language) to `Languages/xx.json` (ISO 639-1 code), translate
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
- Gaze data is never saved (eyetracker licence): no positions file, no gaze coordinates in the
  log. Showing them live on screen is fine.

## Documentation

- `README.md` (English) and `README.fr.md` (French) stay short: what GazeBar is, how to install
  it, links. Keep both in step.
- Details go in `docs/`: `user-guide.md` and `user-guide.fr.md` (for users, keep both in step),
  `architecture.md` (for developers, English only), `manual-tests.md` and `manual-tests.fr.md`.
- A pull request that changes what the user sees updates the user guide; one that adds a class
  or a setting updates the architecture; a new feature adds its manual test.
- A new file taken from someone else (word list, image, code) is listed in
  `THIRD-PARTY-NOTICES.md` with its licence.

## Testing

GazeBar is tested by its user on a Tobii PCEye 5. Please say clearly in your pull request
what was compiled and what was tested on real hardware, using the
[manual tests](docs/manual-tests.md). Every pull request is compiled automatically on GitHub
(see `.github/workflows/build.yml`).
