[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18301
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.101.0: end-to-end coverage of crash alerts through the REAL crash-recovery loop. An isolated
# sidecar is started over a fixture whose persisted lifecycle state says the server was running
# (process id that does not exist, no stop requested), which the real lifecycle service reports as a
# crash. There is no PalServer.exe, so the automatic restart genuinely fails. The loop must announce
# the crash (with the log analysis) and then the failed restart through the notifications route.
# Runs against the PUBLISHED headless exe, so publish first. Default timings are 5 s poll and 10 s
# backoff, so the two alerts arrive within about 20 seconds.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.101.0-crashalerts-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$palLogs = Join-Path $serverRoot 'Pal\Saved\Logs'
New-Item $palLogs -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $palLogs 'PalServer.log') @('Server initialized successfully', '[2026.09.21-08.10.00:000] Out of memory while allocating 4096 bytes')
# Phase 3 = Running, a process id that cannot exist, and no stop requested: the real lifecycle
# service reads that as "the server was running and is gone", i.e. a crash.
$state = @{ Phase = 3; LastKnownProcessId = 2147480000; LastTransitionAt = (Get-Date).AddMinutes(-1).ToString('o'); StopRequested = $false; Detail = 'PalServer was running.' } | ConvertTo-Json
Set-Content (Join-Path $runtime 'lifecycle-state.json') $state
$log = Join-Path $temp 'headless.log'
$err = Join-Path $temp 'headless.err.log'
$proc = $null
$failures = @()

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green
    }
    catch {
        Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red
        $script:failures += $Name
    }
}

function Wait-Notification([string]$base, [scriptblock]$Match, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) {
        try {
            $snapshot = Invoke-RestMethod "$base/api/v1/notifications" -TimeoutSec 5
            $hit = @($snapshot.items | Where-Object $Match)
            if ($hit.Count -gt 0) { return $hit[0] }
        } catch {}
        Start-Sleep -Milliseconds 700
    }
    return $null
}

try {
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err
    $base = "http://127.0.0.1:$Port"
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Sidecar did not become healthy. stdout=$(Get-Content $log -Raw -ErrorAction SilentlyContinue) stderr=$(Get-Content $err -Raw -ErrorAction SilentlyContinue)" }

    $script:crash = $null
    Test-RouteSmoke 'a crash the real recovery loop detects produces a critical notification that carries the log analysis' {
        $script:crash = Wait-Notification $base { $_.title -match 'server crashed, restarting \(attempt 1 of 5\)' } 40
        if (-not $script:crash) { throw "no crash notification arrived. Notifications: $((Invoke-RestMethod "$base/api/v1/notifications").items | ForEach-Object { $_.title })" }
        if ($script:crash.severity -ne 'Critical') { throw "expected Critical, got $($script:crash.severity)" }
        if ($script:crash.message -notmatch 'restarting it in 10 second') { throw "message should say when the restart happens: $($script:crash.message)" }
        if ($script:crash.message -notmatch 'Out of memory' -or $script:crash.message -notmatch 'First thing to try') { throw "message should carry the likely cause and first fix: $($script:crash.message)" }
    }

    Test-RouteSmoke 'the failed automatic restart is announced too, and says whether more attempts remain' {
        $failed = Wait-Notification $base { $_.title -match 'restart attempt 1 of 5 failed' } 40
        if (-not $failed) { throw 'no failed-restart notification arrived' }
        if ($failed.severity -ne 'Warning') { throw "expected Warning, got $($failed.severity)" }
        if ($failed.message -notmatch '4 attempt\(s\) left') { throw "message should say how many attempts remain: $($failed.message)" }
    }

    Test-RouteSmoke 'the crash analysis behind the alert was recorded, so the Crash Analyzer shows the same finding' {
        $history = Invoke-RestMethod "$base/api/v1/crash-analyzer/history" -TimeoutSec 10
        $h = @($history)
        if ($h.Count -lt 1) { throw 'expected at least one recorded crash analysis' }
        if (@($h[0].findings | Where-Object { $_.signatureId -eq 'out-of-memory' }).Count -ne 1) { throw 'the recorded analysis should contain the out-of-memory finding' }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.101.0 crash alerts smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.101.0 crash alerts smoke gate passed." -ForegroundColor Green
