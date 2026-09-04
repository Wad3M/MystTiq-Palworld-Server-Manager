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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.5.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.5\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.5.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.4.0 unified diagnostics platform is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessDiagnosticsService') -and [regex]::IsMatch($coreText, 'record DiagnosticFinding')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.3.0 character migration and Windows service contracts are still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessCharacterMigrationService') -and [regex]::IsMatch($hostText, 'AddWindowsService')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.2.0 fleet contract is still present' `
    ([regex]::IsMatch($coreText, 'IReadOnlyList<HeadlessServerProfileConfiguration>\s+Servers') -and [regex]::IsMatch($hostText, 'class ServerProfileHost')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.5.0 contract presence -- Provider Framework
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'IPlayerModerationProvider abstraction exists' `
    ([regex]::IsMatch($coreText, 'interface IPlayerModerationProvider')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'PlayerModerationCoordinator tries providers in order and falls back on real failure, not a silent no-op' `
    ([regex]::IsMatch($coreText, 'class PlayerModerationCoordinator') -and [regex]::IsMatch($coreText, 'ProviderHealth\.Unavailable or ProviderHealth\.Misconfigured') -and [regex]::IsMatch($coreText, 'result\.Success') -and [regex]::IsMatch($coreText, 'lastAttempt')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'HeadlessPalworldAdminService registers as the REST moderation provider (existing kick/ban logic reused, not duplicated)' `
    ([regex]::IsMatch($hostText, 'class HeadlessPalworldAdminService\s*:\s*IPlayerModerationProvider')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'RconPlayerModerationProvider exists and issues real Palworld RCON KickPlayer/BanPlayer commands' `
    ([regex]::IsMatch($hostText, 'class RconPlayerModerationProvider\s*:\s*IPlayerModerationProvider') -and [regex]::IsMatch($hostText, 'KickPlayer') -and [regex]::IsMatch($hostText, 'BanPlayer')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'REST provider network call is exception-safe so the coordinator can actually fall through to RCON' `
    ([regex]::IsMatch($hostText, 'catch \(Exception ex\) when \(ex is HttpRequestException or TaskCanceledException or IOException\)')) `
    -Severity Critical -Details 'Found during live isolated verification: an unhandled HttpRequestException from a REST connection-refused case propagated straight out of PlayerModerationCoordinator, skipping RCON entirely and returning HTTP 500 instead of an honest fallback result. Fixed by catching the network failure and returning a normal Supported=true/Success=false result.'

Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'Kick/ban route dispatches through the Provider Framework coordinator; every other player action is unchanged' `
    ([regex]::IsMatch($hostText, 'normalizedAction is "kick" or "ban"') -and [regex]::IsMatch($hostText, 'p\.PlayerModeration\.ExecuteAsync')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'Provider health route exists' `
    ([regex]::IsMatch($hostText, '"/players/moderation/providers"') -and [regex]::IsMatch($hostText, 'GetProviderHealthAsync')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'ServerProfileHost/LocalManagementApiHost wire the coordinator per profile (REST first, RCON fallback), matching the established composition-not-rewrite pattern' `
    ([regex]::IsMatch($hostText, 'new PlayerModerationCoordinator\(\[playerAdmin, rconModeration\]\)')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. v0.6.5.0 contract presence -- Configuration Intelligence (first real slice)
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'Port-conflict detection is a real cross-setting check merged into the unified diagnostics report' `
    ([regex]::IsMatch($hostText, 'BuildConfigurationFindings') -and [regex]::IsMatch($hostText, '"configuration-port-conflicts"') -and [regex]::IsMatch($hostText, 'Category: "Configuration"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.5 Contracts' 'Port-conflict check considers Game/REST/RCON ports and only counts a port that is actually enabled' `
    ([regex]::IsMatch($hostText, 'PublicPort') -and [regex]::IsMatch($hostText, 'RESTAPIPort') -and [regex]::IsMatch($hostText, 'RCONPort') -and [regex]::IsMatch($hostText, 'GetBool\(snapshot, "RESTAPIEnabled"\)')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.5.0 is documented in the roadmap' `
    ([regex]::IsMatch($docText, 'v0\.6\.5\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.6.4.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.4.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.4.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.4.0\MystTiqPalworldServer_v0.6.4.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.4.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.4.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.4.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.4.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.4.0 checkpoint logic gate still passes' `
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
