# Removes the full installation of GazeBar: closes GazeBar, deletes C:\Program Files\GazeBar,
# the Start menu shortcut and the certificate made by install.ps1.
# The settings (%APPDATA%\GazeBar) and the log (%LOCALAPPDATA%\GazeBar) are kept.
#
# Started as administrator by Uninstall.cmd. Saved in UTF-8 WITH a BOM (see common.ps1).

. (Join-Path $PSScriptRoot 'common.ps1')

$success = $false

try {
    Assert-Administrator
    Say 'Removing GazeBar (full installation)...' 'Désinstallation de GazeBar (installation complète)...'

    Stop-GazeBar
    if (Test-Path $InstallFolder) { Remove-Item $InstallFolder -Recurse -Force }
    if (Test-Path $ShortcutPath) { Remove-Item $ShortcutPath -Force }
    Remove-GazeBarCertificates

    # "Start with Windows" (Platform/WindowsStartup.cs): its entry is removed if it starts the
    # copy that was just deleted.
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $command = (Get-ItemProperty -Path $runKey -Name 'GazeBar' -ErrorAction SilentlyContinue).GazeBar
    if ($command -and $command -like "*$InstallFolder*") {
        Remove-ItemProperty -Path $runKey -Name 'GazeBar'
    }

    Say 'GazeBar is removed. Your settings are kept.' 'GazeBar est désinstallé. Vos réglages sont gardés.'
    $success = $true
}
catch {
    Say 'UNINSTALLATION FAILED:' 'ÉCHEC DE LA DÉSINSTALLATION :'
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Finish $success
