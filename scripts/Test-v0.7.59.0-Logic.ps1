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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.59.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.59\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.59.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.59\.0"' `
    'Versioning' 'app.manifest reports v0.7.59.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

$safeStartText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModSafeStartService.cs') -Raw
$hostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$profileHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\ServerProfileHost.cs') -Raw
$modDtoText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Models\ModManagementDtos.cs') -Raw
$apiInterfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$apiClientText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw
$viewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$mainWindowAxamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.55.0 SetEnabledAsync stop-server precondition is unchanged (Safe-Start relies on it)' `
    ($hostText -match 'Stop PalServer before enabling or disabling MOD files\.' -or
     (Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs') -Raw) -match 'Stop PalServer before enabling or disabling MOD files\.') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.59.0 contract presence -- Safe-Start MOD Diagnostic
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\HeadlessModSafeStartService.cs' 'v0.7.59.0 Contracts' 'New service file is present' -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'Detection covers BOTH crash and startup-timeout hang, not crash alone' `
    ($safeStartText -match 'result\.Snapshot\.CrashDetected' -and $safeStartText -match 'HeadlessExitCode\.StartupTimeout') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'A baseline sanity check (everything disabled) runs before the per-MOD loop, aborting as not-mod-related on failure' `
    ($safeStartText -match 'job\.FinishNotModRelated\(DescribeFailure\(baseline\)\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'MODs are tested cumulatively (re-enabled on top of the confirmed-good set), not each in total isolation' `
    ($safeStartText -match '(?s)foreach \(var mod in candidates\)\s*\{\s*if \(ct\.IsCancellationRequested\) break;.*?await modManagement\.SetEnabledAsync\(mod\.Type, mod\.Package, true, CancellationToken\.None\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'A failed candidate is disabled again (reverted) rather than left enabled' `
    ($safeStartText -match '(?s)job\.RecordResult\(mod\.Package, ok: false.*?SetEnabledAsync\(mod\.Type, mod\.Package, false, CancellationToken\.None\);' -or
     $safeStartText -match '(?s)await modManagement\.SetEnabledAsync\(mod\.Type, mod\.Package, false, CancellationToken\.None\);\s*job\.RecordResult\(mod\.Package, ok: false') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'Cancellation restores every originally-enabled candidate rather than leaving partial state' `
    ($safeStartText -match '(?s)if \(ct\.IsCancellationRequested\)\s*\{\s*foreach \(var mod in candidates\)\s*await modManagement\.SetEnabledAsync\(mod\.Type, mod\.Package, true, CancellationToken\.None\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'The OperationCoordinator handle is acquired in BeginAsync and held for the ENTIRE background job (Complete/Fail/Dispose happen inside RunAsync''s finally), not released after the synchronous call returns' `
    ($safeStartText -match 'handle = await operations\.BeginAsync\(profileId, "mods-safe-start"' -and
     $safeStartText -match '_ = Task\.Run\(\(\) => RunAsync\(job, candidates, handle, job\.Cts\.Token\)\);' -and
     $safeStartText -match '(?s)private async Task RunAsync\(.*?finally\s*\{.*?operations\.Complete\(handle\.Id.*?operations\.Fail\(handle\.Id.*?handle\.Dispose\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'ServerProfileHost exposes ModSafeStart and LocalManagementApiHost constructs/wires it with the fleet OperationCoordinator and this profile''s own timeouts' `
    ($profileHostText -match 'public required HeadlessModSafeStartService ModSafeStart \{ get; init; \}' -and
     $hostText -match 'new HeadlessModSafeStartService\(\s*lifecycle, modManagement, serverConfig, activity, operations, profileId,' -and
     $hostText -match 'ModSafeStart = modSafeStart') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'Three new routes are registered: begin, status (poll), cancel' `
    ($hostText -match 'routes\.MapPost\("/mods/safe-start", async \(CancellationToken token\) =>' -and
     $hostText -match 'routes\.MapGet\("/mods/safe-start/status", \(\) =>' -and
     $hostText -match 'routes\.MapPost\("/mods/safe-start/cancel", async \(CancellationToken token\) =>') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'Desktop DTOs (SafeStartStatusDto/SafeStartModResultDto) and all three client methods are defined' `
    ($modDtoText -match 'public sealed class SafeStartStatusDto' -and
     $modDtoText -match 'public sealed class SafeStartModResultDto' -and
     $apiInterfaceText -match 'Task<ModMutationResultDto> BeginModSafeStartAsync\(' -and
     $apiInterfaceText -match 'Task<SafeStartStatusDto\?> GetModSafeStartStatusAsync\(' -and
     $apiInterfaceText -match 'Task<ModMutationResultDto> CancelModSafeStartAsync\(' -and
     $apiClientText -match 'public async Task<ModMutationResultDto> BeginModSafeStartAsync\(' -and
     $apiClientText -match 'public async Task<SafeStartStatusDto\?> GetModSafeStartStatusAsync\(' -and
     $apiClientText -match 'public async Task<ModMutationResultDto> CancelModSafeStartAsync\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'The status GET call returns null on 404 instead of throwing (GetFromJsonAsync would throw)' `
    ($apiClientText -match '(?s)GetModSafeStartStatusAsync.*?StatusCode == System\.Net\.HttpStatusCode\.NotFound\) return null;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'ViewModel polling is on its own DispatcherTimer, independent of IsBusy' `
    ($viewModelText -match 'private DispatcherTimer\? _modSafeStartPollTimer;' -and
     $viewModelText -match 'private void StartModSafeStartPolling\(\)' -and
     $viewModelText -match '(?s)private async Task BeginModSafeStartAsync\(\)\s*\{.*?finally \{ IsBusy = false; \}\s*StartModSafeStartPolling\(\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'Poll loop stops itself once the job reports Completed, and refreshes the MOD inventory afterward' `
    ($viewModelText -match '(?s)if \(status is null \|\| status\.Completed\)\s*\{\s*_modSafeStartPollTimer\?\.Stop\(\);.*?ApplyModInventory\(inventory\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'MOD Library has a Safe-Start Diagnostic button and a live-progress card bound to ModSafeStartStatus' `
    ($mainWindowAxamlText -match 'Content="Safe-Start Diagnostic".*?Command="\{Binding BeginModSafeStartCommand\}"' -and
     $mainWindowAxamlText -match 'IsVisible="\{Binding HasModSafeStartStatus\}"' -and
     $mainWindowAxamlText -match 'Command="\{Binding CancelModSafeStartCommand\}"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.59.0-safe-start-mod-diagnostic.md' 'v0.7.59.0 Contracts' 'v0.7.59.0 architecture doc is present' -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.59.0 Contracts' 'Architecture doc discloses the never-run-against-a-real-server gap and the in-memory-only job-state gap' `
    ((Get-Content (Join-Path $root 'docs\architecture\v0.7.59.0-safe-start-mod-diagnostic.md') -Raw) -match 'Never run against a real PalServer' -and
     (Get-Content (Join-Path $root 'docs\architecture\v0.7.59.0-safe-start-mod-diagnostic.md') -Raw) -match 'Job state is in-memory only') `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.59.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.59\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.58.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.58.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.58.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.58.0\MystTiqPalworldServer_v0.7.58.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.58.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.58.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.58.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.58.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.58.0 checkpoint logic gate still passes' `
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
