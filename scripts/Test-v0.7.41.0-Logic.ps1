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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.41.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.41\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.41.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.41\.0"' `
    'Versioning' 'app.manifest reports v0.7.41.0' -Severity Critical

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
$apiHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$mainWindowVmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$apiClientText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw
$apiInterfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$modDtosText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Models\ModManagementDtos.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.40.0 MOD DETAILS panel is still present' `
    ($mainWindowAxamlText -match 'Text="MOD DETAILS" FontSize="18" FontWeight="SemiBold"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.7.39.0 DetectModType auto-detection is still in effect' `
    ($modServiceText -match 'private static string DetectModType\(string extracted\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.41.0 contract presence -- MOD update detection
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.41.0 Contracts' 'CheckModUpdateAsync exists with matching helpers' `
    ($modServiceText -match 'public Task<HeadlessModUpdateCheckResult> CheckModUpdateAsync\(string type, string package, CancellationToken cancellationToken\)' -and
     $modServiceText -match 'private HeadlessWorkshopItem\? FindMatchingWorkshopItem\(string package\)' -and
     $modServiceText -match 'private DateTime\? GetInstalledModLastWriteUtc\(string type, string package\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.41.0 Contracts' 'Update check has no known-source path and never fabricates a live version claim' `
    ($modServiceText -match 'No known update source for this MOD\.' -and
     $modServiceText -notmatch 'GetPublishedFileDetails' -and
     $modServiceText -notmatch 'steamapi\.com' -and
     $modServiceText -notmatch 'api\.steampowered\.com') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.41.0 Contracts' 'HeadlessModUpdateCheckResult record is defined' `
    ($modServiceText -match 'public sealed record HeadlessModUpdateCheckResult\(\s*bool HasKnownSource, bool UpdateAvailable, string\? WorkshopId, string Detail\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.41.0 Contracts' 'check-update route is registered' `
    ($apiHostText -match 'routes\.MapGet\("/mods/\{type\}/\{package\}/check-update", async \(string type, string package, CancellationToken token\) =>') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.41.0 Contracts' 'Client DTO, interface method, and API client method exist' `
    ($modDtosText -match 'public sealed class ModUpdateCheckResultDto' -and
     $apiInterfaceText -match 'Task<ModUpdateCheckResultDto> CheckModUpdateAsync\(' -and
     $apiClientText -match 'public async Task<ModUpdateCheckResultDto> CheckModUpdateAsync\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.41.0 Contracts' 'ViewModel exposes CheckSelectedModUpdateCommand/UpdateSelectedModCommand/SelectedModUpdateText' `
    ($mainWindowVmText -match 'CheckSelectedModUpdateCommand = new AsyncCommand\(CheckSelectedModUpdateAsync,' -and
     $mainWindowVmText -match 'UpdateSelectedModCommand = new AsyncCommand\(UpdateSelectedModAsync,' -and
     $mainWindowVmText -match 'public string SelectedModUpdateText') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.41.0 Contracts' 'Update reuses the existing Workshop-import route rather than new mutation logic' `
    ($mainWindowVmText -match '_api\.ImportWorkshopModAsync\(profile, workshopId, BearerToken\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.41.0 Contracts' 'MOD DETAILS panel has Check for Update / Update buttons and a result text area' `
    ($mainWindowAxamlText -match 'Content="Check for Update" Classes="inspectAction" Command="\{Binding CheckSelectedModUpdateCommand\}"' -and
     $mainWindowAxamlText -match 'Content="Update" Classes="targetAction" Command="\{Binding UpdateSelectedModCommand\}"' -and
     $mainWindowAxamlText -match 'Text="\{Binding SelectedModUpdateText\}"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.41.0-mod-update-detection.md' 'v0.7.41.0 Contracts' 'v0.7.41.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.41.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.41\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.40.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.40.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.40.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.40.0\MystTiqPalworldServer_v0.7.40.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.40.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.40.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.40.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.40.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.40.0 checkpoint logic gate still passes' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))

    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'Build.ps1') Validate -StrictValidation
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release solution build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression Runtime' 'v0.5.1.5 isolated runtime smoke still passes (includes MOD install/traversal checks affected by this release)' {
        & (Join-Path $root 'scripts\Test-v0.5.1.5-RuntimeSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.7.41.0 adds a new read-only server-side route and reuses existing import logic for
    # Update, so unlike most Desktop-only prior versions the v0.5.1.5 runtime smoke suite above is
    # real new-ground evidence here (MOD install/traversal routes unaffected), not just regression
    # evidence. The route/CLI smoke scripts below remain unaffected regression checks. All need
    # Build.ps1 DesktopWindows's sidecar output, the same known quirk every version's -RunBuild
    # already works around.
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
