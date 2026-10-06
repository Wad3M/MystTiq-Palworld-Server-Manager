# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18641,
    # A world to copy (the clone's), and the save tools that decode it; the smoke skips without them.
    [string]$WorldServerRoot = 'C:\GameServers\Palworld\Server-clone-second-local',
    [string]$ToolsSource = 'C:\GameServers\Palworld\Server\Tools'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.4.0 (roadmap S-1 remove an item, S-2 add an item; owner decision D-2: guarded edits only). A COPY of a real world
# (never the live one) in an isolated server folder, edited through the API with the real save tools:
#   1. A player's main inventory is read from the save.
#   2. S-1: one plain stack is removed; the inventory, decoded again, has exactly that slot gone.
#   3. S-2: a stack is added in a free slot; refused for an item the world holds nowhere as a plain stack.
#   4. An item with its own record is not removed; nothing is edited while a PalServer runs from the server folder.
#   5. Each edit took a fresh, checked safety backup; restoring the first one gives the pre-edit Level.sav byte for byte.
if (-not $IsWindows) { Write-Host '[SKIP] The inventory smoke uses Windows saves and tools.' -ForegroundColor Yellow; return }
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }
$settings = Join-Path $WorldServerRoot 'Pal\Saved\Config\WindowsServer\GameUserSettings.ini'
if (-not (Test-Path $settings) -or -not (Test-Path (Join-Path $ToolsSource 'palworld-plm-tools\convert.py'))) {
    Write-Host "[SKIP] The inventory smoke needs a world in $WorldServerRoot and the save tools in $ToolsSource." -ForegroundColor Yellow
    return
}
$worldId = ((Get-Content $settings -Raw) -split "`n" | Where-Object { $_ -match '^DedicatedServerName=(\w+)' } | ForEach-Object { $Matches[1] } | Select-Object -First 1)

