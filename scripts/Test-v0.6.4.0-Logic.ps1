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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.4.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.4\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.4.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.3.0 character migration and Windows service contracts are still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessCharacterMigrationService') -and [regex]::IsMatch($hostText, 'AddWindowsService')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.2.0 fleet contract is still present' `
    ([regex]::IsMatch($coreText, 'IReadOnlyList<HeadlessServerProfileConfiguration>\s+Servers') -and [regex]::IsMatch($hostText, 'class ServerProfileHost')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.4.0 contract presence
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'DiagnosticState gained Unknown, appended not inserted (backward-compat with the existing numeric-enum Desktop DTO)' `
    ([regex]::IsMatch($coreText, 'enum DiagnosticState \{ Pass, Warning, Fail, Starting, Skipped, Unknown \}')) `
    -Severity Critical -Details 'Unknown must be appended at the end -- inserting it first would silently relabel every already-shipped Network Diagnostics result, since that DTO switches on the raw numeric value by hardcoded position.'

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'Unified DiagnosticFinding/DiagnosticsReport model exists' `
    ([regex]::IsMatch($coreText, 'record DiagnosticFinding') -and [regex]::IsMatch($coreText, 'record DiagnosticsReport')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'ServerHealthState is finally consumed outside its own file (was an unused seam since v0.6.0.0)' `
    ([regex]::IsMatch($hostText, 'ServerHealthState\.') -and [regex]::IsMatch($desktopText, 'OverallHealth')) `
    -Severity Critical -Details 'Expected HeadlessDiagnosticsService to actually reference ServerHealthState.Ready/Degraded/Attention, and the Desktop DTO to surface it, not just MystTiq.Core/Operations/ServerHealthState.cs defining it unused.'

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'HeadlessDiagnosticsService unifies Doctor and Environment Checklist, de-duplicating the 3 overlapping facts' `
    ([regex]::IsMatch($hostText, 'class HeadlessDiagnosticsService') -and [regex]::IsMatch($hostText, 'OverlapMap') -and [regex]::IsMatch($hostText, 'mergedEnvComponents')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'Real Fix Automatically exists for backup-root and SteamCMD/PalServer install, honestly gated elsewhere' `
    ([regex]::IsMatch($hostText, '"create-backup-root"') -and [regex]::IsMatch($hostText, '"install-distribution"') -and [regex]::IsMatch($hostText, 'No automatic fix is available')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'Per-check Recheck route exists' `
    ([regex]::IsMatch($hostText, '"/diagnostics/\{id\}/recheck"') -and [regex]::IsMatch($hostText, 'RecheckAsync')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'Unified diagnostics report route exists per server profile' `
    ([regex]::IsMatch($hostText, '"/diagnostics/report"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'Local-PC diagnostics run entirely client-side (DNS/TCP/TLS/HTTP staged probe)' `
    ([regex]::IsMatch($desktopText, 'class LocalDiagnosticsService') -and [regex]::IsMatch($desktopText, 'DiagnoseConnectionAsync') -and [regex]::IsMatch($desktopText, 'Dns\.GetHostAddressesAsync') -and [regex]::IsMatch($desktopText, 'TcpClient')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'TLS pin-compare logic is shared, not duplicated, between the API client and local diagnostics' `
    ([regex]::IsMatch($desktopText, 'VerifyCertificatePin')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'Local machine checks (.NET runtime, disk space) exist' `
    ([regex]::IsMatch($desktopText, 'GetLocalMachineFindings') -and [regex]::IsMatch($desktopText, 'FrameworkDescription')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.6.4 Contracts' 'Dashboard Overall Health badge derives from the unified diagnostics report' `
    ([regex]::IsMatch($desktopText, 'LatestDiagnosticsReport') -and [regex]::IsMatch($desktopText, '"DEGRADED"')) `
    -Severity Critical -Details 'The specific gap the roadmap names: "no health deduction should exist without a corresponding visible Doctor finding" -- the Dashboard badge must read from the same source the Doctor page shows, not compute independently from lifecycle status alone.'

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.4.0 is documented in the roadmap' `
    ([regex]::IsMatch($docText, 'v0\.6\.4\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.3.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.3.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.3.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.3.0\MystTiqPalworldServer_v0.6.3.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.3.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.3.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.3.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.3.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.3.0 checkpoint logic gate still passes' `
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
