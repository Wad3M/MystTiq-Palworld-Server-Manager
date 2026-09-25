[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18383,
    [int]$GamePort = 18483
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.8.3.0: the Give Item picker's catalogue route (GET /api/v1/players/give/catalog) on the PUBLISHED exe, against a
# synthetic decoded world save shaped like palworld-save-tools output (no real player data):
#   1. with no world save yet it answers, lists the kit ids, and says why the world's items are unknown;
#   2. with a decoded save it lists the world's item and Pal ids (alpha as its species), merged with kit ids, and the
#      profile-scoped route answers the same;
#   3. a large save (~20 MB, 200,000 item slots) is read in reasonable time, and the second read comes from the cache;
#   4. a changed save is read again.
# Own FleetRoot and ports; nothing touches a real server.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.3.0-catalog-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $steamCmd = Join-Path $temp 'missing-steamcmd.exe'
New-Item $runtime, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Catalog Smoke`",AdminPassword=`"a-long-random-admin-secret`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False)"
# A saved kit, so kit ids are part of the catalogue.
New-Item (Join-Path $runtime 'players') -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $runtime 'players\kits.json') (@{
    AutoGiftEnabled = $false; AutoGiftKitId = ''; AutoGiftEnabledAtUtc = $null
    Kits = @(@{ Id = 'starter'; Name = 'Starter'; Entries = @(@{ Type = 'Item'; Id = 'Wood'; Amount = 50 }, @{ Type = 'Item'; Id = 'palsphere'; Amount = 5 }) })
} | ConvertTo-Json -Depth 6)

$world = Join-Path $serverRoot 'Pal\Saved\SaveGames\0\SMOKEWORLD'
function Slot([string]$Id) { "{`"RawData`":{`"value`":{`"slot_index`":0,`"count`":1,`"item`":{`"static_id`":`"$Id`",`"dynamic_id`":{}}}}}" }
function Pal([string]$Id) { "{`"value`":{`"RawData`":{`"value`":{`"object`":{`"SaveParameter`":{`"value`":{`"CharacterID`":{`"id`":null,`"value`":`"$Id`",`"type`":`"NameProperty`"},`"Level`":{`"value`":`"NotAPal`"}}}}}}}}" }
function Save([string[]]$Slots, [string[]]$Pals) {
    "{`"properties`":{`"worldSaveData`":{`"value`":{`"CharacterSaveParameterMap`":{`"value`":[$($Pals -join ',')]},`"ItemContainerSaveData`":{`"value`":[{`"value`":{`"Slots`":{`"value`":{`"values`":[$($Slots -join ',')]}}}}]}}}}}"
}

$failures = @()
$script:proc = $null
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Get-Catalog([string]$Base = '/api/v1') { Invoke-RestMethod "http://127.0.0.1:$Port$Base/players/give/catalog" -TimeoutSec 60 }

