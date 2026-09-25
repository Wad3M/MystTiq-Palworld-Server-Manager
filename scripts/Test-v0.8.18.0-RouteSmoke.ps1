[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18418,
    [int]$GamePort = 18518
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] The v0.8.18.0 bandwidth smoke uses the Windows stand-in server; Linux is checked by the isolated Linux test.' -ForegroundColor Yellow
    return
}

# v0.8.18.0: bandwidth, end to end on the published exe in service mode with a stand-in PalServer
# (scripts/Testing/FakePalServer) on a non-8211 port. Own FleetRoot and ports; nothing touches real servers. The
# restarts come from service mode's own supervisor, so the write-before-start is proven on that path, not only the API's.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Headless sidecar not found: $Exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.18.0-bandwidth-" + [guid]::NewGuid().ToString('N'))
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
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Bandwidth Smoke`",ServerPlayerMaxNum=16,PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
# The engine writes its own Engine.ini with [Core.System] paths; MystTiq must keep those lines.
$engineIni = Join-Path $iniDir 'Engine.ini'
$engineOriginal = "[Core.System]`r`nPaths=../../../Engine/Content`r`nPaths=%GAMEDIR%Content`r`n`r`n"
[IO.File]::WriteAllText($engineIni, $engineOriginal)
& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.api.Port = $Port
$cfg.Lifecycle.ServicePollSeconds = 1; $cfg.Lifecycle.RecoveryBackoffSeconds = 1; $cfg.Lifecycle.MaximumRecoveryAttempts = 5
$cfg.Lifecycle.StartupTimeoutSeconds = 8; $cfg.Lifecycle.RecoveryWindowSeconds = 600
foreach ($s in $cfg.Servers) { $s.LaunchArguments = @("-port=$GamePort") }
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$failures = @()
$base = "http://127.0.0.1:$Port/api/v1"
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Fake { @(Get-Process PalServer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$serverRoot*" }) }
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { try { if (& $Condition) { return $true } } catch {}; Start-Sleep -Milliseconds 400 }
    return $false
}
function Bandwidth { Invoke-RestMethod "$base/network/policy" -TimeoutSec 15 }
function Save-Bandwidth([hashtable]$Policy) {
    Invoke-RestMethod "$base/network/policy" -Method Put -ContentType 'application/json' -Body ($Policy | ConvertTo-Json) -TimeoutSec 15
}
function Engine { [IO.File]::ReadAllText($engineIni) }
function Restart-ByCrash {
    $before = (Fake)[0].Id
    Fake | Stop-Process -Force
    if (-not (Wait-Until { @(Fake).Count -eq 1 -and (Fake)[0].Id -ne $before } 30)) { throw 'the supervisor did not restart the server' }
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('service-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    if (-not (Wait-Until { @(Fake).Count -eq 1 } 30)) { throw 'the service did not start the stand-in server' }
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 20)) { throw 'the embedded API is not answering' }

    Test-RouteSmoke 'with no policy the game''s own defaults show and Engine.ini is untouched' {
        $b = Bandwidth
        if ($b.platform -ne 'Windows' -or $b.gameDefaultPerPlayerMbps -ne 64 -or $b.gameDefaultTickRate -ne 60) { throw "defaults $($b | ConvertTo-Json -Compress)" }
        if ($b.effectivePerPlayerMbps -ne 64 -or $b.maxPlayers -ne 16 -or $b.worstCaseUploadMbps -ne 1024) { throw "effective $($b.effectivePerPlayerMbps), players $($b.maxPlayers), worst case $($b.worstCaseUploadMbps)" }
        if ((Engine) -ne $engineOriginal) { throw 'Engine.ini changed' }
    }

    Test-RouteSmoke 'saving while the server runs waits for the next start, says so, and leaves Engine.ini alone' {
        $r = Save-Bandwidth @{ mode = 'Custom'; perPlayerMbps = 2; tickRate = 30; uploadBudgetMbps = 40 }
        if (-not $r.success -or $r.message -notmatch 'next start' -or -not $r.snapshot.restartNeeded) { throw "$($r.message) restartNeeded=$($r.snapshot.restartNeeded)" }
        if ((Engine) -ne $engineOriginal) { throw 'Engine.ini was written under a running server' }
        if ($r.snapshot.suggestedPerPlayerMbps -ne 2) { throw "a 40 Mbit/s upload for 16 players should suggest 2 Mbit/s, got $($r.snapshot.suggestedPerPlayerMbps)" }
    }

    Test-RouteSmoke 'the next start (a supervisor restart) writes the limits into Engine.ini, keeping the engine''s own lines' {
        Restart-ByCrash
        $values = Engine
        if ($values -notmatch "MaxClientRate=250000\r\nMaxInternetClientRate=250000\r\nNetServerMaxTickRate=30" -or -not $values.StartsWith("[Core.System]`r`nPaths=../../../Engine/Content`r`nPaths=%GAMEDIR%Content")) { throw "Engine.ini: $values" }
        if ((Get-Content "$engineIni.mysttiq-original" -Raw) -ne $engineOriginal) { throw 'the original Engine.ini was not kept' }
        $b = Bandwidth
        if ($b.restartNeeded -or -not $b.policyInEngineIni -or $b.effectivePerPlayerMbps -ne 2 -or $b.effectiveTickRate -ne 30) { throw "after the restart: $($b | ConvertTo-Json -Compress)" }
        $console = Get-Content (Join-Path $serverRoot 'Pal\Saved\Logs\MystTiq-PalServer-Console.log') -Raw
        if ($console -notmatch 'Bandwidth: Engine.ini set to 2 Mbit/s per player and 30 network updates per second') { throw 'the console log does not say what was written' }
    }

    Test-RouteSmoke 'when the engine drops the limits (it rewrites Engine.ini on exit), the next start puts them back' {
        [IO.File]::WriteAllText($engineIni, $engineOriginal)
        Restart-ByCrash
        if ((Engine) -notmatch 'MaxClientRate=250000') { throw 'the limits were not written again' }
    }

    Test-RouteSmoke 'the HOST route carries the bandwidth too' {
        $h = Invoke-RestMethod "$base/host" -TimeoutSec 15
        if ($h.bandwidth.policy.mode -ne 'Custom' -or $h.bandwidth.effectivePerPlayerMbps -ne 2) { throw "host bandwidth: $($h.bandwidth | ConvertTo-Json -Compress)" }
    }

    Test-RouteSmoke 'with the server stopped, going back to the game defaults is written at once and removes only the three keys' {
        Invoke-RestMethod "$base/server/stop" -Method Post -TimeoutSec 60 | Out-Null
        if (-not (Wait-Until { @(Fake).Count -eq 0 } 20)) { throw 'the server did not stop' }
        $r = Save-Bandwidth @{ mode = 'GameDefault'; perPlayerMbps = 2; tickRate = 30 }
        if (-not $r.success -or $r.message -notmatch 'written' -or $r.snapshot.restartNeeded) { throw "$($r.message)" }
        $values = Engine
        if ($values -match 'MaxClientRate|NetServerMaxTickRate' -or $values -notmatch 'Paths=%GAMEDIR%Content') { throw "Engine.ini: $values" }
    }

    Test-RouteSmoke 'a limit out of range is refused and the saved policy stays' {
        try { Save-Bandwidth @{ mode = 'Custom'; perPlayerMbps = 500; tickRate = 30 } | Out-Null; throw 'accepted' }
        catch { if ($_.Exception.Message -eq 'accepted' -or $_.Exception.Response.StatusCode.value__ -ne 400) { throw "expected 400: $($_.Exception.Message)" } }
        if ((Bandwidth).policy.mode -ne 'GameDefault') { throw 'the saved policy changed' }
    }

    Test-RouteSmoke 'each save is in the activity log' {
        $text = Get-Content (Join-Path $runtime 'logs\MystTiq-Activity.log') -Raw
        if (([regex]::Matches($text, 'Bandwidth saved')).Count -ne 2) { throw "expected two saves logged, found $(([regex]::Matches($text, 'Bandwidth saved')).Count)" }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    Fake | Stop-Process -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.18.0 bandwidth smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.18.0 bandwidth smoke gate passed." -ForegroundColor Green
