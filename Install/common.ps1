# Shared by install.ps1 and uninstall.ps1 (read with ". common.ps1").
#
# Runs in Windows PowerShell 5.1, the version shipped with Windows 10 and 11.
# This file is saved in UTF-8 WITH a BOM: without it, Windows PowerShell 5.1 would read the
# accented French letters wrongly.

$ErrorActionPreference = 'Stop'

# Where GazeBar is installed. "Program Files" is a protected folder: Windows gives UI access
# only to a program installed there (see Platform/UiAccess.cs).
$InstallFolder = Join-Path $env:ProgramFiles 'GazeBar'

# The Start menu shortcut, for all the users of the computer.
$ShortcutPath = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\GazeBar.lnk'

# The name of the certificate created on this computer to sign GazeBar.exe. Only the
# certificates with exactly this name are ever removed.
$CertificateSubject = 'CN=GazeBar local signature'

# French messages if Windows is in French, English otherwise.
$IsFrench = (Get-UICulture).TwoLetterISOLanguageName -eq 'fr'

function Say([string]$english, [string]$french) {
    if ($IsFrench) { Write-Host $french } else { Write-Host $english }
}

# Stops the program if it is administrator; otherwise says so and stops the script.
function Assert-Administrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw ($(if ($IsFrench) { 'Ce script doit être lancé en administrateur (avec Install.cmd ou Uninstall.cmd).' }
                 else { 'This script must run as administrator (start it with Install.cmd or Uninstall.cmd).' }))
    }
}

# Closes every running GazeBar (the files of a running program cannot be replaced).
function Stop-GazeBar {
    $running = Get-Process -Name 'GazeBar' -ErrorAction SilentlyContinue
    if ($running) {
        Say 'Closing GazeBar...' 'Fermeture de GazeBar...'
        $running | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
}

# Removes the GazeBar certificates from every store of the computer where this script puts one.
function Remove-GazeBarCertificates {
    foreach ($store in 'Root', 'TrustedPublisher', 'My') {
        Get-ChildItem "Cert:\LocalMachine\$store" |
            Where-Object { $_.Subject -eq $CertificateSubject } |
            ForEach-Object {
                # -DeleteKey: also deletes the private key, if one is left (store "My").
                if ($_.HasPrivateKey) { Remove-Item $_.PSPath -DeleteKey } else { Remove-Item $_.PSPath }
            }
    }
}

# Signs GazeBar.exe (UI access needs a signature that the computer trusts):
#  - removes the old GazeBar certificates (a new one is made at each installation);
#  - creates a certificate ON THIS COMPUTER and makes the computer trust it;
#  - signs the program with it;
#  - DELETES its private key: nobody (not even a virus) can sign anything else with it later.
# Used by install.ps1 and by the Setup.exe installer (setup-certificate.ps1).
function Set-GazeBarSignature([string]$exe) {
    Remove-GazeBarCertificates
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $CertificateSubject `
        -CertStoreLocation 'Cert:\LocalMachine\My' -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(10)

    # The computer trusts this certificate: "Trusted Root" (it is its own authority) and
    # "Trusted Publishers". Only the public part goes there (a .cer file, without the key).
    $publicFile = Join-Path $env:TEMP 'GazeBar-signature.cer'
    Export-Certificate -Cert $certificate -FilePath $publicFile | Out-Null
    Import-Certificate -FilePath $publicFile -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Import-Certificate -FilePath $publicFile -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
    Remove-Item $publicFile -Force

    $signature = Set-AuthenticodeSignature -FilePath $exe -Certificate $certificate -HashAlgorithm SHA256

    # The private key is deleted: the certificate can no longer sign anything.
    Remove-Item $certificate.PSPath -DeleteKey

    if ($signature.Status -ne 'Valid') {
        throw "Signature: $($signature.Status) $($signature.StatusMessage)"
    }
}

# "Start with Windows" (Platform/WindowsStartup.cs): removes its entry if it starts the copy of
# GazeBar installed in $InstallFolder (called when that copy is removed).
function Remove-GazeBarStartup {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $command = (Get-ItemProperty -Path $runKey -Name 'GazeBar' -ErrorAction SilentlyContinue).GazeBar
    if ($command -and $command -like "*$InstallFolder*") {
        Remove-ItemProperty -Path $runKey -Name 'GazeBar'
    }
}

# Starts GazeBar as a NORMAL user, not as administrator: explorer.exe opens it like a
# double-click would. (Started directly from this administrator window, GazeBar would also be
# administrator.)
function Start-GazeBarAsUser {
    Start-Process -FilePath "$env:WINDIR\explorer.exe" -ArgumentList "`"$(Join-Path $InstallFolder 'GazeBar.exe')`""
}

# Keeps the window open: 10 seconds after a success, until Enter after an error.
function Finish([bool]$success) {
    if ($success) {
        Start-Sleep -Seconds 10
    } else {
        Say 'Press Enter to close this window.' 'Appuyer sur Entrée pour fermer cette fenêtre.'
        Read-Host | Out-Null
    }
}
