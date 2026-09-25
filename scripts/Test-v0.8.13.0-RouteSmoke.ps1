[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18313
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.8.13.0: item and Pal display names from the server's own game pak, on the PUBLISHED exe. The fixture server has a
# tiny synthetic pak (scripts/Testing/make_test_pak.py: version 11, uncompressed, made-up rows; no game files, no Oodle)
# and a world save with a Pal Sphere, a Lamball (saved as "Sheepball") and a base-worker Cattiva (PinkCat).
#   1. the Give Item catalogue names what the world has, adds what only the game lists (marked), and says where the names come from;
#   2. the explorer's Pal positions carry the species name;
#   3. the names are cached under the manager runtime root and reused.
# Needs Python on PATH (as the feature does). Own FleetRoot and port; no game server is involved.

$python = (Get-Command python.exe, python3, python -ErrorAction SilentlyContinue | Where-Object { $_.Source -notmatch 'WindowsApps' } | Select-Object -First 1).Source
if (-not $python) { Write-Host '[SKIP] Python is not on PATH, so the game-name extractor cannot run here.' -ForegroundColor Yellow; return }
$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
if (-not (Test-Path (Join-Path (Split-Path $exe) 'Tools\extract_game_names.py') -PathType Leaf)) { throw 'The published headless is missing Tools\extract_game_names.py' }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.13.0-gamenames-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $steamCmd = Join-Path $temp 'missing-steamcmd.exe'
New-Item $runtime, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$paks = Join-Path $serverRoot 'Pal\Content\Paks'
New-Item $paks -ItemType Directory -Force | Out-Null
& $python (Join-Path $root 'scripts\Testing\make_test_pak.py') (Join-Path $paks 'Pal-WindowsServer.pak') '{"PalSphere":"Pal Sphere","Wood":"Wood"}' '{"SheepBall":"Lamball","PinkCat":"Cattiva"}'
if ($LASTEXITCODE -ne 0) { throw 'could not build the synthetic pak' }

$world = Join-Path $serverRoot 'Pal\Saved\SaveGames\0\GameNameSmokeWorld'
New-Item (Join-Path $world 'Players') -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $world 'Level.sav') 'game-name-smoke-save'
$worker = '86d4a7a0-4298-7ee5-1dbd-cdbff9f5f22d'; $baseId = 'd04f779e-4ef8-578b-7c84-12b759e117e1'
Set-Content (Join-Path $world 'Level.sav.json') @"
{ "properties": { "worldSaveData": { "value": {
  "ItemContainerSaveData": { "value": [ { "Slots": [ { "RawData": { "value": { "item": { "static_id": "PalSphere" }, "count": 3 } } } ] } ] },
  "BaseCampSaveData": { "value": [ { "key": "$baseId", "value": {
    "RawData": { "value": { "spawn_transform": { "translation": { "x": -351088.7, "y": 271805.2, "z": 7422.7 } } } },
    "WorkerDirector": { "value": { "RawData": { "value": { "container_id": "$worker" } } } } } } ] },
  "CharacterContainerSaveData": { "value": [ { "key": { "ID": { "value": "$worker" } }, "value": { "SlotNum": { "value": 9 } } } ] },
  "CharacterSaveParameterMap": { "value": [
    { "key": { "PlayerUId": { "value": "00000000-0000-0000-0000-000000000000" }, "InstanceId": { "value": "aaaa0001" } },
      "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
        "CharacterID": { "value": "PinkCat" }, "Level": { "value": { "type": "None", "value": 15 } },
        "SlotId": { "value": { "ContainerId": { "value": { "ID": { "value": "$worker" } } } } },
        "LastJumpedLocation": { "value": { "x": -355668.1, "y": 272597.2, "z": 7100 } } } } } } } } },
    { "key": { "PlayerUId": { "value": "00000000-0000-0000-0000-000000000000" }, "InstanceId": { "value": "aaaa0002" } },
      "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
        "CharacterID": { "value": "Sheepball" }, "Level": { "value": { "type": "None", "value": 3 } } } } } } } } }
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

    Test-RouteSmoke 'the Give Item catalogue names what the world has, adds what only the game lists, and says where names come from' {
        $c = Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/players/give/catalog" -TimeoutSec 60
        if (-not $c.namesAvailable) { throw "names not available: $($c.namesDetail)" }
        $sphere = @($c.entries | Where-Object { $_.id -eq 'PalSphere' })
        if ($sphere.Count -ne 1 -or $sphere[0].name -ne 'Pal Sphere' -or $sphere[0].inGameFiles -or $sphere[0].worldCount -lt 1) { throw "PalSphere row wrong: $($sphere | ConvertTo-Json -Compress)" }
        $lamb = @($c.entries | Where-Object { $_.kind -eq 'Pal' -and $_.id -eq 'Sheepball' })
        if ($lamb.Count -ne 1 -or $lamb[0].name -ne 'Lamball') { throw "the save's Sheepball should be named Lamball: $($lamb | ConvertTo-Json -Compress)" }
        $wood = @($c.entries | Where-Object { $_.id -eq 'Wood' })
        if ($wood.Count -ne 1 -or -not $wood[0].inGameFiles -or $wood[0].worldCount -ne 0) { throw "Wood should be listed from the game files: $($wood | ConvertTo-Json -Compress)" }
        if ($c.namesDetail -notmatch 'Pal-WindowsServer\.pak') { throw "the detail should name the pak: $($c.namesDetail)" }
    }

    Test-RouteSmoke 'the explorer Pal positions carry the species display name' {
        $s = Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/world/players-guilds" -TimeoutSec 60
        $cat = @($s.palLocations | Where-Object { $_.species -eq 'PinkCat' })
        if ($cat.Count -ne 1 -or $cat[0].speciesName -ne 'Cattiva') { throw "PinkCat should be named Cattiva: $($s.palLocations | ConvertTo-Json -Compress)" }
    }

    Test-RouteSmoke 'the names are cached under the manager runtime root' {
        $cache = @(Get-ChildItem $temp -Recurse -Filter 'en.json' | Where-Object { $_.Directory.Name -eq 'game-names' })
        if ($cache.Count -ne 1) { throw 'no game-names cache was written' }
        $j = Get-Content $cache[0].FullName -Raw | ConvertFrom-Json
        if ($j.pals.SheepBall -ne 'Lamball' -or $j.pak -notmatch 'Pal-WindowsServer\.pak$') { throw "unexpected cache: $($j | ConvertTo-Json -Compress)" }
    }
}
finally {
    if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.13.0 game names smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.13.0 game names smoke gate passed." -ForegroundColor Green
