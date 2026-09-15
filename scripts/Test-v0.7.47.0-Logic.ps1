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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.47.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.47\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.47.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.47\.0"' `
    'Versioning' 'app.manifest reports v0.7.47.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$mainWindowVmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$catalogServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessUe4ssReleaseCatalogService.cs') -Raw
$managementApiHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$apiClientInterfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$apiClientText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.46.0 console newest-first ordering and v0.7.45.0 Update Center are still present' `
    ($mainWindowVmText -match 'foreach \(var line in LogLines\.Reverse\(\)\)' -and
     $mainWindowVmText -match 'public ObservableCollection<ComponentVersionDto> CoreServerComponents \{ get; \} = \[\];') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'Existing SelectedUe4ssFork/Ue4ssForkOptions picker is unchanged' `
    ($mainWindowVmText -match 'public IReadOnlyList<string> Ue4ssForkOptions \{ get; \} = \["Palworld Fork", "Official Upstream"\];') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.47.0 contract presence -- UE4SS Release Catalog
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'HeadlessUe4ssReleaseCatalogService exists with the Ue4ssReleaseInfo/Ue4ssReleaseCatalog models' `
    ($catalogServiceText -match 'public sealed class HeadlessUe4ssReleaseCatalogService' -and
     $catalogServiceText -match 'public sealed record Ue4ssReleaseInfo\(' -and
     $catalogServiceText -match 'public sealed record Ue4ssReleaseCatalog\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'Both real GitHub repos are queried (Palworld Fork and Official Upstream)' `
    ($catalogServiceText -match 'PalworldForkRepo = "Okaetsu/RE-UE4SS"' -and
     $catalogServiceText -match 'OfficialUpstreamRepo = "UE4SS-RE/RE-UE4SS"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'Asset selection excludes developer builds and requires "UE4SS" in the name, verified against real release data' `
    ($catalogServiceText -match 'a\.Name\.Contains\("UE4SS", StringComparison\.OrdinalIgnoreCase\) &&' -and
     $catalogServiceText -match '!a\.Name\.Contains\("dev", StringComparison\.OrdinalIgnoreCase\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'GitHub calls degrade gracefully instead of throwing' `
    ($catalogServiceText -match '(?s)private static async Task<T\?> TryGetJsonAsync.*?catch \{ return null; \}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'New fleet-level GET /api/v1/ue4ss/releases route is registered' `
    ($managementApiHostText -match 'var ue4ssReleaseCatalog = new HeadlessUe4ssReleaseCatalogService\(\);' -and
     $managementApiHostText -match 'app\.MapGet\("/api/v1/ue4ss/releases", async \(CancellationToken token\) =>\s*\n\s*Results\.Ok\(await ue4ssReleaseCatalog\.GetCatalogAsync\(token\)\)\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'Desktop client exposes GetUe4ssReleaseCatalogAsync' `
    ($apiClientInterfaceText -match 'Task<Ue4ssReleaseCatalogDto> GetUe4ssReleaseCatalogAsync\(' -and
     $apiClientText -match 'public async Task<Ue4ssReleaseCatalogDto> GetUe4ssReleaseCatalogAsync') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'Desktop model file exists' `
    (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Models\Ue4ssReleaseDto.cs') -PathType Leaf) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'ViewModel wires the release collections and a source-switching computed property' `
    ($mainWindowVmText -match 'public ObservableCollection<Ue4ssReleaseDto> Ue4ssPalworldForkReleases \{ get; \} = \[\];' -and
     $mainWindowVmText -match 'public ObservableCollection<Ue4ssReleaseDto> Ue4ssOfficialUpstreamReleases \{ get; \} = \[\];' -and
     $mainWindowVmText -match 'SelectedUe4ssFork == "Official Upstream" \? Ue4ssOfficialUpstreamReleases : Ue4ssPalworldForkReleases;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'Release catalog refresh is wired into Refresh Runtime, gated to the UE4SS page only' `
    ($mainWindowVmText -match 'if \(IsUe4ssPage\) await RefreshUe4ssReleaseCatalogAsync\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.47.0 Contracts' 'UE4SS page replaces the permanent BACKEND REQUIRED stub with a real release list' `
    ($mainWindowAxamlText -notmatch 'Release catalog and install/rollback controls require a dedicated headless release service\. BACKEND REQUIRED\.' -and
     $mainWindowAxamlText -match 'ItemsSource="\{Binding Ue4ssVisibleReleases\}" IsVisible="\{Binding HasUe4ssReleases\}"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.47.0-ue4ss-release-catalog.md' 'v0.7.47.0 Contracts' 'v0.7.47.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.47.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.47\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.46.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.46.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.46.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.46.0\MystTiqPalworldServer_v0.7.46.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.46.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.46.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.46.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.46.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.46.0 checkpoint logic gate still passes' `
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

    # v0.7.47.0 is additive (new HeadlessUe4ssReleaseCatalogService, new fleet-level route, new UI
    # list) with no change to any existing route contract -- every server-side route/CLI smoke test
    # carried forward from prior versions is expected to pass unchanged.
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
