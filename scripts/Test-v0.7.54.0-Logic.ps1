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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.54.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.54\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.54.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.54\.0"' `
    'Versioning' 'app.manifest reports v0.7.54.0' -Severity Critical

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
$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.53.0 Dashboard density values are still present' `
    ($mainWindowAxamlText -match '<StackPanel IsVisible="\{Binding IsDashboardPage\}" Spacing="6">') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'Existing category checks (IsV5HomeCategory/IsV5WorldCategory) are unchanged' `
    ($viewModelText -match 'public bool IsV5HomeCategory => SelectedPage == NavigationPage\.Dashboard;' -and
     $viewModelText -match 'public bool IsV5WorldCategory => SelectedPage is NavigationPage\.Inspector or NavigationPage\.WorldTransactions or NavigationPage\.Players or NavigationPage\.Bases or NavigationPage\.Guilds;') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.54.0 contract presence -- Per-Page Title Background Artwork
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.54.0 Contracts' 'Four Dark/Light per-category art bools are declared, combining category + IsLightMode' `
    ($viewModelText -match 'public bool IsHomePageArtDark => IsV5HomeCategory && !IsLightMode;' -and
     $viewModelText -match 'public bool IsHomePageArtLight => IsV5HomeCategory && IsLightMode;' -and
     $viewModelText -match 'public bool IsWorldPageArtDark => IsV5WorldCategory && !IsLightMode;' -and
     $viewModelText -match 'public bool IsWorldPageArtLight => IsV5WorldCategory && IsLightMode;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.54.0 Contracts' 'Art bools are re-raised on page navigation' `
    ([regex]::Matches($viewModelText, 'RaisePropertyChanged\(nameof\(Is(Home|World)PageArt(Dark|Light)\)\);').Count -ge 4) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.54.0 Contracts' 'Art bools are re-raised in the IsLightMode setter (theme-toggle aware)' `
    ($viewModelText -match '(?s)public bool IsLightMode\s*\{.*?RaisePropertyChanged\(nameof\(IsHomePageArtDark\)\);\s*\n\s*RaisePropertyChanged\(nameof\(IsHomePageArtLight\)\);\s*\n\s*RaisePropertyChanged\(nameof\(IsWorldPageArtDark\)\);\s*\n\s*RaisePropertyChanged\(nameof\(IsWorldPageArtLight\)\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.54.0 Contracts' 'Four Image elements are layered behind the page header, each gated by its own art bool' `
    ($mainWindowAxamlText -match 'Source="/Assets/page-art-home-dark\.jpg".*?IsVisible="\{Binding IsHomePageArtDark\}"' -and
     $mainWindowAxamlText -match 'Source="/Assets/page-art-home-light\.jpg".*?IsVisible="\{Binding IsHomePageArtLight\}"' -and
     $mainWindowAxamlText -match 'Source="/Assets/page-art-world-dark\.jpg".*?IsVisible="\{Binding IsWorldPageArtDark\}"' -and
     $mainWindowAxamlText -match 'Source="/Assets/page-art-world-light\.jpg".*?IsVisible="\{Binding IsWorldPageArtLight\}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.54.0 Contracts' 'All four artwork assets exist on disk' `
    ((Test-Path (Join-Path $root 'src\MystTiq.Desktop\Assets\page-art-home-dark.jpg') -PathType Leaf) -and
     (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Assets\page-art-home-light.jpg') -PathType Leaf) -and
     (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Assets\page-art-world-dark.jpg') -PathType Leaf) -and
     (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Assets\page-art-world-light.jpg') -PathType Leaf)) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.54.0 Contracts' 'Assets folder glob still covers the new files (no .csproj change needed)' `
    ((Get-Content (Join-Path $root 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -Raw) -match 'AvaloniaResource Include="Assets/\*\*"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.54.0-per-page-title-background-artwork.md' 'v0.7.54.0 Contracts' 'v0.7.54.0 architecture doc is present' -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.54.0 Contracts' 'Architecture doc discloses the daily generation-limit gap and which categories are missing art' `
    ((Get-Content (Join-Path $root 'docs\architecture\v0.7.54.0-per-page-title-background-artwork.md') -Raw) -match 'daily anonymous-generation cap') `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.54.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.54\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.53.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.53.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.53.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.53.0\MystTiqPalworldServer_v0.7.53.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.53.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.53.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.53.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.53.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.53.0 checkpoint logic gate still passes' `
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

    # v0.7.54.0 is Desktop-only (page-header artwork) -- no route contract changed, so every
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
