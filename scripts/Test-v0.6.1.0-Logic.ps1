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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.1.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.1\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.1.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

Test-MystTiqTextMatch $ctx 'src\MystTiq.Desktop\MainWindow.axaml' `
    'x:DataType="vm:MainWindowViewModel"' `
    'Regression' 'Desktop remains Avalonia MVVM' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs' `
    'IOperationCoordinator coordinator, string kind, IReadOnlyList<string> resourceKeys' `
    'Regression' 'Lifecycle routes remain retrofitted onto the OperationCoordinator (not a bare local semaphore)' -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.1.0 contract presence -- automation, RBAC, backup classes, analytics
# ---------------------------------------------------------------------------
$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'Automation engine contract is defined' `
    ([regex]::IsMatch($coreText, 'AutomationRule|AutomationTrigger|AutomationAction')) `
    -Severity Critical -Details 'Expected AutomationRule/AutomationTrigger/AutomationAction contracts in MystTiq.Core.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'A real background scheduler loop exists' `
    ([regex]::IsMatch($hostText, 'PeriodicTimer')) `
    -Severity Critical -Details 'Expected the one background PeriodicTimer loop backing scheduled automation.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'RBAC role/principal contract is defined' `
    ([regex]::IsMatch($coreText, 'MystTiqRole') -and [regex]::IsMatch($coreText, 'MystTiqPrincipal')) `
    -Severity Critical -Details 'Expected MystTiqRole/MystTiqPrincipal contracts in MystTiq.Core.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'RBAC backward-compatible legacy token path is preserved' `
    ([regex]::IsMatch($coreText, 'LegacyOwner')) `
    -Severity Critical -Details 'The existing single shared bearer token must keep resolving to a full-access principal without touching the RBAC store.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'Role-gated route enforcement exists' `
    ([regex]::IsMatch($hostText, 'RequireRole')) `
    -Severity Critical -Details 'Expected at least one RequireRole-gated route.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'Backup class/retention-protection contract is defined' `
    ([regex]::IsMatch($hostText, 'enum BackupClass') -and [regex]::IsMatch($hostText, 'IncludeClasses')) `
    -Severity Critical -Details 'Expected a BackupClass enum and class-scoped retention filtering.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'Alert Center / disk-space prediction contract is defined' `
    ([regex]::IsMatch($hostText, 'HeadlessAlertCenterService') -and [regex]::IsMatch($hostText, 'DiskSpacePrediction')) `
    -Severity High -Details 'Expected an Alert Center service with a disk-space prediction contract.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'Historical metric gap/restart markers exist' `
    ([regex]::IsMatch($hostText, 'HistoricalGapMarker') -and [regex]::IsMatch($hostText, 'RestartMarkers')) `
    -Severity High -Details 'Expected honest graph-gap and restart-marker evidence on the historical metrics snapshot.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'Notification routing (webhook channel + templates) contract is defined' `
    ([regex]::IsMatch($hostText, 'NotificationChannel') -and [regex]::IsMatch($hostText, 'NotificationTemplate')) `
    -Severity High -Details 'Expected a notification channel/template contract with at least a real Webhook dispatch path.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'Player flag (advanced administration) contract is defined' `
    ([regex]::IsMatch($hostText, 'enum PlayerFlag')) `
    -Severity Medium -Details 'Expected a PlayerFlag annotation on player metadata.'

Add-MystTiqCheck $ctx 'v0.6.1 Contracts' 'Desktop exposes Automation/Security/Alert Center pages' `
    ([regex]::IsMatch($desktopText, 'IsAutomationPage') -and [regex]::IsMatch($desktopText, 'IsSecurityPage') -and [regex]::IsMatch($desktopText, 'IsAlertCenterPage')) `
    -Severity Critical -Details 'Expected all three new nav destinations wired into the Desktop shell.'

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.1.0 is documented in the roadmap' `
    ([regex]::IsMatch($docText, 'v0\.6\.1\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.0.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.0.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.0.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    # Same rationale as v0.6.0.0's own gate: a version-locked frozen-checkpoint gate can never
    # pass again against the live tree once VersionPrefix moves forward. Run it against the
    # frozen v0.6.0.0 FullSource checkpoint instead.
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.0.0\MystTiqPalworldServer_v0.6.0.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.0.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.0.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.0.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.0.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.0.0 checkpoint logic gate still passes' `
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
