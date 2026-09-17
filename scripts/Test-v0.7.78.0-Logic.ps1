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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.78.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.78\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.78.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.78\.0"' `
    'Versioning' 'app.manifest reports v0.7.78.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.77.0-Logic.ps1' 'Regression' 'v0.7.77.0 logic gate remains available' -Severity High

$modText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.77.0-era RepairModAsync remains present, untouched by this version' `
    ($modText -match 'public async Task<HeadlessModMutationResult> RepairModAsync\(string type, string package, CancellationToken cancellationToken\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.78.0 Contracts -- MOD Library auto-load, layout, ribbon, right-click, drag-and-drop
# ---------------------------------------------------------------------------
$vmText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'SelectedPage setter auto-triggers ScanWorkshopModsAsync for MOD Library, guarded to run once per tab' `
    ($vmText -match 'if \(value == NavigationPage\.ModLibrary && WorkshopItems\.Count == 0\)' -and
     $vmText -match '_ = ScanWorkshopModsAsync\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'new MOD Maintenance ribbon group exists, gated to MOD Library only (not shared with MOD Dashboard)' `
    ($vmText -match 'groups\.Add\(new\("MOD Maintenance"' -and
     $vmText -match 'if \(IsModLibraryPage\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'ModInstallPackage (manual package-name override) was removed end to end' `
    ($vmText -notmatch 'ModInstallPackage') `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'IsModHealthGood/IsModHealthDegraded exist and are notified at both ModHealth write sites' `
    ($vmText -match 'public bool IsModHealthGood => ModHealth == "Healthy";' -and
     (([regex]::Matches($vmText, 'RaisePropertyChanged\(nameof\(IsModHealthGood\)\)')).Count -eq 2)) `
    -Severity Critical

$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'

Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'Installed MODs list has a right-click ContextMenu with all 5 selection-scoped actions' `
    ($mainWindowXaml -match 'MenuItem Header="Enable Selected" Command="\{Binding EnableSelectedModCommand\}"' -and
     $mainWindowXaml -match 'MenuItem Header="Disable Selected" Command="\{Binding DisableSelectedModCommand\}"' -and
     $mainWindowXaml -match 'MenuItem Header="Rollback Selected" Command="\{Binding RollbackSelectedModCommand\}"' -and
     $mainWindowXaml -match 'MenuItem Header="Repair Selected" Command="\{Binding RepairSelectedModCommand\}"' -and
     $mainWindowXaml -match 'MenuItem Header="Delete Selected" Command="\{Binding DeleteSelectedModCommand\}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'ZIP install card has no manual package TextBox and no disabled placeholder buttons' `
    ($mainWindowXaml -notmatch 'Watermark="Package name \(optional\)"' -and
     $mainWindowXaml -notmatch 'Legacy Migration') `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'ZIP drop zone exists with AllowDrop and both drag handlers wired' `
    ($mainWindowXaml -match 'x:Name="ZipInstallDropZone"' -and
     $mainWindowXaml -match 'DragDrop\.AllowDrop="True"') `
    -Severity Critical

$mainWindowCodeBehind = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'drag-and-drop handlers use the current non-obsolete DataTransfer API, not the deprecated Data property' `
    ($mainWindowCodeBehind -match 'ZipInstallDropZone_OnDragOver' -and
     $mainWindowCodeBehind -match 'ZipInstallDropZone_OnDrop' -and
     $mainWindowCodeBehind -match 'e\.DataTransfer\.TryGetFiles\(\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'MOD Library layout is Install ZIP + Available Workshop side by side, then Installed MODs (2/3) + MOD DETAILS (1/3)' `
    ($mainWindowXaml -match 'Grid ColumnDefinitions="\*,1\.4\*" ColumnSpacing="12" IsVisible="\{Binding IsModLibraryPage\}"' -and
     $mainWindowXaml -match 'Grid ColumnDefinitions="2\*,\*" ColumnSpacing="12" IsVisible="\{Binding IsModLibraryPage\}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'MOD Dashboard hero health banner and recolored stat cards exist, distinct from MOD Library' `
    ($mainWindowXaml -match 'Classes="card" Classes\.glowGreen="\{Binding IsModHealthGood\}" IsVisible="\{Binding IsModDashboardPage\}"' -and
     $mainWindowXaml -match 'Text="MOD Overview"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.78.0-mod-pages-ux-pass.md' `
    'v0.7.78.0 Contracts' 'v0.7.78.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.78.0 Contracts -- per-mod version/update fields and UE4SS fixes
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'HeadlessModItem gained UpdateAvailable/UpdateHint/InstalledVersion, computed during GetInventoryAsync' `
    ($modText -match 'bool UpdateAvailable = false, string UpdateHint = "", string\? InstalledVersion = null' -and
     $modText -match 'var check = await CheckModUpdateAsync\(mods\[i\]\.Type, mods\[i\]\.Package, cancellationToken\);' -and
     $modText -match 'var installedVersion = ReadWorkshopManifestVersion\(mods\[i\]\.InstallPath\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'UE4SS runtime verification is persisted and used to avoid reverting to Unverified after a confirmed-good run' `
    ($modText -match 'internal sealed record Ue4ssRuntimeVerification\(DateTimeOffset VerifiedAtUtc\);' -and
     $modText -match 'if \(verified && matches\) WriteUe4ssRuntimeVerification\(\);' -and
     $modText -match 'Confirmed — ran without issue') `
    -Severity Critical

$designSystemText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Styles\DesignSystem.axaml'
# IndexOf-based ordering check rather than a character-count-budget regex span -- this project's
# own established pitfall (a [\s\S]{0,N} span too narrow for the real distance between two lines
# broke two earlier version's own test scripts; see feedback_race_condition_testing memory).
$statuscardSelectorIndex = $designSystemText.IndexOf('Selector="Border.statuscard">')
$statuscardPaddingIndex = $designSystemText.IndexOf('<Setter Property="Padding" Value="11"/>', [Math]::Max($statuscardSelectorIndex, 0))
$statuscardNextStyleIndex = $designSystemText.IndexOf('<Style Selector=', $statuscardSelectorIndex + 1)
Add-MystTiqCheck $ctx 'v0.7.78.0 Contracts' 'Border.statuscard now sets Padding, matching Border.card' `
    ($statuscardSelectorIndex -ge 0 -and $statuscardPaddingIndex -gt $statuscardSelectorIndex -and
     ($statuscardNextStyleIndex -lt 0 -or $statuscardPaddingIndex -lt $statuscardNextStyleIndex)) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.78.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.78\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.78.0.md' 'Documentation' 'v0.7.78.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.78.0 entry' `
    ($changelogText -match '## v0\.7\.78\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.77.0\MystTiqPalworldServer_v0.7.77.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.77.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.77.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.77.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.77.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.77.0 checkpoint logic gate still passes' `
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
