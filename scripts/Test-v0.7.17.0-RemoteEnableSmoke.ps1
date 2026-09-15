[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18260
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.17.0: permanent regression coverage for the CLI bug flagged (found, not fixed) in v0.7.13.0
# -- Program.cs's blanket pre-command "effective configuration" validation ran before the command
# switch for every command, including api-remote-enable/api-remote-disable. It built the effective
# config from the CLI's NEW --bind-address override but the OLD, not-yet-updated
# Authentication/Tls flags, so a fresh config's api-remote-enable --bind-address <non-loopback>
# always failed that pre-check before HeadlessRemoteApiEnrollmentService.EnableRemoteApi -- the
# handler that would have produced a valid config -- ever ran. Exercises the full documented
# workflow (config-write-default -> api-token-create -> api-tls-create -> api-remote-enable ->
# api-run) via actual CLI invocations of the published sidecar exe, matching the exact repro used
# to find and fix the bug, then starts the resulting config for real and confirms it serves
# traffic with authentication+TLS actually enforced.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }

$bindAddress = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {
    $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*'
} | Select-Object -First 1).IPAddress
if (-not $bindAddress) { throw 'Could not determine a real non-loopback IPv4 address on this machine to bind against.' }

$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.17.0-remoteenable-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$missingServer = Join-Path $temp 'missing-server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
$tokenFile = Join-Path $temp 'secrets\api-token'
$certificateFile = Join-Path $temp 'secrets\api-tls.pfx'
$certificatePasswordFile = Join-Path $temp 'secrets\api-tls-password'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $missingServer -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$log = Join-Path $temp 'headless.log'
$err = Join-Path $temp 'headless.err.log'
$proc = $null
$failures = @()

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        Write-Host "[PASS] Remote Enable Smoke :: $Name" -ForegroundColor Green
    }
    catch {
        Write-Host "[FAIL] Remote Enable Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red
        $script:failures += $Name
    }
}

function Invoke-MysttiqCli([string[]]$CliArgs) {
    $stdout = & $exe @CliArgs 2>&1 | Out-String
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $stdout.Trim() }
}

try {
    Test-RouteSmoke 'config-write-default produces a fresh loopback config' {
        $result = Invoke-MysttiqCli @('config-write-default', '--config', $config, '--overwrite')
        if ($result.ExitCode -ne 0) { throw "exit $($result.ExitCode): $($result.Output)" }
    }

    Test-RouteSmoke 'api-token-create writes a bearer token file' {
        $result = Invoke-MysttiqCli @('api-token-create', '--token-file', $tokenFile, '--overwrite')
        if ($result.ExitCode -ne 0) { throw "exit $($result.ExitCode): $($result.Output)" }
        if (-not (Test-Path $tokenFile -PathType Leaf)) { throw 'token file was not written' }
    }

    Test-RouteSmoke 'api-tls-create writes a self-signed certificate for the real bind address' {
        $result = Invoke-MysttiqCli @('api-tls-create', '--certificate-file', $certificateFile, '--certificate-password-file', $certificatePasswordFile, '--bind-address', $bindAddress, '--overwrite')
        if ($result.ExitCode -ne 0) { throw "exit $($result.ExitCode): $($result.Output)" }
        if (-not (Test-Path $certificateFile -PathType Leaf)) { throw 'certificate file was not written' }
    }

    Test-RouteSmoke 'api-remote-enable succeeds against a fresh config with a non-loopback --bind-address (the regressed flow)' {
        $result = Invoke-MysttiqCli @('api-remote-enable', '--config', $config, '--bind-address', $bindAddress, '--api-port', "$Port", '--token-file', $tokenFile, '--certificate-file', $certificateFile, '--certificate-password-file', $certificatePasswordFile)
        # Before the v0.7.17.0 fix this always exited 2 with "Effective configuration validation
        # failed after command-line overrides" / "Non-loopback API binding requires
        # api.authentication.enabled=true"/"...api.tls.enabled=true", rejected by Program.cs's
        # blanket pre-command check before HeadlessRemoteApiEnrollmentService.EnableRemoteApi --
        # the handler that actually sets those flags -- ever ran.
        if ($result.ExitCode -ne 0) { throw "exit $($result.ExitCode): $($result.Output)" }
        if ($result.Output -notmatch 'Authentication\s*:\s*True') { throw "expected Authentication: True in output, got: $($result.Output)" }
        if ($result.Output -notmatch 'TLS\s*:\s*True') { throw "expected TLS: True in output, got: $($result.Output)" }
    }

    Test-RouteSmoke 'config-validate confirms the enabled config is internally consistent' {
        $result = Invoke-MysttiqCli @('config-validate', '--config', $config)
        if ($result.ExitCode -ne 0) { throw "exit $($result.ExitCode): $($result.Output)" }
    }

    Test-RouteSmoke 'api-run starts against the resulting config and actually enforces authentication+TLS' {
        $args = @('api-run', '--config', $config, '--server-root', $missingServer, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
        $script:proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err
        $base = "https://${bindAddress}:$Port"
        $ready = $false
        $health = $null
        for ($i = 0; $i -lt 40; $i++) {
            Start-Sleep -Milliseconds 250
            if ($proc.HasExited) { break }
            try { $health = Invoke-RestMethod "$base/healthz" -TimeoutSec 2 -SkipCertificateCheck; if ($health) { $ready = $true; break } } catch {}
        }
        if (-not $ready) {
            if ($proc.HasExited -and $proc.ExitCode -eq 2) {
                throw "sidecar exited 2 (configuration validation failure) instead of starting -- the pre-command gate is rejecting a config api-remote-enable itself just produced. stdout=$(Get-Content $log -Raw -ErrorAction SilentlyContinue)"
            }
            throw "Sidecar did not become healthy. exited=$($proc.HasExited) stdout=$(Get-Content $log -Raw -ErrorAction SilentlyContinue) stderr=$(Get-Content $err -Raw -ErrorAction SilentlyContinue)"
        }
        if ($health.api -ne 'remote-secured') { throw "expected healthz api='remote-secured', got '$($health.api)'" }
        if ($health.authentication -ne $true) { throw "expected healthz authentication=true, got '$($health.authentication)'" }
        if ($health.tls -ne $true) { throw "expected healthz tls=true, got '$($health.tls)'" }

        try {
            Invoke-RestMethod "$base/api/v1/service" -TimeoutSec 5 -SkipCertificateCheck | Out-Null
            throw 'expected an unauthenticated request to a protected route to be rejected'
        }
        catch {
            if ($_.Exception.Message -eq 'expected an unauthenticated request to a protected route to be rejected') { throw }
            if (-not $_.ErrorDetails -or $_.Exception.Response.StatusCode -ne 401) { throw "expected HTTP 401 for an unauthenticated protected route, got: $($_.Exception.Message)" }
        }

        $token = (Get-Content $tokenFile -Raw).Trim()
        $authed = Invoke-RestMethod "$base/api/v1/service" -TimeoutSec 5 -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
        if (-not $authed) { throw 'expected a bearer-authenticated request to succeed' }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.17.0 remote-enable smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.17.0 remote-enable smoke gate passed." -ForegroundColor Green
