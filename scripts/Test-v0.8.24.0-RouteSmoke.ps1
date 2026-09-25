[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18424,
    [int]$GamePort = 18524
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] The v0.8.24.0 cores smoke uses the Windows stand-in server; Linux is checked by the isolated Linux test.' -ForegroundColor Yellow
    return
}

# v0.8.24.0: processor cores (affinity) and the policy applied at start, end to end on the published exe in service
# mode with the stand-in PalServer (scripts/Testing/FakePalServer). Own FleetRoot and ports; nothing touches real
# servers. Every claim is checked on the real process.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Headless sidecar not found: $Exe" }
$cores = [Environment]::ProcessorCount
if ($cores -lt 2) { Write-Host '[SKIP] The cores smoke needs at least two logical processors.' -ForegroundColor Yellow; return }
$allMask = if ($cores -ge 64) { [uint64]::MaxValue } else { ([uint64]1 -shl $cores) - 1 }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.24.0-cores-" + [guid]::NewGuid().ToString('N'))
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
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Cores Smoke`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
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
function Save-Policy([hashtable]$Policy) {
    Invoke-RestMethod "$base/resources/policy" -Method Put -ContentType 'application/json' -Body ($Policy | ConvertTo-Json) -TimeoutSec 15
}
function Real-Mask { [uint64](Get-Process -Id (Fake)[0].Id).ProcessorAffinity.ToInt64() }
function Real-Priority { (Get-Process -Id (Fake)[0].Id).PriorityClass.ToString() }
function Listed { @((Invoke-RestMethod "$base/host" -TimeoutSec 15).resources.processes | Where-Object { $_.processId -eq (Fake)[0].Id })[0] }

$svc = Start-Process -FilePath $Exe -ArgumentList @('service-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    if (-not (Wait-Until { @(Fake).Count -eq 1 } 30)) { throw 'the service did not start the stand-in server' }
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 20)) { throw 'the embedded API is not answering' }

    Test-RouteSmoke 'by default the server runs on every core, and the HOST route says so with the machine''s core count' {
        if ((Real-Mask) -ne $allMask) { throw "the process runs on mask $(Real-Mask), expected $allMask" }
        $r = (Invoke-RestMethod "$base/host" -TimeoutSec 15).resources
        if ($r.processorCount -ne [Math]::Min($cores, 64) -or (Listed).cores -ne "all $([Math]::Min($cores, 64))") { throw "processorCount $($r.processorCount), cores '$((Listed).cores)'" }
    }

    Test-RouteSmoke 'saving cores 0-1 pins the real process to them at once, and they are kept on disk' {
        $saved = Save-Policy @{ priority = 'Default'; ecoMode = 'Off'; ecoAfterEmptyMinutes = 10; cores = '0-1' }
        if (-not $saved.success) { throw $saved.message }
        if ((Real-Mask) -ne 3) { throw "the process runs on mask $(Real-Mask)" }
        if ((Listed).cores -ne '0, 1') { throw "the HOST route reads '$((Listed).cores)'" }
        $onDisk = Get-Content (Join-Path $runtime 'resource-policy.json') -Raw | ConvertFrom-Json
        if ($onDisk.Cores -ne '0-1') { throw "on disk: $($onDisk | ConvertTo-Json -Compress)" }
    }

    Test-RouteSmoke 'cores the machine does not have are refused and the saved list stays' {
        try { Save-Policy @{ priority = 'Default'; ecoMode = 'Off'; ecoAfterEmptyMinutes = 10; cores = "0-$($cores + 5)" } | Out-Null; throw 'accepted' }
        catch { if ($_.Exception.Message -eq 'accepted' -or $_.Exception.Response.StatusCode.value__ -ne 400) { throw "expected 400: $($_.Exception.Message)" } }
        if ((Invoke-RestMethod "$base/resources/policy").cores -ne '0-1' -or (Real-Mask) -ne 3) { throw 'the saved cores changed' }
    }

    Test-RouteSmoke 'a restart through MystTiq: the new process has the priority and cores as soon as the restart returns' {
        Save-Policy @{ priority = 'BelowNormal'; ecoMode = 'Off'; ecoAfterEmptyMinutes = 10; cores = '1' } | Out-Null
        $before = (Fake)[0].Id
        $result = Invoke-RestMethod "$base/server/restart" -Method Post -TimeoutSec 90
        if (-not (Wait-Until { @(Fake).Count -eq 1 -and (Fake)[0].Id -ne $before } 5)) { throw "no new process after the restart ($($result | ConvertTo-Json -Compress -Depth 4))" }
        if ((Real-Mask) -ne 2 -or (Real-Priority) -ne 'BelowNormal') { throw "the new process runs on mask $(Real-Mask) at $(Real-Priority)" }
    }

    Test-RouteSmoke 'clearing the list gives every core back to the process MystTiq pinned' {
        Save-Policy @{ priority = 'Default'; ecoMode = 'Off'; ecoAfterEmptyMinutes = 10; cores = '' } | Out-Null
        if ((Real-Mask) -ne $allMask) { throw "the process runs on mask $(Real-Mask), expected $allMask" }
        if ((Listed).cores -ne "all $([Math]::Min($cores, 64))") { throw "the HOST route reads '$((Listed).cores)'" }
    }

    Test-RouteSmoke 'each change is written to the activity log' {
        $text = Get-Content (Join-Path $runtime 'logs\MystTiq-Activity.log') -Raw
        if ($text -notmatch 'cores set to 0, 1' -or $text -notmatch 'cores set to all' -or $text -notmatch ', cores 0-1\.') { throw 'the activity log lacks the saved cores or a change' }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    Fake | Stop-Process -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.24.0 cores smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.24.0 cores smoke gate passed." -ForegroundColor Green
exit 0
