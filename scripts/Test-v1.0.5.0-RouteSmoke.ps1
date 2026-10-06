# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18651,
    # Real archives from the repositories the MOD browser reads; without the network those checks are skipped.
    [switch]$SkipNetwork
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.5.0 (roadmap M-1): the service's ZIP install decides by the archive's layout (ModArchivePlanner), the same rules the
# MOD browser shows before an install, on an isolated service with a stand-in UE4SS:
#   1. A plain PAK installs into ~mods; a UE4SS MOD in a subfolder installs from that folder (Mods\<package>\Scripts\main.lua).
#   2. A PAK with scripts, a LogicMods PAK, several PAKs, a shimloader package, a program, a loader DLL and an archive with
#      no MOD are refused with the reason, nothing is written, and the refusal is in the activity log.
#   3. Real archives: a UE4SS MOD released on GitHub installs; Thunderstore's shimloader and mixed packages and PalDefender's
#      own release ZIP are refused.
if (-not $Exe) {
    $exeName = if ($IsWindows) { 'mysttiq-server.exe' } else { 'mysttiq-server' }
    $Exe = Join-Path $root "artifacts\publish\desktop-win-x64\headless\$exeName"
}
if (-not (Test-Path $Exe -PathType Leaf)) {
    if (-not $IsWindows) { Write-Host "[SKIP] The MOD archive smoke needs a published service: $Exe" -ForegroundColor Yellow; return }
    throw "Service not found: $Exe"
}
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.5.0-mods-" + [guid]::NewGuid().ToString('N'))
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

    Test-RouteSmoke 'a plain PAK installs into ~mods, and a UE4SS MOD in a subfolder installs from that folder' {
        $r = Install (New-Zip 'Plain.zip' @('Plain/Plain_P.pak', 'Plain/readme.txt')) 'PlainPak'
        if ($r.Status -ne 200 -or -not (Test-Path (Join-Path $paks 'PlainPak.pak'))) { throw "pak: $($r.Status) $($r.Message)" }
        $r = Install (New-Zip 'Nested.zip' @('CoolMod/enabled.txt', 'CoolMod/Scripts/main.lua', 'CoolMod/Scripts/util.lua', 'README.md')) 'CoolMod'
        if ($r.Status -ne 200) { throw "nested: $($r.Status) $($r.Message)" }
        if (-not (Test-Path (Join-Path $modsRoot 'CoolMod\Scripts\main.lua')) -or (Test-Path (Join-Path $modsRoot 'CoolMod\CoolMod'))) { throw "nested installed at the wrong depth: $(Files)" }
        if ((Get-Content (Join-Path $modsRoot 'mods.txt') -Raw) -notmatch 'CoolMod : 1') { throw 'the nested MOD is not enabled in mods.txt' }
    }
    Test-RouteSmoke 'archives MystTiq would install wrongly are refused with the reason, and nothing is written' {
        Expect-Refused (New-Zip 'Mixed.zip' @('Pal/Content/Paks/~mods/X.pak', 'Pal/Binaries/Win64/Mods/X/Scripts/main.lua')) 'Mixed' 'PAK and UE4SS scripts together' | Out-Null
        Expect-Refused (New-Zip 'Logic.zip' @('Pal/Content/Paks/LogicMods/Bp.pak')) 'Logic' 'LogicMods' | Out-Null
        Expect-Refused (New-Zip 'Several.zip' @('Option A/Big.pak', 'Option B/Small.pak')) 'Several' '2 different PAKs' | Out-Null
        Expect-Refused (New-Zip 'Shim.zip' @('manifest.json', 'icon.png', 'mod/scripts/main.lua', 'pak/Shim.pak')) 'Shim' 'unreal_shimloader' | Out-Null
        Expect-Refused (New-Zip 'Program.zip' @('Tool/Scripts/main.lua', 'Tool/setup.exe')) 'Program' 'program or script \(setup\.exe\)' | Out-Null
        Expect-Refused (New-Zip 'Loader.zip' @('dwmapi.dll', 'Mods/X/Scripts/main.lua')) 'Loader' 'loader DLL' | Out-Null
        Expect-Refused (New-Zip 'Empty.zip' @('readme.txt', 'docs/guide.md')) 'Empty' 'No MOD found' | Out-Null
        Expect-Refused (New-Zip 'Two.zip' @('A/Scripts/main.lua', 'B/Scripts/main.lua')) 'Two' '2 UE4SS MODs' | Out-Null
        $text = Invoke-RestMethod "$base/activity/tail?lines=300" | ConvertTo-Json -Depth 6
        $logged = ([regex]::Matches($text, 'MOD archive refused')).Count
        if ($logged -lt 8) { throw "the refusals are not all in the activity log ($logged)" }
    }
    $online = -not $SkipNetwork -and (Wait-Until { (Invoke-WebRequest 'https://api.github.com/zen' -TimeoutSec 10).StatusCode -eq 200 } 5)
    if (-not $online) { Write-Host '[SKIP] Route Smoke :: real repository archives (no network)' -ForegroundColor Yellow }
    else {
        Test-RouteSmoke 'real archives: a UE4SS MOD from a GitHub release installs; Thunderstore shimloader and mixed packages and PalDefender''s release ZIP are refused' {
            $get = { param($url, $name) $p = Join-Path $zips $name; Invoke-WebRequest $url -OutFile $p -TimeoutSec 120; $p }
            $gfb = & $get 'https://github.com/Stians92/palworld-guild-feed-box-sync/releases/download/v0.4.1/GuildFeedBox-0.4.1-manual.zip' 'gfb.zip'
            $r = Install $gfb 'GuildFeedBox'
            if ($r.Status -ne 200 -or -not (Test-Path (Join-Path $modsRoot 'GuildFeedBox\Scripts\main.lua'))) { throw "GuildFeedBox: $($r.Status) $($r.Message)" }
            Write-Host "    GitHub GuildFeedBox 0.4.1: $($r.Message)"
            $shim = & $get 'https://thunderstore.io/package/download/dubcats/ElementalRebalance/1.0.1/' 'ts-shim.zip'
            Write-Host "    Thunderstore ElementalRebalance: $(Expect-Refused $shim 'ElementalRebalance' 'unreal_shimloader')"
            $mixed = & $get 'https://thunderstore.io/package/download/PalModders/BasesPlus/1.1.1/' 'ts-mixed.zip'
            Write-Host "    Thunderstore BasesPlus: $(Expect-Refused $mixed 'BasesPlus' 'PAK and UE4SS scripts together')"
            $pd = & $get 'https://github.com/Ultimeit/PalDefender/releases/download/v1.9.3/PalDefender.zip' 'pd.zip'
            Write-Host "    GitHub PalDefender.zip: $(Expect-Refused $pd 'PalDefender' 'PalDefender|loader DLL')"
        }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(10000) | Out-Null }
    Start-Sleep -Milliseconds 300
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { throw "MystTiq v1.0.5.0 MOD archive smoke failed: $($failures -join '; ')" }
Write-Host 'MystTiq v1.0.5.0 MOD archive smoke passed.' -ForegroundColor Green
