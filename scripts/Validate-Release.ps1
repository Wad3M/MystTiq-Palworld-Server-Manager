[CmdletBinding()]
param([switch]$Strict)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$issues = [System.Collections.Generic.List[object]]::new()

function Add-Issue {
    param([ValidateSet('Error','Warning')][string]$Severity,[string]$Check,[string]$Message)
    $issues.Add([pscustomobject]@{ Severity=$Severity; Check=$Check; Message=$Message })
}
function Test-RequiredPath {
    param([string]$Relative,[ValidateSet('File','Directory','Any')][string]$Type='Any')
    $path = Join-Path $root $Relative
    $ok = if ($Type -eq 'File') { Test-Path $path -PathType Leaf } elseif ($Type -eq 'Directory') { Test-Path $path -PathType Container } else { Test-Path $path }
    if (-not $ok) { Add-Issue Error 'Repository structure' "Missing required $($Type.ToLowerInvariant()): $Relative" }
}

$version = & (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')
Write-Host "==> Validating MystTiq v$version release candidate..." -ForegroundColor Cyan
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { Add-Issue Error 'Version format' "VersionPrefix is not a supported four-part version: $version" }

@('Directory.Build.props','Build.ps1','README.md','CONTRIBUTING.md','CHANGELOG.md','LICENSE','RELEASE_CHECKLIST.md','PalworldServerManager.slnx','scripts\Build.ps1','scripts\Build-Release.ps1','scripts\Build-Installer.ps1','scripts\Build-Checksums.ps1','scripts\Validate-Release.ps1','scripts\Package-Portable.ps1','installer\MystTiqPalworldServer.iss','src\PalworldManager\PalworldManager.csproj','src\PalworldManager\MainWindow.xaml') | ForEach-Object { Test-RequiredPath $_ File }
Test-RequiredPath 'release-notes' Directory

$blockedDirectoryNames = @('.git','.vs','bin','obj','artifacts','publish','Backups','Logs')
Get-ChildItem $root -Directory -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -in $blockedDirectoryNames } | ForEach-Object { Add-Issue Error 'Repository hygiene' "Generated/private directory found: $($_.FullName.Substring($root.Length + 1))" }
$blockedExtensions = @('.sav','.bak','.tmp','.dmp','.pfx','.snk')
Get-ChildItem $root -File -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { $_.Extension.ToLowerInvariant() -in $blockedExtensions } | ForEach-Object { Add-Issue Error 'Repository hygiene' "Blocked runtime or signing file found: $($_.FullName.Substring($root.Length + 1))" }

foreach ($pattern in @('APPLY_*.md','BUILD_TEST_PLAN_*.md','COMPILE_HOTFIX_*.md','RELEASE_NOTES_*.md')) {
    Get-ChildItem $root -File -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object { Add-Issue Error 'Release-note organization' "Move root note into release-notes: $($_.Name)" }
}
foreach ($requiredNote in @("release-notes\v$version.md","release-notes\BUILD_TEST_PLAN_v$version.md","release-notes\APPLY_v${version}_CHANGED_FILES.md")) {
    if (-not (Test-Path (Join-Path $root $requiredNote) -PathType Leaf)) { Add-Issue Error 'Release documentation' "Missing release document: $requiredNote" }
}

# Version consistency in active product and release files.
$expectedVersionFiles = @('Directory.Build.props','src\PalworldManager\app.manifest','README.md','docs\index.html',("release-notes\v$version.md"))
foreach ($relative in $expectedVersionFiles) {
    $path = Join-Path $root $relative
    if ((Test-Path $path -PathType Leaf) -and -not (Select-String -Path $path -SimpleMatch $version -Quiet)) { Add-Issue Warning 'Version consistency' "Current version $version was not found in $relative" }
}
# Scan active source/build files for accidental stale references within the current release line.
# Documentation landing pages are intentionally excluded from this stale-reference scan because
# they may legitimately mention the current candidate, official baseline, and planned versions
# at the same time. They are still checked above to ensure the current version is present.
$activeFiles = Get-ChildItem $root -File -Recurse -Include *.cs,*.xaml,*.csproj,*.props,*.ps1,*.iss,*.yml,*.yaml,*.html | Where-Object {
    $relativePath = $_.FullName.Substring($root.Length + 1)
    $_.FullName -notlike "*\release-notes\*" -and
    $_.Name -ne 'CHANGELOG.md' -and
    $relativePath -ne 'docs\index.html'
}
$versionParts = $version.Split('.')
$releaseLinePrefix = [regex]::Escape(($versionParts[0..2] -join '.'))
$versionPattern = "(?<!\d)$releaseLinePrefix\.\d+(?:-[0-9A-Za-z.-]+)?(?!\d)"
foreach ($file in $activeFiles) {
    foreach ($lineMatch in Select-String -Path $file.FullName -Pattern $versionPattern -AllMatches -ErrorAction SilentlyContinue) {
        $stale = @($lineMatch.Matches | Where-Object { $_.Value -ne $version })
        if ($stale.Count -gt 0) {
            Add-Issue Warning 'Version consistency' "Possible stale version in $($file.FullName.Substring($root.Length + 1)):$($lineMatch.LineNumber): $($lineMatch.Line.Trim())"
        }
    }
}

