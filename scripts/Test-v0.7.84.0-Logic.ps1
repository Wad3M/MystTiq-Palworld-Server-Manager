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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.84.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.84\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.84.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.84\.0"' `
    'Versioning' 'app.manifest reports v0.7.84.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.83.0-Logic.ps1' 'Regression' 'v0.7.83.0 logic gate remains available' -Severity High

$vmText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$bootstrapperTextRegression = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\LocalManagementBootstrapper.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.83.0-era second-server provisioning remains present, untouched by this version' `
    ($vmText -match 'private async Task ContinueFromInstallDirectoryAsync\(\)' -and
     $bootstrapperTextRegression -match 'Task<LocalManagementBootstrapResult> RestartOwnedSidecarAsync') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.84.0 Contracts -- Fleet Clone World workflow
# ---------------------------------------------------------------------------
$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.84.0 Contracts' 'Clone World shows a labeled SOURCE/NEW PROFILE/PORT OFFSET workflow, not a flat unlabeled card' `
    ($mainWindowXaml -match 'Text="SOURCE \(the connection this tab currently uses\)"' -and
     $mainWindowXaml -match 'Text="NEW PROFILE"' -and
     $mainWindowXaml -match 'Text="PORT OFFSET"' -and
     $mainWindowXaml -match '\{Binding SelectedProfile\.BaseAddress\}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.84.0 Contracts' 'a successful clone surfaces a Restart Now button instead of leaving the restart requirement unactionable' `
    ($vmText -match 'public bool CloneWorldNeedsRestart' -and
     $vmText -match 'public ICommand RestartAfterCloneCommand \{ get; \}' -and
     $vmText -match 'private async Task RestartAfterCloneAsync\(\)' -and
     $mainWindowXaml -match 'Command="\{Binding RestartAfterCloneCommand\}" HorizontalAlignment="Left" IsVisible="\{Binding CloneWorldNeedsRestart\}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.84.0 Contracts' 'RestartAfterCloneAsync reuses the v0.7.83.0 RestartOwnedSidecarAsync mechanism and declines for a Windows Service install' `
    ($vmText -match 'var restartResult = await _localBootstrapper\.RestartOwnedSidecarAsync\(snapshot, CloneWorldNewProfileId\);' -and
     $vmText -match "This machine runs MystTiq as an installed Windows Service, which the desktop app can't restart on its own -- restart it manually, then refresh this page\.") `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.84.0 Contracts' 'CloneWorldNeedsRestart re-queries RestartAfterCloneCommand CanExecute (avoids the v0.7.82.0-class stuck-disabled bug)' `
    ($vmText -match '\(RestartAfterCloneCommand as AsyncCommand\)\?\.RaiseCanExecuteChanged\(\);') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.84.0-fleet-clone-world-workflow.md' `
    'v0.7.84.0 Contracts' 'v0.7.84.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.84.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.84\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.84.0.md' 'Documentation' 'v0.7.84.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.84.0 entry' `
    ($changelogText -match '## v0\.7\.84\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.83.0\MystTiqPalworldServer_v0.7.83.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.83.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.83.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.83.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.83.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.83.0 checkpoint logic gate still passes' `
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