$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.4.0-inventory-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'
New-Item $runtime, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$worldCopy = Join-Path $serverRoot "Pal\Saved\SaveGames\0\$worldId"
New-Item $worldCopy, (Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer') -ItemType Directory -Force | Out-Null
$source = Join-Path $WorldServerRoot "Pal\Saved\SaveGames\0\$worldId"
Copy-Item (Join-Path $source 'Level.sav'), (Join-Path $source 'LevelMeta.sav') $worldCopy -ErrorAction SilentlyContinue
Copy-Item (Join-Path $source 'Players') (Join-Path $worldCopy 'Players') -Recurse
Copy-Item $settings (Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer\GameUserSettings.ini')
New-Item -ItemType Junction -Path (Join-Path $serverRoot 'Tools') -Target $ToolsSource | Out-Null
$level = Join-Path $worldCopy 'Level.sav'

& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.Api.Port = $Port
# Crash recovery waits 10 minutes, so the PalServer killed below stays crashed (with its last-known process id) for the
# restore, instead of being started again by the service.
$cfg.Lifecycle.RecoveryBackoffSeconds = 600
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config
$base = "http://127.0.0.1:$Port/api/v1"
function Call([string]$Method, [string]$Path, $Body = $null) {
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 6 } else { $null }
    $r = Invoke-WebRequest "$base$Path" -Method $Method -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 600
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null }) }
}
function Hash([string]$Path) { (Get-FileHash $Path -Algorithm SHA256).Hash }
$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
$fake = $null
try {
    $up = $false
    for ($i = 0; $i -lt 80 -and -not $up; $i++) { try { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 | Out-Null; $up = $true } catch { Start-Sleep -Milliseconds 500 } }
    if (-not $up) { throw "the service is not answering. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }
    $script:player = $null; $script:stack = $null; $script:recorded = $null; $script:before = @()

    Test-RouteSmoke 'a player''s main inventory is read from the save (a copy of the clone''s world)' {
        foreach ($f in Get-ChildItem (Join-Path $worldCopy 'Players') -Filter '*.sav') {
            $view = (Call GET "/players/$($f.BaseName)/inventory").Body
            if (-not $view.available) { continue }
            $plain = @($view.slots | Where-Object { -not $_.hasOwnRecord -and $_.count -gt 0 })
            if ($plain.Count -gt 0 -and @($view.slots).Count -lt $view.capacity) {
                $script:player = $f.BaseName; $script:stack = $plain[0]; $script:before = @($view.slots)
                $script:recorded = @($view.slots | Where-Object hasOwnRecord) | Select-Object -First 1
                break
            }
        }
        if (-not $script:player) { throw 'no player with a plain stack and a free slot in this world' }
        Write-Host "    player $($script:player): $(@($script:before).Count) slots used, e.g. $($script:stack.itemId) x $($script:stack.count)"
    }
    $script:hash0 = Hash $level
    $script:firstBackup = $null
    Test-RouteSmoke 'S-1: one plain stack is removed, with a checked safety backup, and only that slot changes' {
        $preview = (Call POST '/players/inventory/preview' @{ playerId = $script:player; action = 'remove'; itemId = $script:stack.itemId; slotIndex = $script:stack.slotIndex }).Body
        if (-not $preview.canApply) { throw "preview: $($preview.findings -join ' ')" }
        $apply = Call POST '/players/inventory/apply' @{ token = $preview.token; confirmed = $true }
        if ($apply.Status -ne 200 -or -not $apply.Body.safetyBackup) { throw "apply: $($apply.Status) $($apply.Body.message)" }
        $script:firstBackup = $apply.Body.safetyBackup
        $after = @((Call GET "/players/$($script:player)/inventory").Body.slots)
        $expected = @($script:before | Where-Object { $_.slotIndex -ne $script:stack.slotIndex })
        if ((ConvertTo-Json $after -Compress) -ne (ConvertTo-Json $expected -Compress)) { throw 'the inventory is not exactly the old one minus that stack' }
        if ((Hash $level) -eq $script:hash0) { throw 'Level.sav did not change' }
    }
    Test-RouteSmoke 'S-2: a stack is added in a free slot; an item the world holds nowhere as a plain stack is refused' {
        $preview = (Call POST '/players/inventory/preview' @{ playerId = $script:player; action = 'add'; itemId = $script:stack.itemId; count = 5 }).Body
        if (-not $preview.canApply) { throw "preview: $($preview.findings -join ' ')" }
        $apply = Call POST '/players/inventory/apply' @{ token = $preview.token; confirmed = $true }
        if ($apply.Status -ne 200) { throw "apply: $($apply.Status) $($apply.Body.message)" }
        $after = @((Call GET "/players/$($script:player)/inventory").Body.slots)
        if (-not ($after | Where-Object { $_.itemId -eq $script:stack.itemId -and $_.count -eq 5 })) { throw 'the added stack is not there' }
        $refused = (Call POST '/players/inventory/preview' @{ playerId = $script:player; action = 'add'; itemId = 'Unobtainium_MystTiqTest'; count = 1 }).Body
        if ($refused.canApply -or ($refused.findings -join ' ') -notmatch 'no plain stack') { throw "unknown item: $($refused.findings -join ' ')" }
    }
    Test-RouteSmoke 'an item with its own record is not removed' {
        if (-not $script:recorded) { Write-Host '    (this player holds no item with its own record; covered by the logic harness)'; return }
        $preview = (Call POST '/players/inventory/preview' @{ playerId = $script:player; action = 'remove'; itemId = $script:recorded.itemId; slotIndex = $script:recorded.slotIndex }).Body
        if ($preview.canApply -or ($preview.findings -join ' ') -notmatch 'its own record') { throw "own record: $($preview.findings -join ' ')" }
    }
    Test-RouteSmoke 'nothing is edited while a PalServer runs from the server folder' {
        $fakeBuild = Join-Path $temp 'fakepal'; $fakeSource = Join-Path $temp 'fakepal-src'
        Copy-Item (Join-Path $root 'scripts\Testing\FakePalServer') $fakeSource -Recurse
        & dotnet build (Join-Path $fakeSource 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
        Copy-Item "$fakeBuild\*" $serverRoot -Force
        $script:fake = Start-Process (Join-Path $serverRoot 'PalServer.exe') -ArgumentList "-port=$($Port + 100)" -PassThru -WindowStyle Hidden
        Start-Sleep 3
        $preview = (Call POST '/players/inventory/preview' @{ playerId = $script:player; action = 'add'; itemId = $script:stack.itemId; count = 1 }).Body
        Stop-Process -Id $script:fake.Id -Force; $script:fake.WaitForExit(20000) | Out-Null; $script:fake = $null
        # The service sees the stop on its next status read, as in the v1.0.0.4 smoke; wait for it before going on. The
        # status keeps the killed process's id as last-known; v1.0.4.0's guards no longer count that as running.
        $clock = [Diagnostics.Stopwatch]::StartNew(); $stopped = $false
        while (-not $stopped -and $clock.Elapsed.TotalSeconds -lt 30) {
            $s = (Call GET '/status').Body; $stopped = -not $s.processes -and -not $s.ready
            if (-not $stopped) { Start-Sleep -Milliseconds 500 }
        }
        Write-Host "    service saw PalServer stopped after $([int]$clock.Elapsed.TotalMilliseconds) ms"
        if (-not $stopped) { throw 'the service still reports PalServer running 30 s after it was stopped' }
        if ($preview.canApply -or ($preview.findings -join ' ') -notmatch 'Stop PalServer first') { throw "while running: $($preview.findings -join ' ')" }
    }
    Test-RouteSmoke 'restoring the first edit''s safety backup gives the pre-edit Level.sav byte for byte' {
        # v1.0.4.0 fix: the killed server leaves a crashed status that still names its process; that must not block a restore.
        $s = (Call GET '/status').Body
        if ($s.processes -or $null -eq $s.nativeProcessId) { throw "expected a crashed status with a last-known process id, got phase $($s.phase), id $($s.nativeProcessId)" }
        Write-Host "    status before the restore: $($s.phase), last-known process $($s.nativeProcessId), no process running"
        $restore = Call POST "/backups/$($script:firstBackup)/restore" @{ confirmed = $true }
        if ($restore.Status -ne 200) { throw "restore: $($restore.Status) $($restore.Body.message)" }
        if ((Hash $level) -ne $script:hash0) { throw 'the restored Level.sav differs from the pre-edit one' }
        $back = @((Call GET "/players/$($script:player)/inventory").Body.slots)
        if ((ConvertTo-Json $back -Compress) -ne (ConvertTo-Json @($script:before) -Compress)) { throw 'the restored inventory differs' }
    }
    Test-RouteSmoke 'every edit is in the activity log' {
        $tail = (Call GET '/activity/tail?lines=300').Body
        $text = ($tail | ConvertTo-Json -Depth 6)
        if ($text -notmatch 'Item removed from a player' -or $text -notmatch 'Item added to a player') { throw 'the edits are not logged' }
    }
}
finally {
    if ($fake) { Stop-Process -Id $fake.Id -Force -ErrorAction SilentlyContinue }
    Stop-Process -Id $svc.Id -Force -ErrorAction SilentlyContinue
    Get-CimInstance Win32_Process -Filter "Name='PalServer.exe'" | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500
    $junction = Join-Path $serverRoot 'Tools'
    if (Test-Path $junction) { [IO.Directory]::Delete($junction) }
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
if ($failures.Count -gt 0) { throw "MystTiq v1.0.4.0 inventory smoke failed: $($failures -join '; ')" }
Write-Host 'MystTiq v1.0.4.0 inventory smoke gate passed.' -ForegroundColor Green
