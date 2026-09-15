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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.51.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.51\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.51.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.51\.0"' `
    'Versioning' 'app.manifest reports v0.7.51.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\HeadlessMonitoringService.cs' 'Regression' 'v0.7.50.0 console source fix is still present' -Severity Critical

$profileText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Models\ConnectionProfile.cs') -Raw
$catalogText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\ThemeCatalog.cs') -Raw
$tabSessionText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\TabSession.cs') -Raw
$viewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.50.0 UE4SS.log console sources still present' `
    ($(Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessMonitoringService.cs') -Raw) -match 'Add\(result, "UE4SS\.log"') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.51.0 contract presence -- Per-Tab Color Coding
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.51.0 Contracts' 'ConnectionProfile has an AccentColorKey with a safe default (backward-compatible deserialization)' `
    ($profileText -match 'string AccentColorKey = "Blue"\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.51.0 Contracts' 'ThemeCatalog exposes the 10-color assignable identity pool, excluding Neutral' `
    ($catalogText -match 'public static readonly string\[\] TabIdentityColorNames =\s*\n\s*\["Blue", "Cyan", "Violet", "Magenta", "Orange", "Green", "Amber", "Red", "Purple", "DarkGreen"\];') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.51.0 Contracts' 'TabSession resolves AccentBrush from the live theme resource and exposes a refresh method' `
    ($tabSessionText -match 'public IBrush AccentBrush' -and
     $tabSessionText -match 'public void RefreshAccentVisual\(\)' -and
     $tabSessionText -match 'Application\.Current\?\.TryGetResource\(resourceKey, null, out var resource\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.51.0 Contracts' 'Profile setter triggers an accent refresh (new tab / reconnect picks up its color immediately)' `
    ($tabSessionText -match '(?s)public ConnectionProfile\? Profile\s*\{.*?RefreshAccentVisual\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.51.0 Contracts' 'MainWindowViewModel resolves and reuses an existing profile''s color rather than reshuffling it on every save' `
    ($viewModelText -match 'private string ResolveAccentColorKey\(string\? existingId, ConnectionProfile\? tabProfile\)' -and
     $viewModelText -match 'if \(tabProfile is \{ AccentColorKey\.Length: > 0 \}\) return tabProfile\.AccentColorKey;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.51.0 Contracts' 'Both profile-construction call sites resolve AccentColorKey before building the record' `
    ($viewModelText -match 'var accentColorKey = ResolveAccentColorKey\(existingId, tab\.Profile\);' -and
     $viewModelText -match 'var accentColorKey = ResolveAccentColorKey\(existingId, ActiveTab\?\.Profile\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.51.0 Contracts' 'Every open tab is re-notified after a theme change (accent brushes are not {DynamicResource}, so they need an explicit nudge)' `
    ([regex]::Matches($viewModelText, 'ThemeApplier\.Apply\(_selectedAccentTheme, _selectedThemeVariant\);\s*\n\s*_themeStore\.Save\(_selectedAccentTheme, _selectedThemeVariant\);\s*\n\s*RefreshTabAccentVisuals\(\);').Count -eq 2) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.51.0 Contracts' 'Tab strip template renders the accent stripe as its own column, distinct from the status dot' `
    ($mainWindowAxamlText -match '<Grid ColumnDefinitions="4,10,\*,Auto" RowDefinitions="Auto,Auto">' -and
     $mainWindowAxamlText -match '<Rectangle Grid\.RowSpan="2" Width="3" Height="22".*?Fill="\{Binding AccentBrush\}"' -and
     $mainWindowAxamlText -match '<Ellipse Grid\.Column="1" Grid\.RowSpan="2" Width="8" Height="8" Fill="\{Binding StatusDotColor\}"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.51.0-per-tab-color-coding.md' 'v0.7.51.0 Contracts' 'v0.7.51.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.51.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.51\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.50.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.50.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.50.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.50.0\MystTiqPalworldServer_v0.7.50.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.50.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.50.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.50.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.50.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.50.0 checkpoint logic gate still passes' `
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

    # v0.7.51.0 is Desktop-only (tab-strip UI + ConnectionProfile model) -- no route contract
    # changed, so every server-side route/CLI smoke test carried forward is expected to pass unchanged.
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
