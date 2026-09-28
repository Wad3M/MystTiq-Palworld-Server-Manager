# MystTiq v0.9.1.0: file reviewed for this release (2026-09-28).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18426,
    [int]$RconPort = 18436,
    [int]$RestPort = 18446,
    [int]$HookPort = 18456
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] The v0.8.26.0 live-script rehearsal uses the Windows sidecar.' -ForegroundColor Yellow
    return
}

# v0.8.26.0: a rehearsal of the two live scripts that act on a real server, so they are known to work before anyone
# runs them for real. The PUBLISHED headless exe runs as a sidecar with its own folders and ports; the stand-ins in
# scripts\Testing\FakePalServerEndpoints.ps1 play PalServer's RCON (recording every command) and REST player list (one
# player, Alice, with a map position), and a small local listener plays a webhook. Then:
#   - Test-v0.8.26.0-InGame.ps1 must pass, and RCON must have received exactly the give, getpos and tp it claims, with
#     the teleport settings back as they were;
#   - Test-v0.8.26.0-Alerts.ps1 must pass with the webhook switched on, and the webhook must have received the test;
#     pointed at a closed port, it must fail and name the failed send.
# What stays unverified here: a real player seeing the item and moving, and real Discord/email delivery. That is what
# the live scripts themselves are for.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$fake = Join-Path $root 'scripts\Testing\FakePalServerEndpoints.ps1'
$inGame = Join-Path $root 'scripts\Test-v0.8.26.0-InGame.ps1'
$alerts = Join-Path $root 'scripts\Test-v0.8.26.0-Alerts.ps1'
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.26.0-live-scripts-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$rconLog = Join-Path $temp 'rcon-commands.txt'; $hookLog = Join-Path $temp 'webhook.txt'
New-Item $runtime, $backupRoot -ItemType Directory -Force | Out-Null
# PalDefender is "installed" (its folder), so Give Item and Teleport go through RCON.
New-Item (Join-Path $serverRoot 'Pal\Binaries\Win64\PalDefender\Logs') -ItemType Directory -Force | Out-Null
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') @"
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Live Scripts Rehearsal",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=True,RESTAPIPort=$RestPort,RCONEnabled=True,RCONPort=$RconPort)
"@
$playersPath = Join-Path $temp 'players.json'
Set-Content $playersPath '{"players":[{"name":"Alice","userId":"steam_1","playerId":"P1","ip":"10.0.0.5","ping":20,"level":10,"location_x":-120450.5,"location_y":88210.25}]}'
# The webhook stand-in: answers 200 and appends each body it receives.
$hookScript = Join-Path $temp 'webhook.ps1'
Set-Content $hookScript @"
`$l = [System.Net.HttpListener]::new(); `$l.Prefixes.Add('http://127.0.0.1:$HookPort/'); `$l.Start()
while (`$true) { `$c = `$l.GetContext(); `$b = [IO.StreamReader]::new(`$c.Request.InputStream).ReadToEnd(); Add-Content -LiteralPath '$hookLog' -Value `$b; `$c.Response.StatusCode = 200; `$c.Response.Close() }
"@

$failures = @()
$procs = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Get-Commands { if (Test-Path $rconLog) { @(Get-Content $rconLog) } else { @() } }
function Invoke-Script([string]$Path, [string[]]$Arguments) {
    $output = & $pwsh -NoProfile -File $Path @Arguments 2>&1 | ForEach-Object { "$_" }
    [pscustomobject]@{ Exit = $LASTEXITCODE; Output = ($output -join "`n") }
}

