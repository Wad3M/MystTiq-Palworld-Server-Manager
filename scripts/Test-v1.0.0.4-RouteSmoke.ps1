# MystTiq v1.0.0.4: file reviewed for this release (2026-10-05).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18614,
    # Real Palworld backups (PlM saves) and the save tools that decode them; the smoke skips without them.
    [string]$BackupSource = 'C:\GameServers\Palworld\Backups',
    [string]$ToolsSource = 'C:\GameServers\Palworld\Server\Tools'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.0.4 (reported 2026-10-05: backup and restore "doesnt seem to work correctly. verify using the day of the game server
# after a restoration"). Real backups restored into an isolated server folder (never the live world), checked by the world's
# in-game day read from the restored Level.sav:
#   1. Every backup's day is read from its own Level.sav and listed; at least two differ.
#   2. Restoring one makes the world that backup's day ("as in the backup"), and the Dashboard shows it, current.
#   3. With a world file held open the restore fails, names the holder, and changes nothing; held briefly, it waits and succeeds.
#   4. A PalServer started outside MystTiq from the server's folder blocks a restore.
#   5. A world saved after the last decode is shown as not current, then read again.
#   6. Restores, failed and done, are in the activity log.
if (-not $IsWindows) { Write-Host '[SKIP] The restore smoke uses Windows saves and tools.' -ForegroundColor Yellow; return }
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }
$sources = @(Get-ChildItem $BackupSource -Filter 'Palworld_*.zip' -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 8)
if ($sources.Count -lt 2 -or -not (Test-Path (Join-Path $ToolsSource 'palworld-plm-tools\convert.py'))) {
    Write-Host "[SKIP] The restore smoke needs real backups in $BackupSource and the save tools in $ToolsSource." -ForegroundColor Yellow
    return
}

$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.0.4-restore-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$sources | ForEach-Object { Copy-Item $_.FullName $backupRoot }
# The tools are linked, not copied (122 MB); the link alone is removed at the end.
$toolsLink = Join-Path $serverRoot 'Tools'
New-Item -ItemType Junction -Path $toolsLink -Target $ToolsSource | Out-Null
$saveRoot = Join-Path $serverRoot 'Pal\Saved\SaveGames'
Expand-Archive (Join-Path $backupRoot $sources[-1].Name) -DestinationPath $saveRoot

& $Exe config-write-default --config $config --overwrite | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'the service could not write its configuration' }
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.api.Port = $Port
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$base = "http://127.0.0.1:$Port/api/v1"
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) { try { if (& $Condition) { return $true } } catch { }; Start-Sleep -Milliseconds 500 }
    return $false
}
function Invoke-Api([string]$Method, [string]$Path, $Body = $null) {
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 6 } else { $null }
    $r = Invoke-WebRequest "$base$Path" -Method $Method -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 180
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null }) }
}
function World { (Invoke-Api GET '/status/poll').Body.world }
function Day($x) { "Day $($x.worldDayNumber) $($x.worldTimeText)" }
function Active-Level { Get-ChildItem $saveRoot -Recurse -Filter 'Level.sav' | Where-Object { $_.FullName -notmatch '\\backup\\' } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1 }

