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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.19.1' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.19\.1</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.19.1' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\ViewModels\TabSession.cs' 'Regression' 'v0.6.19.0 TabSession.cs is still present' -Severity Critical

$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$fleetDtoText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Models\FleetDtos.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.6.19.0 Tabs/ActiveTab tab bar is still present' `
    ([regex]::IsMatch($axamlText, 'ItemsSource="\{Binding Tabs\}"\s+SelectedItem="\{Binding ActiveTab\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.18.4 ServerIsRunning contract is still present' `
    ([regex]::IsMatch($vmText, 'NativeProcessId\.HasValue\s*\|\|\s*status\.Ready')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.19.1 contract presence -- Ribbon cleanup and Fleet page fixes
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.19.1 Contracts' 'the five category-conditional ribbon duplicate groups are gone' `
    (-not [regex]::IsMatch($axamlText, 'IsVisible="\{Binding IsV5WorldCategory\}"><StackPanel><StackPanel Orientation="Horizontal"') -and
     -not [regex]::IsMatch($axamlText, 'CommandParameter="ServerSetup"><StackPanel><TextBlock Classes="flatIcon" Foreground="#7AD5FF" Text="▰"/><TextBlock Text="Setup"') -and
     -not [regex]::IsMatch($axamlText, 'CommandParameter="ModDashboard"><StackPanel><TextBlock Classes="flatIcon" Foreground="#E676FF" Text="◆"/><TextBlock Text="Dashboard"') -and
     -not [regex]::IsMatch($axamlText, 'CommandParameter="UpdateCenter"><StackPanel><TextBlock Classes="flatIcon" Foreground="#7AD5FF" Text="↻"/><TextBlock Text="Updates"') -and
     -not [regex]::IsMatch($axamlText, 'CommandParameter="Settings"><StackPanel><TextBlock Classes="flatIcon" Foreground="#72C8E8" Text="⚙"/><TextBlock Text="Settings"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.1 Contracts' 'the always-visible Server Control and Quick Actions ribbon groups are untouched' `
    ([regex]::IsMatch($axamlText, 'Text="Server Control" HorizontalAlignment="Center"') -and
     [regex]::IsMatch($axamlText, 'Text="Quick Actions" HorizontalAlignment="Center"') -and
     [regex]::IsMatch($axamlText, 'Command="\{Binding StartCommand\}"') -and
     [regex]::IsMatch($axamlText, 'Command="\{Binding CreateBackupCommand\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.1 Contracts' 'the category ToggleButtons and per-category nav sidebar lists are untouched' `
    ([regex]::IsMatch($axamlText, 'Classes="categoryTab world"') -and
     [regex]::IsMatch($axamlText, 'IsVisible="\{Binding IsV5WorldCategory\}" VerticalAlignment="Top"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.1 Contracts' 'ServerProfileSummaryDto exposes a StatusDotColor for the Fleet server list' `
    ([regex]::IsMatch($fleetDtoText, 'public\s+string\s+StatusDotColor\s*=>\s*Status\.CrashDetected')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.1 Contracts' 'Fleet Server Profiles list binds the new status dot' `
    ([regex]::IsMatch($axamlText, 'ItemsSource="\{Binding FleetServers\}".*?StatusDotColor', 'Singleline')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.19.1 Contracts' 'Fleet Last Action Results now shows the Error message on a failed row' `
    ([regex]::IsMatch($axamlText, 'ItemsSource="\{Binding FleetActionResults\}".*?Text="\{Binding Error\}".*?IsVisible="\{Binding !Success\}"', 'Singleline')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.19.1 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.19\.1')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.19.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.19.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.19.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.19.0\MystTiqPalworldServer_v0.6.19.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.19.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.19.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.19.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.19.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.19.0 checkpoint logic gate still passes' `
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
