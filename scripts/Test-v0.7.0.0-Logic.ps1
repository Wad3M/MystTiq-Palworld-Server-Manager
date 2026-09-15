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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.0.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.0\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.0.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\ViewModels\TabSession.cs' 'Regression' 'v0.6.19.0 TabSession.cs is still present' -Severity Critical

$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$designSystemText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\DesignSystem.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.6.18.4 ServerIsRunning contract is still present' `
    ([regex]::IsMatch($vmText, 'NativeProcessId\.HasValue\s*\|\|\s*status\.Ready')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.19.0 Tabs/ActiveTab tab bar is still present' `
    ([regex]::IsMatch($axamlText, 'ItemsSource="\{Binding Tabs\}"\s+SelectedItem="\{Binding ActiveTab\}"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.0.0 contract presence -- Theme engine
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Services\ThemeCatalog.cs' 'v0.7.0.0 Contracts' 'ThemeCatalog.cs exists' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Services\ThemeApplier.cs' 'v0.7.0.0 Contracts' 'ThemeApplier.cs exists' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Services\LocalThemePreferencesStore.cs' 'v0.7.0.0 Contracts' 'LocalThemePreferencesStore.cs exists' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Styles\IconGeometries.axaml' 'v0.7.0.0 Contracts' 'IconGeometries.axaml exists' -Severity Critical

$catalogText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\ThemeCatalog.cs') -Raw
$applierText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\ThemeApplier.cs') -Raw
$themeStoreText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\LocalThemePreferencesStore.cs') -Raw
$iconText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\IconGeometries.axaml') -Raw

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'ThemeCatalog defines 4 accent themes and 2 variants' `
    ([regex]::IsMatch($catalogText, 'AccentThemes\s*=\s*\["Default",\s*"Emerald",\s*"Crimson",\s*"Violet"\]') -and
     [regex]::IsMatch($catalogText, 'Variants\s*=\s*\["Dark",\s*"Light"\]')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'Default+Dark gradient-stop baseline is byte-for-byte the pre-v0.7.0.0 hardcoded value (ButtonGradientStop0 sample)' `
    ([regex]::IsMatch($catalogText, '"ButtonGradientStop0"\]\s*=\s*Combo\(\("#FF27455E"')) `
    -Severity Critical -Details 'The literal #27455E was ButtonGradient''s original hardcoded first stop before this release; the Default/Dark catalog entry must still be exactly that value so the baseline look is unchanged until a theme is actually picked.'

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'ThemeApplier writes structural/semantic/accent/gradient-stop resources onto Application.Current.Resources and syncs RequestedThemeVariant' `
    ([regex]::IsMatch($applierText, 'app\.Resources\[key\]\s*=\s*byVariant\[variant\]') -and
     [regex]::IsMatch($applierText, 'app\.Resources\[key\]\s*=\s*byTheme\[accentTheme\]\[variant\]') -and
     [regex]::IsMatch($applierText, 'RequestedThemeVariant\s*=')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'LocalThemePreferencesStore follows the established JSON preference-store pattern (atomic write, try-catch-swallow load, MystTiq config dir)' `
    ([regex]::IsMatch($themeStoreText, 'theme-preferences\.json') -and
     [regex]::IsMatch($themeStoreText, 'File\.Move\(temp,\s*StoragePath,\s*overwrite:\s*true\)') -and
     [regex]::IsMatch($themeStoreText, 'catch\s*\{')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'MainWindowViewModel exposes SelectedAccentTheme/IsLightMode wired to ThemeApplier and the theme store' `
    ([regex]::IsMatch($vmText, 'public\s+string\s+SelectedAccentTheme') -and
     [regex]::IsMatch($vmText, 'public\s+bool\s+IsLightMode') -and
     [regex]::IsMatch($vmText, 'ThemeApplier\.Apply\(_selectedAccentTheme,\s*_selectedThemeVariant\)') -and
     [regex]::IsMatch($vmText, '_themeStore\.Save\(_selectedAccentTheme,\s*_selectedThemeVariant\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'App.axaml.cs applies the persisted theme at startup before the window is constructed' `
    ([regex]::IsMatch((Get-Content (Join-Path $root 'src\MystTiq.Desktop\App.axaml.cs') -Raw), 'ThemeApplier\.Apply\(accentTheme,\s*variant\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'Settings page has an Appearance card with the light-mode toggle and accent theme picker' `
    ([regex]::IsMatch($axamlText, 'Text="Appearance"') -and
     [regex]::IsMatch($axamlText, 'IsChecked="\{Binding IsLightMode\}"') -and
     [regex]::IsMatch($axamlText, 'SetAccentThemeCommand')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'the ~20 primary-chrome gradients reference DynamicResource stops, not hardcoded hex, in DesignSystem.axaml' `
    ([regex]::IsMatch($designSystemText, 'GradientStop Offset="0"\s*Color="\{DynamicResource ButtonGradientStop0\}"') -and
     [regex]::IsMatch($designSystemText, 'GradientStop Offset="0"\s*Color="\{DynamicResource CategoryActiveGradientStop0\}"') -and
     [regex]::IsMatch($designSystemText, 'GradientStop Offset="0"\s*Color="\{DynamicResource CardGradientStop0\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'IconGeometries.axaml defines the 8 hand-authored icons' `
    ([regex]::IsMatch($iconText, 'x:Key="IconDashboard"') -and
     [regex]::IsMatch($iconText, 'x:Key="IconServer"') -and
     [regex]::IsMatch($iconText, 'x:Key="IconPlayers"') -and
     [regex]::IsMatch($iconText, 'x:Key="IconFleet"') -and
     [regex]::IsMatch($iconText, 'x:Key="IconSecurity"') -and
     [regex]::IsMatch($iconText, 'x:Key="IconBase"') -and
     [regex]::IsMatch($iconText, 'x:Key="IconBackup"') -and
     [regex]::IsMatch($iconText, 'x:Key="IconAddTabRing"') -and
     [regex]::IsMatch($iconText, 'x:Key="IconAddTabDot"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'App.axaml includes the new IconGeometries style dictionary' `
    ([regex]::IsMatch((Get-Content (Join-Path $root 'src\MystTiq.Desktop\App.axaml') -Raw), 'StyleInclude Source="avares://MystTiq\.Desktop/Styles/IconGeometries\.axaml"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'the new icons are actually referenced from MainWindow.axaml' `
    ([regex]::IsMatch($axamlText, 'StaticResource IconDashboard') -and
     [regex]::IsMatch($axamlText, 'StaticResource IconServer') -and
     [regex]::IsMatch($axamlText, 'StaticResource IconPlayers') -and
     [regex]::IsMatch($axamlText, 'StaticResource IconFleet') -and
     [regex]::IsMatch($axamlText, 'StaticResource IconSecurity') -and
     [regex]::IsMatch($axamlText, 'StaticResource IconBase') -and
     [regex]::IsMatch($axamlText, 'StaticResource IconBackup') -and
     [regex]::IsMatch($axamlText, 'StaticResource IconAddTabRing')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.0.0 Contracts' 'inline status Foreground/Fill colors were migrated to theme-aware brushes (no bare status hex left on MainWindow.axaml)' `
    (-not [regex]::IsMatch($axamlText, 'Foreground="#(63DF7B|F4B83F|A997FF|969AAD|777C90|8CA8C2)"')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.0.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.0\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.19.1 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.19.1-Logic.ps1' `
    'Regression Baseline' 'v0.6.19.1 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.19.1\MystTiqPalworldServer_v0.6.19.1_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.19.1 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.19.1-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.19.1-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.19.1-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.19.1 checkpoint logic gate still passes' `
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
