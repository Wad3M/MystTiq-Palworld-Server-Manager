[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18320
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v0.8.20.0: the HOST tab's history on the published exe: a reading is taken when MystTiq starts and then every minute,
# kept in the fleet folder, served thinned with averages and peaks, and kept across a restart of MystTiq. Own FleetRoot
# and port; nothing touches real servers.
if (-not $Exe) {
    $Exe = if ($IsWindows) { Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' } else { Join-Path $root 'artifacts/publish/linux-x64/mysttiq-server' }
}
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Headless sidecar not found: $Exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.20.0-history-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'
New-Item $runtime, $serverRoot, $backupRoot -ItemType Directory -Force | Out-Null
$failures = @()
$proc = $null
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Start-Sidecar {
    $script:proc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--steamcmd', (Join-Path $temp 'none.exe'), '--backup-root', $backupRoot, '--runtime-root', $runtime) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'headless.log') -RedirectStandardError (Join-Path $temp 'headless.err.log')
    for ($i = 0; $i -lt 60; $i++) { Start-Sleep -Milliseconds 250; try { if (Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2) { return } } catch {} }
    throw 'the sidecar did not become healthy'
}
function Get-HostHistory([double]$Hours) { Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/host/history?hours=$Hours" -TimeoutSec 15 }
function Wait-Readings([int]$Count) {
    for ($i = 0; $i -lt 40; $i++) { $h = Get-HostHistory 1; if ($h.readingsInRange -ge $Count) { return $h }; Start-Sleep -Milliseconds 500 }
    throw "expected at least $Count readings, got $((Get-HostHistory 1).readingsInRange)"
}

try {
    & $Exe config-write-default --config $config --overwrite | Out-Null
    $cfg = Get-Content $config -Raw | ConvertFrom-Json
    $cfg.FleetRoot = $fleetRoot; $cfg.api.Port = $Port
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $config
    Start-Sidecar

    Test-RouteSmoke 'a reading is taken when MystTiq starts, with real processor and memory values' {
        $h = Wait-Readings 1
        $s = @($h.samples)[0]
        if ($null -eq $s.cpuPercent -or $s.cpuPercent -lt 0 -or $s.cpuPercent -gt 100) { throw "processor $($s.cpuPercent)" }
        if ($s.memoryUsedPercent -le 0 -or $s.memoryUsedPercent -gt 100) { throw "memory $($s.memoryUsedPercent)" }
        if ($h.peakCpuPercent -ne $s.cpuPercent -or -not $h.firstReadingAt) { throw 'the summary does not match the one reading' }
    }

    Test-RouteSmoke 'the history is kept in the fleet folder' {
        $file = Join-Path $fleetRoot 'host\history.json'
        if (-not (Test-Path $file)) { throw "no $file" }
        if (@(Get-Content $file -Raw | ConvertFrom-Json).Count -lt 1) { throw 'the file holds no reading' }
    }

    Test-RouteSmoke 'ranges are clamped to 1 hour .. 7 days' {
        if ((Get-HostHistory 0).rangeHours -ne 1 -or (Get-HostHistory 100000).rangeHours -ne 168) { throw "ranges $((Get-HostHistory 0).rangeHours) / $((Get-HostHistory 100000).rangeHours)" }
    }

    Test-RouteSmoke 'the history survives a restart of MystTiq (and a new reading is added at start)' {
        Stop-Process -Id $proc.Id -Force; $proc.WaitForExit(5000) | Out-Null
        Start-Sidecar
        $h = Wait-Readings 2
        if ($h.readingsInRange -lt 2) { throw "readings after restart: $($h.readingsInRange)" }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) { throw "MystTiq v0.8.20.0 host history smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')" }
Write-Host "MystTiq v0.8.20.0 host history smoke gate passed." -ForegroundColor Green
