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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.64.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.64\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.64.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.64\.0"' `
    'Versioning' 'app.manifest reports v0.7.64.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.63.0-Logic.ps1' 'Regression' 'v0.7.63.0 logic gate remains available' -Severity High

$appAxamlRegressionText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\App.axaml') -Raw
Add-MystTiqCheck $ctx 'Regression' 'v0.7.63.0 App.axaml zero-hex-literal regression guard still holds' `
    (-not [regex]::IsMatch($appAxamlRegressionText, '#[0-9A-Fa-f]{6,8}')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.64.0 Contracts -- the actual fixes (automation validation, log rotation, stale text)
# ---------------------------------------------------------------------------

# 3a. Automation validation.
$automationServiceText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAutomationService.cs'
Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'ValidateTriggerAndAction exists and checks IdleThresholdMinutes, JitterSeconds, Interval and WarningCountdownSecondsBeforeAction' `
    ($automationServiceText -match 'public static void ValidateTriggerAndAction' -and
     $automationServiceText -match 'JitterSeconds < 0' -and
     $automationServiceText -match 'interval <= TimeSpan\.Zero' -and
     $automationServiceText -match 'idleMinutes < 1' -and
     $automationServiceText -match 'seconds < 0') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'CreateRule and UpdateRule both call ValidateTriggerAndAction' `
    (([regex]::Matches($automationServiceText, 'ValidateTriggerAndAction\(trigger, action\);')).Count -ge 2) `
    -Severity Critical

$localApiHostText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'POST and PUT /automation/rules routes catch ArgumentException and return BadRequest' `
    (([regex]::Matches($localApiHostText, 'catch \(ArgumentException ex\) \{ return Results\.BadRequest\(new \{ error = ex\.Message \}\); \}')).Count -ge 2) `
    -Severity Critical

# 3b. Console log rotation.
Test-MystTiqFile $ctx 'src\MystTiq.Core\Services\ConsoleLogRotation.cs' `
    'v0.7.64.0 Contracts' 'ConsoleLogRotation.cs exists' -Severity Critical

$rotationText = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ConsoleLogRotation.cs'
Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'ConsoleLogRotation exposes MaxBytes and RotateIfNeeded' `
    ($rotationText -match 'public const long MaxBytes' -and $rotationText -match 'public static void RotateIfNeeded') `
    -Severity Critical

$consoleWriterText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessConsoleLogWriter.cs'
Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'HeadlessConsoleLogWriter calls ConsoleLogRotation.RotateIfNeeded before appending' `
    ($consoleWriterText -match 'ConsoleLogRotation\.RotateIfNeeded\(logPath\)') `
    -Severity Critical

$lifecycleServiceText = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsServerLifecycleService.cs'
Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'WindowsServerLifecycleService.AppendConsoleLine calls ConsoleLogRotation.RotateIfNeeded before appending' `
    ($lifecycleServiceText -match 'ConsoleLogRotation\.RotateIfNeeded\(path\)') `
    -Severity Critical

# 3c. Stale-text fixes.
$mainWindowText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'World Transactions page no longer claims Base ownership/recovery repair is BACKEND REQUIRED' `
    ($mainWindowText -notmatch 'Base ownership/Palbox binary repair remains BACKEND REQUIRED') `
    -Severity Critical

$checklistServiceText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessEnvironmentChecklistService.cs'
Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'UE4SS Runtime checklist row no longer claims install is unscheduled, and ActionSupported is unconditionally true' `
    ($checklistServiceText -notmatch 'UE4SS installation is scheduled for the MOD/UE4SS restoration phase' -and
     $checklistServiceText -match 'ue4ss\.Status == "MISSING" \? "INSTALL" : "MANAGE", true,') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'Backup Storage checklist row note points to Doctor Fix Automatically instead of claiming no capability exists' `
    ($checklistServiceText -notmatch 'must be performed by a server-side storage operation' -and
     $checklistServiceText -match "Use Server Doctor's Fix Automatically to create it\.") `
    -Severity Critical

# 3d. New test coverage itself is present.
$harnessText = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.64.0 Contracts' 'LogicHarness includes the new ValidateTriggerAndAction and ConsoleLogRotation scenarios' `
    ($harnessText -match 'ValidateTriggerAndAction rejects a negative IdleThresholdMinutes' -and
     $harnessText -match 'ConsoleLogRotation rotates a file at/over MaxBytes') `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.64.0-RouteSmoke.ps1' `
    'v0.7.64.0 Contracts' 'v0.7.64.0 route smoke script exists' -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.64.0-roadmap-wide-gap-audit-and-fixes.md' `
    'v0.7.64.0 Contracts' 'v0.7.64.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.64.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.64\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.64.0.md' 'Documentation' 'v0.7.64.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.64.0 entry' `
    ($changelogText -match '## v0\.7\.64\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.63.0\MystTiqPalworldServer_v0.7.63.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.63.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.63.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.63.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.63.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.63.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'New Tests' 'v0.7.64.0 route smoke gate passes' {
        & (Join-Path $root 'scripts\Test-v0.7.64.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'New Tests' 'MystTiq.LogicHarness (17 scenarios, incl. v0.7.64.0 additions) passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
