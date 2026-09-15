[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18250
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.15.0: permanent coverage for the two backlog items shipped this release --
# HeadlessTemporaryBanService's new routes and HeadlessHistoricalMetricsService's new FPS fields.
# Same isolated-sidecar-with-no-real-PalServer setup as Test-v0.5.1.5-RuntimeSmoke.ps1/
# Test-v0.7.12.0-RouteSmoke.ps1: checked for reachability and correct graceful-failure shape (RCON
# genuinely unconfigured in this environment), not real in-game effect against an actual Palworld
# server -- that remains a disclosed gap, same as every RCON/REST-touching feature shipped this
# session. Carried forward from here the same way Test-v0.5.1.5-RuntimeSmoke.ps1 and
# Test-v0.7.12.0-RouteSmoke.ps1 already are in every subsequent version's -RunBuild block.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.15.0-routesmoke-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$missingServer = Join-Path $temp 'missing-server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $missingServer -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$saveRoot = Join-Path $missingServer 'Pal\Saved\SaveGames\0\RouteSmokeWorld'
New-Item $saveRoot -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $saveRoot 'Level.sav') 'route-smoke-save'
$log = Join-Path $temp 'headless.log'
$err = Join-Path $temp 'headless.err.log'
$proc = $null
$failures = @()

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green
    }
    catch {
        Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red
        $script:failures += $Name
    }
}

try {
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $missingServer, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err
    $base = "http://127.0.0.1:$Port"
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Sidecar did not become healthy. stdout=$(Get-Content $log -Raw -ErrorAction SilentlyContinue) stderr=$(Get-Content $err -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'GET /players/temp-bans returns the correct empty default' {
        $result = Invoke-RestMethod "$base/api/v1/players/temp-bans" -TimeoutSec 5
        if (@($result.entries).Count -ne 0) { throw "expected a fresh temp-ban list to have no entries" }
    }

    Test-RouteSmoke 'POST /players/{id}/temp-ban is reachable and fails gracefully with no RCON configured, without persisting an entry' {
        $body = @{ playerName = 'RouteSmokePlayer'; reason = 'Route smoke'; durationHours = 1 } | ConvertTo-Json -Compress
        try {
            Invoke-RestMethod "$base/api/v1/players/smoke-test-id/temp-ban" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 5 | Out-Null
            throw "expected a non-success HTTP status (RCON unconfigured)"
        }
        catch {
            if ([string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) { throw }
            $payload = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($payload.success) { throw "expected success=false" }
            if ($payload.action -ne 'ban') { throw "expected the underlying action to be 'ban', got '$($payload.action)'" }
        }

        # The ban itself failed, so HeadlessTemporaryBanService.BanAsync must not have persisted an
        # expiry entry -- confirming the "only track it if the ban actually succeeded" guard works.
        $reread = Invoke-RestMethod "$base/api/v1/players/temp-bans" -TimeoutSec 5
        if (@($reread.entries).Count -ne 0) { throw "a failed temp-ban should not have persisted an entry, but one exists" }
    }

    Test-RouteSmoke 'GET /history includes the v0.7.15.0 averageFps/peakFps fields, null with no samples yet' {
        $result = Invoke-RestMethod "$base/api/v1/history?hours=1" -TimeoutSec 5
        if (-not ($result.PSObject.Properties.Name -contains 'averageFps')) { throw "response is missing the averageFps field" }
        if (-not ($result.PSObject.Properties.Name -contains 'peakFps')) { throw "response is missing the peakFps field" }
        if ($null -ne $result.averageFps -or $null -ne $result.peakFps) { throw "expected null averageFps/peakFps with zero samples recorded, got averageFps=$($result.averageFps) peakFps=$($result.peakFps)" }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.15.0 route smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.15.0 route smoke gate passed." -ForegroundColor Green
