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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.5.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.5\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.5.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$designSystemText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\DesignSystem.axaml') -Raw
$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.3.0 wizard is still present' `
    ([regex]::IsMatch($vmText, 'public int NewServerWizardStep')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.7.4.0 card-flare accent styles are still present' `
    ([regex]::IsMatch($designSystemText, 'Border\.card\.accentHome')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.5.0 contract presence -- wizard bleed-through bug fix
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'the Managed Server Configuration card immediately precedes a Managed Server / Headless Configuration heading and is gated behind !IsCreatingNewProfile' `
    ([regex]::IsMatch($axamlText, '(?s)Border Classes="card accentSystem" IsVisible="\{Binding !IsCreatingNewProfile\}">\s*<StackPanel Spacing="12">\s*<Grid ColumnDefinitions="\*,Auto">\s*<StackPanel>\s*<TextBlock Text="Managed Server / Headless Configuration"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'both the Managed Server Configuration and Security cards on the Settings page now carry the !IsCreatingNewProfile gate' `
    (([regex]::Matches($axamlText, [regex]::Escape('Border Classes="card accentSystem" IsVisible="{Binding !IsCreatingNewProfile}"'))).Count -ge 2) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.5.0 contract presence -- global denser spacing
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'Border.card default padding was tightened to 11' `
    ([regex]::IsMatch($designSystemText, '(?s)Selector="Border\.card">.*?Padding" Value="11"')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'the shared Button/TextBox/ComboBox minimum height was tightened to 31' `
    (([regex]::Matches($designSystemText, 'MinHeight" Value="31"')).Count -ge 3) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'no page-root stack still uses the old Spacing="16"' `
    (-not [regex]::IsMatch($axamlText, 'IsVisible="\{Binding Is\w+Page\}" Spacing="16"')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'page-root stacks now use the tightened Spacing="12"' `
    (([regex]::Matches($axamlText, 'IsVisible="\{Binding Is\w+Page\}" Spacing="12"')).Count -ge 15) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. v0.7.5.0 contract presence -- collapsible secondary sections
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'Button.sectionToggle style exists in DesignSystem.axaml' `
    ([regex]::IsMatch($designSystemText, 'Selector="Button\.sectionToggle"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'all 6 collapsible sections are wired: World Map, Pal Editor, WAN Diagnostics, Local Diagnostics, Discord Bot, Anti-Cheat' `
    ($(
        $expected = @('ToggleWorldMapCommand','TogglePalEditorCommand','ToggleWanDiagnosticsCommand','ToggleLocalDiagnosticsCommand','ToggleDiscordBotCommand','ToggleAntiCheatCommand')
        $axamlHasAll = -not ($expected | Where-Object { $axamlText -notmatch [regex]::Escape($_) })
        $vmHasAll = -not ($expected | Where-Object { $vmText -notmatch [regex]::Escape($_) })
        $axamlHasAll -and $vmHasAll
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'all 6 expanded-state bool properties exist on MainWindowViewModel' `
    ($(
        $expected = @('IsWorldMapExpanded','IsPalEditorExpanded','IsWanDiagnosticsExpanded','IsLocalDiagnosticsExpanded','IsDiscordBotExpanded','IsAntiCheatExpanded')
        -not ($expected | Where-Object { $vmText -notmatch "public bool $_" })
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.5.0 Contracts' 'the always-primary sections (Network Diagnostics, Threshold Rules) were NOT made collapsible' `
    (-not [regex]::IsMatch($axamlText, 'ToggleNetworkDiagnosticsCommand') -and -not [regex]::IsMatch($axamlText, 'ToggleThresholdRulesCommand')) `
    -Severity Medium

# ---------------------------------------------------------------------------
# 6. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.5.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.5\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 7. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 8. Existing v0.7.4.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.4.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.4.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 9. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.4.0\MystTiqPalworldServer_v0.7.4.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.4.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.4.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.4.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.4.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.4.0 checkpoint logic gate still passes' `
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
