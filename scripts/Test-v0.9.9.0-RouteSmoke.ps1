# MystTiq v1.0.0.1: file reviewed for this release (2026-10-04).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18601,
    [int]$AdvertisedPort = 18801,
    [int]$BoundPort = 18802
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v0.9.9.0, on isolated data with no game server running (api-run, as the Desktop runs it):
#   1. The Doctor's "game port" finding offers a fix when the server binds one port and advertises another; the fix sets
#      PublicPort in PalWorldSettings.ini to the bound port, and the finding then passes.
#   2. Crash analysis names a PalDefender session whose log ends on a player joining (the log lines are from this
#      project's own live session on 2026-09-28, the player id shortened), and says nothing for a session that ended
#      cleanly.
if (-not $Exe) { $Exe = Join-Path $root ('artifacts\publish\desktop-win-x64\headless\mysttiq-server' + $(if ($IsWindows) { '.exe' } else { '' })) }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Headless sidecar not found: $Exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.9.9.0-route-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $fleet = Join-Path $temp 'fleet'
$serverRoot = Join-Path $temp 'server'; $runtime = Join-Path $temp 'runtime'; $backups = Join-Path $temp 'backups'
New-Item $fleet, $serverRoot, $runtime, $backups -ItemType Directory -Force | Out-Null
$platformFolder = if ($IsWindows) { 'WindowsServer' } else { 'LinuxServer' }
$iniDir = Join-Path $serverRoot "Pal\Saved\Config\$platformFolder"; New-Item $iniDir -ItemType Directory -Force | Out-Null
$ini = Join-Path $iniDir 'PalWorldSettings.ini'
Set-Content $ini "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Route Smoke`",PublicPort=$AdvertisedPort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
$binaryFolder = if ($IsWindows) { 'Win64' } else { 'Linux' }
$defenderLogs = Join-Path $serverRoot "Pal\Binaries\$binaryFolder\PalDefender\Logs"; New-Item $defenderLogs -ItemType Directory -Force | Out-Null

& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleet; $cfg.api.Port = $Port
foreach ($s in $cfg.Servers) { $s.LaunchArguments = @("-port=$BoundPort") }
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$failures = @()
$base = "http://127.0.0.1:$Port/api/v1"
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Finding([string]$Id) { @((Invoke-RestMethod "$base/diagnostics/report" -TimeoutSec 60).findings | Where-Object { $_.id -eq $Id }) | Select-Object -First 1 }

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--steamcmd', (Join-Path $temp 'none.exe'), '--backup-root', $backups, '--runtime-root', $runtime) -WorkingDirectory (Split-Path $Exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    $up = $false
    for ($i = 0; $i -lt 80 -and -not $up; $i++) { Start-Sleep -Milliseconds 250; try { $null = Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2; $up = $true } catch {} }
    if (-not $up) { throw "the service did not start. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'the Doctor offers a fix when the server binds one port and advertises another' {
        $f = Finding 'configuration-game-port'
        if (-not $f -or $f.state -ne 1) { throw "expected a warning: $($f | ConvertTo-Json -Compress)" }
        if ($f.actionKind -ne 'align-public-port' -or -not $f.actionSupported) { throw "no fix offered: $($f.actionKind) / $($f.actionSupported)" }
        if ($f.recommendation -notmatch "PublicPort to $BoundPort" -or $f.recommendation -notmatch "-port=$AdvertisedPort") { throw "recommendation: $($f.recommendation)" }
    }

    Test-RouteSmoke 'the fix sets PublicPort to the bound port in PalWorldSettings.ini (keeping a backup), and the finding passes' {
        $r = Invoke-WebRequest "$base/diagnostics/configuration-game-port/fix" -Method Post -SkipHttpErrorCheck -TimeoutSec 60
        $body = $r.Content | ConvertFrom-Json
        if ([int]$r.StatusCode -ne 200 -or -not $body.success) { throw "fix failed: $($r.StatusCode) $($body.message)" }
        $text = Get-Content $ini -Raw
        if ($text -notmatch "PublicPort=$BoundPort\b") { throw "the ini still says: $(([regex]::Match($text, 'PublicPort=\d+')).Value)" }
        if ($text -notmatch 'ServerName="Route Smoke"') { throw 'the other settings were not kept' }
        $after = Finding 'configuration-game-port'
        if ($after.state -ne 0 -or $after.actionSupported) { throw "after the fix: $($after | ConvertTo-Json -Compress)" }
    }

    Test-RouteSmoke 'crash analysis names a PalDefender session that ended on a player joining, and nothing for a clean end' {
        $clean = Join-Path $defenderLogs '28.09 18.41.59.log'
        Set-Content $clean @(
            "[18:44:26][info] steam_7656119 ('127.0.0.1') connected to the server.",
            "[18:46:14][info] 'Wadetest' (UserId=steam_7656119, IP=127.0.0.1) has logged in.",
            "[18:47:30][info] 'Wadetest' (UserId=steam_7656119, IP=127.0.0.1) has logged out.",
            '[19:30:32][info] REST API stopped')
        $first = Invoke-RestMethod "$base/crash-analyzer/analyze" -Method Post -TimeoutSec 60
        if (@($first.findings | Where-Object { $_.title -match 'right after a player joined' }).Count -ne 0) { throw 'a clean session was reported' }

        $crashed = Join-Path $defenderLogs '28.09 18.50.00.log'
        Set-Content $crashed @(
            '[18:35:55][info] Starting PalDefender Anti Cheat v1.8.3 (console)',
            '[18:36:09][info] Running Palworld dedicated server on :8311',
            "[18:39:21][info] steam_7656119 ('127.0.0.1') connected to the server.")
        $second = Invoke-RestMethod "$base/crash-analyzer/analyze" -Method Post -TimeoutSec 60
        $hit = @($second.findings | Where-Object { $_.title -in @('Server stopped right after a player joined', 'Server session ended on a player joining') }) | Select-Object -First 1
        if (-not $hit) { throw "not reported: $(($second.findings | ForEach-Object title) -join ', ')" }
        if ($hit.severity -ne 'Critical' -or -not $hit.isNew) { throw "severity $($hit.severity), new $($hit.isNew)" }
        if (($hit.evidence -join ' ') -notmatch 'steam_7656119 connected to the server' -or ($hit.evidence -join ' ') -match '127\.0\.0\.1') { throw "evidence: $($hit.evidence -join ' | ')" }
        if (-not (@($hit.fixes) -match 'PalDefender')) { throw 'the fixes do not name PalDefender' }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(10000) | Out-Null }
}

if ($failures.Count -gt 0) { throw "MystTiq v0.9.9.0 route smoke failed: $($failures.Count) check(s)." }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'MystTiq v0.9.9.0 route smoke passed.' -ForegroundColor Green
