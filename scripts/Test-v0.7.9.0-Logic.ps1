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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.9.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.9\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.9.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$monitoringText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessMonitoringService.cs') -Raw
$dtoText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Models\MonitoringDtos.cs') -Raw
$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.8.0 RCON admin tools are still present' `
    ([regex]::IsMatch((Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw), 'MapPost\("/world/save-now"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.9.0 contract presence -- server-side
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.9.0 Contracts' 'GetGamePerformanceAsync exists, is best-effort (swallows exceptions to null,null)' `
    ([regex]::IsMatch($monitoringText, 'private async Task<\(double\? Fps, double\? FrameTimeMs\)> GetGamePerformanceAsync') -and
     [regex]::IsMatch($monitoringText, '(?s)GetGamePerformanceAsync.*?catch\s*\{\s*return \(null, null\);')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.9.0 Contracts' 'GetGamePerformanceAsync requests the metrics endpoint (not players)' `
    ([regex]::IsMatch($monitoringText, 'HttpMethod\.Get, "metrics"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.9.0 Contracts' 'HeadlessRuntimeMetricsSnapshot gained nullable ServerFps/ServerFrameTimeMs' `
    ([regex]::IsMatch($monitoringText, 'double\? ServerFps = null,\s*double\? ServerFrameTimeMs = null')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.9.0 Contracts' 'GetMetricsAsync threads gamePerformance into every return path (at least 4 usages)' `
    (([regex]::Matches($monitoringText, 'gamePerformance\.Fps')).Count -ge 4) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.9.0 contract presence -- Desktop
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.9.0 Contracts' 'RuntimeMetricsSnapshotDto gained serverFps/serverFrameTimeMs' `
    ([regex]::IsMatch($dtoText, '\[JsonPropertyName\("serverFps"\)\] public double\? ServerFps') -and
     [regex]::IsMatch($dtoText, '\[JsonPropertyName\("serverFrameTimeMs"\)\] public double\? ServerFrameTimeMs')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.9.0 Contracts' 'ApplyMetrics sets ServerFpsText/ServerFrameTimeText independently of the Available early-return' `
    ([regex]::IsMatch($vmText, '(?s)private void ApplyMetrics\(RuntimeMetricsSnapshotDto snapshot\)\s*\{\s*//.*?ServerFpsText = snapshot\.ServerFps\.HasValue.*?if \(!snapshot\.Available\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.9.0 Contracts' 'Monitoring page shows Server FPS and Frame Time cards' `
    ([regex]::IsMatch($axamlText, 'Text="Server FPS"') -and [regex]::IsMatch($axamlText, 'Text="Frame Time"') -and [regex]::IsMatch($axamlText, 'Binding ServerFpsText\}') -and [regex]::IsMatch($axamlText, 'Binding ServerFrameTimeText\}')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.9.0 Contracts' 'Activity & Audit stat grid was widened to 5 columns' `
    ([regex]::IsMatch($axamlText, 'ColumnDefinitions="\*,\*,\*,\*,\*" ColumnSpacing="14" IsVisible="\{Binding IsActivityAuditPage\}"')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.9.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.9\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.7.8.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.8.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.8.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.8.0\MystTiqPalworldServer_v0.7.8.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.8.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.8.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.8.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.8.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.8.0 checkpoint logic gate still passes' `
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
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
