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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.92.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.92\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.92.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.92\.0"' `
    'Versioning' 'app.manifest reports v0.7.92.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.91.0-Logic.ps1' 'Regression' 'v0.7.91.0 logic gate remains available' -Severity High

$svcText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessPlayerGuildExplorerService.cs'
$diagText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiagnosticsService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.91.0-era crash-risk Doctor finding remains present, untouched by this version' `
    ($diagText -match 'findings\.AddRange\(BuildCrashRiskFindings\(\)\);') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.92.0 Contracts -- World Map base markers and click-to-select
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.92.0 Contracts' 'the explorer collects BaseCampSaveData roots in the same traversal as GroupSaveDataMap' `
    ($svcText -match 'key == "basecampsavedata"' -and $svcText -match 'FindAuthoritativeRoots\(document\.RootElement, roots, baseCampRoots, 0\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.92.0 Contracts' 'base coordinates are read from spawn_transform.translation and exposed as BaseLocations' `
    ($svcText -match 'TryFindNestedProperty\(baseStruct, "spawn_transform"' -and
     $svcText -match 'TryFindNestedProperty\(transform, "translation"' -and
     $svcText -match 'public sealed record HeadlessBaseLocation\(' -and
     $svcText -match 'IReadOnlyList<HeadlessBaseLocation> BaseLocations\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.92.0 Contracts' 'a base no guild claims is still returned as Unowned rather than dropped' `
    ($svcText -match 'new HeadlessBaseLocation\(baseId, string\.Empty, "Unowned", point\.X, point\.Y\)') `
    -Severity High

$vmText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'v0.7.92.0 Contracts' 'base markers are drawn only in the calibrated real-world mode' `
    ($vmText -match 'public bool IsBaseMarkersActive => UseCalibratedWorldPositions && IsPalpagosMapActive;' -and
     $vmText -match 'if \(IsBaseMarkersActive && ShowBasesOnMap\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.92.0 Contracts' 'bases are fetched on demand, not on the status poll, and cleared on tab switch' `
    ($vmText -match 'private void LoadMapBasesIfNeeded\(\)' -and
     $vmText -match '_lastBaseLocations = \[\];\s*\r?\n\s*BaseMapPoints\.Clear\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.92.0 Contracts' 'clicking a player dot selects the matching player record for the existing teleport commands' `
    ($vmText -match 'private void SelectMapPlayer\(string\? playerId\)' -and
     $vmText -match 'if \(match is not null\) SelectedPlayerRecord = match;' -and
     $vmText -match 'SelectMapPlayerCommand = new RelayCommand<string>\(SelectMapPlayer\);') `
    -Severity Critical

$mapDtoText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\WorldMapDtos.cs'
Add-MystTiqCheck $ctx 'v0.7.92.0 Contracts' 'map point DTOs carry PlayerId and a base marker DTO exists' `
    ($mapDtoText -match 'record PlayerMapPointDto\(string PlayerId, string Name' -and $mapDtoText -match 'record BaseMapPointDto\(') `
    -Severity Critical

$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.92.0 Contracts' 'the map has a base marker layer, clickable player dots and a teleport action row' `
    ($xaml -match 'ItemsSource="\{Binding BaseMapPoints\}"' -and
     $xaml -match 'SelectMapPlayerCommand\}"' -and
     $xaml -match 'Command="\{Binding TeleportPlayerToMeCommand\}"' -and
     $xaml -match 'Command="\{Binding RefreshMapBasesCommand\}"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.92.0-world-map-base-markers.md' 'v0.7.92.0 Contracts' 'v0.7.92.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.92.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.92\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.92.0.md' 'Documentation' 'v0.7.92.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.92.0 entry' ($changelogText -match '## v0\.7\.92\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.91.0\MystTiqPalworldServer_v0.7.91.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.91.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.91.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.91.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.91.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.91.0 checkpoint logic gate still passes' `
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

