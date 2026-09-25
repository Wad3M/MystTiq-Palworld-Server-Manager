[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18384,
    [int]$GamePort = 18484
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.8.4.0: pausing outside delivery (Discord, email, webhooks) on the PUBLISHED exe, with a real webhook receiver
# (an HttpListener in this script) as the outside channel:
#   1. with delivery on, "Send test notification" reaches the webhook;
#   2. paused: the test notification is kept on the Notifications page but the webhook receives nothing;
#   3. the pause survives a sidecar restart and re-saving the channel list;
#   4. resumed: the webhook receives again, and nothing from the pause is sent late;
#   5. a very long pause is capped at 30 days, set on the host's clock.
# Own FleetRoot and ports; the webhook is on localhost only.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.4.0-delivery-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $steamCmd = Join-Path $temp 'missing-steamcmd.exe'
New-Item $runtime, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Delivery Smoke`",AdminPassword=`"a-long-random-admin-secret`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False)"

$probe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0); $probe.Start(); $hookPort = $probe.LocalEndpoint.Port; $probe.Stop()
$hookUrl = "http://localhost:$hookPort/hook/"
$listener = [System.Net.HttpListener]::new(); $listener.Prefixes.Add($hookUrl); $listener.Start()
$script:pending = $null
# The next webhook POST within $Seconds, answered 200, or $null if none arrived.
function Receive-Hook([int]$Seconds) {
    if (-not $script:pending) { $script:pending = $listener.GetContextAsync() }
    if (-not $script:pending.Wait($Seconds * 1000)) { return $null }
    $ctx = $script:pending.Result; $script:pending = $null
    $body = [IO.StreamReader]::new($ctx.Request.InputStream).ReadToEnd()
    $ctx.Response.StatusCode = 200; $ctx.Response.Close()
    return ($body | ConvertFrom-Json)
}

$failures = @()
$script:proc = $null
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Start-Sidecar {
    $runArgs = @('api-run', '--desktop-sidecar', '--config', $config, '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $script:proc = Start-Process -FilePath $exe -ArgumentList $runArgs -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp "headless-$([guid]::NewGuid().ToString('N').Substring(0,6)).log") -RedirectStandardError (Join-Path $temp 'headless.err.log')
    for ($i = 0; $i -lt 60; $i++) { Start-Sleep -Milliseconds 250; try { if (Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2) { return } } catch {} }
    throw "Sidecar did not become healthy. $(Get-Content (Join-Path $temp 'headless.err.log') -Raw -ErrorAction SilentlyContinue)"
}
function Stop-Sidecar { if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force -ErrorAction SilentlyContinue; $script:proc.WaitForExit(5000) | Out-Null } }
function Api([string]$Method, [string]$Path, $Body = $null) {
    $p = @{ Method = $Method; Uri = "http://127.0.0.1:$Port/api/v1$Path"; TimeoutSec = 15 }
    if ($null -ne $Body) { $p.Body = ($Body | ConvertTo-Json -Depth 6); $p.ContentType = 'application/json' }
    Invoke-RestMethod @p
}
function Save-Channels { Api PUT '/notifications/channels' @{ channels = @(@{ channel = 'Desktop'; enabled = $true; targetUrl = $null }, @{ channel = 'Webhook'; enabled = $true; targetUrl = $hookUrl }) } | Out-Null }

try {
    & $exe config-write-default --config $config --overwrite | Out-Null
    $cfg = Get-Content $config -Raw | ConvertFrom-Json
    $cfg.FleetRoot = $fleetRoot
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $config
    Start-Sidecar

    Test-RouteSmoke 'with delivery on, a test notification reaches the webhook' {
        $state = Api GET '/notifications/delivery'
        if ($state.pausedUntilUtc) { throw 'paused by default' }
        Save-Channels
        $r = Api POST '/notifications/test'
        if ($r.message -notmatch 'sent to Webhook') { throw "message: $($r.message)" }
        $hook = Receive-Hook 10
        if (-not $hook -or $hook.title -ne 'Test notification') { throw 'the webhook did not receive the test notification' }
    }

    Test-RouteSmoke 'paused: the notification stays on the Notifications page and the webhook receives nothing' {
        $state = Api POST '/notifications/delivery/pause' @{ minutes = 60 }
        $until = [DateTimeOffset]$state.pausedUntilUtc
        if ([Math]::Abs(($until - [DateTimeOffset]::UtcNow.AddMinutes(60)).TotalMinutes) -gt 2) { throw "pausedUntil $until is not about an hour away" }
        $r = Api POST '/notifications/test'
        if ($r.message -notmatch 'Notifications page only') { throw "message: $($r.message)" }
        if (Receive-Hook 3) { throw 'the webhook received a notification while delivery was paused' }
        $count = @((Api GET '/notifications').items | Where-Object { $_.title -eq 'Test notification' }).Count
        if ($count -ne 2) { throw "expected both test notifications on the Notifications page, found $count" }
    }

    Test-RouteSmoke 'the pause survives a sidecar restart and re-saving the channel list' {
        Stop-Sidecar
        Start-Sidecar
        Save-Channels
        $state = Api GET '/notifications/delivery'
        if (-not $state.pausedUntilUtc -or $state.detail -notmatch 'paused until') { throw "not paused after restart and save: $($state.detail)" }
        Api POST '/notifications/test' | Out-Null
        if (Receive-Hook 3) { throw 'the webhook received a notification after the restart, while paused' }
    }

    Test-RouteSmoke 'resumed: the webhook receives again, and nothing from the pause arrives late' {
        $state = Api POST '/notifications/delivery/pause' @{ minutes = 0 }
        if ($state.pausedUntilUtc) { throw 'still paused after resuming' }
        Api POST '/notifications/test' | Out-Null
        if (-not (Receive-Hook 10)) { throw 'the webhook did not receive after resuming' }
        if (Receive-Hook 2) { throw 'a notification from the pause was sent late' }
    }

    Test-RouteSmoke 'a very long pause is capped at 30 days' {
        $state = Api POST '/notifications/delivery/pause' @{ minutes = 10000000 }
        $days = (([DateTimeOffset]$state.pausedUntilUtc) - [DateTimeOffset]::UtcNow).TotalDays
        if ($days -gt 30.01 -or $days -lt 29.9) { throw "expected 30 days, got $days" }
        Api POST '/notifications/delivery/pause' @{ minutes = 0 } | Out-Null
    }
}
finally {
    Stop-Sidecar
    $listener.Stop(); $listener.Close()
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.4.0 delivery pause smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.4.0 delivery pause smoke gate passed." -ForegroundColor Green
