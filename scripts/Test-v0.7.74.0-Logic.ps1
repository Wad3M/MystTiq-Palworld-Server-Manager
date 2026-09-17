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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.74.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.74\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.74.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.74\.0"' `
    'Versioning' 'app.manifest reports v0.7.74.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.73.0-Logic.ps1' 'Regression' 'v0.7.73.0 logic gate remains available' -Severity High

$appText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\App.axaml.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.73.0 ExitGui_OnClick running-state check remains present' `
    ($appText -match 'private void ExitGui_OnClick[\s\S]{0,400}viewModel\.Tabs\.Any\(t => t\.ServerIsRunning\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.74.0 Contracts -- close-dialog exit shortcuts
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.74.0 Contracts' 'App exposes public SafeExitAsync/ForceExitAsync (shared by tray menu and dialog)' `
    ($appText -match 'public async Task SafeExitAsync\(\)' -and $appText -match 'public async Task ForceExitAsync\(\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.74.0 Contracts' 'tray menu click handlers delegate to the shared methods, not duplicated logic' `
    ($appText -match 'private async void SafeExit_OnClick\(object\? sender, EventArgs e\) => await SafeExitAsync\(\);' -and
     $appText -match 'private async void ForceExit_OnClick\(object\? sender, EventArgs e\) => await ForceExitAsync\(\);') `
    -Severity Critical

$dialogXamlText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Views\ConfirmMinimizeToTrayDialog.axaml'
Add-MystTiqCheck $ctx 'v0.7.74.0 Contracts' 'ConfirmMinimizeToTrayDialog XAML has Safe Exit and Force Exit buttons' `
    ($dialogXamlText -match 'Content="Force Exit"' -and $dialogXamlText -match 'Content="Safe Exit"') `
    -Severity Critical

$dialogCodeText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Views\ConfirmMinimizeToTrayDialog.axaml.cs'
Add-MystTiqCheck $ctx 'v0.7.74.0 Contracts' 'ConfirmMinimizeToTrayResult gained SafeExit/ForceExit values' `
    ($dialogCodeText -match 'enum ConfirmMinimizeToTrayResult \{ Cancel, MinimizeToTray, SafeExit, ForceExit \}') `
    -Severity Critical

$mainWindowCodeText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
Add-MystTiqCheck $ctx 'v0.7.74.0 Contracts' 'MainWindow_Closing handles SafeExit/ForceExit by calling the shared App methods' `
    ($mainWindowCodeText -match 'ConfirmMinimizeToTrayResult\.SafeExit:[\s\S]{0,80}await app\.SafeExitAsync\(\);' -and
     $mainWindowCodeText -match 'ConfirmMinimizeToTrayResult\.ForceExit:[\s\S]{0,80}await app\.ForceExitAsync\(\);') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.74.0 Contracts -- tab-restore exception isolation
# ---------------------------------------------------------------------------
$vmText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'v0.7.74.0 Contracts' 'startup continuation isolates InitializeLocalDashboardAsync failures from tab restoration' `
    ($vmText -match 'try \{ await InitializeLocalDashboardAsync\(\); \}\s*\r?\n\s*catch \(Exception ex\) \{ StatusBarText = \$"Local dashboard initialization failed: \{ex\.Message\}"; \}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.74.0 Contracts' 'RestoreTabSessionAsync itself is also isolated in its own try/catch' `
    ($vmText -match 'try \{ await RestoreTabSessionAsync\(\); \}\s*\r?\n\s*catch \(Exception ex\) \{ StatusBarText = \$"Restoring saved tabs failed: \{ex\.Message\}"; \}') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.74.0-close-dialog-shortcuts-and-tab-restore-fix.md' `
    'v0.7.74.0 Contracts' 'v0.7.74.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.74.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.74\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.74.0.md' 'Documentation' 'v0.7.74.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.74.0 entry' `
    ($changelogText -match '## v0\.7\.74\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.73.0\MystTiqPalworldServer_v0.7.73.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.73.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.73.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.73.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.73.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.73.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.12.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.12.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.15.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.15.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.17.0 api-remote-enable smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.64.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.64.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'MystTiq.LogicHarness passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'MystTiq.Desktop builds clean (not part of PalworldServerManager.slnx)' {
        & dotnet build (Join-Path $root 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -c Release
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
