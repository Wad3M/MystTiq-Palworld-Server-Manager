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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.87.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.87\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.87.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.87\.0"' `
    'Versioning' 'app.manifest reports v0.7.87.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.86.0-Logic.ps1' 'Regression' 'v0.7.86.0 logic gate remains available' -Severity High

$vmText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.86.0-era MODs wizard step remains present, untouched by this version' `
    ($vmText -match 'public bool IsWizardStepMods => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 7;') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.87.0 Contracts -- Configuration dirty highlighting, save/discard prompt, default tab
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.87.0 Contracts' 'Navigate() guards leaving Configuration with unsaved changes' `
    ($vmText -match 'if \(SelectedPage == NavigationPage\.Configuration && page != NavigationPage\.Configuration && PalworldConfigIsDirty\)' -and
     $vmText -match '_pendingNavigationAfterConfigPrompt = page;' -and
     $vmText -match 'ConfigurationUnsavedChangesNavigationBlocked\?\.Invoke\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.87.0 Contracts' 'PerformNavigation is the single shared completion path for direct and prompted navigation' `
    ($vmText -match 'private void PerformNavigation\(NavigationPage page\)' -and
     ([regex]::Matches($vmText, 'PerformNavigation\(')).Count -ge 4) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.87.0 Contracts' 'all three navigation continuations exist (Save/Discard/Cancel)' `
    ($vmText -match 'public async Task ContinuePendingNavigationSavingConfigurationAsync\(\)' -and
     $vmText -match 'public void ContinuePendingNavigationDiscardingConfigurationChanges\(\)' -and
     $vmText -match 'public void CancelPendingConfigurationNavigation\(\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.87.0 Contracts' 'saving continuation reuses the existing Save/Reset methods rather than duplicating logic' `
    ($vmText -match 'await SavePalworldConfigurationAsync\(\);\s*\r?\n\s*PerformNavigation\(target\.Value\);' -and
     $vmText -match 'ResetPalworldConfigurationChanges\(\);\s*\r?\n\s*PerformNavigation\(target\.Value\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.87.0 Contracts' 'Configuration navigation always resets to Simple Settings view first' `
    ($vmText -match 'case NavigationPage\.Configuration:\s*(?:\r?\n\s*//[^\r\n]*)*\s*\r?\n\s*SetConfigurationView\(true\);\s*\r?\n\s*await LoadConfigurationAsync\(\);') `
    -Severity Critical

$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.87.0 Contracts' 'Advanced Settings table rows and the Active Value TextBox bind IsDirty highlighting' `
    ($mainWindowXaml -match 'Selector="Border\.rowDirty"' -and
     $mainWindowXaml -match 'Classes\.rowDirty="\{Binding IsDirty\}"' -and
     $mainWindowXaml -match 'Grid\.Column="4" Text="\{Binding DisplayValue, Mode=TwoWay\}" VerticalAlignment="Center" Classes\.dirty="\{Binding IsDirty\}"') `
    -Severity Critical

$mainWindowCodeBehind = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
Add-MystTiqCheck $ctx 'v0.7.87.0 Contracts' 'code-behind subscribes to the navigation-blocked event and shows the confirm dialog' `
    ($mainWindowCodeBehind -match 'vm\.ConfigurationUnsavedChangesNavigationBlocked \+= ' -and
     $mainWindowCodeBehind -match 'OnConfigurationUnsavedChangesNavigationBlockedAsync' -and
     $mainWindowCodeBehind -match 'new Views\.ConfirmSaveDiscardDialog\(vm\.PalworldConfigDirtyText\)') `
    -Severity Critical

Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Views\ConfirmSaveDiscardDialog.axaml' 'v0.7.87.0 Contracts' 'ConfirmSaveDiscardDialog view exists' -Severity Critical
$confirmDialogCode = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Views\ConfirmSaveDiscardDialog.axaml.cs'
Add-MystTiqCheck $ctx 'v0.7.87.0 Contracts' 'ConfirmSaveDiscardDialog exposes Cancel/Discard/Save results' `
    ($confirmDialogCode -match 'public enum ConfirmSaveDiscardResult \{ Cancel, Discard, Save \}') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.87.0-configuration-dirty-highlighting-and-save-prompt.md' `
    'v0.7.87.0 Contracts' 'v0.7.87.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.87.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.87\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.87.0.md' 'Documentation' 'v0.7.87.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.87.0 entry' `
    ($changelogText -match '## v0\.7\.87\.0') `
    -Severity High

$roadmapText = Get-MystTiqText $ctx 'docs\roadmap\PRODUCT_ROADMAP.md'
Add-MystTiqCheck $ctx 'Documentation' 'Live-Session Backlog Configuration follow-up entry was cleared' `
    ($roadmapText -notmatch 'Configuration page follow-up') `
    -Severity Medium

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.86.0\MystTiqPalworldServer_v0.7.86.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.86.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.86.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.86.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.86.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.86.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.81.0 new-server wizard route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.81.0-RouteSmoke.ps1') -ProjectRoot $root
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