try {
    & $exe config-write-default --config $config --overwrite | Out-Null
    $cfg = Get-Content $config -Raw | ConvertFrom-Json
    $cfg.FleetRoot = $fleetRoot
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $config
    $runArgs = @('api-run', '--desktop-sidecar', '--config', $config, '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $script:proc = Start-Process -FilePath $exe -ArgumentList $runArgs -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'headless.log') -RedirectStandardError (Join-Path $temp 'headless.err.log')
    $healthy = $false
    for ($i = 0; $i -lt 60 -and -not $healthy; $i++) { Start-Sleep -Milliseconds 250; try { $healthy = [bool](Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2) } catch {} }
    if (-not $healthy) { throw "Sidecar did not become healthy. $(Get-Content (Join-Path $temp 'headless.err.log') -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'with no world save it lists the kit ids and says why the world is unknown' {
        $c = Get-Catalog
        if ($c.worldAvailable) { throw 'reported a world where there is none' }
        $ids = @($c.entries | ForEach-Object { $_.id }) -join ','
        if ($ids -ne 'palsphere,Wood') { throw "expected the two kit ids, got '$ids'" }
        if ($c.detail -notmatch 'No world save') { throw "detail: $($c.detail)" }
    }

    Test-RouteSmoke 'a decoded save gives the world''s item and Pal ids merged with kit ids, on both route forms' {
        New-Item $world -ItemType Directory -Force | Out-Null
        Set-Content (Join-Path $world 'Level.sav') 'sav'
        Set-Content (Join-Path $world 'Level.sav.json') (Save @((Slot 'PalSphere'), (Slot 'Money'), (Slot 'Money'), (Slot 'None'), (Slot '')) @((Pal 'Alpaca'), (Pal 'BOSS_Garm')))
        $c = Get-Catalog
        if (-not $c.worldAvailable -or $c.worldId -ne 'SMOKEWORLD') { throw "world not read: $($c.detail)" }
        $rows = @($c.entries | ForEach-Object { "$($_.kind):$($_.id)" }) -join ','
        if ($rows -ne 'Item:Money,Item:PalSphere,Item:Wood,Pal:Alpaca,Pal:Garm') { throw "rows: $rows" }
        $sphere = @($c.entries | Where-Object { $_.id -eq 'PalSphere' })[0]
        if ($sphere.worldCount -ne 1 -or -not $sphere.inKit) { throw 'the kit''s "palsphere" did not join the world''s PalSphere row' }
        if (-not @($c.entries | Where-Object { $_.id -eq 'Garm' })[0].alphaSeen) { throw 'the alpha was not marked' }
        $scoped = Get-Catalog '/api/v1/servers/default'
        if ((@($scoped.entries | ForEach-Object { $_.id }) -join ',') -ne (@($c.entries | ForEach-Object { $_.id }) -join ',')) { throw 'the profile-scoped route answers differently' }
    }

    Test-RouteSmoke 'a large save (200,000 slots) is read in reasonable time and then served from the cache' {
        $slots = [System.Collections.Generic.List[string]]::new()
        for ($i = 0; $i -lt 200000; $i++) { $slots.Add((Slot ("Item_{0}" -f ($i % 1500)))) }
        [IO.File]::WriteAllText((Join-Path $world 'Level.sav.json'), (Save $slots.ToArray() @((Pal 'Alpaca'))))
        $size = (Get-Item (Join-Path $world 'Level.sav.json')).Length
        $first = Measure-Command { $script:big = Get-Catalog }
        if ($script:big.itemCount -ne 1502) { throw "expected 1500 world items + 2 kit items, got $($script:big.itemCount)" }
        if ($first.TotalSeconds -gt 20) { throw "first read took $([int]$first.TotalSeconds) s for $([int]($size / 1MB)) MB" }
        $second = Measure-Command { Get-Catalog | Out-Null }
        if ($second.TotalMilliseconds -gt [Math]::Max(1500, $first.TotalMilliseconds / 2)) { throw "second read was not cached ($([int]$second.TotalMilliseconds) ms vs $([int]$first.TotalMilliseconds) ms)" }
        Write-Host ("        {0:N0} MB: first read {1:N0} ms, cached {2:N0} ms" -f ($size / 1MB), $first.TotalMilliseconds, $second.TotalMilliseconds)
    }

    Test-RouteSmoke 'a changed save is read again' {
        Set-Content (Join-Path $world 'Level.sav.json') (Save @((Slot 'Stone')) @())
        (Get-Item (Join-Path $world 'Level.sav.json')).LastWriteTimeUtc = (Get-Date).ToUniversalTime().AddMinutes(1)
        $ids = @((Get-Catalog).entries | ForEach-Object { $_.id }) -join ','
        if ($ids -ne 'palsphere,Stone,Wood') { throw "expected the new save's ids, got '$ids'" }
    }
}
finally {
    if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force -ErrorAction SilentlyContinue; $script:proc.WaitForExit(5000) | Out-Null }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.3.0 give item catalogue smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.3.0 give item catalogue smoke gate passed." -ForegroundColor Green
