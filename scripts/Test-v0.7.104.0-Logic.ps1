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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.104.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.104\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.104.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.104\.0"' `
    'Versioning' 'app.manifest reports v0.7.104.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.103.0-Logic.ps1' 'Regression' 'v0.7.103.0 logic gate remains available' -Severity High

$dto = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\PalworldConfigurationDtos.cs'
$diag = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiagnosticsService.cs'
$rules = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\DoctorHealthRules.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.103.0 fixes remain: slider echo guard and the Doctor scheduled-backups fix' `
    ($dto -match 'if \(IsCoercionEcho\(value, current\)\) return;' -and $diag -match 'ActionKind = "create-backup-rule"' -and
     $rules -match 'ScheduledBackups\(IReadOnlyList<BackupScheduleRule> rules\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.104.0 Contracts -- Alert Center: unpin a pinned alert on recovery
# ---------------------------------------------------------------------------
$episodes = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\AlertEpisodes.cs'
$notif = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessNotificationService.cs'
$alert = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'

Add-MystTiqCheck $ctx 'v0.7.104.0 Contracts' 'the episode tracker remembers the alert''s own notification id and hands it back only when the episode actually ends' `
    ($episodes -match 'public void SetAlertNotificationId\(string ruleKey, string notificationId\)' -and
     $episodes -match 'out string\? recoveredNotificationId' -and
     $episodes -match 'recoveredNotificationId = state\.AlertNotificationId;' -and
     $episodes -match 'state\.AlertNotificationId = null;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.104.0 Contracts' 'the original 4-argument Observe still exists so every existing call site and harness scenario is unaffected' `
    ($episodes -match 'public EpisodeAction Observe\(string ruleKey, bool active, DateTimeOffset now, TimeSpan flapGuard\)\s*\r?\n\s*=> Observe\(ruleKey, active, now, flapGuard, out _\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.104.0 Contracts' 'switching a rule off also hands back the pinned alert''s id, since that path sends no Resolved notice at all' `
    ($episodes -match 'public string\? Close\(string ruleKey\)' -and
     $episodes -match 'var notificationId = state\.AlertNotificationId;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.104.0 Contracts' 'notification creation can hand back the new id without a second lookup, and the old call sites are untouched' `
    ($notif -match 'public HeadlessNotificationSnapshot Create\(string severity, string title, string message, bool pinned, out string id\)' -and
     $notif -match 'public HeadlessNotificationSnapshot Create\(string severity, string title, string message, bool pinned = false\)\s*\r?\n\s*=> Create\(severity, title, message, pinned, out _\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.104.0 Contracts' 'the Alert Center records the alert''s id on Alert, unpins on Recovered, and unpins on every disabled-rule Close path' `
    ($alert -match 'episodes\.SetAlertNotificationId\(ruleKey, id\)' -and
     $alert -match 'case EpisodeAction\.Recovered:' -and
     $alert -match 'Unpin\(recoveredNotificationId\)' -and
     ([regex]::Matches($alert, 'Unpin\(episodes\.Close\(')).Count -ge 5) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.104.0 Contracts' 'unpinning swallows a notification that was already dismissed instead of failing the evaluation tick' `
    ($alert -match 'private void Unpin\(string\? notificationId\)' -and $alert -match 'catch \(KeyNotFoundException\) \{ \}') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.104.0 Contracts' 'the logic harness covers the notification-id handoff on both the recovery and the switched-off paths' `
    ($harness -match 'RunScenario\("Alert episodes hand back the alert''s notification id when the episode ends' -and
     $harness -match 'RunScenario\("Notification create hands back the new id, and unpinning a missing or already-unpinned one is a no-op') `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.104.0-RouteSmoke.ps1' 'v0.7.104.0 Contracts' 'the alert unpin route smoke exists' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.104.0-alert-unpin-on-recovery.md' 'v0.7.104.0 Contracts' 'v0.7.104.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.104.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.104\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.104.0.md' 'Documentation' 'v0.7.104.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.104.0 entry' ($changelogText -match '## v0\.7\.104\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.103.0\MystTiqPalworldServer_v0.7.103.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.103.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.103.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.103.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.103.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.103.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'v0.7.104.0 Runtime' 'v0.7.104.0 alert unpin route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.104.0-RouteSmoke.ps1') -ProjectRoot $root
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
