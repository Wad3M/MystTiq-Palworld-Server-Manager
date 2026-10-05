# MystTiq v1.0.0.3: file reviewed for this release (2026-10-05).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [string]$BaselineExe = 'C:\GameServers\_Backups\MystTiqPalworldServer\v0.8.25.0\publish\headless\mysttiq-server.exe',
    [int]$Port = 18599
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] The accounts upgrade smoke uses the Windows baseline build.' -ForegroundColor Yellow
    return
}

# v0.9.8.0: accounts across an upgrade (v0.9.5.0's upgrade smoke ran without sign-in). On isolated data with
# authentication on (own FleetRoot, where accounts and sessions live; own token file and port), no game server needed:
#   1. The accepted baseline (v0.8.25.0) creates named accounts with the Owner token: an Operator, an Admin, a Viewer
#      whose password is then changed, and an Admin who is then disabled.
#   2. The current version starts on the same data: the account list is unchanged, every account signs in with its
#      password (the changed one with the new password only) and gets its own role, the disabled one is still refused,
#      and the Owner token still works.
#   3. An account created by the current version signs in after rolling back to the baseline, as do the others.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
foreach ($e in $Exe, $BaselineExe) { if (-not (Test-Path $e -PathType Leaf)) { throw "Service not found: $e" } }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.9.8.0-accounts-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $tokenFile = Join-Path $temp 'owner.token'
$serverRoot = Join-Path $temp 'server'; $runtime = Join-Path $temp 'runtime'; $backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'
New-Item $serverRoot, $runtime, $backupRoot, $fleetRoot -ItemType Directory -Force | Out-Null
$iniDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'; New-Item $iniDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $iniDir 'PalWorldSettings.ini') "[/Script/Pal.PalGameWorldSettings]`nOptionSettings=(ServerName=`"Accounts Smoke`",PublicPort=18799,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword=`"a-long-random-admin-secret`")"

& $BaselineExe config-write-default --config $config --overwrite | Out-Null
& $BaselineExe api-token-create --token-file $tokenFile --overwrite | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'the baseline could not write its configuration and Owner token' }
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.api.Port = $Port
$cfg.api.Authentication.Enabled = $true
$cfg.api.Authentication.TokenFile = $tokenFile
$cfg.FleetRoot = $fleetRoot
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config
$owner = (Get-Content $tokenFile -Raw).Trim()

$failures = @()
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
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $parsed }
}
function Sign-In([string]$User, [string]$Password) {
    $login = Invoke-Api POST '/api/v1/auth/login' @{ username = $User; password = $Password } $null
    if ($login.Status -ne 200 -or -not $login.Body.token) { return $null }
    $who = (Invoke-Api GET '/api/v1/security/whoami' $null $login.Body.token).Body
    [pscustomobject]@{ Token = $login.Body.token; Name = $who.name; Role = $who.role }
}
function Accounts { @((Invoke-Api GET '/api/v1/security/users' $null $owner).Body | ForEach-Object { $_ } | Sort-Object username | ForEach-Object { "$($_.username)|$($_.role)|$($_.enabled)|$($_.displayName)" }) }
$script:svc = $null
function Start-Api([string]$Path, [string]$Tag) {
    $script:svc = Start-Process -FilePath $Path -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--steamcmd', (Join-Path $temp 'none.exe'), '--backup-root', $backupRoot, '--runtime-root', $runtime) -WorkingDirectory (Split-Path $Path -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp "svc-$Tag.log") -RedirectStandardError (Join-Path $temp "svc-$Tag.err.log")
    for ($i = 0; $i -lt 80; $i++) {
        Start-Sleep -Milliseconds 250
        if ($script:svc.HasExited) { break }
        try { $h = Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2; if ($h) { return $h } } catch {}
    }
    throw "the $Tag service did not start. $(Get-Content (Join-Path $temp "svc-$Tag.err.log") -Raw -ErrorAction SilentlyContinue)"
}
function Stop-Api {
    if ($script:svc -and -not $script:svc.HasExited) { Stop-Process -Id $script:svc.Id -Force; $script:svc.WaitForExit(10000) | Out-Null }
    for ($i = 0; $i -lt 40; $i++) { try { $null = Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 1; Start-Sleep -Milliseconds 250 } catch { break } }
}

