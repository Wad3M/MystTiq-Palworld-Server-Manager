#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$LinuxHost = "192.168.1.248",
    [string]$LinuxUser = "mystroth",
    [string]$KeyPath = "$HOME\.ssh\mysttiq_linux_ed25519",
    [switch]$ForceNewKey
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Step([string]$Text) { Write-Host "`n==> $Text" -ForegroundColor Cyan }
function Pass([string]$Text) { Write-Host "[PASS] $Text" -ForegroundColor Green }

$ssh = (Get-Command ssh -ErrorAction Stop).Source
$sshKeygen = (Get-Command ssh-keygen -ErrorAction Stop).Source

$keyPathResolved = [Environment]::ExpandEnvironmentVariables($KeyPath)
$keyDirectory = Split-Path -Parent $keyPathResolved
$publicKeyPath = "$keyPathResolved.pub"
$target = "$LinuxUser@$LinuxHost"

if (-not (Test-Path $keyDirectory)) {
    New-Item -ItemType Directory -Force $keyDirectory | Out-Null
}

if ($ForceNewKey -and (Test-Path $keyPathResolved)) {
    Step "Removing existing dedicated MystTiq SSH key"
    Remove-Item $keyPathResolved -Force
    Remove-Item $publicKeyPath -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path $keyPathResolved)) {
    Step "Creating dedicated MystTiq Ed25519 SSH key"
    & $sshKeygen `
        -t ed25519 `
        -a 100 `
        -f $keyPathResolved `
        -N "" `
        -C "MystTiq Linux deployment key"
    if ($LASTEXITCODE -ne 0) {
        throw "ssh-keygen failed with exit code $LASTEXITCODE."
    }
    Pass "Created key: $keyPathResolved"
}
else {
    Pass "Dedicated key already exists: $keyPathResolved"
}

if (-not (Test-Path $publicKeyPath)) {
    throw "Public key was not found: $publicKeyPath"
}

$publicKey = (Get-Content $publicKeyPath -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($publicKey)) {
    throw "Public key file is empty: $publicKeyPath"
}

Step "Installing public key on $target"
Write-Host "You should be asked for the Linux account password once during this initial setup."

# Feed only the PUBLIC key into the remote shell; the private key never leaves Windows.
$remoteCommand = @'
umask 077
mkdir -p ~/.ssh
touch ~/.ssh/authorized_keys
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys
key=$(cat)
grep -qxF "$key" ~/.ssh/authorized_keys || printf '%s\n' "$key" >> ~/.ssh/authorized_keys
'@

$publicKey | & $ssh $target $remoteCommand
if ($LASTEXITCODE -ne 0) {
    throw "Unable to install the public SSH key."
}
Pass "Public key installed in ~/.ssh/authorized_keys"

Step "Testing passwordless SSH"
& $ssh `
    -i $keyPathResolved `
    -o BatchMode=yes `
    -o PreferredAuthentications=publickey `
    -o PasswordAuthentication=no `
    $target `
    "printf 'MYSTTIQ_KEY_AUTH_OK\n'; whoami; hostname"

if ($LASTEXITCODE -ne 0) {
    throw "Passwordless SSH test failed. The key was created but is not yet usable."
}

Pass "Passwordless SSH authentication is working."
Write-Host ""
Write-Host "Dedicated private key:" -ForegroundColor White
Write-Host "  $keyPathResolved"
Write-Host ""
Write-Host "Normal MystTiq deployments can now use:" -ForegroundColor White
Write-Host "  .\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended"
Write-Host ""
Write-Host "The private key remains on this Windows machine and is never copied to Linux."