# Build scripts must parse successfully before release work begins.
foreach ($script in Get-ChildItem (Join-Path $root 'scripts') -File -Filter *.ps1) {
    $tokens=$null; $parseErrors=$null
    [void][System.Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$tokens,[ref]$parseErrors)
    foreach ($parseError in $parseErrors) { Add-Issue Error 'PowerShell syntax' "$($script.Name):$($parseError.Extent.StartLineNumber): $($parseError.Message)" }
}
$tokens=$null; $parseErrors=$null
[void][System.Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'Build.ps1'),[ref]$tokens,[ref]$parseErrors)
foreach ($parseError in $parseErrors) { Add-Issue Error 'PowerShell syntax' "Build.ps1:$($parseError.Extent.StartLineNumber): $($parseError.Message)" }

# Direct message boxes should remain behind the dialog service.
Get-ChildItem (Join-Path $root 'src\PalworldManager') -File -Recurse -Filter *.cs | Where-Object { $_.FullName -notlike '*\Services\Infrastructure\DialogService.cs' } | ForEach-Object {
    foreach ($match in Select-String -Path $_.FullName -Pattern 'MessageBox\.Show\s*\(' -ErrorAction SilentlyContinue) { Add-Issue Error 'Dialog consistency' "Direct MessageBox.Show in $($_.FullName.Substring($root.Length + 1)):$($match.LineNumber)" }
}

# XAML resource and MainWindow handler checks.
$xamlFiles = Get-ChildItem (Join-Path $root 'src\PalworldManager') -File -Recurse -Filter *.xaml
$defined = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$referenced = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($file in $xamlFiles) {
    $text = Get-Content $file.FullName -Raw
    foreach ($m in [regex]::Matches($text, 'x:Key\s*=\s*"([^"]+)"')) { [void]$defined.Add($m.Groups[1].Value) }
    foreach ($m in [regex]::Matches($text, '\{StaticResource\s+([^}\s]+)\}')) { [void]$referenced.Add($m.Groups[1].Value) }
}
foreach ($key in $referenced) { if (-not $defined.Contains($key) -and $key -ne 'BooleanToVisibilityConverter') { Add-Issue Error 'XAML resources' "Unresolved StaticResource: $key" } }
$mainXaml = Join-Path $root 'src\PalworldManager\MainWindow.xaml'
if (Test-Path $mainXaml) {
    $xaml = Get-Content $mainXaml -Raw
    $handlers = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($m in [regex]::Matches($xaml, '(?<![A-Za-z0-9_:])(?:Click|Loaded|SelectionChanged|Checked|Unchecked|TextChanged|Closing|Closed|PreviewMouseDown|MouseDown)\s*=\s*"([A-Za-z_][A-Za-z0-9_]*)"')) { [void]$handlers.Add($m.Groups[1].Value) }
    $codeText = (Get-ChildItem (Join-Path $root 'src\PalworldManager') -File -Recurse -Filter 'MainWindow*.cs' | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
    foreach ($handler in $handlers) { if ($codeText -notmatch "\b$([regex]::Escape($handler))\s*\(") { Add-Issue Error 'XAML handlers' "Handler referenced by MainWindow.xaml was not found: $handler" } }
}

$errors = @($issues | Where-Object Severity -eq 'Error')
$warnings = @($issues | Where-Object Severity -eq 'Warning')
if ($issues.Count -gt 0) { $issues | Sort-Object Severity,Check | Format-Table -AutoSize }
Write-Host "Validation summary: $($errors.Count) error(s), $($warnings.Count) warning(s)." -ForegroundColor $(if ($errors.Count) {'Red'} elseif ($warnings.Count) {'Yellow'} else {'Green'})
if ($errors.Count -gt 0 -or ($Strict -and $warnings.Count -gt 0)) { throw 'Release validation failed.' }
Write-Host 'Release validation passed.' -ForegroundColor Green
