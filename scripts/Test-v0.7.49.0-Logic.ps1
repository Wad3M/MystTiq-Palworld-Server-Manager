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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.49.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.49\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.49.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.49\.0"' `
    'Versioning' 'app.manifest reports v0.7.49.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Services\ThemeColorMath.cs' 'Regression' 'v0.7.48.0 theme derivation engine is still present' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\HeadlessUe4ssReleaseCatalogService.cs' 'Regression' 'v0.7.47.0 UE4SS release catalog is still present' -Severity Critical

$modServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs') -Raw
$apiHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$releaseDtoText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Models\Ue4ssReleaseDto.cs') -Raw
$apiClientInterfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$apiClientText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw
$viewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'ResolveUe4ss/DetectUe4ssVersion (v0.7.30.0/v0.7.48.0-era logic) still present' `
    ($modServiceText -match 'private HeadlessUe4ssStatus ResolveUe4ss\(\)' -and
     $modServiceText -match 'private string DetectUe4ssVersion\(\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.49.0 contract presence -- UE4SS Install/Rollback
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'HeadlessModManagementService exposes Preview/Apply/Rollback for UE4SS install' `
    ($modServiceText -match 'public async Task<Ue4ssInstallPreview\?> PreviewUe4ssInstallAsync\(string source, string tagName, CancellationToken cancellationToken\)' -and
     $modServiceText -match 'public async Task<Ue4ssInstallResult> ApplyUe4ssInstallAsync\(string token, CancellationToken cancellationToken\)' -and
     $modServiceText -match 'public async Task<Ue4ssInstallResult> RollbackUe4ssInstallAsync\(CancellationToken cancellationToken\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'Preview resolves against the live release catalog server-side (no client-supplied download URL)' `
    ($modServiceText -match 'var catalog = await ue4ssReleaseCatalog\.GetCatalogAsync\(cancellationToken\);' -and
     $modServiceText -match 'var release = releases\.FirstOrDefault\(r => r\.TagName == tagName\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'Apply requires PalServer stopped before writing engine files' `
    ($modServiceText -match 'Stop PalServer before installing UE4SS -- its engine files are loaded into the running process\.' -and
     $modServiceText -match 'Stop PalServer before rolling back UE4SS -- its engine files are loaded into the running process\.') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'Any zip entry under a Mods folder at any nesting depth is skipped (mod state is never touched)' `
    ($modServiceText -match 'relative\.Split\(''/''\)\.Any\(segment => segment\.Equals\("Mods", StringComparison\.OrdinalIgnoreCase\)\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'UE4SS-settings.ini is preserved (not overwritten) if it already exists on disk' `
    ($modServiceText -match 'Path\.GetFileName\(relative\)\.Equals\("UE4SS-settings\.ini", StringComparison\.OrdinalIgnoreCase\) && File\.Exists\(destination\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'A layout mismatch between the selected release and an existing install is refused, not silently applied' `
    ($modServiceText -match 'if \(zipIsModern && legacyInstalled\)' -and
     $modServiceText -match 'if \(!zipIsModern && modernInstalled\)' -and
     $modServiceText -match 'risk UE4SS loading twice') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'Engine snapshot is scoped to exactly the planned writes, not a whole-directory backup' `
    ($modServiceText -match 'private void CaptureUe4ssEngineSnapshot\(IReadOnlyList<string> plannedRelativePaths\)' -and
     $modServiceText -match 'private void RestoreUe4ssEngineSnapshot\(\)' -and
     $modServiceText -match 'internal sealed record Ue4ssSnapshotMeta\(string\[\] PlannedPaths, string\[\] ExistedPaths, DateTimeOffset CapturedAt\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'Install failure triggers automatic rollback (no half-overwritten engine left on disk)' `
    ($modServiceText -match 'RestoreUe4ssEngineSnapshot\(\);\s*\n\s*return Ue4ssInstallResult\.Failure\(\$"Install failed and any partially-written files were rolled back') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'LocalManagementApiHost exposes the four new UE4SS install/rollback routes' `
    ($apiHostText -match 'routes\.MapGet\("/ue4ss/install/status"' -and
     $apiHostText -match 'routes\.MapPost\("/ue4ss/install/preview"' -and
     $apiHostText -match 'routes\.MapPost\("/ue4ss/install/apply"' -and
     $apiHostText -match 'routes\.MapPost\("/ue4ss/install/rollback"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'Install/apply/rollback routes require Admin role' `
    ([regex]::Matches($apiHostText, 'routes\.MapPost\("/ue4ss/install/(preview|apply|rollback)"[\s\S]{0,400}?RequireRole\(MystTiqRole\.Admin, p\.Id\);').Count -eq 3) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'Desktop DTOs for install preview/result/status exist' `
    ($releaseDtoText -match 'public sealed class Ue4ssInstallPreviewDto' -and
     $releaseDtoText -match 'public sealed class Ue4ssInstallResultDto' -and
     $releaseDtoText -match 'public sealed class Ue4ssInstallStatusDto') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'IMystTiqApiClient/MystTiqApiClient expose Preview/Apply/Rollback UE4SS install methods' `
    ($apiClientInterfaceText -match 'Task<Ue4ssInstallPreviewDto\?> PreviewUe4ssInstallAsync\(' -and
     $apiClientInterfaceText -match 'Task<Ue4ssInstallResultDto> ApplyUe4ssInstallAsync\(' -and
     $apiClientInterfaceText -match 'Task<Ue4ssInstallResultDto> RollbackUe4ssInstallAsync\(' -and
     $apiClientText -match 'public async Task<Ue4ssInstallPreviewDto\?> PreviewUe4ssInstallAsync\(' -and
     $apiClientText -match 'public async Task<Ue4ssInstallResultDto> ApplyUe4ssInstallAsync\(' -and
     $apiClientText -match 'public async Task<Ue4ssInstallResultDto> RollbackUe4ssInstallAsync\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'ViewModel exposes SelectedUe4ssRelease, install state, and three install commands' `
    ($viewModelText -match 'public Ue4ssReleaseDto\? SelectedUe4ssRelease' -and
     $viewModelText -match 'public string Ue4ssInstallState' -and
     $viewModelText -match 'public bool Ue4ssRollbackAvailable' -and
     $viewModelText -match 'public ICommand PreviewUe4ssInstallCommand \{ get; \}' -and
     $viewModelText -match 'public ICommand ApplyUe4ssInstallCommand \{ get; \}' -and
     $viewModelText -match 'public ICommand RollbackUe4ssInstallCommand \{ get; \}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'UE4SS ribbon group gains Preview Install / Confirm Install / Rollback buttons' `
    ($viewModelText -match 'new\("👁", "Preview Install", "Preview installing the selected UE4SS release", PreviewUe4ssInstallCommand' -and
     $viewModelText -match 'new\("⬇", "Confirm Install", "Install the previewed UE4SS release", ApplyUe4ssInstallCommand.*IsSuccessButton: true' -and
     $viewModelText -match 'new\("↩", "Rollback", "Undo the last UE4SS install", RollbackUe4ssInstallCommand.*IsDangerButton: true') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'UE4SS release list is a selectable ListBox, not the old read-only ItemsControl stub' `
    ($mainWindowAxamlText -match '<ListBox ItemsSource="\{Binding Ue4ssVisibleReleases\}" SelectedItem="\{Binding SelectedUe4ssRelease\}"' -and
     $mainWindowAxamlText -notmatch "Installing a specific release isn't wired up yet") `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.49.0-ue4ss-install-rollback.md' 'v0.7.49.0 Contracts' 'v0.7.49.0 architecture doc is present' -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.49.0 Contracts' 'Architecture doc discloses the live verification actually performed' `
    ((Get-Content (Join-Path $root 'docs\architecture\v0.7.49.0-ue4ss-install-rollback.md') -Raw) -match 'Live verification performed') `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.49.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.49\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.48.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.48.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.48.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.48.0\MystTiqPalworldServer_v0.7.48.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.48.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.48.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.48.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.48.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.48.0 checkpoint logic gate still passes' `
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

    # v0.7.49.0 adds new HeadlessHost routes but does not change any existing route contract --
    # every server-side route/CLI smoke test carried forward from prior versions is expected to
    # pass unchanged.
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
