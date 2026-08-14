#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ProjectRoot = ".",
    [string]$Version = "0.3.0.7",
    [string]$LinuxHost = "192.168.1.248",
    [string]$LinuxUser = "mystroth",
    [string]$RemoteBase = "/home/mystroth/mysttiq-builds",
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519",
    [switch]$SkipBuild,
    [switch]$Extended,
    [switch]$InstallCurrent,
    [switch]$AllowPasswordFallback
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Step([string]$Text) { Write-Host "`n==> $Text" -ForegroundColor Cyan }
function Pass([string]$Text) { Write-Host "[PASS] $Text" -ForegroundColor Green }
function Warn([string]$Text) { Write-Warning $Text }

$root = (Resolve-Path $ProjectRoot).Path
$ssh = (Get-Command ssh -ErrorAction Stop).Source
$scp = (Get-Command scp -ErrorAction Stop).Source

$identity = [Environment]::ExpandEnvironmentVariables($IdentityFile)
$target = "$LinuxUser@$LinuxHost"
$archiveName = "MystTiqHeadless-v$Version-linux-x64.tar.gz"
$archive = Join-Path $root "artifacts\$archiveName"
$remoteArchive = "$RemoteBase/$archiveName"
$remoteDeploy = "$RemoteBase/v$Version"
$acceptance = "scripts/Test-v$Version-LinuxAcceptance.sh"

$script:SshArgs = @()
$script:ScpArgs = @()

function Initialize-Authentication {
    Step "Selecting SSH authentication"

    if (Test-Path $identity -PathType Leaf) {
        $script:SshArgs = @(
            "-i", $identity,
            "-o", "BatchMode=yes",
            "-o", "PreferredAuthentications=publickey",
            "-o", "PasswordAuthentication=no"
        )
        $script:ScpArgs = @(
            "-i", $identity,
            "-o", "BatchMode=yes",
            "-o", "PreferredAuthentications=publickey",
            "-o", "PasswordAuthentication=no"
        )

        & $ssh @script:SshArgs $target "printf 'MYSTTIQ_KEY_AUTH_OK\n'"
        if ($LASTEXITCODE -eq 0) {
            Pass "Using dedicated MystTiq SSH key: $identity"
            return
        }

        if (-not $AllowPasswordFallback) {
            throw @"
A dedicated MystTiq SSH key exists but authentication failed.

Run:
  .\scripts\Initialize-MystTiqLinuxSSH.ps1

Or use -AllowPasswordFallback for an interactive password-based deployment.
"@
        }

        Warn "Dedicated key authentication failed; using interactive OpenSSH fallback."
        $script:SshArgs = @()
        $script:ScpArgs = @()
        return
    }

    if (-not $AllowPasswordFallback) {
        throw @"
Dedicated MystTiq SSH key was not found:
  $identity

Run this one-time setup first:
  .\scripts\Initialize-MystTiqLinuxSSH.ps1

Then rerun this deployment command.
"@
    }

    Warn "Dedicated SSH key not found; using interactive OpenSSH password fallback."
}

function Invoke-Remote([string]$Command, [switch]$AllowFailure) {
    & $ssh @script:SshArgs $target $Command
    $code = $LASTEXITCODE
    if (-not $AllowFailure -and $code -ne 0) {
        throw "Remote command failed with exit code $code."
    }
    return $code
}

function Copy-Remote([string]$LocalPath, [string]$DestinationDirectory) {
    & $scp @script:ScpArgs $LocalPath "${target}:$DestinationDirectory/"
    if ($LASTEXITCODE -ne 0) {
        throw "SCP failed with exit code $LASTEXITCODE."
    }
}

Initialize-Authentication

if (-not $SkipBuild) {
    Step "Building v$Version Linux headless package"
    Push-Location $root
    try {
        & .\Build.ps1 LinuxHeadless
        if ($LASTEXITCODE -ne 0) {
            throw "LinuxHeadless build failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path $archive -PathType Leaf)) {
    throw "Archive not found: $archive"
}

$localHash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Pass "Local archive SHA256: $localHash"

Step "Preparing remote deployment folder"
Invoke-Remote "mkdir -p '$RemoteBase' '$remoteDeploy' && rm -rf '$remoteDeploy'/*" | Out-Null

Step "Copying Linux archive"
Copy-Remote $archive $RemoteBase

Step "Verifying transferred archive and extracting"
$cmdTemplate = @'
remoteHash=$(sha256sum '{0}' | awk '{{print $1}}'); echo REMOTE_SHA256=$remoteHash; test "$remoteHash" = '{1}' && tar -xzf '{0}' -C '{2}' && chmod +x '{2}/mysttiq-server' '{2}/{3}'
'@
$cmd = $cmdTemplate -f $remoteArchive, $localHash, $remoteDeploy, $acceptance
Invoke-Remote $cmd | Out-Null
Pass "Remote archive verified and extracted."

Step "Running automated Linux acceptance"
$acceptanceArgs = @()
if ($InstallCurrent) { $acceptanceArgs += "--install-current" }
if ($Extended) { $acceptanceArgs += "--extended" }
$remoteAcceptanceArgs = $acceptanceArgs -join " "

Invoke-Remote "cd '$remoteDeploy' && bash './$acceptance' $remoteAcceptanceArgs"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "MystTiq v$Version deployment + Linux acceptance completed." -ForegroundColor Green
Write-Host "Linux:  $target"
Write-Host "Deploy: $remoteDeploy"
Write-Host "Auth:   $(if ($script:SshArgs.Count -gt 0) { 'Dedicated SSH key' } else { 'Interactive password fallback' })"
Write-Host "============================================================"
