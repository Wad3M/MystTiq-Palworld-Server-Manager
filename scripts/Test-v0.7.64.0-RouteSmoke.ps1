[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18264
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.64.0: permanent live-HTTP coverage for the roadmap-audit fixes shipped this release --
# HeadlessAutomationService.ValidateTriggerAndAction now rejects invalid trigger/action values at
# the API boundary instead of silently persisting them and reinterpreting at evaluation time (the
# pure-logic scenarios live in scripts/Testing/MystTiq.LogicHarness; this checks the actual
# POST/PUT routes return 400, not just that the underlying method throws), and the Server Setup
# checklist's UE4SS row now reports ActionSupported=true when missing (v0.7.49.0 shipped a real
# install path; this row's own text/flag was stale). Same isolated-sidecar-with-no-real-PalServer
# setup as Test-v0.7.12.0-RouteSmoke.ps1/Test-v0.7.15.0-RouteSmoke.ps1. Carried forward from here
# the same way those are in every subsequent version's -RunBuild block.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.64.0-routesmoke-" + [guid]::NewGuid().ToString('N'))
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

    $createdRuleIds = [System.Collections.Generic.List[string]]::new()

    Test-RouteSmoke 'POST /automation/rules rejects a negative IdleThresholdMinutes with 400, and creates nothing' {
        $before = @((Invoke-RestMethod "$base/api/v1/automation/rules" -TimeoutSec 5))
        $body = @{
            name = 'Route smoke bad idle rule'
            trigger = @{ kind = 'IdleEmpty'; idleThresholdMinutes = -5; jitterSeconds = 0 }
            condition = @{ requireServerRunning = $false; requireServerStopped = $false }
            action = @{ kind = 'StopServer' }
        } | ConvertTo-Json -Compress -Depth 5
        try {
            Invoke-RestMethod "$base/api/v1/automation/rules" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 5 | Out-Null
            throw "expected HTTP 400 for a negative IdleThresholdMinutes, got a success response"
        }
        catch {
            if (-not $_.Exception.Message.Contains('400')) { throw "expected HTTP 400, got: $($_.Exception.Message)" }
        }
        $after = @((Invoke-RestMethod "$base/api/v1/automation/rules" -TimeoutSec 5))
        if ($after.Count -ne $before.Count) { throw "a rejected create must not persist a rule (before=$($before.Count) after=$($after.Count))" }
    }

    Test-RouteSmoke 'POST /automation/rules rejects negative JitterSeconds with 400' {
        $body = @{
            name = 'Route smoke bad jitter rule'
            trigger = @{ kind = 'DailyTime'; timeOfDayUtc = '03:00:00'; jitterSeconds = -1 }
            condition = @{ requireServerRunning = $false; requireServerStopped = $false }
            action = @{ kind = 'CreateBackup' }
        } | ConvertTo-Json -Compress -Depth 5
        try {
            Invoke-RestMethod "$base/api/v1/automation/rules" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 5 | Out-Null
            throw "expected HTTP 400 for negative JitterSeconds, got a success response"
        }
        catch {
            if (-not $_.Exception.Message.Contains('400')) { throw "expected HTTP 400, got: $($_.Exception.Message)" }
        }
    }

    Test-RouteSmoke 'POST /automation/rules accepts a valid rule and stores the values unchanged' {
        $body = @{
            name = 'Route smoke valid idle rule'
            trigger = @{ kind = 'IdleEmpty'; idleThresholdMinutes = 45; jitterSeconds = 10 }
            condition = @{ requireServerRunning = $false; requireServerStopped = $false }
            action = @{ kind = 'StopServer' }
        } | ConvertTo-Json -Compress -Depth 5
        $created = Invoke-RestMethod "$base/api/v1/automation/rules" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 5
        if ($created.trigger.idleThresholdMinutes -ne 45) { throw "expected idleThresholdMinutes=45 to round-trip unchanged, got $($created.trigger.idleThresholdMinutes)" }
        $script:createdRuleIds.Add($created.id)
    }

    Test-RouteSmoke 'PUT /automation/rules/{id} rejects a zero-or-negative Interval with 400 on an existing rule' {
        if ($createdRuleIds.Count -eq 0) { throw "no rule was created by the prior check to update" }
        $id = $createdRuleIds[0]
        $body = @{
            name = 'Route smoke valid idle rule'
            trigger = @{ kind = 'Interval'; interval = '00:00:00'; jitterSeconds = 0 }
            condition = @{ requireServerRunning = $false; requireServerStopped = $false }
            action = @{ kind = 'StopServer' }
        } | ConvertTo-Json -Compress -Depth 5
        try {
            Invoke-RestMethod "$base/api/v1/automation/rules/$id" -Method Put -ContentType 'application/json' -Body $body -TimeoutSec 5 | Out-Null
            throw "expected HTTP 400 for a zero Interval, got a success response"
        }
        catch {
            if (-not $_.Exception.Message.Contains('400')) { throw "expected HTTP 400, got: $($_.Exception.Message)" }
        }
        # The rule must still exist, unchanged, after a rejected update.
        $rules = @((Invoke-RestMethod "$base/api/v1/automation/rules" -TimeoutSec 5))
        $still = $rules | Where-Object { $_.id -eq $id }
        if (-not $still) { throw "the existing rule must survive a rejected update" }
        if ($still.trigger.kind -ne 'IdleEmpty') { throw "a rejected update must not have changed the rule's trigger kind" }
    }

    Test-RouteSmoke 'GET /server/environment reports the UE4SS row as action-supported' {
        $result = Invoke-RestMethod "$base/api/v1/server/environment" -TimeoutSec 5
        $row = @($result.items) | Where-Object { $_.component -eq 'UE4SS Runtime' }
        if (-not $row) { throw "UE4SS Runtime row not found in the checklist response" }
        if ($row.actionSupported -ne $true) { throw "expected UE4SS Runtime's actionSupported=true (v0.7.49.0 shipped a real install path), got $($row.actionSupported)" }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.64.0 route smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.64.0 route smoke gate passed." -ForegroundColor Green
