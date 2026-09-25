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

# v0.8.11.0: Pal positions on the map, on the PUBLISHED exe, over a synthetic world save shaped like the real one: a
# guild with one base, the base's worker container, a party and a Palbox, and Pals in each (plus unset positions).
#   1. the explorer route returns the base worker and the party Pal, named by guild and owner, and nothing else;
#   2. it counts, but does not return, the Palbox Pal and the Pals without a real position.
# Own FleetRoot and port; no game server is involved.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.11.0-palpositions-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $steamCmd = Join-Path $temp 'missing-steamcmd.exe'
New-Item $runtime, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$world = Join-Path $serverRoot 'Pal\Saved\SaveGames\0\PalPositionSmokeWorld'
New-Item (Join-Path $world 'Players') -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $world 'Level.sav') 'pal-position-smoke-save'
Set-Content (Join-Path $world 'Players\A3835C7B000000000000000000000000.sav') 'keeper'

$worker = '86d4a7a0-4298-7ee5-1dbd-cdbff9f5f22d'; $party = '79a83df4-42e1-5120-c940-0fb782bfa00a'; $box = '744e8384-449e-0dd3-c5e3-7c98bd50b9bd'
$owner = 'a3835c7b-0000-0000-0000-000000000000'; $baseId = 'd04f779e-4ef8-578b-7c84-12b759e117e1'
function Pal([string]$Instance, [string]$Species, [int]$Level, [string]$Container, [string]$X, [string]$Y, [string]$Z, [string]$Owner = '00000000-0000-0000-0000-000000000000') {
    "{ `"key`": { `"PlayerUId`": { `"value`": `"00000000-0000-0000-0000-000000000000`" }, `"InstanceId`": { `"value`": `"$Instance`" } }, `"value`": { `"RawData`": { `"value`": { `"object`": { `"SaveParameter`": { `"value`": { " +
    "`"CharacterID`": { `"value`": `"$Species`" }, `"Level`": { `"value`": { `"type`": `"None`", `"value`": $Level } }, `"OwnerPlayerUId`": { `"value`": `"$Owner`" }, " +
    "`"SlotId`": { `"value`": { `"ContainerId`": { `"id`": null, `"value`": { `"ID`": { `"id`": null, `"value`": `"$Container`" } } } } }, " +
    "`"LastJumpedLocation`": { `"value`": { `"x`": $X, `"y`": $Y, `"z`": $Z } } } } } } } } }"
}
$pals = @(
    (Pal 'aaaa0001' 'PinkCat' 15 $worker '-355668.1' '272597.2' '7100'),
    (Pal 'aaaa0002' 'BOSS_WeaselDragon' 12 $party '-351444.0' '270714.0' '7050' $owner),
    (Pal 'aaaa0003' 'Garm' 20 $box '-250000.0' '200000.0' '6000' $owner),
    (Pal 'aaaa0004' 'Boar' 10 $worker '0' '0' '7004'),
    (Pal 'aaaa0005' 'Plesiosaur' 7 $party '-5.4' '-58.1' '7004.8' $owner)
) -join ",`n"
Set-Content (Join-Path $world 'Level.sav.json') @"
{ "properties": { "worldSaveData": { "value": {
  "GroupSaveDataMap": { "value": [ { "key": "11d02ac3-4634-e88b-6a92-62a0c74fcd53", "value": {
    "GroupType": { "value": { "value": "EPalGroupType::Guild" } },
    "RawData": { "value": { "group_type": "EPalGroupType::Guild", "group_id": "11d02ac3-4634-e88b-6a92-62a0c74fcd53", "guild_name": "Smoke Guild",
      "admin_player_uid": "$owner", "base_ids": [ "$baseId" ],
      "players": [ { "player_uid": "$owner", "player_info": { "player_name": "Keeper" } } ] } } } } ] },
  "BaseCampSaveData": { "value": [ { "key": "$baseId", "value": {
    "RawData": { "value": { "spawn_transform": { "translation": { "x": -351088.7, "y": 271805.2, "z": 7422.7 } } } },
    "WorkerDirector": { "value": { "RawData": { "value": { "container_id": "$worker" } } } } } } ] },
  "CharacterContainerSaveData": { "value": [
    { "key": { "ID": { "value": "$worker" } }, "value": { "SlotNum": { "value": 9 } } },
    { "key": { "ID": { "value": "$party" } }, "value": { "SlotNum": { "value": 5 } } },
    { "key": { "ID": { "value": "$box" } }, "value": { "SlotNum": { "value": 960 } } } ] },
  "CharacterSaveParameterMap": { "value": [
$pals
  ] } } } } }
"@

$failures = @()
$script:proc = $null
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { try { if (& $Condition) { return $true } } catch {}; Start-Sleep -Milliseconds 500 }
    return $false
}

try {
    & $exe config-write-default --config $config --overwrite | Out-Null
    $cfg = Get-Content $config -Raw | ConvertFrom-Json
    $cfg.FleetRoot = $fleetRoot
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $config
    $runArgs = @('api-run', '--desktop-sidecar', '--config', $config, '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $script:proc = Start-Process -FilePath $exe -ArgumentList $runArgs -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'headless.log') -RedirectStandardError (Join-Path $temp 'headless.err.log')
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 20)) { throw "Sidecar did not become healthy. $(Get-Content (Join-Path $temp 'headless.err.log') -Raw -ErrorAction SilentlyContinue)" }

    $script:snapshot = $null
    Test-RouteSmoke 'the explorer route returns the base worker and the party Pal, named by guild and owner, and nothing else' {
        $script:snapshot = Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/world/players-guilds" -TimeoutSec 60
        $pals = @($script:snapshot.palLocations)
        if ($pals.Count -ne 2) { throw "expected 2 Pals on the map, got $($pals.Count): $(($pals | ForEach-Object { $_.species }) -join ', ')" }
        $cat = @($pals | Where-Object { $_.species -eq 'PinkCat' })[0]
        if (-not $cat -or $cat.placement -ne 'BaseWorker' -or $cat.guildName -ne 'Smoke Guild' -or $cat.level -ne 15) { throw "base worker wrong: $($cat | ConvertTo-Json -Compress)" }
        $alpha = @($pals | Where-Object { $_.species -eq 'WeaselDragon' })[0]
        if (-not $alpha -or -not $alpha.isAlpha -or $alpha.placement -ne 'Party' -or $alpha.ownerName -ne 'Keeper') { throw "party Pal wrong: $($alpha | ConvertTo-Json -Compress)" }
        if ([Math]::Abs([double]$alpha.x - -351444.0) -gt 0.01) { throw "unexpected coordinates: $($alpha.x), $($alpha.y)" }
    }

    Test-RouteSmoke 'the Palbox Pal and the Pals without a real position are counted, not returned' {
        $s = $script:snapshot.palSummary
        if ($s.totalPals -ne 5 -or $s.onMap -ne 2 -or $s.inPalbox -ne 1 -or $s.withoutPosition -ne 2) { throw "summary wrong: $($s | ConvertTo-Json -Compress)" }
        if (@($script:snapshot.palLocations | Where-Object { $_.species -in 'Garm', 'Boar', 'Plesiosaur' }).Count -ne 0) { throw 'a Palbox or unset Pal was returned' }
    }
}
finally {
    if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.11.0 Pal positions smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.11.0 Pal positions smoke gate passed." -ForegroundColor Green
