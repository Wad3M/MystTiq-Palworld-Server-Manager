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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.45.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.45\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.45.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.45\.0"' `
    'Versioning' 'app.manifest reports v0.7.45.0' -Severity Critical

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
$componentServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessComponentUpdateService.cs') -Raw
$profileHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\ServerProfileHost.cs') -Raw
$managementApiHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$apiClientInterfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$apiClientText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.44.0 machine-wide instance panel and v0.7.28.0 managed-process tracking are still present' `
    ($mainWindowAxamlText -match 'ALL PALWORLD INSTANCES ON THIS MACHINE' -and
     $mainWindowVmText -match 'public ObservableCollection<ServerProcessDto> ManagedProcesses \{ get; \} = \[\];') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'Existing SteamCMD update action route is unchanged' `
    ($managementApiHostText -match 'routes\.MapPost\("/server/distribution/update"') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.45.0 contract presence -- Update Center Overhaul
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'HeadlessComponentUpdateService exists with the ComponentVersionInfo/ComponentVersionSnapshot models' `
    ($componentServiceText -match 'public sealed class HeadlessComponentUpdateService' -and
     $componentServiceText -match 'public sealed record ComponentVersionInfo\(' -and
     $componentServiceText -match 'public sealed record ComponentVersionSnapshot\(IReadOnlyList<ComponentVersionInfo> Components, DateTimeOffset ObservedAt\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'All 11 v0.2.16.4-reference components are checked' `
    ($(
        $expected = @(
            'CheckMystTiqAsync', 'CheckSteamCmd\(', 'CheckPalworldServerAsync', 'CheckUe4ssAsync',
            'CheckPythonAsync', 'CheckPipAsync', 'CheckSaveToolsAsync', 'CheckPlmOodle\(',
            'CheckDotNetAsync', 'CheckVcRuntime\(', 'CheckCppBuildTools\('
        )
        $missing = @($expected | Where-Object { $componentServiceText -notmatch $_ })
        $missing.Count -eq 0
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'MystTiq self-update check is informational only (no auto-install)' `
    ($componentServiceText -match 'No automatic self-update is performed -- this is informational only\.' -and
     $componentServiceText -match 'updateIsInformationalOnly: true') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'Palworld Dedicated Server uses the real SteamCMD appmanifest buildid and the unauthenticated Steam Web API UpToDateCheck endpoint' `
    ($componentServiceText -match 'appmanifest_\{PalworldDedicatedServerAppId\}\.acf' -and
     $componentServiceText -match 'ISteamApps/UpToDateCheck/v1') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'UE4SS reuses the existing tested installed-version detection instead of duplicating it' `
    ($componentServiceText -match 'modManagement\.GetInventoryAsync\(ct\)' -and
     $componentServiceText -match 'inventory\.Ue4ss\.InstalledVersion') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'pip and Palworld Save Tools compare against real PyPI package feeds' `
    ($componentServiceText -match 'https://pypi\.org/pypi/pip/json' -and
     $componentServiceText -match 'https://pypi\.org/pypi/palworld-save-tools/json') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' '.NET Runtime compares against the official dotnet/core releases-index feed' `
    ($componentServiceText -match 'raw\.githubusercontent\.com/dotnet/core/main/release-notes/releases-index\.json') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'Components with no reliable unattended "latest version" source are disclosed honestly, not faked' `
    ($componentServiceText -match 'does not publish a simple, reliable unattended feed for' -and
     $componentServiceText -match 'No single canonical upstream project exists for this component' -and
     $componentServiceText -match 'cannot be safely auto-compared') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'Every network/process call degrades gracefully instead of throwing' `
    ($(
        $tryGetJson = [regex]::Match($componentServiceText, '(?s)private static async Task<T\?> TryGetJsonAsync.*?catch \{ return null; \}')
        $runProcess = [regex]::Match($componentServiceText, '(?s)private static async Task<\(int ExitCode, string Stdout, string Stderr\)> RunProcessAsync.*?catch \{ return \(-1, string\.Empty, string\.Empty\); \}')
        $tryGetJson.Success -and $runProcess.Success
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'New GET /update-center/components route is registered and wired to the profile-scoped service' `
    ($managementApiHostText -match 'routes\.MapGet\("/update-center/components", async \(CancellationToken token\) =>\s*\n\s*Results\.Ok\(await p\.ComponentUpdates\.GetSnapshotAsync\(token\)\)\);' -and
     $managementApiHostText -match 'var componentUpdates = new HeadlessComponentUpdateService\(paths, modManagement\);' -and
     $managementApiHostText -match 'ComponentUpdates = componentUpdates') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'ServerProfileHost declares the ComponentUpdates member' `
    ($profileHostText -match 'public required HeadlessComponentUpdateService ComponentUpdates \{ get; init; \}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'Desktop client exposes GetComponentVersionsAsync' `
    ($apiClientInterfaceText -match 'Task<ComponentVersionSnapshotDto> GetComponentVersionsAsync\(' -and
     $apiClientText -match 'public async Task<ComponentVersionSnapshotDto> GetComponentVersionsAsync') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'Desktop model and status-color converter files exist' `
    ((Test-Path (Join-Path $root 'src\MystTiq.Desktop\Models\ComponentVersionDto.cs') -PathType Leaf) -and
     (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Converters\ComponentStatusColorConverter.cs') -PathType Leaf)) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'ViewModel wires CoreServerComponents/SaveRuntimeDependencyComponents and refreshes them from Refresh' `
    ($mainWindowVmText -match 'public ObservableCollection<ComponentVersionDto> CoreServerComponents \{ get; \} = \[\];' -and
     $mainWindowVmText -match 'public ObservableCollection<ComponentVersionDto> SaveRuntimeDependencyComponents \{ get; \} = \[\];' -and
     $mainWindowVmText -match '(?s)private async Task RefreshDistributionAsync\(\).*?await RefreshComponentVersionsAsync\(\);\s*\n\s*\}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.45.0 Contracts' 'Update Center page renders the two labeled component sections' `
    ($mainWindowAxamlText -match 'Text="CORE SERVER" Classes="muted" FontWeight="Bold"' -and
     $mainWindowAxamlText -match 'Text="SAVE &amp; RUNTIME DEPENDENCIES" Classes="muted" FontWeight="Bold"' -and
     $mainWindowAxamlText -match 'ItemsSource="\{Binding CoreServerComponents\}"' -and
     $mainWindowAxamlText -match 'ItemsSource="\{Binding SaveRuntimeDependencyComponents\}"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.45.0-update-center-overhaul.md' 'v0.7.45.0 Contracts' 'v0.7.45.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.45.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.45\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.44.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.44.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.44.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.44.0\MystTiqPalworldServer_v0.7.44.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.44.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.44.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.44.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.44.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.44.0 checkpoint logic gate still passes' `
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

    # v0.7.45.0 is additive (new HeadlessComponentUpdateService, new route, new UI table) with no
    # change to any existing route contract -- every server-side route/CLI smoke test carried
    # forward from prior versions is expected to pass unchanged.
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
