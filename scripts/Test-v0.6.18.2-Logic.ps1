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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.18.2' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.18\.2</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.18.2' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.18.0 Anti-Cheat service is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessAntiCheatService')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.18.1 Gender-verification fix is still present' `
    ([regex]::IsMatch($hostText, 'GenderMatches\(')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.18.2 contract presence -- Server Setup page cleanup
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.18.2 Contracts' 'Server Setup hero card and its duplicate description text are gone' `
    (-not [regex]::IsMatch($desktopText, 'Install, verify, and maintain the components required to run your Palworld dedicated server\.\" Classes=\"muted\" FontSize=\"13\"')) `
    -Severity Critical -Details 'That description text was a verbatim duplicate of PageSubtitle, already shown by the shared global page header above every page.'

Add-MystTiqCheck $ctx 'v0.6.18.2 Contracts' 'Server Setup stat row folds in the environment-health badge and a Verify Files action' `
    ([regex]::IsMatch($desktopText, 'ColumnDefinitions="\*,\*,\*,\*"[\s\S]{0,1800}EnvironmentHealthText[\s\S]{0,300}VerifyEnvironmentCommand')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.18.2 Contracts' 'First-Run Server Defaults card no longer appears on the Server Setup page' `
    (-not [regex]::IsMatch($desktopText, 'IsVisible="\{Binding IsServerSetupPage\}"[\s\S]{0,3000}FIRST-RUN SERVER DEFAULTS')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.18.2 Contracts' 'In-Game Server Defaults card exists on the Settings page, gated to new-profile mode' `
    ([regex]::IsMatch($desktopText, 'IN-GAME SERVER DEFAULTS \(new server only\)') -and [regex]::IsMatch($desktopText, 'IsVisible="\{Binding IsCreatingNewProfile\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.18.2 Contracts' 'IsCreatingNewProfile exists and is raised even when SelectedProfile becomes null' `
    ([regex]::IsMatch($desktopText, 'IsCreatingNewProfile\s*=>\s*SelectedProfile is null') -and [regex]::IsMatch($desktopText, 'RaisePropertyChanged\(nameof\(IsCreatingNewProfile\)\);\s*\r?\n\s*if \(value is null\) return;')) `
    -Severity Critical -Details 'The notification must fire before the null-guard, not after -- BeginNewProfile() setting SelectedProfile=null is exactly the transition this property exists to catch.'

Add-MystTiqCheck $ctx 'v0.6.18.2 Contracts' 'Check for Updates / Install Missing commands were removed, not just hidden' `
    (-not [regex]::IsMatch($desktopText, 'CheckEnvironmentUpdatesCommand') -and -not [regex]::IsMatch($desktopText, 'ICommand InstallMissingEnvironmentCommand')) `
    -Severity Critical -Details 'Update Center already owns the real implementation (PreviewDistributionPlanAsync/UpdatePalworldServerAsync) -- these were confirmed-duplicate wrappers, not unique functionality.'

Add-MystTiqCheck $ctx 'v0.6.18.2 Contracts' 'The per-row Install action for a genuinely missing component still works' `
    ([regex]::IsMatch($desktopText, 'private async Task InstallMissingEnvironmentAsync')) `
    -Severity Critical -Details 'Only the toolbar command/button was removed; the method itself is still called by the environment checklist row action for a genuine first-run install.'

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.18.2 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.18\.2')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.18.1 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.18.1-Logic.ps1' `
    'Regression Baseline' 'v0.6.18.1 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.18.1\MystTiqPalworldServer_v0.6.18.1_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.18.1 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.18.1-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.18.1-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.18.1-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.18.1 checkpoint logic gate still passes' `
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
