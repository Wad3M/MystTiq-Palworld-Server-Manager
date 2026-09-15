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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.15.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.15\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.15.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.15\.0"' `
    'Versioning' 'app.manifest reports v0.7.15.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs' 'Regression' 'v0.7.12.0 whitelist enforcement harness is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical

$designSystemText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\DesignSystem.axaml') -Raw
Add-MystTiqCheck $ctx 'Regression' 'v0.7.14.0 focus-visible fixes are still present' `
    ($designSystemText -match [regex]::Escape('Selector="ToggleButton.categoryTab:focus-visible"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.15.0 contract presence -- temporary bans & historical FPS charting
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\Models\TemporaryBanModels.cs' 'v0.7.15.0 Contracts' 'temporary ban models exist' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\HeadlessTemporaryBanService.cs' 'v0.7.15.0 Contracts' 'temporary ban service exists' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'v0.7.15.0 Contracts' 'route smoke script exists' -Severity Critical

$tempBanServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessTemporaryBanService.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.15.0 Contracts' 'HeadlessTemporaryBanService exposes BanAsync, EnforceAsync, and ForgetIfPresent (manual-unban cleanup)' `
    ($(
        $expected = @('public async Task<PlayerModerationResult> BanAsync', 'public async Task EnforceAsync', 'public void ForgetIfPresent')
        -not ($expected | Where-Object { $tempBanServiceText -notmatch [regex]::Escape($_) })
    )) `
    -Severity Critical

$serverProfileHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\ServerProfileHost.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.15.0 Contracts' 'ServerProfileHost exposes the TemporaryBans service' `
    ($serverProfileHostText -match 'public required HeadlessTemporaryBanService TemporaryBans') `
    -Severity Critical

$hostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.15.0 Contracts' 'temporary-ban routes and poll-driven enforcement are wired in' `
    ($(
        $expected = @('/players/temp-bans', '/players/{playerId}/temp-ban', 'p.TemporaryBans.EnforceAsync', 'p.TemporaryBans.ForgetIfPresent')
        -not ($expected | Where-Object { $hostText -notmatch [regex]::Escape($_) })
    )) `
    -Severity Critical

$historyServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessHistoricalMetricsService.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.15.0 Contracts' 'historical metrics sample and snapshot carry nullable FPS fields' `
    ($(
        $expected = @('double? ServerFps = null', 'double? AverageFps = null', 'double? PeakFps = null')
        -not ($expected | Where-Object { $historyServiceText -notmatch [regex]::Escape($_) })
    )) `
    -Severity Critical

$chartControlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Controls\ResourceHistoryChart.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.15.0 Contracts' 'ResourceHistoryChart plots a third FPS series, gapped across null samples' `
    ($chartControlText -match 'fpsPen' -and $chartControlText -match 'FpsPoint') `
    -Severity Critical

$mainWindowText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
Add-MystTiqCheck $ctx 'v0.7.15.0 Contracts' 'the "Whitelist -- BACKEND REQUIRED" and "Remove Admin -- BACKEND REQUIRED" stub buttons are gone' `
    (-not [regex]::IsMatch($mainWindowText, 'Whitelist\s*—\s*BACKEND REQUIRED') -and -not [regex]::IsMatch($mainWindowText, 'Remove Admin\s*—\s*BACKEND REQUIRED')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.15.0 Contracts' 'a real Temp Ban control and the new Temporary Bans card are present' `
    ($mainWindowText -match [regex]::Escape('Command="{Binding CreateTemporaryBanCommand}"') -and $mainWindowText -match [regex]::Escape('Command="{Binding ToggleTemporaryBansCommand}"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.15.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.15\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.14.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.14.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.14.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.14.0\MystTiqPalworldServer_v0.7.14.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.14.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.14.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.14.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.14.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.14.0 checkpoint logic gate still passes' `
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

    # Both need Build.ps1 DesktopWindows's sidecar output, the same known quirk every version's
    # -RunBuild already works around.
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

    Test-MystTiqCommand $ctx 'v0.7.15.0 New Tests' 'Temporary-ban and historical-FPS route smoke gate passes' {
        & (Join-Path $root 'scripts\Test-v0.7.15.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
