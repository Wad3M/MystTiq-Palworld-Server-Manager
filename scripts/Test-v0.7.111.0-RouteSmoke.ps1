[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18311
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.111.0: per-profile alert mute and crash alert settings, end to end through the PUBLISHED headless
# exe. The profile starts already muted (a rules.json written before launch, the same file the Alert
# Center page saves) and with the same crash fixture v0.7.101.0/v0.7.110.0 use (a persisted "Running"
# state under a PID that cannot exist, no PalServer.exe), so the REAL crash-recovery loop runs and fails
# while muted. That proves the host actually wires this profile's rules into the crash observer, which
# the logic harness cannot. Then Unmute through the new route and both alert paths speak again.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.111.0-alertmute-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item (Join-Path $runtime 'alerts') -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
New-Item $serverRoot -ItemType Directory -Force | Out-Null
$state = @{ Phase = 3; LastKnownProcessId = 2147480000; LastTransitionAt = (Get-Date).AddMinutes(-1).ToString('o'); StopRequested = $false; Detail = 'PalServer was running.' } | ConvertTo-Json
Set-Content (Join-Path $runtime 'lifecycle-state.json') $state
$mutedUntil = (Get-Date).ToUniversalTime().AddHours(2).ToString('o')
Set-Content (Join-Path $runtime 'alerts\rules.json') "{ ""MutedUntilUtc"": ""$mutedUntil"" }"
$log = Join-Path $temp 'headless.log'
$err = Join-Path $temp 'headless.err.log'
$proc = $null
$failures = @()

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Get-Notifications([string]$base) {
    $snapshot = Invoke-RestMethod "$base/api/v1/notifications" -TimeoutSec 5
    foreach ($n in $snapshot.items) { $n }
}
function Get-ActivityLines([string]$base) {
    $tail = Invoke-RestMethod "$base/api/v1/activity/tail?lines=500" -TimeoutSec 5
    foreach ($l in $tail.lines) { $l }
}
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { try { if (& $Condition) { return $true } } catch {}; Start-Sleep -Milliseconds 700 }
    return $false
}
function Get-Rules([string]$base) { Invoke-RestMethod "$base/api/v1/alerts/rules" -TimeoutSec 5 }
function Set-Rules([string]$base, $rules) { Invoke-RestMethod "$base/api/v1/alerts/rules" -Method Put -ContentType 'application/json' -Body ($rules | ConvertTo-Json -Depth 6) -TimeoutSec 5 }
function Set-Mute([string]$base, [int]$minutes) { Invoke-RestMethod "$base/api/v1/alerts/mute" -Method Post -ContentType 'application/json' -Body (@{ minutes = $minutes } | ConvertTo-Json) -TimeoutSec 5 }

try {
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err -Environment @{ MYSTTIQ_ALERT_EVAL_SECONDS = '2' }
    $base = "http://127.0.0.1:$Port"
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Sidecar did not become healthy. stderr=$(Get-Content $err -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'a rules.json with only a mute loads muted, with crash alerts and back-up notices on by default' {
        $rules = Get-Rules $base
        if (-not $rules.mutedUntilUtc) { throw 'the pre-written mute was not loaded' }
        if (-not $rules.crashAlerts.enabled -or -not $rules.crashAlerts.recoveryNotices) { throw "expected crash alert defaults on, got $($rules.crashAlerts | ConvertTo-Json -Compress)" }
    }

    Test-RouteSmoke 'a real crash while muted sends no notification, but the Activity log records the alert that was not sent' {
        if (-not (Wait-Until { @(Get-ActivityLines $base | Where-Object { $_ -match 'Crash alert not sent' -and $_ -match 'server crashed' }).Count -ge 1 } 60)) {
            throw "no 'Crash alert not sent' activity line for the crash. Notifications: $(@(Get-Notifications $base | ForEach-Object { $_.title }) -join ' | ')"
        }
        $crashNotices = @(Get-Notifications $base | Where-Object { $_.title -match 'server crashed|restart attempt|DOWN' })
        if ($crashNotices.Count -ne 0) { throw "a crash notification was sent during the mute: $($crashNotices[0].title)" }
    }

    Test-RouteSmoke 'a threshold rule that is true while muted does not alert, and a Save Rules round trip keeps the mute' {
        $rules = Get-Rules $base
        $rules.lowDiskSpace.thresholdPercent = 99
        $saved = Set-Rules $base $rules
        if (-not $saved.mutedUntilUtc) { throw 'saving the rules dropped the mute' }
        Start-Sleep -Seconds 10
        $low = @(Get-Notifications $base | Where-Object { $_.title -eq 'Low disk space' })
        if ($low.Count -ne 0) { throw 'Low disk space alerted during the mute' }
    }

    Test-RouteSmoke 'Unmute through the new route: the still-true threshold alerts, and the still-failing crash recovery alerts again' {
        $after = Set-Mute $base 0
        if ($after.mutedUntilUtc) { throw "mute 0 should clear the end time, got $($after.mutedUntilUtc)" }
        if (-not (Wait-Until { @(Get-Notifications $base | Where-Object { $_.title -eq 'Low disk space' }).Count -ge 1 } 30)) { throw 'the still-true low-disk condition did not alert after Unmute' }
        if (-not (Wait-Until { @(Get-Notifications $base | Where-Object { $_.title -match 'restart attempt \d+ of \d+ failed|DOWN' }).Count -ge 1 } 120)) {
            throw 'the crash-recovery loop, still failing, sent nothing after Unmute'
        }
    }

    Test-RouteSmoke 'Mute 60 is timed on the host clock and capped values are accepted' {
        $before = (Get-Date).ToUniversalTime()
        $muted = Set-Mute $base 60
        $until = ([DateTimeOffset]$muted.mutedUntilUtc).UtcDateTime
        $minutes = ($until - $before).TotalMinutes
        if ($minutes -lt 59 -or $minutes -gt 61) { throw "expected about 60 minutes ahead, got $minutes" }
        $capped = Set-Mute $base 999999
        $cappedDays = (([DateTimeOffset]$capped.mutedUntilUtc).UtcDateTime - $before).TotalDays
        if ($cappedDays -gt 30.01) { throw "a huge mute should cap at 30 days, got $cappedDays days" }
        $null = Set-Mute $base 0
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.111.0 alert mute smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.111.0 alert mute smoke gate passed." -ForegroundColor Green
