# Checks the two manifests of GazeBar (see app.manifest and app.uiaccess.manifest).
#
# app.uiaccess.manifest (the "full installation", with UI access) must be the same as
# app.manifest except for uiAccess="true". The comments are ignored.
#
# Used by the automatic build (.github/workflows/build.yml). Can also be run by hand
# from the repository folder: pwsh .github/scripts/check-manifests.ps1

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')

# Reads a manifest without its comments and without spaces, so that only the XML is compared.
function Read-Manifest([string]$name) {
    $text = Get-Content -Raw -Encoding utf8 (Join-Path $root $name)
    $text = [regex]::Replace($text, '<!--.*?-->', '', 'Singleline')
    return [regex]::Replace($text, '\s+', '')
}

$normal = Read-Manifest 'app.manifest'
$full = Read-Manifest 'app.uiaccess.manifest'

$errors = 0
if (-not $normal.Contains('uiAccess="false"')) {
    Write-Host '::error file=app.manifest::app.manifest must contain uiAccess="false".'
    $errors++
}
if (-not $full.Contains('uiAccess="true"')) {
    Write-Host '::error file=app.uiaccess.manifest::app.uiaccess.manifest must contain uiAccess="true".'
    $errors++
}
if ($normal.Replace('uiAccess="false"', 'uiAccess="true"') -ne $full) {
    Write-Host '::error file=app.uiaccess.manifest::app.uiaccess.manifest must be the same as app.manifest, except uiAccess="true".'
    $errors++
}

if ($errors -gt 0) { exit 1 }
Write-Host 'OK: the two manifests match.'
