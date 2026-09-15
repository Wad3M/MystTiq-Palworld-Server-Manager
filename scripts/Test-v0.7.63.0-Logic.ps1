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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.63.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.63\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.63.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.63\.0"' `
    'Versioning' 'app.manifest reports v0.7.63.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.62.0-Logic.ps1' 'Regression' 'v0.7.62.0 logic gate remains available' -Severity High

$mainWindowFixText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
Add-MystTiqCheck $ctx 'Regression' 'v0.7.62.0 page-header MaxWidth fix is unchanged' `
    ($mainWindowFixText -match 'HorizontalAlignment="Right" MinWidth="460" MaxWidth="620"') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.63.0 Contracts -- the actual fix (status-color theme bypass)
# ---------------------------------------------------------------------------

# 3a. New shared converter exists and resolves resources live, not cached.
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Converters\SemanticStatusColorConverter.cs' `
    'v0.7.63.0 Contracts' 'SemanticStatusColorConverter.cs exists' -Severity Critical

$semanticConverterText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Converters\SemanticStatusColorConverter.cs'
Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'SemanticStatusColorConverter resolves via TryGetResource on every call (not a cached brush)' `
    ($semanticConverterText -match 'TryGetResource' -and $semanticConverterText -notmatch 'static readonly IBrush') `
    -Severity Critical

# 3b. ComponentStatusColorConverter no longer caches hardcoded hex brushes.
$componentConverterText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Converters\ComponentStatusColorConverter.cs'
Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'ComponentStatusColorConverter no longer hardcodes Color.Parse hex literals' `
    ($componentConverterText -notmatch 'Color\.Parse') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'ComponentStatusColorConverter delegates to SemanticStatusColorConverter' `
    ($componentConverterText -match 'SemanticStatusColorConverter\.Instance') `
    -Severity Critical

# 3c. TabSession: StatusDotColor -> StatusDotColorKey (semantic key, not hex).
$tabSessionText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\TabSession.cs'
Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'TabSession.StatusDotColorKey returns semantic keys, not hex literals' `
    ($tabSessionText -match 'StatusDotColorKey' -and $tabSessionText -notmatch '"#[0-9A-Fa-f]{6}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'TabSession.RefreshAccentVisual re-raises StatusDotColorKey after a theme switch' `
    ($tabSessionText -match 'RaisePropertyChanged\(nameof\(StatusDotColorKey\)\)') `
    -Severity Critical

# 3d. MainWindowViewModel: HealthStateColor -> HealthStateColorKey, refreshed on theme switch.
$mainViewModelText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'MainWindowViewModel.HealthStateColorKey returns semantic keys, not hex literals' `
    ($mainViewModelText -match 'HealthStateColorKey' -and $mainViewModelText -notmatch '"#[0-9A-Fa-f]{6}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'RefreshTabAccentVisuals re-raises HealthStateColorKey after a theme switch' `
    ($mainViewModelText -match '(?s)private void RefreshTabAccentVisuals\(\).*?RaisePropertyChanged\(nameof\(HealthStateColorKey\)\).*?\}') `
    -Severity Critical

# 3e. FleetDtos: StatusDotColor -> StatusDotColorKey.
$fleetDtosText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\FleetDtos.cs'
Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'ServerProfileSummaryDto.StatusDotColorKey returns semantic keys, not hex literals' `
    ($fleetDtosText -match 'StatusDotColorKey' -and $fleetDtosText -notmatch '"#[0-9A-Fa-f]{6}"') `
    -Severity Critical

# 3f. MainWindow.axaml bindings route through the converter.
Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'MainWindow.axaml has zero remaining direct bindings to the old hex-string properties' `
    ($mainWindowFixText -notmatch '\{Binding StatusDotColor\}' -and $mainWindowFixText -notmatch '\{Binding HealthStateColor\}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'MainWindow.axaml routes status-color bindings through SemanticStatusColorConverter' `
    (([regex]::Matches($mainWindowFixText, 'Converter=\{x:Static converters:SemanticStatusColorConverter\.Instance\}')).Count -ge 4) `
    -Severity Critical

# 3g. DesignSystem.axaml gained the missing bare Border.statuscard style.
$designSystemText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Styles\DesignSystem.axaml'
Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'DesignSystem.axaml now declares a themed bare Border.statuscard style' `
    ($designSystemText -match '<Style Selector="Border\.statuscard">') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'DesignSystem.axaml now declares a themed Button:focus-visible style' `
    ($designSystemText -match '<Style Selector="Button:focus-visible">\s*<Setter Property="BorderBrush" Value="\{DynamicResource') `
    -Severity Critical

# 3h. App.axaml's dead legacy parallel theme system is gone (regression guard against
#     reintroducing the exact shadowed-style trap that caused the Border.statuscard bug).
$appAxamlText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\App.axaml'
Add-MystTiqCheck $ctx 'v0.7.63.0 Contracts' 'App.axaml no longer defines the legacy MystTiq* resource/style block' `
    ($appAxamlText -notmatch 'MystTiqBackground' -and $appAxamlText -notmatch 'MystTiqButtonMetal' -and $appAxamlText -notmatch 'Button\.navitem' -and $appAxamlText -notmatch 'Expander\.navgroup') `
    -Severity Critical

Test-MystTiqTextMatch $ctx 'src\MystTiq.Desktop\App.axaml' `
    '#[0-9A-Fa-f]{6,8}' `
    'v0.7.63.0 Contracts' 'App.axaml contains zero hardcoded hex color literals (regression guard: the whole point of this file is to no longer own any color)' `
    -Not -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.63.0-central-theme-system-audit-and-fix.md' `
    'v0.7.63.0 Contracts' 'v0.7.63.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.63.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.63\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.63.0.md' 'Documentation' 'v0.7.63.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.63.0 entry' `
    ($changelogText -match '## v0\.7\.63\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.62.0\MystTiqPalworldServer_v0.7.62.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.62.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.62.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.62.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.62.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.62.0 checkpoint logic gate still passes' `
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
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
