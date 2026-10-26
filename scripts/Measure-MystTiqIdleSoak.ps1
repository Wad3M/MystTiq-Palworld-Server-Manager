# MystTiq v1.1.6.0: file reviewed for this release (2026-10-26).
[CmdletBinding()]
param(
    [ValidateSet('Windows', 'Linux')][string]$Target = 'Windows',
    [ValidateSet('Run', 'Start', 'Status', 'Collect', 'Analyze')][string]$Action = 'Run',
    [double]$Hours = 24,
    [double]$SampleMinutes = 60,
    [int]$PollSeconds = 5,
    [int]$Port = 18697,
    [int]$GamePort = 18797,
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [string]$OutputDir = '',
    [string]$LinuxHost = '192.168.1.122',
    [string]$LinuxUser = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519",
    [string]$RemoteName = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
# v1.0.7.5 (roadmap Q-10): the idle soak. An isolated MystTiq runs for $Hours with a client asking for the status every
# $PollSeconds (as a connected desktop does); every $SampleMinutes its memory, handles (open files on Linux) and threads
# are recorded, then it is stopped and the stop is checked: how long it took, that the process is gone and its port free.
#   Windows: -Action Run does it all in this process (start it detached for a long run); the stand-in PalServer runs.
#   Linux:   -Action Start copies a linux-x64 build to the test VM and starts it as per-user systemd units (no sudo,
#            nothing installed, the installed mysttiq-palworld service untouched); -Action Status shows progress;
#            -Action Collect fetches the results, analyses them and removes the folder from the VM.
#   -Action Analyze re-reads samples.csv in -OutputDir.
# Verdict: from the first sample after the warm-up (an hour, or a quarter of a shorter run), memory may not grow by more than 30% and 50 MB, handles by more than 25% and
# 100, threads by more than 15; no status request may fail; the stop must finish within 15 s with nothing left behind.
$root = (Resolve-Path $ProjectRoot).Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if (-not $OutputDir) { $OutputDir = Join-Path $root "artifacts\soak\$($Target.ToLowerInvariant())-$stamp" }
New-Item $OutputDir -ItemType Directory -Force | Out-Null
$sshArgs = @('-i', [Environment]::ExpandEnvironmentVariables($IdentityFile), '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10', '-o', 'ServerAliveInterval=15', '-o', 'ServerAliveCountMax=4')
$remote = "$LinuxUser@$LinuxHost"

function Write-SoakReport([string]$Dir, [string]$TargetName) {
    $csv = Join-Path $Dir 'samples.csv'
    if (-not (Test-Path $csv)) { throw "No samples in $Dir" }
    $rows = @(Import-Csv $csv | Where-Object { $_.PSObject.Properties.Name -contains 'private_kb' -and $_.private_kb -match '^\d+$' })
    if ($rows.Count -lt 2) { throw "Only $($rows.Count) sample(s) in $csv" }
    # Start-up is not growth: the baseline is the first sample after the warm-up (an hour, or a quarter of a shorter run).
    $hoursRun = [double]$rows[-1].elapsed_hours
    $warmup = [math]::Min(1.0, $hoursRun / 4)
    $settled = @($rows | Where-Object { [double]$_.elapsed_hours -ge $warmup })
    if ($settled.Count -lt 2) { $settled = $rows }
    $first = $settled[0]; $last = $rows[-1]
    function Growth([string]$Field) { [double]$last.$Field - [double]$first.$Field }
    function Percent([string]$Field) { if ([double]$first.$Field -eq 0) { 0 } else { 100 * (Growth $Field) / [double]$first.$Field } }
    $memMb = (Growth 'private_kb') / 1024; $memPct = Percent 'private_kb'
    $handles = Growth 'open_files'; $handlePct = Percent 'open_files'; $threads = Growth 'threads'
    # The trend: least squares over every sample, as MB per day.
    $xs = @($settled | ForEach-Object { [double]$_.elapsed_hours }); $ys = @($settled | ForEach-Object { [double]$_.private_kb / 1024 })
    $mx = ($xs | Measure-Object -Average).Average; $my = ($ys | Measure-Object -Average).Average
    $num = 0.0; $den = 0.0
    for ($i = 0; $i -lt $settled.Count; $i++) { $num += ($xs[$i] - $mx) * ($ys[$i] - $my); $den += ($xs[$i] - $mx) * ($xs[$i] - $mx) }
    $slopePerDay = if ($den -gt 0) { 24 * $num / $den } else { 0 }
    $pollFailed = [int]$last.poll_failed; $pollOk = [int]$last.poll_ok
    $shutdownFile = Join-Path $Dir 'shutdown.txt'
    $shutdown = if (Test-Path $shutdownFile) { (Get-Content $shutdownFile -Raw).Trim() } else { 'not recorded' }
    $stopOk = $shutdown -match 'stop_ms=(\d+)' -and [int]$Matches[1] -le 15000 -and $shutdown -match 'process_left=no' -and $shutdown -match 'port=free'
    $checks = [ordered]@{
        "memory grew $([math]::Round($memMb, 1)) MB ($([math]::Round($memPct, 1))%)" = -not ($memMb -gt 50 -and $memPct -gt 30)
        "handles / open files grew by $handles ($([math]::Round($handlePct, 1))%)" = -not ($handles -gt 100 -and $handlePct -gt 25)
        "threads grew by $threads" = $threads -le 15
        "$pollOk status requests answered, $pollFailed failed" = $pollFailed -eq 0 -and $pollOk -gt 0
        "the stop: $shutdown" = $stopOk
    }
    $verdict = if ($checks.Values -contains $false) { 'FAILED' } else { 'PASSED' }
    $lines = @("# MystTiq idle soak ($TargetName): $verdict", '',
        "Ran $([math]::Round($hoursRun, 2)) h, $($rows.Count) samples, from $($rows[0].utc) to $($last.utc). Growth and trend are measured from $($first.utc) (after a $([math]::Round($warmup * 60)) min warm-up). Memory trend: $([math]::Round($slopePerDay, 1)) MB per day.", '')
    foreach ($k in $checks.Keys) { $lines += "- [$(if ($checks[$k]) { 'x' } else { ' ' })] $k" }
    $lines += '', '| UTC | Hours | Private MB | Working set MB | Handles / files | Threads | Status ms |', '|---|---|---|---|---|---|---|'
    foreach ($r in $rows) { $lines += "| $($r.utc) | $($r.elapsed_hours) | $([math]::Round([double]$r.private_kb / 1024, 1)) | $([math]::Round([double]$r.rss_kb / 1024, 1)) | $($r.open_files) | $($r.threads) | $($r.status_ms) |" }
    Set-Content (Join-Path $Dir 'summary.md') ($lines -join "`n")
    $lines | Select-Object -First (5 + $checks.Count) | ForEach-Object { Write-Host $_ }
    return $verdict
}

if ($Action -eq 'Analyze') { $v = Write-SoakReport $OutputDir $Target; if ($v -ne 'PASSED') { exit 1 }; return }

if ($Target -eq 'Linux') {
    if (-not $RemoteName) { $RemoteName = "mysttiq-soak-$stamp" }
    $dir = "/home/$LinuxUser/$RemoteName"
    switch ($Action) {
        { $_ -in 'Run', 'Start' } {
            $work = Join-Path $OutputDir 'deploy'
            & dotnet publish (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $work 'app') | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish failed' }
            [IO.File]::WriteAllText((Join-Path $work 'Measure-MystTiqIdleSoak.sh'), [IO.File]::ReadAllText((Join-Path $root 'scripts\Measure-MystTiqIdleSoak.sh')).Replace("`r`n", "`n"))
            $tgz = Join-Path $OutputDir 'soak.tgz'
            # Windows' own tar: started from Git Bash, "tar" can be GNU tar, which reads C:\... as a remote host.
            $tar = if ($IsWindows) { Join-Path $env:SystemRoot 'System32\tar.exe' } else { 'tar' }
            Push-Location $work; & $tar -czf $tgz app Measure-MystTiqIdleSoak.sh; $tarExit = $LASTEXITCODE; Pop-Location
            if ($tarExit -ne 0 -or -not (Test-Path $tgz)) { throw "Packing the soak for the VM failed (tar exit $tarExit)." }
            Remove-Item $work -Recurse -Force
            ssh -n @sshArgs $remote "mkdir -p $dir"
            if ($LASTEXITCODE -ne 0) { throw "Cannot reach $remote over SSH (pass -LinuxHost if the VM moved)." }
            scp @sshArgs $tgz "${remote}:$dir/" | Out-Null
            Remove-Item $tgz
            $out = @(ssh -n @sshArgs $remote "cd $dir && tar -xzf soak.tgz && rm -f soak.tgz && bash Measure-MystTiqIdleSoak.sh start $Hours $SampleMinutes $PollSeconds $Port" 2>&1)
            $out | ForEach-Object { Write-Host $_ }
            Set-Content (Join-Path $OutputDir 'remote.txt') "$remote $dir"
            if (-not ($out -match '^started ')) { throw 'The Linux soak did not start.' }
            Write-Host "Collect later with: -Target Linux -Action Collect -RemoteName $RemoteName -OutputDir `"$OutputDir`""
            return
        }
        'Status' { ssh -n @sshArgs $remote "bash $dir/Measure-MystTiqIdleSoak.sh status"; return }
        'Collect' {
            $state = @(ssh -n @sshArgs $remote "test -f $dir/results/finished.txt && echo finished || echo running" 2>&1)
            if (-not ($state -match '^finished$')) { Write-Host "The soak is still running on the VM:"; ssh -n @sshArgs $remote "bash $dir/Measure-MystTiqIdleSoak.sh status"; exit 2 }
            scp @sshArgs "${remote}:$dir/results/*" $OutputDir | Out-Null
            ssh -n @sshArgs $remote "bash $dir/Measure-MystTiqIdleSoak.sh stop >/dev/null; rm -rf $dir"
            $v = Write-SoakReport $OutputDir 'Linux, per-user systemd unit'
            if ($v -ne 'PASSED') { exit 1 }
            return
        }
    }
}

# --- Windows ---------------------------------------------------------------------------------------------------------
if (-not $IsWindows) { throw 'The Windows soak runs on Windows; use -Target Linux for the VM.' }
if ($Action -ne 'Run') { throw "On Windows only -Action Run (and Analyze) apply." }
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }
$temp = Join-Path $OutputDir 'run'
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $fakeBuild = Join-Path $temp 'fakepal'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$fakeSource = Join-Path $temp 'fakepal-src'
Copy-Item (Join-Path $root 'scripts\Testing\FakePalServer') $fakeSource -Recurse
& dotnet build (Join-Path $fakeSource 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
Copy-Item "$fakeBuild\*" $serverRoot -Force
$iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Idle Soak`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
New-Item (Join-Path $serverRoot 'Pal\Saved\SaveGames\0\SoakWorld\Players') -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $serverRoot 'Pal\Saved\SaveGames\0\SoakWorld\Level.sav') ('x' * 4096)
& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.Api.Port = $Port
# The stand-in gets no console of its own (createNoWindow), so the console stop sent to the service does not reach it.
foreach ($s in $cfg.Servers) { $s.LaunchArguments = @("-port=$GamePort", '@mysttiq:createNoWindow=true') }
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$base = "http://127.0.0.1:$Port"
$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
$up = $false
for ($i = 0; $i -lt 80 -and -not $up; $i++) { try { Invoke-RestMethod "$base/healthz" -TimeoutSec 2 | Out-Null; $up = $true } catch { Start-Sleep -Milliseconds 250 } }
if (-not $up) { throw "the soak service is not answering. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }
Invoke-WebRequest "$base/api/v1/server/start" -Method Post -ContentType 'application/json' -SkipHttpErrorCheck -TimeoutSec 120 | Out-Null

$csv = Join-Path $OutputDir 'samples.csv'
Set-Content $csv 'utc,elapsed_hours,rss_kb,private_kb,open_files,threads,poll_ok,poll_failed,status_ms'
$http = [System.Net.Http.HttpClient]::new(); $http.Timeout = [TimeSpan]::FromSeconds(30)
$pollOk = 0; $pollFailed = 0
$clock = [Diagnostics.Stopwatch]::StartNew()
$end = [TimeSpan]::FromHours($Hours); $step = [TimeSpan]::FromMinutes($SampleMinutes); $nextSample = [TimeSpan]::Zero
while ($true) {
    if ($clock.Elapsed -ge $nextSample) {
        $p = Get-Process -Id $svc.Id -ErrorAction SilentlyContinue
        if (-not $p) { Add-Content $csv "$([DateTime]::UtcNow.ToString('s'))Z,gone"; break }
        $t = [Diagnostics.Stopwatch]::StartNew()
        try { $http.GetStringAsync("$base/api/v1/status").GetAwaiter().GetResult() | Out-Null; $ms = $t.ElapsedMilliseconds } catch { $ms = -1 }
        Add-Content $csv ("{0}Z,{1},{2},{3},{4},{5},{6},{7},{8}" -f [DateTime]::UtcNow.ToString('s'), [math]::Round($clock.Elapsed.TotalHours, 3), [int]($p.WorkingSet64 / 1KB), [int]($p.PrivateMemorySize64 / 1KB), $p.HandleCount, $p.Threads.Count, $pollOk, $pollFailed, $ms)
        $nextSample += $step
        if ($nextSample -gt $end) { break }
    }
    try { $http.GetStringAsync("$base/api/v1/status/poll?lines=500").GetAwaiter().GetResult() | Out-Null; $pollOk++ } catch { $pollFailed++ }
    Start-Sleep -Seconds $PollSeconds
}

# The stop: a console stop (Ctrl+Break), the way a console host ends it.
$fakeBefore = @(Get-Process PalServer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$serverRoot*" })
$t0 = [Diagnostics.Stopwatch]::StartNew()
& (Join-Path $root 'scripts\Testing\Send-MystTiqConsoleStop.ps1') -ProcessId $svc.Id
$exited = $svc.WaitForExit(15000); $stopMs = $t0.ElapsedMilliseconds
if (-not $exited) { Stop-Process -Id $svc.Id -Force -ErrorAction SilentlyContinue }
$portFree = $true
try { $l = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port); $l.Start(); $l.Stop() } catch { $portFree = $false }
$held = @(Get-ChildItem $runtime, $backupRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    try { $s = [IO.File]::Open($_.FullName, 'Open', 'ReadWrite', 'None'); $s.Dispose(); $false } catch { $true } })
Set-Content (Join-Path $OutputDir 'shutdown.txt') "stop_ms=$stopMs result=$(if ($exited) { 'exited' } else { 'killed' }) exit=$(if ($exited) { $svc.ExitCode } else { 'n/a' }) process_left=$(if ($exited) { 'no' } else { 'yes' }) port=$(if ($portFree) { 'free' } else { 'in-use' }) files_held=$($held.Count) palserver_running_at_stop=$($fakeBefore.Count)"
Get-Process PalServer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$serverRoot*" } | Stop-Process -Force -ErrorAction SilentlyContinue
$v = Write-SoakReport $OutputDir 'Windows, isolated api-run with the stand-in PalServer'
if ($held.Count -gt 0) { Write-Host "Files still held after the stop: $($held.FullName -join ', ')" -ForegroundColor Red; $v = 'FAILED' }
if ($v -ne 'PASSED') { exit 1 }
