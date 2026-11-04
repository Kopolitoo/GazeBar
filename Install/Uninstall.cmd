@echo off
rem Removes the full installation of GazeBar (program folder, shortcut,
rem certificate). The settings in %APPDATA%\GazeBar are kept.
rem Double-click this file: Windows asks for permission (administrator).

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0uninstall.ps1\"'"
