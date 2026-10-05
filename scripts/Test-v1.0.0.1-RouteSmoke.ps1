# MystTiq v1.0.0.6: file reviewed for this release (2026-10-05).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18611,
    [int]$GamePort = 18711
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] The stuck-start smoke uses the Windows stand-in server.' -ForegroundColor Yellow
    return
}

# v1.0.0.1: the stuck-start protocol end to end, on isolated data (own FleetRoot, server root and ports) with the stand-in
# PalServer, which hangs (never opens its port) while the stand-in MOD "HangsStartup" is enabled:
#   1. With HangsStartup on, a start times out with the process left running, and the status says how long it has been
#      starting; a test is refused while that start is still within its normal time.
#   2. The test load starts once with every MOD off: ready, so "a MOD is the likely cause"; every MOD is switched back on and
#      the server is stopped.
#   3. The one-at-a-time test finds HangsStartup, leaves it off, and leaves the server running with the other MOD.
#   4. The addresses route reports this machine's local addresses and the server's port; the identity guard is on by default.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.0.1-stuck-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $fakeBuild = Join-Path $temp 'fakepal'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null

$fakeSource = Join-Path $temp 'fakepal-src'
Copy-Item (Join-Path $root 'scripts\Testing\FakePalServer') $fakeSource -Recurse
& dotnet build (Join-Path $fakeSource 'FakePalServer.csproj') -c Release -o $fakeBuild -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'FakePalServer did not build' }
Copy-Item "$fakeBuild\*" $serverRoot -Force
$iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Stuck Smoke`",PublicPort=$GamePort,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
New-Item (Join-Path $serverRoot 'Pal\Saved\SaveGames\0\StuckSmokeWorld\Players') -ItemType Directory -Force | Out-Null
$mods = Join-Path $serverRoot 'Pal\Binaries\Win64\ue4ss\Mods'
foreach ($name in 'Harmless', 'HangsStartup') {
    New-Item (Join-Path $mods "$name\Scripts") -ItemType Directory -Force | Out-Null
    Set-Content (Join-Path $mods "$name\Scripts\main.lua") "print('$name')"
}
$modsTxt = Join-Path $mods 'mods.txt'
Set-Content $modsTxt "Harmless : 1`nHangsStartup : 1"

& $Exe config-write-default --config $config --overwrite | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'the service could not write its configuration' }
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.api.Port = $Port
$cfg.Lifecycle.StartupTimeoutSeconds = 6; $cfg.Lifecycle.StopTimeoutSeconds = 5
foreach ($s in $cfg.Servers) { $s.LaunchArguments = @("-port=$GamePort") }
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$base = "http://127.0.0.1:$Port/api/v1"
function Fake { @(Get-Process PalServer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$serverRoot*" }) }
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) { try { if (& $Condition) { return $true } } catch { }; Start-Sleep -Milliseconds 400 }
    return $false
}
function Invoke-Api([string]$Method, [string]$Path) {
    $r = Invoke-WebRequest "$base$Path" -Method $Method -ContentType 'application/json' -SkipHttpErrorCheck -TimeoutSec 90
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null }) }
}
function Wait-Test([int]$Seconds) {
    $script:testStatus = $null
    $done = Wait-Until { $s = (Invoke-Api GET '/mods/safe-start/status').Body; $script:testStatus = $s; $s -and $s.completed } $Seconds
    if (-not $done) { throw "the test did not finish within $Seconds s: $($script:testStatus | ConvertTo-Json -Compress -Depth 4)" }
    return $script:testStatus
}
function ModLine([string]$Name) { (Get-Content $modsTxt | Where-Object { $_ -match "^\s*$Name\s*:" }) -replace '\s', '' }

