# MystTiq v0.9.1.0: file reviewed for this release (2026-09-28).
#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    # Defaults to the version in Directory.Build.props.
    [string]$Version,
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),
    # Only report what would change.
    [switch]$WhatIf,
    # Write nothing; list the text files without this version's stamp (any date) and exit 1 if there are any. The
    # release gate uses this.
    [switch]$Check,
    # Write the per-file list (CSV) here.
    [string]$CsvPath
)

# v0.8.26.0: marks every text file in the repository as reviewed for a release, with one comment line in the file's own
# comment syntax:
#     MystTiq v<version>: file reviewed for this release (<yyyy-MM-dd>).
# It is idempotent: a file that already has a stamp gets its version and date updated in place, never a second line.
# The stamp goes on the first line, except after a shebang (#!), an XML declaration (<?xml ...?>) or an HTML doctype,
# which must stay first. The file's encoding (UTF-8 with or without BOM) and line endings (CRLF or LF) are kept.
# Not stamped, and listed with the reason: JSON (no comment syntax), LICENSE (GitHub's licence detection reads it
# verbatim) and binaries (images, icons). Generated and local folders (bin, obj, artifacts, .git, .claude, .vs) are
# skipped entirely.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path $ProjectRoot).Path
if (-not $Version) { $Version = & (Join-Path $root 'scripts\Get-ProjectVersion.ps1') }
$stampText = "MystTiq v${Version}: file reviewed for this release ($Date)."
$stampPattern = [regex]'MystTiq v\d+\.\d+\.\d+\.\d+: file reviewed for this release \(\d{4}-\d{2}-\d{2}\)\.'

# Comment syntax per extension: prefix and suffix around the stamp text.
$syntax = @{
    '.cs' = @('// ', ''); '.cpp' = @('// ', ''); '.h' = @('// ', '')
    '.ps1' = @('# ', ''); '.psm1' = @('# ', ''); '.sh' = @('# ', ''); '.py' = @('# ', ''); '.yml' = @('# ', ''); '.yaml' = @('# ', '')
    '.gitignore' = @('# ', ''); '.gitattributes' = @('# ', ''); '.editorconfig' = @('# ', '')
    '.def' = @('; ', '')
    '.css' = @('/* ', ' */')
    '.md' = @('<!-- ', ' -->'); '.html' = @('<!-- ', ' -->'); '.axaml' = @('<!-- ', ' -->'); '.xaml' = @('<!-- ', ' -->')
    '.csproj' = @('<!-- ', ' -->'); '.props' = @('<!-- ', ' -->'); '.targets' = @('<!-- ', ' -->'); '.slnx' = @('<!-- ', ' -->')
    '.manifest' = @('<!-- ', ' -->'); '.svg' = @('<!-- ', ' -->'); '.xml' = @('<!-- ', ' -->'); '.resx' = @('<!-- ', ' -->')
}
$binary = '.png', '.jpg', '.jpeg', '.gif', '.ico', '.webp', '.bmp', '.dll', '.exe', '.zip', '.pyc', '.pak', '.sav'
$skipDirs = 'bin', 'obj', 'artifacts', '.git', '.claude', '.vs', '__pycache__', 'node_modules', 'TestResults'

function Get-Kind([IO.FileInfo]$File) {
    $ext = if ($File.Name -in '.gitignore', '.gitattributes', '.editorconfig') { $File.Name } else { $File.Extension.ToLowerInvariant() }
    if ($File.Name -eq 'LICENSE') { return @{ Action = 'Skipped'; Reason = 'licence text is kept verbatim so GitHub detects the MIT licence' } }
    if ($ext -in $binary) { return @{ Action = 'Binary'; Reason = 'binary file: reviewed, cannot carry a text stamp' } }
    if ($ext -eq '.json') { return @{ Action = 'Skipped'; Reason = 'JSON has no comment syntax; reviewed, not stamped' } }
    if ($syntax.ContainsKey($ext)) { return @{ Action = 'Stamp'; Syntax = $syntax[$ext] } }
    return @{ Action = 'Skipped'; Reason = "no known comment syntax for '$ext'; reviewed, not stamped" }
}

