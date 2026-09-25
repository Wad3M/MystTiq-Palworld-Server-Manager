[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18240
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.12.0: closes a real gap found by a test-coverage audit -- every route added in v0.7.8.0
# (Unban/ban-list/teleport-to-me/teleport-to-player/save-now) and v0.7.10.0 (whitelist GET/PUT) had
# been verified only by one-off manual live checks during development, with nothing in the standard
# release pipeline that would catch a regression later. This is a permanent addition to the pipeline
# (called from Test-v0.7.12.0-Logic.ps1's -RunBuild, and should be carried forward the same way
# Test-v0.5.1.5-RuntimeSmoke.ps1 already is in every subsequent version's -RunBuild block).
# Same isolated-sidecar-with-no-real-PalServer setup as Test-v0.5.1.5-RuntimeSmoke.ps1: these routes
# are checked for reachability and correct graceful-failure shape (RCON/REST genuinely unconfigured
# in this environment), not for their real in-game effect against an actual Palworld server -- that
# remains a disclosed gap, same as every RCON/REST-touching feature shipped this session.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.12.0-routesmoke-" + [guid]::NewGuid().ToString('N'))
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
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $missingServer, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err
    $base = "http://127.0.0.1:$Port"
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Sidecar did not become healthy. stdout=$(Get-Content $log -Raw -ErrorAction SilentlyContinue) stderr=$(Get-Content $err -Raw -ErrorAction SilentlyContinue)" }

    # v0.7.12.0: these all correctly return HTTP 409 (RCON genuinely unconfigured in this isolated
    # test environment) -- Invoke-RestMethod throws on non-2xx, so the graceful-failure body is read
    # from $_.ErrorDetails.Message in the catch block rather than from a direct return value.
    Test-RouteSmoke 'GET /players/ban-list is reachable and fails gracefully with no RCON configured' {
        try { Invoke-RestMethod "$base/api/v1/players/ban-list" -TimeoutSec 5 | Out-Null; throw "expected a 409 Conflict with no PalWorldSettings.ini present" }
        catch {
            if ([string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) { throw }
            $body = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($body.success) { throw "expected success=false" }
            if ([string]::IsNullOrWhiteSpace($body.message)) { throw "expected a non-empty explanatory message" }
        }
    }

    Test-RouteSmoke 'POST /world/save-now is reachable and fails gracefully with no RCON configured' {
        try { Invoke-RestMethod "$base/api/v1/world/save-now" -Method Post -TimeoutSec 5 | Out-Null; throw "expected a 409 Conflict with no PalWorldSettings.ini present" }
        catch {
            if ([string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) { throw }
            $body = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($body.command -ne 'Save') { throw "expected the RCON command text to be exactly 'Save', got '$($body.command)'" }
        }
    }

    Test-RouteSmoke 'POST /players/{id}/teleport-to-me builds the correct RCON command and fails gracefully' {
        try { Invoke-RestMethod "$base/api/v1/players/smoke-test-id/teleport-to-me" -Method Post -TimeoutSec 5 | Out-Null; throw "expected a 409 Conflict with no PalWorldSettings.ini present" }
        catch {
            if ([string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) { throw }
            $body = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($body.command -ne 'TeleportToMe smoke-test-id') { throw "expected command 'TeleportToMe smoke-test-id', got '$($body.command)'" }
        }
    }

    Test-RouteSmoke 'POST /players/{id}/teleport-to-player builds the correct RCON command and fails gracefully' {
        try { Invoke-RestMethod "$base/api/v1/players/smoke-test-id/teleport-to-player" -Method Post -TimeoutSec 5 | Out-Null; throw "expected a 409 Conflict with no PalWorldSettings.ini present" }
        catch {
            if ([string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) { throw }
            $body = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($body.command -ne 'TeleportToPlayer smoke-test-id') { throw "expected command 'TeleportToPlayer smoke-test-id', got '$($body.command)'" }
        }
    }

    Test-RouteSmoke 'POST /players/{id}/action with action=unban resolves to the rcon provider, not the REST-only admin provider' {
        $requestBody = @{ action = 'unban' } | ConvertTo-Json -Compress
        try {
            Invoke-RestMethod "$base/api/v1/players/smoke-test-id/action" -Method Post -ContentType 'application/json' -Body $requestBody -TimeoutSec 5 | Out-Null
            throw "expected a non-success HTTP status (RCON unconfigured)"
        }
        catch {
            if ([string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) { throw }
            $payload = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($payload.providerId -ne 'rcon') { throw "expected providerId='rcon' for an unban action, got '$($payload.providerId)'" }
        }
    }

    Test-RouteSmoke 'GET /players/whitelist returns the correct disabled-empty default' {
        $result = Invoke-RestMethod "$base/api/v1/players/whitelist" -TimeoutSec 5
        if ($result.enabled) { throw "expected a fresh whitelist to default to disabled" }
        if (@($result.entries).Count -ne 0) { throw "expected a fresh whitelist to have no entries" }
    }

    Test-RouteSmoke 'PUT /players/whitelist persists a new config, and a subsequent GET reflects it' {
        $body = @{ enabled = $true; entries = @(@{ playerId = 'route-smoke-player'; label = 'Route Smoke' }) } | ConvertTo-Json -Compress
        $saved = Invoke-RestMethod "$base/api/v1/players/whitelist" -Method Put -ContentType 'application/json' -Body $body -TimeoutSec 5
        if (-not $saved.enabled) { throw "expected the saved config to report enabled=true" }
        if (@($saved.entries).Count -ne 1 -or $saved.entries[0].playerId -ne 'route-smoke-player') { throw "expected exactly one persisted entry for route-smoke-player" }

        $reread = Invoke-RestMethod "$base/api/v1/players/whitelist" -TimeoutSec 5
        if (-not $reread.enabled -or @($reread.entries).Count -ne 1) { throw "a subsequent GET did not reflect the saved config -- persistence is not real" }
    }

    Test-RouteSmoke 'GET /metrics includes the v0.7.9.0 serverFps/serverFrameTimeMs fields' {
        $result = Invoke-RestMethod "$base/api/v1/metrics" -TimeoutSec 5
        if (-not ($result.PSObject.Properties.Name -contains 'serverFps')) { throw "response is missing the serverFps field" }
        if (-not ($result.PSObject.Properties.Name -contains 'serverFrameTimeMs')) { throw "response is missing the serverFrameTimeMs field" }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.12.0 route smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.12.0 route smoke gate passed." -ForegroundColor Green
