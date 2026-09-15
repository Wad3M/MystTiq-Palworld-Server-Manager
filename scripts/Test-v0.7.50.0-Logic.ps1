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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.50.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.50\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.50.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.50\.0"' `
    'Versioning' 'app.manifest reports v0.7.50.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'Regression' 'v0.7.49.0 UE4SS install/rollback is still present' -Severity Critical

$monitoringText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessMonitoringService.cs') -Raw
$modServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.49.0 UE4SS install/rollback methods still present' `
    ($modServiceText -match 'public async Task<Ue4ssInstallPreview\?> PreviewUe4ssInstallAsync\(' -and
     $modServiceText -match 'public async Task<Ue4ssInstallResult> ApplyUe4ssInstallAsync\(' -and
     $modServiceText -match 'public async Task<Ue4ssInstallResult> RollbackUe4ssInstallAsync\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'GetLogTail / ResolveConsoleSources still present and unchanged in shape' `
    ($monitoringText -match 'public HeadlessLogTailSnapshot GetLogTail\(int requestedLines\)' -and
     $monitoringText -match 'private IEnumerable<\(string Label, string Path\)> ResolveConsoleSources\(\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.50.0 contract presence -- Console Source Completeness (UE4SS.log)
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.50.0 Contracts' 'ResolveConsoleSources includes UE4SS.log (modern and legacy layout paths)' `
    ($monitoringText -match 'Add\(result, "UE4SS\.log", Path\.Combine\(paths\.Ue4ssRoot, "UE4SS\.log"\)\);' -and
     $monitoringText -match 'Add\(result, "UE4SS\.log \(legacy layout\)", Path\.Combine\(paths\.RuntimeBinaryRoot, "UE4SS\.log"\)\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.50.0 Contracts' 'Pal.log source is retained, not removed (still checked, just no longer the only real-output hope)' `
    ($monitoringText -match 'Add\(result, "Pal\.log", Path\.Combine\(paths\.LogsRoot, "Pal\.log"\)\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.50.0 Contracts' 'UE4SS.log sources are added after the MystTiq stdout and Pal.log sources (merge order matches doc)' `
    ($monitoringText -match '(?s)Add\(result, "Pal\.log".*?Add\(result, "UE4SS\.log",.*?Add\(result, "UE4SS\.log \(legacy layout\)",.*?var adminLogs') `
    -Severity High

Test-MystTiqFile $ctx 'docs\architecture\v0.7.50.0-console-source-completeness.md' 'v0.7.50.0 Contracts' 'v0.7.50.0 architecture doc is present' -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.50.0 Contracts' 'Architecture doc discloses the live verification actually performed' `
    ((Get-Content (Join-Path $root 'docs\architecture\v0.7.50.0-console-source-completeness.md') -Raw) -match 'Live verification') `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.50.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.50\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.49.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.49.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.49.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.49.0\MystTiqPalworldServer_v0.7.49.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.49.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.49.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.49.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.49.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.49.0 checkpoint logic gate still passes' `
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

    # v0.7.50.0 adds no new routes and changes no existing route contract -- every server-side
    # route/CLI smoke test carried forward from prior versions is expected to pass unchanged.
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
