# MystTiq v1.0.9.0: file reviewed for this release (2026-10-12).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18674,
    [int]$GamePort = 18774
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.7.2 (roadmap Q-8, Q-12), through a real isolated service:
#   1. While a start runs (a stand-in PalServer that never becomes ready), the status says which step it is at and since
#      when; when the startup timeout ends, the result names the step it stopped at.
#   2. Verify All over many backups reports "N of M" while it runs, names the archive it cannot read, and the cancel
#      route stops it with the ones done so far kept.
if (-not $IsWindows) { Write-Host '[SKIP] The progress smoke uses the Windows stand-in PalServer.' -ForegroundColor Yellow; return }
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }

$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.7.2-progress-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $fakeBuild = Join-Path $temp 'fakepal'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$fakeSource = Join-Path $temp 'fakepal-src'
Copy-Item (Join-Path $root 'scripts\Testing\FakePalServer') $fakeSource -Recurse
& dotnet build (Join-Path $fakeSource 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
Copy-Item "$fakeBuild\*" $serverRoot -Force
$iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Progress Smoke`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
New-Item (Join-Path $serverRoot 'Pal\Saved\SaveGames\0\ProgressSmokeWorld\Players') -ItemType Directory -Force | Out-Null
# 60 backups of about 2 MB each that do not compress, so Verify All takes long enough to watch and cancel; one is broken.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$rng = [Random]::new(11)
for ($i = 0; $i -lt 60; $i++) {
    $zip = [IO.Compression.ZipFile]::Open((Join-Path $backupRoot ("Palworld_2026-10-08_10-00-{0:00}-000.zip" -f $i)), 'Create')
    $bytes = [byte[]]::new(2000000); $rng.NextBytes($bytes)
    $stream = $zip.CreateEntry('SaveGames/0/W/Level.sav', 'NoCompression').Open(); $stream.Write($bytes, 0, $bytes.Length); $stream.Dispose(); $zip.Dispose()
}
Set-Content (Join-Path $backupRoot 'Palworld_2026-10-08_09-00-00-000.zip') 'not a zip'

& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.Api.Port = $Port
$cfg.Lifecycle.StartupTimeoutSeconds = 8; $cfg.Lifecycle.StopTimeoutSeconds = 5; $cfg.Lifecycle.RecoveryBackoffSeconds = 600
foreach ($s in $cfg.Servers) { $s.LaunchArguments = @("-port=$GamePort", '-notready') }
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$base = "http://127.0.0.1:$Port/api/v1"
$http = [System.Net.Http.HttpClient]::new(); $http.Timeout = [TimeSpan]::FromSeconds(300)
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

    Test-RouteSmoke 'Q-8: while a start runs, the status says it is waiting for the game port and since when; the timed-out start names that step' {
        $start = Post-Async '/server/start'
        $seen = $null
        for ($i = 0; $i -lt 40 -and -not $seen; $i++) {
            $st = (Call GET '/status').Body
            if ($st.startupStage -eq 'Waiting for the game port') { $seen = $st } else { Start-Sleep -Milliseconds 250 }
        }
        $result = Result $start
        $after = (Call GET '/status').Body
        Stop-Fake
        if (-not $seen) { throw 'the status never showed the waiting step' }
        if (-not $seen.startupStageSinceUtc -or -not $seen.startupBeganUtc -or [DateTimeOffset]$seen.startupStageSinceUtc -lt [DateTimeOffset]$seen.startupBeganUtc) { throw 'the step has no times' }
        if ("$($result.Body.message)" -notmatch '^The start stopped at "Waiting for the game port": ') { throw "the result does not name the step: $($result.Status) $($result.Body.message)" }
        if ($after.startupStage) { throw "the step is still shown after the start: $($after.startupStage)" }
        Write-Host "    $($result.Body.message)"
    }

    Test-RouteSmoke 'Q-12: Verify All reports N of M while it runs, names the broken archive, and the cancel route stops it with the ones done kept' {
        Start-Sleep 1
        $verify = Post-Async '/backups/verify-all'
        $progress = $null
        for ($i = 0; $i -lt 400 -and -not $progress; $i++) {
            $scan = (Call GET '/backups').Body.scan
            if ($scan -and $scan.kind -eq 'Verifying backups' -and -not $scan.finishedUtc -and $scan.done -ge 2) { $progress = $scan } else { Start-Sleep -Milliseconds 50 }
        }
        $cancel = Call POST '/backups/scan/cancel'
        $result = Result $verify
        $final = (Call GET '/backups').Body.scan
        if (-not $progress -or $progress.total -ne 61) { throw "no progress seen while it ran ($($progress.done) of $($progress.total))" }
        if (-not $cancel.Body.cancelled -or -not $final.cancelled -or $final.done -ge 61) { throw "not cancelled: $($final.done) of $($final.total)" }
        if ("$($result.Body.message)" -notmatch '^Cancelled after \d+ of 61') { throw "result: $($result.Body.message)" }
        $full = Result (Post-Async '/backups/verify-all')
        $done = (Call GET '/backups').Body.scan
        if ($done.done -ne 61 -or @($done.unreadable).Count -ne 1 -or $done.unreadable[0].fileName -ne 'Palworld_2026-10-08_09-00-00-000.zip') { throw "a full run did not name the broken archive ($($done.done) of 61)" }
        Write-Host "    seen $($progress.done) of $($progress.total); $($result.Body.message); a full run named $($done.unreadable[0].fileName)"
    }
}
finally {
    Stop-Fake
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(15000) | Out-Null }
    $http.Dispose()
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { Write-Host "$($failures.Count) route smoke check(s) failed." -ForegroundColor Red; exit 1 }
Write-Host 'All v1.0.7.2 route smoke checks passed.' -ForegroundColor Green
