# MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18698,
    [int]$GamePort = 18798,
    [double]$SoakMinutes = 10
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.7.5 (roadmap Q-10): the long test set, opt-in in the full gate (-RunLong), through a real isolated service with the
# stand-in PalServer and a stand-in SteamCMD:
#   1. An update interrupted half way (MystTiq stopped while SteamCMD runs) is not reported as installed and is not
#      started; a finished update clears it.
#   2. Simultaneous operations (backups, starts, stops, updates at once, three rounds) never fail with a server error and
#      leave no half-written backup.
#   3. A save file held open: a backup waits for it, and names it if it stays held.
#   4. 10,000 console lines arriving while the console is polled: every poll stays quick and bounded.
#   5. Shutdown (a console stop, Ctrl+Break): the service exits within 15 s, frees its port and files, and the next one finds the server it
#      left running.
#   6. A short idle soak ($SoakMinutes min, sampled every minute) through Measure-MystTiqIdleSoak.ps1.
# Rapid tab switching is in the ArtworkHarness (it needs the desktop's view model); the 24-hour soak is run separately.
if (-not $IsWindows) { Write-Host '[SKIP] The long set uses the Windows stand-in PalServer and SteamCMD.' -ForegroundColor Yellow; return }
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }

$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.7.5-longset-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $steamDir = Join-Path $temp 'steamcmd'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot, $steamDir -ItemType Directory -Force | Out-Null
foreach ($stand in @(@{ Name = 'FakePalServer'; Out = (Join-Path $temp 'fakepal') }, @{ Name = 'FakeSteamCmd'; Out = $steamDir })) {
    $src = Join-Path $temp "$($stand.Name)-src"
    Copy-Item (Join-Path $root "scripts\Testing\$($stand.Name)") $src -Recurse
    & dotnet build (Join-Path $src "$($stand.Name).csproj") -c Release -o $stand.Out -nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$($stand.Name) did not build" }
}
Copy-Item (Join-Path $temp 'fakepal\*') $serverRoot -Force
$steamCmd = Join-Path $steamDir 'steamcmd.exe'
function Set-SteamMode([string]$Mode) { Set-Content (Join-Path $steamDir 'mode.txt') $Mode }
$iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Long Set`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
$world = Join-Path $serverRoot 'Pal\Saved\SaveGames\0\LongSetWorld'
New-Item (Join-Path $world 'Players') -ItemType Directory -Force | Out-Null
$levelSav = Join-Path $world 'Level.sav'
[IO.File]::WriteAllBytes($levelSav, [byte[]]::new(256KB))
$logs = Join-Path $serverRoot 'Pal\Saved\Logs'; New-Item $logs -ItemType Directory -Force | Out-Null

& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.Api.Port = $Port
$cfg.Lifecycle.StartupTimeoutSeconds = 20; $cfg.Lifecycle.StopTimeoutSeconds = 5; $cfg.Lifecycle.RecoveryBackoffSeconds = 600
# The stand-in gets no console of its own (createNoWindow), so the console stop sent to the service does not reach it.
foreach ($s in $cfg.Servers) { $s.LaunchArguments = @("-port=$GamePort", '@mysttiq:createNoWindow=true') }
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$base = "http://127.0.0.1:$Port/api/v1"
$http = [System.Net.Http.HttpClient]::new(); $http.Timeout = [TimeSpan]::FromSeconds(300)
function Send-Async([string]$Method, [string]$Path) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), "$base$Path")
    if ($Method -ne 'GET') { $request.Content = [System.Net.Http.StringContent]::new('', [Text.Encoding]::UTF8, 'application/json') }
    $http.SendAsync($request)
}
function Result($Task) {
    $r = $Task.GetAwaiter().GetResult(); $text = $r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    [pscustomobject]@{ Status = [int]$r.StatusCode; Text = $text; Body = $(if ($text) { try { $text | ConvertFrom-Json } catch { $text } } else { $null }) }
}
function Call([string]$Method, [string]$Path) { Result (Send-Async $Method $Path) }
function Fake { @(Get-Process PalServer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$serverRoot*" }) }
function FakeSteam { @(Get-Process steamcmd -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$steamDir*" }) }
$script:svc = $null
function Start-LongSetService {
    $n = @(Get-ChildItem $temp -Filter 'svc*.log').Count
    $script:svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', $steamCmd) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp "svc$n.log") -RedirectStandardError (Join-Path $temp "svc$n.err.log")
    for ($i = 0; $i -lt 80; $i++) { try { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 | Out-Null; return } catch { Start-Sleep -Milliseconds 250 } }
    throw "the service is not answering. $(Get-Content (Join-Path $temp "svc$n.err.log") -Raw -ErrorAction SilentlyContinue)"
}
$failures = @()
function Test-LongSet([string]$Name, [scriptblock]$Body) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    try { & $Body; Write-Host "[PASS] Long Set :: $Name ($([math]::Round($clock.Elapsed.TotalSeconds)) s)" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Long Set :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

try {
    Start-LongSetService

    Test-LongSet 'an update interrupted half way (MystTiq stopped while SteamCMD runs) is not reported as installed and is not started; a finished update clears it' {
        Set-SteamMode 'hang'
        $update = Send-Async POST '/server/distribution/update?validate=false'
        for ($i = 0; $i -lt 80 -and @(FakeSteam).Count -eq 0; $i++) { Start-Sleep -Milliseconds 250 }
        if (@(FakeSteam).Count -eq 0) { throw 'the stand-in SteamCMD did not start' }
        if (-not (Test-Path (Join-Path $serverRoot '.mysttiq-update-in-progress'))) { throw 'no update marker while SteamCMD runs' }
        $during = (Call GET '/server/distribution').Body
        if (-not $during.serverExecutableExists -or $during.incompleteReason) { throw "while the update runs it is not yet called incomplete: $($during.detail)" }
        Stop-Process -Id $script:svc.Id -Force; FakeSteam | Stop-Process -Force
        try { $update.GetAwaiter().GetResult() | Out-Null } catch { }
        Start-LongSetService
        $after = (Call GET '/server/distribution').Body
        if ($after.serverExecutableExists -or $after.incompleteReason -notmatch 'did not finish') { throw "after the interruption: installed=$($after.serverExecutableExists), reason '$($after.incompleteReason)'" }
        $start = Call POST '/server/start'
        if (@(Fake).Count -gt 0 -or $start.Text -notmatch 'Run Update again') { throw "the half-updated server was started or the refusal does not say why: $($start.Status) $($start.Text)" }
        Set-SteamMode 'complete'
        $done = Call POST '/server/distribution/update?validate=false'
        $final = (Call GET '/server/distribution').Body
        if ($done.Status -ne 200 -or -not $final.serverExecutableExists -or $final.incompleteReason -or (Test-Path (Join-Path $serverRoot '.mysttiq-update-in-progress'))) {
            throw "the finished update did not clear it: $($done.Status) $($done.Text); installed=$($final.serverExecutableExists)" }
    }

    Test-LongSet 'a SteamCMD that fails before it starts (no connection) leaves the install as it was' {
        Set-SteamMode 'fail'
        $r = Call POST '/server/distribution/update?validate=false'
        $st = (Call GET '/server/distribution').Body
        if ($r.Status -ne 409 -or -not $st.serverExecutableExists -or $st.incompleteReason) { throw "$($r.Status); installed=$($st.serverExecutableExists), reason '$($st.incompleteReason)'" }
        Set-SteamMode 'complete'
    }

    Test-LongSet 'simultaneous operations (backups, starts, stops and updates at once, three rounds) never fail with a server error and leave no half-written backup' {
        $codes = @()
        for ($round = 1; $round -le 3; $round++) {
            $tasks = @(
                (Send-Async POST '/backups/create'), (Send-Async POST '/backups/create'), (Send-Async POST '/backups/create'), (Send-Async POST '/backups/create'),
                (Send-Async POST '/server/start'), (Send-Async POST '/server/start'), (Send-Async POST '/server/stop'), (Send-Async POST '/server/stop'),
                (Send-Async POST '/server/distribution/update?validate=false'), (Send-Async POST '/server/distribution/update?validate=false'),
                (Send-Async GET '/status/poll?lines=500'), (Send-Async GET '/backups'))
            [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks, [TimeSpan]::FromMinutes(4)) | Out-Null
            foreach ($t in $tasks) { $codes += (Result $t).Status }
        }
        $bad = @($codes | Where-Object { $_ -ge 500 -or $_ -eq 0 })
        if ($bad.Count -gt 0) { throw "server errors: $($bad -join ', ') (all: $($codes -join ','))" }
        if (@(Get-ChildItem $backupRoot -Filter '*.partial').Count -gt 0) { throw 'a half-written backup was left' }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        foreach ($zip in Get-ChildItem $backupRoot -Filter '*.zip') { $z = [IO.Compression.ZipFile]::OpenRead($zip.FullName); $n = $z.Entries.Count; $z.Dispose(); if ($n -eq 0) { throw "$($zip.Name) is empty" } }
        if ((Call GET '/status').Status -ne 200) { throw 'the status no longer answers' }
        Call POST '/server/stop' | Out-Null
        if (@(Fake).Count -gt 0) { Fake | Stop-Process -Force }
        Write-Host "    $($codes.Count) requests: $(($codes | Group-Object | ForEach-Object { "$($_.Count)x $($_.Name)" }) -join ', ')"
    }

    Test-LongSet 'a save file held open: a backup waits for it (held 2.5 s), and when it stays held the message names it' {
        # Another process holds it for 2.5 s (as the server does while it writes the save), then lets go.
        $holder = "`$f = [IO.File]::Open('$levelSav', 'Open', 'ReadWrite', 'None'); Start-Sleep -Milliseconds 2500; `$f.Dispose()"
        $p = Start-Process pwsh -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($holder))) -WindowStyle Hidden -PassThru
        $locked = $false
        for ($i = 0; $i -lt 100 -and -not $locked; $i++) { try { ([IO.File]::Open($levelSav, 'Open', 'Read', 'Read')).Dispose(); Start-Sleep -Milliseconds 50 } catch { $locked = $true } }
        if (-not $locked) { throw 'the holder did not take the file' }
        $r = Call POST '/backups/create'
        $p.WaitForExit(10000) | Out-Null
        if ($r.Status -ne 200) { throw "the backup did not wait for the file: $($r.Status) $($r.Text)" }
        $hold = [IO.File]::Open($levelSav, 'Open', 'ReadWrite', 'None')
        try {
            $clock = [Diagnostics.Stopwatch]::StartNew()
            $r = Call POST '/backups/create'
            $clock.Stop()
        } finally { $hold.Dispose() }
        if ($r.Status -eq 200 -or $r.Text -notmatch 'Level\.sav' -or $r.Text -notmatch 'stayed in use' -or $clock.Elapsed.TotalSeconds -lt 8) {
            throw "held throughout: $($r.Status) after $([math]::Round($clock.Elapsed.TotalSeconds, 1)) s: $($r.Text)" }
        if (@(Get-ChildItem $backupRoot -Filter '*.partial').Count -gt 0) { throw 'a half-written backup was left' }
    }

    Test-LongSet '10,000 console lines arriving while the console is polled: every poll answers within 2 s with at most 500 lines, and the newest line is shown' {
        $palLog = Join-Path $logs 'Pal.log'
        $slowest = 0; $most = 0
        for ($chunk = 0; $chunk -lt 20; $chunk++) {
            Add-Content $palLog (($chunk * 500)..($chunk * 500 + 499) | ForEach-Object { "[2026.10.11-12.00.00:000][  0]LogPal: chatty line $_ with some padding to look like a real log line" })
            $clock = [Diagnostics.Stopwatch]::StartNew(); $poll = Call GET '/status/poll?lines=500'; $clock.Stop()
            $slowest = [math]::Max($slowest, $clock.ElapsedMilliseconds)
            $lines = @($poll.Body.logTail.lines); $most = [math]::Max($most, $lines.Count)
            if ($poll.Status -ne 200 -or -not ($lines -match "chatty line $($chunk * 500 + 499) ")) { throw "poll $chunk did not show the newest line ($($poll.Status), $($lines.Count) lines)" }
        }
        if ($slowest -gt 2000 -or $most -gt 520) { throw "slowest poll $slowest ms, most lines $most" }
        Write-Host "    slowest poll $slowest ms; at most $most lines"
    }

    Test-LongSet 'shutdown (a console stop): the service exits within 15 s, frees its port and files, and the next one finds the server it left running' {
        $start = Call POST '/server/start'
        if ($start.Status -ne 200 -or @(Fake).Count -ne 1) { throw "the stand-in server did not start: $($start.Status) $($start.Text)" }
        Call POST '/backups/create' | Out-Null
        $fakeId = @(Fake)[0].Id
        $clock = [Diagnostics.Stopwatch]::StartNew()
        & (Join-Path $root 'scripts\Testing\Send-MystTiqConsoleStop.ps1') -ProcessId $script:svc.Id
        $exited = $script:svc.WaitForExit(15000); $clock.Stop()
        if (-not $exited) { Stop-Process -Id $script:svc.Id -Force; throw 'the service did not exit within 15 s of the console stop' }
        $exitCode = $script:svc.ExitCode
        if ($exitCode -ne 0) { throw "the service exited with code $exitCode after the console stop" }
        try { $l = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port); $l.Start(); $l.Stop() } catch { throw "the port is still in use after the stop" }
        $held = @(Get-ChildItem $runtime, $backupRoot, $fleetRoot -Recurse -File | Where-Object { try { $s = [IO.File]::Open($_.FullName, 'Open', 'ReadWrite', 'None'); $s.Dispose(); $false } catch { $true } })
        if ($held.Count -gt 0) { throw "files still held after the stop: $($held.Name -join ', ')" }
        if (-not (Get-Process -Id $fakeId -ErrorAction SilentlyContinue)) { throw 'the server was stopped with the service (it is meant to keep running)' }
        Start-LongSetService
        $status = (Call GET '/status').Body
        $again = Call POST '/server/start'
        if (-not $status.serverMayBeRunning -or @(Fake).Count -ne 1) { throw "the next service did not find the running server (mayBeRunning=$($status.serverMayBeRunning), $(@(Fake).Count) PalServer)" }
        Write-Host "    stopped in $($clock.ElapsedMilliseconds) ms (exit $exitCode); the next service found PalServer $fakeId; a second start: $($again.Status)"
        Call POST '/server/stop' | Out-Null
    }

    # v1.0.8.0: with the default launch (sharing the service's console), a Ctrl+C on that console stopped the game too.
    Test-LongSet 'a Ctrl+C on the service''s console (mysttiq-server api-run in a terminal) stops MystTiq but not the game server, with the default launch' {
        & (Join-Path $root 'scripts\Testing\Send-MystTiqConsoleStop.ps1') -ProcessId $script:svc.Id
        if (-not $script:svc.WaitForExit(15000)) { throw 'the service did not stop' }
        Fake | Stop-Process -Force -ErrorAction SilentlyContinue
        $c = Get-Content $config -Raw | ConvertFrom-Json
        foreach ($s in $c.Servers) { $s.LaunchArguments = @("-port=$GamePort") }
        $c | ConvertTo-Json -Depth 20 | Set-Content $config
        Start-LongSetService
        $start = Call POST '/server/start'
        if ($start.Status -ne 200 -or @(Fake).Count -ne 1) { throw "the stand-in server did not start: $($start.Status) $($start.Text)" }
        $fakeId = @(Fake)[0].Id
        & (Join-Path $root 'scripts\Testing\Send-MystTiqConsoleStop.ps1') -ProcessId $script:svc.Id -Signal CtrlC
        if (-not $script:svc.WaitForExit(15000)) {
            # A process started from a parent that ignores Ctrl+C inherits that (a bash-started pwsh does); then nothing
            # here can show the difference, so say so instead of failing.
            Write-Host '    [SKIP] this service ignores Ctrl+C (inherited from the process that started the long set); run it from PowerShell' -ForegroundColor Yellow
            Stop-Process -Id $script:svc.Id -Force
            return
        }
        if (-not (Get-Process -Id $fakeId -ErrorAction SilentlyContinue)) { throw 'the Ctrl+C stopped the game server too' }
        Write-Host "    the service stopped on Ctrl+C (exit $($script:svc.ExitCode)); PalServer $fakeId kept running"
    }
}
finally {
    if ($script:svc -and -not $script:svc.HasExited) { Stop-Process -Id $script:svc.Id -Force -ErrorAction SilentlyContinue }
    Fake | Stop-Process -Force -ErrorAction SilentlyContinue
    FakeSteam | Stop-Process -Force -ErrorAction SilentlyContinue
}

if ($SoakMinutes -gt 0) {
    Test-LongSet "a $SoakMinutes-minute idle soak (sampled every minute) stays flat and stops cleanly" {
        $out = Join-Path $temp 'soak'
        & pwsh -NoProfile -File (Join-Path $root 'scripts\Measure-MystTiqIdleSoak.ps1') -ProjectRoot $root -Exe $Exe -Hours ($SoakMinutes / 60) -SampleMinutes 1 -PollSeconds 2 -Port ($Port + 1) -GamePort ($GamePort + 1) -OutputDir $out | ForEach-Object { Write-Host "    $_" }
        if ($LASTEXITCODE -ne 0) { throw "the soak failed; see $out\summary.md" }
    }
}

if ($failures.Count -gt 0) { throw "MystTiq v1.0.7.5 long set failed: $($failures -join '; ')" }
Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'MystTiq v1.0.7.5 long set passed.' -ForegroundColor Green
