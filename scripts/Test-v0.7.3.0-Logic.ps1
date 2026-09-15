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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.3.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.3\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.3.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Core\Services\PortAvailabilityService.cs' 'Regression' 'v0.7.2.0 PortAvailabilityService.cs is still present' -Severity Critical

$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.2.0 port-check endpoint is still present' `
    ([regex]::IsMatch((Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw), 'MapGet\("/api/v1/diagnostics/port-check"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.3.0 contract presence -- new-server wizard
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.3.0 Contracts' 'MainWindowViewModel exposes the wizard step state and commands' `
    ([regex]::IsMatch($vmText, 'public int NewServerWizardStep') -and
     [regex]::IsMatch($vmText, 'public bool IsWizardStep1\s*=>\s*IsCreatingNewProfile\s*&&\s*NewServerWizardStep\s*==\s*1') -and
     [regex]::IsMatch($vmText, 'public bool IsWizardStep2\s*=>\s*IsCreatingNewProfile\s*&&\s*NewServerWizardStep\s*==\s*2') -and
     [regex]::IsMatch($vmText, 'public bool IsWizardStep3\s*=>\s*IsCreatingNewProfile\s*&&\s*NewServerWizardStep\s*==\s*3') -and
     [regex]::IsMatch($vmText, 'public ICommand WizardAdvanceCommand') -and
     [regex]::IsMatch($vmText, 'public ICommand WizardBackCommand')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.3.0 Contracts' 'BeginNewProfile resets the wizard to step 1 every time "Set Up New Server" is opened' `
    ([regex]::IsMatch($vmText, '(?s)private void BeginNewProfile\(\).*?NewServerWizardStep\s*=\s*1;.*?\}')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.3.0 Contracts' 'Step 1 (Connection Details) gains a Next button and hides Save/Delete Profile while creating a new profile' `
    ([regex]::IsMatch($axamlText, 'Step 1 of 3') -and
     [regex]::IsMatch($axamlText, 'Content="Next: Server Defaults →" Command="\{Binding WizardAdvanceCommand\}"') -and
     [regex]::IsMatch($axamlText, 'Content="Save Profile" Command="\{Binding SaveProfileCommand\}" IsVisible="\{Binding !IsCreatingNewProfile\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.3.0 Contracts' 'Step 2 (In-Game Server Defaults) is gated on IsWizardStep2, not the old IsCreatingNewProfile, and has Back/Next' `
    ([regex]::IsMatch($axamlText, 'IsVisible="\{Binding IsWizardStep2\}"') -and
     [regex]::IsMatch($axamlText, 'Step 2 of 3') -and
     [regex]::IsMatch($axamlText, 'Content="← Back" Command="\{Binding WizardBackCommand\}"') -and
     [regex]::IsMatch($axamlText, 'Content="Next →" Command="\{Binding WizardAdvanceCommand\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.3.0 Contracts' 'Step 3 (Confirm and Finish) exists, gated on IsWizardStep3, and reuses SaveProfileCommand' `
    ([regex]::IsMatch($axamlText, 'IsVisible="\{Binding IsWizardStep3\}"') -and
     [regex]::IsMatch($axamlText, 'Step 3 of 3') -and
     [regex]::IsMatch($axamlText, '(?s)IsWizardStep3\}".*?Content="Save Profile" FontWeight="Bold" Command="\{Binding SaveProfileCommand\}"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.3.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.3\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.2.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.2.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.2.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.2.0\MystTiqPalworldServer_v0.7.2.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.2.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.2.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.2.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.2.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.2.0 checkpoint logic gate still passes' `
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
