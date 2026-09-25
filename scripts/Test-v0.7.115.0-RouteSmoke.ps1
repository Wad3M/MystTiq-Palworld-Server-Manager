[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18315,
    [int]$GamePort = 18415
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.115.0 (deficiency report): what MystTiq knows about crash recovery and operations must survive a
# restart of MystTiq itself, and "the server is back" must mean READY, not "a process exists". Proven on the
# PUBLISHED exe by restarting the real sidecar in the middle of a crash cycle:
#   1. an operation journal left "Running" by a MystTiq that died comes back as Failed/Interrupted;
#   2. recovery gives up (real lifecycle, crash fixture, no PalServer.exe) and pins a DOWN notice;
#   3. after a sidecar restart it does NOT treat the still-down server as a new crash;
#   4. a real "PalServer.exe" process (a renamed waitfor.exe, as in v0.7.110.0) that is not listening on its
#      game port is NOT reported as back up;
#   5. once the game port is bound (this script binds the fixture's UDP port), it is reported back up and the
#      DOWN notice pinned by the PREVIOUS sidecar process is unpinned.
# Uses its own FleetRoot (see the v0.7.114.0 lesson) so nothing reaches the machine-wide fleet store.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.115.0-persist-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
$fleetRoot = Join-Path $temp 'fleet'
New-Item $runtime, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') @"
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Persistence Smoke",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=$GamePort,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=False,RCONPort=25575)
"@
# Crash fixture: "Running" under a PID that cannot exist, and no PalServer.exe, so every restart fails.
$state = @{ Phase = 3; LastKnownProcessId = 2147480000; LastTransitionAt = (Get-Date).AddMinutes(-1).ToString('o'); StopRequested = $false; Detail = 'PalServer was running.' } | ConvertTo-Json
Set-Content (Join-Path $runtime 'lifecycle-state.json') $state
# An operation journal a previous MystTiq left mid-run.
$opId = [guid]::NewGuid().ToString('N')
$journalDir = Join-Path $fleetRoot 'operations\journals\default'
New-Item $journalDir -ItemType Directory -Force | Out-Null
$now = (Get-Date).ToUniversalTime().ToString('o')
Set-Content (Join-Path $journalDir "operation-$opId.json") (@{
    OperationId = $opId; ServerProfileId = 'default'; Kind = 'world-restore'; Source = 'smoke'; ResourceKeys = @('world')
    Phase = 'Running'; CreatedUtc = $now; UpdatedUtc = $now; SafetyBackup = $null; RolledBack = $false
    Stages = @(@{ State = 'Started'; Detail = 'started'; TimestampUtc = $now }); JournalPath = ''
} | ConvertTo-Json -Depth 5)

$failures = @()
$script:proc = $null
$fakeServer = $null
$udp = $null

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Start-Sidecar {
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $script:proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp "headless-$([guid]::NewGuid().ToString('N').Substring(0,6)).log") -RedirectStandardError (Join-Path $temp 'headless.err.log')
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 250
        if ($script:proc.HasExited) { break }
        try { if (Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2) { return } } catch {}
    }
    throw "Sidecar did not become healthy. $(Get-Content (Join-Path $temp 'headless.err.log') -Raw -ErrorAction SilentlyContinue)"
}
function Stop-Sidecar { if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force -ErrorAction SilentlyContinue; $script:proc.WaitForExit(5000) | Out-Null } }
function Get-Notifications { foreach ($n in (Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/notifications" -TimeoutSec 5).items) { $n } }
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { try { if (& $Condition) { return $true } } catch {}; Start-Sleep -Milliseconds 700 }
    return $false
}

try {
    & $exe config-write-default --config $config --overwrite | Out-Null
    $cfg = Get-Content $config -Raw | ConvertFrom-Json
    $cfg.FleetRoot = $fleetRoot
    $cfg.Lifecycle.ServicePollSeconds = 1
    $cfg.Lifecycle.RecoveryBackoffSeconds = 1
    $cfg.Lifecycle.MaximumRecoveryAttempts = 1
    $cfg.Lifecycle.StartupTimeoutSeconds = 10
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $config
    Start-Sidecar

    Test-RouteSmoke 'an operation left Running by a MystTiq that died comes back as Failed with an Interrupted stage' {
        $ops = @((Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/operations?max=50").ForEach({ $_ }))
        $op = @($ops | Where-Object { $_.operationId -eq $opId })
        if ($op.Count -ne 1) { throw "the journal was not reloaded; operations: $($ops.Count)" }
        if ($op[0].phase -ne 'Failed' -or @($op[0].stages)[-1].state -ne 'Interrupted') { throw "expected Failed/Interrupted, got $($op[0].phase) / $(@($op[0].stages)[-1].state)" }
    }

    $script:downId = $null
    Test-RouteSmoke 'recovery gives up, pins a DOWN notice, and saves that it gave up' {
        if (-not (Wait-Until { @(Get-Notifications | Where-Object { $_.title -match 'DOWN' }).Count -ge 1 } 60)) { throw 'no give-up' }
        $down = @(Get-Notifications | Where-Object { $_.title -match 'DOWN' })[0]
        if (-not $down.pinned) { throw 'the DOWN notice is not pinned' }
        $script:downId = $down.id
        $saved = Get-Content (Join-Path $runtime 'crash-recovery\state.json') -Raw | ConvertFrom-Json
        if (-not $saved.GaveUpAtUtc -or $saved.PinnedNotificationId -ne $down.id) { throw "state.json does not hold the give-up and pinned id: $($saved | ConvertTo-Json -Compress)" }
    }

    Test-RouteSmoke 'after a sidecar restart the still-down server is not treated as a new crash' {
        $crashesBefore = @(Get-Notifications | Where-Object { $_.title -match 'server crashed' }).Count
        Stop-Sidecar
        Start-Sidecar
        Start-Sleep -Seconds 8
        $crashesAfter = @(Get-Notifications | Where-Object { $_.title -match 'server crashed' }).Count
        if ($crashesAfter -ne $crashesBefore) { throw "a new crash was reported after the restart ($crashesBefore -> $crashesAfter)" }
        $down = @(Get-Notifications | Where-Object { $_.id -eq $script:downId })
        if ($down.Count -ne 1 -or -not $down[0].pinned) { throw 'the DOWN notice is gone or no longer pinned after the restart' }
    }

    Test-RouteSmoke 'a PalServer process that is not listening on its game port is not reported as back up' {
        $waitfor = Join-Path $env:SystemRoot 'System32\waitfor.exe'
        Copy-Item -LiteralPath $waitfor -Destination (Join-Path $serverRoot 'PalServer.exe') -Force
        $script:fakeServer = Start-Process -FilePath (Join-Path $serverRoot 'PalServer.exe') -ArgumentList @('mysttiqpersistsmoke') -PassThru -WindowStyle Hidden
        Start-Sleep -Seconds 8
        if (@(Get-Notifications | Where-Object { $_.title -match 'back up' }).Count -ne 0) { throw '"back up" was reported before the server was ready' }
    }

    Test-RouteSmoke 'once the game port is up it is reported back up, and the DOWN notice pinned by the previous sidecar process is unpinned' {
        $script:udp = [System.Net.Sockets.UdpClient]::new($GamePort)
        if (-not (Wait-Until { @(Get-Notifications | Where-Object { $_.title -match 'back up' }).Count -ge 1 } 30)) { throw 'no "back up" after the game port came up' }
        $down = @(Get-Notifications | Where-Object { $_.id -eq $script:downId })
        if ($down.Count -ne 1 -or $down[0].pinned) { throw 'the DOWN notice was not unpinned' }
        $saved = Get-Content (Join-Path $runtime 'crash-recovery\state.json') -Raw | ConvertFrom-Json
        if ($saved.GaveUpAtUtc) { throw 'the give-up was not cleared' }
    }
}
finally {
    if ($script:udp) { $script:udp.Dispose() }
    if ($script:fakeServer -and -not $script:fakeServer.HasExited) { Stop-Process -Id $script:fakeServer.Id -Force -ErrorAction SilentlyContinue }
    Stop-Sidecar
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.115.0 persistence and readiness smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.115.0 persistence and readiness smoke gate passed." -ForegroundColor Green
