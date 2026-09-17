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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.89.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.89\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.89.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.89\.0"' `
    'Versioning' 'app.manifest reports v0.7.89.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.88.0-Logic.ps1' 'Regression' 'v0.7.88.0 logic gate remains available' -Severity High

$vmText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.88.0-era IsProcessLive dashboard fix remains present, untouched by this version' `
    ($vmText -match 'ServerIsRunning = status\.IsProcessLive \|\| status\.Ready;') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.89.0 Contracts -- seven live bug fixes
# ---------------------------------------------------------------------------

# Bug 1+2: loopback-address checks replace "Id == default" proxy
Add-MystTiqCheck $ctx 'v0.7.89.0 Contracts' 'EnsureManagementConnectionForLifecycleAsync checks the actual loopback address, not a literal default-Id comparison' `
    ($vmText -match 'var isLoopbackProfile = SelectedProfile is not null &&\s*\r?\n\s*System\.Net\.IPAddress\.TryParse\(SelectedProfile\.BaseAddress\.Host, out var loopbackCandidate\) &&\s*\r?\n\s*System\.Net\.IPAddress\.IsLoopback\(loopbackCandidate\);' -and
     $vmText -match 'if \(!isLoopbackProfile\)') `
    -Severity Critical

$tabSessionText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\TabSession.cs'
Add-MystTiqCheck $ctx 'v0.7.89.0 Contracts' 'ConnectionKindText checks the actual loopback address, not a literal default-Id comparison' `
    ($tabSessionText -match 'System\.Net\.IPAddress\.TryParse\(Profile\.BaseAddress\.Host, out var address\) && System\.Net\.IPAddress\.IsLoopback\(address\)') `
    -Severity Critical

# Bug 3: stale world data cleared
Add-MystTiqCheck $ctx 'v0.7.89.0 Contracts' 'ApplyDashboardSupport clears all six Active World fields when the world is not available, not just the clock text' `
    ($vmText -match 'ActiveWorldIdText = "Not resolved";\s*\r?\n\s*DashboardWorldNicknameText = string\.Empty;\s*\r?\n\s*DashboardWorldText = "Unavailable";\s*\r?\n\s*WorldPlayerSaveCountText = "0";\s*\r?\n\s*WorldSizeText = "0\.00 MB";\s*\r?\n\s*DashboardWorldPulseText = "World telemetry unavailable";') `
    -Severity Critical

# Bug 4: OVERALL PROGRESS relabeled
$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.89.0 Contracts' 'the misleading "OVERALL PROGRESS" label was renamed to "OPERATION PROGRESS"' `
    ($mainWindowXaml -match 'Text="OPERATION PROGRESS"' -and $mainWindowXaml -notmatch 'Text="OVERALL PROGRESS"') `
    -Severity Critical

# Bug 5: wizard install auto-refresh + output visibility
Add-MystTiqCheck $ctx 'v0.7.89.0 Contracts' 'InstallPalworldServerFromWizardAsync chains the install with an environment refresh' `
    ($vmText -match 'private async Task InstallPalworldServerFromWizardAsync\(\)\s*\r?\n\s*\{\s*\r?\n\s*await UpdatePalworldServerAsync\(\);\s*\r?\n\s*await RefreshEnvironmentAsync\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.89.0 Contracts' 'the wizard Install button is wired to the auto-refreshing command, and shows SteamCMD output' `
    ($mainWindowXaml -match 'Content="Install Palworld Dedicated Server" Command="\{Binding InstallPalworldServerFromWizardCommand\}"' -and
     $mainWindowXaml -match 'Text="\{Binding DistributionOutputText\}" IsVisible="\{Binding HasDistributionOutput\}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.89.0 Contracts' 'HasDistributionOutput raises change notifications when DistributionOutputText changes' `
    ($vmText -match 'private set \{ if \(SetField\(ref _distributionOutputText, value\)\) RaisePropertyChanged\(nameof\(HasDistributionOutput\)\); \}') `
    -Severity High

# Bug 6: MODs auto-scan
Add-MystTiqCheck $ctx 'v0.7.89.0 Contracts' 'FinishInstallAndAdvanceAsync auto-triggers a Workshop scan on arrival at the MODs step' `
    ($vmText -match 'if \(IsWizardStepMods && WorkshopItems\.Count == 0\)\s*\r?\n\s*_ = ScanWorkshopModsAsync\(\);') `
    -Severity Critical

# Bug 7: MOD ZIP install button
Add-MystTiqCheck $ctx 'v0.7.89.0 Contracts' 'the MODs step has a distinctly-colored local MOD ZIP install button reusing the existing install path' `
    ($mainWindowXaml -match 'Classes="success" Content="📦 Install MOD ZIP…" Click="InstallModZip_Click"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.89.0-second-server-lifecycle-and-wizard-polish.md' `
    'v0.7.89.0 Contracts' 'v0.7.89.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.89.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.89\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.89.0.md' 'Documentation' 'v0.7.89.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.89.0 entry' `
    ($changelogText -match '## v0\.7\.89\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.88.0\MystTiqPalworldServer_v0.7.88.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.88.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.88.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.88.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.88.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.88.0 checkpoint logic gate still passes' `
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
