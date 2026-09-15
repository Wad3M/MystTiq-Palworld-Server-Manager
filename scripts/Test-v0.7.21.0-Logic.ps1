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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.21.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.21\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.21.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.21\.0"' `
    'Versioning' 'app.manifest reports v0.7.21.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Services\MapPresetService.cs' 'Regression' 'v0.7.20.0 map preset service is still present' -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.21.0 contract presence -- experimental real-world map positions
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Services\PalworldMapCoordinates.cs' 'v0.7.21.0 Contracts' 'PalworldMapCoordinates exists' -Severity Critical

$coordText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\PalworldMapCoordinates.cs') -Raw

# v0.7.21.0's whole point was verifying the formula numerically rather than trusting a fetched
# source blind (the exact failure mode that sank the earlier v0.7.0.2 attempt). This check extracts
# the actual shipped constants and recomputes the source project's own published worked example
# (sav_to_map(-167230, 96430) -> (-134, -94)) fresh, so any future accidental drift in these
# constants is caught by the same verification this release was built on, not just a string match.
Add-MystTiqCheck $ctx 'v0.7.21.0 Contracts' 'the shipped coordinate constants reproduce the palworld-coord source project''s own worked example exactly' `
    ($(
        $translateXMatch = [regex]::Match($coordText, 'TranslateX\s*=\s*(-?[\d.]+)')
        $translateYMatch = [regex]::Match($coordText, 'TranslateY\s*=\s*(-?[\d.]+)')
        $scaleMatch = [regex]::Match($coordText, 'Scale\s*=\s*(-?[\d.]+)')
        if (-not ($translateXMatch.Success -and $translateYMatch.Success -and $scaleMatch.Success)) { $false }
        else {
            $translateX = [double]$translateXMatch.Groups[1].Value
            $translateY = [double]$translateYMatch.Groups[1].Value
            $scale = [double]$scaleMatch.Groups[1].Value
            $worldX = -167230.0; $worldY = 96430.0
            $mapX = [math]::Round(($worldY - $translateY) / $scale)
            $mapY = [math]::Round(($worldX + $translateX) / $scale)
            ($mapX -eq -134) -and ($mapY -eq -94)
        }
    )) `
    -Severity Critical

$mapPresetServiceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MapPresetService.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.21.0 Contracts' 'MapPresetService exposes TryGetPresetForPath' `
    ($mapPresetServiceText -match 'public MapPreset\? TryGetPresetForPath') `
    -Severity Critical

$viewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.21.0 Contracts' 'MainWindowViewModel exposes IsPalpagosMapActive and UseCalibratedWorldPositions, gated to Palpagos' `
    ($(
        $expected = @(
            'public bool IsPalpagosMapActive',
            'public bool UseCalibratedWorldPositions',
            'if (UseCalibratedWorldPositions && IsPalpagosMapActive)',
            'PalworldMapCoordinates.ToCanvasPosition'
        )
        -not ($expected | Where-Object { $viewModelText -notmatch [regex]::Escape($_) })
    )) `
    -Severity Critical

$mainWindowText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
Add-MystTiqCheck $ctx 'v0.7.21.0 Contracts' 'the World Map card has the experimental toggle, gated to IsPalpagosMapActive' `
    ($mainWindowText -match [regex]::Escape('IsChecked="{Binding UseCalibratedWorldPositions}"') -and $mainWindowText -match [regex]::Escape('IsVisible="{Binding IsPalpagosMapActive}"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.21.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.21\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.20.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.20.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.20.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.20.0\MystTiqPalworldServer_v0.7.20.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.20.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.20.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.20.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.20.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.20.0 checkpoint logic gate still passes' `
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

    # v0.7.21.0 is Desktop-only (experimental map calibration toggle), so every server-side route/CLI
    # smoke test carried forward from prior versions is expected to pass unchanged -- regression
    # evidence, not new ground. All need Build.ps1 DesktopWindows's sidecar output, the same known
    # quirk every version's -RunBuild already works around.
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
