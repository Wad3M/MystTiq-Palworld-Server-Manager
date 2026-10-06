# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18612
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.0.2: unique player names through the service's routes, on isolated data (own FleetRoot, runtime and port):
#   1. On first use the names come from the players already known: each to the account seen first (case ignored), and a
#      name two accounts share goes to the earlier one; the guard is on and kicks by default.
#   2. A saved list is made consistent: one claim per name in any case, empty names dropped, a blocked name (no owner)
#      reserved, a bare SteamID64 made an account.
#   3. The saved list survives a restart and the known players are not taken again over it.
if (-not $Exe) {
    $exeName = if ($IsWindows) { 'mysttiq-server.exe' } else { 'mysttiq-server' }
    $Exe = Join-Path $root "artifacts\publish\desktop-win-x64\headless\$exeName"
}
if (-not (Test-Path $Exe -PathType Leaf)) {
    if (-not $IsWindows) { Write-Host "[SKIP] The unique-names smoke needs a published service: $Exe" -ForegroundColor Yellow; return }
    throw "Service not found: $Exe"
}
$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.0.2-names-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null

& $Exe config-write-default --config $config --overwrite | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'the service could not write its configuration' }
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.api.Port = $Port
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$base = "http://127.0.0.1:$Port/api/v1"
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) { try { if (& $Condition) { return $true } } catch { }; Start-Sleep -Milliseconds 400 }
    return $false
}
function Invoke-Api([string]$Method, [string]$Path, $Body = $null) {
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 8 } else { $null }
    $r = Invoke-WebRequest "$base$Path" -Method $Method -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 60
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null }) }
}
$script:svc = $null
function Start-Helper {
    $script:svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 40)) { throw "the service is not answering. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }
}
function Stop-Helper {
    if ($script:svc -and -not $script:svc.HasExited) { Stop-Process -Id $script:svc.Id -Force; $script:svc.WaitForExit(10000) | Out-Null }
}
function Claims($snapshot) { @($snapshot.config.claims | Sort-Object name | ForEach-Object { "$($_.name)=$($_.ownerId)$(if ($_.reserved) { '*' })" }) -join ', ' }

$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

try {
    # Where this profile keeps its player files: the first start writes name-guard.json there (no players known yet).
    Start-Helper
    $null = Invoke-Api GET '/players/name-guard'
    Stop-Helper
    $guardFile = @(Get-ChildItem $temp -Recurse -Filter 'name-guard.json' | Select-Object -First 1)
    if ($guardFile.Count -eq 0) { throw 'name-guard.json was not written on the first start' }
    $players = $guardFile[0].DirectoryName
    Remove-Item $guardFile[0].FullName -Force
    $now = [DateTimeOffset]::UtcNow
    function Record([string]$Id, [string]$Name, [string]$UserId, [int]$DaysAgo) {
        [ordered]@{ PlayerId = $Id; LastKnownName = $Name; SteamId = ''; UserId = $UserId; FirstSeenUtc = $now.AddDays(-$DaysAgo).ToString('o'); LastSeenUtc = $now.ToString('o');
            TotalSessions = 1; TotalPlaytimeMinutes = 10; CurrentlyOnline = $false; CurrentSessionStartUtc = $null }
    }
    ConvertTo-Json -Depth 4 -InputObject @(
        (Record 'A3835C7B000000000000000000000000' 'Melly' 'steam_76561198653223616' 9),
        (Record '014308E2000000000000000000000000' 'melly' 'steam_76561198000000002' 1),
        (Record '67D8D355000000000000000000000000' 'Wade' 'steam_76561197962020201' 5)) | Set-Content (Join-Path $players 'player-registry.json')
    Start-Helper

    Test-RouteSmoke 'on first use the known players own their names, each to the account seen first (case ignored); the guard is on and kicks by default' {
        $guard = (Invoke-Api GET '/players/name-guard').Body
        if (-not $guard.config.enabled -or -not $guard.config.kickDuplicates) { throw "config: $($guard.config | ConvertTo-Json -Compress)" }
        $claims = Claims $guard
        if ($claims -ne 'Melly=steam_76561198653223616, Wade=steam_76561197962020201') { throw "claims: $claims" }
    }

    Test-RouteSmoke 'a saved list keeps one claim per name in any case, drops empty names, reserves a blocked name and makes a bare SteamID64 an account' {
        $body = @{ enabled = $true; kickDuplicates = $false; claims = @(
            @{ name = ' Admin '; ownerId = ''; ownerName = ''; reserved = $false },
            @{ name = 'ADMIN'; ownerId = 'steam_76561198000000009'; ownerName = ''; reserved = $true },
            @{ name = 'Melly'; ownerId = 'steam_76561198653223616'; ownerName = 'Melly'; reserved = $false },
            @{ name = 'Wade'; ownerId = '76561197962020201'; ownerName = ''; reserved = $true },
            @{ name = '   '; ownerId = 'steam_1'; ownerName = ''; reserved = $false }) }
        $saved = Invoke-Api PUT '/players/name-guard' $body
        if ($saved.Status -ne 200) { throw "save: $($saved.Status)" }
        $claims = @($saved.Body.claims | Sort-Object name | ForEach-Object { "$($_.name)=$($_.ownerId)$(if ($_.reserved) { '*' })" }) -join ', '
        if ($claims -ne 'Admin=*, Melly=steam_76561198653223616, Wade=steam_76561197962020201*' -or $saved.Body.kickDuplicates) { throw "saved: $claims kick=$($saved.Body.kickDuplicates)" }
    }

    Test-RouteSmoke 'the saved list survives a restart, and the known players are not taken again over it' {
        Stop-Helper
        Start-Helper
        $guard = (Invoke-Api GET '/players/name-guard').Body
        $claims = Claims $guard
        if ($claims -ne 'Admin=*, Melly=steam_76561198653223616, Wade=steam_76561197962020201*' -or $guard.config.kickDuplicates) { throw "after restart: $claims kick=$($guard.config.kickDuplicates)" }
        if (@($guard.events).Count -ne 0) { throw "events: $(@($guard.events).Count)" }
    }
}
finally {
    Stop-Helper
}

if ($failures.Count -gt 0) { throw "MystTiq v1.0.0.2 unique-names smoke failed: $($failures.Count) check(s)." }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'MystTiq v1.0.0.2 unique-names smoke passed.' -ForegroundColor Green
