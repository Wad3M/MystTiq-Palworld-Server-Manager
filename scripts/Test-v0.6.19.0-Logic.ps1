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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.19.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.19\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.19.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\ViewModels\TabSession.cs' 'v0.6.19.0 Contracts' 'TabSession.cs exists' -Severity Critical

$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$tabSessionText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\TabSession.cs') -Raw
$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.6.18.4 ServerIsRunning contract is still present' `
    ([regex]::IsMatch($vmText, 'NativeProcessId\.HasValue\s*\|\|\s*status\.Ready')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.18.3 Configuration page overhaul (DisplayValue) is still present' `
    ([regex]::IsMatch($desktopText, 'DisplayValue')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.19.0 contract presence -- True multi-tab server connections
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.19.0 Contracts' 'TabSession carries the per-tab connection fields' `
    ([regex]::IsMatch($tabSessionText, 'public\s+ConnectionProfile\?\s+Profile') -and
     [regex]::IsMatch($tabSessionText, 'public\s+string\s+BearerToken') -and
     [regex]::IsMatch($tabSessionText, 'public\s+bool\s+ManagementApiConnected') -and
     [regex]::IsMatch($tabSessionText, 'public\s+string\s+ConnectionState') -and
     [regex]::IsMatch($tabSessionText, 'public\s+bool\s+IsBusy') -and
     [regex]::IsMatch($tabSessionText, 'public\s+bool\s+ServerIsRunning') -and
     [regex]::IsMatch($tabSessionText, 'DispatcherTimer\?\s+Timer')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.0 Contracts' 'MainWindowViewModel exposes Tabs/ActiveTab' `
    ([regex]::IsMatch($vmText, 'ObservableCollection<TabSession>\s+Tabs') -and
     [regex]::IsMatch($vmText, 'TabSession\?\s+ActiveTab')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.0 Contracts' 'SelectedProfile/BearerToken/ManagementApiConnected/ConnectionState/IsBusy/ServerIsRunning are ActiveTab-backed, not private fields' `
    ([regex]::IsMatch($vmText, 'get\s*=>\s*ActiveTab\?\.Profile') -and
     [regex]::IsMatch($vmText, 'get\s*=>\s*ActiveTab\?\.BearerToken') -and
     [regex]::IsMatch($vmText, 'get\s*=>\s*ActiveTab\?\.ManagementApiConnected') -and
     [regex]::IsMatch($vmText, 'get\s*=>\s*ActiveTab\?\.ConnectionState') -and
     [regex]::IsMatch($vmText, 'get\s*=>\s*ActiveTab\?\.IsBusy') -and
     [regex]::IsMatch($vmText, 'get\s*=>\s*ActiveTab\?\.ServerIsRunning') -and
     -not [regex]::IsMatch($vmText, 'private\s+ConnectionProfile\?\s+_selectedProfile') -and
     -not [regex]::IsMatch($vmText, 'private\s+bool\s+_isBusy') -and
     -not [regex]::IsMatch($vmText, 'private\s+bool\s+_managementApiConnected')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.0 Contracts' 'the old single app-wide refresh timer is gone in favor of one timer per tab' `
    (-not [regex]::IsMatch($vmText, '_refreshTimer') -and [regex]::IsMatch($vmText, 'private void WireTabTimer\(TabSession tab\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.0 Contracts' 'background tabs poll lightweight state, not the shared delegated properties' `
    ([regex]::IsMatch($vmText, 'private async Task RefreshTabLightweightAsync\(TabSession tab\)') -and
     [regex]::IsMatch($vmText, 'BuildProfileFromTab\(tab\)') -and
     [regex]::IsMatch($vmText, '_api\.GetHealthAsync\(profile, tab\.BearerToken\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.0 Contracts' 'the "+" flow offers both Set Up New Server and Connect to Existing Server' `
    ([regex]::IsMatch($vmText, 'private void OpenNewServerTab\(\)') -and
     [regex]::IsMatch($vmText, 'private void ConnectExistingProfileTab\(ConnectionProfile\? profile\)') -and
     [regex]::IsMatch($desktopText, 'Set Up New Server') -and
     [regex]::IsMatch($desktopText, 'Connect to')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.0 Contracts' 'connecting to an existing profile is filtered against already-open tabs (duplicate-connection guard)' `
    ([regex]::IsMatch($desktopText, 'vm\.Tabs\.All\(t\s*=>\s*t\.Profile\?\.Id\s*!=\s*p\.Id\)') -and
     [regex]::IsMatch($vmText, 'Tabs\.Any\(t\s*=>\s*t\.Profile\?\.Id\s*==\s*profile\.Id\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.0 Contracts' 'the tab bar binds to Tabs/ActiveTab with a per-tab close command, not the old Profiles/SelectedProfile bookmark list' `
    ([regex]::IsMatch($desktopText, 'ItemsSource="\{Binding Tabs\}"\s+SelectedItem="\{Binding ActiveTab\}"') -and
     [regex]::IsMatch($desktopText, 'CloseTabCommand')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.0 Contracts' 'closing the last remaining tab is guarded' `
    ([regex]::IsMatch($vmText, 'if \(tab is null \|\| Tabs\.Count <= 1\)') -and
     [regex]::IsMatch($vmText, "_ => Tabs\.Count > 1")) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.19.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.19\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.18.4 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.18.4-Logic.ps1' `
    'Regression Baseline' 'v0.6.18.4 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.18.4\MystTiqPalworldServer_v0.6.18.4_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.18.4 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.18.4-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.18.4-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.18.4-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.18.4 checkpoint logic gate still passes' `
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
