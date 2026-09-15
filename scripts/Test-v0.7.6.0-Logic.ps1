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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.6.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.6\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.6.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$tabText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\TabSession.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.4.0 card-flare accent styles are still present' `
    ([regex]::IsMatch((Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\DesignSystem.axaml') -Raw), 'Border\.card\.accentHome')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.7.5.0 collapsible section pattern is still present' `
    ([regex]::IsMatch($vmText, 'IsWorldMapExpanded')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.6.0 contract presence -- stale-tab-data fix
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.6.0 Contracts' 'ActiveTab setter clears all 5 destructive-action selections before refreshing' `
    ($(
        $expected = @('SelectedOnlinePlayer = null','SelectedBackup = null','SelectedMod = null','SelectedExplorerPal = null','SelectedExplorerBase = null')
        -not ($expected | Where-Object { $vmText -notmatch [regex]::Escape($_) })
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.6.0 Contracts' 'ActiveTab setter triggers an immediate refresh via RefreshActiveTabDataAsync' `
    ([regex]::IsMatch($vmText, '(?s)_activeTab = value;.*?RefreshActiveTabDataAsync\(\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.6.0 Contracts' 'RefreshActiveTabDataAsync exists and is shared by the timer tick' `
    ([regex]::IsMatch($vmText, 'private async Task RefreshActiveTabDataAsync') -and [regex]::IsMatch($vmText, '(?s)if \(!ReferenceEquals\(tab, ActiveTab\)\).*?await RefreshActiveTabDataAsync\(\);')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.6.0 Contracts' 'RefreshStatusPollingAsync and RefreshAsync both discard a response for a tab that is no longer active' `
    (([regex]::Matches($vmText, [regex]::Escape('if (!ReferenceEquals(requestTab, ActiveTab)) return;'))).Count -ge 3) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.6.0 Contracts' 'NewServerWizardStep now delegates to ActiveTab.WizardStep instead of a shared field' `
    ([regex]::IsMatch($vmText, 'get => ActiveTab\?\.WizardStep \?\? 1') -and -not [regex]::IsMatch($vmText, '_newServerWizardStep')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.6.0 Contracts' 'TabSession declares a per-tab WizardStep property' `
    ([regex]::IsMatch($tabText, 'public int WizardStep')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.6.0 Contracts' 'SaveProfile propagates the edited profile to every other open tab using the same profile Id' `
    ([regex]::IsMatch($vmText, '(?s)foreach \(var tab in Tabs\)\s*\{\s*if \(ReferenceEquals\(tab, ActiveTab\) \|\| tab\.Profile\?\.Id != profile\.Id\) continue;')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.6.0 Contracts' 'DeleteSelectedProfile blocks deleting a profile that is open in another tab' `
    ([regex]::IsMatch($vmText, 'Tabs\.Any\(t => !ReferenceEquals\(t, ActiveTab\) && t\.Profile\?\.Id == selected\.Id\)')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.6.0 Contracts' 'CloseTab nulls the closed tab Timer after stopping it' `
    ([regex]::IsMatch($vmText, '(?s)tab\.Timer\?\.Stop\(\);\s*tab\.Timer = null;')) `
    -Severity Medium

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.6.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.6\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.5.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.5.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.5.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.5.0\MystTiqPalworldServer_v0.7.5.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.5.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.5.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.5.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.5.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.5.0 checkpoint logic gate still passes' `
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
