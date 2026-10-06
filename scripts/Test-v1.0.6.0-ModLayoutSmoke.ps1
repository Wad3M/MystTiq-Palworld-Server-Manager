# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18662,
    # Real archives from the repositories the MOD browser reads; without the network those checks are skipped.
    [switch]$SkipNetwork
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.6.0 (roadmap M-2; carries the v1.0.5.0 M-1 rules): the service's ZIP install decides by the archive's layout
# (ModArchivePlanner), on an isolated service with a stand-in UE4SS:
#   1. A plain PAK installs into ~mods; a UE4SS MOD in a subfolder installs from that folder.
#   2. M-2: a LogicMods PAK installs into Paks\LogicMods\<package> with the files beside it; a PAK with UE4SS scripts
#      installs as two parts under one name; both are listed and both parts are removed; without UE4SS, or with the name
#      taken, they are refused and nothing is written.
#   3. Several PAKs, a shimloader package, a program, a loader DLL, an archive with no MOD and two UE4SS MODs are refused
#      with the reason, nothing is written, and each refusal is in the activity log.
#   4. Real archives: a UE4SS MOD from a GitHub release and Thunderstore's BasesPlus (a LogicMods PAK with scripts) install;
#      Thunderstore's shimloader package and PalDefender's release ZIP are refused.
if (-not $Exe) {
    $exeName = if ($IsWindows) { 'mysttiq-server.exe' } else { 'mysttiq-server' }
    $Exe = Join-Path $root "artifacts\publish\desktop-win-x64\headless\$exeName"
}
if (-not (Test-Path $Exe -PathType Leaf)) {
    if (-not $IsWindows) { Write-Host "[SKIP] The MOD layout smoke needs a published service: $Exe" -ForegroundColor Yellow; return }
    throw "Service not found: $Exe"
}
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.6.0-modlayout-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $zips = Join-Path $temp 'zips'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot, $zips -ItemType Directory -Force | Out-Null
$win64 = Join-Path $serverRoot 'Pal\Binaries\Win64'
$modsRoot = Join-Path $win64 'ue4ss\Mods'; $paks = Join-Path $serverRoot 'Pal\Content\Paks\~mods'
New-Item $modsRoot -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $win64 'ue4ss\UE4SS.dll') 'stand-in UE4SS'
Set-Content (Join-Path $win64 'dwmapi.dll') 'stand-in loader'
Set-Content (Join-Path $modsRoot 'mods.txt') ''

& $Exe config-write-default --config $config --overwrite | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'the service could not write its configuration' }
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.api.Port = $Port
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$base = "http://127.0.0.1:$Port/api/v1"
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) { try { if (& $Condition) { return $true } } catch { }; Start-Sleep -Milliseconds 400 }
    return $false
}
function New-Zip([string]$Name, [string[]]$Entries) {
    $path = Join-Path $zips $Name
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try { foreach ($e in $Entries) { $w = [IO.StreamWriter]::new($archive.CreateEntry($e).Open()); try { $w.Write("stand-in $e") } finally { $w.Dispose() } } }
    finally { $archive.Dispose() }
    $path
}
function Install([string]$Zip, [string]$Package) {
    $r = Invoke-WebRequest "$base/mods/PAK/$Package/install-zip" -Method POST -InFile $Zip -ContentType 'application/zip' -SkipHttpErrorCheck -TimeoutSec 120
    [pscustomobject]@{ Status = [int]$r.StatusCode; Message = ($r.Content | ConvertFrom-Json).message }
}
function Files { @(Get-ChildItem $serverRoot -Recurse -File | ForEach-Object { $_.FullName.Substring($serverRoot.Length + 1) } | Sort-Object) -join '|' }
function Expect-Refused([string]$Zip, [string]$Package, [string]$Pattern) {
    $before = Files
    $r = Install $Zip $Package
    if ($r.Status -ne 409 -or $r.Message -notmatch $Pattern) { throw "expected a refusal matching '$Pattern', got $($r.Status): $($r.Message)" }
    if ((Files) -ne $before) { throw "a refused archive changed the server folder: $($r.Message)" }
    $r.Message
}

