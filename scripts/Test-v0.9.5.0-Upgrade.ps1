# MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    # The current service (the published sidecar by default) and the accepted baseline's service.
    [string]$Exe = '',
    [string]$BaselineExe = 'C:\GameServers\_Backups\MystTiqPalworldServer\v0.8.25.0\publish\headless\mysttiq-server.exe',
    [int]$Port = 18595,
    [int]$GamePort = 18695
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] The upgrade smoke uses the Windows stand-in server and the Windows baseline build.' -ForegroundColor Yellow
    return
}

# v0.9.5.0: upgrade and recovery, end to end on isolated data (own FleetRoot, server root, ports; the stand-in PalServer
# from scripts/Testing/FakePalServer). Nothing touches real servers.
#   1. The accepted baseline (v0.8.25.0) writes its configuration and is given settings and data through its API:
#      process priority, bandwidth, teleport points, the whitelist, an automation rule, a kit, alert rules, a backup.
#   2. The current version starts on the same configuration and data: every value the baseline returned is returned
#      unchanged (new fields may be added), and the baseline's backup is listed and verifies.
#   3. Backup and restore: a world changed after a backup comes back byte for byte.
#   4. Rollback: the baseline starts again on the data the current version used, and still reads every value.
#   5. A fresh setup: the current version writes a new configuration in an empty folder, starts, and creates the server's
#      settings from the game's defaults.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
foreach ($e in $Exe, $BaselineExe) { if (-not (Test-Path $e -PathType Leaf)) { throw "Service not found: $e" } }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.9.5.0-upgrade-" + [guid]::NewGuid().ToString('N'))
$fakeBuild = Join-Path $temp 'fakepal'
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null

