[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18417,
    [int]$GamePort = 18517
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] The v0.8.17.0 host and priority smoke uses the Windows stand-in server; Linux is checked by the isolated Linux test.' -ForegroundColor Yellow
    return
}

# v0.8.17.0: the HOST tab's route and a server's process priority and eco mode, end to end on the published exe in
# service mode, with a stand-in PalServer (scripts/Testing/FakePalServer) on a non-8211 port. Own FleetRoot and ports;
# nothing touches real servers. Every priority claim is checked on the real process, not only on MystTiq's answer.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Headless sidecar not found: $Exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.17.0-host-" + [guid]::NewGuid().ToString('N'))
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
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Host Smoke`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.api.Port = $Port
$cfg.Lifecycle.ServicePollSeconds = 1; $cfg.Lifecycle.RecoveryBackoffSeconds = 1; $cfg.Lifecycle.MaximumRecoveryAttempts = 3
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
function Host-Page { Invoke-RestMethod "$base/host" -TimeoutSec 15 }
function Save-Policy([hashtable]$Policy) {
    Invoke-RestMethod "$base/resources/policy" -Method Put -ContentType 'application/json' -Body ($Policy | ConvertTo-Json) -TimeoutSec 15
}
function Real-Priority { (Get-Process -Id (Fake)[0].Id).PriorityClass.ToString() }

$svc = Start-Process -FilePath $Exe -ArgumentList @('service-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    if (-not (Wait-Until { @(Fake).Count -eq 1 } 30)) { throw 'the service did not start the stand-in server' }
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 20)) { throw 'the embedded API is not answering' }

    Test-RouteSmoke 'the HOST route reads this machine: processor, memory, the disk holding the install, adapters' {
        $page = Host-Page
        $h = $page.host
        if ($h.logicalProcessors -ne [Environment]::ProcessorCount) { throw "logical processors $($h.logicalProcessors), expected $([Environment]::ProcessorCount)" }
        if (-not $h.processorName) { throw 'no processor name' }
        if ($null -eq $h.cpuPercent -or $h.cpuPercent -lt 0 -or $h.cpuPercent -gt 100) { throw "CPU $($h.cpuPercent)" }
        $totalMb = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1MB)
        if ([math]::Abs($h.memoryTotalBytes / 1MB - $totalMb) -gt 64 -or $h.memoryAvailableBytes -gt $h.memoryTotalBytes) { throw "memory $($h.memoryTotalBytes) / $($h.memoryAvailableBytes), expected about $totalMb MB" }
        $drive = [IO.Path]::GetPathRoot($serverRoot)
        $disk = @($h.disks | Where-Object { $_.name -eq $drive })
        if ($disk.Count -ne 1 -or $disk[0].holds -notcontains 'install' -or $disk[0].freeBytes -le 0) { throw "the install's disk $drive is not marked: $($h.disks | ConvertTo-Json -Compress -Depth 4)" }
        # Found live: with a dozen data drives the server's own disk was one row among many; it comes first now.
        if (@($h.disks)[0].holds -notcontains 'install') { throw "the first disk listed is $(@($h.disks)[0].name), not the install's" }
        if (@($h.network).Count -lt 1) { throw 'no network adapter listed' }
        # Found live on the Hyper-V host: Windows lists each NDIS filter on an adapter as an adapter of its own.
        $filters = @($h.network | Where-Object { $_.name -match '(Filter|Scheduler|Extension)-\d{4}$' })
        if ($filters.Count -gt 0) { throw "filter layers listed as adapters: $(($filters | ForEach-Object name) -join ' | ')" }
    }

    Test-RouteSmoke 'the server processes are listed, and the default policy leaves their priority alone' {
        $r = (Host-Page).resources
        if (-not $r.running -or @($r.processes | Where-Object { $_.processId -eq (Fake)[0].Id }).Count -ne 1) { throw "the stand-in server is not listed: $($r | ConvertTo-Json -Compress -Depth 5)" }
        if ($r.policy.priority -ne 'Default' -or $r.policy.ecoMode -ne 'Off') { throw "default policy is $($r.policy | ConvertTo-Json -Compress)" }
        if ((Real-Priority) -ne 'Normal') { throw "priority changed to $(Real-Priority) with the default policy" }
    }

    Test-RouteSmoke 'saving a priority applies it to the real process at once, and it is kept on disk' {
        $saved = Save-Policy @{ priority = 'BelowNormal'; ecoMode = 'Off'; ecoAfterEmptyMinutes = 10 }
        if (-not $saved.success) { throw $saved.message }
        if ((Real-Priority) -ne 'BelowNormal') { throw "the process is $(Real-Priority)" }
        $onDisk = Get-Content (Join-Path $runtime 'resource-policy.json') -Raw | ConvertFrom-Json
        if ($onDisk.Priority -ne 'BelowNormal' -and $onDisk.priority -ne 'BelowNormal') { throw "on disk: $($onDisk | ConvertTo-Json -Compress)" }
        $proc = @((Host-Page).resources.processes | Where-Object { $_.processId -eq (Fake)[0].Id })[0]
        if ($proc.priority -ne 'BelowNormal') { throw "the HOST route reads $($proc.priority)" }
    }

    Test-RouteSmoke 'eco mode on: efficiency mode on the real process and below-normal priority, whatever priority was chosen' {
        $saved = Save-Policy @{ priority = 'High'; ecoMode = 'On'; ecoAfterEmptyMinutes = 10 }
        $r = $saved.snapshot
        $proc = @($r.processes | Where-Object { $_.processId -eq (Fake)[0].Id })[0]
        if (-not $r.ecoActive -or $proc.efficiency -ne 'On') { throw "eco $($r.ecoActive), efficiency $($proc.efficiency)" }
        if ((Real-Priority) -ne 'BelowNormal') { throw "the process is $(Real-Priority)" }
    }

    Test-RouteSmoke 'eco mode off: efficiency mode returns to the system default and the chosen priority applies' {
        $saved = Save-Policy @{ priority = 'AboveNormal'; ecoMode = 'Off'; ecoAfterEmptyMinutes = 10 }
        $proc = @($saved.snapshot.processes | Where-Object { $_.processId -eq (Fake)[0].Id })[0]
        if ($saved.snapshot.ecoActive -or $proc.efficiency -ne 'Default') { throw "eco $($saved.snapshot.ecoActive), efficiency $($proc.efficiency)" }
        if ((Real-Priority) -ne 'AboveNormal') { throw "the process is $(Real-Priority)" }
    }

    Test-RouteSmoke 'eco mode when empty never starts on a guess: with no way to read who is online it waits and says why' {
        $saved = Save-Policy @{ priority = 'AboveNormal'; ecoMode = 'WhenEmpty'; ecoAfterEmptyMinutes = 1 }
        if ($saved.snapshot.ecoActive -or $saved.snapshot.ecoReason -notmatch 'cannot be read') { throw "eco $($saved.snapshot.ecoActive): $($saved.snapshot.ecoReason)" }
        if ((Real-Priority) -ne 'AboveNormal') { throw "the process is $(Real-Priority)" }
    }

    Test-RouteSmoke 'a policy that is out of range is refused and the saved one stays' {
        try { Save-Policy @{ priority = 'High'; ecoMode = 'WhenEmpty'; ecoAfterEmptyMinutes = 0 } | Out-Null; throw 'accepted' }
        catch { if ($_.Exception.Message -eq 'accepted' -or $_.Exception.Response.StatusCode.value__ -ne 400) { throw "expected 400: $($_.Exception.Message)" } }
        if ((Invoke-RestMethod "$base/resources/policy").ecoAfterEmptyMinutes -ne 1) { throw 'the saved policy changed' }
    }

    Test-RouteSmoke 'after a crash the restarted server gets the policy again on the next tick' {
        $before = (Fake)[0].Id
        Fake | Stop-Process -Force
        if (-not (Wait-Until { @(Fake).Count -eq 1 -and (Fake)[0].Id -ne $before } 30)) { throw 'the server was not restarted' }
        if (-not (Wait-Until { (Real-Priority) -eq 'AboveNormal' } 25)) { throw "the restarted process stayed $(Real-Priority)" }
    }

    Test-RouteSmoke 'each change is written to the activity log once' {
        $text = Get-Content (Join-Path $runtime 'logs\MystTiq-Activity.log') -Raw
        if ($text -notmatch 'Process priority and eco mode saved' -or $text -notmatch 'priority set to AboveNormal') { throw 'the activity log lacks the save or the change' }
        if (([regex]::Matches($text, 'Eco mode on')).Count -ne 1) { throw "expected one 'Eco mode on' entry, found $(([regex]::Matches($text, 'Eco mode on')).Count)" }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    Fake | Stop-Process -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.17.0 host and priority smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.17.0 host and priority smoke gate passed." -ForegroundColor Green
