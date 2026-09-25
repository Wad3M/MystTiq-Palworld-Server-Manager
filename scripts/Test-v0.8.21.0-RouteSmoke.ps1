[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18321
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v0.8.21.0: --fleet-root on the published exe. The config file is left with the default (real) FleetRoot on purpose:
# the option alone must keep the fleet's shared state in the given folder, without writing that choice back to the
# file. Own port; nothing touches real servers.
if (-not $Exe) {
    $Exe = if ($IsWindows) { Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' } else { Join-Path $root 'artifacts/publish/linux-x64/mysttiq-server' }
}
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Headless sidecar not found: $Exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.21.0-fleetroot-" + [guid]::NewGuid().ToString('N'))
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
$common = @('--config', $config, '--server-root', $serverRoot, '--steamcmd', (Join-Path $temp 'none.exe'), '--backup-root', $backupRoot, '--runtime-root', $runtime)

try {
    & $Exe config-write-default --config $config --overwrite | Out-Null
    $defaultFleetRoot = (Get-Content $config -Raw | ConvertFrom-Json).FleetRoot
    $configBefore = Get-Content $config -Raw

    $script:proc = Start-Process -FilePath $Exe -ArgumentList (@('api-run', '--desktop-sidecar', '--api-port', "$Port", '--fleet-root', $fleetRoot) + $common) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'headless.log') -RedirectStandardError (Join-Path $temp 'headless.err.log')
    $healthy = $false
    for ($i = 0; $i -lt 60 -and -not $healthy; $i++) { Start-Sleep -Milliseconds 250; try { if (Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2) { $healthy = $true } } catch {} }
    if (-not $healthy) { throw 'the sidecar did not become healthy' }

    Test-RouteSmoke 'the fleet state goes to the --fleet-root folder (host history written there)' {
        $file = Join-Path $fleetRoot 'host\history.json'
        for ($i = 0; $i -lt 40 -and -not (Test-Path $file); $i++) { Start-Sleep -Milliseconds 250 }
        if (-not (Test-Path $file)) { throw "no $file" }
        if ((Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/host/history?hours=1" -TimeoutSec 15).readingsInRange -lt 1) { throw 'the route has no reading' }
    }

    Test-RouteSmoke 'the option is not written back: the config file still names the default FleetRoot' {
        if ([string]::IsNullOrWhiteSpace($defaultFleetRoot) -or $defaultFleetRoot -eq $fleetRoot) { throw "default FleetRoot: $defaultFleetRoot" }
        if ((Get-Content $config -Raw) -ne $configBefore) { throw 'the config file changed' }
    }

    Test-RouteSmoke 'a relative --fleet-root is refused before anything starts' {
        $out = & $Exe config-validate --fleet-root 'relative\fleet' @common 2>&1 | Out-String
        $code = $LASTEXITCODE
        if ($code -eq 0 -or $out -notmatch 'fleetRoot') { throw "exit $code, output: $out" }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) { throw "MystTiq v0.8.21.0 fleet-root smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')" }
Write-Host "MystTiq v0.8.21.0 fleet-root smoke gate passed." -ForegroundColor Green
exit 0
