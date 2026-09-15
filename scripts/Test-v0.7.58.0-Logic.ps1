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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.58.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.58\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.58.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.58\.0"' `
    'Versioning' 'app.manifest reports v0.7.58.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'native\MystTiqConsoleProxy\dllmain.cpp' 'Regression' 'v0.7.57.0 native proxy DLL source is still present' -Severity High

$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$mainWindowCodeText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml.cs') -Raw
$viewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.55.0 description fetch/set-source commands are unchanged' `
    ($mainWindowAxamlText -match 'Command="\{Binding FetchSelectedModDescriptionCommand\}"' -and
     $mainWindowAxamlText -match 'Command="\{Binding SetSelectedModDescriptionSourceCommand\}"') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.58.0 contract presence -- Light Polish
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.58.0 Contracts' 'The description text is wrapped in a scrollable container, not a bare fixed-height TextBlock' `
    ($mainWindowAxamlText -match '(?s)<ScrollViewer MaxHeight="220" VerticalScrollBarVisibility="Auto">\s*<TextBlock Text="\{Binding SelectedModDescription\.Description\}" TextWrapping="Wrap"/>\s*</ScrollViewer>') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.58.0 Contracts' 'An Open button next to the Source URL is wired to the new click handler' `
    ($mainWindowAxamlText -match 'Content="Open" FontSize="11" Padding="8,2" Click="OpenModDescriptionSource_Click"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.58.0 Contracts' 'OpenModDescriptionSource_Click exists, validates http/https before Process.Start, and reuses the established UseShellExecute pattern' `
    ($mainWindowCodeText -match 'private void OpenModDescriptionSource_Click\(object\? sender, RoutedEventArgs e\)' -and
     $mainWindowCodeText -match '(?s)OpenModDescriptionSource_Click.*?uri\.Scheme != Uri\.UriSchemeHttp && uri\.Scheme != Uri\.UriSchemeHttps.*?Process\.Start\(new ProcessStartInfo \{ FileName = url, UseShellExecute = true \}\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.58.0 Contracts' 'The no-match message is bound to the new gated property, not the old premature !HasSelectedModDescription condition' `
    ($mainWindowAxamlText -match 'Text="No known Workshop match.*?IsVisible="\{Binding ShowNoModDescriptionMatchMessage\}"' -and
     -not ($mainWindowAxamlText -match 'Text="No known Workshop match.*?IsVisible="\{Binding !HasSelectedModDescription\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.58.0 Contracts' 'ShowNoModDescriptionMatchMessage requires both a completed fetch and no match found' `
    ($viewModelText -match 'public bool ShowNoModDescriptionMatchMessage => HasModDescriptionResult && !HasSelectedModDescription;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.58.0 Contracts' 'ShowNoModDescriptionMatchMessage is re-raised whenever SelectedModDescription changes' `
    ($viewModelText -match '(?s)RaisePropertyChanged\(nameof\(HasModDescriptionResult\)\);\s*RaisePropertyChanged\(nameof\(ShowNoModDescriptionMatchMessage\)\);') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.58.0-mod-description-light-polish.md' 'v0.7.58.0 Contracts' 'v0.7.58.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.58.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.58\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.57.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.57.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.57.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.57.0\MystTiqPalworldServer_v0.7.57.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.57.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.57.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.57.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.57.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.57.0 checkpoint logic gate still passes' `
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

    # v0.7.58.0 is Desktop-only (UI polish) -- no route contract changed, so every server-side
    # route/CLI smoke test carried forward is expected to pass unchanged.
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
