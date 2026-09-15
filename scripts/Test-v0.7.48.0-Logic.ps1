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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.48.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.48\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.48.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.48\.0"' `
    'Versioning' 'app.manifest reports v0.7.48.0' -Severity Critical

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
$designSystemText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\DesignSystem.axaml') -Raw
$themeCatalogText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\ThemeCatalog.cs') -Raw
$themeApplierText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\ThemeApplier.cs') -Raw
$componentServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessComponentUpdateService.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.47.0 UE4SS release catalog and v0.7.45.0 Update Center are still present' `
    ((Test-Path (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessUe4ssReleaseCatalogService.cs') -PathType Leaf) -and
     $componentServiceText -match 'CheckUe4ssAsync') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'ThemeApplier still writes the original Structural/Semantic/Accent/GradientStops dictionaries' `
    ($themeApplierText -match 'foreach \(var \(key, byVariant\) in ThemeCatalog\.Structural\)' -and
     $themeApplierText -match 'foreach \(var \(key, byTheme\) in ThemeCatalog\.Accent\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.48.0 contract presence -- Central Theme System Completion, Foundation Pass
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'ThemeColorMath exists with Blend/Lighten/Darken/WithAlpha helpers' `
    ($(
        $mathText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\ThemeColorMath.cs') -Raw
        $mathText -match 'public static class ThemeColorMath' -and
        $mathText -match 'public static Color Blend\(' -and
        $mathText -match 'public static Color Lighten\(' -and
        $mathText -match 'public static Color Darken\(' -and
        $mathText -match 'public static Color WithAlpha\('
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'ThemeCatalog declares the 11 derived base colors with a resolver' `
    ($themeCatalogText -match 'public static readonly string\[\] DerivedColorNames =\s*\n\s*\["Blue", "Cyan", "Violet", "Magenta", "Orange", "Green", "Amber", "Red", "Purple", "DarkGreen", "Neutral"\];' -and
     $themeCatalogText -match 'public static Color ResolveDerivedBaseColor\(string name, string accentTheme, string variant\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'ThemeApplier computes per-color BorderBrush/HighlightBrush/GlowShadow/CardGradient resources' `
    ($themeApplierText -match 'app\.Resources\[\$"\{name\}AccentBorderBrush"\]' -and
     $themeApplierText -match 'app\.Resources\[\$"\{name\}AccentHighlightBrush"\]' -and
     $themeApplierText -match 'app\.Resources\[\$"\{name\}AccentGlowShadowLow"\]' -and
     $themeApplierText -match 'app\.Resources\[\$"\{name\}AccentGlowShadowHigh"\]' -and
     $themeApplierText -match 'app\.Resources\[\$"Card\{name\}Gradient"\]') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'BorderBrush and HighlightBrush use distinct derivations (muted vs. bright), not the same value' `
    ($themeApplierText -match 'ThemeColorMath\.Blend\(baseColor, structuralBorder, 0\.55\)' -and
     $themeApplierText -match 'ThemeColorMath\.Lighten\(baseColor, 0\.5\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'Inspect/Target gradients (v0.7.35.0) are now wired into ThemeCatalog.GradientStops' `
    ($themeCatalogText -match '\["InspectGradientStop0"\] = Combo\(' -and
     $themeCatalogText -match '\["TargetGradientStop0"\] = Combo\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'Page-accent-family classes reference derived resources, not hardcoded hex, for BorderBrush/BoxShadow' `
    ($designSystemText -match 'Border\.card\.accentHome, Border\.statuscard\.accentHome">\s*\n\s*<Setter Property="BorderBrush" Value="\{DynamicResource BlueAccentBorderBrush\}"/>\s*\n\s*<Setter Property="BoxShadow" Value="\{DynamicResource BlueAccentGlowShadowLow\}"/>' -and
     $designSystemText -notmatch 'BorderBrush" Value="#3D6E96"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'Status glow classes (glowGreen/Red/Amber/Purple/DarkGreen/Neutral) reference derived resources' `
    ($designSystemText -match 'Border\.glowGreen">\s*\n\s*<Setter Property="Background" Value="\{DynamicResource CardGreenGradient\}"/>\s*\n\s*<Setter Property="BorderBrush" Value="\{DynamicResource GreenAccentBorderBrush\}"/>' -and
     $designSystemText -match 'Border\.glowNeutral">\s*\n\s*<Setter Property="Background" Value="\{DynamicResource CardNeutralGradient\}"/>\s*\n\s*<Setter Property="BorderBrush" Value="\{DynamicResource NeutralAccentBorderBrush\}"/>') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'General interactive glow color is unified (no more literal #35B9FF DropShadowEffect colors)' `
    ($designSystemText -notmatch 'Color="#35B9FF"' -and
     $designSystemText -match 'Color="\{DynamicResource Blue\}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'MainWindow.axaml structural literals are converted; the 3 terminal-style consoles (5 attribute occurrences) are a disclosed, deliberate exception' `
    ($(
        $remaining = [regex]::Matches($mainWindowAxamlText, '(Background|BorderBrush|Foreground|Fill)="#[0-9A-Fa-f]{6,8}"')
        $remaining.Count -eq 5
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.48.0 Contracts' 'UE4SS Update Center row queries the full release list instead of a hardcoded stale tag' `
    ($componentServiceText -match 'var releases = await TryGetJsonAsync<List<GitHubReleaseDto>>\(\$"https://api\.github\.com/repos/\{Ue4ssRepo\}/releases", ct\);' -and
     $componentServiceText -notmatch 'Ue4ssReleaseTag') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.48.0-central-theme-system-foundation.md' 'v0.7.48.0 Contracts' 'v0.7.48.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.48.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.48\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.47.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.47.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.47.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.47.0\MystTiqPalworldServer_v0.7.47.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.47.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.47.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.47.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.47.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.47.0 checkpoint logic gate still passes' `
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

    # v0.7.48.0 is almost entirely Desktop-only theming/XAML (plus one small, unrelated HeadlessHost
    # fix for UE4SS staleness) -- no existing route contract changed, so every server-side route/CLI
    # smoke test carried forward from prior versions is expected to pass unchanged.
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
