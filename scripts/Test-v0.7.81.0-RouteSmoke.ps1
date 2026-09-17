[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18281
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.81.0: live-HTTP coverage for the redesigned "Set Up New Server" wizard's actual backend
# routes -- direct request ("please create a logic test to create a new server with all the steps,
# it should also have the option to setup a new world or clone, choose one of the preset settings
# like vanilla or quality of life, and choose a new port, etc."). Same isolated-sidecar-with-no-
# real-PalServer pattern as Test-v0.7.12.0/15.0/64.0-RouteSmoke.ps1, but pointed at a genuinely
# FRESH, empty ServerRoot (no pre-existing Palworld install at all) to match what "Set Up a New
# World" actually targets, rather than the pre-populated "missingServer" fixture those tests use.
#
# Deliberately does NOT run a real SteamCMD install -- that would download several GB from Steam on
# every test run, which doesn't fit a fast, repeatable smoke test. Instead this verifies the
# Install step's own status route (GET /server/distribution/status) responds honestly against a
# fresh, empty root, and leaves the real download itself to live/manual verification (as every
# other version this session has done before shipping SteamCMD-touching changes).

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.81.0-newserverwizard-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$freshServerRoot = Join-Path $temp 'fresh-server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $freshServerRoot -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
# CreateDefault (POST /palworld/config/defaults) copies ServerRoot\DefaultPalWorldSettings.ini as
# its starting template -- a real Palworld dedicated server install always ships this file, but
# this test deliberately never runs a real (multi-GB, slow) SteamCMD install, so it needs a minimal
# stand-in with the same OptionSettings=(...) format PalworldSettingsConfigurationService parses.
$defaultSettingsPath = Join-Path $freshServerRoot 'DefaultPalWorldSettings.ini'
Set-Content $defaultSettingsPath @'
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(Difficulty=None,DayTimeSpeedRate=1.000000,NightTimeSpeedRate=1.000000,ServerPlayerMaxNum=32,ServerName="Default Palworld Server",ServerDescription="",AdminPassword="",ServerPassword="",PublicPort=8211,RESTAPIPort=8212,RCONPort=25575)
'@
$log = Join-Path $temp 'headless.log'
$err = Join-Path $temp 'headless.err.log'
$proc = $null
$failures = @()
$portListener = $null

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green
    }
    catch {
        Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red
        $script:failures += $Name
    }
}

