# MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$LinuxHost = '192.168.1.122',
    [string]$LinuxUser = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519"
)
$ErrorActionPreference = 'Stop'
# v1.0.6.0 (roadmap R-3, carried): the Linux headless service under systemd on the test VM, as a per-user unit, so it
# needs no sudo and never reboots the VM (Test-v1.0.6.0-LinuxUserSystemd.sh: install, crash, stop, cleanup in one session).
# Starting at boot is the system unit's part; the v1.0.2.0 script covers it where passwordless sudo is allowed.
$root = (Resolve-Path $ProjectRoot).Path
$work = Join-Path $root ("artifacts\linux-systemd\v1.0.6.0-user-" + [guid]::NewGuid().ToString('N'))
New-Item $work -ItemType Directory -Force | Out-Null
& dotnet publish (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $work 'app') -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish failed' }
[IO.File]::WriteAllText((Join-Path $work 'check.sh'), [IO.File]::ReadAllText((Join-Path $root 'scripts\Test-v1.0.6.0-LinuxUserSystemd.sh')).Replace("`r`n", "`n"))
$tgz = Join-Path $work 'linux-user-systemd.tgz'
Push-Location $work; tar -czf $tgz app check.sh; Pop-Location
$sshArgs = @('-i', [Environment]::ExpandEnvironmentVariables($IdentityFile), '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10')
$remote = "$LinuxUser@$LinuxHost"
$dir = '/tmp/mysttiq-v1060-user-systemd'
try {
    ssh @sshArgs $remote "rm -rf $dir; mkdir -p $dir"
    if ($LASTEXITCODE -ne 0) { throw "Cannot reach $remote over SSH (the VM's address may have changed; pass -LinuxHost)." }
    scp @sshArgs $tgz "${remote}:$dir/" | Out-Null
    $lines = @(ssh @sshArgs $remote "cd $dir && tar -xzf linux-user-systemd.tgz && bash check.sh; echo exit=`$?; rm -rf $dir" 2>&1)
    $lines | ForEach-Object { Write-Host ("$_" -replace '\b(\d{1,3}\.){3}\d{1,3}\b', '<ip>') }
    if (-not ($lines -match '^exit=0$')) { throw 'MystTiq v1.0.6.0 Linux user-unit check failed.' }
}
finally { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host 'MystTiq v1.0.6.0 Linux user-unit check passed.' -ForegroundColor Green
