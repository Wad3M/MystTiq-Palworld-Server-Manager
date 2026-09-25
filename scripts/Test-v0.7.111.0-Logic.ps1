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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.111.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.111\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.111.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.111\.0"' `
    'Versioning' 'app.manifest reports v0.7.111.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.110.0-Logic.ps1' 'Regression' 'v0.7.110.0 logic gate remains available' -Severity High

$fleetRecovery = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessFleetCrashRecoveryService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.110.0 fixes remain: crash recovery still watches after a give-up and resumes' `
    ($fleetRecovery -match 'private async Task RunWithGiveUpWatchAsync\(CancellationToken token\)' -and
     $fleetRecovery -match 'await NotifyManualRecoveryAsync\(token\);') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.111.0 Contracts -- Alert mute and per-profile crash alert settings
# ---------------------------------------------------------------------------
$alertCenter = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'
$crashAlerts = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\CrashAlerts.cs'
$host_ = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$dtos = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\AlertCenterDtos.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'

Add-MystTiqCheck $ctx 'v0.7.111.0 Contracts' 'the rule set carries crash alert settings (default on) and a mute stored as an end time' `
    ($alertCenter -match 'public CrashAlertRule CrashAlerts \{ get; init; \} = new\(true, true\);' -and
     $alertCenter -match 'public DateTimeOffset\? MutedUntilUtc \{ get; init; \}' -and
     $alertCenter -match 'public sealed record CrashAlertRule\(bool Enabled, bool RecoveryNotices\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.111.0 Contracts' 'the mute is capped at 30 days, 0 unmutes, and threshold evaluation is skipped entirely while muted' `
    ($alertCenter -match 'public const int MaximumMuteMinutes = 43200;' -and
     $alertCenter -match 'minutes <= 0 \? null' -and
     $alertCenter -match 'if \(AlertMutePolicy\.IsMuted\(set, now\)\) return;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.111.0 Contracts' 'the crash observer reads this profile''s rules per event, logs a skipped alert to Activity, and still unpins a DOWN notice when the back-up notice is not sent' `
    ($crashAlerts -match 'AlertMutePolicy\.ShouldSendCrashAlert\(rules\(\), supervisorEvent\.Kind, DateTimeOffset\.UtcNow\)' -and
     $crashAlerts -match 'Crash alert not sent \(muted or switched off\)' -and
     $crashAlerts -match 'if \(send\) notifications\.Create\(alert\.Severity, alert\.Title, alert\.Message, alert\.Pinned\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.111.0 Contracts' 'each profile''s crash observer is wired to that profile''s own alert center, and the mute route is Admin-only' `
    ($host_ -match 'alertCenter\.GetRules, activity\);' -and
     $host_ -match 'routes\.MapPost\("/alerts/mute", \(AlertMuteRequest request\) => Results\.Ok\(p\.AlertCenter\.Mute\(request\.Minutes\)\)\)\.RequireRole\(MystTiqRole\.Admin, p\.Id\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.111.0 Contracts' 'the Desktop DTO round-trips the new fields so Save Rules cannot reset them, and the Alert Center shows crash alert and mute controls' `
    ($dtos -match 'public CrashAlertRuleDto CrashAlerts \{ get; set; \} = new\(\);' -and
     $dtos -match 'public DateTimeOffset\? MutedUntilUtc \{ get; set; \}' -and
     $xaml -match 'IsChecked="\{Binding AlertRules\.CrashAlerts\.Enabled\}"' -and
     $xaml -match 'Command="\{Binding MuteAlertsCommand\}" CommandParameter="0"') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.111.0 Contracts' 'the logic harness covers the mute policy, the observer under a mute, and per-profile persistence' `
    ($harness -match 'RunScenario\("Alert mute policy: a mute ends on its own' -and
     $harness -match 'RunScenarioAsync\("Crash alert observer honours this profile''s mute and settings' -and
     $harness -match 'muting one profile does not mute another') `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.111.0-RouteSmoke.ps1' 'v0.7.111.0 Contracts' 'the alert mute route smoke exists' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.111.0-alert-mute-and-crash-alert-settings.md' 'v0.7.111.0 Contracts' 'v0.7.111.0 architecture doc is present' -Severity Critical
# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.111.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.111\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.111.0.md' 'Documentation' 'v0.7.111.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.111.0 entry' ($changelogText -match '## v0\.7\.111\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.110.0\MystTiqPalworldServer_v0.7.110.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.110.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.110.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.110.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.110.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.110.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'v0.7.111.0 Runtime' 'v0.7.111.0 alert mute and crash alert settings route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.111.0-RouteSmoke.ps1') -ProjectRoot $root
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
