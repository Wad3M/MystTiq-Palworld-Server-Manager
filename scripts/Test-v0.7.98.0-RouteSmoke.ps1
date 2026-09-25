[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18298
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.98.0: live-HTTP coverage for the Doctor's new resource, backup, security and stability
# findings, through the same /diagnostics/report route the Doctor page and the Dashboard badge read.
# Isolated sidecar over a fixture server root: a world save, a weak admin password with RCON on, no
# backups yet, and a log with an out-of-memory line. Runs against the PUBLISHED headless exe, so
# publish first.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.98.0-doctor-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
$weakPassword = 'letmein'
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') @"
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Doctor Smoke",ServerDescription="",AdminPassword="$weakPassword",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=True,RCONPort=25575,DayTimeSpeedRate=1.000000)
"@
$saveRoot = Join-Path $serverRoot 'Pal\Saved\SaveGames\0\DoctorSmokeWorld'
New-Item $saveRoot -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $saveRoot 'Level.sav') 'doctor-smoke-save'
$palLogs = Join-Path $serverRoot 'Pal\Saved\Logs'
New-Item $palLogs -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $palLogs 'PalServer.log') @('Server initialized successfully', '[2026.09.21-08.10.00:000] Out of memory while allocating 4096 bytes')
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

# The enum may serialise as a name or a number; normalise so the checks read the same either way.
function Get-StateName($value) {
    $names = @('Pass', 'Warning', 'Fail', 'Starting', 'Skipped', 'Unknown')
    if ($value -is [string] -and $value -notmatch '^\d+$') { return $value }
    return $names[[int]$value]
}
function Get-Finding($report, [string]$id) { @($report.findings | Where-Object { $_.id -eq $id })[0] }

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

    $script:report = $null
    Test-RouteSmoke 'the report carries the new resource, backup, security and stability findings' {
        $script:report = Invoke-RestMethod "$base/api/v1/diagnostics/report" -TimeoutSec 60
        foreach ($id in 'resources-memory', 'backups-freshness', 'security-admin-access', 'stability-recent-crashes') {
            if (-not (Get-Finding $script:report $id)) { throw "expected a finding with id '$id'" }
        }
        $disk = @($script:report.findings | Where-Object { $_.id -like 'resources-disk-space-*' })
        if ($disk.Count -lt 1) { throw 'expected at least one disk space finding' }
        if ($disk[0].evidence -notmatch 'GiB free of') { throw "disk finding should report real GiB figures, got: $($disk[0].evidence)" }
    }

    Test-RouteSmoke 'a common admin password with RCON on is a warning, and the password is never in the report' {
        $f = Get-Finding $script:report 'security-admin-access'
        if ((Get-StateName $f.state) -ne 'Warning') { throw "expected Warning, got $(Get-StateName $f.state): $($f.evidence)" }
        $whole = $script:report | ConvertTo-Json -Depth 10 -Compress
        if ($whole -match $weakPassword) { throw 'the admin password appeared in the diagnostics report' }
    }

    Test-RouteSmoke 'a world with no backup is a warning, and creating one clears it' {
        $f = Get-Finding $script:report 'backups-freshness'
        if ((Get-StateName $f.state) -ne 'Warning') { throw "expected Warning before any backup, got $(Get-StateName $f.state): $($f.evidence)" }
        $made = Invoke-RestMethod "$base/api/v1/backups/create" -Method Post -TimeoutSec 60
        if ($made.success -ne $true) { throw "backup creation failed: $($made.message)" }
        $after = Invoke-RestMethod "$base/api/v1/diagnostics/report" -TimeoutSec 60
        $g = Get-Finding $after 'backups-freshness'
        if ((Get-StateName $g.state) -ne 'Pass') { throw "expected Pass after a fresh backup, got $(Get-StateName $g.state): $($g.evidence)" }
    }

    Test-RouteSmoke 'an unreviewed critical crash finding warns, and analysing again marks it reviewed' {
        $before = Get-Finding $script:report 'stability-recent-crashes'
        if ((Get-StateName $before.state) -ne 'Skipped') { throw "expected Skipped before any analysis, got $(Get-StateName $before.state)" }
        $null = Invoke-RestMethod "$base/api/v1/crash-analyzer/analyze" -Method Post -TimeoutSec 30
        $warned = Get-Finding (Invoke-RestMethod "$base/api/v1/diagnostics/report" -TimeoutSec 60) 'stability-recent-crashes'
        if ((Get-StateName $warned.state) -ne 'Warning' -or $warned.evidence -notmatch 'Out of memory') { throw "expected a Warning naming Out of memory, got $(Get-StateName $warned.state): $($warned.evidence)" }
        $null = Invoke-RestMethod "$base/api/v1/crash-analyzer/analyze" -Method Post -TimeoutSec 30
        $cleared = Get-Finding (Invoke-RestMethod "$base/api/v1/diagnostics/report" -TimeoutSec 60) 'stability-recent-crashes'
        if ((Get-StateName $cleared.state) -ne 'Pass') { throw "expected Pass after the finding was reviewed, got $(Get-StateName $cleared.state): $($cleared.evidence)" }
    }

    Test-RouteSmoke 'the new findings count toward the report totals the Dashboard badge uses' {
        $r = Invoke-RestMethod "$base/api/v1/diagnostics/report" -TimeoutSec 60
        $states = @($r.findings | Where-Object { $_.category -ne 'Identity' } | ForEach-Object { Get-StateName $_.state })
        $warn = @($states | Where-Object { $_ -eq 'Warning' }).Count
        if ([int]$r.warnings -ne $warn) { throw "report.warnings=$($r.warnings) but $warn health-relevant findings are Warning" }
        if ([int]$r.warnings -lt 1) { throw 'the weak admin password should still leave at least one warning' }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.98.0 doctor smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.98.0 doctor smoke gate passed." -ForegroundColor Green
