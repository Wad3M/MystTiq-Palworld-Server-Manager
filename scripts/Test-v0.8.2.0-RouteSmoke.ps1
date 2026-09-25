[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18382,
    [int]$GamePort = 18482
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows service-run smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.8.2.0: service mode (the `service-run` command an installed Windows service / systemd unit runs), end to end on the
# PUBLISHED exe, run in the foreground (it degrades to a plain process outside the Service Control Manager). A real
# stand-in PalServer (scripts/Testing/FakePalServer: process name PalServer, binds its -port=N UDP port like a ready
# server) on a NON-8211 game port. Own FleetRoot, own ports; nothing touches real servers.
#   1. service-run starts the server on its configured port and stays up (it used to wait for UDP 8211, report the
#      start as failed and shut itself down);
#   2. a crash is handled by exactly ONE supervisor (the API host used to run a second one), which now sends alerts;
#   3. giving up still exits with the service exit code, and records the give-up;
#   4. when the OS service manager starts it again and the server is up, it says so and unpins the DOWN notice.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.2.0-servicerun-" + [guid]::NewGuid().ToString('N'))
$fakeBuild = Join-Path $temp 'fakepal'
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null

$fakeProject = Join-Path $root 'scripts\Testing\FakePalServer'
& dotnet build (Join-Path $fakeProject 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
foreach ($leftover in 'bin', 'obj') { $d = Join-Path $fakeProject $leftover; if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
Copy-Item "$fakeBuild\*" $serverRoot -Force
$iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Service Smoke`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
& $exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.api.Port = $Port
$cfg.Lifecycle.ServicePollSeconds = 1; $cfg.Lifecycle.RecoveryBackoffSeconds = 1; $cfg.Lifecycle.MaximumRecoveryAttempts = 1
$cfg.Lifecycle.StartupTimeoutSeconds = 8; $cfg.Lifecycle.RecoveryWindowSeconds = 600
foreach ($s in $cfg.Servers) { $s.LaunchArguments = @("-port=$GamePort") }
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$failures = @()
$script:svc = $null
$script:run = 0
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Start-ServiceRun {
    $script:run++
    $script:out = Join-Path $temp "svc$($script:run).log"; $script:err = Join-Path $temp "svc$($script:run).err.log"
    $script:svc = Start-Process -FilePath $exe -ArgumentList @('service-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput $script:out -RedirectStandardError $script:err
}
function Fake { @(Get-Process PalServer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$serverRoot*" }) }
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { try { if (& $Condition) { return $true } } catch {}; Start-Sleep -Milliseconds 400 }
    return $false
}
function Get-Notifications { foreach ($n in (Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/notifications" -TimeoutSec 5).items) { $n } }
function Launches { @(Get-Content (Join-Path $serverRoot 'Pal\Saved\Logs\MystTiq-PalServer-Console.log') -ErrorAction SilentlyContinue | Select-String -Pattern 'bootstrap PID').Count }
function CrashLines { @(Get-Content $script:err -ErrorAction SilentlyContinue | Select-String -SimpleMatch 'crash/disappearance detected').Count }

try {
    Start-ServiceRun
    Test-RouteSmoke 'service-run starts the server on its configured (non-8211) game port and stays up' {
        if (-not (Wait-Until { @(Fake).Count -eq 1 } 30)) { throw 'the service did not start the server' }
        Start-Sleep -Seconds 12   # past the 8 s startup timeout that used to end the service
        if ($script:svc.HasExited) { throw "service-run exited ($($script:svc.ExitCode)): $(Get-Content $script:err -Raw)" }
        if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 10)) { throw 'the embedded API is not answering' }
    }

    Test-RouteSmoke 'a crash is handled by exactly one supervisor, which now sends the crash and back-up alerts' {
        $launchesBefore = Launches
        Fake | Stop-Process -Force
        if (-not (Wait-Until { @(Fake).Count -eq 1 -and (Launches) -gt $launchesBefore } 25)) { throw 'the server was not restarted' }
        Start-Sleep -Seconds 4
        if ((Launches) - $launchesBefore -ne 1) { throw "expected exactly one relaunch, got $((Launches) - $launchesBefore)" }
        if ((CrashLines) -ne 1) { throw "expected one supervisor to detect the crash, got $(CrashLines) detections" }
        if (-not (Wait-Until { @(Get-Notifications | Where-Object { $_.title -match 'server crashed' }).Count -eq 1 -and @(Get-Notifications | Where-Object { $_.title -match 'back up' }).Count -ge 1 } 15)) {
            throw "expected one crash alert and a back-up alert: $(@(Get-Notifications | ForEach-Object { $_.title }) -join ' | ')"
        }
    }

    Test-RouteSmoke 'giving up still exits with the service exit code and records the give-up' {
        Rename-Item (Join-Path $serverRoot 'PalServer.exe') 'PalServer.exe.hold'
        Fake | Stop-Process -Force
        if (-not (Wait-Until { $script:svc.HasExited } 40)) { throw 'service-run did not exit after giving up' }
        if ($script:svc.ExitCode -ne 16) { throw "expected exit code 16 (CrashDetected), got $($script:svc.ExitCode)" }
        $state = Get-Content (Join-Path $runtime 'crash-recovery\state.json') -Raw | ConvertFrom-Json
        if (-not $state.GaveUpAtUtc -or -not $state.PinnedNotificationId) { throw "state.json lacks the give-up or the pinned id: $($state | ConvertTo-Json -Compress)" }
        $script:downId = $state.PinnedNotificationId
    }

    Test-RouteSmoke 'when the OS starts the service again and the server comes up, it is announced back up and the DOWN notice unpins' {
        Rename-Item (Join-Path $serverRoot 'PalServer.exe.hold') 'PalServer.exe'
        Start-ServiceRun
        if (-not (Wait-Until { @(Fake).Count -eq 1 } 30)) { throw 'the restarted service did not start the server' }
        if (-not (Wait-Until { @(Get-Notifications | Where-Object { $_.title -match 'back up' -and $_.message -match 'service restarted' }).Count -ge 1 } 30)) {
            throw "no recovery notice after the service restart: $(@(Get-Notifications | ForEach-Object { $_.title }) -join ' | ')"
        }
        $down = @(Get-Notifications | Where-Object { $_.id -eq $script:downId })
        if ($down.Count -ne 1 -or $down[0].pinned) { throw 'the DOWN notice from before the service restart is still pinned (or gone)' }
        $state = Get-Content (Join-Path $runtime 'crash-recovery\state.json') -Raw | ConvertFrom-Json
        if ($state.GaveUpAtUtc) { throw 'the give-up was not cleared' }
    }
}
finally {
    if ($script:svc -and -not $script:svc.HasExited) { Stop-Process -Id $script:svc.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    Fake | Stop-Process -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.2.0 service mode smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.2.0 service mode smoke gate passed." -ForegroundColor Green
