# MystTiq v1.1.1.3: file reviewed for this release (2026-10-19).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18692
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.9.2 (roadmap B-3). A real service, isolated (its own fleet folder and port, sign-in on), over a server folder with
# UE4SS (three MODs in mods.txt) and one PAK, PalServer stopped:
#   1. The load order reads mods.txt top to bottom and lists the PAK.
#   2. Saving a new order rewrites mods.txt (only the MOD lines move) and keeps mods.txt.previous; a stale list is refused
#      with nothing changed; a Viewer may read but not change.
#   3. A profile exports with each MOD's state, UE4SS place and file hash; importing it previews, then applies the switch
#      and the order, lists the MOD this server lacks, and a file that is not a profile is refused in words.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not $IsWindows -and -not $PSBoundParameters.ContainsKey('Exe')) { $Exe = Join-Path $root 'artifacts\publish\linux-x64\headless\mysttiq-server' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }

$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.9.2-mod-order-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $tokenFile = Join-Path $temp 'api-token.txt'
New-Item $runtime, $backupRoot, $fleetRoot, $serverRoot -ItemType Directory -Force | Out-Null
$binaries = if ($IsWindows) { 'Win64' } else { 'Linux' }
$modsRoot = Join-Path $serverRoot "Pal\Binaries\$binaries\ue4ss\Mods"
foreach ($mod in 'ModA', 'ModB', 'Keybinds') {
    New-Item (Join-Path $modsRoot "$mod\Scripts") -ItemType Directory -Force | Out-Null
    Set-Content (Join-Path $modsRoot "$mod\Scripts\main.lua") "print('$mod')"
}
$modsTxt = Join-Path $modsRoot 'mods.txt'
[IO.File]::WriteAllLines($modsTxt, [string[]]@('; smoke', 'ModA : 1', 'ModB : 0', '', 'Keybinds : 1'))
$paks = Join-Path $serverRoot 'Pal\Content\Paks\~mods'
New-Item $paks -ItemType Directory -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $paks 'Thing.pak'), [byte[]](1, 2, 3, 4))

& $Exe config-write-default --config $config --overwrite | Out-Null
& $Exe api-token-create --token-file $tokenFile --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.Api.Port = $Port
$cfg.Api.Authentication.Enabled = $true
$cfg.Api.Authentication.TokenFile = $tokenFile
$cfg.FleetRoot = $fleetRoot
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config
$owner = (Get-Content $tokenFile -Raw).Trim()

function Call([string]$Method, [string]$Path, $Body = $null, [string]$Token = $owner, [switch]$Raw) {
    $headers = @{}; if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    $json = if ($Raw) { $Body } elseif ($null -ne $Body) { $Body | ConvertTo-Json -Depth 8 } else { $null }
    $r = Invoke-WebRequest "http://127.0.0.1:$Port$Path" -Method $Method -Headers $headers -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 60
    $parsed = $null; if ($r.Content) { try { $parsed = $r.Content | ConvertFrom-Json } catch { } }
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $parsed; Text = [string]$r.Content }
}
function Lines { @([IO.File]::ReadAllLines($modsTxt)) -join ' | ' }
$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

