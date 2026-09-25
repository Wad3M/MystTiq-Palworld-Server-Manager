[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18314
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.114.0: named user accounts end to end through the PUBLISHED headless exe, with authentication
# really switched on (loopback, shared token from api-token-create, as a real remote setup has). Proves
# the whole chain on the real middleware: the Owner creates an account, the person signs in, their session
# token is accepted by every existing route check with their own role, a role change applies to the open
# session, the audit log names them, and disabling or signing out ends the session.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.114.0-users-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
$tokenFile = Join-Path $temp 'secrets\api-token'
$fleetRoot = Join-Path $temp 'fleet'
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
    $r = Invoke-WebRequest "http://127.0.0.1:$Port$Path" -Method $Method -Headers $headers -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 20
    $parsed = $null
    if ($r.Content) { try { $parsed = $r.Content | ConvertFrom-Json } catch { } }
    @{ Status = [int]$r.StatusCode; Body = $parsed }
}

try {
    & $exe config-write-default --config $config --overwrite | Out-Null
    & $exe api-token-create --token-file $tokenFile --overwrite | Out-Null
    $cfg = Get-Content $config -Raw | ConvertFrom-Json
    $cfg.api.Port = $Port
    $cfg.api.Authentication.Enabled = $true
    $cfg.api.Authentication.TokenFile = $tokenFile
    # Accounts, sessions and the fleet audit log live under FleetRoot. The default is machine-wide
    # (ProgramData), shared with the real app, so a smoke must always use its own.
    $cfg.FleetRoot = $fleetRoot
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $config
    $owner = (Get-Content $tokenFile -Raw).Trim()

    $args = @('api-run', '--config', $config, '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'headless.log') -RedirectStandardError (Join-Path $temp 'headless.err.log')
    $health = $null
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { $health = Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2; if ($health) { break } } catch {}
    }
    if (-not $health) { throw "Sidecar did not become healthy. $(Get-Content (Join-Path $temp 'headless.err.log') -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'authentication is really on: no token is 401' {
        if ($health.authentication -ne $true) { throw 'healthz does not report authentication on' }
        if ((Invoke-Api GET '/api/v1/security/users' $null $null).Status -ne 401) { throw 'an anonymous request was not refused' }
    }

    Test-RouteSmoke 'the Owner (shared token) creates named accounts; bad input is refused' {
        $a = Invoke-Api POST '/api/v1/security/users' @{ username = 'alice'; displayName = 'Alice A'; role = 'Operator'; password = 'alice password 1' } $owner
        $c = Invoke-Api POST '/api/v1/security/users' @{ username = 'carol'; role = 'Viewer'; password = 'carol password 1' } $owner
        $bad = Invoke-Api POST '/api/v1/security/users' @{ username = 'x y'; role = 'Viewer'; password = 'short' } $owner
        if ($a.Status -ne 200 -or $c.Status -ne 200 -or $bad.Status -ne 400) { throw "expected 200/200/400, got $($a.Status)/$($c.Status)/$($bad.Status)" }
        $script:aliceId = $a.Body.account.id
    }

    Test-RouteSmoke 'a wrong password is 401 with a message that does not say whether the account exists' {
        $wrong = Invoke-Api POST '/api/v1/auth/login' @{ username = 'alice'; password = 'nope nope nope' } $null
        $unknown = Invoke-Api POST '/api/v1/auth/login' @{ username = 'nobody'; password = 'nope nope nope' } $null
        if ($wrong.Status -ne 401 -or $wrong.Body.message -ne $unknown.Body.message) { throw "got $($wrong.Status): '$($wrong.Body.message)' vs '$($unknown.Body.message)'" }
    }

    Test-RouteSmoke 'signing in gives a session token the real middleware accepts, as Alice with her own role' {
        $login = Invoke-Api POST '/api/v1/auth/login' @{ username = 'alice'; password = 'alice password 1' } $null
        if ($login.Status -ne 200 -or -not $login.Body.token) { throw "sign-in failed: $($login.Status) $($login.Body.message)" }
        $script:alice = $login.Body.token
        $who = (Invoke-Api GET '/api/v1/security/whoami' $null $script:alice).Body
        if ($who.name -ne 'Alice A' -or $who.role -ne 'Operator') { throw "whoami says $($who.name) / $($who.role)" }
    }

    Test-RouteSmoke 'Operator routes work for Alice; Admin and Owner routes are refused (403)' {
        $kits = Invoke-Api GET '/api/v1/players/kits' $null $script:alice
        $mute = Invoke-Api POST '/api/v1/alerts/mute' @{ minutes = 0 } $script:alice
        $users = Invoke-Api GET '/api/v1/security/users' $null $script:alice
        if ($kits.Status -ne 200 -or $mute.Status -ne 403 -or $users.Status -ne 403) { throw "expected 200/403/403, got $($kits.Status)/$($mute.Status)/$($users.Status)" }
    }

    Test-RouteSmoke 'promoting Alice to Admin applies to her open session at once' {
        $up = Invoke-Api PUT "/api/v1/security/users/$script:aliceId" @{ displayName = 'Alice A'; role = 'Admin'; enabled = $true } $owner
        if ($up.Status -ne 200) { throw "update failed: $($up.Status)" }
        $mute = Invoke-Api POST '/api/v1/alerts/mute' @{ minutes = 0 } $script:alice
        if ($mute.Status -ne 200) { throw "the Admin route is still refused after promotion: $($mute.Status)" }
    }

    Test-RouteSmoke 'the audit log names Alice for what she did' {
        Start-Sleep -Milliseconds 500
        $hits = @(Get-ChildItem $fleetRoot -Recurse -File -Filter '*.jsonl' -ErrorAction SilentlyContinue | Select-String -SimpleMatch '"actor":"Alice A"')
        if ($hits.Count -eq 0) { $hits = @(Get-ChildItem $fleetRoot -Recurse -File | Select-String -SimpleMatch 'Alice A' | Where-Object { $_.Line -match 'alerts/mute' }) }
        if ($hits.Count -eq 0) { throw 'no audit entry attributed to Alice A' }
    }

    Test-RouteSmoke 'disabling Alice ends her session immediately (401)' {
        $null = Invoke-Api PUT "/api/v1/security/users/$script:aliceId" @{ displayName = 'Alice A'; role = 'Admin'; enabled = $false } $owner
        if ((Invoke-Api GET '/api/v1/security/whoami' $null $script:alice).Status -ne 401) { throw 'the disabled account''s session still works' }
    }

    Test-RouteSmoke 'signing out ends the session' {
        $login = Invoke-Api POST '/api/v1/auth/login' @{ username = 'carol'; password = 'carol password 1' } $null
        $carol = $login.Body.token
        if ((Invoke-Api GET '/api/v1/security/whoami' $null $carol).Status -ne 200) { throw 'carol could not use her session' }
        $null = Invoke-Api POST '/api/v1/auth/logout' $null $carol
        if ((Invoke-Api GET '/api/v1/security/whoami' $null $carol).Status -ne 401) { throw 'the session still works after sign-out' }
    }

    Test-RouteSmoke '5 wrong passwords lock the account even against the right one' {
        for ($i = 0; $i -lt 5; $i++) { $null = Invoke-Api POST '/api/v1/auth/login' @{ username = 'carol'; password = "wrong guess $i" } $null }
        $locked = Invoke-Api POST '/api/v1/auth/login' @{ username = 'carol'; password = 'carol password 1' } $null
        if ($locked.Status -ne 401 -or $locked.Body.message -notmatch 'Too many failed') { throw "expected the lockout, got $($locked.Status): $($locked.Body.message)" }
        # Clear this IP's own failure count so nothing after this is affected.
        $null = Invoke-Api GET '/api/v1/security/whoami' $null $owner
    }

    Test-RouteSmoke 'the users file on disk holds no password and the sessions file no token' {
        $users = Get-Content (Join-Path $fleetRoot 'security\users.json') -Raw
        $sessions = Get-Content (Join-Path $fleetRoot 'security\sessions.json') -Raw
        if ($users -match 'alice password 1|carol password 1') { throw 'a password is stored in plain text' }
        if ($script:alice -and $sessions.Contains($script:alice)) { throw 'a session token is stored in plain text' }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.114.0 multi-user login smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.114.0 multi-user login smoke gate passed." -ForegroundColor Green
