# Notes for Claude

- Code, names (classes, methods, files, folders) and comments are in **English**.
  Conversations and explanations with the project owner are in **French**.
- UI texts live in `Languages/*.json` and are read with `Texts.Get` / `Texts.Format`: never
  write a user-facing text directly in the code. English (`en.json`) is the reference file and the
  fallback; keep `fr.json` and `en.json` in step (same keys).
- WPF on .NET Framework 4.8, x86. No new dependency without asking.
- Pull the latest `main` before coding. One roadmap step per branch.
- Code cannot be compiled or tested on Windows from the cloud: always say what was not
  compiled or not tested, and never promise untested behaviour.
- The personal dictionary stays out of the public repository. Typed text is never logged. Gaze data is never saved
  (no positions file, no gaze coordinates in the log): eyetracker licence.
- Docs: short README.md + README.fr.md; details in docs/ (user guide fr + en, architecture in
  English, manual tests fr + en). Update them with the code (see CONTRIBUTING.md, Documentation).
- See CONTRIBUTING.md for the general rules.