$log = Join-Path $temp 'svc'
$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput "$log.log" -RedirectStandardError "$log.err.log"
try {
    $up = $false
    for ($i = 0; $i -lt 80 -and -not $up; $i++) { try { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 | Out-Null; $up = $true } catch { Start-Sleep -Milliseconds 250 } }
    if (-not $up) { throw "the service is not answering. $(Get-Content "$log.err.log" -Raw -ErrorAction SilentlyContinue)" }
    $viewer = (Call POST '/api/v1/security/principals' @{ name = 'smoke-viewer'; role = 'Viewer' }).Body.plaintextToken
    if (-not $viewer) { throw 'could not create a Viewer token' }

    Test-RouteSmoke 'the load order reads mods.txt top to bottom and lists the PAK' {
        $r = Call GET '/api/v1/mods/load-order'
        $names = @($r.Body.ue4ss | ForEach-Object package) -join ','
        if ($r.Status -ne 200 -or $names -ne 'ModA,ModB,Keybinds' -or (@($r.Body.paks) -join ',') -ne 'Thing' -or -not $r.Body.hasModsTxt) { throw "$($r.Status) $names / $(@($r.Body.paks) -join ',')" }
    }
    Test-RouteSmoke 'saving an order moves only the MOD lines and keeps mods.txt.previous; a stale list and a Viewer are refused with nothing changed' {
        $original = [IO.File]::ReadAllText($modsTxt)
        $r = Call POST '/api/v1/mods/load-order' @{ order = @('Keybinds', 'ModB', 'ModA') }
        if ($r.Status -ne 200 -or (Lines) -ne '; smoke | Keybinds : 1 | ModB : 0 |  | ModA : 1') { throw "$($r.Status) $($r.Body.message) :: $(Lines)" }
        if ([IO.File]::ReadAllText("$modsTxt.previous") -ne $original) { throw 'the previous mods.txt is not kept' }
        $now = Lines
        $stale = Call POST '/api/v1/mods/load-order' @{ order = @('Ghost', 'ModA') }
        if ($stale.Status -ne 409 -or $stale.Body.message -notmatch 'is not in mods\.txt' -or (Lines) -ne $now) { throw "stale: $($stale.Status) $($stale.Body.message)" }
        $denied = Call POST '/api/v1/mods/load-order' @{ order = @('ModA', 'ModB', 'Keybinds') } -Token $viewer
        if ($denied.Status -ne 403 -or (Lines) -ne $now) { throw "viewer: $($denied.Status)" }
        if ((Call GET '/api/v1/mods/load-order' -Token $viewer).Status -ne 200) { throw 'a Viewer cannot read the order' }
        Write-Host "    $($r.Body.message)"
    }
    Test-RouteSmoke 'a profile exports each MOD with its state, UE4SS place and hash; importing previews, then applies, and lists the MOD this server lacks' {
        $profile = (Call GET '/api/v1/mods/profile').Body
        if ($profile.format -ne 'mysttiq-mod-profile' -or (@($profile.ue4ssOrder) -join ',') -ne 'Keybinds,ModB,ModA') { throw "export: $($profile.format) $(@($profile.ue4ssOrder) -join ',')" }
        $modB = @($profile.mods | Where-Object { $_.type -eq 'UE4SS' -and $_.package -eq 'ModB' })[0]
        $pak = @($profile.mods | Where-Object { $_.type -eq 'PAK' -and $_.package -eq 'Thing' })[0]
        if ($modB.enabled -or $modB.order -ne 2 -or $pak.sha256.Length -ne 64) { throw 'the exported MODs are wrong' }
        $modB.enabled = $true
        $profile.ue4ssOrder = @('ModA', 'ModB', 'Keybinds')
        $profile.mods = @($profile.mods) + [pscustomobject]@{ type = 'UE4SS'; package = 'Ghost'; enabled = $true; order = 4; version = '1.0'; source = 'https://example.invalid/ghost'; sha256 = $null }
        $before = Lines
        $preview = Call POST '/api/v1/mods/profile/import?apply=false' $profile
        if ($preview.Status -ne 200 -or $preview.Body.applied -or (Lines) -ne $before) { throw "preview: $($preview.Status) $($preview.Body.message)" }
        if ((@($preview.Body.changes) -join ' / ') -ne "Switch on UE4SS ModB / Set the UE4SS load order to the profile's" -or @($preview.Body.missing).Count -ne 1) { throw "preview changes: $(@($preview.Body.changes) -join ' / ')" }
        $applied = Call POST '/api/v1/mods/profile/import?apply=true' $profile
        if ($applied.Status -ne 200 -or -not $applied.Body.applied -or (Lines) -ne '; smoke | ModA : 1 | ModB : 1 |  | Keybinds : 1') { throw "apply: $($applied.Status) $($applied.Body.message) :: $(Lines)" }
        Write-Host "    $($applied.Body.message)"
    }
    Test-RouteSmoke 'a file that is not a profile is refused in words, and a Viewer cannot import' {
        $bad = Call POST '/api/v1/mods/profile/import?apply=false' 'this is not json' -Raw
        if ($bad.Status -ne 409 -or $bad.Body.message -ne 'This file is not a MystTiq MOD profile.') { throw "not json: $($bad.Status) $($bad.Text)" }
        $denied = Call POST '/api/v1/mods/profile/import?apply=false' '{}' -Token $viewer -Raw
        if ($denied.Status -ne 403) { throw "viewer import: $($denied.Status)" }
        if ((Call GET '/api/v1/mods/profile' -Token $viewer).Status -ne 200) { throw 'a Viewer cannot export' }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(15000) | Out-Null }
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { Write-Host "$($failures.Count) route smoke check(s) failed." -ForegroundColor Red; exit 1 }
Write-Host 'All v1.0.9.2 route smoke checks passed.' -ForegroundColor Green
