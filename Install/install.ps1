# Full installation of GazeBar, with UI access (see Platform/UiAccess.cs).
#
# Started as administrator by Install.cmd. Works the same for a first installation and for an
# update. Steps:
#  1. close GazeBar if it is running;
#  2. copy the GazeBar folder of the zip to C:\Program Files\GazeBar;
#  3. create a certificate ON THIS COMPUTER, make the computer trust it, sign GazeBar.exe
#     with it, then DELETE its private key: nobody (not even a virus) can sign anything else
#     with it later. A new certificate is made at each installation; the old one is removed;
#  4. add GazeBar to the Start menu;
#  5. start GazeBar (as a normal user). If the option "Start with Windows" is on, GazeBar
#     itself updates its path at startup (see Platform/WindowsStartup.cs).
# The settings (%APPDATA%\GazeBar) are not touched.
#
# Saved in UTF-8 WITH a BOM (see common.ps1).

. (Join-Path $PSScriptRoot 'common.ps1')

$source = Join-Path $PSScriptRoot 'GazeBar'
$success = $false

try {
    Assert-Administrator
    if (-not (Test-Path (Join-Path $source 'GazeBar.exe'))) {
        throw ($(if ($IsFrench) { "GazeBar.exe introuvable dans $source. Extraire d'abord tout le zip." }
                 else { "GazeBar.exe not found in $source. Extract the whole zip first." }))
    }

    Say 'Installing GazeBar (full installation)...' 'Installation de GazeBar (installation complète)...'

    # 1. Close GazeBar.
    Stop-GazeBar

    # 2. Copy the program. The old files are removed first, except the "Eyetracker" folder
    #    (eyetracker DLLs copied there by the user, see the user guide).
    Say "Copying to $InstallFolder..." "Copie dans $InstallFolder..."
    if (Test-Path $InstallFolder) {
        Get-ChildItem $InstallFolder | Where-Object { $_.Name -ne 'Eyetracker' } | Remove-Item -Recurse -Force
    } else {
        New-Item -ItemType Directory -Path $InstallFolder | Out-Null
    }
    Copy-Item -Path (Join-Path $source '*') -Destination $InstallFolder -Recurse -Force
    # Files downloaded from Internet carry a "comes from Internet" mark: removed.
    Get-ChildItem $InstallFolder -Recurse -File | Unblock-File

    # 3. Certificate and signature.
    Say 'Signing GazeBar.exe with a certificate made on this computer...' 'Signature de GazeBar.exe avec un certificat créé sur cet ordinateur...'
    $exe = Join-Path $InstallFolder 'GazeBar.exe'
    Set-GazeBarSignature $exe

    # 4. Start menu shortcut.
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $InstallFolder
    $shortcut.Save()

    Say 'GazeBar is installed. Starting it...' 'GazeBar est installé. Démarrage...'

    # 5. Start GazeBar.
    Start-GazeBarAsUser
    $success = $true
}
catch {
    Say 'INSTALLATION FAILED:' "ÉCHEC DE L'INSTALLATION :"
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Finish $success
