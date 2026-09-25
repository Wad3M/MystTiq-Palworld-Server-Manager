[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$LinuxHost = '192.168.1.122',
    [string]$LinuxUser = 'mystroth',
    [string]$IdentityFile = "$HOME\.ssh\mysttiq_linux_ed25519",
    [int]$Port = 18415
)
$ErrorActionPreference = 'Stop'
# v0.8.15.0: a real remote Desktop sign-in. An isolated MystTiq is started on the Linux test VM with remote access
# switched on the documented way (bearer token, self-signed TLS certificate, api-remote-enable) and three accounts
# (Viewer, Operator, Admin) with passwords generated for this run. The Desktop's own ViewModel, window and API client
# (with certificate pinning) then sign in over the network as each one (scripts/Testing/MystTiq.RemoteSignInHarness).
# Afterwards the instance is stopped and its folder, token, certificate and accounts deleted. The VM's installed
# service and /etc/mysttiq are never touched; nothing on this PC's real Desktop setup is read or written. The VM's
# address changes (DHCP): pass -LinuxHost.
$root = (Resolve-Path $ProjectRoot).Path
$work = Join-Path $root ("artifacts\remote-signin\v0.8.15.0-" + [guid]::NewGuid().ToString('N'))
New-Item $work -ItemType Directory -Force | Out-Null
function New-Password { -join ((1..20) | ForEach-Object { 'abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'[(Get-Random -Maximum 56)] }) }
$accounts = @(
    [pscustomobject]@{ Role = 'Viewer'; Number = 0; Username = 'rs-viewer'; Password = (New-Password) },
    [pscustomobject]@{ Role = 'Operator'; Number = 1; Username = 'rs-operator'; Password = (New-Password) },
    [pscustomobject]@{ Role = 'Admin'; Number = 2; Username = 'rs-admin'; Password = (New-Password) }
)
$sshArgs = @('-i', [Environment]::ExpandEnvironmentVariables($IdentityFile), '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10')
$remote = "$LinuxUser@$LinuxHost"
$dir = '/tmp/mysttiq-v0815-remote'
$tornDown = $false
try {
    & dotnet publish (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $work 'app') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish failed' }
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

    $settings = Join-Path $work 'settings.json'
    @{ baseUrl = "https://${LinuxHost}:$Port/"; pin = $pin; accounts = @($accounts | ForEach-Object { @{ role = $_.Role; username = $_.Username; password = $_.Password } }) } |
        ConvertTo-Json -Depth 4 | Set-Content $settings
    Push-Location (Join-Path $root 'scripts\Testing\MystTiq.RemoteSignInHarness')
    try { & dotnet run -c Release -- $settings (Join-Path $work 'renders'); $harnessExit = $LASTEXITCODE }
    finally { Pop-Location }
    Copy-Item (Join-Path $work 'renders\remote-*.png') (Join-Path $root 'artifacts\remote-signin') -ErrorAction SilentlyContinue
    if ($harnessExit -ne 0) { throw 'MystTiq v0.8.15.0 remote sign-in failed.' }
}
finally {
    $down = @(ssh @sshArgs $remote "bash $dir/teardown.sh" 2>&1)
    $tornDown = [bool]($down -match 'TORN-DOWN')
    Write-Host "   remote instance removed: $tornDown"
    Remove-Item -LiteralPath (Join-Path $work 'settings.json') -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $work 'accounts.txt') -Force -ErrorAction SilentlyContinue
}
Write-Host 'MystTiq v0.8.15.0 remote sign-in passed.' -ForegroundColor Green