try {
    $pwsh = (Get-Process -Id $PID).Path
    $procs += Start-Process $pwsh -ArgumentList @('-NoProfile', '-File', $fake, '-Mode', 'Rcon', '-Port', "$RconPort", '-LogPath', $rconLog) -PassThru -WindowStyle Hidden
    $procs += Start-Process $pwsh -ArgumentList @('-NoProfile', '-File', $fake, '-Mode', 'Rest', '-Port', "$RestPort", '-PlayersJsonPath', $playersPath) -PassThru -WindowStyle Hidden
    $procs += Start-Process $pwsh -ArgumentList @('-NoProfile', '-File', $hookScript) -PassThru -WindowStyle Hidden
    $sidecarArgs = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $procs += Start-Process -FilePath $exe -ArgumentList $sidecarArgs -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'headless.log') -RedirectStandardError (Join-Path $temp 'headless.err.log')
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        try { if (Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw 'Sidecar did not become healthy.' }
    $serverId = @(Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/servers" -TimeoutSec 15)[0].id
    $api = "http://127.0.0.1:$Port/api/v1/servers/$serverId"
    $teleportBefore = (Invoke-RestMethod "$api/teleport" -TimeoutSec 15).config | ConvertTo-Json -Depth 6 -Compress

    Test-RouteSmoke 'the in-game script passes against the stand-in player and names every step in its report' {
        $report = Join-Path $temp 'ingame.md'
        $r = Invoke-Script $inGame @('-ServerId', $serverId, '-BaseUrl', "http://127.0.0.1:$Port", '-WaitMinutes', '1', '-ReportPath', $report)
        if ($r.Exit -ne 0) { throw "exit $($r.Exit): $($r.Output)" }
        $text = Get-Content $report -Raw
        foreach ($expected in 'A player is online', 'live map position', 'Give Item: 1 x Wood', 'Teleport capture', 'Teleport the player to the point', 'Teleport settings restored exactly', 'Map calibration pair') {
            if ($text -notmatch [regex]::Escape($expected)) { throw "the report does not mention '$expected'" }
        }
    }

    Test-RouteSmoke 'RCON received exactly what the script claims: the give, the getpos and the tp to the captured position' {
        $c = Get-Commands
        if ($c -notcontains 'giveitems steam_1 Wood:1') { throw "no give. Commands: $($c -join ' | ')" }
        if ($c -notcontains 'getpos steam_1') { throw "no getpos. Commands: $($c -join ' | ')" }
        if ($c -notcontains 'tp steam_1 -358.2 270.5 1200') { throw "no tp to the captured position. Commands: $($c -join ' | ')" }
    }

    Test-RouteSmoke 'the teleport settings are exactly as they were before the script' {
        $after = (Invoke-RestMethod "$api/teleport" -TimeoutSec 15).config | ConvertTo-Json -Depth 6 -Compress
        if ($after -ne $teleportBefore) { throw "before $teleportBefore, after $after" }
    }

    Test-RouteSmoke 'the alerts script sends a test through a switched-on webhook, and the webhook receives it' {
        $channels = @{ channels = @(@{ channel = 'Desktop'; enabled = $true; targetUrl = $null }, @{ channel = 'Webhook'; enabled = $true; targetUrl = "http://127.0.0.1:$HookPort/hook" }) }
        Invoke-RestMethod "$api/notifications/channels" -Method Put -ContentType 'application/json' -Body ($channels | ConvertTo-Json -Depth 6) -TimeoutSec 15 | Out-Null
        $r = Invoke-Script $alerts @('-BaseUrl', "http://127.0.0.1:$Port", '-SettleSeconds', '5', '-ReportPath', (Join-Path $temp 'alerts.md'))
        if ($r.Exit -ne 0) { throw "exit $($r.Exit): $($r.Output)" }
        if (-not (Test-Path $hookLog) -or (Get-Content $hookLog -Raw) -notmatch 'Test notification') { throw 'the webhook did not receive the test notification' }
    }

    Test-RouteSmoke 'pointed at a closed port, the alerts script fails and names the failed send' {
        $closed = @{ channels = @(@{ channel = 'Desktop'; enabled = $true; targetUrl = $null }, @{ channel = 'Webhook'; enabled = $true; targetUrl = 'http://127.0.0.1:9/hook' }) }
        Invoke-RestMethod "$api/notifications/channels" -Method Put -ContentType 'application/json' -Body ($closed | ConvertTo-Json -Depth 6) -TimeoutSec 15 | Out-Null
        $r = Invoke-Script $alerts @('-BaseUrl', "http://127.0.0.1:$Port", '-SettleSeconds', '8', '-ReportPath', (Join-Path $temp 'alerts-closed.md'))
        if ($r.Exit -eq 0) { throw "it passed: $($r.Output)" }
        if ((Get-Content (Join-Path $temp 'alerts-closed.md') -Raw) -notmatch 'Webhook dispatch failed') { throw 'the report does not name the failed webhook send' }
    }
}
finally {
    foreach ($p in $procs) { if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.26.0 live-script rehearsal failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host 'MystTiq v0.8.26.0 live-script rehearsal passed.' -ForegroundColor Green
