[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18302
)
$ErrorActionPreference = 'Stop'
# The logic gate runs every smoke under strict mode, where a single object has no Count property. Running
# strict here too means a standalone run reproduces the gate instead of passing where the gate would fail.
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.102.0: live coverage for the Alert Center's episode behaviour and the Doctor/Alert Center disk
# agreement. An isolated sidecar over a fixture root. The low-disk rule is set to a percentage every real
# disk is under (99), which forces the condition on, and the evaluation interval is shortened with
# MYSTTIQ_ALERT_EVAL_SECONDS. Checks: exactly one alert while the condition stays true across several
# evaluations, still exactly one after a sidecar restart, the Doctor agreeing with the Alert Center, and
# exactly one recovery notice once the condition clears. Runs against the PUBLISHED headless exe.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.102.0-alertepisodes-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') @'
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Alert Smoke",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=False,RCONPort=25575,DayTimeSpeedRate=1.000000)
'@
$failures = @()
$script:proc = $null

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

function Start-Sidecar([string]$tag) {
    $log = Join-Path $temp "headless-$tag.log"; $err = Join-Path $temp "headless-$tag.err.log"
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $script:proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err -Environment @{ MYSTTIQ_ALERT_EVAL_SECONDS = '2' }
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($script:proc.HasExited) { break }
        try { if (Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2) { return } } catch {}
    }
    throw "Sidecar did not become healthy. $(Get-Content $err -Raw -ErrorAction SilentlyContinue)"
}
function Stop-Sidecar { if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 800 } }

function Get-LowDiskNotifications([string]$base) {
    $snapshot = Invoke-RestMethod "$base/api/v1/notifications" -TimeoutSec 5
    # Plain items out; every caller wraps the call in @() so .Count works for zero, one or many under strict mode.
    $snapshot.items | Where-Object { $_.title -eq 'Low disk space' }
}
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { if (& $Condition) { return $true }; Start-Sleep -Milliseconds 700 }
    return $false
}
function Set-LowDiskPercent([string]$base, [double]$percent) {
    $rules = Invoke-RestMethod "$base/api/v1/alerts/rules" -TimeoutSec 5
    $rules.lowDiskSpace.thresholdPercent = $percent
    $null = Invoke-RestMethod "$base/api/v1/alerts/rules" -Method Put -ContentType 'application/json' -Body ($rules | ConvertTo-Json -Depth 6) -TimeoutSec 5
}

try {
    Start-Sidecar 'a'
    $base = "http://127.0.0.1:$Port"

    Test-RouteSmoke 'a condition that becomes true sends exactly one alert, and it stays one across many evaluations' {
        Set-LowDiskPercent $base 99
        if (-not (Wait-Until { @(Get-LowDiskNotifications $base).Count -ge 1 } 40)) { throw 'no low-disk alert arrived after the condition became true' }
        $first = @(Get-LowDiskNotifications $base)[0]
        if ($first.severity -ne 'Critical' -or -not $first.pinned) { throw "expected a pinned Critical alert, got $($first.severity) pinned=$($first.pinned)" }
        # With a 2-second evaluation interval the condition is checked many times here. The old behaviour repeated the alert.
        Start-Sleep -Seconds 20
        $count = @(Get-LowDiskNotifications $base).Count
        if ($count -ne 1) { throw "expected exactly 1 low-disk alert after about 10 evaluations, found $count" }
    }

    Test-RouteSmoke 'the Doctor agrees with the Alert Center about the same disk' {
        $report = Invoke-RestMethod "$base/api/v1/diagnostics/report" -TimeoutSec 60
        $disk = @($report.findings | Where-Object { $_.id -like 'resources-disk-space-*' })
        if ($disk.Count -lt 1) { throw 'no disk finding in the Doctor report' }
        $names = 'Pass', 'Warning', 'Fail', 'Starting', 'Skipped', 'Unknown'
        $state = if ($disk[0].state -is [string] -and $disk[0].state -notmatch '^\d+$') { $disk[0].state } else { $names[[int]$disk[0].state] }
        if ($state -ne 'Fail') { throw "the Alert Center calls this disk Critical, so the Doctor must not call it $state" }
        if ($disk[0].evidence -notmatch '\(\d+(\.\d+)?%\)') { throw "the Doctor evidence should state the percentage: $($disk[0].evidence)" }
    }

    Test-RouteSmoke 'restarting the sidecar does not repeat an alert for a condition that is still true' {
        Stop-Sidecar
        Start-Sidecar 'b'
        # Give the restarted sidecar time to run its first evaluation (its tick is 15 s), so the persisted state, not
        # a fresh first look, is what is being tested.
        Start-Sleep -Seconds 25
        $count = @(Get-LowDiskNotifications $base).Count
        if ($count -ne 1) { throw "expected still exactly 1 low-disk alert after a restart, found $count" }
        if (-not (Test-Path (Join-Path $runtime 'alerts\episodes.json'))) { throw 'the episode state file was not persisted' }
    }

    Test-RouteSmoke 'when the condition clears, exactly one recovery notice is sent and nothing repeats' {
        Set-LowDiskPercent $base 1
        $recovered = Wait-Until { @((Invoke-RestMethod "$base/api/v1/notifications").items | Where-Object { $_.title -eq 'Resolved: Low disk space' }).Count -ge 1 } 40
        if (-not $recovered) { throw 'no recovery notice arrived after the condition cleared' }
        Start-Sleep -Seconds 12
        $resolved = @((Invoke-RestMethod "$base/api/v1/notifications").items | Where-Object { $_.title -eq 'Resolved: Low disk space' })
        if ($resolved.Count -ne 1) { throw "expected exactly 1 recovery notice, found $($resolved.Count)" }
        if ($resolved[0].severity -ne 'Success') { throw "a recovery should be a Success notice, got $($resolved[0].severity)" }
        if (@(Get-LowDiskNotifications $base).Count -ne 1) { throw 'the alert count changed after recovery' }
    }
}
finally {
    Stop-Sidecar
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.102.0 alert episodes smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.102.0 alert episodes smoke gate passed." -ForegroundColor Green
