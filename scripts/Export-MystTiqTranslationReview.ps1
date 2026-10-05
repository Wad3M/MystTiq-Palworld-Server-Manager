# MystTiq v1.0.0.5: file reviewed for this release (2026-10-05).
#requires -Version 7.0
# v0.9.3.0: writes one review sheet per language (CSV, opens in any spreadsheet): every text the Desktop can show, the
# English beside the translation, what kind of text it is and where it is used, and empty columns for the reviewer.
# A reviewer's corrected Translation column is the input for the next release; see docs/i18n/TRANSLATION_REVIEW.md.
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    # Language codes as in Assets/i18n (ja, de, pt-BR...). Default: every language but English.
    [string[]]$Language,
    [string]$OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'MystTiq-translation-review')
)
$ErrorActionPreference = 'Stop'
$i18n = Join-Path $ProjectRoot 'src\MystTiq.Desktop\Assets\i18n'
$english = Get-Content (Join-Path $i18n 'en.json') -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
# pwsh -File passes "ja,de" as one argument.
$Language = @($Language | ForEach-Object { $_ -split ',' } | ForEach-Object Trim | Where-Object { $_ })
if (-not $Language) { $Language = @(Get-ChildItem $i18n -Filter *.json | Where-Object BaseName -ne 'en' | ForEach-Object BaseName) }

# Where each keyed text is used, from the inventory Update-MystTiqUiText.ps1 -Inventory writes.
$where = @{}
$inventory = Join-Path $ProjectRoot 'docs\i18n\UI_TEXT_INVENTORY.csv'
if (Test-Path $inventory) {
    foreach ($row in Import-Csv $inventory -Encoding utf8) {
        if ($row.Key -and -not $where.ContainsKey($row.Key)) { $where[$row.Key] = $row.Where }
    }
}

function Get-Kind([string]$Key) {
    switch -Regex ($Key) {
        '^msg\.' { 'Message (status, result or error; {0} marks a value)' }
        '^(nav|category|page|ribbon)\.' { 'Navigation, page title or Ribbon' }
        '^ui\.' { 'Window text (label, button, tip)' }
        default { 'Other' }
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
foreach ($code in $Language) {
    $path = Join-Path $i18n "$code.json"
    if (-not (Test-Path $path)) { throw "No language file for '$code' ($path)." }
    $translated = Get-Content $path -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    $rows = foreach ($key in ($english.Keys | Sort-Object)) {
        if ($key -like 'language.*') { continue }
        [pscustomobject]@{
            Key         = $key
            Kind        = Get-Kind $key
            English     = $english[$key]
            Translation = $translated[$key]
            Where       = $where[$key]
            Correction  = ''
            Notes       = ''
        }
    }
    $sheet = Join-Path $OutputDirectory "MystTiq-review-$code.csv"
    $rows | Export-Csv -Path $sheet -NoTypeInformation -Encoding utf8BOM
    Write-Host ("{0,-8} {1,5} texts -> {2}" -f $code, @($rows).Count, $sheet)
}
