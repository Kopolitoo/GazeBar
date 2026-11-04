# Contributing to GazeBar

*Version française : [CONTRIBUTING.fr.md](CONTRIBUTING.fr.md).*

Thank you for your interest in GazeBar, a Windows tool that controls the mouse with eye gaze.
Start with the [architecture](docs/architecture.md) to find your way in the code.

## Language rules

- **Code, names and comments are written in English**: classes, methods, fields, files,
  folders, log messages and code comments.
- **Comments describe the code as it is now**, in the present tense. No "step 12:", no dates,
  no "used to": the history is in Git and in the pull requests.
- Texts shown to the user are NOT written in the code: they live in the language files of the
  `Languages` folder (`Languages/en.json`, `Languages/fr.json`…) and are read with `Texts.Get("key")` or
  `Texts.Format("key", values)` (see `Texts.cs`). Log messages stay in the code, in English.
- To add a language: copy `Languages/en.json` (the reference language) to `Languages/xx.json` (ISO 639-1 code), translate
  the values (never the keys) and keep the `{0}`, `{1}` placeholders.
- The project owner discusses the project in French; issues and pull requests may be
  written in English or French.

## Issues and pull requests

- To report a problem or suggest an idea, open an issue: GitHub offers two forms,
  "Problem" and "Idea" (`.github/ISSUE_TEMPLATE/`).
- A new pull request starts with a short checklist (`.github/pull_request_template.md`).
  Write `Closes #12` in it to close issue 12 automatically when the pull request is merged.

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

- Every explanatory file exists in English (`name.md`) and in French (`name.fr.md`), with a
  link at the top to the other version. Keep both in step.
- `README.md` stays short: what GazeBar is, how to install it, links.
- Details go in `docs/`: `user-guide.md` (for users), `architecture.md` (for developers),
  `manual-tests.md`. `docs/GUIDE-GIT.md` is in French only.
- A pull request that changes what the user sees updates the user guide; one that adds a class
  or a setting updates the architecture; a new feature adds its manual test.
- A new file taken from someone else (word list, image, code) is listed in
  `THIRD-PARTY-NOTICES.md` (and `THIRD-PARTY-NOTICES.fr.md`) with its licence.

## Testing

GazeBar is tested by its user on a Tobii PCEye 5. Please say clearly in your pull request
what was compiled and what was tested on real hardware, using the
[manual tests](docs/manual-tests.md). Every pull request is compiled automatically on GitHub
(see `.github/workflows/build.yml`).

Without an eyetracker, start GazeBar with the `--mouse` option (in Visual Studio, choose the
launch profile "GazeBar (mouse as gaze)"): the mouse then plays the gaze. Keep the mouse still
to fixate, move it below the bottom of the screen to open the menu. This checks the modes and
the screens, not the feel of a real gaze: say in the pull request if you only tested this way.
See [architecture](docs/architecture.md#mouse-as-gaze-developer-mode).
