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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.1.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.1\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.1.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Services\ThemeCatalog.cs' 'Regression' 'v0.7.0.0 ThemeCatalog.cs is still present' -Severity Critical

$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$tabSessionText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\TabSession.cs') -Raw
$iconText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\IconGeometries.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.0.1 Fleet nav-sidebar fix is still present' `
    ([regex]::IsMatch($vmText, 'IsV5SystemCategory\s*=>\s*SelectedPage is[^;]*?or\s+NavigationPage\.Fleet\s*;')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.1.0 contract presence -- page ordering, tab bar, Simple Settings gap indicator
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'Players: World Map card now follows the player list/detail Grid instead of preceding it' `
    ([regex]::IsMatch($axamlText, '(?s)FilteredPlayerRecords.*?WORLD MAP')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'Backups: top action bar no longer duplicates Verify/Restore/Delete Selected' `
    (-not [regex]::IsMatch($axamlText, 'Create Backup[\s\S]{0,400}Verify Selected')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'Automation: Rules grid now precedes the New Rule form' `
    ([regex]::IsMatch($axamlText, '(?s)Text="Rules".*?Text="New Rule"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'Monitoring: CPU/RAM/Threads stat row now precedes the Activity/Audit log card' `
    ([regex]::IsMatch($axamlText, '(?s)Text="PalServer RAM".*?MystTiq Activity / Audit Log')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'Fleet: Server Profiles now precedes Clone World' `
    ([regex]::IsMatch($axamlText, '(?s)Text="Server Profiles".*?Text="Clone World"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'Diagnostics Center: Restart Server is danger-styled and separated from read-only actions' `
    ([regex]::IsMatch($axamlText, 'Classes="danger" Content="Restart Server"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'Settings no longer has editable Server Paths TextBoxes duplicating Workspace (read-only summary + Workspace link instead)' `
    ([regex]::IsMatch($axamlText, 'Manage in Workspace') -and
     -not [regex]::IsMatch($axamlText, 'Text="Server Paths"[\s\S]{0,300}TextBox Grid\.Column="1" Text="\{Binding ConfigServerRoot\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'Pal Editor relocated onto the Players page (RefreshPalsCommand no longer inside the Guilds sub-page)' `
    ([regex]::IsMatch($axamlText, '(?s)IsPlayersPage\}".*?Pal Editor.*?IsMonitoringPage')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'Simple Settings gap indicator: computed independent of search/category, surfaced in the ViewModel and bound in XAML' `
    ([regex]::IsMatch($vmText, 'public string SimpleSettingsGapText') -and
     [regex]::IsMatch($vmText, 'public bool HasSimpleSettingsGap') -and
     [regex]::IsMatch($axamlText, 'IsVisible="\{Binding HasSimpleSettingsGap\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' 'TabSession exposes ConnectionKindText (Local/Remote), bound in the tab bar instead of ServerUrl' `
    ([regex]::IsMatch($tabSessionText, 'public string ConnectionKindText') -and
     [regex]::IsMatch($axamlText, 'Text="\{Binding ConnectionKindText\}"') -and
     -not [regex]::IsMatch($axamlText, 'Text="\{Binding ServerUrl\}" FontSize="10"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.1.0 Contracts' '"+" tab button uses a plain plus icon (IconAddTab), not the old two-part capture-sphere icon' `
    ([regex]::IsMatch($iconText, 'x:Key="IconAddTab"') -and
     -not [regex]::IsMatch($iconText, 'IconAddTabRing') -and
     [regex]::IsMatch($axamlText, 'StaticResource IconAddTab\}')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.1.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.1\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.0.1 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.0.1-Logic.ps1' `
    'Regression Baseline' 'v0.7.0.1 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.0.1\MystTiqPalworldServer_v0.7.0.1_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.0.1 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.0.1-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.0.1-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.0.1-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.0.1 checkpoint logic gate still passes' `
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
