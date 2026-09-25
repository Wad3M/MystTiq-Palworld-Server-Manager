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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.99.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.99\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.99.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.99\.0"' `
    'Versioning' 'app.manifest reports v0.7.99.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.98.0-Logic.ps1' 'Regression' 'v0.7.98.0 logic gate remains available' -Severity High

$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.96.0 calibrated map behaviour remains (on by default, first-run Palpagos, bases applied from the Players read)' `
    ($vm -match 'private bool _useCalibratedWorldPositions = true;' -and $vm -match 'HasSavedPreference' -and $vm -match 'private void ApplyBaseLocations\(PlayerGuildSnapshotDto snapshot\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.7.97.0 and v0.7.98.0 pages remain present (crash detail pane, Doctor rules)' `
    ($xaml -match 'x:DataType="models:CrashFindingDto"' -and (Test-Path (Join-Path $root 'src\MystTiq.HeadlessHost\DoctorHealthRules.cs'))) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.99.0 Contracts -- dedicated Map page under World
# ---------------------------------------------------------------------------
$nav = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\NavigationPage.cs'
Add-MystTiqCheck $ctx 'v0.7.99.0 Contracts' 'Map is appended to the end of the NavigationPage enum, not inserted, so a page saved by number keeps its meaning' `
    ($nav -match 'Fleet,[\s\S]*Map\s*\}' -and $nav -notmatch 'Guilds,\s*Map') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.99.0 Contracts' 'the ViewModel treats Map as a World page: page flag, group, category, refresh on open, refresh on tab switch, ribbon' `
    ($vm -match 'public bool IsMapPage => SelectedPage == NavigationPage\.Map;' -and
     ([regex]::Matches($vm, 'NavigationPage\.Guilds or NavigationPage\.Map;')).Count -ge 2 -and
     $vm -match 'case NavigationPage\.Map:\s*//[^\n]*\s*await RefreshPlayersPageAsync\(\);' -and
     $vm -match 'SelectedPage is NavigationPage\.Players or NavigationPage\.Map\) _ = RefreshPlayersPageAsync\(\);' -and
     $vm -match 'else if \(IsMapPage\)' -and $vm -match 'RaisePropertyChanged\(nameof\(IsMapPage\)\);') `
    -Severity Critical

$icon = Join-Path $root 'src\MystTiq.Desktop\Assets\Icons\icon-map.png'
Add-MystTiqCheck $ctx 'v0.7.99.0 Contracts' 'the World navigation lists Map with its own icon, a real PNG' `
    ($xaml -match 'CommandParameter="Map"><Border Classes="navSurface"><Grid ColumnDefinitions="60,\*" ColumnSpacing="12"><Image Source="/Assets/Icons/icon-map\.png"' -and
     (Test-Path $icon) -and ((Get-Item $icon).Length -gt 10000) -and
     ([System.IO.File]::ReadAllBytes($icon)[1..3] -join ',') -eq '80,78,71') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.99.0 Contracts' 'the Map page exists, with the map first and at a size that fits the window' `
    ($xaml -match 'IsVisible="\{Binding IsMapPage\}"' -and $xaml -match '<Viewbox Width="560" Height="560"') `
    -Severity Critical

# The bug this version found by looking at the running app: Canvas.Left/Top on a template root or
# container did not position markers, so every marker sat in the top-left corner. They are now
# positioned by a margin from the data in a Grid layer, with the shape left-aligned so its centre
# (not the centre of the wider label under it) lands on the point.
Add-MystTiqCheck $ctx 'v0.7.99.0 Contracts' 'markers are positioned by a data margin in a Grid layer, never by Canvas attached properties' `
    (([regex]::Matches($xaml, 'Margin="\{Binding MarkerMargin\}"')).Count -eq 2 -and
     $xaml -notmatch 'Canvas\.Left="\{Binding CanvasX\}"' -and $xaml -notmatch '<Canvas Width="480" Height="480"/>' -and
     $xaml -match '<Rectangle Width="10" Height="10" HorizontalAlignment="Left"' -and $xaml -match '<Ellipse Width="10" Height="10" HorizontalAlignment="Left"') `
    -Severity Critical

$dto = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\WorldMapDtos.cs'
Add-MystTiqCheck $ctx 'v0.7.99.0 Contracts' 'both marker DTOs centre the marker on its point and carry the in-game coordinate text' `
    (([regex]::Matches($dto, 'MarkerMargin => new\(CanvasX - 5, CanvasY - 5, 0, 0\)')).Count -eq 2 -and ([regex]::Matches($dto, 'string CoordinateText = ""')).Count -eq 2 -and
     $vm -match 'PalworldMapCoordinates\.Describe\(location\.X, location\.Y\)' -and $vm -match 'PalworldMapCoordinates\.Describe\(x, y\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.99.0 Contracts' 'the map moved off the Players page, which now points to it' `
    ($xaml -match 'Content="Open Map" Command="\{Binding NavigateCommand\}" CommandParameter="Map"' -and $xaml -notmatch 'Command="\{Binding ToggleWorldMapCommand\}"') `
    -Severity Critical

$coords = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\PalworldMapCoordinates.cs'
Add-MystTiqCheck $ctx 'v0.7.99.0 Contracts' 'coordinates are described as the game shows them, with no negative zero' `
    ($coords -match 'public static string Describe\(double worldX, double worldY\)' -and $coords -match '\{mapX \+ 0\.0:0\}') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.99.0 Contracts' 'the logic harness covers the coordinate description' `
    ($harness -match 'RunScenario\("Map coordinates are described') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.99.0-map-page.md' 'v0.7.99.0 Contracts' 'v0.7.99.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.99.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.99\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.99.0.md' 'Documentation' 'v0.7.99.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.99.0 entry' ($changelogText -match '## v0\.7\.99\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.98.0\MystTiqPalworldServer_v0.7.98.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.98.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.98.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.98.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.98.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.98.0 checkpoint logic gate still passes' `
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
