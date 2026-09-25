[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18300
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.100.0: live-HTTP coverage for the player last-known positions the Map page draws for offline
# players. Isolated sidecar over a fixture world whose decoded Level.sav.json holds two player
# characters with a real last location, one with a placeholder location, and a pal. It runs against
# the PUBLISHED headless exe, so publish first.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.100.0-playerlocations-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$world = Join-Path $serverRoot 'Pal\Saved\SaveGames\0\PlayerLocationSmokeWorld'
New-Item (Join-Path $world 'Players') -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $world 'Level.sav') 'player-location-smoke-save'
Set-Content (Join-Path $world 'Players\67D8D355000000000000000000000000.sav') 'wade'
Set-Content (Join-Path $world 'Players\6F7EEEE6000000000000000000000000.sav') 'spiral'
Set-Content (Join-Path $world 'Players\1467C601000000000000000000000000.sav') 'placeholder'
Set-Content (Join-Path $world 'Level.sav.json') @'
{ "properties": { "worldSaveData": { "value": { "CharacterSaveParameterMap": { "value": [
  { "key": { "PlayerUId": { "value": "67d8d355-0000-0000-0000-000000000000" }, "InstanceId": { "value": "aaaa" } },
    "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
      "IsPlayer": { "value": true }, "NickName": { "value": "Wade" },
      "LastJumpedLocation": { "value": { "x": -350789.1, "y": 270390.7, "z": 7160.9 } } } } } } } } },
  { "key": { "PlayerUId": { "value": "6f7eeee6-0000-0000-0000-000000000000" }, "InstanceId": { "value": "bbbb" } },
    "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
      "IsPlayer": { "value": true }, "NickName": { "value": "Spiral" },
      "LastJumpedLocation": { "value": { "x": -352764.5, "y": 269927.3, "z": 7150.0 } } } } } } } } },
  { "key": { "PlayerUId": { "value": "1467c601-0000-0000-0000-000000000000" }, "InstanceId": { "value": "cccc" } },
    "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
      "IsPlayer": { "value": true }, "NickName": { "value": "M3llyM" },
      "LastJumpedLocation": { "value": { "x": 0.0, "y": 0.0, "z": 7062.1 } } } } } } } } },
  { "key": { "PlayerUId": { "value": "00000000-0000-0000-0000-000000000000" }, "InstanceId": { "value": "dddd" } },
    "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
      "LastJumpedLocation": { "value": { "x": -351336.2, "y": 271490.8, "z": 7000.0 } } } } } } } } }
] } } } } }
'@
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

    $script:snapshot = $null
    Test-RouteSmoke 'the explorer route returns the last known position of each player with a real location, and only those' {
        $script:snapshot = Invoke-RestMethod "$base/api/v1/world/players-guilds" -TimeoutSec 60
        $locations = @($script:snapshot.playerLocations)
        if ($locations.Count -ne 2) { throw "expected 2 player locations (Wade and Spiral), got $($locations.Count): $(($locations | ForEach-Object { $_.name }) -join ', ')" }
        $wade = @($locations | Where-Object { $_.name -eq 'Wade' })[0]
        if (-not $wade) { throw 'Wade is missing' }
        if ($wade.playerId -ne '67D8D355000000000000000000000000') { throw "the id must match the player list's format, got '$($wade.playerId)'" }
        if ([Math]::Abs([double]$wade.x - -350789.1) -gt 0.01 -or [Math]::Abs([double]$wade.y - 270390.7) -gt 0.01) { throw "unexpected coordinates: $($wade.x), $($wade.y)" }
    }

    Test-RouteSmoke 'a placeholder 0,0 location and a pal with no player id are left out' {
        $names = @($script:snapshot.playerLocations | ForEach-Object { $_.name })
        if ($names -contains 'M3llyM') { throw 'a 0,0 placeholder location must not be drawn' }
        if (@($script:snapshot.playerLocations | Where-Object { $_.playerId -match '^0+$' }).Count -ne 0) { throw 'a pal with a zero player id must not appear' }
    }

    Test-RouteSmoke 'every location id matches a player in the same snapshot, so the map can join them' {
        $ids = @($script:snapshot.players | ForEach-Object { $_.playerId })
        foreach ($l in $script:snapshot.playerLocations) {
            if ($ids -notcontains $l.playerId) { throw "location for $($l.name) ($($l.playerId)) has no matching player in the list: $($ids -join ', ')" }
        }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.100.0 player locations smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.100.0 player locations smoke gate passed." -ForegroundColor Green
