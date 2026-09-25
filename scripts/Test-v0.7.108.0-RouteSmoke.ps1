[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18308
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

# v0.7.108.0: the reminder interval added in v0.7.107.0 was an env-var-only live-testing knob
# (MYSTTIQ_ALERT_REMINDER_MINUTES); it is now also a real, persisted AlertRuleSet.ReminderMinutes setting
# editable on the Alert Center page. Deliberately does NOT set that env var here -- the whole point is to
# prove the CONFIGURED value drives the reminder cadence on its own, not just the override path
# v0.7.107.0's own smoke already covers. Runs against the PUBLISHED headless exe.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.108.0-remindercfg-" + [guid]::NewGuid().ToString('N'))
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
OptionSettings=(ServerName="Reminder Config Smoke",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=False,RCONPort=25575,DayTimeSpeedRate=1.000000)
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
    # 2s evaluation tick, but MYSTTIQ_ALERT_REMINDER_MINUTES is deliberately NOT set.
    $script:proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err -Environment @{ MYSTTIQ_ALERT_EVAL_SECONDS = '2' }
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
function Get-Rules([string]$base) { Invoke-RestMethod "$base/api/v1/alerts/rules" -TimeoutSec 5 }
function Set-Rules([string]$base, $rules) { Invoke-RestMethod "$base/api/v1/alerts/rules" -Method Put -ContentType 'application/json' -Body ($rules | ConvertTo-Json -Depth 6) -TimeoutSec 5 }
function Set-LowDiskPercent([string]$base, [double]$percent) {
    $rules = Get-Rules $base
    $rules.lowDiskSpace.thresholdPercent = $percent
    $null = Set-Rules $base $rules
}

try {
    Start-Sidecar
    $base = "http://127.0.0.1:$Port"

    Test-RouteSmoke 'the default reminder interval is 1440 minutes (24 hours), and it survives a save that does not touch it' {
        $rules = Get-Rules $base
        if ($rules.reminderMinutes -ne 1440) { throw "expected the default 1440, got $($rules.reminderMinutes)" }
        $null = Set-Rules $base $rules
        if ((Get-Rules $base).reminderMinutes -ne 1440) { throw 're-saving the unchanged rules should not change the default' }
    }

    Test-RouteSmoke 'setting reminderMinutes to 1 through the API alone (no env var) makes a still-active condition remind within about a minute' {
        $rules = Get-Rules $base
        $rules.reminderMinutes = 1
        $null = Set-Rules $base $rules
        Set-LowDiskPercent $base 99
        if (-not (Wait-Until { @(Get-Notifications $base | Where-Object { $_.title -eq 'Low disk space' }).Count -ge 1 } 40)) { throw 'no alert arrived' }
        if (-not (Wait-Until { @(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' }).Count -ge 1 } 90)) {
            throw 'the configured 1-minute reminder interval did not produce a reminder -- the setting is not actually driving the cadence'
        }
    }

    Test-RouteSmoke 'setting reminderMinutes to 0 through the API disables reminders even though the condition is still active' {
        $rules = Get-Rules $base
        $rules.reminderMinutes = 0
        $null = Set-Rules $base $rules
        $countAtDisable = @(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' }).Count
        Start-Sleep -Seconds 75
        $countAfterWait = @(Get-Notifications $base | Where-Object { $_.title -eq 'Still active: Low disk space' }).Count
        if ($countAfterWait -ne $countAtDisable) { throw "a reminder arrived after reminders were disabled: $countAtDisable -> $countAfterWait" }
    }
}
finally {
    Stop-Sidecar
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.108.0 configured reminder smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.108.0 configured reminder smoke gate passed." -ForegroundColor Green
