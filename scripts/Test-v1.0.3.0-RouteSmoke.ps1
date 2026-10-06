# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18631
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.3.0 (roadmap W-1, owner decision D-1: a read-only browser view, no write routes in the browser). An isolated
# service with authentication on (its own config, token, fleet and port), never the owner's:
#   1. /web is served without a token, with a strict content policy, and holds no data.
#   2. A browser sign-in with an existing account reads status, players and backups.
#   3. EVERY change route of the API (each POST, PUT and DELETE in LocalManagementApiHost.cs) is refused for that session.
#   4. The same account signed in normally (the desktop's way) can still change things: only the browser session is read-only.
#   5. Roles still apply to reading; signing out ends the session; with authentication off the browser view says it needs it.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }

$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.3.0-browser-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $tokenFile = Join-Path $temp 'secrets\api-token'
New-Item $runtime, $serverRoot, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
& $Exe config-write-default --config $config --overwrite | Out-Null
& $Exe api-token-create --token-file $tokenFile | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.FleetRoot = $fleetRoot; $cfg.Api.Port = $Port
$cfg.Api.Authentication.Enabled = $true; $cfg.Api.Authentication.TokenFile = $tokenFile
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config
$owner = (Get-Content $tokenFile -Raw).Trim()

$site = "http://127.0.0.1:$Port"
function Call([string]$Method, [string]$Path, [string]$Token, $Body = $null) {
    $headers = @{}; if ($Token) { $headers.Authorization = "Bearer $Token" }
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 6 } elseif ($Method -ne 'GET') { '{}' } else { $null }
    $r = Invoke-WebRequest "$site$Path" -Method $Method -Headers $headers -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 60
    $parsed = $null; try { if ($r.Content -and "$($r.Headers['Content-Type'])" -match 'json') { $parsed = $r.Content | ConvertFrom-Json } } catch { }
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $parsed; Raw = $r }
}
$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}

$svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
try {
    $up = $false
    for ($i = 0; $i -lt 80 -and -not $up; $i++) { try { Invoke-RestMethod "$site/healthz" -TimeoutSec 2 | Out-Null; $up = $true } catch { Start-Sleep -Milliseconds 500 } }
    if (-not $up) { throw "the service is not answering. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }
    foreach ($u in @(@{ u = 'browser-admin'; r = 'Admin' }, @{ u = 'browser-viewer'; r = 'Viewer' })) {
        $made = Call POST '/api/v1/security/users' $owner @{ username = $u.u; displayName = $u.u; role = $u.r; password = 'correct horse battery'; scopedServerProfileId = $null }
        if ($made.Status -ne 200) { throw "creating $($u.u) failed: HTTP $($made.Status) $($made.Raw.Content)" }
    }

    Test-RouteSmoke 'the page is served without a token, with a strict content policy, and holds no data' {
        $page = Call GET '/web' $null
        $csp = "$($page.Raw.Headers['Content-Security-Policy'])"
        if ($page.Status -ne 200 -or "$($page.Raw.Headers['Content-Type'])" -notmatch 'text/html' -or $csp -notmatch "default-src 'none'" -or $csp -notmatch "script-src 'self'") { throw "page: $($page.Status) $csp" }
        if ((Call GET '/web/app.js' $null).Status -ne 200 -or (Call GET '/web/app.css' $null).Status -ne 200) { throw 'script or style not served' }
        if ($page.Raw.Content -match 'browser-admin|serverProfileIds') { throw 'the page contains data' }
        if ((Call GET '/api/v1/servers/default/status' $null).Status -ne 401) { throw 'data is served without a token' }
    }
    $script:browser = $null
    Test-RouteSmoke 'a browser sign-in with an existing account reads status, players and backups' {
        $login = Call POST '/api/v1/auth/browser-login' $null @{ username = 'browser-admin'; password = 'correct horse battery' }
        if ($login.Status -ne 200 -or -not $login.Body.token) { throw "sign-in: $($login.Status)" }
        $script:browser = $login.Body.token
        foreach ($p in '/api/v1/servers/default/status', '/api/v1/servers/default/players', '/api/v1/servers/default/backups') {
            $r = Call GET $p $script:browser
            if ($r.Status -ne 200) { throw "$p -> $($r.Status)" }
        }
        if ((Call POST '/api/v1/auth/browser-login' $null @{ username = 'browser-admin'; password = 'wrong password!' }).Status -ne 401) { throw 'a wrong password was accepted' }
    }
    Test-RouteSmoke 'every change route of the API is refused for the browser session (403, read-only)' {
        $hostText = [IO.File]::ReadAllText((Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'))
        $routes = @([regex]::Matches($hostText, '(routes|app)\.Map(Post|Put|Delete)\("([^"]+)"') | ForEach-Object {
            $path = $_.Groups[3].Value -replace '\{[^}]+\}', 'x'
            if ($_.Groups[1].Value -eq 'routes') { $path = '/api/v1/servers/default/' + $path.TrimStart('/') }
            [pscustomobject]@{ Method = $_.Groups[2].Value.ToUpperInvariant(); Path = $path }
        } | Where-Object { $_.Path -notin '/api/v1/auth/logout', '/api/v1/auth/login', '/api/v1/auth/browser-login' } | Sort-Object Method, Path -Unique)
        if ($routes.Count -lt 100) { throw "only $($routes.Count) change routes found" }
        $let = @($routes | Where-Object { $r = Call $_.Method $_.Path $script:browser; -not ($r.Status -eq 403 -and $r.Body.error -eq 'read-only-browser-session') } | ForEach-Object { "$($_.Method) $($_.Path)" })
        if ($let.Count -gt 0) { throw "not refused: $($let -join ', ')" }
        Write-Host "    ($($routes.Count) change routes refused)"
    }
    Test-RouteSmoke 'the same account signed in the desktop''s way can still change things; roles still apply to reading' {
        $desktop = (Call POST '/api/v1/auth/login' $null @{ username = 'browser-admin'; password = 'correct horse battery' }).Body.token
        $r = Call POST '/api/v1/servers/default/notifications/mark-all-read' $desktop
        if ($r.Status -ne 200) { throw "desktop session write: $($r.Status)" }
        $viewer = (Call POST '/api/v1/auth/browser-login' $null @{ username = 'browser-viewer'; password = 'correct horse battery' }).Body.token
        if ((Call GET '/api/v1/servers/default/status' $viewer).Status -ne 200) { throw 'a viewer cannot read status' }
        if ((Call GET '/api/v1/security/users' $viewer).Status -ne 403) { throw 'a viewer read the account list' }
    }
    Test-RouteSmoke 'signing out ends the browser session' {
        if ((Call POST '/api/v1/auth/logout' $script:browser).Status -ne 200) { throw 'sign-out refused' }
        if ((Call GET '/api/v1/servers/default/status' $script:browser).Status -ne 401) { throw 'the session still works after signing out' }
    }
}
finally {
    Stop-Process -Id $svc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
if ($failures.Count -gt 0) { throw "MystTiq v1.0.3.0 browser view smoke failed: $($failures -join '; ')" }
Write-Host 'MystTiq v1.0.3.0 browser view smoke gate passed.' -ForegroundColor Green
