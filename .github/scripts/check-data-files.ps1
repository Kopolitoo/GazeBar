# Checks the data files read by GazeBar at startup.
#
# - Every Languages/*.json and Keyboards/*.json file must be valid JSON.
# - Every language file must have exactly the same keys as Languages/en.json
#   (the reference language), and the same {0}, {1}... placeholders in each text.
#
# Used by the automatic build (.github/workflows/build.yml). Can also be run by hand
# from the repository folder: pwsh .github/scripts/check-data-files.ps1

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$errors = 0

function Report([string]$file, [string]$message) {
    # "::error file=...::" makes GitHub show the message on the pull request.
    Write-Host "::error file=$file::$message"
    $script:errors++
}

function Read-Json([string]$path) {
    $relative = [IO.Path]::GetRelativePath($root, $path).Replace('\', '/')
    try {
        return Get-Content -Raw -Encoding utf8 $path | ConvertFrom-Json -AsHashtable
    } catch {
        Report $relative "Invalid JSON: $($_.Exception.Message)"
        return $null
    }
}

function Get-Placeholders([string]$text) {
    return ([regex]::Matches($text, '\{\d+') | ForEach-Object { $_.Value } | Sort-Object -Unique) -join ','
}

# 1. Keyboard layouts: valid JSON.
foreach ($file in Get-ChildItem (Join-Path $root 'Keyboards') -Filter '*.json') {
    if ($null -ne (Read-Json $file.FullName)) { Write-Host "OK  Keyboards/$($file.Name)" }
}

# 2. Language files: valid JSON, same keys and placeholders as en.json.
$reference = Read-Json (Join-Path $root 'Languages/en.json')
if ($null -eq $reference) { exit 1 }

foreach ($file in Get-ChildItem (Join-Path $root 'Languages') -Filter '*.json') {
    $name = "Languages/$($file.Name)"
    $texts = Read-Json $file.FullName
    if ($null -eq $texts) { continue }
    $before = $errors

    foreach ($key in $reference.Keys) {
        if (-not $texts.ContainsKey($key)) {
            Report $name "Missing key: $key"
        } elseif ((Get-Placeholders $texts[$key]) -ne (Get-Placeholders $reference[$key])) {
            Report $name "Placeholders differ from en.json for key: $key"
        }
    }
    foreach ($key in $texts.Keys) {
        if (-not $reference.ContainsKey($key)) { Report $name "Key not in en.json: $key" }
    }

    if ($errors -eq $before) { Write-Host "OK  $name ($($texts.Count) keys)" }
}

if ($errors -gt 0) {
    Write-Host "$errors problem(s) found."
    exit 1
}
Write-Host 'All data files are OK.'
