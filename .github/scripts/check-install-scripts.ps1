# Checks that the installation scripts (Install/*.ps1) have no syntax error.
# Nothing is run: the scripts are only read by the PowerShell parser.
#
# Used by the automatic build (build.yml), with Windows PowerShell 5.1 (powershell.exe), the
# version that runs them on the user's computer.

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$errors = 0

foreach ($file in Get-ChildItem (Join-Path $root 'Install') -Filter '*.ps1') {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
    foreach ($e in $parseErrors) {
        Write-Host "::error file=Install/$($file.Name),line=$($e.Extent.StartLineNumber)::$($e.Message)"
        $errors++
    }
    # Without a BOM, Windows PowerShell 5.1 reads the accented letters wrongly.
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -lt 3 -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF) {
        Write-Host "::error file=Install/$($file.Name)::Must be saved in UTF-8 with a BOM."
        $errors++
    }
}

if ($errors -gt 0) { exit 1 }
Write-Host 'OK: the installation scripts can be read.'