$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
$fake = $null
try {
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 40)) { throw "the service is not answering. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }
    $script:items = @()

    Test-RouteSmoke 'every backup''s day is read from its own Level.sav and listed; at least two differ' {
        $ok = Wait-Until { $script:items = @((Invoke-Api GET '/backups').Body.items); @($script:items | Where-Object { $null -eq $_.worldDayNumber }).Count -eq 0 } 180
        if (-not $ok) { throw "days not read: $(@($script:items | ForEach-Object { "$($_.fileName)=$($_.worldDayNumber)" }) -join ', ')" }
        $distinct = @($script:items | ForEach-Object { Day $_ } | Sort-Object -Unique)
        if ($distinct.Count -lt 2) { throw "every backup has the same day: $($distinct -join ', ')" }
    }
    $script:a = $script:items[-1]
    $script:b = @($script:items | Where-Object { (Day $_) -ne (Day $script:a) })[0]

    Test-RouteSmoke 'restoring a backup makes the world that backup''s day, and the Dashboard shows it as current' {
        $r = Invoke-Api POST "/backups/$($script:b.fileName)/restore" @{ confirmed = $true }
        $expected = "The world is now $(Day $script:b), as in the backup."
        if ($r.Status -ne 200 -or $r.Body.message -notlike "*$expected*" -or $r.Body.restoredWorldDayNumber -ne $script:b.worldDayNumber) { throw "restore: $($r.Status) $($r.Body.message)" }
        $w = World
        if ((Day $w) -ne (Day $script:b) -or -not $w.worldClockCurrent) { throw "Dashboard: $(Day $w) current=$($w.worldClockCurrent)" }
    }

    Test-RouteSmoke 'with a world file held open the restore fails, names the holder and changes nothing; held for a moment it waits and succeeds' {
        $level = Active-Level
        $handle = [IO.File]::Open($level.FullName, 'Open', 'Read', 'Read')
        try {
            $r = Invoke-Api POST "/backups/$($script:a.fileName)/restore" @{ confirmed = $true }
            if ($r.Status -ne 409 -or $r.Body.message -notmatch 'nothing was restored' -or $r.Body.message -notmatch "pid $PID\b") { throw "held: $($r.Status) $($r.Body.message)" }
        }
        finally { $handle.Dispose() }
        if ((Day (World)) -ne (Day $script:b)) { throw "the world changed: $(Day (World))" }
        $handle = [IO.File]::Open((Active-Level).FullName, 'Open', 'Read', 'Read')
        $client = [Net.Http.HttpClient]::new(); $client.Timeout = [TimeSpan]::FromMinutes(3)
        $content = [Net.Http.StringContent]::new('{"confirmed":true}', [Text.Encoding]::UTF8, 'application/json')
        $pending = $client.PostAsync("$base/backups/$($script:a.fileName)/restore", $content)
        Start-Sleep -Milliseconds 2000
        $handle.Dispose()
        $response = $pending.GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        $client.Dispose()
        if ([int]$response.StatusCode -ne 200 -or $body.message -notlike "*The world is now $(Day $script:a), as in the backup.*") { throw "brief hold: $([int]$response.StatusCode) $($body.message)" }
    }

    Test-RouteSmoke 'a PalServer started outside MystTiq from the server''s folder blocks a restore' {
        $fakeBuild = Join-Path $temp 'fakepal'; $fakeSource = Join-Path $temp 'fakepal-src'
        Copy-Item (Join-Path $root 'scripts\Testing\FakePalServer') $fakeSource -Recurse
        & dotnet build (Join-Path $fakeSource 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
        Copy-Item "$fakeBuild\*" $serverRoot -Force
        $script:fake = Start-Process (Join-Path $serverRoot 'PalServer.exe') -ArgumentList '-port=18714' -PassThru -WindowStyle Hidden
        Start-Sleep -Milliseconds 1500
        $r = Invoke-Api POST "/backups/$($script:b.fileName)/restore" @{ confirmed = $true }
        # MystTiq's own status may already see it ("Stop PalServer…"); otherwise the new check names the process.
        if ($r.Body.success -or $r.Body.message -notmatch "Stop PalServer before restoring|process $($script:fake.Id)\b.*started outside MystTiq") { throw "outside: $($r.Status) $($r.Body.message)" }
        # Stopped through MystTiq, as a user would: killing it counts as a crash, and crash recovery starts it again.
        $null = Invoke-Api POST '/server/stop'
        $script:fake.WaitForExit(20000) | Out-Null; $script:fake = $null
        $script:lastStatus = $null
        if (-not (Wait-Until { $s = (Invoke-Api GET '/status').Body; $script:lastStatus = $s; -not $s.processes -and -not $s.ready -and $null -eq $s.nativeProcessId } 30)) {
            throw "the stand-in is still reported after it exited: $($script:lastStatus | ConvertTo-Json -Compress -Depth 3)"
        }
    }

    # As on the owner's server (2026-10-05): SaveGames belonged to administrators, so the service, running as the user,
    # could not move it. Here the user is denied DELETE on the folder and delete-child on Pal\Saved (Windows grants DELETE
    # through the parent's delete-child otherwise, and on the owner's server Pal\Saved gave users none); both are removed afterwards.
    $script:user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $script:saved = Split-Path $saveRoot -Parent
    Test-RouteSmoke 'a save folder MystTiq may not replace is reported in the backups list and before a restore, which then changes nothing' {
        & icacls $saveRoot /deny "$($script:user):(DE)" | Out-Null
        & icacls $script:saved /deny "$($script:user):(DC)" | Out-Null
        try {
            $inventory = (Invoke-Api GET '/backups').Body
            if ($inventory.saveFolderReplaceable -or -not $inventory.savedFolderPath) { throw "inventory: replaceable=$($inventory.saveFolderReplaceable) saved=$($inventory.savedFolderPath)" }
            $r = Invoke-Api POST "/backups/$($script:b.fileName)/restore" @{ confirmed = $true }
            if ($r.Status -ne 409 -or $r.Body.message -notmatch 'Fix Save Folder Access') { throw "restore: $($r.Status) $($r.Body.message)" }
            if ((Day (World)) -ne (Day $script:a)) { throw "the world changed: $(Day (World))" }
        }
        finally { & icacls $saveRoot /remove:d $script:user | Out-Null; & icacls $script:saved /remove:d $script:user | Out-Null }
        if (-not (Invoke-Api GET '/backups').Body.saveFolderReplaceable) { throw 'still reported after the deny was removed' }
    }

    Test-RouteSmoke 'a world saved after the last decode is shown as not current, then read again, through MystTiq''s own copy when the one beside it is read-only' {
        $beside = Join-Path (Active-Level).DirectoryName 'Level.sav.json'
        $besideTime = (Get-Item $beside).LastWriteTimeUtc
        & icacls $beside /deny "$($script:user):(W)" | Out-Null
        try {
            (Active-Level).LastWriteTimeUtc = [DateTime]::UtcNow
            $w = World
            if ($w.worldClockCurrent) { throw 'a newer Level.sav still counts as current' }
            if (-not (Wait-Until { (World).worldClockCurrent } 60)) { throw 'the newer save was not read again' }
            if ((Day (World)) -ne (Day $script:a)) { throw "day after the re-read: $(Day (World))" }
            if ((Get-Item $beside).LastWriteTimeUtc -ne $besideTime) { throw 'the read-only copy beside Level.sav was written' }
            if (-not (Get-ChildItem $temp -Recurse -Filter '*.Level.sav.json' -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\world-clock\\decoded\\' })) { throw 'MystTiq''s own copy was not written' }
        }
        finally { & icacls $beside /remove:d $script:user | Out-Null }
    }

    Test-RouteSmoke 'restores, failed and done, are in the activity log' {
        $tail = ((Invoke-Api GET '/activity/tail?lines=400').Body | ConvertTo-Json -Depth 6)
        if ($tail -notmatch 'Restored backup' -or $tail -notmatch 'Restore failed' -or $tail -notmatch 'Restore refused') { throw 'the activity log lacks the restores' }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(10000) | Out-Null }
    # With the service gone nothing restarts it: clear any stand-in started from this fixture.
    Get-Process PalServer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$serverRoot*" } | Stop-Process -Force -ErrorAction SilentlyContinue
    # Remove the link itself first, so nothing below it is touched.
    if (Test-Path $toolsLink) { [IO.Directory]::Delete($toolsLink, $false) }
}

if ($failures.Count -gt 0) { throw "MystTiq v1.0.0.4 restore smoke failed: $($failures.Count) check(s)." }
# The stopped service can hold its log files for a moment.
foreach ($try in 1..10) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue; if (-not (Test-Path $temp)) { break }; Start-Sleep -Milliseconds 500 }
Write-Host 'MystTiq v1.0.0.4 restore smoke passed.' -ForegroundColor Green
