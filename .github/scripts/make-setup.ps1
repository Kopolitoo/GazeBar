# Makes the installer GazeBar-<version>-setup.exe with Inno Setup (Install/GazeBar.iss), from
# the "full installation" build (dotnet build -p:UiAccess=true).
#
#   pwsh .github/scripts/make-setup.ps1 <UI access build folder> <version> <destination folder>
#
# Inno Setup is installed on the GitHub machine if it is not there yet (with Chocolatey, a fixed
# version). It is only needed to MAKE the installer: nothing to install on the user's computer.
# Used by the automatic build (build.yml).

param(
    [Parameter(Mandatory = $true)][string]$ProgramFolder,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Destination
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')

# The program must ask for UI access, otherwise the installation would be useless.
& (Join-Path $PSScriptRoot 'check-exe-manifest.ps1') (Join-Path $ProgramFolder 'GazeBar.exe') true
if ($LASTEXITCODE -ne 0) { exit 1 }

$iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) {
    Write-Host 'Installing Inno Setup...'
    choco install innosetup --version=6.4.3 --yes --no-progress
    if ($LASTEXITCODE -ne 0) { Write-Host '::error::Inno Setup could not be installed.'; exit 1 }
}
if (-not (Test-Path $iscc)) { Write-Host "::error::ISCC.exe not found: $iscc"; exit 1 }

New-Item -ItemType Directory -Force $Destination | Out-Null
$programPath = (Resolve-Path $ProgramFolder).Path.TrimEnd('\')
$destinationPath = (Resolve-Path $Destination).Path.TrimEnd('\')

& $iscc "/DAppVersion=$Version" "/DProgramFolder=$programPath" "/DOutputFolder=$destinationPath" (Join-Path $root 'Install\GazeBar.iss')
if ($LASTEXITCODE -ne 0) { Write-Host '::error::Inno Setup could not make the installer.'; exit 1 }

$setup = Join-Path $destinationPath "GazeBar-$Version-setup.exe"
if (-not (Test-Path $setup)) { Write-Host "::error::Installer not found: $setup"; exit 1 }
Write-Host "OK: installer ready: $setup"
