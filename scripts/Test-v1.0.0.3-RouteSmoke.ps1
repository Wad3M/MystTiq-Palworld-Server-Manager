# MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18613
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.0.3: NATIVE MODs through the service's routes, on isolated data laid out like the owner's server on 2026-10-05
# (PalDefender's d3d9.dll and UE4SS's dwmapi.dll renamed to *.disabled-test by hand, and an older dwmapi.dll.myst-disabled
# from July beside it):
#   1. The MODs list shows PalDefender and the UE4SS loader as switched-off NATIVE MODs, and a UE4SS MOD enabled in mods.txt
#      as not loading because its loader is off.
#   2. Switching them on restores d3d9.dll and the newest dwmapi.dll (not the July one); the UE4SS MOD is fine again.
#   3. Switching PalDefender off writes d3d9.dll.mysttiq-disabled, and switching it on restores that one first.
#   4. Disable all and enable all include them; deleting a NATIVE MOD and installing PalDefender as a ZIP are refused.
if (-not $Exe) {
    $exeName = if ($IsWindows) { 'mysttiq-server.exe' } else { 'mysttiq-server' }
    $Exe = Join-Path $root "artifacts\publish\desktop-win-x64\headless\$exeName"
}
if (-not (Test-Path $Exe -PathType Leaf)) {
    if (-not $IsWindows) { Write-Host "[SKIP] The NATIVE MOD smoke needs a published service: $Exe" -ForegroundColor Yellow; return }
    throw "Service not found: $Exe"
}
$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.0.3-native-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null

$win64 = Join-Path $serverRoot 'Pal\Binaries\Win64'
New-Item (Join-Path $win64 'PalDefender\Logs'), (Join-Path $win64 'ue4ss\Mods\Harmless\Scripts') -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $win64 'PalDefender.dll') 'stand-in PalDefender'
Set-Content (Join-Path $win64 'd3d9.dll.disabled-test') 'stand-in d3d9 loader'
Set-Content (Join-Path $win64 'd3d9_config.json') '{ "load_dlls": [ "PalDefender.dll" ] }'
Set-Content (Join-Path $win64 'ue4ss\UE4SS.dll') 'stand-in UE4SS'
Set-Content (Join-Path $win64 'dwmapi.dll.myst-disabled') 'old July loader'
(Get-Item (Join-Path $win64 'dwmapi.dll.myst-disabled')).LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-90)
Set-Content (Join-Path $win64 'dwmapi.dll.disabled-test') 'current loader'
Set-Content (Join-Path $win64 'ue4ss\Mods\Harmless\Scripts\main.lua') "print('Harmless')"
Set-Content (Join-Path $win64 'ue4ss\Mods\mods.txt') 'Harmless : 1'

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
function Invoke-Api([string]$Method, [string]$Path) {
    $r = Invoke-WebRequest "$base$Path" -Method $Method -ContentType 'application/json' -SkipHttpErrorCheck -TimeoutSec 60
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null }) }
}
function Mod([string]$Package) { @((Invoke-Api GET '/mods').Body.mods | Where-Object { $_.package -eq $Package })[0] }
function Has([string]$Name) { Test-Path (Join-Path $win64 $Name) -PathType Leaf }
function Text([string]$Name) { (Get-Content (Join-Path $win64 $Name) -Raw).Trim() }

