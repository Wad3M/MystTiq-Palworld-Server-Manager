[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18313,
    [int]$RconPort = 18323,
    [int]$RestPort = 18333
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.113.0: Teleport Points end to end through the PUBLISHED headless exe. There is no real PalServer
# here, so scripts\Testing\FakePalServerEndpoints.ps1 stands in for its RCON (recording every command
# MystTiq sends) and its REST player list (one online player, Alice / steam_1). A PalDefender-format chat
# line is written into a fresh PalDefender session log, exactly as PalDefender would, and the check is
# what reaches RCON: the real chat watcher, parser, identity check and command builder, not a fake. What
# stays unverified: that real PalDefender writes chat in this exact form and moves a real player.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$fake = Join-Path $root 'scripts\Testing\FakePalServerEndpoints.ps1'
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.113.0-teleport-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
$rconLog = Join-Path $temp 'rcon-commands.txt'
New-Item $runtime, $backupRoot -ItemType Directory -Force | Out-Null
$logs = Join-Path $serverRoot 'Pal\Binaries\Win64\PalDefender\Logs'
New-Item $logs -ItemType Directory -Force | Out-Null
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') @"
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Teleport Smoke",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=True,RESTAPIPort=$RestPort,RCONEnabled=True,RCONPort=$RconPort)
"@
$playersPath = Join-Path $temp 'players.json'
Set-Content $playersPath '{"players":[{"name":"Alice","userId":"steam_1","playerId":"P1","ip":"10.0.0.5","ping":20,"level":10,"location_x":0,"location_y":0}]}'
$failures = @()
$procs = @()

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Get-Commands { if (Test-Path $rconLog) { foreach ($l in (Get-Content $rconLog)) { $l } } }
function Get-TpCommands { foreach ($c in @(Get-Commands)) { if ($c -like 'tp *') { $c } } }
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { if (& $Condition) { return $true }; Start-Sleep -Milliseconds 500 }
    return $false
}
function Invoke-Api([string]$Method, [string]$Path, $Body) {
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 6 } else { $null }
    $r = Invoke-WebRequest "http://127.0.0.1:$Port/api/v1$Path" -Method $Method -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 15
    @{ Status = [int]$r.StatusCode; Body = ($r.Content | ConvertFrom-Json) }
}
function Chat([string]$name, [string]$uid, [string]$text) { "[$((Get-Date).ToString('HH:mm:ss'))][info] [Chat::Global]['$name' (UserId=$uid, IP=10.0.0.5)]: $text" }

