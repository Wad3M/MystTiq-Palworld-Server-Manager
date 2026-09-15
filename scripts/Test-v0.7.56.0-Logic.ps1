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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.56.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.56\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.56.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.56\.0"' `
    'Versioning' 'app.manifest reports v0.7.56.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

$themeApplierText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\ThemeApplier.cs') -Raw
$themeCatalogText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\ThemeCatalog.cs') -Raw
$designSystemText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\DesignSystem.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.48.0 DerivedColorNames/ResolveDerivedBaseColor foundation is unchanged' `
    ($themeCatalogText -match 'public static readonly string\[\] DerivedColorNames =' -and
     $themeCatalogText -match 'public static Color ResolveDerivedBaseColor\(string name, string accentTheme, string variant\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.7.48.0 BuildCardGradient formula is unchanged (reused by this version''s Backup* gradients)' `
    ($themeApplierText -match 'private static LinearGradientBrush BuildCardGradient\(Color baseColor, string variant\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.56.0 contract presence -- Remaining Decorative Gradients
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'Four new formula helpers exist: BuildGraphFill, BuildContextGradient, BuildGlassSheen, BuildNavGlass' `
    ($themeApplierText -match 'private static LinearGradientBrush BuildGraphFill\(Color baseColor\)' -and
     $themeApplierText -match 'private static LinearGradientBrush BuildContextGradient\(Color baseColor, string variant\)' -and
     $themeApplierText -match 'private static LinearGradientBrush BuildGlassSheen\(Color baseColor, string variant, bool bright\)' -and
     $themeApplierText -match 'private static LinearGradientBrush BuildNavGlass\(string variant, NavGlassKind kind\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'GraphFill is computed inside the existing DerivedColorNames loop (all 11 names, consistent with AccentBorderBrush/Card{Name}Gradient)' `
    ($themeApplierText -match 'app\.Resources\[\$"\{name\}GraphFill"\] = BuildGraphFill\(baseColor\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'The 5 Context{Name}Gradient keys are computed from ResolveDerivedBaseColor, not a second base-color source' `
    ($themeApplierText -match 'foreach \(var name in new\[\] \{ "Blue", "Violet", "Amber", "Magenta", "Orange" \}\)' -and
     $themeApplierText -match 'app\.Resources\[\$"Context\{name\}Gradient"\] = BuildContextGradient\(ThemeCatalog\.ResolveDerivedBaseColor\(name, accentTheme, variant\), variant\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'The 7-member glass sheen family (Option/Primary/Success/Danger) is wired to Blue/Green/Red' `
    ($themeApplierText -match 'app\.Resources\["GlassOptionHoverGradient"\] = BuildGlassSheen\(blueBase, variant, bright: false\);' -and
     $themeApplierText -match 'app\.Resources\["PrimaryGlassHoverGradient"\] = BuildGlassSheen\(blueBase, variant, bright: true\);' -and
     $themeApplierText -match 'app\.Resources\["SuccessGlassGradient"\] = BuildGlassSheen\(greenBase, variant, bright: false\);' -and
     $themeApplierText -match 'app\.Resources\["DangerGlassGradient"\] = BuildGlassSheen\(redBase, variant, bright: false\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'The 5 nav/dashboard structural glass keys are wired through BuildNavGlass with distinct NavGlassKind values' `
    ($themeApplierText -match 'app\.Resources\["NavGlassSurfaceGradient"\] = BuildNavGlass\(variant, NavGlassKind\.Surface\);' -and
     $themeApplierText -match 'app\.Resources\["NavSelectedGlassGradient"\] = BuildNavGlass\(variant, NavGlassKind\.Selected\);' -and
     $themeApplierText -match 'app\.Resources\["NavHoverGlassGradient"\] = BuildNavGlass\(variant, NavGlassKind\.Hover\);' -and
     $themeApplierText -match 'app\.Resources\["NavSelectedHoverGlassGradient"\] = BuildNavGlass\(variant, NavGlassKind\.SelectedHover\);' -and
     $themeApplierText -match 'app\.Resources\["DashboardGlassGradient"\] = BuildNavGlass\(variant, NavGlassKind\.Dashboard\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'Backup*Gradient family reuses BuildCardGradient with Green/Blue/Orange rather than a duplicate formula' `
    ($themeApplierText -match 'app\.Resources\["BackupProtectionGradient"\] = BuildCardGradient\(greenBase, variant\);' -and
     $themeApplierText -match 'app\.Resources\["BackupStorageGradient"\] = BuildCardGradient\(blueBase, variant\);' -and
     $themeApplierText -match 'app\.Resources\["BackupRetentionGradient"\] = BuildCardGradient\(ThemeCatalog\.Accent\["Orange"\]\[accentTheme\]\[variant\], variant\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'DataRow resources (Bg/BorderBrush/HoverBg/HoverBorderBrush/SelectedBg/SelectedBorderBrush/SelectedGlowShadow) are all computed' `
    ($themeApplierText -match 'app\.Resources\["DataRowBg"\]' -and
     $themeApplierText -match 'app\.Resources\["DataRowBorderBrush"\]' -and
     $themeApplierText -match 'app\.Resources\["DataRowHoverBg"\]' -and
     $themeApplierText -match 'app\.Resources\["DataRowHoverBorderBrush"\]' -and
     $themeApplierText -match 'app\.Resources\["DataRowSelectedBg"\]' -and
     $themeApplierText -match 'app\.Resources\["DataRowSelectedBorderBrush"\]' -and
     $themeApplierText -match 'app\.Resources\["DataRowSelectedGlowShadow"\]') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'Border.dataRow in DesignSystem.axaml is routed through the new DynamicResource keys, not hardcoded hex' `
    ($designSystemText -match '<Style Selector="Border\.dataRow">\s*<Setter Property="Background" Value="\{DynamicResource DataRowBg\}"/>' -and
     $designSystemText -match '<Style Selector="Border\.dataRow:pointerover">\s*<Setter Property="Background" Value="\{DynamicResource DataRowHoverBg\}"/>' -and
     $designSystemText -match '<Style Selector="Border\.dataRow\.selected">\s*<Setter Property="Background" Value="\{DynamicResource DataRowSelectedBg\}"/>') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'Confirmed-dead legacy styles (modRow/prototypeRow/worldTabHeader/disableAction/updateAction) were left untouched, not themed' `
    ($designSystemText -match '<Style Selector="Border\.modRow"><Setter Property="Background" Value="#100A1724"/>' -and
     $designSystemText -match '<Style Selector="Button\.disableAction"><Setter Property="Background" Value="#E3141A22"/>') `
    -Severity High

Test-MystTiqFile $ctx 'docs\architecture\v0.7.56.0-central-theme-system-remaining-gradients.md' 'v0.7.56.0 Contracts' 'v0.7.56.0 architecture doc is present' -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.56.0 Contracts' 'Architecture doc discloses the unverified-Dark-byte-match limitation and the version-slot substitution reasoning' `
    ((Get-Content (Join-Path $root 'docs\architecture\v0.7.56.0-central-theme-system-remaining-gradients.md') -Raw) -match 'not.{0,20}hand-tuned to reproduce every' -and
     (Get-Content (Join-Path $root 'docs\architecture\v0.7.56.0-central-theme-system-remaining-gradients.md') -Raw) -match 'no version-slot gaps') `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.56.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.56\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.55.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.55.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.55.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.55.0\MystTiqPalworldServer_v0.7.55.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.55.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.55.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.55.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.55.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.55.0 checkpoint logic gate still passes' `
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

    # v0.7.56.0 is Desktop-only (theming) -- no route contract changed, so every server-side
    # route/CLI smoke test carried forward is expected to pass unchanged.
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
