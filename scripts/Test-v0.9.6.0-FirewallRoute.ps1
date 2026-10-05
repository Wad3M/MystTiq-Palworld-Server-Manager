# MystTiq v1.0.1.0: file reviewed for this release (2026-10-05).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18598,
    [int]$GamePortMain = 18698,
    [int]$AdvertisedOther = 18699,
    [int]$GamePortOther = 18700
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] The firewall route smoke reads the Windows Firewall; Linux is covered by the logic harness.' -ForegroundColor Yellow
    return
}

# v0.9.6.0: two server profiles in one MystTiq service (api-run, as the Desktop runs it) on isolated data, neither started.
# "default" binds the port its ini advertises; "other" advertises one port and binds another (-port=), as a second server
# set up through the wizard did before this version. The firewall route must report the port each server binds, the
# Doctor must say which one does not match, and nothing here changes the firewall (only GET routes are called).
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Headless sidecar not found: $Exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.9.6.0-firewall-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'
$fleet = Join-Path $temp 'fleet'
$profiles = [ordered]@{
    default = @{ Advertised = $GamePortMain; Bound = $GamePortMain }
    other = @{ Advertised = $AdvertisedOther; Bound = $GamePortOther }
}
New-Item $fleet -ItemType Directory -Force | Out-Null
foreach ($id in $profiles.Keys) {
    $serverRoot = Join-Path $temp "server-$id"
    New-Item $serverRoot, (Join-Path $temp "runtime-$id"), (Join-Path $temp "backups-$id") -ItemType Directory -Force | Out-Null
    $iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
    Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Firewall $id`",PublicPort=$($profiles[$id].Advertised),RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"
}

& $Exe config-write-default --config $config --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleet; $cfg.api.Port = $Port
$template = $cfg.Servers[0] | ConvertTo-Json -Depth 10
$cfg.Servers = @(foreach ($id in $profiles.Keys) {
    $s = $template | ConvertFrom-Json
    $s.Id = $id
    foreach ($name in 'Name', 'DisplayName') { if ($s.PSObject.Properties[$name]) { $s.$name = "Firewall $id" } }
    $s.ServerRoot = Join-Path $temp "server-$id"
    $s.RuntimeRoot = Join-Path $temp "runtime-$id"
    if ($s.PSObject.Properties['BackupRoot']) { $s.BackupRoot = Join-Path $temp "backups-$id" }
    $s.LaunchArguments = @("-port=$($profiles[$id].Bound)")
    if ($s.PSObject.Properties['SteamCmdPath']) { $s.SteamCmdPath = Join-Path $temp 'none.exe' }
    $s
})
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config

$failures = @()
$base = "http://127.0.0.1:$Port/api/v1/servers"
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $until = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $until) { try { if (& $Condition) { return $true } } catch {}; Start-Sleep -Milliseconds 400 }
    return $false
}
function Finding([string]$Id, [string]$FindingId) {
    $report = Invoke-RestMethod "$base/$Id/diagnostics/report" -TimeoutSec 60
    @($report.findings | Where-Object { $_.id -eq $FindingId }) | Select-Object -First 1
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    if (-not (Wait-Until { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } 30)) { throw 'the service is not answering' }

    Test-RouteSmoke 'the firewall route reports the port each server binds (its -port=), read in seconds' {
        foreach ($id in $profiles.Keys) {
            $clock = [Diagnostics.Stopwatch]::StartNew()
            $status = Invoke-RestMethod "$base/$id/diagnostics/network/firewall" -TimeoutSec 60
            $clock.Stop()
            if ($status.port -ne $profiles[$id].Bound) { throw "$id reports port $($status.port), expected the bound port $($profiles[$id].Bound)" }
            if ($status.protocol -ne 'UDP' -or -not $status.supported) { throw "$id reports $($status.protocol), supported=$($status.supported)" }
            if ($status.error) { throw "$id could not read the firewall: $($status.error)" }
            if ([string]::IsNullOrWhiteSpace($status.summary) -or $status.summary -notmatch "UDP $($profiles[$id].Bound)") { throw "$id summary: $($status.summary)" }
            if ($clock.Elapsed.TotalSeconds -gt 15) { throw "$id took $([int]$clock.Elapsed.TotalSeconds) s" }
            Write-Host "    $id -> $($status.summary) ($([int]$clock.Elapsed.TotalMilliseconds) ms)"
        }
    }

    Test-RouteSmoke 'the Doctor says whether the port the server binds matches the port the ini advertises' {
        $same = Finding 'default' 'configuration-game-port'
        if (-not $same -or $same.state -ne 0) { throw "default: $($same | ConvertTo-Json -Compress)" }
        $differ = Finding 'other' 'configuration-game-port'
        if (-not $differ -or $differ.state -ne 1) { throw "other should be a warning: $($differ | ConvertTo-Json -Compress)" }
        if ($differ.evidence -notmatch "binds UDP $GamePortOther" -or $differ.evidence -notmatch "PublicPort says $AdvertisedOther") { throw "other evidence: $($differ.evidence)" }
        if ($differ.recommendation -notmatch "-port=$AdvertisedOther") { throw "other recommendation: $($differ.recommendation)" }
    }

    Test-RouteSmoke 'the Doctor reports the firewall for the bound port, with a fix only when MystTiq can make one' {
        $firewall = Finding 'other' 'network-firewall'
        if (-not $firewall) { throw 'no network-firewall finding' }
        if ($firewall.location -ne "UDP $GamePortOther") { throw "location: $($firewall.location)" }
        $fixable = $firewall.actionKind -eq 'allow-firewall'
        if ($fixable -ne [bool]$firewall.actionSupported) { throw "actionKind $($firewall.actionKind) against actionSupported $($firewall.actionSupported)" }
        if ($firewall.state -eq 0 -and $fixable) { throw 'an allowed port with no old rules offers no fix' }
        Write-Host "    other -> state $($firewall.state): $($firewall.evidence)"
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force }
}

if ($failures.Count -gt 0) { throw "MystTiq v0.9.6.0 firewall route smoke failed: $($failures.Count) check(s)." }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'MystTiq v0.9.6.0 firewall route smoke passed.' -ForegroundColor Green
