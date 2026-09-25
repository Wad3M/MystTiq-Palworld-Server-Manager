[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$LinuxHost = '192.168.1.122',
    [string]$LinuxUser = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519"
)
$ErrorActionPreference = 'Stop'
# v0.8.13.0: an isolated MystTiq on the Linux test VM, exercising what v0.8.9.0-v0.8.13.0 added (v0.8.13.0: game names,
# read from the VM's real Linux game pak through a read-only symlink when there is one, else from a synthetic pak). It publishes the
# linux-x64 headless, builds a synthetic server folder (a crash report, a world save with Pals, Linux settings), copies
# both to a temp folder on the VM, runs Test-v0.8.13.0-LinuxIsolated.sh there (own config, FleetRoot, runtime and port
# 18412; the installed service and /etc/mysttiq are never touched), and deletes the folder afterwards. IPv4 addresses
# in the output are masked. The VM's address changes (DHCP), so pass -LinuxHost when it moves.
$root = (Resolve-Path $ProjectRoot).Path
$work = Join-Path $root ("artifacts\linux-isolated\v0.8.13.0-" + [guid]::NewGuid().ToString('N'))
$server = Join-Path $work 'fixture\server'

# Synthetic crash report shaped like the real ones (v0.8.9.0).
$crash = Join-Path $server 'Pal/Saved/Crashes/UECC-Linux-TEST_0000'
New-Item $crash -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $crash 'CrashContext.runtime-xml') '<?xml version="1.0" encoding="UTF-8"?><FGenericCrashContext><RuntimeProperties><CrashVersion>3</CrashVersion><ErrorMessage>LowLevelFatalError [File:Array.cpp] [Line: 8] Trying to resize TArray to an invalid size of 1</ErrorMessage><CrashType>Assert</CrashType><EngineVersion>5.1.1-0+++UE5+Release-5.1</EngineVersion></RuntimeProperties></FGenericCrashContext>' -NoNewline
# Settings in the Linux layout.
$cfg = Join-Path $server 'Pal/Saved/Config/LinuxServer'
New-Item $cfg -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $cfg 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Linux Check`",AdminPassword=`"a-long-random-admin-secret`",PublicPort=18512,RESTAPIEnabled=False,RCONEnabled=False)" -NoNewline
# Synthetic world save with Pals in a base, a party and a Palbox (v0.8.11.0), the same shape as the Windows smoke.
$world = Join-Path $server 'Pal/Saved/SaveGames/0/LinuxCheckWorld'
New-Item (Join-Path $world 'Players') -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $world 'Level.sav') 'linux-check-save' -NoNewline
Set-Content (Join-Path $world 'Players/A3835C7B000000000000000000000000.sav') 'keeper' -NoNewline
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
    (Pal 'aaaa0004' 'Sheepball' 3 $box '-250000.0' '200000.0' '6000' $owner)
) -join ",`n"
Set-Content (Join-Path $world 'Level.sav.json') -NoNewline @"
{ "properties": { "worldSaveData": { "value": {
  "GroupSaveDataMap": { "value": [ { "key": "11d02ac3-4634-e88b-6a92-62a0c74fcd53", "value": {
    "GroupType": { "value": { "value": "EPalGroupType::Guild" } },
    "RawData": { "value": { "group_type": "EPalGroupType::Guild", "group_id": "11d02ac3-4634-e88b-6a92-62a0c74fcd53", "guild_name": "Linux Guild",
      "admin_player_uid": "$owner", "base_ids": [ "$baseId" ],
      "players": [ { "player_uid": "$owner", "player_info": { "player_name": "Keeper" } } ] } } } } ] },
  "BaseCampSaveData": { "value": [ { "key": "$baseId", "value": {
    "RawData": { "value": { "spawn_transform": { "translation": { "x": -351088.7, "y": 271805.2, "z": 7422.7 } } } },
    "WorkerDirector": { "value": { "RawData": { "value": { "container_id": "$worker" } } } } } } ] },
  "CharacterContainerSaveData": { "value": [
    { "key": { "ID": { "value": "$worker" } }, "value": { "SlotNum": { "value": 9 } } },
    { "key": { "ID": { "value": "$party" } }, "value": { "SlotNum": { "value": 5 } } },
    { "key": { "ID": { "value": "$box" } }, "value": { "SlotNum": { "value": 960 } } } ] },
  "ItemContainerSaveData": { "value": [ { "Slots": [ { "RawData": { "value": { "item": { "static_id": "PalSphere" }, "count": 3 } } } ] } ] },
  "CharacterSaveParameterMap": { "value": [
$pals
  ] } } } } }
"@

& dotnet publish (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $work 'app') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish failed' }
if (-not (Test-Path (Join-Path $work 'app\Tools\extract_game_names.py'))) { throw 'the linux publish is missing Tools/extract_game_names.py' }
$sh = [IO.File]::ReadAllText((Join-Path $root 'scripts\Test-v0.8.13.0-LinuxIsolated.sh')).Replace("`r`n", "`n")
[IO.File]::WriteAllText((Join-Path $work 'check.sh'), $sh)
Copy-Item (Join-Path $root 'scripts\Testing\make_test_pak.py') (Join-Path $work 'make_test_pak.py')
$tgz = Join-Path $work 'linux-isolated.tgz'
Push-Location $work; tar -czf $tgz app fixture check.sh make_test_pak.py; Pop-Location

$sshArgs = @('-i', [Environment]::ExpandEnvironmentVariables($IdentityFile), '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10')
$remote = "$LinuxUser@$LinuxHost"
$dir = '/tmp/mysttiq-v0813-isolated'
ssh @sshArgs $remote "rm -rf $dir; mkdir -p $dir"
if ($LASTEXITCODE -ne 0) { throw "Cannot reach $remote over SSH (the VM's address may have changed; pass -LinuxHost)." }
scp @sshArgs $tgz "${remote}:$dir/" | Out-Null
$lines = @(ssh @sshArgs $remote "cd $dir && tar -xzf linux-isolated.tgz && bash check.sh; echo exit=`$?; cd /; rm -rf $dir" 2>&1)
$lines | ForEach-Object { "$_" -replace '\b(\d{1,3}\.){3}\d{1,3}\b', '<ip>' }
Remove-Item -LiteralPath $work -Recurse -Force
if (-not ($lines -match '^exit=0$')) { throw 'MystTiq v0.8.13.0 Linux isolated check failed.' }
Write-Host 'MystTiq v0.8.13.0 Linux isolated check passed.' -ForegroundColor Green
