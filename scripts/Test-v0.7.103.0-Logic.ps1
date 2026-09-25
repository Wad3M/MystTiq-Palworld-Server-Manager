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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.103.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.103\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.103.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.103\.0"' `
    'Versioning' 'app.manifest reports v0.7.103.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.102.0-Logic.ps1' 'Regression' 'v0.7.102.0 logic gate remains available' -Severity High

$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$api = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$dto = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\PalworldConfigurationDtos.cs'
$alert = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.102.0 fixes remain: slider echo guard, alert episodes, shared disk rule' `
    ($dto -match 'if \(IsCoercionEcho\(value, current\)\) return;' -and $alert -match 'episodes\.Observe\(ruleKey, active, DateTimeOffset\.UtcNow, flapGuard\)' -and
     (Test-Path (Join-Path $root 'src\MystTiq.Core\Services\DiskSpaceRules.cs'))) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.103.0 Contracts -- Doctor: scheduled backups and a one-click fix
# ---------------------------------------------------------------------------
$rules = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\DoctorHealthRules.cs'
$diag = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiagnosticsService.cs'
Add-MystTiqCheck $ctx 'v0.7.103.0 Contracts' 'the rule offers the fix only when no backup rule exists at all, so it can never duplicate one' `
    ($rules -match 'CanCreateRule: true' -and ([regex]::Matches($rules, 'CanCreateRule: false')).Count -ge 4 -and $rules -match 'to avoid a duplicate') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.103.0 Contracts' 'a schedule counts only if it runs at least weekly: an idle-only rule, a daily rule on no days and a slow interval do not' `
    ($rules -match 'AutomationTriggerKind\.DailyTime => rule\.Days != AutomationDayOfWeekMask\.None' -and $rules -match 'interval <= TimeSpan\.FromDays\(7\)' -and $rules -match '_ => false') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.103.0 Contracts' 'a rule whose every scheduled run last failed warns, but a missed or not-yet-run rule does not' `
    ($rules -match 'string\.Equals\(r\.LastRunState, "Failed", StringComparison\.OrdinalIgnoreCase\)' -and $rules -match 'failing\.Length == regular\.Length') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.103.0 Contracts' 'the diagnostics service adds the finding only when Automation is available, with the action only on request of the rule' `
    ($diag -match 'BuildScheduledBackupFindings\(\)' -and $diag -match 'ActionKind = "create-backup-rule"' -and $diag -match 'if \(automation is null\) return \[\];' -and
     $api -match 'alertCenter\.LowDiskCriticalPercent\(\), automation\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.103.0 Contracts' 'the fix needs the Admin role, is re-checked under a lock, and names what it created and where to change it' `
    ($diag -match 'canAdminister' -and $diag -match 'Creating an automation rule needs the Admin role' -and $diag -match 'lock \(backupRuleGate\)' -and
     $diag -match 'You can change or delete it in Automation' -and $diag -match 'so nothing was created' -and
     $api -match 'canAdminister: \(http\.Items\[RbacEndpointExtensions\.PrincipalItemKey\] as MystTiqPrincipal\)\?\.Role >= MystTiqRole\.Admin') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.103.0 Contracts' 'the created rule is a nightly 03:00 UTC backup on every day' `
    ($diag -match 'Nightly backup \(added by Doctor\)' -and $diag -match 'TimeOfDayUtc = new TimeOnly\(3, 0\)' -and $diag -match 'DaysOfWeek = AutomationDayOfWeekMask\.All' -and $diag -match 'AutomationActionKind\.CreateBackup') `
    -Severity Critical

$dtoFind = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\DiagnosticFindingDtos.cs'
Add-MystTiqCheck $ctx 'v0.7.103.0 Contracts' 'the Doctor button says what the fix does instead of a generic "Fix Automatically"' `
    ($dtoFind -match '"create-backup-rule" => "Create Nightly Backup Rule"' -and $dtoFind -match '"install-distribution" => "Install / Repair Server Files"' -and $xaml -match 'Content="\{Binding FixButtonText\}"' -and $xaml -notmatch 'Content="Fix Automatically"') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.103.0 Contracts' 'the logic harness covers every branch of the scheduled backups rule' `
    ($harness -match 'RunScenario\("Doctor scheduled backups: no rule offers' -and $harness -match 'RunScenario\("Doctor scheduled backups warn when the schedule exists but keeps failing') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.103.0 Contracts' 'the live smoke enumerates the rules explicitly (a nested JSON array would make its counts vacuous) and runs under strict mode' `
    ((Get-MystTiqText $ctx 'scripts\Test-v0.7.103.0-RouteSmoke.ps1') -match 'foreach \(\$rule in \$rules\) \{ \$rule \}' -and (Get-MystTiqText $ctx 'scripts\Test-v0.7.103.0-RouteSmoke.ps1') -match 'Set-StrictMode -Version 3\.0') `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.103.0-RouteSmoke.ps1' 'v0.7.103.0 Contracts' 'the backup schedule route smoke exists' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.103.0-doctor-scheduled-backups.md' 'v0.7.103.0 Contracts' 'v0.7.103.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.103.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.103\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.103.0.md' 'Documentation' 'v0.7.103.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.103.0 entry' ($changelogText -match '## v0\.7\.103\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.102.0\MystTiqPalworldServer_v0.7.102.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.102.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.102.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.102.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.102.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.102.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'v0.7.103.0 Runtime' 'v0.7.103.0 backup schedule route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.103.0-RouteSmoke.ps1') -ProjectRoot $root
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
