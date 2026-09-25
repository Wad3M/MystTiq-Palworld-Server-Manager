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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.101.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.101\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.101.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.101\.0"' `
    'Versioning' 'app.manifest reports v0.7.101.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.100.0-Logic.ps1' 'Regression' 'v0.7.100.0 logic gate remains available' -Severity High

$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.100.0 map zoom, click-to-zoom and offline players remain' `
    ($vm -match 'private void ZoomToBase\(string\? baseId\)' -and $vm -match 'private bool _showOfflinePlayersOnMap = true;' -and
     $xaml -match 'PointerWheelChanged="MapSurface_OnPointerWheelChanged"' -and
     (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Services\MapViewport.cs'))) `
    -Severity Critical

$builder = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\CrashAnalysisBuilder.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.97.0 new-versus-repeated crash findings remain (the alert text relies on them)' `
    ($builder -match 'previouslyReportedKeys is null \|\| !previouslyReportedKeys\.Contains\(key\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.101.0 Contracts -- crash alerts
# ---------------------------------------------------------------------------
$obs = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\SupervisorObserver.cs'
$sup = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\HeadlessSupervisor.cs'
Add-MystTiqCheck $ctx 'v0.7.101.0 Contracts' 'the supervisor reports a crash, a successful recovery, a failed restart and giving up' `
    ($obs -match 'CrashDetected,' -and $obs -match 'RecoverySucceeded,' -and $obs -match 'RecoveryFailed,' -and $obs -match 'RecoverySuppressed' -and
     $sup -match 'NotifyAsync\(SupervisorEventKind\.CrashDetected' -and $sup -match 'NotifyAsync\(SupervisorEventKind\.RecoverySucceeded' -and
     $sup -match 'NotifyAsync\(SupervisorEventKind\.RecoveryFailed' -and $sup -match 'NotifyAsync\(SupervisorEventKind\.RecoverySuppressed') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.101.0 Contracts' 'an observer that throws can never stop crash recovery, and the observer is optional so service-run is unchanged' `
    ($sup -match 'catch \(Exception ex\) when \(ex is not OperationCanceledException\)' -and $sup -match 'ISupervisorObserver\? observer = null' -and $sup -match 'if \(observer is null\) return;') `
    -Severity Critical

$alerts = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\CrashAlerts.cs'
Add-MystTiqCheck $ctx 'v0.7.101.0 Contracts' 'giving up is a pinned critical alert saying the server is down, and a repeated finding is never presented as the cause' `
    ($alerts -match 'server is DOWN, automatic recovery gave up' -and $alerts -match ',\s*true\);' -and $alerts -match 'if \(!top\.IsNew\)' -and $alerts -match 'no new crash evidence') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.101.0 Contracts' 'the crash analysis runs at the moment of the crash, and giving up reuses the last analysis instead of re-marking it' `
    ($alerts -match 'crashTools\.Analyze\(names\)' -and $alerts -match 'crashTools\.History\(1\)\.FirstOrDefault\(\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.101.0 Contracts' 'alerts go through the normal notification pipeline so they reach the admin''s configured routes' `
    ($alerts -match 'notifications\.Create\(alert\.Severity, alert\.Title, alert\.Message, alert\.Pinned\)') `
    -Severity Critical

$api = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$recovery = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessFleetCrashRecoveryService.cs'
Add-MystTiqCheck $ctx 'v0.7.101.0 Contracts' 'every profile''s crash recovery is composed with the alert observer' `
    ($api -match 'new CrashAlertObserver\(' -and $api -match 'serverConfig\.LaunchArguments, crashAlerts\)' -and $recovery -match 'ISupervisorObserver\? observer = null') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.101.0 Contracts' 'the logic harness runs the real recovery loop against a scripted server, and covers the alert text and the observer' `
    ($harness -match 'RunScenario\("Crash recovery reports a crash' -and $harness -match 'RunScenario\("Crash recovery still restarts' -and
     $harness -match 'RunScenario\("Crash alert text' -and $harness -match 'RunScenarioAsync\("Crash alert observer' -and $harness -match 'sealed class ScriptedLifecycle') `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.101.0-RouteSmoke.ps1' 'v0.7.101.0 Contracts' 'the crash alerts route smoke exists' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.101.0-crash-alerts.md' 'v0.7.101.0 Contracts' 'v0.7.101.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.101.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.101\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.101.0.md' 'Documentation' 'v0.7.101.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.101.0 entry' ($changelogText -match '## v0\.7\.101\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.100.0\MystTiqPalworldServer_v0.7.100.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.100.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.100.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.100.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.100.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.100.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'v0.7.101.0 Runtime' 'v0.7.101.0 crash alerts route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.101.0-RouteSmoke.ps1') -ProjectRoot $root
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
