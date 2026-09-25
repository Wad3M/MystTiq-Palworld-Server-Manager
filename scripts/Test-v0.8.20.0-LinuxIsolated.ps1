[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$LinuxHost = '192.168.1.122',
    [string]$LinuxUser = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519"
)
$ErrorActionPreference = 'Stop'
# v0.8.20.0: an isolated MystTiq on the Linux test VM for the HOST tab's history (/proc readings, the fleet folder, a restart). It publishes
# the linux-x64 headless, copies it with Test-v0.8.20.0-LinuxIsolated.sh to a temp folder on the VM, runs the check
# there (own config, FleetRoot, runtime and port 18420, no server needed; the
# installed service and /etc/mysttiq are never touched), and deletes the folder afterwards. IPv4 addresses in the
# output are masked. The VM's address changes (DHCP), so pass -LinuxHost when it moves.
$root = (Resolve-Path $ProjectRoot).Path
$work = Join-Path $root ("artifacts\linux-isolated\v0.8.20.0-" + [guid]::NewGuid().ToString('N'))
New-Item (Join-Path $work 'fixture\server\Pal\Saved\SaveGames') -ItemType Directory -Force | Out-Null

& dotnet publish (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $work 'app') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish failed' }
$sh = [IO.File]::ReadAllText((Join-Path $root 'scripts\Test-v0.8.20.0-LinuxIsolated.sh')).Replace("`r`n", "`n")
[IO.File]::WriteAllText((Join-Path $work 'check.sh'), $sh)
$tgz = Join-Path $work 'linux-isolated.tgz'
Push-Location $work; tar -czf $tgz app fixture check.sh; Pop-Location

$sshArgs = @('-i', [Environment]::ExpandEnvironmentVariables($IdentityFile), '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10')
$remote = "$LinuxUser@$LinuxHost"
$dir = '/tmp/mysttiq-v0820-isolated'
ssh @sshArgs $remote "rm -rf $dir; mkdir -p $dir"
if ($LASTEXITCODE -ne 0) { throw "Cannot reach $remote over SSH (the VM's address may have changed; pass -LinuxHost)." }
scp @sshArgs $tgz "${remote}:$dir/" | Out-Null
$lines = @(ssh @sshArgs $remote "cd $dir && tar -xzf linux-isolated.tgz && bash check.sh; echo exit=`$?; cd /; rm -rf $dir" 2>&1)
$lines | ForEach-Object { "$_" -replace '\b(\d{1,3}\.){3}\d{1,3}\b', '<ip>' }
Remove-Item -LiteralPath $work -Recurse -Force
if (-not ($lines -match '^exit=0$')) { throw 'MystTiq v0.8.20.0 Linux isolated check failed.' }
Write-Host 'MystTiq v0.8.20.0 Linux isolated check passed.' -ForegroundColor Green