# The stand-in is built from a copy of its project, so a build elsewhere cannot collide with this one.
$fakeSource = Join-Path $temp 'fakepal-src'
Copy-Item (Join-Path $root 'scripts\Testing\FakePalServer') $fakeSource -Recurse
& dotnet build (Join-Path $fakeSource 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
Copy-Item "$fakeBuild\*" $serverRoot -Force
$iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Upgrade Smoke`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
$saveRoot = Join-Path $serverRoot 'Pal\Saved\SaveGames\0\UpgradeSmokeWorld'
New-Item (Join-Path $saveRoot 'Players') -ItemType Directory -Force | Out-Null
$levelSav = Join-Path $saveRoot 'Level.sav'
[IO.File]::WriteAllBytes($levelSav, [byte[]](1..200 | ForEach-Object { $_ % 251 }))
Set-Content (Join-Path $saveRoot 'Players\0123456789ABCDEF0123456789ABCDEF.sav') 'upgrade-smoke-player'
$originalHash = (Get-FileHash $levelSav -Algorithm SHA256).Hash

# The baseline writes the configuration, as an existing installation would have it.
& $BaselineExe config-write-default --config $config --overwrite | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'the baseline could not write its configuration' }
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
function Invoke-Api([string]$Method, [string]$Path, $Body) {
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 8 } else { $null }
    $r = Invoke-WebRequest "$base$Path" -Method $Method -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 60
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null }) }
}
$script:svc = $null
function Start-Service([string]$Path, [string]$Tag) {
    $script:svc = Start-Process -FilePath $Path -ArgumentList @('service-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp "svc-$Tag.log") -RedirectStandardError (Join-Path $temp "svc-$Tag.err.log")
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 30)) { throw "the $Tag service is not answering" }
    (Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 5).version
}
function Stop-Service {
    try { $null = Invoke-Api POST '/server/stop' @{} } catch {}
    $null = Wait-Until { @(Fake).Count -eq 0 } 20
    if ($script:svc -and -not $script:svc.HasExited) { Stop-Process -Id $script:svc.Id -Force; $script:svc.WaitForExit(10000) | Out-Null }
    Fake | Stop-Process -Force -ErrorAction SilentlyContinue
    $null = Wait-Until { -not (Test-NetConnection 127.0.0.1 -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue) } 10
}

# Every value the old version returned must come back unchanged; new properties are allowed.
function Compare-Kept($Old, $New, [string]$At) {
    if ($null -eq $Old) { return @() }
    if ($Old -is [System.Management.Automation.PSCustomObject]) {
        if ($New -isnot [System.Management.Automation.PSCustomObject]) { return @("${At}: object became '$New'") }
        return @(foreach ($p in $Old.PSObject.Properties) {
            if ($p.Name -cmatch '(Utc|At|Time|Age|Ago)$|^(updated|checked|generated|elapsed|uptime|next|last)|^(recentUses|runs|history|claims|observedAt)$') { continue }
            $n = $New.PSObject.Properties[$p.Name]
            if (-not $n) { "${At}.$($p.Name): missing" } else { Compare-Kept $p.Value $n.Value "$At.$($p.Name)" }
        })
    }
    if ($Old -is [System.Array]) {
        $newArr = @($New)
        if ($newArr.Count -ne $Old.Count) { return @("${At}: $($Old.Count) items became $($newArr.Count)") }
        return @(for ($i = 0; $i -lt $Old.Count; $i++) { Compare-Kept $Old[$i] $newArr[$i] "$At[$i]" })
    }
    if ("$Old" -ne "$New") { return @("${At}: '$Old' became '$New'") }
    return @()
}
$routes = '/resources/policy', '/network/policy', '/teleport', '/players/whitelist', '/automation/rules', '/players/kits', '/alerts/rules', '/palworld/config'
# /network/policy also reports whether the saved policy is in Engine.ini yet (applied when the server starts); only the
# saved policy itself must be kept.
function Snapshot { $s = [ordered]@{}; foreach ($r in $routes) { $b = (Invoke-Api GET $r $null).Body; $s[$r] = if ($r -eq '/network/policy') { $b.policy } else { $b } }; $s }

try {
    $baselineVersion = Start-Service $BaselineExe 'baseline'
    if (-not (Wait-Until { @(Fake).Count -eq 1 } 30)) { throw 'the baseline did not start the stand-in server' }

    Test-RouteSmoke "the baseline ($baselineVersion) takes settings and data through its API" {
        $saves = @(
            (Invoke-Api PUT '/resources/policy' @{ priority = 'BelowNormal'; ecoMode = 'Off'; ecoAfterEmptyMinutes = 25; cores = '' }),
            (Invoke-Api PUT '/network/policy' @{ mode = 'Custom'; perPlayerMbps = 3; tickRate = 25; uploadBudgetMbps = 50 }),
            (Invoke-Api PUT '/teleport' @{ enabled = $true; commandPrefix = '!tp'; cooldownSeconds = 45; points = @(@{ name = 'spawn'; x = -358.5; y = 270; z = $null }, @{ name = 'base'; x = 1; y = 2; z = 3 }) }),
            (Invoke-Api PUT '/players/whitelist' @{ enabled = $true; entries = @(@{ playerId = 'upgrade-player'; label = 'Upgrade Smoke' }) }),
            (Invoke-Api POST '/automation/rules' @{ name = 'Upgrade smoke rule'; trigger = @{ kind = 'IdleEmpty'; idleThresholdMinutes = 40; jitterSeconds = 5 }; condition = @{ requireServerRunning = $false; requireServerStopped = $false }; action = @{ kind = 'StopServer' } })
        )
        $bad = @($saves | Where-Object { $_.Status -ne 200 -and $_.Status -ne 201 })
        if ($bad.Count) { throw "saves refused: $(($bad | ForEach-Object { "$($_.Status) $($_.Body | ConvertTo-Json -Compress -Depth 3)" }) -join ' | ')" }
        $k = Invoke-Api PUT '/players/kits' @{ autoGiftEnabled = $false; autoGiftKitId = ''; autoGiftEnabledAtUtc = $null; kits = @(@{ id = 'starter'; name = 'Starter'; entries = @(@{ type = 'Item'; id = 'Wood'; amount = 5 }) }) }
        if ($k.Status -ne 200) { throw "kits refused: $($k.Status) $($k.Body | ConvertTo-Json -Compress -Depth 4)" }
        $alerts = (Invoke-Api GET '/alerts/rules' $null).Body
        $alerts.reminderMinutes = 17
        $a = Invoke-Api PUT '/alerts/rules' $alerts
        if ($a.Status -ne 200) { throw "alert rules refused: $($a.Status) $($a.Body | ConvertTo-Json -Compress -Depth 4)" }
        $b = Invoke-Api POST '/backups/create' $null
        if ($b.Status -ne 200 -or -not $b.Body.success) { throw "backup failed: $($b.Status) $($b.Body.message)" }
    }
    $script:before = Snapshot
    $script:backupsBefore = @((Invoke-Api GET '/backups' $null).Body.items)
    Stop-Service

    $currentVersion = Start-Service $Exe 'current'
    Test-RouteSmoke "the current version ($currentVersion) starts on the baseline's configuration and data and returns every value unchanged" {
        $after = Snapshot
        $diff = @(foreach ($r in $routes) { Compare-Kept $script:before[$r] $after[$r] $r })
        if ($diff.Count) { throw ($diff | Select-Object -First 8) -join ' | ' }
        $script:afterUpgrade = $after
    }
    Test-RouteSmoke "the baseline's backup is listed and verifies after the upgrade" {
        $list = @((Invoke-Api GET '/backups' $null).Body.items)
        $diff = @(Compare-Kept $script:backupsBefore $list '/backups')
        if ($diff.Count) { throw ($diff | Select-Object -First 5) -join ' | ' }
        $name = @($list)[0].fileName
        $v = Invoke-Api POST "/backups/$name/verify" $null
        if ($v.Status -ne 200 -or $v.Body.success -eq $false) { throw "verify failed: $($v.Status) $($v.Body | ConvertTo-Json -Compress -Depth 3)" }
    }
    Test-RouteSmoke 'a world changed after a backup is restored byte for byte' {
        # The service may still be starting the stand-in when the stop arrives (seen once in the v0.9.9.0 gate: the stop
        # found nothing to stop, the stand-in came up a moment later and the restore was refused). The stop is repeated
        # until the server stays down.
        $down = $false
        for ($try = 0; $try -lt 4 -and -not $down; $try++) {
            $null = Invoke-Api POST '/server/stop' @{}
            if (-not (Wait-Until { @(Fake).Count -eq 0 } 20)) { throw 'the stand-in server did not stop' }
            Start-Sleep -Seconds 3
            $down = @(Fake).Count -eq 0
        }
        if (-not $down) { throw 'the stand-in server kept coming back after a stop' }
        $name =@((Invoke-Api GET '/backups' $null).Body.items)[0].fileName
        [IO.File]::WriteAllBytes($levelSav, [byte[]](1..50))
        $r = Invoke-Api POST "/backups/$name/restore" @{ confirmed = $true }
        if ($r.Status -ne 200 -or -not $r.Body.success) { throw "restore failed: $($r.Status) $($r.Body.message)" }
        $hash = (Get-FileHash $levelSav -Algorithm SHA256).Hash
        if ($hash -ne $originalHash) { throw "Level.sav after restore is $hash, expected $originalHash" }
    }
    Stop-Service

    $rolledBack = Start-Service $BaselineExe 'rollback'
    Test-RouteSmoke "rolling back to the baseline ($rolledBack) on the same data still reads every value" {
        $again = Snapshot
        $diff = @(foreach ($r in $routes) { Compare-Kept $script:before[$r] $again[$r] $r })
        if ($diff.Count) { throw ($diff | Select-Object -First 8) -join ' | ' }
    }
    Stop-Service

    # A fresh setup in an empty folder with the current version.
    $fresh = Join-Path $temp 'fresh'; $freshServer = Join-Path $fresh 'server'
    New-Item (Join-Path $freshServer 'Pal\Saved\Config\WindowsServer'), (Join-Path $fresh 'runtime'), (Join-Path $fresh 'backups'), (Join-Path $fresh 'fleet') -ItemType Directory -Force | Out-Null
    Copy-Item "$fakeBuild\*" $freshServer -Force
    Set-Content (Join-Path $freshServer 'DefaultPalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Default Palworld Server`",PublicPort=8211,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"`")"
    $freshConfig = Join-Path $fresh 'mysttiq.json'
    & $Exe config-write-default --config $freshConfig --overwrite | Out-Null
    $fc = Get-Content $freshConfig -Raw | ConvertFrom-Json
    $fc.FleetRoot = (Join-Path $fresh 'fleet'); $fc.api.Port = $Port
    foreach ($s in $fc.Servers) { $s.LaunchArguments = @("-port=$GamePort") }
    $fc | ConvertTo-Json -Depth 20 | Set-Content $freshConfig
    $script:svc = Start-Process -FilePath $Exe -ArgumentList @('service-run', '--config', $freshConfig, '--server-root', $freshServer, '--runtime-root', (Join-Path $fresh 'runtime'), '--backup-root', (Join-Path $fresh 'backups'), '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc-fresh.log') -RedirectStandardError (Join-Path $temp 'svc-fresh.err.log')
    Test-RouteSmoke 'a fresh setup in an empty folder starts and creates the server settings from the game''s defaults' {
        if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 30)) { throw 'the fresh service is not answering' }
        $r = Invoke-Api POST '/palworld/config/defaults' @{ confirmCreate = $true; serverName = 'Fresh Smoke'; serverDescription = 'fresh'; adminPassword = 'a-long-random-admin-secret'; serverPassword = ''; maximumPlayers = 8; gamePort = $GamePort; restPort = $GamePort + 1 }
        if ($r.Status -ne 200 -or -not $r.Body.success) { throw "creating settings failed: $($r.Status) $($r.Body | ConvertTo-Json -Compress -Depth 3)" }
        $ini = Get-Content (Join-Path $freshServer 'Pal\Saved\Config\WindowsServer\PalWorldSettings.ini') -Raw
        if ($ini -notmatch 'ServerName="Fresh Smoke"') { throw 'PalWorldSettings.ini does not carry the chosen name' }
    }
}
finally {
    Stop-Service
    Fake | Stop-Process -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { throw "MystTiq v0.9.5.0 upgrade smoke failed: $($failures.Count) check(s)." }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'MystTiq v0.9.5.0 upgrade smoke passed.' -ForegroundColor Green
