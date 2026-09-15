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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.52.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.52\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.52.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.52\.0"' `
    'Versioning' 'app.manifest reports v0.7.52.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

$viewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$mainWindowCsText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.51.0 per-tab accent coloring is still present' `
    ($viewModelText -match 'private void RefreshTabAccentVisuals\(\)' -and
     $viewModelText -match 'private string ResolveAccentColorKey\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'ChooseLocalConnection/ChooseRemoteConnection/BeginNewProfile unchanged and still present' `
    ($viewModelText -match 'private void ChooseLocalConnection\(\)' -and
     $viewModelText -match 'private void ChooseRemoteConnection\(\)' -and
     $viewModelText -match 'private void BeginNewProfile\(\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'CloneWorldCommand and its local-only CanExecute gate are unchanged' `
    ($viewModelText -match 'CloneWorldCommand = new AsyncCommand\(CloneWorldAsync, \(\) => !IsBusy && ManagementApiConnected && !string\.IsNullOrWhiteSpace\(CloneNewProfileId\)\);') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.52.0 contract presence -- "+" Flow Restructure
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.52.0 Contracts' 'Three new flyout commands are declared and wired in the constructor' `
    ($viewModelText -match 'public ICommand ConnectLocalServerTabCommand \{ get; \}' -and
     $viewModelText -match 'public ICommand ConnectRemoteServerTabCommand \{ get; \}' -and
     $viewModelText -match 'public ICommand CloneServerFlowCommand \{ get; \}' -and
     $viewModelText -match 'ConnectLocalServerTabCommand = new RelayCommand\(OpenConnectLocalServerTab\);' -and
     $viewModelText -match 'ConnectRemoteServerTabCommand = new RelayCommand\(OpenConnectRemoteServerTab\);' -and
     $viewModelText -match 'CloneServerFlowCommand = new RelayCommand\(OpenCloneServerFlow, \(\) => HasCloneableLocalTab\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.52.0 Contracts' 'Connect Local/Remote skip the wizard''s step-0 choice cards by calling ChooseLocalConnection/ChooseRemoteConnection directly' `
    ($viewModelText -match '(?s)private void OpenConnectLocalServerTab\(\)\s*\{.*?BeginNewProfile\(\);\s*ChooseLocalConnection\(\);' -and
     $viewModelText -match '(?s)private void OpenConnectRemoteServerTab\(\)\s*\{.*?BeginNewProfile\(\);\s*ChooseRemoteConnection\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.52.0 Contracts' 'Clone a Server requires an already-connected local tab and routes to the Fleet page' `
    ($viewModelText -match 'public bool HasCloneableLocalTab => Tabs\.Any\(t => t\.ManagementApiConnected && t\.Profile\?\.Id == ConnectionProfile\.LocalDefault\.Id\);' -and
     $viewModelText -match 'NavigateCommand\.Execute\("Fleet"\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.52.0 Contracts' 'Flyout builds four top-level entries, with Clone conditionally added only when eligible' `
    ($mainWindowCsText -match 'Header = "Set Up New Server"' -and
     $mainWindowCsText -match 'Header = "Connect to Local Server"' -and
     $mainWindowCsText -match 'Header = "Connect to Remote Server"' -and
     $mainWindowCsText -match 'if \(vm\.HasCloneableLocalTab\)' -and
     $mainWindowCsText -match 'Header = "Clone a Server"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.52.0 Contracts' 'Existing per-saved-profile "Connect to {profile}" list is unchanged and still appears' `
    ($mainWindowCsText -match 'var availableProfiles = vm\.Profiles\.Where\(p => vm\.Tabs\.All\(t => t\.Profile\?\.Id != p\.Id\)\)\.ToList\(\);' -and
     $mainWindowCsText -match 'Header = \$"Connect to \{profile\.Name\}"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.52.0-plus-flow-restructure.md' 'v0.7.52.0 Contracts' 'v0.7.52.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.52.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.52\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.51.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.51.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.51.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.51.0\MystTiqPalworldServer_v0.7.51.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.51.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.51.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.51.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.51.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.51.0 checkpoint logic gate still passes' `
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

    # v0.7.52.0 is Desktop-only (flyout/wizard entry points) -- no route contract changed, so every
    # server-side route/CLI smoke test carried forward is expected to pass unchanged.
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