try {
    $pwsh = (Get-Process -Id $PID).Path
    $procs += Start-Process $pwsh -ArgumentList @('-NoProfile', '-File', $fake, '-Mode', 'Rcon', '-Port', "$RconPort", '-LogPath', $rconLog) -PassThru -WindowStyle Hidden
    $procs += Start-Process $pwsh -ArgumentList @('-NoProfile', '-File', $fake, '-Mode', 'Rest', '-Port', "$RestPort", '-PlayersJsonPath', $playersPath) -PassThru -WindowStyle Hidden
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $procs += Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'headless.log') -RedirectStandardError (Join-Path $temp 'headless.err.log')
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        try { if (Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw 'Sidecar did not become healthy.' }

    Test-RouteSmoke 'teleporting is off by default and PalDefender plus RCON are reported ready' {
        $r = Invoke-Api GET '/teleport' $null
        if ($r.Body.config.enabled) { throw 'teleporting must be off by default' }
        if (-not $r.Body.provider.canDeliver) { throw "provider not ready: $($r.Body.provider.detail)" }
    }

    Test-RouteSmoke 'invalid settings are refused: a / prefix, and switching on with no points' {
        $a = Invoke-Api PUT '/teleport' @{ enabled = $false; commandPrefix = '/tp'; cooldownSeconds = 60; points = @() }
        $b = Invoke-Api PUT '/teleport' @{ enabled = $true; commandPrefix = '!tp'; cooldownSeconds = 60; points = @() }
        if ($a.Status -ne 400 -or $b.Status -ne 400) { throw "expected 400/400, got $($a.Status)/$($b.Status)" }
    }

    Test-RouteSmoke 'saving points and switching on works' {
        $r = Invoke-Api PUT '/teleport' @{ enabled = $true; commandPrefix = '!tp'; cooldownSeconds = 60; points = @(@{ name = 'spawn'; x = -358.5; y = 270; z = $null }, @{ name = 'base'; x = 1; y = 2; z = 3 }) }
        if ($r.Status -ne 200 -or -not $r.Body.success) { throw "save failed: $($r.Status) $($r.Body.message)" }
    }

    Test-RouteSmoke 'a chat command in a fresh PalDefender log teleports the online player and replies, while an impostor line sends nothing' {
        $log = Join-Path $logs ((Get-Date).ToString('dd.MM HH.mm.ss') + '.log')
        # Impostor first: Alice's UserId under another name. Then the real Alice.
        Set-Content $log @((Chat 'Mallory' 'steam_1' '!tp base'), (Chat 'Alice' 'steam_1' '!tp spawn'))
        if (-not (Wait-Until { @(Get-Commands) -contains 'send msg steam_1 Teleported to spawn.' } 20)) { throw "no teleport reached RCON. Commands: $(@(Get-Commands) -join ' | ')" }
        $tp = @(Get-TpCommands)
        if ($tp.Count -ne 1 -or $tp[0] -ne 'tp steam_1 -358.5 270') { throw "expected exactly 'tp steam_1 -358.5 270', got: $($tp -join ' | ')" }
    }

    Test-RouteSmoke 'the cooldown stops a second teleport and the player is told to wait' {
        Add-Content (Get-ChildItem $logs -Filter *.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName (Chat 'Alice' 'steam_1' '!tp base')
        if (-not (Wait-Until { @(Get-Commands | Where-Object { $_ -like 'send msg steam_1 Please wait*' }).Count -ge 1 } 15)) { throw 'no cooldown reply' }
        if (@(Get-TpCommands).Count -ne 1) { throw 'a second teleport was sent during the cooldown' }
    }

    Test-RouteSmoke 'recent uses show the teleport, the ignored impostor and the cooldown' {
        $uses = @((Invoke-Api GET '/teleport' $null).Body.recentUses)
        if (-not ($uses | Where-Object { $_.playerName -eq 'Alice' -and $_.success })) { throw 'no successful use by Alice' }
        if (-not ($uses | Where-Object { $_.playerName -eq 'Mallory' -and -not $_.success })) { throw 'the impostor was not recorded as ignored' }
        if (-not ($uses | Where-Object { $_.detail -like 'Cooldown*' })) { throw 'the cooldown was not recorded' }
    }

    Test-RouteSmoke 'Capture asks PalDefender getpos for the online player and returns the position' {
        $r = Invoke-Api POST '/teleport/capture' @{ playerId = 'P1' }
        if ($r.Status -ne 200 -or $r.Body.x -ne -358.2 -or $r.Body.y -ne 270.5 -or $r.Body.z -ne 1200) { throw "capture failed: $($r.Status) $($r.Body.message)" }
        if (@(Get-Commands) -notcontains 'getpos steam_1') { throw 'getpos was not sent' }
    }

    Test-RouteSmoke 'Send puts the online player on a saved point; an offline player and an unknown point are refused' {
        $ok = Invoke-Api POST '/teleport/points/base/send' @{ playerId = 'P1' }
        if ($ok.Status -ne 200 -or @(Get-TpCommands) -notcontains 'tp steam_1 1 2 3') { throw "send failed: $($ok.Status) $($ok.Body.message)" }
        $off = Invoke-Api POST '/teleport/points/base/send' @{ playerId = 'P9' }
        $unknown = Invoke-Api POST '/teleport/points/moon/send' @{ playerId = 'P1' }
        if ($off.Status -ne 409 -or $unknown.Status -ne 409) { throw "expected 409/409, got $($off.Status)/$($unknown.Status)" }
    }

    Test-RouteSmoke 'switched off, a chat command is ignored' {
        $null = Invoke-Api PUT '/teleport' @{ enabled = $false; commandPrefix = '!tp'; cooldownSeconds = 0; points = @(@{ name = 'spawn'; x = -358.5; y = 270; z = $null }) }
        $before = @(Get-TpCommands).Count
        Add-Content (Get-ChildItem $logs -Filter *.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName (Chat 'Alice' 'steam_1' '!tp spawn')
        Start-Sleep -Seconds 6
        if (@(Get-TpCommands).Count -ne $before) { throw 'a teleport was sent while switched off' }
    }
}
finally {
    foreach ($p in $procs) { if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.113.0 teleport points smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.113.0 teleport points smoke gate passed." -ForegroundColor Green
