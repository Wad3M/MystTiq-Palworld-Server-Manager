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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.3.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.3\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.3.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.2.0 fleet contract is still present' `
    ([regex]::IsMatch($coreText, 'IReadOnlyList<HeadlessServerProfileConfiguration>\s+Servers') -and [regex]::IsMatch($hostText, 'class ServerProfileHost')) `
    -Severity Critical -Details 'Expected plural Servers config and ServerProfileHost to remain intact.'

Add-MystTiqCheck $ctx 'Regression' 'RaisePageVisibility still notifies IsAutomationPage (the v0.6.1.0 blank-page regression must not resurface)' `
    ([regex]::IsMatch($desktopText, 'RaisePropertyChanged\(nameof\(IsAutomationPage\)\)')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.3.0 contract presence
# ---------------------------------------------------------------------------

# -- Windows service hardening --
Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Supervisor loop is platform-neutral (renamed from LinuxHeadlessSupervisor)' `
    ([regex]::IsMatch($coreText, 'class HeadlessSupervisor\b') -and -not [regex]::IsMatch($coreText, 'class LinuxHeadlessSupervisor\b') -and [regex]::IsMatch($coreText, 'record HeadlessSupervisorOptions')) `
    -Severity Critical -Details 'Expected HeadlessSupervisor/HeadlessSupervisorOptions (platform-neutral), with the old Linux-only names gone.'

Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Windows branch exists in the service-* CLI command block' `
    ([regex]::IsMatch($hostText, 'WindowsServiceRunLifecycleFactory') -and [regex]::IsMatch($hostText, 'new WindowsServiceManager\(\)')) `
    -Severity Critical -Details 'Expected Program.cs to construct WindowsServiceManager and a Windows lifecycle factory for service-status/install/uninstall/run.'

Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Windows service-run participates in the SCM lifecycle via AddWindowsService' `
    ([regex]::IsMatch($hostText, 'AddWindowsService')) `
    -Severity Critical -Details 'Expected the Windows service-run branch to register with the Service Control Manager so sc.exe stop shuts down gracefully instead of hanging/force-killing.'

Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Real Windows SCM-backed service status provider exists' `
    ([regex]::IsMatch($hostText, 'class WindowsSystemServiceStatusProvider')) `
    -Severity Critical -Details 'Expected a real IManagementServiceStatusProvider backed by WindowsServiceManager, distinct from the hardcoded WindowsStandaloneManagementServiceStatusProvider stub.'

# -- Character/account migration --
Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Player identity mapping engine exists in Core' `
    ([regex]::IsMatch($coreText, 'namespace MystTiq\.Core\.Migration') -and [regex]::IsMatch($coreText, 'class PlayerMappingEngine') -and [regex]::IsMatch($coreText, 'enum PlayerMappingMethod')) `
    -Severity Critical -Details 'Expected MystTiq.Core.Migration.PlayerMappingEngine with the ported identity-matching heuristics.'

Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Character migration service follows the guild-ownership Preview/Apply/Journal pattern' `
    ([regex]::IsMatch($hostText, 'class HeadlessCharacterMigrationService') -and [regex]::IsMatch($hostText, 'PreviewAsync') -and [regex]::IsMatch($hostText, '"character-migration"')) `
    -Severity Critical -Details 'Expected HeadlessCharacterMigrationService reusing the world-transactions journal store with mode "character-migration".'

Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Source-character disposition (Keep/Archive/Delete, Reset explicitly deferred) is defined' `
    ([regex]::IsMatch($hostText, 'enum CharacterDisposition') -and [regex]::IsMatch($hostText, 'DisposeSourceCharacterAsync') -and [regex]::IsMatch($hostText, 'not yet supported')) `
    -Severity Critical -Details 'Expected CharacterDisposition {Keep,Archive,Delete,Reset} with Reset explicitly rejected (not silently no-op''d or guessed).'

Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Character migration routes are registered per server profile' `
    ([regex]::IsMatch($hostText, '"/players/migration/preview"') -and [regex]::IsMatch($hostText, '"/players/migration/apply"') -and [regex]::IsMatch($hostText, '/players/migration/\{sourcePlayerId\}/disposition')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Desktop exposes character migration UI on the Players page' `
    ([regex]::IsMatch($desktopText, 'MigrationSourcePlayer') -and [regex]::IsMatch($desktopText, 'PreviewCharacterMigrationCommand') -and [regex]::IsMatch($desktopText, 'ApplyCharacterMigrationCommand')) `
    -Severity Critical

# -- Server Setup vs. Update Center cleanup --
Add-MystTiqCheck $ctx 'v0.6.3 Contracts' 'Setup no longer silently mutates an already-installed component' `
    ([regex]::IsMatch($desktopText, 'item\.IsMissing') -and [regex]::IsMatch($desktopText, 'stillMissing')) `
    -Severity Critical -Details 'Expected RunEnvironmentAction/InstallMissingEnvironmentAsync to check IsMissing before calling the mutating update route, navigating to Update Center instead when already installed.'

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.3.0 is documented in the roadmap' `
    ([regex]::IsMatch($docText, 'v0\.6\.3\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.2.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.2.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.2.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.2.0\MystTiqPalworldServer_v0.6.2.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.2.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.2.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.2.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.2.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.2.0 checkpoint logic gate still passes' `
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
