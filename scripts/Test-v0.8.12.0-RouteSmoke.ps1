[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18312
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.8.12.0: the second-NAT check in the WAN reachability report, on the PUBLISHED exe. What the check finds depends on the
# network this runs on (router, UPnP, ISP), so the smoke checks the contract, not a verdict:
#   1. the report has a "Second NAT (CGNAT / double NAT)" check with a real state and a reason, and a routerWanIPv4 field;
#   2. the "Outside-in test" entry says plainly that nothing here can prove outside reachability (Skipped, never Pass).
# Read-only: it looks up the public address and asks the router over UPnP; it adds no mapping. Own FleetRoot and port.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.12.0-secondnat-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $steamCmd = Join-Path $temp 'missing-steamcmd.exe'
New-Item $runtime, $backupRoot, $fleetRoot, $serverRoot -ItemType Directory -Force | Out-Null

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

    $script:report = $null
    Test-RouteSmoke 'the WAN report has a second-NAT check with a real state and a reason, and the router WAN field' {
        $script:report = Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/diagnostics/network/wan" -TimeoutSec 90
        if (-not ($script:report.PSObject.Properties.Name -contains 'routerWanIPv4')) { throw 'routerWanIPv4 is missing from the report' }
        $nat = @($script:report.checks | Where-Object { $_.test -eq 'Second NAT (CGNAT / double NAT)' })
        if ($nat.Count -ne 1) { throw "expected one second-NAT check, got $($nat.Count): $(($script:report.checks | ForEach-Object test) -join ', ')" }
        # DiagnosticState: 0 Pass, 1 Warning, 2 Fail, 4 Skipped.
        if ([int]$nat[0].state -notin 0, 1, 2, 4) { throw "unexpected state $($nat[0].state)" }
        if ([string]::IsNullOrWhiteSpace($nat[0].details)) { throw 'the check gives no reason' }
        Write-Host "      (this network: state $($nat[0].state))"
    }

    Test-RouteSmoke 'the outside-in entry says nothing here can prove outside reachability, and is never a pass' {
        $outside = @($script:report.checks | Where-Object { $_.test -eq 'Outside-in test' })
        if ($outside.Count -ne 1 -or [int]$outside[0].state -ne 4) { throw "expected one Skipped outside-in entry, got $($outside | ConvertTo-Json -Compress)" }
        if ($outside[0].details -notmatch 'cannot prove' -or $outside[0].recommendation -notmatch 'UDP') { throw "unexpected wording: $($outside[0].details) / $($outside[0].recommendation)" }
    }
}
finally {
    if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.12.0 second NAT smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.12.0 second NAT smoke gate passed." -ForegroundColor Green
