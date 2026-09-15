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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.60.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.60\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.60.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.60\.0"' `
    'Versioning' 'app.manifest reports v0.7.60.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

$lifecycleModelsText = Get-Content (Join-Path $root 'src\MystTiq.Core\Models\ServerLifecycleModels.cs') -Raw
$winLifecycleText = Get-Content (Join-Path $root 'src\MystTiq.Core\Services\WindowsServerLifecycleService.cs') -Raw
$linuxLifecycleText = Get-Content (Join-Path $root 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs') -Raw
$hostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$modServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs') -Raw
$componentUpdateText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessComponentUpdateService.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.29.0 mismatched-path detection and the AlreadyRunning guard are both unchanged' `
    ($winLifecycleText -match 'FindProcessesWithMismatchedPath' -and
     $winLifecycleText -match 'if \(FindManagedServerProcesses\(\)\.Count > 0\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.7.30.0 UE4SS 0.0.0.0 version filter (IsMeaningfulVersion) is unchanged' `
    ($modServiceText -match 'private static bool IsMeaningfulVersion\(string\? value\) =>') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.60.0 contract presence -- Port Conflict Prevention
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'HeadlessExitCode.PortConflict exists' `
    ($lifecycleModelsText -match 'PortConflict = 17,') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'Windows StartAsync checks GetGuardedListeningPorts() for the configured game port before launching' `
    ($winLifecycleText -match '(?s)if \(sessionInspector\.GetGuardedListeningPorts\(\)\.Contains\(expectedGamePort\)\)\s*return new ServerLifecycleOperationResult\(HeadlessExitCode\.PortConflict,') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'Linux StartAsync has the same port-conflict guard' `
    ($linuxLifecycleText -match '(?s)if \(sessionInspector\.GetGuardedListeningPorts\(\)\.Contains\(expectedGamePort\)\)\s*\{\s*var conflictSnapshot = await GetStatusAsync\(cancellationToken\);\s*return new ServerLifecycleOperationResult\(\s*HeadlessExitCode\.PortConflict,') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'PortConflict maps to HTTP 409 alongside the other lifecycle conflict codes' `
    ($hostText -match 'HeadlessExitCode\.PortConflict => StatusCodes\.Status409Conflict,') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.60.0 contract presence -- UE4SS Version Tracking
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'Ue4ssInstallManifest record and its read/write/delete helpers exist' `
    ($modServiceText -match 'internal sealed record Ue4ssInstallManifest\(string Source, string TagName, DateTimeOffset InstalledAtUtc\);' -and
     $modServiceText -match 'private void WriteUe4ssInstallManifest\(string source, string tagName\)' -and
     $modServiceText -match 'private void DeleteUe4ssInstallManifest\(\)' -and
     $modServiceText -match 'private Ue4ssInstallManifest\? TryReadUe4ssInstallManifest\(\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'ApplyUe4ssInstallAsync writes the manifest on success' `
    ($modServiceText -match 'WriteUe4ssInstallManifest\(preview\.Source, preview\.TagName\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'RollbackUe4ssInstallAsync deletes the manifest, since a restored snapshot no longer matches what it would describe' `
    ($modServiceText -match '(?s)RestoreUe4ssEngineSnapshot\(\);\s*//.*?DeleteUe4ssInstallManifest\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'DetectUe4ssVersion checks the manifest first, ahead of the existing marker-file/DLL-metadata fallbacks' `
    ($modServiceText -match '(?s)private string DetectUe4ssVersion\(\)\s*\{\s*// Checked first.*?var manifest = TryReadUe4ssInstallManifest\(\);\s*if \(manifest is not null && Directory\.Exists\(paths\.Ue4ssRoot\)\)\s*return \$"\{manifest\.TagName\} \(\{manifest\.Source\}\)";') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'TryGetInstalledUe4ssReleaseTag is publicly exposed for HeadlessComponentUpdateService to consume' `
    ($modServiceText -match 'public string\? TryGetInstalledUe4ssReleaseTag\(\) => TryReadUe4ssInstallManifest\(\)\?\.TagName;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'CheckUe4ssAsync does a real exact-tag comparison when a manifest exists, reporting genuine UpToDate/UpdateAvailable rather than always CheckManually' `
    ($componentUpdateText -match 'var installedTag = modManagement\.TryGetInstalledUe4ssReleaseTag\(\);' -and
     $componentUpdateText -match 'var upToDate = string\.Equals\(installedTag, release\.TagName, StringComparison\.OrdinalIgnoreCase\);' -and
     $componentUpdateText -match 'upToDate \? "UpToDate" : "UpdateAvailable"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.60.0 Contracts' 'CheckUe4ssAsync still falls back to the honest CheckManually status when no manifest exists' `
    ($componentUpdateText -match '"CheckManually", \$"GitHub: \{Ue4ssRepo\}", now,') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.60.0-port-conflict-ue4ss-version-tracking.md' 'v0.7.60.0 Contracts' 'v0.7.60.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.60.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.60\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.7.59.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.59.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.59.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.59.0\MystTiqPalworldServer_v0.7.59.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.59.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.59.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.59.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.59.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.59.0 checkpoint logic gate still passes' `
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
