[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [switch]$RunBuild,
    # v0.7.115.0: where the previous checkpoint's FullSource ZIP is. Defaults to the release machine's backup
    # folder; when it is not there, the frozen-baseline check is reported as SKIP instead of crashing the gate.
    [string]$FrozenBaselineZip = '',
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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.115.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.115\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.115.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.115\.0"' `
    'Versioning' 'app.manifest reports v0.7.115.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.114.0-Logic.ps1' 'Regression' 'v0.7.114.0 logic gate remains available' -Severity High

$users = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessUserAccountService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.114.0 fixes remain: named accounts ride the existing auth middleware' `
    ($users -match 'Rfc2898DeriveBytes\.Pbkdf2\(' -and (Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -match 'rbac\.Authenticate\(supplied\) \?\? userAccounts\.Authenticate\(supplied\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.115.0 Contracts -- the user's deficiency report (items 1-5)
# ---------------------------------------------------------------------------
$layout = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MapLabelLayout.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$supervisor = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\HeadlessSupervisor.cs'
$recoveryState = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\SupervisorRecoveryState.cs'
$fleetRecovery = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessFleetCrashRecoveryService.cs'
$crashAlerts = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\CrashAlerts.cs'
$operations = Get-MystTiqText $ctx 'src\MystTiq.Core\Operations\OperationCoordinator.cs'
$validate = Get-MystTiqText $ctx 'scripts\Validate-Release.ps1'
$framework = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.TestFramework.ps1'
$checklist = Get-MystTiqText $ctx 'RELEASE_CHECKLIST.md'

Add-MystTiqCheck $ctx 'v0.7.115.0 Contracts' '1. map labels are placed as text rectangles that avoid placed labels and other dots, not by a dot-distance circle' `
    ($layout -match 'public static IReadOnlyList<\(double X, double Y\)> ComputeLabelOffsets\(IReadOnlyList<MapLabel> labels\)' -and
     $layout -match 'placed\.Any\(box\.Overlaps\)' -and $layout -notmatch 'CollisionRadius') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.115.0 Contracts' '1. map labels are drawn in their own layer at the computed position, and marker buttons no longer take the app-wide 31px minimum' `
    ($xaml -match 'Margin="\{Binding LabelPosition\}"' -and $xaml -match 'ItemsSource="\{Binding PlayerMapPoints\}" IsHitTestVisible="False"' -and
     ([regex]::Matches($xaml, 'Margin="\{Binding MarkerMargin\}" Padding="0" MinWidth="0" MinHeight="0"')).Count -eq 2) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.115.0 Contracts' '2+4. crash recovery keeps its restart window, give-up and pinned DOWN notice in a per-profile file that survives a restart' `
    ($recoveryState -match 'public sealed record SupervisorRecoveryState\(IReadOnlyList<DateTimeOffset> RestartHistory, DateTimeOffset\? GaveUpAtUtc, string\? PinnedNotificationId\)' -and
     $supervisor -match 'SupervisorRecoveryStateStore\? stateStore = null' -and
     $fleetRecovery -match 'var resumeWatching = stateStore\?\.Read\(\)\.GaveUpAtUtc is not null;' -and
     $crashAlerts -match 'stateStore\.Read\(\)\.PinnedNotificationId') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.115.0 Contracts' '3. recovery counts as success only when the server is ready (game port up), both after a restart and after a give-up' `
    ($supervisor -match 'else if \(!restart\.Snapshot\.Ready && !await WaitUntilReadyAsync\(cancellationToken\)\)' -and
     $fleetRecovery -match 'if \(status\.Processes\.Count > 0 && status\.Ready\) return true;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.115.0 Contracts' '4. operation journals are reloaded at start-up and a Running one is closed as Interrupted' `
    ($operations -match 'private void Reload\(\)' -and $operations -match 'public const string InterruptedState = "Interrupted";') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.115.0 Contracts' '5. strict validation can run inside a gate (-AllowBuildOutputs), a missing baseline archive is a SKIP, and the checklist names no fixed version' `
    ($validate -match 'param\(\[switch\]\$Strict, \[switch\]\$AllowBuildOutputs\)' -and
     $framework -match '\[switch\]\$Skipped' -and
     $checklist -notmatch 'Current development candidate:\*\* v0\.') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.115.0 Contracts' 'the logic harness checks label rectangles, readiness, restart-surviving recovery state and operation reload' `
    ($harness -match 'Map label layout: labels are placed as real text rectangles' -and
     $harness -match 'Recovery only counts as success once the server is ready' -and
     $harness -match 'Crash recovery state survives a MystTiq restart' -and
     $harness -match 'Operation history is reloaded after a restart') `
    -Severity Critical

$smoke = Get-MystTiqText $ctx 'scripts\Test-v0.7.115.0-RouteSmoke.ps1'
Add-MystTiqCheck $ctx 'v0.7.115.0 Contracts' 'the persistence smoke restarts the real sidecar mid-cycle and uses its own FleetRoot' `
    ($smoke -match '\$cfg\.FleetRoot = \$fleetRoot' -and $smoke -match 'Stop-Sidecar\s+Start-Sidecar') `
    -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.115.0-deficiency-fixes.md' 'v0.7.115.0 Contracts' 'v0.7.115.0 architecture doc is present' -Severity Critical
# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.115.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.115\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.115.0.md' 'Documentation' 'v0.7.115.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.115.0 entry' ($changelogText -match '## v0\.7\.115\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = if ($FrozenBaselineZip) { $FrozenBaselineZip } else { Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.114.0\MystTiqPalworldServer_v0.7.114.0_FullSource.zip' }
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        # v0.7.115.0: reported, not fatal: the rest of the gate is still worth running on a machine without it.
        Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.114.0 checkpoint logic gate still passes' $false -Skipped -Severity Critical `
            -Details "Baseline archive not found at $frozenZip. Run on the release machine, or pass -FrozenBaselineZip <path>."
    }
    else {
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.114.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.114.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.114.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.114.0 checkpoint logic gate still passes' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))
    }

    # v0.7.115.0: -AllowBuildOutputs, because this gate has already built and published by now; repository
    # hygiene (no bin/obj/artifacts) is checked by the release pipeline after Clean instead. This check used to
    # fail in every gate for exactly that reason.
    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'scripts\Validate-Release.ps1') -Strict -AllowBuildOutputs
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release solution build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression Runtime' 'v0.5.1.5 isolated runtime smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.5.1.5-RuntimeSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.12.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.12.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.15.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.15.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.17.0 api-remote-enable smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.64.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.64.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.81.0 new-server wizard route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.81.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.97.0 crash analyzer route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.97.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.98.0 doctor route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.98.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.100.0 player locations route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.100.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.101.0 crash alerts route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.101.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.102.0 alert episodes route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.102.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.103.0 backup schedule route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.103.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.104.0 alert unpin route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.104.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.107.0 alert reminder route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.107.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.108.0 configured reminder route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.108.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.110.0 crash-recovery give-up/manual-recovery route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.110.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.111.0 alert mute route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.111.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.112.0 give item route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.112.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.113.0 teleport points route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.113.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.114.0 multi-user login route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.114.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'v0.7.115.0 Runtime' 'v0.7.115.0 persistence and readiness route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.115.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'MystTiq.LogicHarness passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'MystTiq.Desktop builds clean (not part of PalworldServerManager.slnx)' {
        & dotnet build (Join-Path $root 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -c Release
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
