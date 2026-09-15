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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.4.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.4\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.4.0' -Severity Critical

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

# ---------------------------------------------------------------------------
# 3. v0.7.4.0 contract presence -- card flare
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.4.0 Contracts' 'DesignSystem.axaml defines all 7 category accent style pairs, declared before the status-glow selectors' `
    ($(
        $accentIdx = $designSystemText.IndexOf('Border.card.accentHome')
        $glowIdx = $designSystemText.IndexOf('Border.card.glowGreen')
        $accentIdx -ge 0 -and $glowIdx -ge 0 -and $accentIdx -lt $glowIdx -and
        [regex]::IsMatch($designSystemText, 'Border\.card\.accentServer,\s*Border\.statuscard\.accentServer') -and
        [regex]::IsMatch($designSystemText, 'Border\.card\.accentWorld,\s*Border\.statuscard\.accentWorld') -and
        [regex]::IsMatch($designSystemText, 'Border\.card\.accentBackups,\s*Border\.statuscard\.accentBackups') -and
        [regex]::IsMatch($designSystemText, 'Border\.card\.accentMods,\s*Border\.statuscard\.accentMods') -and
        [regex]::IsMatch($designSystemText, 'Border\.card\.accentTools,\s*Border\.statuscard\.accentTools') -and
        [regex]::IsMatch($designSystemText, 'Border\.card\.accentSystem,\s*Border\.statuscard\.accentSystem')
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.4.0 Contracts' 'MainWindow.axaml tags a substantial number of cards with a category accent across every category' `
    ($(
        $counts = @{}
        foreach ($accent in @('accentHome','accentServer','accentWorld','accentBackups','accentMods','accentTools','accentSystem')) {
            $counts[$accent] = ([regex]::Matches($axamlText, [regex]::Escape($accent))).Count
        }
        ($counts.Values | Measure-Object -Sum).Sum -ge 150 -and @($counts.Values | Where-Object { $_ -eq 0 }).Count -eq 0
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.4.0 Contracts' 'the 3 Dashboard cards with a dynamic status-glow toggle were NOT also given a static accentHome class (no double-accent on the same element)' `
    (-not [regex]::IsMatch($axamlText, 'Classes="statuscard accentHome" Padding="10" Classes\.glowGreen') -and
     -not [regex]::IsMatch($axamlText, 'Classes="card accentHome" Padding="10" Classes\.glowGreen')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.4.0 Contracts' 'the Monitoring page splits its accent by sub-page: Console cards get accentServer, Activity and Audit cards get accentSystem' `
    ([regex]::IsMatch($axamlText, 'IsVisible="\{Binding IsConsolePage\}">\s*<Grid[^>]*RowDefinitions="Auto,Auto,\*"') -or
     [regex]::IsMatch($axamlText, '(?s)IsVisible="\{Binding IsConsolePage\}".{0,400}?accentServer')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.4.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.4\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.3.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.3.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.3.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.3.0\MystTiqPalworldServer_v0.7.3.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.3.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.3.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.3.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.3.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.3.0 checkpoint logic gate still passes' `
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
