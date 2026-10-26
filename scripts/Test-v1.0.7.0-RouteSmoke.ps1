# MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Exe = '',
    [int]$Port = 18670
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v1.0.7.0 (roadmap Q-1, owner decision D-10). A real service, isolated (its own fleet folder and port, sign-in on), with
# user accounts whose users.json is then damaged as after a power cut:
#   1. With a last good copy: the service loads it (the accounts saved before the last change), a named account still
#      signs in, the owner gets a warning linking to Server Doctor, and the next change saves normally.
#   2. With no good copy: no account is lost: users.json is left byte for byte, creating an account is refused (HTTP 409)
#      with the reason, the shared owner token still works, the owner gets a pinned critical alert linking to the repair,
#      and Server Doctor has the repair; after it, the list starts empty, the unreadable file is kept, and changes work.
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe' }
if (-not $IsWindows -and -not $PSBoundParameters.ContainsKey('Exe')) { $Exe = Join-Path $root 'artifacts\publish\linux-x64\headless\mysttiq-server' }
if (-not (Test-Path $Exe -PathType Leaf)) { throw "Service not found: $Exe" }

$temp = Join-Path $root ("artifacts\runtime-smoke\v1.0.7.0-saved-lists-" + [guid]::NewGuid().ToString('N'))
$config = Join-Path $temp 'mysttiq.json'; $runtime = Join-Path $temp 'runtime'; $serverRoot = Join-Path $temp 'server'
$backupRoot = Join-Path $temp 'backups'; $fleetRoot = Join-Path $temp 'fleet'; $tokenFile = Join-Path $temp 'api-token.txt'
New-Item $runtime, $backupRoot, $fleetRoot, $serverRoot -ItemType Directory -Force | Out-Null
& $Exe config-write-default --config $config --overwrite | Out-Null
& $Exe api-token-create --token-file $tokenFile --overwrite | Out-Null
$cfg = Get-Content $config -Raw | ConvertFrom-Json
$cfg.Api.Port = $Port
$cfg.Api.Authentication.Enabled = $true
$cfg.Api.Authentication.TokenFile = $tokenFile
# Accounts live under FleetRoot; the default is the machine's real store, so a smoke always uses its own.
$cfg.FleetRoot = $fleetRoot
$cfg | ConvertTo-Json -Depth 20 | Set-Content $config
$owner = (Get-Content $tokenFile -Raw).Trim()
$users = Join-Path $fleetRoot 'security\users.json'

function Call([string]$Method, [string]$Path, $Body = $null, [string]$Token = $owner) {
    $headers = @{}; if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 6 } else { $null }
    $r = Invoke-WebRequest "http://127.0.0.1:$Port$Path" -Method $Method -Headers $headers -ContentType 'application/json' -Body $json -SkipHttpErrorCheck -TimeoutSec 60
    $parsed = $null; if ($r.Content) { try { $parsed = $r.Content | ConvertFrom-Json } catch { } }
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $parsed }
}
$failures = @()
function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
$script:svc = $null
function Start-Service1070 {
    $log = Join-Path $temp ("svc-" + [guid]::NewGuid().ToString('N').Substring(0, 6))
    $script:svc = Start-Process -FilePath $Exe -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--runtime-root', $runtime, '--backup-root', $backupRoot, '--steamcmd', (Join-Path $temp 'none.exe')) -PassThru -WindowStyle Hidden -RedirectStandardOutput "$log.log" -RedirectStandardError "$log.err.log"
    for ($i = 0; $i -lt 80; $i++) { try { Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 | Out-Null; return } catch { Start-Sleep -Milliseconds 250 } }
    throw "the service is not answering. $(Get-Content "$log.err.log" -Raw -ErrorAction SilentlyContinue)"
}
function Stop-Service1070 {
    if ($script:svc -and -not $script:svc.HasExited) { Stop-Process -Id $script:svc.Id -Force; $script:svc.WaitForExit(15000) | Out-Null }
    $script:svc = $null
}
function Damage([string]$Path) { $t = [IO.File]::ReadAllText($Path); [IO.File]::WriteAllText($Path, $t.Substring(0, [int]($t.Length / 2))) }
function Names { @((Call GET '/api/v1/security/users').Body | ForEach-Object { $_.username }) | Sort-Object }

