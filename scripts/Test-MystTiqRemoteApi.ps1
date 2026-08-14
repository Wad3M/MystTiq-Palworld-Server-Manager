#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$LinuxHost = "192.168.1.248",
    [string]$LinuxUser = "mystroth",
    [int]$Port = 8213,
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519",
    [string]$TokenFile = "/etc/mysttiq/secrets/api-token",
    [string]$ConfigFile = "/etc/mysttiq/mysttiq.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$passed = 0
$failed = 0

function Pass([string]$Text) {
    $script:passed++
    Write-Host "[PASS] $Text" -ForegroundColor Green
}

function Fail([string]$Text) {
    $script:failed++
    Write-Host "[FAIL] $Text" -ForegroundColor Red
}

function Stop-OnFailure {
    if ($script:failed -gt 0) {
        Write-Host ""
        Write-Host "================ REMOTE API FAIL ================" -ForegroundColor Red
        Write-Host "Passed : $passed"
        Write-Host "Failed : $failed"
        exit 1
    }
}

$identity = [Environment]::ExpandEnvironmentVariables($IdentityFile)
$ssh = (Get-Command ssh -ErrorAction Stop).Source
$target = "$LinuxUser@$LinuxHost"

Write-Host "`nMystTiq v0.3.0.7 Remote API Acceptance"
Write-Host "Target: https://${LinuxHost}:$Port"

if (-not (Test-Path $identity -PathType Leaf)) {
    Fail "Dedicated MystTiq SSH identity not found: $identity"
    Stop-OnFailure
}

$sshArgs = @(
    "-i", $identity,
    "-o", "BatchMode=yes",
    "-o", "PreferredAuthentications=publickey",
    "-o", "PasswordAuthentication=no"
)

& $ssh @sshArgs $target "printf 'MYSTTIQ_SSH_OK\n'" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Fail "Trusted SSH key authentication failed for $target."
    Stop-OnFailure
}
Pass "Trusted SSH channel is available."

$configJson = & $ssh @sshArgs $target "/opt/mysttiq/bin/mysttiq-server config-show --config '$ConfigFile'"
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($configJson -join "`n"))) {
    Fail "Unable to read effective MystTiq configuration over SSH."
    Stop-OnFailure
}

try {
    $config = (($configJson -join "`n") | ConvertFrom-Json)
}
catch {
    Fail "Effective MystTiq configuration was not valid JSON."
    Stop-OnFailure
}

$bind = $config.Api.BindAddress
$authEnabled = [bool]$config.Api.Authentication.Enabled
$tlsEnabled = [bool]$config.Api.Tls.Enabled

if ($bind -ne $LinuxHost) {
    Fail "Effective API bind is '$bind'; expected '$LinuxHost'. Run Configure-MystTiqRemoteApi.sh again."
}
else {
    Pass "Effective API bind is $bind."
}

if (-not $authEnabled) {
    Fail "API authentication is not enabled."
}
else {
    Pass "API authentication is enabled."
}

if (-not $tlsEnabled) {
    Fail "API TLS is not enabled."
}
else {
    Pass "API TLS is enabled."
}

Stop-OnFailure

$tokenLines = & $ssh @sshArgs $target "test -s '$TokenFile' && cat '$TokenFile'"
if ($LASTEXITCODE -ne 0) {
    Fail "API token file is missing or empty: $TokenFile"
    Stop-OnFailure
}

$token = (($tokenLines -join "")).Trim()
if ([string]::IsNullOrWhiteSpace($token)) {
    Fail "API token file returned an empty value."
    Stop-OnFailure
}
Pass "API token retrieved into process memory only."

$listener = & $ssh @sshArgs $target "ss -lnt | grep -F '${LinuxHost}:${Port}'"
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($listener -join "`n"))) {
    Fail "Linux is not listening on ${LinuxHost}:${Port}."
    Stop-OnFailure
}
Pass "Linux listener is active on ${LinuxHost}:${Port}."

$base = "https://${LinuxHost}:$Port"

try {
    $health = Invoke-WebRequest `
        -Uri "$base/healthz" `
        -SkipCertificateCheck `
        -SkipHttpErrorCheck `
        -TimeoutSec 10

    if ($health.StatusCode -eq 200) {
        Pass "Remote HTTPS health endpoint returned HTTP 200."
    }
    else {
        Fail "Remote HTTPS health endpoint returned HTTP $($health.StatusCode)."
    }
}
catch {
    Fail "Unable to connect to remote HTTPS health endpoint: $($_.Exception.Message)"
}
Stop-OnFailure

try {
    $unauthorized = Invoke-WebRequest `
        -Uri "$base/api/v1/status" `
        -SkipCertificateCheck `
        -SkipHttpErrorCheck `
        -TimeoutSec 10

    if ($unauthorized.StatusCode -eq 401) {
        Pass "Unauthenticated management request was rejected with HTTP 401."
    }
    else {
        Fail "Unauthenticated API request returned HTTP $($unauthorized.StatusCode), expected 401."
    }
}
catch {
    Fail "Unauthenticated API request failed unexpectedly: $($_.Exception.Message)"
}
Stop-OnFailure

try {
    $headers = @{ Authorization = "Bearer $token" }
    $authorized = Invoke-WebRequest `
        -Uri "$base/api/v1/status" `
        -Headers $headers `
        -SkipCertificateCheck `
        -SkipHttpErrorCheck `
        -TimeoutSec 10

    if ($authorized.StatusCode -eq 200) {
        Pass "Authenticated management request returned HTTP 200."
    }
    else {
        Fail "Authenticated API request returned HTTP $($authorized.StatusCode), expected 200."
    }
}
catch {
    Fail "Authenticated API request failed unexpectedly: $($_.Exception.Message)"
}
Stop-OnFailure

try {
    $status = $authorized.Content | ConvertFrom-Json
    if ($null -ne $status.ready) {
        Pass "Lifecycle JSON was returned (ready=$($status.ready))."
    }
    else {
        Fail "Authorized response did not contain expected lifecycle JSON."
    }
}
catch {
    Fail "Authorized response could not be parsed as lifecycle JSON."
}
Stop-OnFailure

Write-Host ""
Write-Host "================ REMOTE API PASS ================" -ForegroundColor Green
Write-Host "Passed : $passed"
Write-Host "Failed : $failed"
Write-Host "HTTPS:             PASS"
Write-Host "LAN reachability:  PASS"
Write-Host "Unauthorized 401:  PASS"
Write-Host "Bearer auth 200:   PASS"
Write-Host "Token persisted:   NO (process memory only)"
