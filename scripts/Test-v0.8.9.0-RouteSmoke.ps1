[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18389,
    [int]$GamePort = 18489
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.8.9.0: Unreal crash reports in the Crash Analyzer, on the PUBLISHED exe, with synthetic crash report folders shaped like
# the real ones (FGenericCrashContext/RuntimeProperties) under the fixture server's Pal\Saved\Crashes:
#   1. a report already on disk when MystTiq starts is recorded silently (no alert);
#   2. the analyze route reads it and classifies it (the TArray engine check has its own signature);
#   3. a new report written while MystTiq runs is announced exactly once, naming the report and its finding;
#   4. while alerts are muted a new report is held back, and announced once the mute is lifted.
# Own FleetRoot and ports; nothing touches a real server.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.9.0-crashreports-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $steamCmd = Join-Path $temp 'missing-steamcmd.exe'
New-Item $runtime, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Crash Smoke`",AdminPassword=`"a-long-random-admin-secret`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False)"
$crashes = Join-Path $serverRoot 'Pal\Saved\Crashes'

function Write-Report([string]$Folder, [string]$ErrorMessage, [string]$Type = 'Crash') {
    $dir = Join-Path $crashes $Folder
    New-Item $dir -ItemType Directory -Force | Out-Null
    $escaped = [System.Security.SecurityElement]::Escape($ErrorMessage)
    Set-Content (Join-Path $dir 'CrashContext.runtime-xml') "<?xml version=`"1.0`" encoding=`"UTF-8`"?><FGenericCrashContext><RuntimeProperties><CrashVersion>3</CrashVersion><ErrorMessage>$escaped</ErrorMessage><CrashType>$Type</CrashType><EngineVersion>5.1.1-0+++UE5+Release-5.1</EngineVersion></RuntimeProperties></FGenericCrashContext>"
}
Write-Report 'UECC-Windows-OLD_0000' 'LowLevelFatalError [File:C:\works\Pal-UE-EngineSource\Engine\Source\Runtime\Core\Private\Containers\Array.cpp] [Line: 8] Trying to resize TArray to an invalid size of 1' 'Assert'

$failures = @()
$script:proc = $null
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Api([string]$Method, [string]$Path, $Body = $null) {
    $p = @{ Method = $Method; Uri = "http://127.0.0.1:$Port/api/v1$Path"; TimeoutSec = 30 }
    if ($null -ne $Body) { $p.Body = ($Body | ConvertTo-Json -Depth 6); $p.ContentType = 'application/json' }
    Invoke-RestMethod @p
}
function Crash-Alerts { @((Api GET '/notifications').items | Where-Object { $_.title -like '*: new crash report' }) }
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { try { if (& $Condition) { return $true } } catch {}; Start-Sleep -Milliseconds 700 }
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
    $seenFile = Join-Path $runtime 'crash-analyzer\seen-reports.json'

    Test-RouteSmoke 'a report already on disk at start-up is recorded silently' {
        if (-not (Wait-Until { Test-Path $seenFile } 40)) { throw 'the watcher never recorded the existing reports' }
        if ((Get-Content $seenFile -Raw) -notmatch 'UECC-Windows-OLD_0000') { throw "not recorded: $(Get-Content $seenFile -Raw)" }
        if (@(Crash-Alerts).Count -ne 0) { throw 'an old report was announced' }
    }

    Test-RouteSmoke 'the analyze route reads the report and gives the TArray engine check its own finding' {
        $a = Api POST '/crash-analyzer/analyze'
        if ($a.crashReportsRead -ne 1) { throw "crashReportsRead = $($a.crashReportsRead)" }
        $finding = @($a.findings | Where-Object { $_.signatureId -eq 'engine-array-size' })
        if ($finding.Count -ne 1 -or -not (@($finding[0].evidence) -match 'UECC-Windows-OLD_0000')) { throw "no engine-array-size finding naming the report: $(@($a.findings | ForEach-Object signatureId) -join ',')" }
        if ($a.summary -notmatch 'Read 1 Unreal crash report') { throw "summary: $($a.summary)" }
    }

    Test-RouteSmoke 'a new report is announced exactly once, naming the report and its finding' {
        Write-Report 'UECC-Windows-NEW_0000' 'Unhandled Exception: EXCEPTION_ACCESS_VIOLATION reading address 0xffffffffffffffff'
        if (-not (Wait-Until { @(Crash-Alerts).Count -ge 1 } 45)) { throw 'no alert for the new report' }
        $alert = @(Crash-Alerts)[0]
        if ($alert.message -notmatch 'UECC-Windows-NEW_0000' -or $alert.message -notmatch 'Access violation') { throw "message: $($alert.message)" }
        if ($alert.message -match 'invalid array size') { throw 'the old report''s finding was presented as this crash''s cause' }
        Start-Sleep -Seconds 20
        if (@(Crash-Alerts).Count -ne 1) { throw "announced $(@(Crash-Alerts).Count) times" }
    }

    Test-RouteSmoke 'while muted a new report is held back, and announced once the mute is lifted' {
        Api POST '/alerts/mute' @{ minutes = 60 } | Out-Null
        Write-Report 'UECC-Windows-MUTED_0000' 'Unhandled Exception: EXCEPTION_ACCESS_VIOLATION writing address 0x000001fc3b9167e4'
        Start-Sleep -Seconds 35
        if (@(Crash-Alerts).Count -ne 1) { throw 'a report was announced during the mute' }
        Api POST '/alerts/mute' @{ minutes = 0 } | Out-Null
        if (-not (Wait-Until { @(Crash-Alerts).Count -ge 2 } 45)) { throw 'the held-back report was not announced after the mute' }
        if (@(Crash-Alerts | Where-Object { $_.message -match 'UECC-Windows-MUTED_0000' }).Count -ne 1) { throw 'the announcement does not name the held-back report' }
    }
}
finally {
    if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force -ErrorAction SilentlyContinue; $script:proc.WaitForExit(5000) | Out-Null }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.9.0 crash report smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.9.0 crash report smoke gate passed." -ForegroundColor Green