try {
    Start-Service1070
    Test-RouteSmoke 'three accounts are created on an isolated service with sign-in on' {
        foreach ($n in 'alice', 'bob', 'carol') {
            $r = Call POST '/api/v1/security/users' @{ username = $n; displayName = $n; role = 'Operator'; password = 'long enough pw' }
            if ($r.Status -ne 200) { throw "$n`: $($r.Status) $($r.Body.message)" }
        }
        if ((Names) -join ',' -ne 'alice,bob,carol') { throw "listed: $((Names) -join ',')" }
        if (-not (Test-Path "$users.last-good")) { throw 'no last good copy was kept' }
    }
    Stop-Service1070

    Damage $users
    Start-Service1070
    Test-RouteSmoke 'with a last good copy: it is loaded, a named account still signs in, and the owner is warned with a link to Server Doctor' {
        $listed = (Names) -join ','
        if ($listed -ne 'alice,bob') { throw "expected the copy saved before carol (alice,bob), got: $listed" }
        $login = Call POST '/api/v1/auth/login' @{ username = 'alice'; password = 'long enough pw' } ''
        if ($login.Status -ne 200 -or -not $login.Body.token) { throw "alice could not sign in: $($login.Status) $($login.Body.message)" }
        $note = @((Call GET '/api/v1/notifications').Body.items | Where-Object { $_.title -eq 'Saved user accounts loaded from its last good copy' })
        if ($note.Count -ne 1 -or $note[0].link -ne 'diagnostics:saved-file-security-users') { throw 'no warning with the repair link' }
        $finding = @((Call GET '/api/v1/diagnostics/report').Body.findings | Where-Object { $_.id -eq 'saved-file-security-users' })
        if ($finding.Count -ne 1 -or $finding[0].state -ne 1 -or $finding[0].actionKind) { throw 'Server Doctor does not show it as a warning with nothing to fix' }
        if (@(Get-ChildItem (Split-Path $users) -Filter 'users.json.unreadable-*').Count -ne 1) { throw 'the damaged file was not kept' }
        Write-Host "    $($note[0].message)"
    }
    Test-RouteSmoke 'the next change saves normally (carol again) and the file reads again' {
        $r = Call POST '/api/v1/security/users' @{ username = 'carol'; displayName = 'carol'; role = 'Operator'; password = 'long enough pw' }
        if ($r.Status -ne 200) { throw "$($r.Status) $($r.Body.message)" }
        $null = Get-Content $users -Raw | ConvertFrom-Json
    }
    Stop-Service1070

    Damage $users
    Remove-Item "$users.last-good" -Force
    $damaged = (Get-FileHash $users -Algorithm SHA256).Hash
    # A server's own list too: the whitelist, unreadable with no copy, so its save throws the refusal inside the service.
    $whitelist = Join-Path $runtime 'players\whitelist.json'
    New-Item (Split-Path $whitelist) -ItemType Directory -Force | Out-Null
    Set-Content $whitelist '{ "Enabled": true, "Entries": [ ' -NoNewline
    Remove-Item "$whitelist.last-good" -ErrorAction SilentlyContinue
    Start-Service1070
    Test-RouteSmoke 'a change a service refuses inside comes back as HTTP 409 with the reason, and the file is left as it was' {
        $before = (Get-FileHash $whitelist -Algorithm SHA256).Hash
        $r = Call PUT '/api/v1/players/whitelist' @{ enabled = $false; entries = @() }
        if ($r.Status -ne 409 -or $r.Body.error -ne 'saved-file-refused' -or "$($r.Body.message)" -notmatch 'saved whitelist') { throw "got $($r.Status) $($r.Body.error) $($r.Body.message)" }
        if ((Get-FileHash $whitelist -Algorithm SHA256).Hash -ne $before) { throw 'whitelist.json was changed' }
    }
    Test-RouteSmoke 'with no good copy: users.json is left byte for byte, creating an account is refused with the reason, and the owner token still works' {
        $r = Call POST '/api/v1/security/users' @{ username = 'dave'; displayName = 'dave'; role = 'Viewer'; password = 'long enough pw' }
        if ($r.Status -ne 400 -and $r.Status -ne 409) { throw "expected a refusal, got $($r.Status)" }
        if ("$($r.Body.message)" -notmatch 'changes to it are refused' -or "$($r.Body.message)" -notmatch 'Server Doctor') { throw "the refusal does not say why or where: $($r.Body.message)" }
        if ((Get-FileHash $users -Algorithm SHA256).Hash -ne $damaged) { throw 'users.json was changed' }
        if ((Call GET '/api/v1/security/users').Status -ne 200) { throw 'the shared owner token no longer works' }
        Write-Host "    $($r.Body.message)"
    }
    Test-RouteSmoke 'the owner gets a pinned critical alert linking to the repair, and Server Doctor offers it' {
        $note = @((Call GET '/api/v1/notifications').Body.items | Where-Object { $_.title -eq 'Saved user accounts could not be read' })
        if ($note.Count -ne 1 -or -not $note[0].pinned -or $note[0].severity -ne 'Critical' -or $note[0].link -ne 'diagnostics:saved-file-security-users') { throw 'no pinned critical alert with the repair link' }
        $finding = @((Call GET '/api/v1/diagnostics/report').Body.findings | Where-Object { $_.id -eq 'saved-file-security-users' })
        if ($finding.Count -ne 1 -or $finding[0].state -ne 2 -or $finding[0].actionKind -ne 'repair-saved-file') { throw 'Server Doctor has no repair for it' }
    }
    Test-RouteSmoke 'the repair starts the list empty, keeps the unreadable file, and changes work again' {
        $fix = Call POST '/api/v1/diagnostics/saved-file-security-users/fix'
        if ($fix.Status -ne 200) { throw "fix: $($fix.Status) $($fix.Body.message)" }
        $kept = @(Get-ChildItem (Split-Path $users) -Filter 'users.json.unreadable-*' | Where-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash -eq $damaged })
        if ($kept.Count -ne 1) { throw 'the unreadable file is not kept' }
        $r = Call POST '/api/v1/security/users' @{ username = 'dave'; displayName = 'dave'; role = 'Viewer'; password = 'long enough pw' }
        if ($r.Status -ne 200 -or (Names) -join ',' -ne 'dave') { throw "after the repair: $($r.Status) $((Names) -join ',')" }
        Write-Host "    $($fix.Body.message)"
    }
}
finally {
    Stop-Service1070
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { Write-Host "$($failures.Count) route smoke check(s) failed." -ForegroundColor Red; exit 1 }
Write-Host 'All v1.0.7.0 route smoke checks passed.' -ForegroundColor Green
