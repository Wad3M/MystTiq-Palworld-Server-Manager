# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18661,
    # A world to copy (the clone's), and the save tools that decode it; the smoke skips without them.
    [string]$WorldServerRoot = 'C:\GameServers\Palworld\Server-clone-second-local',
    [string]$ToolsSource = 'C:\GameServers\Palworld\Server\Tools'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.6.0 (roadmap S-3 add and remove a Pal; owner decisions D-2, D-7). A COPY of a real world (never the live one) in
# an isolated server folder, edited through the API with the real save tools:
#   1. A player's Pal box is read from the save, with the species that can be added.
#   2. A Pal is added (a copy of one of that species in a Pal box), then removed again; each time the box, decoded again,
#      has exactly that Pal more or less.
#   3. Refused: a species with no Pal in a Pal box, a Pal not in this player's box, any edit while a server runs.
#   4. Each edit took a fresh, checked safety backup; restoring the first one gives the pre-edit Level.sav byte for byte.
if (-not $IsWindows) { Write-Host '[SKIP] The Pal box smoke uses Windows saves and tools.' -ForegroundColor Yellow; return }
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }
$settings = Join-Path $WorldServerRoot 'Pal\Saved\Config\WindowsServer\GameUserSettings.ini'
if (-not (Test-Path $settings) -or -not (Test-Path (Join-Path $ToolsSource 'palworld-plm-tools\convert.py'))) {
    Write-Host "[SKIP] The Pal box smoke needs a world in $WorldServerRoot and the save tools in $ToolsSource." -ForegroundColor Yellow
    return
}
$worldId = ((Get-Content $settings -Raw) -split "`n" | Where-Object { $_ -match '^DedicatedServerName=(\w+)' } | ForEach-Object { $Matches[1] } | Select-Object -First 1)

