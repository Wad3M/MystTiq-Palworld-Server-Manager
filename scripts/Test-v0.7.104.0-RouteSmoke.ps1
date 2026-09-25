[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18304
)
$ErrorActionPreference = 'Stop'
# The logic gate runs every smoke under strict mode, where a single object has no Count property. Running
# strict here too means a standalone run reproduces the gate instead of passing where the gate would fail.
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.104.0: a pinned Critical alert used to stay pinned forever -- even once its own "Resolved" notice
# had already been sent, or, worse, once the rule was switched off (which sends no notice at all). Live
# coverage over the same low-disk fixture v0.7.102.0's smoke uses: the alert unpins itself once recovered,
# and unpins itself when the rule is switched off mid-episode too, without inventing a fake recovery notice
# for that second case. Runs against the PUBLISHED headless exe.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$failures = @()

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

function New-Fixture([string]$tag) {
    $temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.104.0-unpin-$tag-" + [guid]::NewGuid().ToString('N'))
    $runtime = Join-Path $temp 'runtime'
    $serverRoot = Join-Path $temp 'server'
    $backupRoot = Join-Path $temp 'backups'
    $configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
    New-Item $runtime -ItemType Directory -Force | Out-Null
    New-Item $backupRoot -ItemType Directory -Force | Out-Null
    New-Item $configDir -ItemType Directory -Force | Out-Null
    Set-Content (Join-Path $configDir 'PalWorldSettings.ini') @'
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Unpin Smoke",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=False,RCONPort=25575,DayTimeSpeedRate=1.000000)
'@
    [pscustomobject]@{ Temp = $temp; Config = (Join-Path $temp 'mysttiq.json'); Runtime = $runtime; ServerRoot = $serverRoot; SteamCmd = (Join-Path $temp 'missing-steamcmd.exe'); BackupRoot = $backupRoot }
}

function Start-Sidecar($fixture, [int]$port) {
    $log = Join-Path $fixture.Temp 'headless.log'; $err = Join-Path $fixture.Temp 'headless.err.log'
    $args = @('api-run', '--desktop-sidecar', '--config', $fixture.Config, '--fleet-root', (Join-Path $fixture.Temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$port", '--server-root', $fixture.ServerRoot, '--steamcmd', $fixture.SteamCmd, '--backup-root', $fixture.BackupRoot, '--runtime-root', $fixture.Runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err -Environment @{ MYSTTIQ_ALERT_EVAL_SECONDS = '2' }
    $base = "http://127.0.0.1:$port"
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { return [pscustomobject]@{ Proc = $proc; Base = $base } } } catch {}
    }
    throw "Sidecar did not become healthy. $(Get-Content $err -Raw -ErrorAction SilentlyContinue)"
}
function Stop-Sidecar($sidecar) { if ($sidecar -and -not $sidecar.Proc.HasExited) { Stop-Process -Id $sidecar.Proc.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 800 } }

function Get-LowDiskAlerts([string]$base) {
    $snapshot = Invoke-RestMethod "$base/api/v1/notifications" -TimeoutSec 5
    foreach ($n in $snapshot.items) { if ($n.title -eq 'Low disk space') { $n } }
}
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { if (& $Condition) { return $true }; Start-Sleep -Milliseconds 700 }
    return $false
}
function Set-LowDiskPercent([string]$base, [double]$percent) {
    $rules = Invoke-RestMethod "$base/api/v1/alerts/rules" -TimeoutSec 5
    $rules.lowDiskSpace.thresholdPercent = $percent
    $null = Invoke-RestMethod "$base/api/v1/alerts/rules" -Method Put -ContentType 'application/json' -Body ($rules | ConvertTo-Json -Depth 6) -TimeoutSec 5
}
function Set-LowDiskEnabled([string]$base, [bool]$enabled) {
    $rules = Invoke-RestMethod "$base/api/v1/alerts/rules" -TimeoutSec 5
    $rules.lowDiskSpace.enabled = $enabled
    $null = Invoke-RestMethod "$base/api/v1/alerts/rules" -Method Put -ContentType 'application/json' -Body ($rules | ConvertTo-Json -Depth 6) -TimeoutSec 5
}