$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 40)) { throw "the service is not answering. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }

    $logicRoot = Join-Path $serverRoot 'Pal\Content\Paks\LogicMods'
    function Listed([string]$Type, [string]$Package) { @((Invoke-RestMethod "$base/mods").mods | Where-Object { $_.type -eq $Type -and $_.package -eq $Package }).Count -eq 1 }
    function Delete([string]$Type, [string]$Package) { [int](Invoke-WebRequest "$base/mods/$Type/$Package" -Method DELETE -SkipHttpErrorCheck -TimeoutSec 60).StatusCode }
    Test-RouteSmoke 'a plain PAK installs into ~mods, and a UE4SS MOD in a subfolder installs from that folder' {
        $r = Install (New-Zip 'Plain.zip' @('Plain/Plain_P.pak', 'Plain/readme.txt')) 'PlainPak'
        if ($r.Status -ne 200 -or -not (Test-Path (Join-Path $paks 'PlainPak.pak'))) { throw "pak: $($r.Status) $($r.Message)" }
        $r = Install (New-Zip 'Nested.zip' @('CoolMod/enabled.txt', 'CoolMod/Scripts/main.lua', 'README.md')) 'CoolMod'
        if ($r.Status -ne 200 -or -not (Test-Path (Join-Path $modsRoot 'CoolMod\Scripts\main.lua'))) { throw "nested: $($r.Status) $($r.Message)" }
    }
    Test-RouteSmoke 'M-2: a LogicMods PAK installs into its own LogicMods folder with the files beside it, is listed, and is removed' {
        $r = Install (New-Zip 'Logic.zip' @('Bp/Pal/Content/Paks/LogicMods/Bp.pak', 'Bp/Pal/Content/Paks/LogicMods/Bp.modconfig.json', 'README.md')) 'LogicBp'
        if ($r.Status -ne 200 -or -not (Test-Path (Join-Path $logicRoot 'LogicBp\Bp.pak')) -or -not (Test-Path (Join-Path $logicRoot 'LogicBp\Bp.modconfig.json'))) { throw "logic: $($r.Status) $($r.Message)" }
        if ($r.Message -notmatch 'BPModLoaderMod') { throw "no note that BPModLoaderMod is off: $($r.Message)" }
        if (-not (Listed 'PAK' 'LogicBp')) { throw 'the LogicMods MOD is not listed' }
        Write-Host "    $($r.Message)"
        $again = Install (New-Zip 'Logic2.zip' @('Pal/Content/Paks/LogicMods/Other.pak')) 'LogicBp'
        if ($again.Status -ne 409 -or $again.Message -notmatch 'already exists') { throw "name taken: $($again.Status) $($again.Message)" }
        if ((Delete 'PAK' 'LogicBp') -ne 200 -or (Test-Path (Join-Path $logicRoot 'LogicBp'))) { throw 'the LogicMods MOD was not removed' }
    }
    Test-RouteSmoke 'M-2: a PAK with UE4SS scripts installs as two parts under one name, both listed, both removed' {
        $r = Install (New-Zip 'Mixed.zip' @('Mixed/Mixed_P.pak', 'Mixed/Scripts/main.lua', 'Mixed/enabled.txt')) 'MixedMod'
        if ($r.Status -ne 200 -or -not (Test-Path (Join-Path $paks 'MixedMod.pak')) -or -not (Test-Path (Join-Path $modsRoot 'MixedMod\Scripts\main.lua'))) { throw "mixed: $($r.Status) $($r.Message)" }
        if (-not (Listed 'PAK' 'MixedMod') -or -not (Listed 'UE4SS' 'MixedMod')) { throw 'both parts are not listed' }
        if ((Get-Content (Join-Path $modsRoot 'mods.txt') -Raw) -notmatch 'MixedMod : 1') { throw 'the script part is not enabled in mods.txt' }
        if ((Delete 'PAK' 'MixedMod') -ne 200 -or (Delete 'UE4SS' 'MixedMod') -ne 200 -or (Test-Path (Join-Path $paks 'MixedMod.pak')) -or (Test-Path (Join-Path $modsRoot 'MixedMod'))) { throw 'the parts were not removed' }
    }
    Test-RouteSmoke 'M-2: without UE4SS a LogicMods MOD is refused and nothing is written' {
        $ue = Join-Path $win64 'ue4ss'; $aside = Join-Path $win64 'ue4ss.aside'
        Rename-Item $ue $aside
        try { Expect-Refused (New-Zip 'NoUe.zip' @('Pal/Content/Paks/LogicMods/Need.pak')) 'NeedUe' 'needs UE4SS' | Out-Null }
        finally { Rename-Item $aside $ue }
    }
    Test-RouteSmoke 'archives MystTiq would install wrongly are refused with the reason, and nothing is written' {
        Expect-Refused (New-Zip 'Several.zip' @('Option A/Big.pak', 'Option B/Small.pak')) 'Several' '2 different PAKs' | Out-Null
        Expect-Refused (New-Zip 'Shim.zip' @('manifest.json', 'icon.png', 'mod/scripts/main.lua', 'pak/Shim.pak')) 'Shim' 'unreal_shimloader' | Out-Null
        Expect-Refused (New-Zip 'Program.zip' @('Tool/Scripts/main.lua', 'Tool/setup.exe')) 'Program' 'program or script \(setup\.exe\)' | Out-Null
        Expect-Refused (New-Zip 'Loader.zip' @('dwmapi.dll', 'Mods/X/Scripts/main.lua')) 'Loader' 'loader DLL' | Out-Null
        Expect-Refused (New-Zip 'Empty.zip' @('readme.txt', 'docs/guide.md')) 'Empty' 'No MOD found' | Out-Null
        Expect-Refused (New-Zip 'Two.zip' @('A/Scripts/main.lua', 'B/Scripts/main.lua')) 'Two' '2 UE4SS MODs' | Out-Null
        $text = Invoke-RestMethod "$base/activity/tail?lines=300" | ConvertTo-Json -Depth 6
        if (([regex]::Matches($text, 'MOD archive refused')).Count -lt 6) { throw 'the refusals are not all in the activity log' }
    }
    $online = -not $SkipNetwork -and (Wait-Until { (Invoke-WebRequest 'https://api.github.com/zen' -TimeoutSec 10).StatusCode -eq 200 } 5)
    if (-not $online) { Write-Host '[SKIP] Route Smoke :: real repository archives (no network)' -ForegroundColor Yellow }
    else {
        Test-RouteSmoke 'real archives: a GitHub UE4SS MOD and Thunderstore''s BasesPlus install where the game loads them; a shimloader package and PalDefender''s ZIP are refused' {
            $get = { param($url, $name) $p = Join-Path $zips $name; Invoke-WebRequest $url -OutFile $p -TimeoutSec 120; $p }
            $r = Install (& $get 'https://github.com/Stians92/palworld-guild-feed-box-sync/releases/download/v0.4.1/GuildFeedBox-0.4.1-manual.zip' 'gfb.zip') 'GuildFeedBox'
            if ($r.Status -ne 200 -or -not (Test-Path (Join-Path $modsRoot 'GuildFeedBox\Scripts\main.lua'))) { throw "GuildFeedBox: $($r.Status) $($r.Message)" }
            $r = Install (& $get 'https://thunderstore.io/package/download/PalModders/BasesPlus/1.1.1/' 'ts-basesplus.zip') 'BasesPlus'
            if ($r.Status -ne 200 -or -not (Test-Path (Join-Path $logicRoot 'BasesPlus\BasesPlus.pak')) -or -not (Test-Path (Join-Path $logicRoot 'BasesPlus\BasesPlus.modconfig.json')) -or
                -not (Test-Path (Join-Path $modsRoot 'BasesPlus\Scripts\main.lua'))) { throw "BasesPlus: $($r.Status) $($r.Message)" }
            Write-Host "    Thunderstore BasesPlus: $($r.Message)"
            Write-Host "    Thunderstore ElementalRebalance: $(Expect-Refused (& $get 'https://thunderstore.io/package/download/dubcats/ElementalRebalance/1.0.1/' 'ts-shim.zip') 'ElementalRebalance' 'unreal_shimloader')"
            Write-Host "    GitHub PalDefender.zip: $(Expect-Refused (& $get 'https://github.com/Ultimeit/PalDefender/releases/download/v1.9.3/PalDefender.zip' 'pd.zip') 'PalDefender' 'PalDefender|loader DLL')"
        }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(10000) | Out-Null }
    Start-Sleep -Milliseconds 300
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { throw "MystTiq v1.0.6.0 MOD layout smoke failed: $($failures -join '; ')" }
Write-Host 'MystTiq v1.0.6.0 MOD layout smoke passed.' -ForegroundColor Green
