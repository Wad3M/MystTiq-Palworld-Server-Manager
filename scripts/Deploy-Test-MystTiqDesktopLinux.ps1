[CmdletBinding()]
param(
    [string]$HostName = '192.168.1.248',
    [string]$UserName = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519",
    [string]$RemoteRoot = '~/mysttiq-desktop/v0.3.1.9',
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'
$Version = '0.3.1.9'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'artifacts\publish\desktop-linux-x64'
$archive = Join-Path $root ("artifacts\MystTiqDesktop-v{0}-linux-x64.tar.gz" -f $Version)

if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) { throw 'ssh was not found in PATH.' }
if (-not (Get-Command scp -ErrorAction SilentlyContinue)) { throw 'scp was not found in PATH.' }
if (-not (Get-Command tar -ErrorAction SilentlyContinue)) { throw 'tar was not found in PATH.' }
if (-not (Test-Path $IdentityFile -PathType Leaf)) { throw "SSH identity file not found: $IdentityFile" }

Write-Host "`n==> Building Linux Avalonia desktop" -ForegroundColor Cyan
& (Join-Path $root 'Build.ps1') DesktopLinux

if (-not (Test-Path (Join-Path $publish 'MystTiq.Desktop') -PathType Leaf)) {
    throw "Linux desktop executable was not found: $publish"
}

Write-Host "`n==> Packaging Linux desktop publish" -ForegroundColor Cyan
Remove-Item $archive -Force -ErrorAction SilentlyContinue
& tar -czf $archive -C $publish .
if ($LASTEXITCODE -ne 0) { throw 'Failed to create Linux desktop archive.' }

$localHash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "[PASS] Local SHA256: $localHash" -ForegroundColor Green

$target = "$UserName@$HostName"
$remoteArchive = "~/MystTiqDesktop-v$Version-linux-x64.tar.gz"

Write-Host "`n==> Preparing remote folder" -ForegroundColor Cyan
& ssh -i $IdentityFile $target "rm -rf $RemoteRoot && mkdir -p $RemoteRoot"
if ($LASTEXITCODE -ne 0) { throw 'Unable to prepare remote desktop folder.' }

Write-Host "`n==> Copying Linux desktop archive" -ForegroundColor Cyan
& scp -i $IdentityFile $archive "${target}:$remoteArchive"
if ($LASTEXITCODE -ne 0) { throw 'Linux desktop archive transfer failed.' }

Write-Host "`n==> Verifying and extracting remote archive" -ForegroundColor Cyan
$remoteHash = (& ssh -i $IdentityFile $target "sha256sum $remoteArchive | awk '{print `$1}'").Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0) { throw 'Unable to calculate remote SHA256.' }
if ($remoteHash -ne $localHash) { throw "SHA256 mismatch. local=$localHash remote=$remoteHash" }

& ssh -i $IdentityFile $target "tar -xzf $remoteArchive -C $RemoteRoot && chmod +x $RemoteRoot/MystTiq.Desktop"
if ($LASTEXITCODE -ne 0) { throw 'Remote desktop extraction failed.' }

Write-Host "[PASS] Linux desktop deployed: $RemoteRoot/MystTiq.Desktop" -ForegroundColor Green

if ($Launch) {
    Write-Host "`n==> Launching in active XFCE graphical session" -ForegroundColor Cyan

    $launch = @'
set -e
APP=~/mysttiq-desktop/v0.3.1.9/MystTiq.Desktop
LOG=~/mysttiq-desktop/v0.3.1.9/desktop-smoke.log
PID=$(pgrep -n xfce4-session || true)
if [ -z "$PID" ]; then
  echo "NO_XFCE_SESSION"
  exit 3
fi

ENVFILE="/proc/$PID/environ"
DISPLAY_VALUE=$(tr '\0' '\n' < "$ENVFILE" | sed -n 's/^DISPLAY=//p' | head -1)
DBUS_VALUE=$(tr '\0' '\n' < "$ENVFILE" | sed -n 's/^DBUS_SESSION_BUS_ADDRESS=//p' | head -1)
XAUTH_VALUE=$(tr '\0' '\n' < "$ENVFILE" | sed -n 's/^XAUTHORITY=//p' | head -1)

if [ -z "$DISPLAY_VALUE" ]; then
  echo "NO_DISPLAY"
  exit 4
fi

export DISPLAY="$DISPLAY_VALUE"
[ -n "$DBUS_VALUE" ] && export DBUS_SESSION_BUS_ADDRESS="$DBUS_VALUE"
[ -n "$XAUTH_VALUE" ] && export XAUTHORITY="$XAUTH_VALUE"

nohup "$APP" >"$LOG" 2>&1 &
sleep 3

if pgrep -f "$APP" >/dev/null; then
  echo "MYSTTIQ_DESKTOP_RUNNING display=$DISPLAY_VALUE log=$LOG"
else
  echo "MYSTTIQ_DESKTOP_NOT_RUNNING"
  cat "$LOG" 2>/dev/null || true
  exit 5
fi
'@

    $result = $launch | & ssh -i $IdentityFile $target 'bash -s'
    $result | ForEach-Object { Write-Host $_ }

    if ($LASTEXITCODE -eq 3 -or $result -contains 'NO_XFCE_SESSION') {
        Write-Warning 'No active XFCE session. Log in through RDP, then rerun with -Launch.'
    } elseif ($LASTEXITCODE -ne 0) {
        throw 'Linux desktop graphical smoke launch failed.'
    }
}

Write-Host "`n================ DESKTOP DEPLOY PASS ================" -ForegroundColor Green
Write-Host "Host:       $HostName"
Write-Host "Remote app: $RemoteRoot/MystTiq.Desktop"
Write-Host "Token:      never copied or persisted by this deployment script"
