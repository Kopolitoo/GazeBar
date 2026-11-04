# Checks the manifest built into a compiled GazeBar.exe: does it ask for UI access?
#
#   pwsh .github/scripts/check-exe-manifest.ps1 <path of GazeBar.exe> <true|false>
#
# The manifest is stored as plain text inside the .exe: it is enough to look for
# uiAccess="true" or uiAccess="false" in the bytes of the file.
# Used by the automatic build (.github/workflows/build.yml).

param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][ValidateSet('true', 'false')][string]$Expected
)

$ErrorActionPreference = 'Stop'
$text = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($ExePath))
$wanted = "uiAccess=""$Expected"""
$other = if ($Expected -eq 'true') { 'uiAccess="false"' } else { 'uiAccess="true"' }

if (-not $text.Contains($wanted) -or $text.Contains($other)) {
    Write-Host "::error::$ExePath should contain $wanted in its manifest, and not $other."
    exit 1
}
Write-Host "OK: $ExePath asks for $wanted."
