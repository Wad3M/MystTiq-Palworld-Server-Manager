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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.2.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.2\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.2.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

Test-MystTiqTextMatch $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs' `
    'RaisePropertyChanged\(nameof\(IsAutomationPage\)\)' `
    'Regression' 'RaisePageVisibility still notifies IsAutomationPage (the v0.6.1.0 blank-page regression fixed this session must not resurface)' -Severity Critical

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.1.0 automation engine contract is still present' `
    ([regex]::IsMatch($coreText, 'AutomationRule|AutomationTrigger|AutomationAction') -and [regex]::IsMatch($hostText, 'PeriodicTimer')) `
    -Severity Critical -Details 'Expected AutomationRule/AutomationTrigger/AutomationAction plus the PeriodicTimer scheduler loop to remain intact.'

Add-MystTiqCheck $ctx 'Regression' 'v0.6.1.0 RBAC contract is still present' `
    ([regex]::IsMatch($coreText, 'MystTiqRole') -and [regex]::IsMatch($coreText, 'MystTiqPrincipal') -and [regex]::IsMatch($coreText, 'LegacyOwner')) `
    -Severity Critical -Details 'Expected MystTiqRole/MystTiqPrincipal and the backward-compatible LegacyOwner path to remain intact.'

Add-MystTiqCheck $ctx 'Regression' 'v0.6.1.0 backup classes and Alert Center are still present' `
    ([regex]::IsMatch($hostText, 'enum BackupClass') -and [regex]::IsMatch($hostText, 'HeadlessAlertCenterService')) `
    -Severity Critical -Details 'Expected BackupClass and HeadlessAlertCenterService to remain intact.'

# ---------------------------------------------------------------------------
# 3. v0.6.2.0 contract presence -- Multi-Server Fleet (Runtime Providers deferred,
#    see the implementation note for the explicit Docker/Wine scope-down).
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'Config schema v3: plural server profiles replace the single Server block' `
    ([regex]::IsMatch($coreText, 'IReadOnlyList<HeadlessServerProfileConfiguration>\s+Servers') -and [regex]::IsMatch($coreText, 'CurrentSchemaVersion\s*=\s*3')) `
    -Severity Critical -Details 'Expected HeadlessConfiguration.Servers (plural) and CurrentSchemaVersion bumped to 3.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'v2 -> v3 configuration migration exists' `
    ([regex]::IsMatch($coreText, 'MigrateV2')) `
    -Severity Critical -Details 'Expected a MigrateV2 method wrapping a legacy single-Server config into one "default" Servers entry.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'Runtime-provider seam (ServerRuntimeKind) is defined' `
    ([regex]::IsMatch($coreText, 'enum ServerRuntimeKind')) `
    -Severity High -Details 'Expected a ServerRuntimeKind enum (WindowsNative/LinuxNative) as the seam for a later Docker/Wine pass.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'OperationCoordinator locks are partitioned per server profile' `
    ([regex]::IsMatch($coreText, 'Dictionary<\(ServerProfileId Profile, string Key\), OperationId>')) `
    -Severity Critical -Details 'Expected resourceLocks keyed by (ServerProfileId, resourceKey) so one server''s lock cannot block another''s.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'One full service graph is constructed per server profile' `
    ([regex]::IsMatch($hostText, 'class ServerProfileHost') -and [regex]::IsMatch($hostText, 'foreach \(var serverConfig in configuration\.Servers\)')) `
    -Severity Critical -Details 'Expected a ServerProfileHost bundle type and a per-profile construction loop in LocalManagementApiHost.Create.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'Fleet-scope routes exist (list/add/remove profiles, staggered bulk actions)' `
    ([regex]::IsMatch($hostText, '"/api/v1/servers"') -and [regex]::IsMatch($hostText, '"/api/v1/fleet/backup-all"') -and [regex]::IsMatch($hostText, '"/api/v1/fleet/doctor-all"') -and [regex]::IsMatch($hostText, '"/api/v1/fleet/update-all"')) `
    -Severity Critical -Details 'Expected GET/POST /api/v1/servers and the three fleet bulk-action routes.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'Every profile-scoped route is also mapped under /api/v1/servers/{profileId}/...' `
    ([regex]::IsMatch($hostText, '\$"/api/v1/servers/\{profileHost\.Id\.Value\}"')) `
    -Severity Critical -Details 'Expected MapProfileRoutes to be invoked under a per-profile route-group prefix for every configured server, in addition to the backward-compatible unprefixed default-profile mapping.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'Per-server RBAC scoping exists' `
    ([regex]::IsMatch($coreText, 'ScopedServerProfileId')) `
    -Severity Critical -Details 'Expected MystTiqPrincipal.ScopedServerProfileId, checked by RequireRole against the resolved route profile.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'Compound process identity: /healthz reports every managed server profile' `
    ([regex]::IsMatch($hostText, 'serverProfileIds\s*=')) `
    -Severity Critical -Details 'Expected /healthz to report serverProfileIds so LocalManagementBootstrapper can verify identity instead of trusting any compatible-looking process.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'Desktop LocalManagementBootstrapper verifies expected server profile before reusing an instance' `
    ([regex]::IsMatch($desktopText, 'HasExpectedProfile') -and [regex]::IsMatch($desktopText, 'expectedServerProfileId')) `
    -Severity Critical -Details 'Expected the "already reachable, reuse it" fast path to check the probed instance''s serverProfileIds, closing the cross-talk bug found during the v0.6.1.0 screenshot session.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'Desktop exposes a Fleet page' `
    ([regex]::IsMatch($desktopText, 'IsFleetPage') -and [regex]::IsMatch($desktopText, 'ServerProfileSummaryDto') -and [regex]::IsMatch($desktopText, 'FleetActionResultDto')) `
    -Severity Critical -Details 'Expected the Fleet nav destination, server-profile list, and fleet bulk-action results wired into the Desktop shell.'

Add-MystTiqCheck $ctx 'v0.6.2 Contracts' 'Fleet bulk actions stagger across profiles' `
    ([regex]::IsMatch($hostText, 'FleetStaggerSeconds')) `
    -Severity High -Details 'Expected a configurable stagger delay so Backup All / Doctor All / Update All do not fire every server simultaneously.'

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.2.0 is documented in the roadmap' `
    ([regex]::IsMatch($docText, 'v0\.6\.2\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.1.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.1.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.1.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    # Same rationale as v0.6.0.0/v0.6.1.0's own gates: a version-locked frozen-checkpoint gate can
    # never pass again against the live tree once VersionPrefix moves forward. Run it against the
    # frozen v0.6.1.0 FullSource checkpoint instead.
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.1.0\MystTiqPalworldServer_v0.6.1.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.1.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.1.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.1.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.1.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.1.0 checkpoint logic gate still passes' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))

    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'Build.ps1') Validate -StrictValidation
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release solution build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    # Preserve the last known-good runtime acceptance test.
    Test-MystTiqCommand $ctx 'Regression Runtime' 'v0.5.1.5 isolated runtime smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.5.1.5-RuntimeSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
