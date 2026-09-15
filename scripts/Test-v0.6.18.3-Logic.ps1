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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.18.3' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.18\.3</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.18.3' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.18.2 IsCreatingNewProfile / Server Setup cleanup is still present' `
    ([regex]::IsMatch($desktopText, 'IsCreatingNewProfile')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.18.3 contract presence -- Configuration page overhaul
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.18.3 Contracts' 'DisplayValue exists and is gated to the four known quoted-string setting names' `
    ([regex]::IsMatch($desktopText, 'QuotedStringNames\s*=\s*new\(StringComparer\.OrdinalIgnoreCase\)[\s\S]{0,120}"ServerName"[\s\S]{0,120}"ServerDescription"[\s\S]{0,120}"AdminPassword"[\s\S]{0,120}"ServerPassword"')) `
    -Severity Critical -Details 'A fixed, known-by-name list -- not runtime quote-sniffing, which would be fragile once a value has been edited once.'

Add-MystTiqCheck $ctx 'v0.6.18.3 Contracts' 'Advanced Settings live-highlights values that differ from default' `
    ([regex]::IsMatch($desktopText, 'IsDifferentFromDefault') -and [regex]::IsMatch($desktopText, 'Classes\.differsFromDefault="\{Binding IsDifferentFromDefault\}"')) `
    -Severity Critical -Details 'Live, not the server-computed-at-load-time IsModified flag -- updates as the user edits a value in the current session.'

Add-MystTiqCheck $ctx 'v0.6.18.3 Contracts' 'QoL preset selection auto-applies; the manual Apply Preset button is gone' `
    (-not [regex]::IsMatch($desktopText, 'ApplyConfigPresetCommand') -and [regex]::IsMatch($desktopText, 'ApplySelectedConfigurationPreset\(\);\s*\r?\n\s*\}') -and [regex]::IsMatch($desktopText, 'public string SelectedConfigPreset')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.18.3 Contracts' 'LocalConfigPresetStore exists, mirroring LocalMapPreferencesStore''s local-storage shape' `
    ([regex]::IsMatch($desktopText, 'class LocalConfigPresetStore') -and [regex]::IsMatch($desktopText, 'config-presets\.json') -and [regex]::IsMatch($desktopText, 'SaveCurrentAsPresetCommand')) `
    -Severity Critical -Details 'Desktop-local only, not synced across machines/profiles -- same pattern as connection profiles and map preferences.'

Add-MystTiqCheck $ctx 'v0.6.18.3 Contracts' 'Simple Settings is data-driven off the full 34-name curated list, not the old 8-item array' `
    ([regex]::IsMatch($desktopText, 'SimpleRateDefinitions') -and [regex]::IsMatch($desktopText, 'SimpleToggleDefinitions') -and [regex]::IsMatch($desktopText, 'SimpleNetworkDefinitions') -and [regex]::IsMatch($desktopText, 'class PalworldSimpleToggleItem')) `
    -Severity Critical -Details 'Reuses the pre-existing SimpleConfigurationNames curation (identity/credentials handled separately, 22 rate sliders, 2 toggles, 6 network/limit fields -- 34 total) rather than re-curating from scratch.'

Add-MystTiqCheck $ctx 'v0.6.18.3 Contracts' 'Search/filter now also drives the Simple view, not just Advanced' `
    ([regex]::IsMatch($desktopText, 'RebuildSimplePalworldSettings\(search, category\)') -and [regex]::IsMatch($desktopText, 'MatchesConfigFilter')) `
    -Severity Critical -Details 'Previously Simple Settings was a separate, unfiltered collection that ignored ConfigSearchText/SelectedConfigCategory entirely.'

Add-MystTiqCheck $ctx 'v0.6.18.3 Contracts' 'Both dead IsVisible=False legacy blocks on the Configuration page are gone' `
    (-not [regex]::IsMatch($desktopText, 'IsVisible="False"[\s\S]{0,400}PalServer" Foreground') -and -not [regex]::IsMatch($desktopText, 'IsVisible="False"[\s\S]{0,400}Uptime" Foreground')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.18.3 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.18\.3')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.18.2 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.18.2-Logic.ps1' `
    'Regression Baseline' 'v0.6.18.2 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.18.2\MystTiqPalworldServer_v0.6.18.2_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.18.2 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.18.2-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.18.2-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.18.2-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.18.2 checkpoint logic gate still passes' `
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