$files = Get-ChildItem $root -File -Recurse -Force | Where-Object {
    $parts = $_.FullName.Substring($root.Length + 1).Split([IO.Path]::DirectorySeparatorChar)
    -not @($parts | Select-Object -SkipLast 1 | Where-Object { $_ -in $skipDirs }).Count
} | Sort-Object FullName

$rows = [System.Collections.Generic.List[object]]::new()
$utf8 = [Text.UTF8Encoding]::new($false, $true)
foreach ($file in $files) {
    $relative = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
    $kind = Get-Kind $file
    if ($kind.Action -ne 'Stamp') { $rows.Add([pscustomobject]@{ Path = $relative; Action = $kind.Action; Detail = $kind.Reason }); continue }

    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    try { $text = $utf8.GetString($bytes, $(if ($bom) { 3 } else { 0 }), $bytes.Length - $(if ($bom) { 3 } else { 0 })) }
    catch { $rows.Add([pscustomobject]@{ Path = $relative; Action = 'Skipped'; Detail = 'not valid UTF-8; left untouched' }); continue }
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $line = $kind.Syntax[0] + $stampText + $kind.Syntax[1]

    if ($Check) {
        $found = $stampPattern.Match($text)
        $ok = $found.Success -and $found.Value.StartsWith("MystTiq v${Version}:")
        $rows.Add([pscustomobject]@{ Path = $relative; Action = $(if ($ok) { 'Current' } else { 'Missing' }); Detail = $(if ($found.Success) { $found.Value } else { 'no stamp' }) })
        continue
    }
    if ($stampPattern.IsMatch($text)) {
        $updated = $stampPattern.Replace($text, $stampText, 1)
        $action = if ($updated -eq $text) { 'Current' } else { 'Updated' }
    } else {
        # Where the stamp goes: after a shebang, an XML declaration or a doctype; otherwise first.
        $at = 0
        if ($text.StartsWith('#!')) {
            $end = $text.IndexOf("`n"); $at = if ($end -lt 0) { $text.Length } else { $end + 1 }
            if ($end -lt 0) { $line = $newline + $line }
        } elseif ($text.StartsWith('<?xml')) {
            $at = $text.IndexOf('?>') + 2
        } elseif ($text -match '^(?i)<!doctype[^>]*>') {
            $at = $Matches[0].Length
        }
        if ($at -gt 0 -and $at -lt $text.Length -and ($text[$at - 1] -ne "`n")) {
            # The declaration ends mid-line: put the stamp on its own line after it.
            $insert = $newline + $line
            if ($text.Substring($at).StartsWith($newline)) { $updated = $text.Insert($at, $insert) }
            else { $updated = $text.Insert($at, $insert + $newline) }
        } else {
            $updated = $text.Insert($at, $line + $newline)
        }
        $action = 'Stamped'
    }
    if ($action -ne 'Current' -and -not $WhatIf) {
        $out = $utf8.GetBytes($updated)
        if ($bom) { $out = [byte[]](0xEF, 0xBB, 0xBF) + $out }
        [IO.File]::WriteAllBytes($file.FullName, $out)
    }
    $rows.Add([pscustomobject]@{ Path = $relative; Action = $action; Detail = $line.Trim() })
}

if ($CsvPath) { $rows | Export-Csv -LiteralPath $CsvPath -NoTypeInformation -Encoding utf8 }
$rows | Group-Object Action | Sort-Object Name | ForEach-Object { '{0,-8} {1}' -f $_.Name, $_.Count }
if ($WhatIf) { Write-Host '(-WhatIf: nothing was written)' -ForegroundColor Yellow }
if ($Check) {
    $missing = @($rows | Where-Object Action -eq 'Missing')
    $missing | Select-Object -First 20 | ForEach-Object { Write-Host "  no v$Version stamp: $($_.Path) ($($_.Detail))" -ForegroundColor Red }
    if ($missing.Count -gt 0) { exit 1 }
}
