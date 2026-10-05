# MystTiq v1.0.1.0: file reviewed for this release (2026-10-05).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18596,
    [int]$GamePortMain = 18696,
    [int]$GamePortOther = 18697
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] The fleet recovery smoke uses the Windows stand-in server; Linux is covered by the logic harness.' -ForegroundColor Yellow
    return
}

# v0.9.5.0: two server profiles in one MystTiq service (api-run, as the Desktop runs it), on isolated data with the stand-in
# PalServer (scripts/Testing/FakePalServer). Live on 2026-09-28, whenever one server started or exited, the other profiles
# logged "server crashed, restarting" and started their own servers, even one stopped on purpose. Here "default" is started,
# restarted three times and finally killed; "other" is stopped on purpose and must stay stopped throughout.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Headless sidecar not found: $Exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.9.5.0-fleet-" + [guid]::NewGuid().ToString('N'))
$fakeBuild = Join-Path $temp 'fakepal'
$config = Join-Path $temp 'mysttiq.json'
$fleet = Join-Path $temp 'fleet'
$profiles = [ordered]@{ default = $GamePortMain; other = $GamePortOther }
New-Item $fleet -ItemType Directory -Force | Out-Null

$fakeSource = Join-Path $temp 'fakepal-src'
Copy-Item (Join-Path $root 'scripts\Testing\FakePalServer') $fakeSource -Recurse
& dotnet build (Join-Path $fakeSource 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
foreach ($id in $profiles.Keys) {
    $serverRoot = Join-Path $temp "server-$id"
    New-Item $serverRoot, (Join-Path $temp "runtime-$id"), (Join-Path $temp "backups-$id") -ItemType Directory -Force | Out-Null
    Copy-Item "$fakeBuild\*" $serverRoot -Force
    $iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
    Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Fleet $id`",PublicPort=$($profiles[$id]),RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
}

& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleet; $cfg.api.Port = $Port
$cfg.Lifecycle.ServicePollSeconds = 1; $cfg.Lifecycle.RecoveryBackoffSeconds = 1; $cfg.Lifecycle.MaximumRecoveryAttempts = 3
$cfg.Lifecycle.StartupTimeoutSeconds = 8; $cfg.Lifecycle.RecoveryWindowSeconds = 600
$template = $cfg.Servers[0] | ConvertTo-Json -Depth 10
$cfg.Servers = @(foreach ($id in $profiles.Keys) {
    $s = $template | ConvertFrom-Json
    $s.Id = $id
    foreach ($name in 'Name', 'DisplayName') { if ($s.PSObject.Properties[$name]) { $s.$name = "Fleet $id" } }
    $s.ServerRoot = Join-Path $temp "server-$id"
    $s.RuntimeRoot = Join-Path $temp "runtime-$id"
    if ($s.PSObject.Properties['BackupRoot']) { $s.BackupRoot = Join-Path $temp "backups-$id" }
    $s.LaunchArguments = @("-port=$($profiles[$id])")
    if ($s.PSObject.Properties['SteamCmdPath']) { $s.SteamCmdPath = Join-Path $temp 'none.exe' }
    $s
})
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$failures = @()
$base = "http://127.0.0.1:$Port/api/v1/servers"
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Fake([string]$Id) { @(Get-Process PalServer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$(Join-Path $temp "server-$Id")*" }) }
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { try { if (& $Condition) { return $true } } catch {}; Start-Sleep -Milliseconds 400 }
    return $false
}
# A stop of a server that is not running answers 409 (not running); the body still says what happened.
function Post([string]$Id, [string]$Path) { Invoke-RestMethod "$base/$Id$Path" -Method Post -ContentType 'application/json' -Body '{}' -TimeoutSec 120 -SkipHttpErrorCheck }
function Assert-OtherStopped([string]$When) {
    if (@(Fake 'other').Count -ne 0) { throw "$When, the stopped server 'other' was started" }
    $status = Invoke-RestMethod "$base/other/status" -TimeoutSec 15
    if ($status.ready -or $status.crashDetected) { throw "$When, 'other' reads ready=$($status.ready) crashDetected=$($status.crashDetected): $($status.detail)" }
    $activity = @((Invoke-RestMethod "$base/other/activity/tail?lines=200" -TimeoutSec 15).lines) -join "`n"
    if ($activity -match 'crashed|restarting') { throw "$When, 'other' logged a crash or restart: $(($activity -split "`n" | Select-String 'crashed|restarting' | Select-Object -First 1).Line)" }
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 30)) { throw 'the fleet service is not answering' }

    Test-RouteSmoke 'the fleet lists both servers; "default" starts and "other" is stopped on purpose' {
        $ids = @((Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 5).serverProfileIds)
        if (-not ($ids -contains 'default' -and $ids -contains 'other')) { throw "profiles: $($ids -join ', ')" }
        $null = Post 'other' '/server/stop'
        $started = Post 'default' '/server/start'
        if (-not $started.success) { throw "default did not start: $($started.message)" }
        if (-not (Wait-Until { @(Fake 'default').Count -eq 1 } 20)) { throw 'default has no stand-in process' }
        Start-Sleep 3
        Assert-OtherStopped 'after default started'
    }

    Test-RouteSmoke 'restarting "default" three times never starts "other" or reports it crashed' {
        for ($i = 1; $i -le 3; $i++) {
            $null = Post 'default' '/server/stop'
            if (-not (Wait-Until { @(Fake 'default').Count -eq 0 } 20)) { throw "default did not stop (round $i)" }
            Start-Sleep 3
            Assert-OtherStopped "after default stopped (round $i)"
            $started = Post 'default' '/server/start'
            if (-not $started.success) { throw "default did not start (round $i): $($started.message)" }
            Start-Sleep 3
            Assert-OtherStopped "after default started again (round $i)"
        }
    }

    Test-RouteSmoke 'when "default" dies unexpectedly, "default" is recovered and "other" stays stopped' {
        Fake 'default' | Stop-Process -Force
        if (-not (Wait-Until { @(Fake 'default').Count -eq 1 } 30)) { throw 'default was not recovered after it died' }
        Start-Sleep 4
        Assert-OtherStopped 'after default died and was recovered'
        $status = Invoke-RestMethod "$base/default/status" -TimeoutSec 15
        if (-not $status.ready) { throw "default is not ready after recovery: $($status.detail)" }
    }
}
finally {
    foreach ($id in $profiles.Keys) { try { $null = Post $id '/server/stop' } catch {} }
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force }
    foreach ($id in $profiles.Keys) { Fake $id | Stop-Process -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) { throw "MystTiq v0.9.5.0 fleet recovery smoke failed: $($failures.Count) check(s)." }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'MystTiq v0.9.5.0 fleet recovery smoke passed.' -ForegroundColor Green
