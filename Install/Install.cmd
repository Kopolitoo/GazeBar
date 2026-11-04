@echo off
rem Installs GazeBar with "UI access" (circle above the Start menu, clicks in
rem administrator windows). Double-click this file: Windows asks for permission
rem (administrator), then install.ps1 does the work. See docs/user-guide.md.

if not exist "%~dp0GazeBar\GazeBar.exe" (
    echo Extract the whole zip first, then start Install.cmd from the extracted folder.
    echo Extraire d'abord tout le zip, puis lancer Install.cmd depuis le dossier extrait.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0install.ps1\"'"
