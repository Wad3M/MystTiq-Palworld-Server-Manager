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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.11.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.11\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.11.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Views\ConfirmCloseTabDialog.axaml' 'v0.7.11.0 Contracts' 'ConfirmCloseTabDialog.axaml exists' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Views\TrayReminderToast.axaml' 'v0.7.11.0 Contracts' 'TrayReminderToast.axaml exists' -Severity Critical

$programText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Program.cs') -Raw
$appText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\App.axaml.cs') -Raw
$mainWindowCsText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml.cs') -Raw
$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.10.0 whitelist enforcement is still present' `
    ([regex]::IsMatch((Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessWhitelistService.cs') -Raw), 'EnforceAsync')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.11.0 contract presence -- single-instance enforcement
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.11.0 Contracts' 'Program.Main acquires a named Mutex and exits without starting the UI when a second instance is detected' `
    ([regex]::IsMatch($programText, 'new Mutex\(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out var createdNew\)') -and
     [regex]::IsMatch($programText, '(?s)if \(!createdNew\)\s*\{\s*NotifyAlreadyRunning\(\);\s*return;')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.11.0 contract presence -- window close behavior
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.11.0 Contracts' 'MainWindow_Closing checks every tab (not just the active one) for a running server' `
    ([regex]::IsMatch($mainWindowCsText, 'vm\.Tabs\.Any\(t => t\.ServerIsRunning\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.11.0 Contracts' 'closing performs a real exit when nothing is running, and shows the tray reminder when something is' `
    ([regex]::IsMatch($mainWindowCsText, 'app\.ExitGuiOnlyIfNothingIsRunning\(\);') -and
     [regex]::IsMatch($mainWindowCsText, 'app\.ShowTrayStillRunningReminder\(')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.11.0 Contracts' 'App exposes ShowTrayStillRunningReminder and ExitGuiOnlyIfNothingIsRunning' `
    ([regex]::IsMatch($appText, 'public void ShowTrayStillRunningReminder\(string message\)') -and
     [regex]::IsMatch($appText, 'public void ExitGuiOnlyIfNothingIsRunning\(\) => RequestExplicitExit\(\);')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 5. v0.7.11.0 contract presence -- tab-close confirmation
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.11.0 Contracts' 'tab close button uses Click+Tag instead of Command/CommandParameter' `
    ([regex]::IsMatch($axamlText, 'Click="CloseTabButton_OnClick" Tag="\{Binding\}"') -and
     -not [regex]::IsMatch($axamlText, 'Command="\{Binding \$parent\[ListBox\]\.\(\(vm:MainWindowViewModel\)DataContext\)\.CloseTabCommand\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.11.0 Contracts' 'CloseTabButton_OnClick shows the confirmation dialog only when the tab server is running, and honors all three outcomes' `
    ([regex]::IsMatch($mainWindowCsText, 'if \(tab\.ServerIsRunning\)') -and
     [regex]::IsMatch($mainWindowCsText, 'ConfirmCloseTabResult\.Cancel') -and
     [regex]::IsMatch($mainWindowCsText, 'ConfirmCloseTabResult\.StopAndClose') -and
     [regex]::IsMatch($mainWindowCsText, 'await vm\.StopTabServerAsync\(tab\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.11.0 Contracts' 'StopTabServerAsync operates on the specific tab via BuildProfileFromTab, not the ActiveTab-delegated properties' `
    ([regex]::IsMatch($vmText, '(?s)public async Task StopTabServerAsync\(TabSession tab\)\s*\{\s*ConnectionProfile profile;\s*try \{ profile = BuildProfileFromTab\(tab\); \}')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 6. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.11.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.11\.0')) `
    -Severity High

Add-MystTiqCheck $ctx 'Documentation' 'the 4 stale roadmap labels found by the audit are fixed' `
    ($(
        $roadmap = Get-Content (Join-Path $root 'docs\roadmap\PRODUCT_ROADMAP.md') -Raw
        $backport = Get-Content (Join-Path $root 'docs\roadmap\WINDOWS_BACKPORT_REGISTRY.md') -Raw
        $grouped = Get-Content (Join-Path $root 'docs\roadmap\MystTiq_v0.6_Grouped_Development_Roadmap.md') -Raw
        (-not [regex]::IsMatch($roadmap, 'v0\.3\.1\.0.*\(Current Candidate\)')) -and
        (-not [regex]::IsMatch($roadmap, 'v0\.5\.1\.1.*\(Current Release Candidate\)')) -and
        (-not [regex]::IsMatch($backport, '\| v0\.3 \| In progress \|')) -and
        (-not [regex]::IsMatch($grouped, 'v0\.6\.1[678]\.0 - .*\(planned\)'))
    )) `
    -Severity High

# ---------------------------------------------------------------------------
# 7. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 8. Existing v0.7.10.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.10.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.10.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 9. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.10.0\MystTiqPalworldServer_v0.7.10.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.10.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.10.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.10.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.10.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.10.0 checkpoint logic gate still passes' `
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
