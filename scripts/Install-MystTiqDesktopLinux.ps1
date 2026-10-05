# MystTiq v1.0.0.3: file reviewed for this release (2026-10-05).
[CmdletBinding()]
param(
    [string]$HostName = '192.168.1.122',
    [string]$UserName = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519",
    [string]$Package = '',
    [switch]$Launch
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

# v0.9.8.0: installs the Linux download (the desktop with its headless service) into the user's home on a Linux machine
# and puts a MystTiq launcher on their desktop and in the applications menu. Nothing outside the home folder is touched
# (no sudo, no service install). Versions sit side by side in ~/MystTiq/<version>; ~/MystTiq/current points at the newest,
# and the launcher starts ~/MystTiq/current/MystTiq.Desktop, so a later install updates the shortcut without replacing it.
$root = Split-Path -Parent $PSScriptRoot
$version = & (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')
if (-not $Package) { $Package = Join-Path $root "artifacts\MystTiqPalworldServer_v${version}_Linux-x64.zip" }
if (-not (Test-Path $Package -PathType Leaf)) { throw "Linux package not found: $Package (run .\Build.ps1 Package first)" }
if (-not (Test-Path $IdentityFile -PathType Leaf)) { throw "SSH identity file not found: $IdentityFile" }
$icon = Join-Path $root 'src\MystTiq.Desktop\Assets\PalworldServerManager.png'
$packageVersion = if ((Split-Path $Package -Leaf) -match '_v(\d+(?:\.\d+){3})_') { $Matches[1] } else { $version }
$ssh = @('-i', $IdentityFile, '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10')
$target = "$UserName@$HostName"

Write-Host "==> Copying MystTiq v$packageVersion to $target" -ForegroundColor Cyan
& ssh @ssh $target 'mkdir -p ~/MystTiq/.incoming'
if ($LASTEXITCODE -ne 0) { throw "Cannot reach $target over SSH." }
& scp @ssh $Package "${target}:MystTiq/.incoming/package.zip"
if ($LASTEXITCODE -ne 0) { throw 'Copying the package failed.' }
& scp @ssh $icon "${target}:MystTiq/.incoming/mysttiq.png"
if ($LASTEXITCODE -ne 0) { throw 'Copying the icon failed.' }
$localHash = (Get-FileHash $Package -Algorithm SHA256).Hash.ToLowerInvariant()
$remoteHash = ((& ssh @ssh $target "sha256sum ~/MystTiq/.incoming/package.zip | cut -d' ' -f1") | Out-String).Trim().ToLowerInvariant()
if ($remoteHash -ne $localHash) { throw "The copied package does not match (local $localHash, remote $remoteHash)." }

$install = @'
set -e
V="__VERSION__"
cd ~/MystTiq
rm -rf ".incoming/unpacked" "$V"
unzip -q .incoming/package.zip -d .incoming/unpacked
top=$(find .incoming/unpacked -mindepth 1 -maxdepth 1 -type d | head -1)
mv "$top" "$V"
chmod +x "$V/MystTiq.Desktop" "$V/headless/mysttiq-server"
ln -sfn "$V" current
mkdir -p ~/.local/share/icons ~/.local/share/applications
mv -f .incoming/mysttiq.png ~/.local/share/icons/mysttiq.png
rm -rf .incoming
entry=~/.local/share/applications/mysttiq.desktop
cat > "$entry" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=MystTiq
GenericName=Palworld Server Manager
Comment=Manage Palworld dedicated servers
Exec=$HOME/MystTiq/current/MystTiq.Desktop
Path=$HOME/MystTiq/current
Icon=$HOME/.local/share/icons/mysttiq.png
Terminal=false
Categories=Game;Utility;
StartupWMClass=MystTiq.Desktop
EOF
chmod +x "$entry"
desk=$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Desktop")
mkdir -p "$desk"
cp -f "$entry" "$desk/mysttiq.desktop"
chmod +x "$desk/mysttiq.desktop"
# XFCE (4.18+) opens a desktop launcher without asking only when it is marked trusted: the launcher's checksum in its
# metadata. That needs the logged-in session's bus; without a session the launcher asks once ("Mark executable").
trusted=no
pid=$(pgrep -u "$(id -u)" -n xfce4-session || true)
if [ -n "$pid" ]; then
  bus=$(tr '\0' '\n' < /proc/$pid/environ | sed -n 's/^DBUS_SESSION_BUS_ADDRESS=//p' | head -1)
  if [ -n "$bus" ]; then
    sum=$(sha256sum "$desk/mysttiq.desktop" | cut -d' ' -f1)
    if DBUS_SESSION_BUS_ADDRESS="$bus" gio set -t string "$desk/mysttiq.desktop" metadata::xfce-exe-checksum "$sum" 2>/dev/null; then trusted=yes; fi
    DBUS_SESSION_BUS_ADDRESS="$bus" gio set -t string "$desk/mysttiq.desktop" metadata::trusted true 2>/dev/null || true
  fi
fi
echo "INSTALLED $HOME/MystTiq/$V"
echo "LAUNCHER $desk/mysttiq.desktop trusted=$trusted"
'@.Replace('__VERSION__', $packageVersion).Replace("`r`n", "`n")

Write-Host '==> Installing and adding the launcher' -ForegroundColor Cyan
$result = @($install | & ssh @ssh $target 'bash -s' 2>&1)
$result | ForEach-Object { Write-Host "   $_" }
if ($LASTEXITCODE -ne 0 -or -not ($result -match '^INSTALLED ')) { throw 'Installing on the Linux machine failed.' }

if ($Launch) {
    $start = @'
pid=$(pgrep -u "$(id -u)" -n xfce4-session || true)
[ -z "$pid" ] && { echo NO_SESSION; exit 3; }
eval "$(tr '\0' '\n' < /proc/$pid/environ | grep -E '^(DISPLAY|XAUTHORITY|DBUS_SESSION_BUS_ADDRESS|XDG_RUNTIME_DIR)=' | sed 's/^/export /')"
desk=$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Desktop")
# Started through the launcher itself (as a double-click would), so this also checks the shortcut.
timeout 20 gio launch "$desk/mysttiq.desktop" </dev/null >/dev/null 2>&1
for i in 1 2 3 4 5 6 7 8 9 10; do sleep 1; pgrep -u "$(id -u)" -f "MystTiq.Desktop" >/dev/null && { echo RUNNING; exit 0; }; done
echo NOT_RUNNING; exit 5
'@.Replace("`r`n", "`n")
    $started = @($start | & ssh @ssh $target 'bash -s' 2>&1)
    $started | ForEach-Object { Write-Host "   $_" }
}

Write-Host "MystTiq v$packageVersion is installed on $HostName; the MystTiq launcher is on $UserName's desktop." -ForegroundColor Green
