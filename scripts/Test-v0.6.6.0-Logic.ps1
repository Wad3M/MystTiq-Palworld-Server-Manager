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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.6.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.6\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.6.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.5.0 Provider Framework is still present' `
    ([regex]::IsMatch($coreText, 'interface IPlayerModerationProvider') -and [regex]::IsMatch($coreText, 'class PlayerModerationCoordinator')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.4.0 unified diagnostics platform is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessDiagnosticsService') -and [regex]::IsMatch($coreText, 'record DiagnosticFinding')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.2.0 fleet contract is still present' `
    ([regex]::IsMatch($coreText, 'IReadOnlyList<HeadlessServerProfileConfiguration>\s+Servers') -and [regex]::IsMatch($hostText, 'class ServerProfileHost')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.6.0 contract presence -- Persistent Player Registry
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.6 Contracts' 'HeadlessPlayerRegistryService persists per-player identity/session history, sampled on the existing status-poll cadence (no new background timer)' `
    ([regex]::IsMatch($hostText, 'class HeadlessPlayerRegistryService') -and [regex]::IsMatch($hostText, 'record PlayerRegistryRecord') -and [regex]::IsMatch($hostText, 'public void Observe\(HeadlessPlayersSnapshot')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.6 Contracts' 'Registry captures real Steam ID / Player UID mapping from live REST player data, not just the raw playerId' `
    ([regex]::IsMatch($hostText, 'record PlayerRegistryRecord\(') -and [regex]::IsMatch($hostText, 'string SteamId,') -and [regex]::IsMatch($hostText, 'string UserId,')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.6 Contracts' 'Join/leave transitions are detected and recorded as a bounded, persisted event history' `
    ([regex]::IsMatch($hostText, 'record PlayerPresenceEvent') -and [regex]::IsMatch($hostText, '"Join"') -and [regex]::IsMatch($hostText, '"Leave"') -and [regex]::IsMatch($hostText, 'MaximumEvents')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.6 Contracts' 'Playtime accrual is capped per observation tick so a large gap between polls cannot be misattributed as playtime' `
    ([regex]::IsMatch($hostText, 'MaximumPerTickAccrual')) `
    -Severity High -Details 'Without this cap, a period with nobody polling (MystTiq restart, no Desktop connected) followed by a fresh poll finding the same player still online would add the entire gap as playtime.'

Add-MystTiqCheck $ctx 'v0.6.6 Contracts' 'Registry is wired per profile and its observation hook is called from the existing /status/poll route' `
    ([regex]::IsMatch($hostText, 'new HeadlessPlayerRegistryService\(paths\)') -and [regex]::IsMatch($hostText, 'p\.PlayerRegistry\.Observe\(players,')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.6 Contracts' 'Registry read routes exist' `
    ([regex]::IsMatch($hostText, '"/players/registry"') -and [regex]::IsMatch($hostText, '"/players/registry/events"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.6 Contracts' 'Desktop surfaces registry history for the selected player' `
    ([regex]::IsMatch($desktopText, 'GetPlayerRegistryAsync') -and [regex]::IsMatch($desktopText, 'PlayerRegistrySummaryText')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. v0.6.6.0 contract presence -- World Explorer 2 (abandoned-base detection slice)
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.6 Contracts' 'Abandoned-base detection is derived from orphaned guilds in the existing guild explorer snapshot' `
    ([regex]::IsMatch($hostText, 'abandonedBaseIds\s*=\s*guilds') -and [regex]::IsMatch($hostText, '"Orphaned / Needs Review"') -and [regex]::IsMatch($coreText + $hostText, 'AbandonedBaseIds')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.6.0 is documented in the roadmap' `
    ([regex]::IsMatch($docText, 'v0\.6\.6\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.6.5.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.5.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.5.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.5.0\MystTiqPalworldServer_v0.6.5.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.5.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.5.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.5.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.5.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.5.0 checkpoint logic gate still passes' `
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
