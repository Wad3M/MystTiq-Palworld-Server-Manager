# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$LinuxHost = '192.168.1.122',
    [string]$LinuxUser = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519",
    [switch]$SkipReboot
)
$ErrorActionPreference = 'Stop'
# v1.0.2.0 (roadmap R-3): the Linux headless service under systemd, on the test VM: installed as its own unit, crashed
# (SIGKILL), the VM rebooted, then stopped and removed (Test-v1.0.2.0-LinuxSystemd.sh does each phase). The production
# unit and folders are never touched. Rebooting the VM restarts whatever else runs on it; -SkipReboot leaves that out.
$root = (Resolve-Path $ProjectRoot).Path
$work = Join-Path $root ("artifacts\linux-systemd\v1.0.2.0-" + [guid]::NewGuid().ToString('N'))
New-Item $work -ItemType Directory -Force | Out-Null
& dotnet publish (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $work 'app') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish failed' }
[IO.File]::WriteAllText((Join-Path $work 'check.sh'), [IO.File]::ReadAllText((Join-Path $root 'scripts\Test-v1.0.2.0-LinuxSystemd.sh')).Replace("`r`n", "`n"))
$tgz = Join-Path $work 'linux-systemd.tgz'
Push-Location $work; tar -czf $tgz app check.sh; Pop-Location

$sshArgs = @('-i', [Environment]::ExpandEnvironmentVariables($IdentityFile), '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10')
$remote = "$LinuxUser@$LinuxHost"
$dir = '/tmp/mysttiq-v1020-systemd'
function Phase([string]$name) {
    $lines = @(ssh @sshArgs $remote "cd $dir && bash check.sh $name; echo exit=`$?" 2>&1)
    $lines | ForEach-Object { Write-Host ("$_" -replace '\b(\d{1,3}\.){3}\d{1,3}\b', '<ip>') }
    return [bool]($lines -match '^exit=0$')
}
ssh @sshArgs $remote "rm -rf $dir; mkdir -p $dir"
if ($LASTEXITCODE -ne 0) { throw "Cannot reach $remote over SSH (the VM's address may have changed; pass -LinuxHost)." }
scp @sshArgs $tgz "${remote}:$dir/" | Out-Null
ssh @sshArgs $remote "cd $dir && tar -xzf linux-systemd.tgz" | Out-Null
$okAll = $true
try {
    $okAll = (Phase 'install') -and $okAll
    $okAll = (Phase 'crash') -and $okAll
    if (-not $SkipReboot) {
        ssh @sshArgs $remote 'sudo -n systemctl reboot' 2>$null | Out-Null
        Start-Sleep 20
        $back = $false
        for ($i = 0; $i -lt 40 -and -not $back; $i++) { ssh @sshArgs $remote 'true' 2>$null | Out-Null; $back = $LASTEXITCODE -eq 0; if (-not $back) { Start-Sleep 5 } }
        if (-not $back) { throw 'The VM did not come back after the reboot.' }
        $okAll = (Phase 'after-reboot') -and $okAll
    }
    $okAll = (Phase 'stop') -and $okAll
}
finally {
    $okAll = (Phase 'cleanup') -and $okAll
    ssh @sshArgs $remote "rm -rf $dir" | Out-Null
    Remove-Item -LiteralPath $work -Recurse -Force
}
if (-not $okAll) { throw 'MystTiq v1.0.2.0 Linux systemd check failed.' }
Write-Host 'MystTiq v1.0.2.0 Linux systemd check passed.' -ForegroundColor Green
