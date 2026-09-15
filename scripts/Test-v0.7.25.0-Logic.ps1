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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.25.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.25\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.25.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.25\.0"' `
    'Versioning' 'app.manifest reports v0.7.25.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

$registryText = Get-Content (Join-Path $root 'docs\roadmap\WINDOWS_BACKPORT_REGISTRY.md') -Raw
Add-MystTiqCheck $ctx 'Regression' 'v0.7.24.0 watchdog registry correction is still present' `
    ($registryText -match 'Service-style watchdog/recovery behavior[\s\S]{0,200}Shipped[\s\S]{0,120}HeadlessFleetCrashRecoveryService') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.25.0 contract presence -- ribbon adaptivity
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Models\RibbonActionViewModel.cs' 'v0.7.25.0 Contracts' 'New ribbon model file is present' -Severity Critical

$ribbonModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Models\RibbonActionViewModel.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.25.0 Contracts' 'RibbonActionViewModel and RibbonGroupViewModel records are defined' `
    ($ribbonModelText -match 'record RibbonActionViewModel' -and $ribbonModelText -match 'record RibbonGroupViewModel') `
    -Severity Critical

$viewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.25.0 Contracts' 'VisibleRibbonGroups/OverflowRibbonGroups/HasOverflowRibbonGroups are exposed' `
    ($viewModelText -match 'ObservableCollection<RibbonGroupViewModel> VisibleRibbonGroups' -and
     $viewModelText -match 'ObservableCollection<RibbonGroupViewModel> OverflowRibbonGroups' -and
     $viewModelText -match 'bool HasOverflowRibbonGroups') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.25.0 Contracts' 'UpdateRibbonWidth/RecomputeRibbonLayout/RebuildRibbonGroups mirror the tab strip pattern' `
    ($viewModelText -match 'public void UpdateRibbonWidth' -and
     $viewModelText -match 'private void RecomputeRibbonLayout' -and
     $viewModelText -match 'private void RebuildRibbonGroups') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.25.0 Contracts' 'RaisePageVisibility rebuilds ribbon groups on every page change' `
    ($(
        $hookStart = $viewModelText.IndexOf('private void RaisePageVisibility()')
        if ($hookStart -lt 0) { $false }
        else { $viewModelText.Substring($hookStart, [Math]::Min(200, $viewModelText.Length - $hookStart)) -match 'RebuildRibbonGroups\(\);' }
    )) `
    -Severity Critical

$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
Add-MystTiqCheck $ctx 'v0.7.25.0 Contracts' 'Ribbon is data-bound to VisibleRibbonGroups with an overflow button' `
    ($mainWindowAxamlText -match 'ItemsSource="\{Binding VisibleRibbonGroups\}"' -and
     $mainWindowAxamlText -match 'RibbonOverflowButton' -and
     $mainWindowAxamlText -match 'IsVisible="\{Binding HasOverflowRibbonGroups\}"') `
    -Severity Critical

$codeBehindText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.25.0 Contracts' 'RibbonHost_OnSizeChanged and RibbonOverflowButton_OnClick handlers are present' `
    ($codeBehindText -match 'private void RibbonHost_OnSizeChanged' -and $codeBehindText -match 'private void RibbonOverflowButton_OnClick') `
    -Severity Critical

$designSystemText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\DesignSystem.axaml') -Raw
Add-MystTiqCheck $ctx 'v0.7.25.0 Contracts' 'Theme-safe ribbon icon color classes are defined, still routed through DynamicResource' `
    ($designSystemText -match 'Selector="TextBlock\.flatIcon\.amber"[\s\S]{0,120}DynamicResource AmberBrush' -and
     $designSystemText -match 'Selector="TextBlock\.flatIcon\.green"[\s\S]{0,120}DynamicResource GreenBrush') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.25.0-ribbon-adaptivity.md' 'v0.7.25.0 Contracts' 'v0.7.25.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.25.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.25\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.24.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.24.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.24.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.24.0\MystTiqPalworldServer_v0.7.24.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.24.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.24.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.24.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.24.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.24.0 checkpoint logic gate still passes' `
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

    # v0.7.25.0 is Desktop-only (ribbon adaptivity infrastructure, no new buttons/behavior), so every
    # server-side route/CLI smoke test carried forward from prior versions is expected to pass
    # unchanged -- regression evidence, not new ground. All need Build.ps1 DesktopWindows's sidecar
    # output, the same known quirk every version's -RunBuild already works around.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.12.0 whitelist enforcement harness (6 scenarios) still passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
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
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