try {
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $freshServerRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err
    $base = "http://127.0.0.1:$Port"
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Sidecar did not become healthy. stdout=$(Get-Content $log -Raw -ErrorAction SilentlyContinue) stderr=$(Get-Content $err -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'Step 1 (Connect): /healthz reports the isolated fresh-root sidecar as up' {
        $health = Invoke-RestMethod "$base/healthz" -TimeoutSec 5
        if (-not $health) { throw "expected a truthy /healthz response" }
    }

    # v0.7.81.0: PortAvailabilityService.CheckAsync shells out to netstat on Windows (pre-existing
    # since v0.7.2.0, not something this version changed) to enumerate every bound UDP/TCP endpoint
    # on the whole machine -- inherently slower than a direct socket probe, and can genuinely take
    # a few seconds under real system load. A 5-second HttpClient timeout here was found live to be
    # too tight (intermittent timeouts, not a functional failure); 20 seconds gives real headroom
    # without masking an actual hang.
    Test-RouteSmoke 'Step 2 (World Source/Settings port): GET /diagnostics/port-check flags a genuinely occupied UDP port' {
        $occupiedPort = 18282
        $script:portListener = [System.Net.Sockets.UdpClient]::new($occupiedPort)
        try {
            $result = Invoke-RestMethod "$base/api/v1/diagnostics/port-check?port=$occupiedPort&protocol=UDP" -TimeoutSec 20
            if ($result.inUse -ne $true) { throw "expected inUse=true for a port this test just bound, got $($result.inUse)" }
        }
        finally { $script:portListener.Close(); $script:portListener = $null }
    }

    Test-RouteSmoke 'Step 2 (World Source/Settings port): GET /diagnostics/port-check reports a free UDP port as not in use' {
        $freePort = 18283
        $result = Invoke-RestMethod "$base/api/v1/diagnostics/port-check?port=$freePort&protocol=UDP" -TimeoutSec 20
        if ($result.inUse -ne $false) { throw "expected inUse=false for an unbound port, got $($result.inUse)" }
    }

    Test-RouteSmoke 'Step 3 (Install): GET /server/distribution reports an honest not-installed baseline on a fresh empty root' {
        $status = Invoke-RestMethod "$base/api/v1/server/distribution" -TimeoutSec 5
        if ($status.serverExecutableExists -ne $false) { throw "expected serverExecutableExists=false on a genuinely empty fresh root, got $($status.serverExecutableExists)" }
        if ($status.steamCmdExists -ne $false) { throw "expected steamCmdExists=false (the test's own missing-steamcmd.exe fixture doesn't exist), got $($status.steamCmdExists)" }
    }

    Test-RouteSmoke 'Step 4 (Set Up a New World): POST /palworld/config/defaults creates PalWorldSettings.ini on the fresh root' {
        $body = @{
            confirmCreate     = $true
            serverName        = 'Wizard Smoke Test Server'
            serverDescription = 'Created by Test-v0.7.81.0-RouteSmoke'
            adminPassword     = 'smoke-admin'
            serverPassword    = ''
            maximumPlayers    = 16
            gamePort          = 28215
            restPort          = 8215
        } | ConvertTo-Json -Compress
        $result = Invoke-RestMethod "$base/api/v1/palworld/config/defaults" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 10
        if ($result.success -ne $true) { throw "expected success=true creating first-run settings on an empty root, got: $($result.message)" }
    }

    Test-RouteSmoke 'Step 4 (Starter Preset): Load -> apply a QoL-style rate change -> Save persists through a reload (the same chain ApplyStarterPresetCommand uses)' {
        $loaded = Invoke-RestMethod "$base/api/v1/palworld/config" -TimeoutSec 5
        if ($loaded.exists -ne $true) { throw "expected the settings file created in the prior step to now load with exists=true" }
        $dayRate = $loaded.settings | Where-Object { $_.name -eq 'DayTimeSpeedRate' }
        if (-not $dayRate) { throw "DayTimeSpeedRate not found in the loaded settings -- can't simulate a QoL preset change" }
        # Mirrors GetQolPreset's own "Balanced QoL" value for this key (MainWindowViewModel.cs).
        $mutated = $loaded.settings | ForEach-Object {
            if ($_.name -eq 'DayTimeSpeedRate') { @{ name = $_.name; value = '0.85' } }
            else { @{ name = $_.name; value = $_.value } }
        }
        $saveBody = @{ settings = $mutated } | ConvertTo-Json -Compress -Depth 5
        $saveResult = Invoke-RestMethod "$base/api/v1/palworld/config" -Method Put -ContentType 'application/json' -Body $saveBody -TimeoutSec 10
        if ($saveResult.success -ne $true) { throw "expected success=true saving the preset-mutated settings, got: $($saveResult.message)" }
        $reloaded = Invoke-RestMethod "$base/api/v1/palworld/config" -TimeoutSec 5
        $reloadedRate = ($reloaded.settings | Where-Object { $_.name -eq 'DayTimeSpeedRate' }).value
        if ($reloadedRate -ne '0.85') { throw "expected DayTimeSpeedRate=0.85 to persist through save+reload, got '$reloadedRate'" }
    }

    Test-RouteSmoke 'Step 2 (Clone an Existing Server): POST /server/clone copies this fresh install into a brand-new profile with ports offset' {
        $cloneTargetRoot = Join-Path $temp 'clone-target-server'
        $body = @{
            newProfileId   = 'wizard-smoke-clone'
            newProfileName = 'Wizard Smoke Clone Target'
            newServerRoot  = $cloneTargetRoot
            portOffset     = 100
        } | ConvertTo-Json -Compress
        $result = Invoke-RestMethod "$base/api/v1/server/clone" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 30
        if ($result.success -ne $true) { throw "expected success=true cloning the fresh install, got: $($result.message)" }
        if ($result.filesCopied -lt 1) { throw "expected at least 1 file copied (PalWorldSettings.ini from the prior step), got $($result.filesCopied)" }
        $clonedIni = Join-Path $cloneTargetRoot 'Pal\Saved\Config\WindowsServer\PalWorldSettings.ini'
        if (-not (Test-Path $clonedIni)) { throw "expected the cloned settings file at $clonedIni" }
        $iniText = Get-Content $clonedIni -Raw
        if ($iniText -notmatch 'PublicPort=28315') { throw "expected the cloned PublicPort to be the source's 28215 offset by +100 = 28315; ini did not contain that value" }
    }
}
finally {
    if ($script:portListener) { $script:portListener.Close() }
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.81.0 new-server wizard smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.81.0 new-server wizard smoke gate passed." -ForegroundColor Green
