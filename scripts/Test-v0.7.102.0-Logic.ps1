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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.102.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.102\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.102.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.102\.0"' `
    'Versioning' 'app.manifest reports v0.7.102.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.101.0-Logic.ps1' 'Regression' 'v0.7.101.0 logic gate remains available' -Severity High

$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$api = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.101.0 crash alerts and v0.7.100.0 map zoom remain' `
    ($api -match 'new CrashAlertObserver\(' -and $vm -match 'private void ZoomToBase\(string\? baseId\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.102.0 Contracts -- the deficiencies found by walking the running app
# ---------------------------------------------------------------------------
$dto = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\PalworldConfigurationDtos.cs'
Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'a slider never writes back its own clamped or snapped echo of a value the file already holds' `
    ($dto -match 'if \(IsCoercionEcho\(value, current\)\) return;' -and $dto -match 'public bool IsCoercionEcho\(double written, double current\)' -and
     $dto -match 'Math\.Abs\(written - current\) > 1e-9 && Math\.Abs\(written - Snap\(current\)\) < 1e-9') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'Item Corruption Rate accepts values below the old 0.1 minimum' `
    ($vm -match '\("ItemCorruptionMultiplier", "Item Corruption Rate", "Controls how quickly items degrade\.", 0, 10, 0\.05, "x", "Items & Work"\)') `
    -Severity Critical

$tracker = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\AlertEpisodes.cs'
$alert = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'
Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'the Alert Center tracks each condition as an episode: one alert, one recovery, persisted across restarts' `
    ($tracker -match 'EpisodeAction\.Alert' -and $tracker -match 'EpisodeAction\.Recovered' -and $tracker -match 'File\.Move\(partial, statePath, true\)' -and
     $alert -match 'episodes\.Observe\(ruleKey, active, DateTimeOffset\.UtcNow, flapGuard\)' -and $alert -match 'episodes = new AlertEpisodeTracker\(Path\.Combine\(stateRoot, "episodes\.json"\)\)' -and
     $alert -notmatch 'lastFiredUtc') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'every alert rule is tracked, and a switched-off rule closes its episode' `
    (([regex]::Matches($alert, 'Track\("(high-cpu|high-memory|low-disk|disk-exhaustion-predicted|mod-health-degraded)"')).Count -eq 5 -and ([regex]::Matches($alert, 'episodes\.Close\(')).Count -eq 5) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'recovery is a Success notice and only a critical alert is pinned' `
    ($alert -match 'notifications\.Create\("Success", \$"Resolved: \{title\}", resolvedMessage\)' -and $alert -match 'pinned: severity == "Critical"') `
    -Severity Critical

$disk = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\DiskSpaceRules.cs'
$rules = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\DoctorHealthRules.cs'
$diag = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiagnosticsService.cs'
Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'the Doctor and the Alert Center judge disk space with one shared rule and the same configured percentage' `
    ($disk -match 'CriticalFreeGiB = 2' -and $disk -match 'WarningFreeGiB = 5' -and
     $alert -match 'DiskSpaceRules\.Evaluate\(free, total, set\.LowDiskSpace\.ThresholdPercent\) == DiskLevel\.Critical' -and
     $rules -match 'DiskSpaceRules\.Evaluate\(freeBytes, totalBytes, criticalPercent\)' -and
     $diag -match 'lowDiskPercent\?\.Invoke\(\)' -and $api -match '\(\) => alertCenter\.LowDiskCriticalPercent\(\)') `
    -Severity Critical

$net = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\NetworkDiagnosticsService.cs'
$netModel = Get-MystTiqText $ctx 'src\MystTiq.Core\Models\NetworkDiagnosticModels.cs'
Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'a server stopped on purpose is NotRunning (appended to the enum), a crash stays an error' `
    ($netModel -match 'Error, NotRunning \}' -and $net -match 'NetworkHealthState\.NotRunning' -and $net -match 'runtime\.CrashDetected\|\|runtime\.Phase==ServerLifecyclePhase\.Crashed' -and
     (Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\NetworkDiagnosticDtos.cs') -match '5=>"NOT RUNNING"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'Restart Server on the Diagnostics ribbon needs a running server, and refreshes with the main Restart command' `
    ($vm -match 'RestartFromDiagnosticsCommand = new AsyncCommand\(RestartFromDiagnosticsAsync, \(\) => !IsBusy && ManagementApiConnected && ServerIsRunning\);' -and
     (([regex]::Matches($vm, 'RestartFromDiagnosticsCommand as AsyncCommand\)\?\.RaiseCanExecuteChanged')).Count -ge 4)) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'the retention boxes are wide enough for a two-digit value' `
    ($xaml -match 'BackupRetentionKeepLatest\}" Minimum="1" Maximum="1000" Width="140" FormatString="0"' -and $xaml -match 'BackupRetentionMaxAgeDays\}" Minimum="1" Maximum="3650" Width="150" FormatString="0"') `
    -Severity Critical

$upd = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessComponentUpdateService.cs'
Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'Update Center says when the install is newer than the latest published release' `
    ($upd -match 'if \(installedVersion > latestVersion\)' -and $upd -match 'is newer than the latest published release') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'the Automation page says when nothing is scheduled, and the hint follows the rule list' `
    ($xaml -match 'IsVisible="\{Binding HasNoAutomationRules\}"' -and $vm -match 'public bool HasNoAutomationRules => AutomationRules\.Count == 0;' -and
     $vm -match 'AutomationRules\.CollectionChanged \+= \(_, _\) => RaisePropertyChanged\(nameof\(HasNoAutomationRules\)\);') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.102.0 Contracts' 'the logic harness covers the sliders, alert episodes, disk rule, network state and version wording' `
    ($harness -match 'RunScenario\("Configuration sliders never rewrite' -and $harness -match 'RunScenario\("Configuration sliders still record' -and
     $harness -match 'RunScenario\("Alert episodes: a condition alerts once' -and $harness -match 'RunScenario\("Alert episodes: a flapping' -and $harness -match 'RunScenario\("Alert episodes survive a restart' -and
     $harness -match 'RunScenario\("Disk rule:' -and $harness -match 'RunScenarioAsync\("A stopped server is not a network error' -and $harness -match 'RunScenario\("Update Center says') `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.102.0-RouteSmoke.ps1' 'v0.7.102.0 Contracts' 'the alert episodes route smoke exists' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.102.0-deficiency-fixes.md' 'v0.7.102.0 Contracts' 'v0.7.102.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.102.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.102\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.102.0.md' 'Documentation' 'v0.7.102.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.102.0 entry' ($changelogText -match '## v0\.7\.102\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.101.0\MystTiqPalworldServer_v0.7.101.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.101.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.101.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.101.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.101.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.101.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.101.0 crash alerts route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.101.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'v0.7.102.0 Runtime' 'v0.7.102.0 alert episodes route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.102.0-RouteSmoke.ps1') -ProjectRoot $root
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
