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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.17.0' -Suite 'Logic'

# ===========================================================================
# GOAL
# Fix api-remote-enable/api-remote-disable being rejected by the blanket
# pre-command effective-configuration validation gate, which compared the new
# --bind-address against the OLD not-yet-updated Authentication/Tls flags
# before EnableRemoteApi ever ran. Closes the CLI bug flagged (found, not
# fixed) in v0.7.13.0.
# ===========================================================================

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.17\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.17.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.17\.0"' `
    'Versioning' 'app.manifest reports v0.7.17.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\HeadlessTemporaryBanService.cs' 'Regression' 'v0.7.15.0 temporary ban service is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical

$mainWindowViewModelText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
Add-MystTiqCheck $ctx 'Regression' 'v0.7.16.0 responsive tab strip (VisibleTabs/OverflowTabs) is still present' `
    ($mainWindowViewModelText -match [regex]::Escape('public ObservableCollection<TabSession> VisibleTabs')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.17.0 contract presence -- api-remote-enable/api-remote-disable no
#    longer gated by the blanket effective-configuration pre-check
# ---------------------------------------------------------------------------
$programText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\Program.cs') -Raw

Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'Program.cs computes a skip flag for api-remote-enable/api-remote-disable before the effective-configuration validation gate' `
    ($programText -match 'skipsEffectiveValidation\s*=\s*command\.Equals\("api-remote-enable"' -and
     $programText -match 'command\.Equals\("api-remote-disable"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'the effective-configuration validation gate itself is now conditional on that skip flag' `
    ($programText -match [regex]::Escape('if (!skipsEffectiveValidation)') -and
     $programText -match [regex]::Escape('Effective configuration validation failed after command-line overrides')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'api-remote-enable''s own case handler (HeadlessRemoteApiEnrollmentService.EnableRemoteApi) is unchanged and still reachable' `
    ($programText -match [regex]::Escape('new HeadlessRemoteApiEnrollmentService(configurationService)') -and
     $programText -match [regex]::Escape('.EnableRemoteApi(')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'api-run/service-run still validate the effective configuration (the pre-check is narrowed, not removed)' `
    ($programText -match [regex]::Escape('var effectiveValidation = configurationService.Validate(effectiveHeadlessConfiguration);')) `
    -Severity Critical

$apiHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'LocalManagementApiHost''s own independent non-loopback auth+TLS guard is still present (defense in depth, unaffected by this fix)' `
    ($apiHostText -match [regex]::Escape('Non-loopback management API requires both authentication and TLS.')) `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' `
    'v0.7.17.0 Contracts' 'permanent real-CLI regression coverage for the fixed flow exists' -Severity Critical

# ---------------------------------------------------------------------------
# 3b. v0.7.17.0 contract presence -- Linux acceptance/deploy pipeline restored
#     (Build.ps1 LinuxHeadless had been broken for every release since v0.6.1.0,
#     found while verifying this release's own Linux fix)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-LinuxAcceptance.sh' `
    'v0.7.17.0 Contracts' 'version-specific Linux acceptance script exists (Build.ps1 LinuxHeadless hard-requires it)' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-ProductionReadiness.sh' `
    'v0.7.17.0 Contracts' 'version-specific Linux production-readiness script exists (Build.ps1 LinuxHeadless hard-requires it)' -Severity Critical

$linuxAcceptanceText = Get-Content (Join-Path $root 'scripts\Test-v0.7.17.0-LinuxAcceptance.sh') -Raw
$linuxProdReadinessText = Get-Content (Join-Path $root 'scripts\Test-v0.7.17.0-ProductionReadiness.sh') -Raw
Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'Linux acceptance script gives ephemeral CLI invocations an isolated --runtime-root (avoids the root-owned /opt/mysttiq/runtime permission crash)' `
    ($linuxAcceptanceText -match [regex]::Escape('--runtime-root "$scratch_runtime"') -and $linuxAcceptanceText -match [regex]::Escape('--runtime-root "$authapi_runtime"')) `
    -Severity Critical
Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'Linux production-readiness script gives its CLI invocations an isolated --runtime-root too' `
    ($linuxProdReadinessText -match [regex]::Escape('--runtime-root "$scratch_runtime"')) `
    -Severity Critical
Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'both Linux scripts scan a recent journal window, not the whole current boot (avoids an unrecoverable stale-event gate on a long-uptime host)' `
    ($linuxAcceptanceText -match [regex]::Escape('--since "-30 minutes"') -and $linuxProdReadinessText -match [regex]::Escape('--since "-30 minutes"')) `
    -Severity High
Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'Linux acceptance script exercises the broadened current-version API surface (read-only GET sweep, safe POST self-tests, and reversible create/delete roundtrips), not just the v0.6.1.0-era subset' `
    ($linuxAcceptanceText -match [regex]::Escape('get_ok "Player registry endpoint"') -and
     $linuxAcceptanceText -match [regex]::Escape('post_reachable "Guild ownership preview') -and
     $linuxAcceptanceText -match [regex]::Escape('automation_create_code=')) `
    -Severity High

$palworldConfigText = Get-Content (Join-Path $root 'src\MystTiq.Core\Services\PalworldSettingsConfigurationService.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'ValidateDefaultRequest is null-safe (real NullReferenceException found via the broadened Linux acceptance pass, on any request omitting an optional string field)' `
    ($palworldConfigText -match [regex]::Escape('(request.ServerName?.Length ?? 0)') -and
     $palworldConfigText -match [regex]::Escape('(request.ServerDescription?.Length ?? 0)') -and
     $palworldConfigText -match [regex]::Escape('if (value is not null && (value.Contains')) `
    -Severity Critical

$backupServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessBackupService.cs') -Raw
Add-MystTiqCheck $ctx 'v0.7.17.0 Contracts' 'HeadlessBackupRetentionRequest.IncludeClasses is JSON-deserializable (real 500 found via real-save-data mutation testing on Linux: System.Text.Json cannot populate an IReadOnlySet<T> from a JSON array)' `
    ($backupServiceText -match [regex]::Escape('IReadOnlyCollection<BackupClass>? IncludeClasses = null')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.17.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.17\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.16.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.16.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.16.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.16.0\MystTiqPalworldServer_v0.7.16.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.16.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.16.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.16.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.16.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.16.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'New Tests' 'v0.7.17.0 api-remote-enable smoke gate passes (the actual regressed CLI flow, end to end)' {
        & (Join-Path $root 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