$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.6.0-palbox-" + [guid]::NewGuid().ToString('N'))
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
    $script:player = $null; $script:box = $null
    Test-RouteSmoke 'a player''s Pal box is read from the save, with the species that can be added (a copy of the clone''s world)' {
        foreach ($f in Get-ChildItem (Join-Path $worldCopy 'Players') -Filter '*.sav') {
            $view = (Call GET "/players/$($f.BaseName)/palbox").Body
            if ($view.available -and @($view.pals).Count -gt 0 -and @($view.addableSpecies).Count -gt 0) { $script:player = $f.BaseName; $script:box = $view; break }
        }
        if (-not $script:player) { throw 'no player with a Pal box and a species to add in this world' }
        Write-Host "    player $($script:player): $(@($script:box.pals).Count) Pals of $($script:box.capacity) places; $(@($script:box.addableSpecies).Count) species can be added"
    }
    $script:hash0 = Hash $level
    $script:firstBackup = $null; $script:added = $null
    Test-RouteSmoke 'S-3 add: a Pal is added to the Pal box with a checked safety backup, and the box has exactly that Pal more' {
        $species = @($script:box.addableSpecies)[0]
        $preview = (Call POST '/players/palbox/preview' @{ playerId = $script:player; action = 'add'; species = $species }).Body
        if (-not $preview.canApply) { throw "preview: $($preview.findings -join ' ')" }
        $apply = Call POST '/players/palbox/apply' @{ token = $preview.token; confirmed = $true }
        if ($apply.Status -ne 200 -or -not $apply.Body.safetyBackup) { throw "apply: $($apply.Status) $($apply.Body.message)" }
        $script:firstBackup = $apply.Body.safetyBackup
        $after = @((Call GET "/players/$($script:player)/palbox").Body.pals)
        $new = @($after | Where-Object { $_.instanceId -notin @($script:box.pals.instanceId) })
        if ($after.Count -ne @($script:box.pals).Count + 1 -or $new.Count -ne 1 -or $new[0].species -ne $species) { throw "the box is not the old one plus one $species" }
        $script:added = $new[0]
        Write-Host "    $($apply.Body.message)"
    }
    Test-RouteSmoke 'S-3 remove: that Pal is removed again, and the box is exactly the one before' {
        $preview = (Call POST '/players/palbox/preview' @{ playerId = $script:player; action = 'remove'; instanceId = $script:added.instanceId }).Body
        if (-not $preview.canApply) { throw "preview: $($preview.findings -join ' ')" }
        $apply = Call POST '/players/palbox/apply' @{ token = $preview.token; confirmed = $true }
        if ($apply.Status -ne 200) { throw "apply: $($apply.Status) $($apply.Body.message)" }
        $after = @((Call GET "/players/$($script:player)/palbox").Body.pals)
        if ((ConvertTo-Json $after -Compress) -ne (ConvertTo-Json @($script:box.pals) -Compress)) { throw 'the box is not the one before the add' }
        Write-Host "    $($apply.Body.message)"
    }
    Test-RouteSmoke 'refused: a species with no Pal in a Pal box, and a Pal that is not in this player''s box' {
        $none = (Call POST '/players/palbox/preview' @{ playerId = $script:player; action = 'add'; species = 'MystTiqTestPal' }).Body
        if ($none.canApply -or ($none.findings -join ' ') -notmatch 'none to copy') { throw "unknown species: $($none.findings -join ' ')" }
        $elsewhere = (Call POST '/players/palbox/preview' @{ playerId = $script:player; action = 'remove'; instanceId = [guid]::NewGuid().ToString() }).Body
        if ($elsewhere.canApply -or ($elsewhere.findings -join ' ') -notmatch 'not in this player''s Pal box') { throw "not in the box: $($elsewhere.findings -join ' ')" }
    }
    Test-RouteSmoke 'nothing is edited while a PalServer runs from the server folder' {
        $fakeBuild = Join-Path $temp 'fakepal'; $fakeSource = Join-Path $temp 'fakepal-src'
        Copy-Item (Join-Path $root 'scripts\Testing\FakePalServer') $fakeSource -Recurse
        & dotnet build (Join-Path $fakeSource 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
        Copy-Item "$fakeBuild\*" $serverRoot -Force
        $script:fake = Start-Process (Join-Path $serverRoot 'PalServer.exe') -ArgumentList "-port=$($Port + 100)" -PassThru -WindowStyle Hidden
        Start-Sleep 3
        $preview = (Call POST '/players/palbox/preview' @{ playerId = $script:player; action = 'add'; species = @($script:box.addableSpecies)[0] }).Body
        Stop-Process -Id $script:fake.Id -Force; $script:fake.WaitForExit(20000) | Out-Null; $script:fake = $null
        $clock = [Diagnostics.Stopwatch]::StartNew(); $stopped = $false
        while (-not $stopped -and $clock.Elapsed.TotalSeconds -lt 30) { $s = (Call GET '/status').Body; $stopped = -not $s.processes -and -not $s.ready; if (-not $stopped) { Start-Sleep -Milliseconds 500 } }
        if ($preview.canApply -or ($preview.findings -join ' ') -notmatch 'Stop PalServer first') { throw "while running: $($preview.findings -join ' ')" }
    }
    Test-RouteSmoke 'restoring the first edit''s safety backup gives the pre-edit Level.sav byte for byte' {
        $restore = Call POST "/backups/$($script:firstBackup)/restore" @{ confirmed = $true }
        if ($restore.Status -ne 200) { throw "restore: $($restore.Status) $($restore.Body.message)" }
        if ((Hash $level) -ne $script:hash0) { throw 'the restored Level.sav differs from the pre-edit one' }
    }
    Test-RouteSmoke 'every Pal box edit is in the activity log' {
        $text = ((Call GET '/activity/tail?lines=300').Body | ConvertTo-Json -Depth 6)
        if ($text -notmatch 'Pal added to a player' -or $text -notmatch 'Pal removed from a player') { throw 'the edits are not logged' }
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
if ($failures.Count -gt 0) { throw "MystTiq v1.0.6.0 Pal box smoke failed: $($failures -join '; ')" }
Write-Host 'MystTiq v1.0.6.0 Pal box smoke passed.' -ForegroundColor Green
