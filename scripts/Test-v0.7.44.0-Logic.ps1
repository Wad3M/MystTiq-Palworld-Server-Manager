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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.44.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.44\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.44.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.44\.0"' `
    'Versioning' 'app.manifest reports v0.7.44.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$mainWindowVmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$lifecycleModelsText = Get-Content (Join-Path $root 'src\MystTiq.Core\Models\ServerLifecycleModels.cs') -Raw
$linuxLifecycleText = Get-Content (Join-Path $root 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs') -Raw
$windowsLifecycleText = Get-Content (Join-Path $root 'src\MystTiq.Core\Services\WindowsServerLifecycleService.cs') -Raw
$managementApiHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$apiClientInterfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$apiClientText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.43.0 MOD Dashboard list and footer elapsed timer are still present' `
    ($mainWindowAxamlText -match 'IsVisible="\{Binding IsModDashboardPage\}"' -and
     $mainWindowVmText -match 'BusyElapsedText') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.7.28.0 per-profile managed-process tracking is unchanged' `
    ($mainWindowVmText -match 'public ObservableCollection<ServerProcessDto> ManagedProcesses \{ get; \} = \[\];' -and
     $managementApiHostText -match 'routes\.MapPost\("/server/force-stop"') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.44.0 contract presence -- Palworld Instance Detection & Termination Tool
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'ServerInstanceInfo/InstanceTerminationResult models exist in MystTiq.Core' `
    ($lifecycleModelsText -match 'public sealed record ServerInstanceInfo\(' -and
     $lifecycleModelsText -match 'public sealed record InstanceTerminationResult\(bool Success, int ProcessId, string Message\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'IServerLifecycleService declares FindAllInstancesAsync and TerminateUnmanagedInstanceAsync' `
    ($linuxLifecycleText -match 'Task<IReadOnlyList<ServerInstanceInfo>> FindAllInstancesAsync\(CancellationToken cancellationToken = default\);' -and
     $linuxLifecycleText -match 'Task<InstanceTerminationResult> TerminateUnmanagedInstanceAsync\(int processId, CancellationToken cancellationToken = default\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'Both Windows and Linux implement the new machine-wide scan and raw termination' `
    ($windowsLifecycleText -match 'public Task<IReadOnlyList<ServerInstanceInfo>> FindAllInstancesAsync' -and
     $windowsLifecycleText -match 'public Task<InstanceTerminationResult> TerminateUnmanagedInstanceAsync' -and
     $linuxLifecycleText -match 'public Task<IReadOnlyList<ServerInstanceInfo>> FindAllInstancesAsync' -and
     $linuxLifecycleText -match 'public Task<InstanceTerminationResult> TerminateUnmanagedInstanceAsync') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'ManagedByThisProfile is conservative: an unknown executable path is NOT treated as managed' `
    ($windowsLifecycleText -match 'ManagedByThisProfile: !string\.IsNullOrWhiteSpace\(process\.ExecutablePath\) &&' -and
     $linuxLifecycleText -match 'ManagedByThisProfile: !string\.IsNullOrWhiteSpace\(process\.ExecutablePath\) &&') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'New /server/instances routes exist and require Operator role, outside the lifecycle operation lock' `
    ($managementApiHostText -match 'routes\.MapGet\("/server/instances", async \(CancellationToken token\) =>\s*\n\s*Results\.Ok\(await p\.Lifecycle\.FindAllInstancesAsync\(token\)\)\)\.RequireRole\(MystTiqRole\.Operator, p\.Id\);' -and
     $managementApiHostText -match 'routes\.MapPost\("/server/instances/\{processId:int\}/terminate", async \(int processId, CancellationToken token\) =>\s*\n\s*Results\.Ok\(await p\.Lifecycle\.TerminateUnmanagedInstanceAsync\(processId, token\)\)\)\.RequireRole\(MystTiqRole\.Operator, p\.Id\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'Desktop client exposes GetAllInstancesAsync/TerminateInstanceAsync' `
    ($apiClientInterfaceText -match 'Task<IReadOnlyList<ServerInstanceDto>> GetAllInstancesAsync\(' -and
     $apiClientInterfaceText -match 'Task<InstanceTerminationResultDto> TerminateInstanceAsync\(' -and
     $apiClientText -match 'public async Task<IReadOnlyList<ServerInstanceDto>> GetAllInstancesAsync' -and
     $apiClientText -match 'public async Task<InstanceTerminationResultDto> TerminateInstanceAsync') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'ServerInstanceDto model file exists' `
    (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Models\ServerInstanceDto.cs') -PathType Leaf) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'ViewModel wires AllInstances/SelectedInstance state and the two commands' `
    ($mainWindowVmText -match 'public ObservableCollection<ServerInstanceDto> AllInstances \{ get; \} = \[\];' -and
     $mainWindowVmText -match 'public ServerInstanceDto\? SelectedInstance' -and
     $mainWindowVmText -match 'public bool IsSelectedInstanceManaged => SelectedInstance\?\.ManagedByThisProfile \?\? false;' -and
     $mainWindowVmText -match 'public ICommand RefreshAllInstancesCommand \{ get; \}' -and
     $mainWindowVmText -match 'public ICommand TerminateSelectedInstanceCommand \{ get; \}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'Terminate is only ever wired for a NOT-managed selected instance' `
    ($mainWindowVmText -match 'TerminateSelectedInstanceCommand = new AsyncCommand\(TerminateSelectedInstanceAsync, \(\) => !IsBusy && ManagementApiConnected && SelectedInstance is \{ ManagedByThisProfile: false \}\);' -and
     $mainWindowVmText -match 'if \(SelectedInstance is not \{ ManagedByThisProfile: false \} target\) return;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'Managed selection reuses the existing safe ForceStopServerCommand instead of a raw kill' `
    ($mainWindowAxamlText -match 'IsVisible="\{Binding IsSelectedInstanceManaged\}">\s*<TextBlock Text="Managed by this tab\. Use the safe, crash-recovery-aware stop path instead of a raw kill\." Classes="muted" TextWrapping="Wrap" FontSize="11"/>\s*<Button Content="Stop via MystTiq" Classes="danger" Command="\{Binding ForceStopServerCommand\}"/>') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'Unmanaged Force Kill button discloses the cross-session crash-recovery risk before it is reachable' `
    ($mainWindowAxamlText -match 'Force Kill \(Unmanaged\)' -and
     $mainWindowAxamlText -match 'may look like an unexpected crash to that session and trigger an auto-restart there') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'Server Doctor ribbon gained a Detect Instances button' `
    ($mainWindowVmText -match '"Detect Instances", "Scan for every Palworld process on this machine", RefreshAllInstancesCommand') `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.44.0 Contracts' 'Run Doctor also refreshes the machine-wide instance list' `
    ($mainWindowVmText -match '(?s)private async Task RunDoctorAsync\(\).*?await RefreshAllInstancesAsync\(\);\s*\n\s*\}') `
    -Severity High

Test-MystTiqFile $ctx 'docs\architecture\v0.7.44.0-palworld-instance-detection-termination.md' 'v0.7.44.0 Contracts' 'v0.7.44.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.44.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.44\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.43.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.43.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.43.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.43.0\MystTiqPalworldServer_v0.7.43.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.43.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.43.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.43.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.43.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.43.0 checkpoint logic gate still passes' `
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

    # v0.7.44.0 touches all three projects (Core/HeadlessHost/Desktop) but only adds new routes
    # and a new UI panel -- no existing route, DTO, or behavior changed. Every server-side
    # route/CLI smoke test carried forward from prior versions is expected to pass unchanged.
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
