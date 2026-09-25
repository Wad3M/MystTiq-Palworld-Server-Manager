[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$LinuxHost = '192.168.1.122',
    [string]$LinuxUser = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519",
    [int]$Port = 18422,
    [switch]$SkipLinuxDesktop
)
$ErrorActionPreference = 'Stop'
# v0.8.22.0: the remaining sign-in tests, on top of v0.8.15.0's (same isolated instance, same setup/teardown scripts):
#   - four accounts: Viewer, Operator, Admin and now Owner;
#   - the harness also checks the v0.8.19.0 Ribbon gating in the signed-in window, and the server agreeing with it;
#   - the Desktop itself running on Linux: the same harness is published for linux-x64 and run on the VM, where it
#     signs in over TLS to the instance there. Its home folder points into the instance's /tmp folder, so nothing is
#     written to the VM user's real settings; the script checks that.
# Afterwards the instance is stopped and its folder, token, certificate, accounts and the Linux harness deleted. The
# VM's installed service and /etc/mysttiq are never touched; nothing on this PC's real Desktop setup is read or written.
$root = (Resolve-Path $ProjectRoot).Path
$work = Join-Path $root ("artifacts\remote-signin\v0.8.22.0-" + [guid]::NewGuid().ToString('N'))
New-Item $work -ItemType Directory -Force | Out-Null
function New-Password { -join ((1..20) | ForEach-Object { 'abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'[(Get-Random -Maximum 56)] }) }
$accounts = @(
    [pscustomobject]@{ Role = 'Viewer'; Number = 0; Username = 'rs-viewer'; Password = (New-Password) },
    [pscustomobject]@{ Role = 'Operator'; Number = 1; Username = 'rs-operator'; Password = (New-Password) },
    [pscustomobject]@{ Role = 'Admin'; Number = 2; Username = 'rs-admin'; Password = (New-Password) },
    [pscustomobject]@{ Role = 'Owner'; Number = 3; Username = 'rs-owner'; Password = (New-Password) }
)
$sshArgs = @('-i', [Environment]::ExpandEnvironmentVariables($IdentityFile), '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10')
$remote = "$LinuxUser@$LinuxHost"
$dir = '/tmp/mysttiq-v0822-remote'
$harnessProject = Join-Path $root 'scripts\Testing\MystTiq.RemoteSignInHarness'
$failures = @()
try {
    & dotnet publish (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $work 'app') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish of the service failed' }
    foreach ($name in 'setup', 'teardown') {
        $sh = [IO.File]::ReadAllText((Join-Path $root "scripts\Test-v0.8.15.0-RemoteSignIn.$name.sh")).Replace("`r`n", "`n")
        [IO.File]::WriteAllText((Join-Path $work "$name.sh"), $sh)
    }
    [IO.File]::WriteAllText((Join-Path $work 'accounts.txt'), (($accounts | ForEach-Object { "$($_.Number) $($_.Username) $($_.Password)" }) -join "`n") + "`n")
    $tgz = Join-Path $work 'remote-signin.tgz'
    Push-Location $work; tar -czf $tgz app setup.sh teardown.sh accounts.txt; Pop-Location

    ssh @sshArgs $remote "rm -rf $dir; mkdir -p $dir"
    if ($LASTEXITCODE -ne 0) { throw "Cannot reach $remote over SSH (the VM's address may have changed; pass -LinuxHost)." }
    scp @sshArgs $tgz "${remote}:$dir/" | Out-Null
    $setup = @(ssh @sshArgs $remote "cd $dir && tar -xzf remote-signin.tgz && rm -f remote-signin.tgz && bash setup.sh $LinuxHost $Port" 2>&1)
    $setup | Where-Object { $_ -notmatch '^PIN=' } | ForEach-Object { "   $_" }
    if (-not ($setup -match '^READY$')) { throw 'the remote instance did not start' }
    if (@($setup -match '^USER-CREATED').Count -ne $accounts.Count) { throw 'not every test account was created' }
    $pin = (@($setup -match '^PIN=')[0]).Substring(4).Trim()
    $settingsFor = { param([string]$Prefix) @{ baseUrl = "https://${LinuxHost}:$Port/"; pin = $pin; renderPrefix = $Prefix; accounts = @($accounts | ForEach-Object { @{ role = $_.Role; username = $_.Username; password = $_.Password } }) } | ConvertTo-Json -Depth 4 }

    # 1. The Desktop on Windows (this PC), all four roles.
    Write-Host '   -- the Desktop on Windows --'
    $settings = Join-Path $work 'settings.json'
    Set-Content $settings (& $settingsFor 'remote')
    Push-Location $harnessProject
    try { & dotnet run -c Release -- $settings (Join-Path $work 'renders'); $windowsExit = $LASTEXITCODE }
    finally { Pop-Location }
    if ($windowsExit -ne 0) { $failures += 'the Desktop on Windows' }

    # 2. The Desktop on Linux: the same harness, published for linux-x64, run on the VM against the same instance.
    if (-not $SkipLinuxDesktop) {
        Write-Host '   -- the Desktop on Linux (the VM) --'
        $linuxHarness = Join-Path $work 'harness'
        & dotnet publish (Join-Path $harnessProject 'MystTiq.RemoteSignInHarness.csproj') -c Release -r linux-x64 --self-contained true -o $linuxHarness | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish of the sign-in harness failed' }
        [IO.File]::WriteAllText((Join-Path $work 'settings-linux.json'), (& $settingsFor 'linux'))
        $htgz = Join-Path $work 'harness.tgz'
        Push-Location $work; tar -czf $htgz harness settings-linux.json; Pop-Location
        scp @sshArgs $htgz "${remote}:$dir/" | Out-Null
        # What the VM user's real settings folders hold before and after: the run must not add or change anything there.
        $probe = 'for d in "$HOME/.config/MystTiq" "$HOME/.local/share/MystTiq" "$HOME/.config/mysttiq" "$HOME/.local/share/mysttiq"; do [ -e "$d" ] && find "$d" -type f -printf "%p %s %T@\n"; done | sort; echo PROBE-END'
        $before = @(ssh @sshArgs $remote $probe 2>&1)
        $run = "cd $dir && tar -xzf harness.tgz && rm -f harness.tgz && mkdir -p home/.config home/.local/share && chmod +x harness/MystTiq.RemoteSignInHarness && " +
            "HOME=$dir/home XDG_CONFIG_HOME=$dir/home/.config XDG_DATA_HOME=$dir/home/.local/share ./harness/MystTiq.RemoteSignInHarness settings-linux.json renders; echo LINUX-EXIT=`$?"
        $linux = @(ssh @sshArgs $remote $run 2>&1)
        $linux | Where-Object { $_ -notmatch '^LINUX-EXIT=' } | ForEach-Object { "   $_" }
        $linuxExit = @($linux -match '^LINUX-EXIT=')
        if ($linuxExit.Count -ne 1 -or $linuxExit[0] -ne 'LINUX-EXIT=0') { $failures += 'the Desktop on Linux' }
        $after = @(ssh @sshArgs $remote $probe 2>&1)
        if (($before -join "`n") -ne ($after -join "`n")) { Write-Host '[FAIL] the Linux run wrote to the VM user''s settings folders' -ForegroundColor Red; $failures += 'the VM user''s settings untouched' }
        else { Write-Host 'PASS the Linux run left the VM user''s settings folders as they were' }
        New-Item (Join-Path $work 'renders-linux') -ItemType Directory -Force | Out-Null
        scp @sshArgs "${remote}:$dir/renders/linux-*.png" (Join-Path $work 'renders-linux') 2>$null | Out-Null
        Copy-Item (Join-Path $work 'renders-linux\linux-*.png') (Join-Path $root 'artifacts\remote-signin') -ErrorAction SilentlyContinue
    }
    Copy-Item (Join-Path $work 'renders\remote-*.png') (Join-Path $root 'artifacts\remote-signin') -ErrorAction SilentlyContinue
}
finally {
    $down = @(ssh @sshArgs $remote "bash $dir/teardown.sh" 2>&1)
    Write-Host "   remote instance removed: $([bool]($down -match 'TORN-DOWN'))"
    foreach ($secret in 'settings.json', 'settings-linux.json', 'accounts.txt') { Remove-Item -LiteralPath (Join-Path $work $secret) -Force -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath (Join-Path $work 'harness.tgz'), (Join-Path $work 'remote-signin.tgz') -Force -ErrorAction SilentlyContinue
}
if ($failures.Count -gt 0) { throw "MystTiq v0.8.22.0 remote sign-in failed: $($failures -join ', ')" }
Write-Host 'MystTiq v0.8.22.0 remote sign-in passed (Windows and Linux Desktop, four roles).' -ForegroundColor Green
exit 0