$sidecarA = $null
$sidecarB = $null
try {
    $fixtureA = New-Fixture 'recover'
    $sidecarA = Start-Sidecar $fixtureA $Port
    $baseA = $sidecarA.Base

    Test-RouteSmoke 'a pinned alert unpins itself once the condition clears (and stays unpinned)' {
        Set-LowDiskPercent $baseA 99
        if (-not (Wait-Until { @(Get-LowDiskAlerts $baseA).Count -ge 1 } 40)) { throw 'no low-disk alert arrived after the condition became true' }
        $alert = @(Get-LowDiskAlerts $baseA)[0]
        if ($alert.severity -ne 'Critical' -or -not $alert.pinned) { throw "expected a pinned Critical alert, got $($alert.severity) pinned=$($alert.pinned)" }
        $alertId = $alert.id

        Set-LowDiskPercent $baseA 1
        if (-not (Wait-Until { @((Invoke-RestMethod "$baseA/api/v1/notifications").items | Where-Object { $_.title -eq 'Resolved: Low disk space' }).Count -ge 1 } 40)) {
            throw 'no recovery notice arrived after the condition cleared'
        }
        if (-not (Wait-Until { -not (@(Get-LowDiskAlerts $baseA) | Where-Object { $_.id -eq $alertId }).pinned } 20)) { throw 'the original alert did not unpin itself after recovery' }
        $stillThere = @(Get-LowDiskAlerts $baseA) | Where-Object { $_.id -eq $alertId }
        if ($stillThere.Count -ne 1) { throw 'the original alert notification should still exist, just unpinned' }
        if ($stillThere[0].pinned) { throw 'the original alert is still pinned after recovery' }
        $resolved = @((Invoke-RestMethod "$baseA/api/v1/notifications").items | Where-Object { $_.title -eq 'Resolved: Low disk space' })
        if ($resolved[0].pinned) { throw 'the Resolved notice itself should not be pinned' }
    }
}
finally { Stop-Sidecar $sidecarA }

try {
    $fixtureB = New-Fixture 'disable'
    $sidecarB = Start-Sidecar $fixtureB ($Port + 1)
    $baseB = $sidecarB.Base

    Test-RouteSmoke 'switching the rule off while an alert is pinned unpins it too, with no fabricated Resolved notice' {
        Set-LowDiskPercent $baseB 99
        if (-not (Wait-Until { @(Get-LowDiskAlerts $baseB).Count -ge 1 } 40)) { throw 'no low-disk alert arrived after the condition became true' }
        $alert = @(Get-LowDiskAlerts $baseB)[0]
        if (-not $alert.pinned) { throw 'expected the alert to be pinned before the rule is switched off' }
        $alertId = $alert.id

        Set-LowDiskEnabled $baseB $false
        if (-not (Wait-Until { -not (@(Get-LowDiskAlerts $baseB) | Where-Object { $_.id -eq $alertId }).pinned } 20)) { throw 'the alert did not unpin itself after the rule was switched off' }
        Start-Sleep -Seconds 5
        $resolved = @((Invoke-RestMethod "$baseB/api/v1/notifications").items | Where-Object { $_.title -eq 'Resolved: Low disk space' })
        if ($resolved.Count -ne 0) { throw 'switching a rule off must not fabricate a Resolved notice; only the pin should clear' }
        $stillThere = @(Get-LowDiskAlerts $baseB) | Where-Object { $_.id -eq $alertId }
        if ($stillThere.Count -ne 1 -or $stillThere[0].pinned) { throw 'the alert should still exist, unpinned, with the record kept' }
    }
}
finally { Stop-Sidecar $sidecarB }

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.104.0 alert unpin smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.104.0 alert unpin smoke gate passed." -ForegroundColor Green
