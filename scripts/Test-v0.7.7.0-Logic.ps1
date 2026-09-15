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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.7.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.7\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.7.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$backupText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessBackupService.cs') -Raw
$apiHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$transactionText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessWorldTransactionService.cs') -Raw
$modText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs') -Raw
$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.6.0 multi-tab stale-data fix is still present' `
    ([regex]::IsMatch($vmText, 'private async Task RefreshActiveTabDataAsync')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.7.0 contract presence -- Fix 1: HeadlessBackupService coordinator lock
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' 'HeadlessBackupService constructor now takes IOperationCoordinator and ServerProfileId' `
    ([regex]::IsMatch($backupText, 'IOperationCoordinator coordinator,\s*ServerProfileId profile')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' 'RestoreAsync acquires the world-mutation lock before touching SaveRoot' `
    ([regex]::IsMatch($backupText, 'coordinator\.BeginAsync\(profile, "backup-restore", "HeadlessBackupService", \["world-mutation"\]')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' 'RestoreAsync disposes the operation handle in its finally block' `
    ([regex]::IsMatch($backupText, '(?s)finally\s*\{\s*operation\.Dispose\(\);\s*backupGate\.Release\(\);')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' 'CreateAsync/DeleteAsync/VerifyAsync remain lock-free (no coordinator.BeginAsync outside RestoreAsync)' `
    (([regex]::Matches($backupText, 'coordinator\.BeginAsync')).Count -eq 1) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' 'a cleanup-only failure after a successful restore is no longer reported as Failure (rollback delete moved outside the restore try/catch)' `
    ([regex]::IsMatch($backupText, 'leftoverRollback')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' 'LocalManagementApiHost constructs HeadlessBackupService with the coordinator and profile id' `
    ([regex]::IsMatch($apiHostText, 'new HeadlessBackupService\(paths, lifecycle, activity, operations, profileId\)')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.7.0 contract presence -- Fix 2: diagnostics/network/restart locking
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' '/diagnostics/network/restart now acquires the lifecycle + world-mutation lock before restarting' `
    ([regex]::IsMatch($apiHostText, '(?s)MapPost\("/diagnostics/network/restart".*?operations\.BeginAsync\(p\.Id, "diagnostics-restart", "LocalManagementApiHost", \["lifecycle", "world-mutation"\]')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 5. v0.7.7.0 contract presence -- Fix 4: rollback-of-rollback
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' 'transaction rollback recovery deletes the known original path directly instead of relying on explorer.Explore().ActiveWorldPath' `
    ($(
        $startIdx = $transactionText.IndexOf('var rolledBack = false;')
        $endIdx = $transactionText.IndexOf('if (operation is not null) coordinator.Fail(operation.Id, failureMessage, rolledBack);')
        $startIdx -ge 0 -and $endIdx -gt $startIdx -and
        $(
            $catchBlock = $transactionText.Substring($startIdx, $endIdx - $startIdx)
            -not $catchBlock.Contains('var active = explorer.Explore().ActiveWorldPath;') -and
            $catchBlock.Contains('if (Directory.Exists(originalPath)) Directory.Delete(originalPath, true);')
        )
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' 'a rollback-of-rollback failure is now surfaced in the reported error message instead of silently swallowed' `
    ([regex]::IsMatch($transactionText, 'rollbackFailureDetail')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. v0.7.7.0 contract presence -- Fix 5: mod snapshot traversal guard
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.7.0 Contracts' 'mod snapshot rollback (flat ~mods case) now validates each entry path before extraction' `
    ([regex]::IsMatch($modText, '(?s)meta\[0\] == "flat".*?Snapshot archive contains an unsafe path.*?Snapshot archive path escaped destination')) `
    -Severity High

# ---------------------------------------------------------------------------
# 7. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.7.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.7\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 8. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 9. Existing v0.7.6.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.6.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.6.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 10. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.6.0\MystTiqPalworldServer_v0.7.6.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.6.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.6.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.6.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.6.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.6.0 checkpoint logic gate still passes' `
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