try {
    $baseline = Start-Api $BaselineExe 'baseline'
    Test-RouteSmoke "the baseline ($($baseline.version)) creates accounts, changes a password and disables an account" {
        if ($baseline.authentication -ne $true) { throw 'authentication is not on' }
        foreach ($u in @(
            @{ username = 'alice'; displayName = 'Alice A'; role = 'Operator'; password = 'alice password 1' },
            @{ username = 'bob'; displayName = 'Bob B'; role = 'Admin'; password = 'bob password 1' },
            @{ username = 'carol'; displayName = 'Carol C'; role = 'Viewer'; password = 'carol password 1' },
            @{ username = 'dave'; displayName = 'Dave D'; role = 'Admin'; password = 'dave password 1' })) {
            $r = Invoke-Api POST '/api/v1/security/users' $u $owner
            if ($r.Status -ne 200) { throw "creating $($u.username) failed: $($r.Status) $($r.Body.message)" }
            if ($u.username -eq 'dave') { $script:daveId = $r.Body.account.id }
        }
        $carol = Sign-In 'carol' 'carol password 1'
        if (-not $carol) { throw 'carol could not sign in on the baseline' }
        $pw = Invoke-Api POST '/api/v1/auth/password' @{ currentPassword = 'carol password 1'; newPassword = 'carol password 2' } $carol.Token
        if ($pw.Status -ne 200) { throw "carol's password change failed: $($pw.Status) $($pw.Body.message)" }
        $off = Invoke-Api PUT "/api/v1/security/users/$script:daveId" @{ displayName = 'Dave D'; role = 'Admin'; enabled = $false } $owner
        if ($off.Status -ne 200) { throw "disabling dave failed: $($off.Status)" }
        $script:before = Accounts
        if ($script:before.Count -ne 4) { throw "the baseline lists $($script:before.Count) accounts" }
    }
    Stop-Api

    $current = Start-Api $Exe 'current'
    Test-RouteSmoke "the current version ($($current.version)) keeps every account, and each signs in with its own role" {
        $after = Accounts
        if (($after -join ';') -ne ($script:before -join ';')) { throw "accounts changed: before [$($script:before -join '; ')] after [$($after -join '; ')]" }
        $alice = Sign-In 'alice' 'alice password 1'; $bob = Sign-In 'bob' 'bob password 1'; $carol = Sign-In 'carol' 'carol password 2'
        if (-not $alice -or $alice.Role -ne 'Operator' -or $alice.Name -ne 'Alice A') { throw "alice: $($alice | ConvertTo-Json -Compress)" }
        if (-not $bob -or $bob.Role -ne 'Admin') { throw "bob: $($bob | ConvertTo-Json -Compress)" }
        if (-not $carol -or $carol.Role -ne 'Viewer') { throw "carol with her changed password: $($carol | ConvertTo-Json -Compress)" }
        if (Sign-In 'carol' 'carol password 1') { throw "carol's old password still works" }
        if (Sign-In 'dave' 'dave password 1') { throw 'the disabled account signed in' }
        if ((Invoke-Api GET '/api/v1/security/users' $null $owner).Status -ne 200) { throw 'the Owner token was refused' }
        if ((Invoke-Api GET '/api/v1/security/users' $null $alice.Token).Status -ne 403) { throw 'an Operator could list accounts' }
    }
    Test-RouteSmoke 'the current version creates an account' {
        $r = Invoke-Api POST '/api/v1/security/users' @{ username = 'erin'; displayName = 'Erin E'; role = 'Viewer'; password = 'erin password 1' } $owner
        if ($r.Status -ne 200) { throw "creating erin failed: $($r.Status) $($r.Body.message)" }
        $script:withErin = Accounts
    }
    Stop-Api

    $rolledBack = Start-Api $BaselineExe 'rollback'
    Test-RouteSmoke "rolling back to the baseline ($($rolledBack.version)) keeps every account, including one the current version created" {
        $again = Accounts
        if (($again -join ';') -ne ($script:withErin -join ';')) { throw "accounts changed: [$($script:withErin -join '; ')] became [$($again -join '; ')]" }
        $erin = Sign-In 'erin' 'erin password 1'; $alice = Sign-In 'alice' 'alice password 1'
        if (-not $erin -or $erin.Role -ne 'Viewer') { throw "erin on the baseline: $($erin | ConvertTo-Json -Compress)" }
        if (-not $alice -or $alice.Role -ne 'Operator') { throw "alice on the baseline: $($alice | ConvertTo-Json -Compress)" }
        if (Sign-In 'dave' 'dave password 1') { throw 'the disabled account signed in on the baseline' }
    }
}
finally {
    Stop-Api
}

if ($failures.Count -gt 0) { throw "MystTiq v0.9.8.0 accounts upgrade smoke failed: $($failures.Count) check(s)." }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'MystTiq v0.9.8.0 accounts upgrade smoke passed.' -ForegroundColor Green
