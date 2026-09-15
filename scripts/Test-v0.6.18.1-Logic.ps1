[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [switch]$RunBuild,
    [bool]$ExportJson = $true,
    [bool]$ExportJUnit = $true
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$root = (Resolve-Path $ProjectRoot).Path
$framework = Join-Path $root 'scripts\Testing\MystTiq.TestFramework.ps1'
if (-not (Test-Path $framework -PathType Leaf)) {
    throw "MystTiq test framework not found: $framework"
}
. $framework

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.18.1' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.18\.1</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.18.1' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.18.0 Anti-Cheat service is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessAntiCheatService')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.17.0 Discord bot control is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessDiscordBotService')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.15.0 Pal Editor is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessPalEditService')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.18.1 contract presence -- the three code-review fixes
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.18.1 Fixes' 'Pal Editor Gender verification uses an exact-suffix match, not EndsWith' `
    ([regex]::IsMatch($hostText, 'GenderMatches\(') -and -not [regex]::IsMatch($hostText, 'Gender\)\.EndsWith\(changes\.Gender')) `
    -Severity Critical -Details 'EndsWith("Male", OrdinalIgnoreCase) also matches "Female" (it ends in "...male") -- the old check could never fail for a Male mutation regardless of what was actually stored.'

Add-MystTiqCheck $ctx 'v0.6.18.1 Fixes' 'Anti-cheat Pal-scan cooldown is keyed per Pal instance, not per owner' `
    ([regex]::IsMatch($hostText, 'cooldownIdentity:\s*pal\.InstanceId') -and [regex]::IsMatch($hostText, 'string\?\s*cooldownIdentity\s*=\s*null')) `
    -Severity Critical -Details 'Previously a second anomalous Pal owned by the same player (or a second unowned anomalous Pal) was silently dropped by a shared cooldown key.'

Add-MystTiqCheck $ctx 'v0.6.18.1 Fixes' 'Discord bot bad-token detector is scoped to Warning-or-worse severity from the Gateway source' `
    ([regex]::IsMatch($hostText, 'isWarningOrWorse\s*&&\s*string\.Equals\(message\.Source,\s*"Gateway"')) `
    -Severity Critical -Details 'Previously any log line at any severity containing the substring "401" counted toward the failure threshold, risking a false-positive teardown of a healthy connection.'

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.18.1 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.18\.1') -or [regex]::IsMatch((Get-Content (Join-Path $root 'CHANGELOG.md') -Raw), 'v0\.6\.18\.1')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.18.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.18.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.18.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.18.0\MystTiqPalworldServer_v0.6.18.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.18.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.18.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.18.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.18.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.18.0 checkpoint logic gate still passes' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))

    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'Build.ps1') Validate -StrictValidation
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release solution build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression Runtime' 'v0.5.1.5 isolated runtime smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.5.1.5-RuntimeSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
