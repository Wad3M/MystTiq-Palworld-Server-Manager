[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18310
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.110.0: end-to-end coverage of the crash-recovery give-up/manual-recovery cycle through the REAL
# lifecycle service, not just the logic harness's ScriptedLifecycle. Same crash fixture v0.7.101.0's
# smoke uses (a persisted lifecycle state claiming the server was Running under a PID that does not
# exist, so the real service reads it as a crash; no PalServer.exe, so automatic recovery genuinely
# fails every attempt and gives up at the default 5). Then a REAL process named "PalServer.exe" is
# copied into the fixture's server root and launched (Windows' own session inspector matches by process
# name AND executable path under ServerRoot, so this is the only honest way to make the real service
# report "running again" without fabricating data) -- simulating an admin starting it manually after
# the automatic loop had given up. Default timings (5s poll, 10s backoff, 5 attempts), so give-up takes
# roughly a minute and a half; this smoke is patient rather than tuning the config to go faster, since
# the whole point is exercising the loop's own real cadence, unmodified.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.110.0-giveuprecover-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
New-Item $serverRoot -ItemType Directory -Force | Out-Null
# v0.7.115.0: "back up" now means READY (the game port is up), not just "a process exists" (deficiency
# report). The fixture gets its own game port, which this smoke binds itself after starting the fake
# PalServer.exe, standing in for PalServer finishing its startup.
$gamePort = 18410
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') @"
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Give-up Smoke",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=$gamePort,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=False,RCONPort=25575)
"@
$script:udp = $null
# Phase 3 = Running, a process id that cannot exist, and no stop requested: the real lifecycle
# service reads that as "the server was running and is gone", i.e. a crash.
$state = @{ Phase = 3; LastKnownProcessId = 2147480000; LastTransitionAt = (Get-Date).AddMinutes(-1).ToString('o'); StopRequested = $false; Detail = 'PalServer was running.' } | ConvertTo-Json
Set-Content (Join-Path $runtime 'lifecycle-state.json') $state
$log = Join-Path $temp 'headless.log'
$err = Join-Path $temp 'headless.err.log'
$proc = $null
$fakeServer = $null
$failures = @()

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
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
function Get-Notification([string]$base, [scriptblock]$Match) {
    $snapshot = Invoke-RestMethod "$base/api/v1/notifications" -TimeoutSec 5
    @($snapshot.items | Where-Object $Match)[0]
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
    if (-not $ready) { throw "Sidecar did not become healthy. stderr=$(Get-Content $err -Raw -ErrorAction SilentlyContinue)" }

    $script:downId = $null
    Test-RouteSmoke 'automatic recovery gives up after every real restart attempt fails, and pins a DOWN notice' {
        $down = Wait-Notification $base { $_.title -match 'server is DOWN, automatic recovery gave up' } 150
        if (-not $down) { throw "no give-up notification arrived. Notifications: $((Invoke-RestMethod "$base/api/v1/notifications").items | ForEach-Object { $_.title })" }
        if (-not $down.pinned -or $down.severity -ne 'Critical') { throw "expected a pinned Critical, got severity=$($down.severity) pinned=$($down.pinned)" }
        $script:downId = $down.id
    }

    Test-RouteSmoke 'starting a real process the session inspector recognises as PalServer is reported as the server coming back up, and unpins the DOWN notice' {
        $waitfor = Join-Path $env:SystemRoot 'System32\waitfor.exe'
        if (-not (Test-Path $waitfor -PathType Leaf)) { throw 'waitfor.exe is not available on this machine to stand in for a real PalServer.exe process' }
        $fakeExe = Join-Path $serverRoot 'PalServer.exe'
        Copy-Item -LiteralPath $waitfor -Destination $fakeExe -Force
        # waitfor <signal> (no /T) blocks for its default ~49-day timeout waiting for a signal that will
        # never arrive -- a real, long-running process under the fixture's ServerRoot, named "PalServer"
        # once launched (Windows reports a process's name from the running image file, not any internal
        # metadata).
        $script:fakeServer = Start-Process -FilePath $fakeExe -ArgumentList @('mysttiqsmokesignal') -PassThru -WindowStyle Hidden
        # v0.7.115.0: PalServer is ready once its game port is up; bind the fixture's port to say so.
        $script:udp = [System.Net.Sockets.UdpClient]::new($gamePort)

        $backUp = Wait-Notification $base { $_.title -match 'server is back up' } 40
        if (-not $backUp) { throw 'no "server is back up" notification arrived after the real process appeared' }
        if ($backUp.pinned -or $backUp.severity -ne 'Success') { throw "expected an unpinned Success notice, got severity=$($backUp.severity) pinned=$($backUp.pinned)" }

        if (-not $script:downId) { throw 'no DOWN notification id was captured by the previous check' }
        $downAfter = Get-Notification $base { $_.id -eq $script:downId }
        if (-not $downAfter) { throw 'the original DOWN notification is gone, not just unpinned' }
        if ($downAfter.pinned) { throw 'the DOWN notification should have unpinned itself' }
    }

    Test-RouteSmoke 'crash-detect-and-restart monitoring resumed: killing the (fake) process again is reported as a fresh crash' {
        if ($script:fakeServer -and -not $script:fakeServer.HasExited) { Stop-Process -Id $script:fakeServer.Id -Force -ErrorAction SilentlyContinue }
        if ($script:udp) { $script:udp.Dispose(); $script:udp = $null }
        $freshCrash = Wait-Notification $base { $_.title -match 'server crashed, restarting \(attempt 1 of' } 40
        if (-not $freshCrash) { throw 'no fresh crash notification arrived -- monitoring did not actually resume after the manual recovery' }
    }
}
finally {
    if ($script:udp) { $script:udp.Dispose() }
    if ($fakeServer -and -not $fakeServer.HasExited) { Stop-Process -Id $fakeServer.Id -Force -ErrorAction SilentlyContinue }
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.110.0 crash-recovery give-up/manual-recovery smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.110.0 crash-recovery give-up/manual-recovery smoke gate passed." -ForegroundColor Green