$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 40)) { throw "the service is not answering. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'with the hanging MOD on, a start (given nothing but -port=, like a double-click) times out with the process left running, the status says how long it has been starting, and a test is refused within the normal start time' {
        $inventory = (Invoke-Api GET '/mods').Body
        $names = @($inventory.mods | Where-Object { $_.enabled } | ForEach-Object package)
        if (-not ($names -contains 'Harmless' -and $names -contains 'HangsStartup')) { throw "inventory: $($names -join ', ')" }
        $start = Invoke-Api POST '/server/start'
        if ($start.Status -eq 200 -and $start.Body.success) { throw 'the start succeeded although the stand-in hangs' }
        if (@(Fake).Count -eq 0) { throw 'the hanging process was not left running' }
        # v1.0.0.1 (the owner, 2026-10-04): a double-click passes no arguments and keeps players' characters; a script with
        # MystTiq's old arguments did not. By default only -port= reaches the server (this one is not on 8211).
        $launchArgs = @(Get-Content (Join-Path $serverRoot 'launch-args.txt') -ErrorAction SilentlyContinue | Where-Object { $_ -ne 'Pal' })
        if (($launchArgs -join ' ') -ne "-port=$GamePort") { throw "the server was started with [$($launchArgs -join ' ')], expected only -port=$GamePort" }
        $status = (Invoke-Api GET '/status').Body
        if ($status.ready -or -not $status.nativeStartedAt -or $status.startupStuck) { throw "status: ready=$($status.ready) startedAt=$($status.nativeStartedAt) stuck=$($status.startupStuck)" }
        if ($status.detail -notmatch '^PalServer is starting \(\d+ s\); UDP \d+ is not open yet\.$') { throw "detail: $($status.detail)" }
        $refused = Invoke-Api POST '/mods/safe-start?mode=testload'
        if ($refused.Status -ne 409 -or $refused.Body.message -notmatch 'A start that is stuck can be tested') { throw "refusal: $($refused.Status) $($refused.Body.message)" }
        $null = Invoke-Api POST '/server/stop'
        if (-not (Wait-Until { @(Fake).Count -eq 0 } 20)) { throw 'the hanging stand-in did not stop' }
    }

    Test-RouteSmoke 'the test load starts once with every MOD off: ready, so a MOD is the likely cause; every MOD is switched back on and the server is stopped' {
        $begin = Invoke-Api POST '/mods/safe-start?mode=testload'
        if ($begin.Status -ne 200) { throw "begin: $($begin.Status) $($begin.Body.message)" }
        $result = Wait-Test 60
        $first = @($result.results)[0]
        if (-not $result.success -or $result.mode -ne 'TestLoad' -or $first.package -ne '(no MODs)' -or -not $first.ok -or $first.detail -notmatch '^Ready in \d+ s\.$') { throw "result: $($result | ConvertTo-Json -Compress -Depth 4)" }
        if ($result.finalMessage -notmatch 'a MOD is the likely cause') { throw "final: $($result.finalMessage)" }
        if ((ModLine 'Harmless') -ne 'Harmless:1' -or (ModLine 'HangsStartup') -ne 'HangsStartup:1') { throw "mods.txt: $((Get-Content $modsTxt) -join ' | ')" }
        if (-not (Wait-Until { @(Fake).Count -eq 0 } 10)) { throw 'the server was left running after the test load' }
    }

    Test-RouteSmoke 'the one-at-a-time test finds the hanging MOD, leaves it off, and leaves the server running with the other one' {
        $begin = Invoke-Api POST '/mods/safe-start'
        if ($begin.Status -ne 200) { throw "begin: $($begin.Status) $($begin.Body.message)" }
        $result = Wait-Test 120
        $byPackage = @{}; foreach ($r in @($result.results)) { $byPackage[$r.package] = $r }
        if (-not $result.success -or -not $byPackage['(no MODs)'].ok -or -not $byPackage['Harmless'].ok -or $byPackage['HangsStartup'].ok) { throw "result: $($result | ConvertTo-Json -Compress -Depth 4)" }
        if ($byPackage['HangsStartup'].detail -notmatch 'Timed out') { throw "HangsStartup: $($byPackage['HangsStartup'].detail)" }
        if ($result.finalMessage -notmatch '^Left off: HangsStartup\.') { throw "final: $($result.finalMessage)" }
        if ((ModLine 'Harmless') -ne 'Harmless:1' -or (ModLine 'HangsStartup') -ne 'HangsStartup:0') { throw "mods.txt: $((Get-Content $modsTxt) -join ' | ')" }
        $status = (Invoke-Api GET '/status').Body
        if (-not $status.ready -or $status.startupStuck) { throw "the server is not running ready: ready=$($status.ready)" }
    }

    Test-RouteSmoke 'the addresses route gives this machine''s local addresses with the server''s port, and the identity guard is on by default' {
        $addresses = (Invoke-Api GET '/network/addresses').Body
        if ($addresses.gamePort -ne $GamePort -or @($addresses.local).Count -lt 1 -or @($addresses.local)[0].address -notmatch '^\d{1,3}(\.\d{1,3}){3}$') { throw "addresses: $($addresses | ConvertTo-Json -Compress -Depth 3)" }
        $guard = (Invoke-Api GET '/players/identity-guard').Body
        if (-not $guard.config.enabled -or -not $guard.config.kickMismatched) { throw "guard: $($guard | ConvertTo-Json -Compress -Depth 3)" }
    }
}
finally {
    try { $null = Invoke-Api POST '/server/stop' } catch { }
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(10000) | Out-Null }
    Fake | Stop-Process -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { throw "MystTiq v1.0.0.1 stuck-start smoke failed: $($failures.Count) check(s)." }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'MystTiq v1.0.0.1 stuck-start smoke passed.' -ForegroundColor Green
