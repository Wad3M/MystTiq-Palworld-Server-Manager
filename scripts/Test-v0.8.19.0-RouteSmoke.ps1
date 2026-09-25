[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18319
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.8.19.0: every route declares its role. With authentication really on (a shared Owner token and named accounts,
# as a remote setup has), routes that used to have no role at all -- RCON commands, kick/ban, PalWorldSettings.ini,
# mods -- are refused below Admin, the Operator-level changes are allowed from Operator, reading stays open to
# Viewer, and an account limited to another server is refused everywhere on this one. Own FleetRoot, own port.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Headless sidecar not found: $Exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.8.19.0-roles-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $tokenFile = Join-Path $temp 'secrets\api-token'; $fleetRoot = Join-Path $temp 'fleet'
New-Item $runtime, $serverRoot, $backupRoot -ItemType Directory -Force | Out-Null
$failures = @()
$proc = $null

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Invoke-Api([string]$Method, [string]$Path, $Body, [string]$Token) {
    $headers = @{}
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 6 } else { $null }
    $r = Invoke-WebRequest "http://127.0.0.1:$Port$Path" -Method $Method -Headers $headers -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 30
    $parsed = $null
    if ($r.Content) { try { $parsed = $r.Content | ConvertFrom-Json } catch { } }
    @{ Status = [int]$r.StatusCode; Body = $parsed }
}
function Refused([hashtable]$r, [string]$needs) { $r.Status -eq 403 -and $r.Body.error -eq 'insufficient-role' -and $r.Body.required -eq $needs }
function Allowed([hashtable]$r) { $r.Status -ne 403 -and $r.Status -ne 401 }
function New-Password { -join ((1..20) | ForEach-Object { 'abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'[(Get-Random -Maximum 56)] }) }

try {
    & $Exe config-write-default --config $config --overwrite | Out-Null
    & $Exe api-token-create --token-file $tokenFile --overwrite | Out-Null
    $cfg = Get-Content $config -Raw | ConvertFrom-Json
    $cfg.api.Port = $Port
    $cfg.api.Authentication.Enabled = $true
    $cfg.api.Authentication.TokenFile = $tokenFile
    $cfg.FleetRoot = $fleetRoot
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $config
    $owner = (Get-Content $tokenFile -Raw).Trim()

    $proc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--steamcmd', (Join-Path $temp 'missing-steamcmd.exe'), '--backup-root', $backupRoot, '--runtime-root', $runtime) -WorkingDirectory (Split-Path $Exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'headless.log') -RedirectStandardError (Join-Path $temp 'headless.err.log')
    $health = $null
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { $health = Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2; if ($health) { break } } catch {}
    }
    if (-not $health) { throw "Sidecar did not become healthy. $(Get-Content (Join-Path $temp 'headless.err.log') -Raw -ErrorAction SilentlyContinue)" }

    $tokens = @{}
    foreach ($a in @(@{ n = 'viewer'; r = 0; s = $null }, @{ n = 'operator'; r = 1; s = $null }, @{ n = 'admin'; r = 2; s = $null }, @{ n = 'elsewhere'; r = 2; s = 'another-server' })) {
        $password = New-Password
        $created = Invoke-Api POST '/api/v1/security/users' @{ username = $a.n; displayName = $a.n; role = $a.r; password = $password; scopedServerProfileId = $a.s } $owner
        if ($created.Status -ne 200) { throw "could not create $($a.n): $($created.Status)" }
        $tokens[$a.n] = (Invoke-Api POST '/api/v1/auth/login' @{ username = $a.n; password = $password } $null).Body.token
        if (-not $tokens[$a.n]) { throw "could not sign in as $($a.n)" }
    }

    Test-RouteSmoke 'a Viewer reads (status, players, notifications, host, the fleet list) as before' {
        foreach ($path in '/api/v1/status', '/api/v1/players', '/api/v1/notifications', '/api/v1/host', '/api/v1/servers', '/api/v1/operations') {
            $r = Invoke-Api GET $path $null $tokens.viewer
            if (-not (Allowed $r)) { throw "$path refused a Viewer ($($r.Status))" }
        }
    }

    Test-RouteSmoke 'the routes that had no role are refused below Admin: RCON commands, kick/ban, PalWorldSettings.ini, mods, teleport' {
        $cases = @(
            @('POST', '/api/v1/rcon/command', @{ command = 'Info' }),
            @('POST', '/api/v1/players/someone/action', @{ action = 'kick'; reason = 'smoke' }),
            @('PUT', '/api/v1/palworld/config', @{ settings = @() }),
            @('POST', '/api/v1/mods/all/enabled', @{ enabled = $false }),
            @('POST', '/api/v1/players/someone/teleport-to-me', @{}))
        foreach ($who in 'viewer', 'operator') {
            foreach ($c in $cases) {
                $r = Invoke-Api $c[0] $c[1] $c[2] $tokens[$who]
                if (-not (Refused $r 'Admin')) { throw "$($c[0]) $($c[1]) as $who gave $($r.Status) $($r.Body | ConvertTo-Json -Compress)" }
            }
        }
    }

    Test-RouteSmoke 'an Admin gets past the role check on the same routes (the route itself answers)' {
        foreach ($c in @(@('POST', '/api/v1/rcon/command', @{ command = 'Info' }), @('PUT', '/api/v1/palworld/config', @{ settings = @() }))) {
            $r = Invoke-Api $c[0] $c[1] $c[2] $tokens.admin
            if (-not (Allowed $r)) { throw "$($c[0]) $($c[1]) refused an Admin ($($r.Status))" }
        }
    }

    Test-RouteSmoke 'Operator-level changes (save now, notifications, backup checks) are refused for a Viewer and allowed from Operator' {
        foreach ($c in @(@('POST', '/api/v1/world/save-now'), @('POST', '/api/v1/notifications/mark-all-read'), @('POST', '/api/v1/backups/verify-all'), @('POST', '/api/v1/crash-analyzer/analyze'))) {
            $v = Invoke-Api $c[0] $c[1] $null $tokens.viewer
            if (-not (Refused $v 'Operator')) { throw "$($c[1]) as Viewer gave $($v.Status)" }
            $o = Invoke-Api $c[0] $c[1] $null $tokens.operator
            if (-not (Allowed $o)) { throw "$($c[1]) refused an Operator ($($o.Status))" }
        }
    }

    Test-RouteSmoke 'an account limited to another server is refused on this one, reading included, on both paths' {
        foreach ($path in '/api/v1/status', '/api/v1/servers/default/status', '/api/v1/players') {
            $r = Invoke-Api GET $path $null $tokens.elsewhere
            if ($r.Status -ne 403 -or $r.Body.error -ne 'server-scope-mismatch') { throw "$path gave $($r.Status) $($r.Body | ConvertTo-Json -Compress)" }
        }
    }

    Test-RouteSmoke 'the shared Owner token and sign-in itself still work' {
        if (-not (Allowed (Invoke-Api POST '/api/v1/rcon/command' @{ command = 'Info' } $owner))) { throw 'the Owner token was refused' }
        if ((Invoke-Api GET '/api/v1/security/whoami' $null $tokens.viewer).Status -ne 200) { throw 'whoami refused a signed-in Viewer' }
        if ((Invoke-Api GET '/api/v1/status' $null $null).Status -ne 401) { throw 'no token was not refused' }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.8.19.0 route roles smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.8.19.0 route roles smoke gate passed." -ForegroundColor Green
