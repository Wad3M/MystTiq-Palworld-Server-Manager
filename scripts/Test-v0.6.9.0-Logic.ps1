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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.9.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.9\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.9.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.8.0 MOD rollback and Alert Center MOD integration are still present' `
    ([regex]::IsMatch($hostText, 'RollbackAsync') -and [regex]::IsMatch($hostText, 'ModHealthDegraded')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.1.0 Automation engine is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessAutomationService')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.9.0 contract presence -- Idle auto-stop with warning and final player recheck
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.9 Contracts' 'IdleEmpty is a real trigger kind, deliberately excluded from the fixed-schedule NextDueUtc polling path' `
    ([regex]::IsMatch($coreText, 'enum AutomationTriggerKind \{ DailyTime, Interval, IdleEmpty \}') -and [regex]::IsMatch($hostText, 'Trigger\.Kind != AutomationTriggerKind\.IdleEmpty')) `
    -Severity Critical -Details 'IdleEmpty is continuously-observed live state, not a computable future timestamp -- if it were folded into ComputeNextDue, it could never correctly represent "still idle" vs "just became idle."'

Add-MystTiqCheck $ctx 'v0.6.9 Contracts' 'Idle duration is tracked and reset per real observed player count and server running state, not assumed' `
    ([regex]::IsMatch($hostText, 'EvaluateIdleRulesAsync') -and [regex]::IsMatch($hostText, 'rule\.IdleSinceUtc \?\?= now') -and [regex]::IsMatch($hostText, 'status\.Phase != ServerLifecyclePhase\.Running \|\| players\.OnlineCount > 0')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.9 Contracts' 'The already-proven warning-countdown/RCON-broadcast/StopServer execution path is reused unchanged for idle-triggered stops' `
    ([regex]::IsMatch($hostText, '_ = Task\.Run\(\(\) => ExecuteRuleAsync\(rule, now, hostToken\), hostToken\);')) `
    -Severity Critical -Details 'Idle rules must execute through the same ExecuteRuleAsync/RunLifecycleActionAsync pipeline every other trigger kind uses -- a separate execution path would mean the warning countdown and coordinator locking were never actually proven for this trigger kind.'

Add-MystTiqCheck $ctx 'v0.6.9 Contracts' 'A final live player recheck runs immediately before the actual stop, aborting if a player joined during the warning countdown' `
    ([regex]::IsMatch($hostText, 'abortIfPlayerPresent') -and [regex]::IsMatch($hostText, 'abortIfTrue is not null && await abortIfTrue\(\)') -and [regex]::IsMatch($hostText, 'a player was online at the final recheck')) `
    -Severity Critical -Details 'This is the specific "final player recheck" the roadmap names -- without it, a player who joins mid-countdown (after the idle check but before the stop) would still get disconnected.'

Add-MystTiqCheck $ctx 'v0.6.9 Contracts' 'Idle Auto-Stop is configurable from the Desktop Automation page (trigger kind, threshold minutes)' `
    ([regex]::IsMatch($desktopText, '"IdleEmpty"') -and [regex]::IsMatch($desktopText, 'NewAutomationIdleThresholdMinutes') -and [regex]::IsMatch($desktopText, 'IdleThresholdMinutes')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.9.0 is documented in the roadmap' `
    ([regex]::IsMatch($docText, 'v0\.6\.9\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.8.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.8.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.8.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.8.0\MystTiqPalworldServer_v0.6.8.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.8.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.8.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.8.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.8.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.8.0 checkpoint logic gate still passes' `
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
