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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.100.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.100\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.100.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.100\.0"' `
    'Versioning' 'app.manifest reports v0.7.100.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.99.0-Logic.ps1' 'Regression' 'v0.7.99.0 logic gate remains available' -Severity High

$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.99.0 Map page remains: World page, data-margin markers, no Canvas positioning, map fits the window' `
    ($vm -match 'public bool IsMapPage => SelectedPage == NavigationPage\.Map;' -and
     (([regex]::Matches($xaml, 'Margin="\{Binding MarkerMargin\}"')).Count -eq 2) -and
     $xaml -notmatch 'Canvas\.Left="\{Binding CanvasX\}"' -and $xaml -match '<Viewbox Width="560" Height="560"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.7.96.0 to v0.7.98.0 behaviour remains (calibrated map on by default, crash detail pane, Doctor rules)' `
    ($vm -match 'private bool _useCalibratedWorldPositions = true;' -and $xaml -match 'x:DataType="models:CrashFindingDto"' -and
     (Test-Path (Join-Path $root 'src\MystTiq.HeadlessHost\DoctorHealthRules.cs'))) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.100.0 Contracts -- map zoom, click to zoom, last-known player positions
# ---------------------------------------------------------------------------
$vp = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MapViewport.cs'
Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'the viewport keeps the point under the cursor fixed, clamps scale and offset, and has no UI dependency' `
    ($vp -match 'MinScale = 1;' -and $vp -match 'MaxScale = 10;' -and $vp -match 'WheelStep = 1\.25;' -and $vp -match 'MarkerZoomScale = 4;' -and
     $vp -match 'OffsetX = viewX - canvasX \* newScale;' -and $vp -match 'Math\.Clamp\(OffsetX, min, 0\)' -and $vp -notmatch 'using Avalonia') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'clicking a marker or a name zooms in on it, for players and for bases, and never zooms out from a closer view' `
    ($vm -match 'if \(marker is not null\) ZoomToMapPoint\(marker\.UnzoomedX, marker\.UnzoomedY\);' -and
     $vm -match 'private void ZoomToBase\(string\? baseId\)' -and $vp -match 'Math\.Max\(scale, Scale\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'markers are recomputed at the current zoom (they keep their size) while only the background image is scaled' `
    ($vm -match '_mapViewport\.ToView\(flatX, flatY\)' -and $vm -match 'MatrixTransform\(new Avalonia\.Matrix\(s, 0, 0, s,' -and
     $xaml -match 'RenderTransformOrigin="0%,0%" RenderTransform="\{Binding MapImageTransform\}"') `
    -Severity Critical

$code = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'the wheel zooms the map without scrolling the page, and drag pans it' `
    ($code -match 'private void MapSurface_OnPointerWheelChanged' -and $code -match 'vm\.ZoomMapAt\(position\.X, position\.Y, e\.Delta\.Y\);' -and $code -match 'e\.Handled = true;' -and
     $code -match 'vm\.PanMapBy\(position\.X - _mapDragLast\.X' -and
     $xaml -match 'PointerWheelChanged="MapSurface_OnPointerWheelChanged"' -and $xaml -match 'PointerMoved="MapSurface_OnPointerMoved"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'the map surface is exactly 480 units inside its border, so the zoom and marker maths line up' `
    ($xaml -match '<Border Width="482" Height="482" ClipToBounds="True"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'base markers and base rows are clickable and zoom in' `
    (([regex]::Matches($xaml, 'ZoomToBaseCommand')).Count -eq 2) `
    -Severity Critical

$reader = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\SavePlayerLocationReader.cs'
$explorer = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessPlayerGuildExplorerService.cs'
Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'last-known positions are read from player characters only, ignoring placeholder locations, and never cost the guild and base data' `
    ($reader -match 'LastJumpedLocation' -and $reader -match 'PlaceholderAltitude = 900000' -and $reader -match 'if \(x == 0 && y == 0\) continue;' -and
     $explorer -match 'try \{ playerLocations = SavePlayerLocationReader\.Read\(document\.RootElement\); \}' -and
     $explorer -match 'IReadOnlyList<SavedPlayerLocation>\? PlayerLocations = null') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'offline players are drawn as hollow markers from the save, online players keep their live position, and someone online is never also drawn offline' `
    ($vm -match 'IsOnline: false, LastSeenText: lastSeen' -and $vm -match 'if \(onlineKeys\.Contains\(key\)\) continue;' -and $vm -match 'if \(record\?\.Online == true\) continue;' -and
     $vm -match 'private bool _showOfflinePlayersOnMap = true;' -and
     $xaml -match 'IsVisible="\{Binding IsOffline\}"' -and $xaml -match 'Show offline players \(last known position\)') `
    -Severity Critical

$dto = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\WorldMapDtos.cs'
Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'marker DTOs carry online state, last-seen text, the on-screen position and the unzoomed position' `
    ($dto -match 'bool IsOnline = true, string LastSeenText = ""' -and ([regex]::Matches($dto, 'double UnzoomedX = 0, double UnzoomedY = 0')).Count -eq 2 -and $dto -match 'public bool IsOffline => !IsOnline;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'a new server starts on the whole map, not zoomed on the previous server''s base' `
    ($vm -match '_mapViewport\.Reset\(\);\s*OnMapViewportChanged\(\);' -and $vm -match '_lastPlayerLocations = \[\];') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'opening the Map or Players page while the app is busy waits and loads instead of silently skipping the load' `
    ($vm -match 'if \(page is not \(NavigationPage\.Map or NavigationPage\.Players\)\) return;' -and
     $vm -match 'for \(var i = 0; i < 120 && IsBusy; i\+\+\) await Task\.Delay\(500\);' -and $vm -match 'if \(IsBusy \|\| SelectedPage != page\) return;') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$harnessProj = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\MystTiq.LogicHarness.csproj'
Add-MystTiqCheck $ctx 'v0.7.100.0 Contracts' 'the logic harness covers the viewport, the save reader and the offline status text' `
    ($harness -match 'RunScenario\("Map viewport: wheel zoom' -and $harness -match 'RunScenario\("Map viewport: panning' -and $harness -match 'RunScenario\("Saved player positions' -and
     $harness -match 'withOffline' -and $harnessProj -match 'MapViewport\.cs') `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.100.0-RouteSmoke.ps1' 'v0.7.100.0 Contracts' 'the player locations route smoke exists' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.100.0-map-zoom-and-player-positions.md' 'v0.7.100.0 Contracts' 'v0.7.100.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.100.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.100\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.100.0.md' 'Documentation' 'v0.7.100.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.100.0 entry' ($changelogText -match '## v0\.7\.100\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.99.0\MystTiqPalworldServer_v0.7.99.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.99.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.99.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.99.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.99.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.99.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'v0.7.100.0 Runtime' 'v0.7.100.0 player locations route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.100.0-RouteSmoke.ps1') -ProjectRoot $root
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
