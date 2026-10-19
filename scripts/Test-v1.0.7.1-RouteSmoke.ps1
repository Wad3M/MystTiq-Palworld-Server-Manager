# MystTiq v1.1.1.1: file reviewed for this release (2026-10-18).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18672,
    [int]$GamePort = 18772
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.7.1 (roadmap Q-4): one authority for conflicting work, through a real isolated service and a stand-in PalServer
# that never becomes ready (so a start holds its locks for the whole startup timeout):
#   1. While a start runs, a manual backup, a MOD change and a SteamCMD update are each refused with the reason, and
#      nothing they would change is touched.
#   2. A backup and a start sent at the same moment never run at the same time: one is refused, or their operations
#      in the history do not overlap.
#   3. Alone, a backup runs, as an operation in the history.
if (-not $IsWindows) { Write-Host '[SKIP] The conflict smoke uses the Windows stand-in PalServer.' -ForegroundColor Yellow; return }
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }

$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.7.1-conflicts-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $fakeBuild = Join-Path $temp 'fakepal'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$fakeSource = Join-Path $temp 'fakepal-src'
Copy-Item (Join-Path $root 'scripts\Testing\FakePalServer') $fakeSource -Recurse
& dotnet build (Join-Path $fakeSource 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
Copy-Item "$fakeBuild\*" $serverRoot -Force
$iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Conflict Smoke`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
$world = Join-Path $serverRoot 'Pal\Saved\SaveGames\0\ConflictSmokeWorld'
New-Item (Join-Path $world 'Players') -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $world 'Level.sav') ('x' * 4096)
$mods = Join-Path $serverRoot 'Pal\Binaries\Win64\ue4ss\Mods'
New-Item (Join-Path $mods 'Harmless\Scripts') -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $mods 'Harmless\Scripts\main.lua') "print('Harmless')"
$modsTxt = Join-Path $mods 'mods.txt'
Set-Content $modsTxt 'Harmless : 1'

& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.Api.Port = $Port
$cfg.Lifecycle.StartupTimeoutSeconds = 15; $cfg.Lifecycle.StopTimeoutSeconds = 5; $cfg.Lifecycle.RecoveryBackoffSeconds = 600
foreach ($s in $cfg.Servers) { $s.LaunchArguments = @("-port=$GamePort", '-notready') }
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$base = "http://127.0.0.1:$Port/api/v1"
$http = [System.Net.Http.HttpClient]::new(); $http.Timeout = [TimeSpan]::FromSeconds(120)
function Post-Async([string]$Path) { $http.PostAsync("$base$Path", [System.Net.Http.StringContent]::new('', [Text.Encoding]::UTF8, 'application/json')) }
function Result($Task) {
    $r = $Task.GetAwaiter().GetResult(); $text = $r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $(if ($text) { try { $text | ConvertFrom-Json } catch { $text } } else { $null }) }
}
function Call([string]$Method, [string]$Path) {
    $r = Invoke-WebRequest "$base$Path" -Method $Method -ContentType 'application/json' -SkipHttpErrorCheck -TimeoutSec 120
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $(if ($r.Content) { try { $r.Content | ConvertFrom-Json } catch { $r.Content } } else { $null }) }
}
function Fake { @(Get-Process PalServer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$serverRoot*" }) }
function Stop-Fake { Call POST '/server/force-stop' | Out-Null; Fake | Stop-Process -Force -ErrorAction SilentlyContinue }
function Wait-Starting {
    for ($i = 0; $i -lt 40; $i++) {
        $running = @((Call GET '/operations?max=20').Body | Where-Object { $_.kind -eq 'server-start' -and $_.phase -in 1, 'Running' })
        if ($running.Count -gt 0) { return }
        Start-Sleep -Milliseconds 250
    }
    throw 'the start did not begin'
}
$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    $up = $false
    for ($i = 0; $i -lt 80 -and -not $up; $i++) { try { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 | Out-Null; $up = $true } catch { Start-Sleep -Milliseconds 250 } }
    if (-not $up) { throw "the service is not answering. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'while a start runs, a backup, a MOD change and a SteamCMD update are each refused with the reason, and nothing is changed' {
        $start = Post-Async '/server/start'
        Wait-Starting
        $backupsBefore = @(Get-ChildItem $backupRoot -File -ErrorAction SilentlyContinue).Count
        $modsBefore = Get-Content $modsTxt -Raw
        $backup = Call POST '/backups/create'
        $mod = Call POST '/mods/all/enabled?enabled=false'
        $update = Call POST '/server/distribution/update?validate=false'
        $startResult = Result $start
        Stop-Fake
        if ($backup.Status -ne 409 -or "$($backup.Body.message)" -notmatch 'A backup is refused while another operation on this server is in progress') { throw "backup: $($backup.Status) $($backup.Body.message)" }
        if ($mod.Status -ne 409 -or "$($mod.Body.message)" -notmatch 'Changing MOD files is refused while another operation') { throw "MOD change: $($mod.Status) $($mod.Body.message)" }
        if ($update.Status -ne 409 -or "$($update.Body.message)" -notmatch 'Updating the server files is refused while another operation') { throw "update: $($update.Status) $($update.Body.message)" }
        if (@(Get-ChildItem $backupRoot -File -ErrorAction SilentlyContinue).Count -ne $backupsBefore) { throw 'a backup was written' }
        if ((Get-Content $modsTxt -Raw) -ne $modsBefore) { throw 'mods.txt was changed' }
        Write-Host "    start: $($startResult.Status); backup: $($backup.Body.message)"
    }

    Test-RouteSmoke 'a backup and a start sent at the same moment never run at the same time' {
        Start-Sleep 2
        $tasks = @((Post-Async '/backups/create'), (Post-Async '/server/start'))
        $backup = Result $tasks[0]; $start = Result $tasks[1]
        Stop-Fake
        $ops = @((Call GET '/operations?max=50').Body)
        $b = @($ops | Where-Object { $_.kind -eq 'backup-create' } | Sort-Object createdUtc -Descending | Select-Object -First 1)
        $s = @($ops | Where-Object { $_.kind -eq 'server-start' } | Sort-Object createdUtc -Descending | Select-Object -First 1)
        $refused = ($backup.Status -eq 409) -or ($start.Status -eq 409)
        $separate = $b.Count -eq 1 -and $s.Count -eq 1 -and
            (([DateTimeOffset]$b[0].updatedUtc -le [DateTimeOffset]$s[0].createdUtc) -or ([DateTimeOffset]$s[0].updatedUtc -le [DateTimeOffset]$b[0].createdUtc))
        if (-not ($refused -or $separate)) { throw "both ran at once: backup $($backup.Status), start $($start.Status)" }
        Write-Host "    backup: $($backup.Status), start: $($start.Status) ($(if ($refused) { 'one refused' } else { 'one after the other' }))"
    }

    Test-RouteSmoke 'alone, a backup runs and is an operation in the history' {
        Start-Sleep 2
        $r = Call POST '/backups/create'
        if ($r.Status -ne 200) { throw "$($r.Status) $($r.Body.message)" }
        $op = @((Call GET '/operations?max=50').Body | Where-Object { $_.kind -eq 'backup-create' } | Sort-Object createdUtc -Descending | Select-Object -First 1)
        if ($op.Count -ne 1 -or $op[0].phase -notin 2, 'Completed') { throw "no completed backup-create operation (phase $($op[0].phase))" }
    }
}
finally {
    Stop-Fake
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(15000) | Out-Null }
    $http.Dispose()
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { Write-Host "$($failures.Count) route smoke check(s) failed." -ForegroundColor Red; exit 1 }
Write-Host 'All v1.0.7.1 route smoke checks passed.' -ForegroundColor Green
