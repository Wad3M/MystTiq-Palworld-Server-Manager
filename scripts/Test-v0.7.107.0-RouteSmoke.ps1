[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18307
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

# v0.7.107.0: live coverage for reminder re-alerts -- a condition that stays true for a long time now gets a
# follow-up "Still active: ..." notice every reminder interval, not just the one alert at the start. An
# isolated sidecar over a fixture with the low-disk rule forced on (99% threshold) and both
# MYSTTIQ_ALERT_EVAL_SECONDS and MYSTTIQ_ALERT_REMINDER_MINUTES shortened so a real interval elapses inside the
# smoke instead of 24 real hours. Runs against the PUBLISHED headless exe.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.107.0-reminders-" + [guid]::NewGuid().ToString('N'))
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
OptionSettings=(ServerName="Reminder Smoke",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=False,RCONPort=25575,DayTimeSpeedRate=1.000000)
'@
$failures = @()
$script:proc = $null

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

function Start-Sidecar {
    $log = Join-Path $temp 'headless.log'; $err = Join-Path $temp 'headless.err.log'
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    # 2s evaluation tick, 1-minute reminder interval: a reminder should arrive well inside a 2-minute wait.
    $script:proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err -Environment @{ MYSTTIQ_ALERT_EVAL_SECONDS = '2'; MYSTTIQ_ALERT_REMINDER_MINUTES = '1' }
    $base = "http://127.0.0.1:$Port"
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($script:proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { return } } catch {}
    }
    throw "Sidecar did not become healthy. $(Get-Content $err -Raw -ErrorAction SilentlyContinue)"
}
function Stop-Sidecar { if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 800 } }

function Get-Notifications([string]$base) {
    $snapshot = Invoke-RestMethod "$base/api/v1/notifications" -TimeoutSec 5
    foreach ($n in $snapshot.items) { $n }
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
    Start-Sidecar
    $base = "http://127.0.0.1:$Port"

    Test-RouteSmoke 'a condition that becomes true sends the original pinned alert, not a reminder' {
        Set-LowDiskPercent $base 99
        if (-not (Wait-Until { @(Get-Notifications $base | Where-Object { $_.title -eq 'Low disk space' }).Count -ge 1 } 40)) { throw 'no low-disk alert arrived' }
        $alert = @(Get-Notifications $base | Where-Object { $_.title -eq 'Low disk space' })[0]
        if ($alert.severity -ne 'Critical' -or -not $alert.pinned) { throw "expected a pinned Critical alert, got $($alert.severity) pinned=$($alert.pinned)" }
        if (@(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' }).Count -ne 0) { throw 'no reminder should exist yet, the interval has not elapsed' }
    }

    Test-RouteSmoke 'once the reminder interval elapses, a plain (unpinned) follow-up arrives without touching the original alert' {
        if (-not (Wait-Until { @(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' }).Count -ge 1 } 90)) {
            throw 'no reminder arrived within the 1-minute reminder interval'
        }
        $reminder = @(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' })[0]
        if ($reminder.severity -ne 'Critical') { throw "the reminder should carry the same severity as the condition, got $($reminder.severity)" }
        if ($reminder.pinned) { throw 'a reminder must not be pinned -- only the original alert is' }
        $alerts = @(Get-Notifications $base | Where-Object { $_.title -eq 'Low disk space' })
        if ($alerts.Count -ne 1 -or -not $alerts[0].pinned) { throw 'the original alert must still exist, unchanged and still pinned' }
    }

    Test-RouteSmoke 'a second reminder does not arrive before its own interval, but does after two intervals total' {
        Start-Sleep -Seconds 20
        $countAfter20s = @(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' }).Count
        if ($countAfter20s -ne 1) { throw "expected still exactly 1 reminder after only 20s more, found $countAfter20s" }
        if (-not (Wait-Until { @(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' }).Count -ge 2 } 90)) {
            throw 'no second reminder arrived within another minute'
        }
    }

    Test-RouteSmoke 'recovering stops the reminders: the Resolved notice arrives and no further reminder follows' {
        Set-LowDiskPercent $base 1
        if (-not (Wait-Until { @(Get-Notifications $base | Where-Object { $_.title -eq 'Resolved: Low disk space' }).Count -ge 1 } 40)) { throw 'no recovery notice arrived' }
        $reminderCountAtRecovery = @(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' }).Count
        Start-Sleep -Seconds 75
        $reminderCountAfterWait = @(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' }).Count
        if ($reminderCountAfterWait -ne $reminderCountAtRecovery) { throw "a reminder arrived after recovery: $reminderCountAtRecovery -> $reminderCountAfterWait" }
        $alerts = @(Get-Notifications $base | Where-Object { $_.title -eq 'Low disk space' })
        if ($alerts[0].pinned) { throw 'the original alert should have unpinned itself on recovery (v0.7.104.0 behaviour), and reminders must not have re-pinned it' }
    }
}
finally {
    Stop-Sidecar
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.107.0 alert reminder smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.107.0 alert reminder smoke gate passed." -ForegroundColor Green
