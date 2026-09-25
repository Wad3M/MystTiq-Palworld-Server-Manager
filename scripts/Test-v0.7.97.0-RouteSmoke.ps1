[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18297
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.97.0: live-HTTP coverage for the Crash Analyzer's known-signature analysis. Same isolated
# sidecar pattern as the other route smokes, pointed at a fresh server root whose PalServer.log is a
# fixture with a mix of real failure lines and lines that used to be mislabelled (the word "room"
# matched the old "oom" needle). It runs against the PUBLISHED headless exe, so publish first.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.97.0-crashanalyzer-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$palLogs = Join-Path $serverRoot 'Pal\Saved\Logs'
New-Item $palLogs -ItemType Directory -Force | Out-Null
$logFile = Join-Path $palLogs 'PalServer.log'
Set-Content $logFile @(
    '[2026.09.21-08.00.00:000] LogPal: Player entered the room',
    'UE4SS: Loaded mod FixtureMod',
    '[2026.09.21-08.10.00:000] Fatal error: test-only crash',
    '[2026-09-21 08:11:00.000] Server session #2 process exited with code -1073741819',
    'Out of memory while allocating 4096 bytes',
    '[2026-09-21 08:12:00.000] Server session #3 process exited with code 0'
)
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
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err
    $base = "http://127.0.0.1:$Port"
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Sidecar did not become healthy. stdout=$(Get-Content $log -Raw -ErrorAction SilentlyContinue) stderr=$(Get-Content $err -Raw -ErrorAction SilentlyContinue)" }

    $script:first = $null
    Test-RouteSmoke 'first analysis returns known signatures with causes and fixes, and never labels ordinary words' {
        $r = Invoke-RestMethod "$base/api/v1/crash-analyzer/analyze" -Method Post -TimeoutSec 20
        $script:first = $r
        $ids = @($r.findings | ForEach-Object { $_.signatureId } | Sort-Object)
        $expected = @('access-violation', 'out-of-memory', 'ue-fatal')
        if (($ids -join ',') -ne ($expected -join ',')) { throw "expected signatures $($expected -join ', '), got: $($ids -join ', ')" }
        foreach ($f in $r.findings) {
            if ([string]::IsNullOrWhiteSpace($f.cause) -or @($f.fixes).Count -lt 1) { throw "finding '$($f.title)' has no cause or fixes" }
            if ($f.isNew -ne $true) { throw "finding '$($f.title)' should be new on the first analysis" }
            if ([string]::IsNullOrWhiteSpace($f.key)) { throw "finding '$($f.title)' has no key" }
        }
        if ([int]$r.newFindings -ne 3 -or [int]$r.repeatedFindings -ne 0) { throw "expected 3 new / 0 repeated, got $($r.newFindings) / $($r.repeatedFindings)" }
    }

    Test-RouteSmoke 'the exit code -1073741819 line is read as an access violation and exit code 0 is ignored' {
        $av = @($script:first.findings | Where-Object { $_.signatureId -eq 'access-violation' })
        if ($av.Count -ne 1 -or [int]$av[0].matchCount -ne 1) { throw 'expected exactly one access-violation match (from the exit code line)' }
        if ($null -eq $av[0].lastSeen) { throw 'expected a lastSeen time from the stamped exit-code line' }
    }

    Test-RouteSmoke 'analysing the same logs again reports every finding as already reported' {
        $r = Invoke-RestMethod "$base/api/v1/crash-analyzer/analyze" -Method Post -TimeoutSec 20
        if (@($r.findings).Count -ne 3) { throw "expected the same 3 findings, got $(@($r.findings).Count)" }
        if (@($r.findings | Where-Object { $_.isNew }).Count -ne 0) { throw 'no finding should be new the second time' }
        if ([int]$r.newFindings -ne 0 -or [int]$r.repeatedFindings -ne 3) { throw "expected 0 new / 3 repeated, got $($r.newFindings) / $($r.repeatedFindings)" }
    }

    Test-RouteSmoke 'a genuinely new crash line makes only its own signature new again' {
        Add-Content $logFile '[2026.09.21-09.00.00:000] Fatal error: a second, different crash'
        $r = Invoke-RestMethod "$base/api/v1/crash-analyzer/analyze" -Method Post -TimeoutSec 20
        $new = @($r.findings | Where-Object { $_.isNew })
        if ($new.Count -ne 1 -or $new[0].signatureId -ne 'ue-fatal') { throw "expected only ue-fatal to be new, got: $(($new | ForEach-Object { $_.signatureId }) -join ', ')" }
        if ($r.findings[0].signatureId -ne 'ue-fatal') { throw 'the new finding should sort first' }
    }

    Test-RouteSmoke 'history keeps the reports and carries the new fields through a reload' {
        # Assign first, then wrap: wrapping the call directly nests the array in this PowerShell.
        $history = Invoke-RestMethod "$base/api/v1/crash-analyzer/history" -TimeoutSec 10
        $h = @($history)
        if ($h.Count -ne 3) { throw "expected the 3 analyses run above to be persisted, got $($h.Count)" }
        $newest = $h[0]
        if ([int]$newest.newFindings -ne 1) { throw "newest report should record 1 new finding, got $($newest.newFindings)" }
        if (-not (@($newest.findings | Where-Object { -not [string]::IsNullOrWhiteSpace($_.cause) }).Count -eq 3)) { throw 'persisted findings lost their cause text' }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.97.0 crash analyzer smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.97.0 crash analyzer smoke gate passed." -ForegroundColor Green
