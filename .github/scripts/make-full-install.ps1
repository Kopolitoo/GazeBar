# Makes the folder of the "full installation" (with UI access, see Platform/UiAccess.cs):
#
#   <Destination>/
#     Install.cmd, Uninstall.cmd, install.ps1, uninstall.ps1, common.ps1   (Install folder)
#     GazeBar/   the program built with UiAccess=true, the licences and the READMEs
#
#   pwsh .github/scripts/make-full-install.ps1 <program folder> <destination folder>
#
# Used by the automatic build (build.yml) and by the release (release.yml).

param(
    [Parameter(Mandatory = $true)][string]$ProgramFolder,
    [Parameter(Mandatory = $true)][string]$Destination
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')

$program = Join-Path $Destination 'GazeBar'
New-Item -ItemType Directory -Force $program | Out-Null

# The program and its data files (languages, keyboards, public dictionaries), without the
# debug files (.pdb).
Copy-Item (Join-Path $ProgramFolder '*') $program -Recurse -Exclude '*.pdb'
foreach ($file in 'LICENSE.txt', 'THIRD-PARTY-NOTICES.md', 'THIRD-PARTY-NOTICES.fr.md', 'README.md', 'README.fr.md') {
    Copy-Item (Join-Path $root $file) $program
}

# The installation scripts, next to the GazeBar folder.
Copy-Item (Join-Path $root 'Install\*') $Destination

# The program must ask for UI access, otherwise the installation would be useless.
& (Join-Path $PSScriptRoot 'check-exe-manifest.ps1') (Join-Path $program 'GazeBar.exe') true
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "OK: full installation ready in $Destination."