$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 40)) { throw "the service is not answering. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'PalDefender and the UE4SS loader are listed as switched-off NATIVE MODs, and an enabled UE4SS MOD is shown as not loading' {
        $pd = Mod 'PalDefender'; $loader = Mod 'UE4SS-Loader'; $harmless = Mod 'Harmless'
        if (-not $pd -or $pd.type -ne 'NATIVE' -or $pd.enabled -or $pd.health -ne 'Disabled' -or $pd.evidence -notmatch 'd3d9\.dll\.disabled-test') { throw "PalDefender: $($pd | ConvertTo-Json -Compress)" }
        if (-not $loader -or $loader.type -ne 'NATIVE' -or $loader.enabled -or $loader.health -ne 'Disabled') { throw "UE4SS loader: $($loader | ConvertTo-Json -Compress)" }
        if (-not $harmless -or $harmless.health -ne 'Attention' -or $harmless.attention -ne 'UE4SS loader is off') { throw "Harmless: $($harmless | ConvertTo-Json -Compress)" }
    }

    Test-RouteSmoke 'switching them on restores d3d9.dll and the newest dwmapi.dll (not the July one); the UE4SS MOD is no longer flagged' {
        foreach ($p in 'PalDefender', 'UE4SS-Loader') {
            $r = Invoke-Api POST "/mods/NATIVE/$p/enabled?enabled=true"
            if ($r.Status -ne 200) { throw "enable ${p}: $($r.Status) $($r.Body.message)" }
        }
        if ((Text 'd3d9.dll') -ne 'stand-in d3d9 loader' -or (Has 'd3d9.dll.disabled-test')) { throw 'd3d9.dll was not restored' }
        if ((Text 'dwmapi.dll') -ne 'current loader' -or -not (Has 'dwmapi.dll.myst-disabled')) { throw "dwmapi.dll is [$(Text 'dwmapi.dll')]; the July copy must stay switched off" }
        $pd = Mod 'PalDefender'; $harmless = Mod 'Harmless'
        if (-not $pd.enabled -or $pd.health -ne 'Active / Unverified' -or $harmless.health -eq 'Attention') { throw "after: PalDefender=$($pd.health) Harmless=$($harmless.health)" }
    }

    Test-RouteSmoke 'switching PalDefender off writes d3d9.dll.mysttiq-disabled, and switching it on restores that copy before a newer hand-made one' {
        $off = Invoke-Api POST '/mods/NATIVE/PalDefender/enabled?enabled=false'
        if ($off.Status -ne 200 -or (Has 'd3d9.dll') -or -not (Has 'd3d9.dll.mysttiq-disabled')) { throw "off: $($off.Status) $($off.Body.message)" }
        Set-Content (Join-Path $win64 'd3d9.dll.disabled') 'some other copy'
        $on = Invoke-Api POST '/mods/NATIVE/PalDefender/enabled?enabled=true'
        if ($on.Status -ne 200 -or (Text 'd3d9.dll') -ne 'stand-in d3d9 loader' -or -not (Has 'd3d9.dll.disabled')) { throw "on: $($on.Status) d3d9.dll=[$(Text 'd3d9.dll')]" }
    }

    Test-RouteSmoke 'disable all and enable all include the NATIVE MODs; deleting one and installing PalDefender as a ZIP are refused' {
        $all = Invoke-Api POST '/mods/all/enabled?enabled=false'
        if ($all.Status -ne 200 -or (Has 'd3d9.dll') -or (Has 'dwmapi.dll')) { throw "disable all: $($all.Status); d3d9=$(Has 'd3d9.dll') dwmapi=$(Has 'dwmapi.dll')" }
        $all = Invoke-Api POST '/mods/all/enabled?enabled=true'
        if ($all.Status -ne 200 -or -not (Has 'd3d9.dll') -or (Text 'dwmapi.dll') -ne 'current loader') { throw "enable all: $($all.Status)" }
        $delete = Invoke-Api DELETE '/mods/NATIVE/PalDefender'
        if ($delete.Status -ne 409 -or $delete.Body.message -notmatch 'can only be switched on or off here' -or -not (Has 'PalDefender.dll')) { throw "delete: $($delete.Status) $($delete.Body.message)" }
        $zipSource = Join-Path $temp 'pd-zip'; New-Item $zipSource -ItemType Directory -Force | Out-Null
        Set-Content (Join-Path $zipSource 'PalDefender.dll') 'x'; Set-Content (Join-Path $zipSource 'd3d9.dll') 'y'
        $zip = Join-Path $temp 'PalDefender.zip'; Compress-Archive -Path (Join-Path $zipSource '*') -DestinationPath $zip
        $install = Invoke-WebRequest "$base/mods/UE4SS/PalDefender/install-zip" -Method POST -InFile $zip -ContentType 'application/zip' -SkipHttpErrorCheck -TimeoutSec 60
        $installBody = $install.Content | ConvertFrom-Json
        if ([int]$install.StatusCode -ne 409 -or $installBody.message -notmatch 'not installed as a MOD folder' -or (Test-Path (Join-Path $win64 'ue4ss\Mods\PalDefender'))) { throw "install-zip: $($install.StatusCode) $($installBody.message)" }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(10000) | Out-Null }
}

if ($failures.Count -gt 0) { throw "MystTiq v1.0.0.3 NATIVE MOD smoke failed: $($failures.Count) check(s)." }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'MystTiq v1.0.0.3 NATIVE MOD smoke passed.' -ForegroundColor Green
