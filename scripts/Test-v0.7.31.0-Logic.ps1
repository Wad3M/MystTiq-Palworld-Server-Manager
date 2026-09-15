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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.31.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.31\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.31.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.31\.0"' `
    'Versioning' 'app.manifest reports v0.7.31.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

$viewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
Add-MystTiqCheck $ctx 'Regression' 'v0.7.30.0 PopulateBackupItems/DoctorSummary fixes are still present' `
    ($viewModelText -match 'private void PopulateBackupItems' -and $viewModelText -match 'LatestDiagnosticsReport\.OverallHealthDetail') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.31.0 contract presence -- duplicate-install prevention
# ---------------------------------------------------------------------------
$tabSessionText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\TabSession.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.31.0 Contracts' 'TabSession exposes ServerRoot, DuplicateInstallWarning, and HasDuplicateInstallWarning' `
    ($tabSessionText -match 'public string ServerRoot' -and
     $tabSessionText -match 'public string DuplicateInstallWarning' -and
     $tabSessionText -match 'public bool HasDuplicateInstallWarning') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.31.0 Contracts' 'RecomputeDuplicateInstallWarnings groups tabs by ServerRoot case-insensitively' `
    ($viewModelText -match 'private void RecomputeDuplicateInstallWarnings\(\)' -and
     $viewModelText -match 'GroupBy\(t => t\.ServerRoot, StringComparer\.OrdinalIgnoreCase\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.31.0 Contracts' 'RecomputeDuplicateInstallWarnings is called from both the distribution-status refresh and CloseTab' `
    ($(
        $refreshStart = $viewModelText.IndexOf('var distribution = await _api.GetServerDistributionStatusAsync')
        $refreshHasCall = $refreshStart -ge 0 -and $viewModelText.Substring($refreshStart, [Math]::Min(900, $viewModelText.Length - $refreshStart)) -match 'RecomputeDuplicateInstallWarnings\(\);'
        $closeTabStart = $viewModelText.IndexOf('private void CloseTab(TabSession? tab)')
        $closeTabHasCall = $closeTabStart -ge 0 -and $viewModelText.Substring($closeTabStart, [Math]::Min(600, $viewModelText.Length - $closeTabStart)) -match 'RecomputeDuplicateInstallWarnings\(\);'
        $refreshHasCall -and $closeTabHasCall
    )) `
    -Severity Critical

$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
Add-MystTiqCheck $ctx 'v0.7.31.0 Contracts' 'Dashboard shows the duplicate-install warning banner' `
    ($mainWindowAxamlText -match 'IsVisible="\{Binding ActiveTab\.HasDuplicateInstallWarning\}"' -and
     $mainWindowAxamlText -match 'Text="\{Binding ActiveTab\.DuplicateInstallWarning\}"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.31.0-duplicate-install-prevention.md' 'v0.7.31.0 Contracts' 'v0.7.31.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.31.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.31\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.30.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.30.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.30.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.30.0\MystTiqPalworldServer_v0.7.30.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.30.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.30.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.30.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.30.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.30.0 checkpoint logic gate still passes' `
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

    # v0.7.31.0 is Desktop-only (duplicate-install detection built on data already transmitted), so
    # every server-side route/CLI smoke test carried forward from prior versions is expected to pass
    # unchanged -- regression evidence, not new ground. All need Build.ps1 DesktopWindows's sidecar
    # output, the same known quirk every version's -RunBuild already works around.
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
