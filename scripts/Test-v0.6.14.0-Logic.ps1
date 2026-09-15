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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.14.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.14\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.14.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.13.0 fleet-wide crash recovery is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessFleetCrashRecoveryService') -and [regex]::IsMatch($hostText, 'RunCrashRecoveryLoopAsync')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.14.0 contract presence -- log-tail merge starvation fix
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.14 Contracts' 'The multi-source log-tail merge no longer applies a trailing global TakeLast that can silently evict an earlier, fresher source' `
    ([regex]::IsMatch($hostText, '(?m)^\s*merged,\s*$') -and -not [regex]::IsMatch($hostText, 'string\.Join\(" \+ ", sources\.Select\(x => x\.Label\)\),\s*\n\s*merged\.TakeLast')) `
    -Severity Critical -Details 'Reproduced live: a real Start operation''s fresh MystTiq lifecycle/stdout lines (added first) were completely evicted from the merged tail by Pal.log''s own chunk (added second, with plenty of historical volume) whenever the requested line count was small enough for the per-source floor to dominate. Fixed by returning each source''s own independently-bounded chunk without a further blind global crop.'

Add-MystTiqCheck $ctx 'v0.6.14 Contracts' 'Each merged source is still independently bounded (no unbounded growth regression)' `
    ([regex]::IsMatch($hostText, 'var perSource = Math\.Max\(12, maxLines / sources\.Count\)') -and [regex]::IsMatch($hostText, 'ReadTailLines\(source\.Path, perSource, 1024 \* 1024\)')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.6.14.0 contract presence -- console visible during Start/Stop/Restart
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.14 Contracts' 'RunLifecycleAsync polls the console tail while the lifecycle operation is in flight, not just once after it completes' `
    ([regex]::IsMatch($desktopText, 'PollConsoleWhileBusyAsync') -and [regex]::IsMatch($desktopText, 'consoleTailLoop = PollConsoleWhileBusyAsync')) `
    -Severity Critical -Details 'Root cause: the passive auto-refresh timer skips its tick entirely while IsBusy, which RunLifecycleAsync sets for the whole duration of a Start/Stop/Restart -- previously ApplyLogs was only called once, after the operation fully completed, so the Console page showed nothing new for the entire transition. This is what the user reported directly.'

Add-MystTiqCheck $ctx 'v0.6.14 Contracts' 'The console-tail poll loop is cancelled cleanly when the lifecycle operation finishes' `
    ([regex]::IsMatch($desktopText, 'consoleTailCts\.Cancel\(\)') -and [regex]::IsMatch($desktopText, 'await consoleTailLoop')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.14.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.14\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.6.13.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.13.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.13.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.13.0\MystTiqPalworldServer_v0.6.13.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.13.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.13.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.13.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.13.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.13.0 checkpoint logic gate still passes' `
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
