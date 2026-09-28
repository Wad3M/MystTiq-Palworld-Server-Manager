# MystTiq v0.9.0.0: file reviewed for this release (2026-09-28).
#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    # Replace the hard-coded texts in the Desktop's XAML with {services:Tr key} and add the keys to en.json.
    [switch]$ConvertXaml,
    # Write the inventory: docs/i18n/UI_TEXT_INVENTORY.md and .csv (every user-visible English text and its state).
    [switch]$Inventory,
    # List the hard-coded XAML texts left and exit 1 if there are any. The release gate uses this.
    [switch]$Check
)

# v0.9.0.0: the list of English texts the Desktop shows, and the tool that makes them translatable.
#
# Where the texts are:
#   - XAML (MainWindow.axaml and the dialogs in Views): the display attributes Text, Content, Header, Watermark,
#     PlaceholderText, ToolTip.Tip, Title and the accessible names. A literal there is English for everyone; -ConvertXaml
#     gives each distinct text a key ("ui.<words>", the same English text sharing one key), writes it to
#     Assets/i18n/en.json and puts {services:Tr ui.<words>} in its place. Texts without a letter (arrows, dashes) stay.
#   - The code (view models, models, services): status and error messages. They are listed here, with their file and
#     line, but converting them is a separate step (they are assigned in code, often with values filled in).
#   - Assets/i18n/en.json: the texts already translatable (navigation, headers, Ribbon, Dashboard).
# Every language file must have every key English has (the release gate checks), so a new key needs its translations.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path $ProjectRoot).Path
$desktop = Join-Path $root 'src\MystTiq.Desktop'
$enPath = Join-Path $desktop 'Assets\i18n\en.json'
$xamlFiles = @((Join-Path $desktop 'MainWindow.axaml'), (Join-Path $desktop 'App.axaml')) + @(Get-ChildItem (Join-Path $desktop 'Views') -Filter *.axaml | ForEach-Object FullName)
$attrPattern = [regex]'(?<![\w.])(?<attr>Text|Content|Header|Watermark|PlaceholderText|ToolTip\.Tip|ToolTipText|Title|AutomationProperties\.Name|AutomationProperties\.HelpText)="(?<val>[^"{][^"]*)"'

function ConvertFrom-XmlText([string]$Text) {
    [regex]::Replace($Text, '&(#x[0-9A-Fa-f]+|#\d+|amp|lt|gt|quot|apos);', {
        param($m)
        switch -Regex ($m.Groups[1].Value) {
            '^#x' { [char]::ConvertFromUtf32([Convert]::ToInt32($m.Groups[1].Value.Substring(2), 16)) }
            '^#' { [char]::ConvertFromUtf32([int]$m.Groups[1].Value.Substring(1)) }
            'amp' { '&' } 'lt' { '<' } 'gt' { '>' } 'quot' { '"' } 'apos' { "'" }
        }
    })
}

# The parts of a XAML file outside comments (only those are converted or counted).
function Get-OutsideComments([string]$Text) {
    $parts = [System.Collections.Generic.List[object]]::new(); $at = 0
    foreach ($c in [regex]::Matches($Text, '<!--[\s\S]*?-->')) {
        if ($c.Index -gt $at) { $parts.Add([pscustomobject]@{ Start = $at; Text = $Text.Substring($at, $c.Index - $at); Code = $true }) }
        $parts.Add([pscustomobject]@{ Start = $c.Index; Text = $c.Value; Code = $false }); $at = $c.Index + $c.Length
    }
    if ($at -lt $Text.Length) { $parts.Add([pscustomobject]@{ Start = $at; Text = $Text.Substring($at); Code = $true }) }
    $parts
}

function Test-Translatable([string]$Phrase) { $Phrase -match '\p{L}' }

function Get-LineNumber([string]$Text, [int]$Index) { ([regex]::Matches($Text.Substring(0, $Index), "`n")).Count + 1 }

