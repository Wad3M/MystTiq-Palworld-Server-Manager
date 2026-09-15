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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.13.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.13\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.13.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.12.0 explorer sidecar refresh is still present' `
    ([regex]::IsMatch($hostText, 'RefreshExplorerSidecar')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.11.0 WAN reachability diagnostics are still present' `
    ([regex]::IsMatch($coreText, 'class WanReachabilityService')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.13.0 contract presence -- api-run fleet-wide crash recovery
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.13 Contracts' 'HeadlessSupervisor exposes a reusable crash-recovery loop independent of the ensure-started-on-launch preamble' `
    ([regex]::IsMatch($coreText, 'public async Task<int> RunCrashRecoveryLoopAsync')) `
    -Severity Critical -Details 'RunAsync (service-run) must call this same method, not duplicate its logic -- see the v0.6.13.0 architecture doc.'

Add-MystTiqCheck $ctx 'v0.6.13 Contracts' 'HeadlessFleetCrashRecoveryService exists with an async Start/Stop lifecycle matching HeadlessAutomationService' `
    ([regex]::IsMatch($hostText, 'class HeadlessFleetCrashRecoveryService') -and [regex]::IsMatch($hostText, 'RunCrashRecoveryLoopAsync')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.13 Contracts' 'Every profile gets its own crash-recovery service, started and stopped alongside Automation' `
    ([regex]::IsMatch($hostText, 'CrashRecovery = crashRecovery') -and [regex]::IsMatch($hostText, 'p\.CrashRecovery\.StartAsync') -and [regex]::IsMatch($hostText, 'p\.CrashRecovery\.StopAsync')) `
    -Severity Critical -Details 'Closes the real gap the v0.6.12.0 gap audit found: crash-detect-and-restart was only ever active inside the installed OS service (service-run), and only for the default profile -- api-run (Desktop''s own sidecar, and every other fleet profile) had zero crash recovery.'

# ---------------------------------------------------------------------------
# 4. v0.6.13.0 contract presence -- per-profile installed OS services
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.13 Contracts' 'WindowsServiceManager is profile-aware and keeps the default profile''s exact prior service name' `
    ([regex]::IsMatch($coreText, 'WindowsServiceManager\(ServerProfileId profileId\)') -and [regex]::IsMatch($coreText, 'ServiceName = isDefault \? "MystTiqPalworld"')) `
    -Severity Critical -Details 'The default profile''s ServiceName must stay byte-identical to every prior version so an upgrade never orphans an already-installed service.'

Add-MystTiqCheck $ctx 'v0.6.13 Contracts' 'LinuxSystemdServiceManager is profile-aware and keeps the default profile''s exact prior unit name' `
    ([regex]::IsMatch($coreText, 'LinuxSystemdServiceManager\(IServerPathProfile paths, ServerProfileId profileId\)') -and [regex]::IsMatch($coreText, 'UnitName = isDefault \? "mysttiq-palworld\.service"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.13 Contracts' 'A non-default profile''s installed service passes --server-id so service-run supervises the right profile' `
    ([regex]::IsMatch($coreText, '--server-id \{profileId\.Value\}')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.13 Contracts' '--server-id is resolved once and threads through every direct verb (status/start/stop/restart/service-*)' `
    ([regex]::IsMatch($hostText, 'requestedServerId = GetOption\("--server-id"\)') -and [regex]::IsMatch($hostText, '--server-id <id>')) `
    -Severity Critical -Details 'Closes the separately-disclosed "--server-id selection for direct verbs" gap as a direct side effect of resolving the target profile in one shared place.'

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.13.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.13\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.6.12.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.12.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.12.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.12.0\MystTiqPalworldServer_v0.6.12.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.12.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.12.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.12.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.12.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.12.0 checkpoint logic gate still passes' `
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
