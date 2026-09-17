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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.82.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.82\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.82.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.82\.0"' `
    'Versioning' 'app.manifest reports v0.7.82.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.81.0-Logic.ps1' 'Regression' 'v0.7.81.0 logic gate remains available' -Severity High
Test-MystTiqFile $ctx 'scripts\Test-v0.7.81.0-RouteSmoke.ps1' 'Regression' 'v0.7.81.0 new-server wizard route smoke gate remains available' -Severity High

$vmTextRegression = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.81.0-era CloneIntoNewServerAsync remains present, untouched by this version' `
    ($vmTextRegression -match 'private async Task CloneIntoNewServerAsync\(\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.82.0 Contracts -- Update Center actionability
# ---------------------------------------------------------------------------
$componentServiceText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessComponentUpdateService.cs'
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'UpdatePipAsync exists and reuses RunProcessAsync with a longer timeout than the version-check default' `
    ($componentServiceText -match 'public async Task<ComponentUpdateResult> UpdatePipAsync\(CancellationToken cancellationToken\)' -and
     $componentServiceText -match 'RunProcessAsync\(python, "-m pip install --upgrade pip", cancellationToken, TimeSpan\.FromSeconds\(60\)\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'RunProcessAsync gained an optional timeout parameter without changing the default for existing callers' `
    ($componentServiceText -match 'private static async Task<\(int ExitCode, string Stdout, string Stderr\)> RunProcessAsync\(string fileName, string arguments, CancellationToken cancellationToken, TimeSpan\? timeout = null\)' -and
     $componentServiceText -match 'timeoutCts\.CancelAfter\(timeout \?\? ProcessTimeout\);') `
    -Severity Critical

$routesText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'pip update route is mapped and mutation-gated (POST, not GET)' `
    ($routesText -match 'MapPost\("/update-center/components/pip/update"') `
    -Severity Critical

$componentDtoText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\ComponentVersionDto.cs'
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'CanUpdateInPlace is scoped to pip only, not every UpdateAvailable row' `
    ($componentDtoText -match 'CanUpdateInPlace => Component == "pip" && Status == "UpdateAvailable"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'SourceUrl is keyed by Component (not just Source text) so two rows sharing the same Source label get different URLs' `
    ($componentDtoText -match '"Visual C\+\+ Runtime" =>' -and
     $componentDtoText -match '"Microsoft C\+\+ Build Tools" =>' -and
     (([regex]::Matches($componentDtoText, 'learn\.microsoft\.com|visualstudio\.microsoft\.com')).Count -ge 2)) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'ComponentUpdateResultDto exists on the client' `
    ($componentDtoText -match 'public sealed class ComponentUpdateResultDto') `
    -Severity Critical

$apiClientText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs'
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'client UpdatePipAsync posts to the pip update route' `
    ($apiClientText -match 'PostAsync\("/api/v1/update-center/components/pip/update", null, cancellationToken\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'UpdatePipCommand is wired in the ViewModel' `
    ($vmTextRegression -match 'UpdatePipCommand = new AsyncCommand\(UpdatePipAsync, \(\) => !IsBusy\);') `
    -Severity Critical

$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'both component tables have Update and Open row actions' `
    ((([regex]::Matches($mainWindowXaml, '\).UpdatePipCommand\}" IsVisible="\{Binding CanUpdateInPlace\}"')).Count -eq 2) -and
     (([regex]::Matches($mainWindowXaml, 'Click="OpenComponentSourceUrl_Click"')).Count -eq 2)) `
    -Severity Critical

$codeBehindText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'OpenComponentSourceUrl_Click reads the URL from the clicked rows own DataContext, not a page-level selected item' `
    ($codeBehindText -match 'private void OpenComponentSourceUrl_Click\(object\? sender, RoutedEventArgs e\)' -and
     $codeBehindText -match 'control\.DataContext is not ComponentVersionDto component') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.82.0 Contracts -- Server Setup table resize
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'ServerSetupTableMaxHeight is recomputed from window height, not a fixed constant' `
    ($vmTextRegression -match 'public double ServerSetupTableMaxHeight' -and
     $vmTextRegression -match 'public void UpdateServerSetupTableHeight\(double windowHeight\)' -and
     $vmTextRegression -match 'Math\.Clamp\(windowHeight - nonTableChromeHeight, minTableHeight, maxTableHeight\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'Window_OnSizeChanged calls UpdateServerSetupTableHeight' `
    ($codeBehindText -match 'vm\.UpdateServerSetupTableHeight\(e\.NewSize\.Height\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'the environment table binds MaxHeight instead of a hardcoded 405' `
    ($mainWindowXaml -match 'MaxHeight="\{Binding ServerSetupTableMaxHeight\}"' -and
     $mainWindowXaml -notmatch 'MaxHeight="405"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.82.0-update-center-actionability.md' `
    'v0.7.82.0 Contracts' 'v0.7.82.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4b. v0.7.82.0 Contracts -- bundled fixes found while testing (Alert Center crash,
#     Configuration reorder/dirty-highlighting, Dashboard status bug, Generate button bug)
# ---------------------------------------------------------------------------
$alertCenterText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'disk-space prediction cannot overflow AddDays (cap, finite checks, and a last-resort catch)' `
    ($alertCenterText -match 'private const double MaxProjectableDays = 36500;' -and
     $alertCenterText -match '!double\.IsFinite\(growthPerDay\) \|\| growthPerDay <= 0' -and
     $alertCenterText -match '!double\.IsFinite\(daysRemaining\) \|\| daysRemaining > MaxProjectableDays' -and
     $alertCenterText -match 'catch \(ArgumentOutOfRangeException\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'Configuration page: Server Identity renders before the Simple/Advanced toolbar (flattened StackPanel, not the old row-indexed Grid)' `
    ($mainWindowXaml -match 'flattened from a row-indexed Grid to a single StackPanel so Server[\s\S]{0,80}Identity can sit first' -and
     ($mainWindowXaml.IndexOf('Text="SERVER IDENTITY"')) -lt ($mainWindowXaml.IndexOf('Content="Simple Settings"'))) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'unsaved-change highlighting exists for Server Identity, Network rows, and World Settings sliders' `
    ((([regex]::Matches($mainWindowXaml, 'Classes\.dirty="\{Binding')).Count -ge 6)) `
    -Severity Critical

$paldtoText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\PalworldConfigurationDtos.cs'
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'PalworldSimpleToggleItem gained an IsDirty passthrough (was missing one, unlike its slider counterpart)' `
    ($paldtoText -match 'public bool IsDirty => Setting\.IsDirty;' -and
     (([regex]::Matches($paldtoText, 'public bool IsDirty => Setting\.IsDirty;')).Count -eq 2)) `
    -Severity Critical

$designSystemDirtyText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Styles\DesignSystem.axaml'
Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'dirty-state styles exist and are declared after the accentXxx block so the cascade tie resolves correctly' `
    ($designSystemDirtyText -match 'Border\.statuscard\.dirty' -and
     $designSystemDirtyText -match 'TextBox\.dirty' -and
     ($designSystemDirtyText.IndexOf('Border.statuscard.accentSystem')) -lt ($designSystemDirtyText.IndexOf('Border.statuscard.dirty'))) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'Dashboard shows Starting/Not Ready instead of falsely claiming Stopped when a process is detected but not yet ready' `
    ($vmTextRegression -match 'status\.NativeProcessId\.HasValue\s*\?\s*"Starting / Not Ready"\s*:\s*"Stopped / Not ready"' -and
     $vmTextRegression -match 'status\.NativeProcessId\.HasValue \? "STARTING" : "STOPPED"' -and
     $vmTextRegression -match 'DashboardHealthText is "DEGRADED" or "STARTING"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.82.0 Contracts' 'GenerateServerNameCommand and SaveCurrentAsPresetCommand are re-queried when IsBusy changes (the bug that left Generate stuck disabled)' `
    ($vmTextRegression -match '\(GenerateServerNameCommand as RelayCommand\)\?\.RaiseCanExecuteChanged\(\);' -and
     $vmTextRegression -match '\(SaveCurrentAsPresetCommand as RelayCommand\)\?\.RaiseCanExecuteChanged\(\);') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.82.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.82\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.82.0.md' 'Documentation' 'v0.7.82.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.82.0 entry' `
    ($changelogText -match '## v0\.7\.82\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.81.0\MystTiqPalworldServer_v0.7.81.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.81.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.81.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.81.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.81.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.81.0 checkpoint logic gate still passes' `
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