# English text -> key. The same text always gets the same key; the key reads like the text.
$english = [ordered]@{}
if (Test-Path $enPath) { foreach ($p in (Get-Content $enPath -Raw | ConvertFrom-Json -AsHashtable).GetEnumerator()) { $english[$p.Key] = $p.Value } }
$byText = @{}
foreach ($k in $english.Keys) { if ($k -like 'ui.*' -and -not $byText.ContainsKey($english[$k])) { $byText[$english[$k]] = $k } }
function Get-UiKey([string]$Phrase) {
    if ($byText.ContainsKey($Phrase)) { return $byText[$Phrase] }
    $slug = ($Phrase.ToLowerInvariant() -replace '&', ' and ' -replace '[^a-z0-9]+', '_').Trim('_')
    if (-not $slug) { $slug = 'text' }
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Phrase))).Substring(0, 6).ToLowerInvariant()
    $key = if ($slug.Length -gt 48) { "ui.$($slug.Substring(0, 48).TrimEnd('_'))_$hash" } else { "ui.$slug" }
    if ($english.Contains($key) -and $english[$key] -ne $Phrase) { $key = "${key}_$hash" }
    $byText[$Phrase] = $key
    $english[$key] = $Phrase
    return $key
}

# ---------------------------------------------------------------------------------------------------------------------
$rows = [System.Collections.Generic.List[object]]::new()
$converted = 0
foreach ($file in $xamlFiles) {
    $text = [IO.File]::ReadAllText($file)
    $relative = $file.Substring($root.Length + 1).Replace('\', '/')
    $out = [Text.StringBuilder]::new()
    foreach ($part in Get-OutsideComments $text) {
        if (-not $part.Code) { [void]$out.Append($part.Text); continue }
        $new = $attrPattern.Replace($part.Text, {
            param($m)
            $value = ConvertFrom-XmlText $m.Groups['val'].Value
            $line = Get-LineNumber $text ($part.Start + $m.Index)
            if (-not (Test-Translatable $value)) { return $m.Value }
            if ($ConvertXaml) {
                $key = Get-UiKey $value
                $script:converted++
                $rows.Add([pscustomobject]@{ Kind = 'XAML'; State = 'Keyed'; Key = $key; English = $value; Where = "${relative}:$line ($($m.Groups['attr'].Value))" })
                return "$($m.Groups['attr'].Value)=`"{services:Tr $key}`""
            }
            $rows.Add([pscustomobject]@{ Kind = 'XAML'; State = 'Hard-coded'; Key = ''; English = $value; Where = "${relative}:$line ($($m.Groups['attr'].Value))" })
            return $m.Value
        })
        [void]$out.Append($new)
    }
    if ($ConvertXaml -and $out.ToString() -ne $text) {
        $result = $out.ToString()
        if ($result -notmatch 'xmlns:services=') {
            # The dialogs need the services namespace for {services:Tr}; add it beside the root element's xmlns.
            $result = [regex]::Replace($result, '(<(?:Window|UserControl|Application)\b[^>]*?xmlns="https://github.com/avaloniaui")', '$1' + "`n        xmlns:services=`"using:MystTiq.Desktop.Services`"", 1)
        }
        [IO.File]::WriteAllText($file, $result, [Text.UTF8Encoding]::new($false))
    }
}

# The keyed XAML texts (already converted), for the inventory.
foreach ($file in $xamlFiles) {
    $text = [IO.File]::ReadAllText($file)
    $relative = $file.Substring($root.Length + 1).Replace('\', '/')
    foreach ($m in [regex]::Matches($text, '\{services:Tr(?:Format)? (?<key>[\w.]+)')) {
        $key = $m.Groups['key'].Value
        if ($ConvertXaml -and @($rows | Where-Object { $_.Key -eq $key -and $_.Where -like "${relative}:*" }).Count) { continue }
        $rows.Add([pscustomobject]@{ Kind = 'XAML'; State = 'Keyed'; Key = $key; English = $(if ($english.Contains($key)) { $english[$key] } else { '(missing in en.json)' }); Where = "${relative}:$(Get-LineNumber $text $m.Index)" })
    }
}

if ($ConvertXaml) {
    $json = [ordered]@{}; foreach ($k in $english.Keys) { $json[$k] = $english[$k] }
    $serialized = ($json | ConvertTo-Json -Depth 3) -replace '(?m)^  ', '  '
    [IO.File]::WriteAllText($enPath, $serialized + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "Converted $converted XAML text(s); en.json now has $($english.Count) keys."
}

if ($Inventory) {
    # Code texts: string literals in the Desktop's code that read as prose shown to the user (a capital letter, a space
    # or a full stop), with interpolated values shown as {0}, {1}. Log categories, routes and JSON names are not prose.
    foreach ($cs in Get-ChildItem $desktop -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) {
        $text = [IO.File]::ReadAllText($cs.FullName)
        $relative = $cs.FullName.Substring($root.Length + 1).Replace('\', '/')
        foreach ($m in [regex]::Matches($text, '(?<interp>\$)?"(?<val>(?:[^"\\\r\n]|\\.)*)"')) {
            $value = $m.Groups['val'].Value
            $lineText = ($text.Substring(0, $m.Index) -split "`n")[-1]
            if ($lineText -match '^\s*//' -or $lineText -match '\[(DllImport|JsonPropertyName|LibraryImport)') { continue }
            if ($m.Groups['interp'].Success) { $n = -1; $value = [regex]::Replace($value, '\{[^{}]+\}', { $script:n++; "{$script:n}" }) }
            if ($value -notmatch '^[A-Z\p{Lu}]' -or $value -notmatch '[a-z]{2}' -or ($value -notmatch ' ' -and $value -notmatch '\.$')) { continue }
            if ($value -match '^(https?:|/api/|avares:)|\.(json|axaml|png|exe|log)$|^[A-Z][a-zA-Z]+\.[A-Z]') { continue }
            $rows.Add([pscustomobject]@{ Kind = 'Code'; State = 'Hard-coded'; Key = ''; English = ($value -replace '\\n', ' ' -replace '\\"', '"'); Where = "${relative}:$(Get-LineNumber $text $m.Index)" })
        }
    }
    foreach ($k in $english.Keys) {
        if (@($rows | Where-Object Key -eq $k).Count -eq 0) {
            $rows.Add([pscustomobject]@{ Kind = 'Code'; State = 'Keyed'; Key = $k; English = $english[$k]; Where = 'set from code (page titles, Ribbon, Dashboard)' })
        }
    }
    $i18nDocs = Join-Path $root 'docs\i18n'
    New-Item $i18nDocs -ItemType Directory -Force | Out-Null
    $rows | Sort-Object Kind, State, English | Export-Csv (Join-Path $i18nDocs 'UI_TEXT_INVENTORY.csv') -NoTypeInformation -Encoding utf8
    $distinct = { param($r) @($r | Select-Object -ExpandProperty English -Unique).Count }
    $keyed = @($rows | Where-Object State -eq 'Keyed'); $xamlLeft = @($rows | Where-Object { $_.Kind -eq 'XAML' -and $_.State -eq 'Hard-coded' }); $codeLeft = @($rows | Where-Object { $_.Kind -eq 'Code' -and $_.State -eq 'Hard-coded' })
    $md = @(
        '# UI text inventory', '',
        'Every English text the MystTiq desktop shows, and whether it changes with the selected language. Generated by',
        '`scripts/Update-MystTiqUiText.ps1 -Inventory`; the full list (text, key, file and line) is',
        '[UI_TEXT_INVENTORY.csv](UI_TEXT_INVENTORY.csv).', '',
        '| | Places | Distinct texts |', '| --- | ---: | ---: |',
        "| Translatable (a key in ``Assets/i18n/en.json``) | $($keyed.Count) | $(& $distinct $keyed) |",
        "| Hard-coded in XAML (not yet translatable) | $($xamlLeft.Count) | $(& $distinct $xamlLeft) |",
        "| Hard-coded in code: status and error messages (not yet translatable) | $($codeLeft.Count) | $(& $distinct $codeLeft) |", '',
        'Texts from the MystTiq service (server messages, Doctor findings) and from the game (item and Pal names) are not in',
        'this list: they arrive from the server already in English.', ''
    )
    [IO.File]::WriteAllLines((Join-Path $i18nDocs 'UI_TEXT_INVENTORY.md'), $md, [Text.UTF8Encoding]::new($false))
    Write-Host "Inventory: $($keyed.Count) keyed, $($xamlLeft.Count) hard-coded in XAML, $($codeLeft.Count) in code."
}

if ($Check) {
    $left = @($rows | Where-Object { $_.Kind -eq 'XAML' -and $_.State -eq 'Hard-coded' })
    $left | Select-Object -First 20 | ForEach-Object { Write-Host "  hard-coded: $($_.Where): $($_.English)" -ForegroundColor Red }
    Write-Host "Hard-coded XAML texts: $($left.Count)"
    if ($left.Count -gt 0) { exit 1 }
}
