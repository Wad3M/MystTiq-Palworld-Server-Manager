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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.83.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.83\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.83.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.83\.0"' `
    'Versioning' 'app.manifest reports v0.7.83.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.82.0-Logic.ps1' 'Regression' 'v0.7.82.0 logic gate remains available' -Severity High
Test-MystTiqFile $ctx 'scripts\Test-v0.7.81.0-RouteSmoke.ps1' 'Regression' 'v0.7.81.0 new-server wizard route smoke gate remains available' -Severity High

$vmText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.82.0-era Update Center/dashboard fixes remain present, untouched by this version' `
    ($vmText -match 'UpdatePipCommand = new AsyncCommand\(UpdatePipAsync, \(\) => !IsBusy\);' -and
     $vmText -match 'public double ServerSetupTableMaxHeight') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.83.0 Contracts -- wizard step machine
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'wizard step gates exist for the new step 3-6 content, keyed off world source' `
    ($vmText -match 'IsWizardStepCloneSource => IsCreatingNewProfile && IsNewServerSetupFlow && IsWorldSourceClone && NewServerWizardStep == 3' -and
     $vmText -match 'IsWizardStepIdentityPorts => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 3' -and
     $vmText -match 'IsWizardStepWorldSettingsChoice => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 4' -and
     $vmText -match 'IsWizardStepInstallDirectory => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 5' -and
     $vmText -match 'IsWizardStepInstall => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 6') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'Connect flow keeps its own Settings/Confirm step numbers unchanged (4 and 5), gated away from the install wizard' `
    ($vmText -match 'IsWizardStepSettings => IsCreatingNewProfile && !IsNewServerSetupFlow && NewServerWizardStep == 4' -and
     $vmText -match 'IsWizardStepConfirm => IsCreatingNewProfile && NewServerWizardStep == \(IsNewServerSetupFlow \? 7 : 5\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'AdvanceWizardStep/GoBackWizardStep cover the full 1-7 step range' `
    ($vmText -match '5 => IsNewServerSetupFlow \? 6 : 5,' -and
     $vmText -match '6 => 7,' -and
     $vmText -match '7 => IsWorldSourceClone \? 3 : 6,') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.83.0 Contracts -- second-server provisioning
# ---------------------------------------------------------------------------
$fleetDtoText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\FleetDtos.cs'
Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'AddFleetProfileRequestDto/ResultDto exist client-side' `
    ($fleetDtoText -match 'public sealed class AddFleetProfileRequestDto' -and
     $fleetDtoText -match 'public sealed class AddFleetProfileResultDto') `
    -Severity Critical

$apiClientText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs'
Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'AddFleetProfileAsync posts to the existing fleet-add route' `
    ($apiClientText -match 'public async Task<AddFleetProfileResultDto> AddFleetProfileAsync' -and
     $apiClientText -match 'PostAsJsonAsync\("/api/v1/servers", request, cancellationToken\)') `
    -Severity Critical

$bootstrapperText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\LocalManagementBootstrapper.cs'
Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'RestartOwnedSidecarAsync composes Stop then EnsureAvailable, verifying the new profile is expected' `
    ($bootstrapperText -match 'Task<LocalManagementBootstrapResult> RestartOwnedSidecarAsync\(LocalInstallationSnapshot snapshot, string expectedServerProfileId, CancellationToken cancellationToken = default\)' -and
     $bootstrapperText -match 'await StopOwnedSidecarAsync\(cancellationToken\);' -and
     $bootstrapperText -match 'return await EnsureAvailableAsync\(snapshot, expectedServerProfileId, cancellationToken\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'ContinueFromInstallDirectoryAsync always registers a new profile (no free-root skip), then restarts and reconnects' `
    ($vmText -match 'private async Task ContinueFromInstallDirectoryAsync\(\)' -and
     $vmText -match 'var addResult = await _api\.AddFleetProfileAsync\(profile, request, BearerToken\);' -and
     $vmText -match 'var restartResult = await _localBootstrapper\.RestartOwnedSidecarAsync\(snapshot, candidateId\);' -and
     $vmText -match 'TargetServerId = candidateId;' -and
     $vmText -notmatch 'if \(!NewServerInstallDirectoryOccupied\)\s*\{\s*AdvanceWizardStep\(\);\s*_ = RefreshEnvironmentAsync\(\);\s*return;\s*\}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'CloneIntoNewServerAsync also restarts and reconnects before landing on Confirm (closes the previously-undisclosed gap)' `
    ($vmText -match 'var restartResult = await _localBootstrapper\.RestartOwnedSidecarAsync\(snapshot, newProfileId\);' -and
     $vmText -match 'NewServerWizardStep = 7;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'second-server provisioning declines outright for a real Windows Service install (Desktop has no restart path for it)' `
    ($vmText -match "This machine runs MystTiq as an installed Windows Service, which the desktop app can't restart on its own") `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'sibling-directory derivation mirrors CloneAsync own pattern with collision suffixing' `
    ($vmText -match 'private static string DeriveAvailableSiblingServerRoot\(string existingServerRoot, string desiredName\)' -and
     $vmText -match 'private static string SlugifyServerName\(string name\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 5. v0.7.83.0 Contracts -- bug fixes found live
# ---------------------------------------------------------------------------
$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'BuildProfileFromEditor skips the duplicate-connection guard only while the wizard has no distinct TargetServerId yet' `
    ($vmText -match 'var skipUniquenessCheckForInProgressSetup = IsNewServerSetupFlow && string\.IsNullOrWhiteSpace\(TargetServerId\);' -and
     $vmText -match 'if \(!skipUniquenessCheckForInProgressSetup\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'Step 1 header is one computed-string TextBlock, not a 3-way IsVisible-toggled row that silently stayed blank' `
    ($vmText -match 'public string WizardStep1HeaderText => IsRemoteConnectionChoice' -and
     $mainWindowXaml -match '<TextBlock Text="\{Binding WizardStep1HeaderText\}" FontSize="18" FontWeight="SemiBold" Foreground="\{DynamicResource BlueBrush\}"/>' -and
     $mainWindowXaml -notmatch 'IsVisible="\{Binding IsWizardStep1LocalNewServer\}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'SetupServerName auto-syncs into ProfileName while still the BeginNewProfile default, never overwriting an explicit rename' `
    ($vmText -match 'if \(IsNewServerSetupFlow && !string\.IsNullOrWhiteSpace\(_setupServerName\) &&\s*\(string\.IsNullOrWhiteSpace\(ProfileName\) \|\| ProfileName == "New Server"\)\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'OpenNewServerTab auto-triggers local-service detection instead of requiring a manual click' `
    ($vmText -match '_ = DetectLocalServiceAsync\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'a genuinely fresh connection auto-advances straight to World Source once connected' `
    ($vmText -match 'else if \(IsCreatingNewProfile && NewServerWizardStep == 1 && IsNewServerSetupFlow && ManagementApiConnected\)\s*AdvanceWizardStep\(\);') `
    -Severity Critical

$alertCenterText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'
Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'Alert Center disk-space prediction round 2: explicit finite checks plus a last-resort catch around AddDays' `
    ($alertCenterText -match '!double\.IsFinite\(growthPerDay\) \|\| growthPerDay <= 0' -and
     $alertCenterText -match '!double\.IsFinite\(daysRemaining\) \|\| daysRemaining > MaxProjectableDays' -and
     $alertCenterText -match 'catch \(ArgumentOutOfRangeException\)') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.83.0-new-server-wizard-second-server-support.md' `
    'v0.7.83.0 Contracts' 'v0.7.83.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 6. Removed: standalone "+" menu Clone a Server entry
# ---------------------------------------------------------------------------
$codeBehindText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' 'the standalone "Clone a Server" menu item and its command are gone' `
    ($codeBehindText -notmatch 'Clone a Server' -and
     $vmText -notmatch 'CloneServerFlowCommand' -and
     $vmText -notmatch 'HasCloneableLocalTab') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.83.0 Contracts' "Fleet's own Clone World card is untouched (tabled by direct instruction, not part of this version)" `
    ($vmText -match 'private async Task CloneWorldAsync\(\)' -and
     $mainWindowXaml -match 'Command="\{Binding CloneWorldCommand\}"') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 7. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.83.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.83\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.83.0.md' 'Documentation' 'v0.7.83.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.83.0 entry' `
    ($changelogText -match '## v0\.7\.83\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 8. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 9. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.82.0\MystTiqPalworldServer_v0.7.82.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.82.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.82.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.82.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.82.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.82.0 checkpoint logic gate still passes' `
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
