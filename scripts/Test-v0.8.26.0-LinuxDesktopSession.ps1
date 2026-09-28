# MystTiq v0.9.3.0: file reviewed for this release (2026-09-28).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$LinuxHost = '192.168.1.122',
    [string]$LinuxUser = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519",
    [int]$Port = 18426
)
$ErrorActionPreference = 'Stop'
# v0.8.26.0: the Desktop in a real Linux desktop session. The VM runs XFCE with the test user logged in on display :0.
# The sign-in harness (the real MainWindow, ViewModel and API client; local services stubbed, so the VM's installed
# MystTiq on 8213 is never contacted) is published for linux-x64 and run on that display in "realWindow" mode, against
# an isolated remote instance in /tmp (the v0.8.15.0 setup/teardown). Besides every sign-in check it checks what only a
# real session has: an X11 window the window manager maximizes and restores, the clipboard through the X server, a tray
# icon on the session bus, and screenshots of the display. The harness's home folder is in /tmp, the VM user's settings
# are checked untouched, and everything is removed afterwards. Nothing is installed; /etc/mysttiq is never touched.
$root = (Resolve-Path $ProjectRoot).Path
$work = Join-Path $root ("artifacts\linux-session\v0.8.26.0-" + [guid]::NewGuid().ToString('N'))
New-Item $work -ItemType Directory -Force | Out-Null
function New-Password { -join ((1..20) | ForEach-Object { 'abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'[(Get-Random -Maximum 56)] }) }
$account = [pscustomobject]@{ Role = 'Admin'; Number = 2; Username = 'ls-admin'; Password = (New-Password) }
$sshArgs = @('-i', [Environment]::ExpandEnvironmentVariables($IdentityFile), '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10')
$remote = "$LinuxUser@$LinuxHost"
$dir = '/tmp/mysttiq-v0826-session'
$harnessProject = Join-Path $root 'scripts\Testing\MystTiq.RemoteSignInHarness'
$failed = $true
try {
    & dotnet publish (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $work 'app') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish of the service failed' }
    & dotnet publish (Join-Path $harnessProject 'MystTiq.RemoteSignInHarness.csproj') -c Release -r linux-x64 --self-contained true -o (Join-Path $work 'harness') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish of the harness failed' }
    foreach ($name in 'setup', 'teardown') {
        $sh = [IO.File]::ReadAllText((Join-Path $root "scripts\Test-v0.8.15.0-RemoteSignIn.$name.sh")).Replace("`r`n", "`n")
        [IO.File]::WriteAllText((Join-Path $work "$name.sh"), $sh)
    }
    [IO.File]::WriteAllText((Join-Path $work 'accounts.txt'), "$($account.Number) $($account.Username) $($account.Password)`n")
    $tgz = Join-Path $work 'session.tgz'
    Push-Location $work; tar -czf $tgz app harness setup.sh teardown.sh accounts.txt; Pop-Location

    ssh @sshArgs $remote "rm -rf $dir; mkdir -p $dir"
    if ($LASTEXITCODE -ne 0) { throw "Cannot reach $remote over SSH (the VM's address may have changed; pass -LinuxHost)." }
    $display = @(ssh @sshArgs $remote 'DISPLAY=:0 XAUTHORITY=$HOME/.Xauthority xwininfo -root 2>&1 | grep -c Width' 2>&1)
    if ($display[0] -ne '1') { throw 'The VM has no reachable desktop session on display :0 (is the test user logged in to the desktop?).' }
    scp @sshArgs $tgz "${remote}:$dir/" | Out-Null
    $setup = @(ssh @sshArgs $remote "cd $dir && tar -xzf session.tgz && rm -f session.tgz && bash setup.sh $LinuxHost $Port" 2>&1)
    if (-not ($setup -match '^READY$') -or -not ($setup -match '^USER-CREATED')) { $setup | ForEach-Object { "   $_" }; throw 'the isolated instance did not start' }
    $pin = (@($setup -match '^PIN=')[0]).Substring(4).Trim()
    @{ baseUrl = "https://${LinuxHost}:$Port/"; pin = $pin; renderPrefix = 'linux-desktop'; realWindow = $true
       accounts = @(@{ role = $account.Role; username = $account.Username; password = $account.Password }) } | ConvertTo-Json -Depth 4 |
        Set-Content (Join-Path $work 'settings.json')
    scp @sshArgs (Join-Path $work 'settings.json') "${remote}:$dir/settings.json" | Out-Null

    $probe = 'for d in "$HOME/.config/MystTiq" "$HOME/.local/share/MystTiq" "$HOME/.config/mysttiq" "$HOME/.local/share/mysttiq"; do [ -e "$d" ] && find "$d" -type f -printf "%p %s %T@\n"; done | sort; echo PROBE-END'
    $before = @(ssh @sshArgs $remote $probe 2>&1)
    # The real session: the user's display and session bus; the harness's own settings in /tmp.
    $run = "cd $dir && mkdir -p home/.config home/.local/share renders && chmod +x harness/MystTiq.RemoteSignInHarness && " +
        "DISPLAY=:0 XAUTHORITY=/home/$LinuxUser/.Xauthority XDG_RUNTIME_DIR=/run/user/`$(id -u) DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/`$(id -u)/bus " +
        "HOME=$dir/home XDG_CONFIG_HOME=$dir/home/.config XDG_DATA_HOME=$dir/home/.local/share ./harness/MystTiq.RemoteSignInHarness settings.json renders; echo SESSION-EXIT=`$?"
    $out = @(ssh @sshArgs $remote $run 2>&1)
    $out | Where-Object { $_ -notmatch '^SESSION-EXIT=' } | ForEach-Object { "   $_" -replace '\b(\d{1,3}\.){3}\d{1,3}\b', '<vm>' }
    $after = @(ssh @sshArgs $remote $probe 2>&1)
    $untouched = ($before -join "`n") -eq ($after -join "`n")
    Write-Host ("{0} the VM user's MystTiq settings folders are as they were" -f $(if ($untouched) { 'PASS' } else { 'FAIL' }))
    New-Item (Join-Path $root 'artifacts\linux-session') -ItemType Directory -Force | Out-Null
    scp @sshArgs "${remote}:$dir/renders/*.png" (Join-Path $root 'artifacts\linux-session') 2>$null | Out-Null
    $failed = -not ($untouched -and @($out -match '^SESSION-EXIT=0$').Count -eq 1)
}
finally {
    $down = @(ssh @sshArgs $remote "bash $dir/teardown.sh" 2>&1)
    Write-Host "   isolated instance removed: $([bool]($down -match 'TORN-DOWN'))"
    foreach ($secret in 'settings.json', 'accounts.txt') { Remove-Item -LiteralPath (Join-Path $work $secret) -Force -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath (Join-Path $work 'session.tgz') -Force -ErrorAction SilentlyContinue
}
if ($failed) { throw 'MystTiq v0.8.26.0 Linux desktop session check failed.' }
Write-Host 'MystTiq v0.8.26.0 Linux desktop session check passed (the real window on the VM''s XFCE desktop).' -ForegroundColor Green
exit 0
