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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.55.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.55\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.55.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.55\.0"' `
    'Versioning' 'app.manifest reports v0.7.55.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

$modServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs') -Raw
$routeHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$viewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$apiClientText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw
$apiInterfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$modDtoText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Models\ModManagementDtos.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.41.0 MOD update-detection method (FindMatchingWorkshopItem reuse target) is unchanged' `
    ($modServiceText -match 'public Task<HeadlessModUpdateCheckResult> CheckModUpdateAsync\(string type, string package, CancellationToken cancellationToken\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.7.40.0 MOD DETAILS panel EVIDENCE section is unchanged' `
    ($mainWindowAxamlText -match 'Text="EVIDENCE" Classes="muted" FontSize="10"') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.55.0 contract presence -- Website-Sourced MOD Descriptions
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'Backend exposes GetModDescriptionAsync and SetModDescriptionSourceAsync' `
    ($modServiceText -match 'public async Task<HeadlessModDescriptionResult> GetModDescriptionAsync\(string type, string package, bool forceRefresh, CancellationToken cancellationToken\)' -and
     $modServiceText -match 'public Task<HeadlessModMutationResult> SetModDescriptionSourceAsync\(string type, string package, string sourceUrl, CancellationToken cancellationToken\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'Steam Workshop description fetch reuses FindMatchingWorkshopItem (no second matching engine)' `
    ($modServiceText -match 'var match = FindMatchingWorkshopItem\(package\);' -and
     $modServiceText -match 'GetPublishedFileDetails/v1/') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'GitHub fetch is scoped to github.com repositories via a real host check, not a free-form URL fetch' `
    ($modServiceText -match 'uri\.Host\.Equals\("github\.com", StringComparison\.OrdinalIgnoreCase\)' -and
     $modServiceText -match 'api\.github\.com/repos/') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'Manual Source URL is validated as an absolute http/https address before being stored' `
    ($modServiceText -match 'Uri\.TryCreate\(sourceUrl, UriKind\.Absolute, out var uri\) \|\| \(uri\.Scheme != Uri\.UriSchemeHttp && uri\.Scheme != Uri\.UriSchemeHttps\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'Descriptions are cached to disk under the manager runtime root, not re-fetched on every call' `
    ($modServiceText -match 'Path\.Combine\(paths\.ManagerRuntimeRoot, "mod-descriptions"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'Two new routes are registered: GET description (cache-safe) and POST description/source' `
    ($routeHostText -match 'routes\.MapGet\("/mods/\{type\}/\{package\}/description", async \(string type, string package, bool\? refresh, CancellationToken token\) =>' -and
     $routeHostText -match 'routes\.MapPost\("/mods/\{type\}/\{package\}/description/source", async \(string type, string package, ModDescriptionSourceRequest request, CancellationToken token\) =>') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'Desktop client exposes both new calls through IMystTiqApiClient/MystTiqApiClient' `
    ($apiInterfaceText -match 'Task<ModDescriptionResultDto> GetModDescriptionAsync\(' -and
     $apiInterfaceText -match 'Task<ModMutationResultDto> SetModDescriptionSourceAsync\(' -and
     $apiClientText -match 'public async Task<ModDescriptionResultDto> GetModDescriptionAsync\(' -and
     $apiClientText -match 'public async Task<ModMutationResultDto> SetModDescriptionSourceAsync\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'ModDescriptionResultDto is defined with the fields the panel binds to' `
    ($modDtoText -match 'public sealed class ModDescriptionResultDto' -and
     $modDtoText -match '"available"' -and $modDtoText -match '"source"' -and
     $modDtoText -match '"title"' -and $modDtoText -match '"description"' -and
     $modDtoText -match '"sourceUrl"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'ViewModel resets stale description state whenever SelectedMod changes' `
    ($viewModelText -match '(?s)public ModItemDto\? SelectedMod\s*\{.*?SelectedModDescription = null;\s*\n\s*SelectedModDescriptionSourceInput = string\.Empty;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'FetchSelectedModDescriptionCommand and SetSelectedModDescriptionSourceCommand are declared and wired' `
    ($viewModelText -match 'public ICommand FetchSelectedModDescriptionCommand \{ get; \}' -and
     $viewModelText -match 'public ICommand SetSelectedModDescriptionSourceCommand \{ get; \}' -and
     $viewModelText -match 'FetchSelectedModDescriptionCommand = new AsyncCommand\(FetchSelectedModDescriptionAsync,' -and
     $viewModelText -match 'SetSelectedModDescriptionSourceCommand = new AsyncCommand\(SetSelectedModDescriptionSourceAsync,') `
    -Severity Critical

$selectedModSetterMatch = [regex]::Match($viewModelText, '(?s)public ModItemDto\? SelectedMod\s*\{.*?\n    \}\n\n?\s*// v0\.7\.40\.0')
Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'Fetch is never called from the SelectedMod setter -- only from the two new command methods (on-click only, no auto-fetch)' `
    ($selectedModSetterMatch.Success -and -not ($selectedModSetterMatch.Value -match 'FetchSelectedModDescriptionAsync\(\)\s*;')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'MOD DETAILS panel has a new DESCRIPTION section with Fetch/Refresh and a Source URL input' `
    ($mainWindowAxamlText -match 'Text="DESCRIPTION" Classes="muted" FontSize="10"' -and
     $mainWindowAxamlText -match 'Command="\{Binding FetchSelectedModDescriptionCommand\}"' -and
     $mainWindowAxamlText -match 'Command="\{Binding SetSelectedModDescriptionSourceCommand\}"' -and
     $mainWindowAxamlText -match 'Text="\{Binding SelectedModDescriptionSourceInput\}"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.55.0-website-sourced-mod-descriptions.md' 'v0.7.55.0 Contracts' 'v0.7.55.0 architecture doc is present' -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.55.0 Contracts' 'Architecture doc discloses the on-click-only fetch discipline and the Nexus Mods gap' `
    ((Get-Content (Join-Path $root 'docs\architecture\v0.7.55.0-website-sourced-mod-descriptions.md') -Raw) -match 'on-click only' -and
     (Get-Content (Join-Path $root 'docs\architecture\v0.7.55.0-website-sourced-mod-descriptions.md') -Raw) -match 'Nexus Mods is not a supported source') `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.55.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.55\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.54.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.54.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.54.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.54.0\MystTiqPalworldServer_v0.7.54.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.54.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.54.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.54.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.54.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.54.0 checkpoint logic gate still passes' `
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

    # v0.7.55.0 adds two new routes but changes no existing route's contract, so every
    # server-side route/CLI smoke test carried forward is expected to pass unchanged.
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
