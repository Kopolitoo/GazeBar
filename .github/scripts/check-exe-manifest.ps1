# Checks the manifest built into a compiled Prunel.exe: does it ask for UI access?
#
#   pwsh .github/scripts/check-exe-manifest.ps1 <path of Prunel.exe> <true|false>
#
# The manifest is stored as plain text inside the .exe, comments included: the script reads
# the uiAccess value of the requestedExecutionLevel tag (not the one quoted in a comment).
# Used by the automatic build (.github/workflows/build.yml).

param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][ValidateSet('true', 'false')][string]$Expected
)

$ErrorActionPreference = 'Stop'
$text = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($ExePath))
$found = [regex]::Matches($text, '<requestedExecutionLevel[^>]*uiAccess="(true|false)"')

if ($found.Count -ne 1 -or $found[0].Groups[1].Value -ne $Expected) {
    $values = ($found | ForEach-Object { $_.Groups[1].Value }) -join ', '
    Write-Host "::error::$ExePath should ask for uiAccess=""$Expected"" in its manifest (found: $values)."
    exit 1
}
Write-Host "OK: $ExePath asks for uiAccess=""$Expected""."
exit 0
