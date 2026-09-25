[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18312
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.112.0: the Players page's Give Item route, POST /api/v1/players/{playerId}/give, through the
# PUBLISHED headless exe. No real player can be online here, so this proves every honest refusal on the
# real route (bad input, no PalDefender, PalDefender without RCON, player not online) and that nothing is
# claimed to have been given. Actual delivery to a live player is covered by the logic harness's fake
# provider only and stays unverified against a real server until a player is online.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.112.0-giveitem-" + [guid]::NewGuid().ToString('N'))
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
$ini = Join-Path $configDir 'PalWorldSettings.ini'
function Write-Ini([string]$rcon) {
    Set-Content $ini @"
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Give Item Smoke",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=$rcon,RCONPort=25575)
"@
}
Write-Ini 'False'
$failures = @()
$proc = $null

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
# Returns @{ Status; Body } without throwing on 4xx, so the refusal itself can be checked.
function Invoke-Give([string]$base, [string]$playerId, $entries) {
    $body = @{ entries = @($entries) } | ConvertTo-Json -Depth 5
    $r = Invoke-WebRequest "$base/api/v1/players/$playerId/give" -Method Post -ContentType 'application/json' -Body $body -SkipHttpErrorCheck -TimeoutSec 15
    @{ Status = [int]$r.StatusCode; Body = ($r.Content | ConvertFrom-Json) }
}

try {
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'headless.log') -RedirectStandardError (Join-Path $temp 'headless.err.log')
    $base = "http://127.0.0.1:$Port"
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw 'Sidecar did not become healthy.' }

    Test-RouteSmoke 'an id that could smuggle another RCON command is refused before anything else is checked' {
        $r = Invoke-Give $base 'steam_76561198000000001' @(@{ type = 'Item'; id = 'PalSphere; Shutdown 1'; amount = 1 })
        if ($r.Status -ne 409 -or $r.Body.success -or $r.Body.message -notmatch 'not a valid id') { throw "expected 409 'not a valid id', got $($r.Status): $($r.Body.message)" }
        if (@($r.Body.commands).Count -ne 0) { throw 'a command was reported as sent' }
    }

    Test-RouteSmoke 'an empty give is refused' {
        $r = Invoke-Give $base 'steam_76561198000000001' @()
        if ($r.Status -ne 409 -or $r.Body.success) { throw "expected 409, got $($r.Status)" }
    }

    Test-RouteSmoke 'without PalDefender installed it says so plainly' {
        $r = Invoke-Give $base 'steam_76561198000000001' @(@{ type = 'Item'; id = 'PalSphere'; amount = 10 })
        if ($r.Status -ne 409 -or $r.Body.message -notmatch 'PalDefender was not found') { throw "expected the PalDefender-missing message, got $($r.Status): $($r.Body.message)" }
    }

    Test-RouteSmoke 'with PalDefender but RCON switched off it says RCON is what is missing' {
        New-Item (Join-Path $serverRoot 'Pal\Binaries\Win64\PalDefender') -ItemType Directory -Force | Out-Null
        $r = Invoke-Give $base 'steam_76561198000000001' @(@{ type = 'Item'; id = 'PalSphere'; amount = 10 })
        if ($r.Status -ne 409 -or $r.Body.message -notmatch 'RCON is not enabled') { throw "expected the RCON message, got $($r.Status): $($r.Body.message)" }
    }

    Test-RouteSmoke 'with PalDefender and RCON set up, a player who is not online is refused and nothing is sent' {
        Write-Ini 'True'
        $r = Invoke-Give $base 'steam_76561198000000001' @(@{ type = 'Pal'; id = 'WeaselDragon'; amount = 5 })
        if ($r.Status -ne 409 -or $r.Body.message -notmatch 'not online') { throw "expected the not-online refusal, got $($r.Status): $($r.Body.message)" }
        if (@($r.Body.commands).Count -ne 0) { throw 'a command was reported as sent to an offline player' }
    }

    Test-RouteSmoke 'every refusal is written to the Activity log' {
        $lines = @((Invoke-RestMethod "$base/api/v1/activity/tail?lines=200").lines | Where-Object { $_ -match 'Give Item failed' })
        if ($lines.Count -lt 5) { throw "expected 5 'Give Item failed' activity lines, got $($lines.Count)" }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.112.0 give item smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.112.0 give item smoke gate passed." -ForegroundColor Green
