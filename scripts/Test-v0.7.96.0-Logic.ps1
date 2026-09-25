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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.96.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.96\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.96.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.96\.0"' `
    'Versioning' 'app.manifest reports v0.7.96.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.95.0-Logic.ps1' 'Regression' 'v0.7.95.0 logic gate remains available' -Severity High

$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.92.0 base markers and click-to-select on the map remain present' `
    ($vm -match 'public bool IsBaseMarkersActive => UseCalibratedWorldPositions && IsPalpagosMapActive;' -and
     $vm -match 'private void SelectMapPlayer\(string\? playerId\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.96.0 Contracts -- live map: real positions and bases work out of the box
# ---------------------------------------------------------------------------
$coords = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\PalworldMapCoordinates.cs'
Add-MystTiqCheck $ctx 'v0.7.96.0 Contracts' 'the world-to-image mapping is calibrated to the expanded map, north up, not the old +/-1000 frame' `
    ($coords -match 'OriginPixelX = 625' -and $coords -match 'OriginPixelY = 335' -and $coords -match 'UnitsPerPixel = 3\.08' -and
     $coords -match 'OriginPixelY - mapY / UnitsPerPixel' -and $coords -notmatch 'MapExtent') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.96.0 Contracts' 'the in-game unit formula from v0.7.21.0 is unchanged (its own gate recomputes the published worked example)' `
    ($coords -match 'TranslateX = 123888' -and $coords -match 'TranslateY = 158000' -and $coords -match 'Scale = 459') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.96.0 Contracts' 'real-world positions are on by default and the map card starts expanded' `
    ($vm -match 'private bool _useCalibratedWorldPositions = true;' -and $vm -match 'private bool _isWorldMapExpanded = true;') `
    -Severity Critical

$prefs = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\LocalMapPreferencesStore.cs'
Add-MystTiqCheck $ctx 'v0.7.96.0 Contracts' 'a first run starts on the Palpagos map, while a deliberate Clear Background is still respected' `
    ($prefs -match 'public bool HasSavedPreference => File\.Exists\(StoragePath\);' -and
     $vm -match 'if \(!_mapPreferences\.HasSavedPreference\)' -and $vm -match 'p\.Key == "palpagos"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.96.0 Contracts' 'base locations arrive with the Players page read and through the Refresh Bases button, by one shared method' `
    ((([regex]::Matches($vm, 'ApplyBaseLocations\(snapshot\);')).Count -ge 2) -and $vm -match 'private void ApplyBaseLocations\(PlayerGuildSnapshotDto snapshot\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.96.0 Contracts' 'the map explains what is drawn (status text from the pure MapContentsText helper)' `
    ($vm -match 'MapContentsText\.Describe\(players\.Count, located\.Count, BaseMapPoints\.Count\)' -and
     (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Services\MapContentsText.cs'))) `
    -Severity Critical

$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.96.0 Contracts' 'the World Map card shows the status line and no longer labels the feature experimental' `
    ($xaml -match 'Text="\{Binding MapPlayersStatusText\}"' -and $xaml -match 'World Map — Players &amp; Bases' -and $xaml -notmatch 'Experimental: real-world positions') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$harnessProj = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\MystTiq.LogicHarness.csproj'
Add-MystTiqCheck $ctx 'v0.7.96.0 Contracts' 'the logic harness compiles the map files directly and covers the conversion and status text' `
    (([regex]::Matches($harness, 'RunScenario\("Map ')).Count -ge 3 -and
     $harnessProj -match 'PalworldMapCoordinates\.cs' -and $harnessProj -match 'MapContentsText\.cs') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.96.0-live-map-players-and-bases.md' 'v0.7.96.0 Contracts' 'v0.7.96.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.96.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.96\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.96.0.md' 'Documentation' 'v0.7.96.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.96.0 entry' ($changelogText -match '## v0\.7\.96\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.95.0\MystTiqPalworldServer_v0.7.95.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.95.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.95.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.95.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.95.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.95.0 checkpoint logic gate still passes' `
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
