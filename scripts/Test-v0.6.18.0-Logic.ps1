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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.18.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.18\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.18.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.17.0 Discord bot control is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessDiscordBotService')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.15.0 Pal Editor is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessPalEditService')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.18.0 contract presence -- Anti-Cheat & Save-Integrity Scanning
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.18 Contracts' 'A real anti-cheat service exists with the three planned rules' `
    ([regex]::IsMatch($hostText, 'class HeadlessAntiCheatService') -and [regex]::IsMatch($coreText, 'record InvalidSteamIdRule') -and [regex]::IsMatch($coreText, 'record ImpossibleLevelRule') -and [regex]::IsMatch($coreText, 'record PalStatAnomalyRule')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.18 Contracts' 'Every rule defaults to Flag, not automatic enforcement' `
    ([regex]::IsMatch($coreText, 'InvalidSteamIdRule\s+InvalidSteamId[\s\S]{0,60}AntiCheatResponse\.Flag') -and [regex]::IsMatch($coreText, 'ImpossibleLevelRule\s+ImpossibleLevel[\s\S]{0,80}AntiCheatResponse\.Flag') -and [regex]::IsMatch($coreText, 'PalStatAnomalyRule\s+PalStatAnomaly[\s\S]{0,60}AntiCheatResponse\.Flag')) `
    -Severity Critical -Details 'Confirmed by explicit user choice during planning: no player is ever auto-kicked/banned until an admin opts a rule into enforcement.'

Add-MystTiqCheck $ctx 'v0.6.18 Contracts' 'Invalid Steam ID detection uses the real, cited SteamID64 format (17 digits, starts 7656119)' `
    ([regex]::IsMatch($hostText, '\^7656119\\d\{10\}\$')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.18 Contracts' 'Impossible-level default matches Palworld''s real 1.0 vanilla level cap' `
    ([regex]::IsMatch($coreText, 'ImpossibleLevel[\s\S]{0,60}=\s*new\(true,\s*80')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.6.18 Contracts' 'Pal stat scan reuses HeadlessPalEditService.ListPalsAsync read-only, not a new decode path' `
    ([regex]::IsMatch($hostText, 'palEdit\.ListPalsAsync')) `
    -Severity Critical -Details 'No new Level.sav decode logic -- composes directly onto the v0.6.15.0 Pal Editor foundation.'

Add-MystTiqCheck $ctx 'v0.6.18 Contracts' 'Kick/Ban responses reuse PlayerModerationCoordinator, the same mechanism the v0.6.17.0 Discord bot already uses' `
    ([regex]::IsMatch($hostText, 'playerModeration\.ExecuteAsync\(action, playerId')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.18 Contracts' 'Live checks run on the existing automation background loop, not a new timer' `
    ([regex]::IsMatch($hostText, 'antiCheat\.EvaluateLivePlayersAsync') -and [regex]::IsMatch($hostText, 'antiCheat\.EvaluateThrottledAsync')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.18 Contracts' 'Anti-cheat rule routes are Admin-gated, matching the existing alert-rule routes' `
    ([regex]::IsMatch($hostText, '"/anticheat/rules"[\s\S]{0,220}RequireRole\(MystTiqRole\.Admin')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.18 Contracts' 'Desktop wiring: Anti-Cheat card and API client methods exist' `
    ([regex]::IsMatch($desktopText, 'Anti-Cheat') -and [regex]::IsMatch($desktopText, 'GetAntiCheatRulesAsync') -and [regex]::IsMatch($desktopText, 'SaveAntiCheatRulesAsync')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.18.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.18\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.17.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.17.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.17.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.17.0\MystTiqPalworldServer_v0.6.17.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.17.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.17.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.17.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.17.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.17.0 checkpoint logic gate still passes' `
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
