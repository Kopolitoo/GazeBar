# Notes for Claude

- Code, names (classes, methods, files, folders) and comments are in **English**.
  Conversations and explanations with the project owner are in **French**.
- UI texts live in `Languages/*.json` and are read with `Texts.Get` / `Texts.Format`: never
  write a user-facing text directly in the code. French (`fr.json`) is the reference file.
- WPF on .NET Framework 4.8, x86. No new dependency without asking.
- Pull the latest `main` before coding. One roadmap step per branch.
- Code cannot be compiled or tested on Windows from the cloud: always say what was not
  compiled or not tested, and never promise untested behaviour.
- The personal dictionary stays out of the public repository. Typed text is never logged.
- See CONTRIBUTING.md for the general rules.
