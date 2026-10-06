# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    # Also install the Linux Palworld server inside the container (SteamCMD, several GB) and run it on a copy of a
    # server's world (the clone), then stop it. Without this, the image is built, started and its API checked.
    [switch]$RunServer,
    [string]$CloneServerRoot = 'C:\GameServers\Palworld\Server-clone-second-local',
    [int]$ApiPort = 18213,
    [int]$GamePort = 18411
)
$ErrorActionPreference = 'Stop'
# v1.0.3.0 (roadmap P-1): the headless service as a Linux container image (deploy/docker), built and run on Docker Desktop
# with its own data volume: first start, the API over HTTPS with the generated token, and (-RunServer) the server itself.
# The data lives in a Docker volume: with a folder bind-mounted from Windows the server exited before it was ready.
# The container and its volume are removed afterwards; the image (tagged with the MystTiq version) is kept.
$root = (Resolve-Path $ProjectRoot).Path
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.VersionPrefix | Where-Object { $_ } | Select-Object -First 1
$image = "mysttiq-headless:$version"
$name = 'mysttiq-p1-test'
$volume = 'mysttiq-p1-test-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$work = Join-Path $root ("artifacts\docker\v1.0.3.0-" + [guid]::NewGuid().ToString('N'))
$context = Join-Path $work 'context'
New-Item $context -ItemType Directory -Force | Out-Null
function Say([string]$text) { Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] $text" }
function InContainer([string]$command) { & docker exec $name sh -c $command }

& docker info --format '{{.OSType}}' 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Docker is not running.' }
& dotnet publish (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o (Join-Path $context 'app') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'linux-x64 publish failed' }
foreach ($f in 'Dockerfile', 'entrypoint.sh') {
    [IO.File]::WriteAllText((Join-Path $context $f), [IO.File]::ReadAllText((Join-Path $root "deploy\docker\$f")).Replace("`r`n", "`n"))
}
& docker build -q -t $image --build-arg "VERSION=$version" $context | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'docker build failed' }
$label = & docker image inspect $image --format '{{ index .Config.Labels "org.opencontainers.image.version" }}'
Say "built $image (label version $label)"
if ($label -ne $version) { throw "image label $label is not $version" }

& docker rm -f $name 2>$null | Out-Null
& docker volume create $volume | Out-Null
& docker run -d --name $name -p "${ApiPort}:8213" -p "${GamePort}:8211/udp" -v "${volume}:/data" $image | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'docker run failed' }
$ok = $true
try {
    $health = $null
    for ($i = 0; $i -lt 60 -and -not $health; $i++) {
        try { $health = Invoke-RestMethod "https://127.0.0.1:$ApiPort/healthz" -SkipCertificateCheck -TimeoutSec 3 } catch { Start-Sleep 2 }
    }
    if (-not $health) { throw "the container did not answer: $(& docker logs $name 2>&1 | Select-Object -Last 10)" }
    Say "healthz: version $($health.version), $($health.platform), api $($health.api), authentication $($health.authentication), tls $($health.tls)"
    $config = (InContainer 'cat /data/mysttiq.json') -join "`n" | ConvertFrom-Json
    $checks = [ordered]@{
        'the image runs the service of this version'          = ($health.version -eq $version)
        'it runs on Linux'                                     = ($health.platform -eq 'linux')
        'the API is remote-secured (token and TLS)'            = ($health.api -eq 'remote-secured' -and $health.authentication -and $health.tls)
        'every path in the configuration is under /data'      = ($config.Servers[0].ServerRoot -eq '/data/server' -and $config.FleetRoot -eq '/data/fleet')
        'the service runs as the container''s own user, not root' = ((InContainer 'id -un') -eq 'mysttiq')
    }
    $token = ((InContainer 'cat /data/secrets/api-token') -join '').Trim()
    $headers = @{ Authorization = "Bearer $token" }
    $base = "https://127.0.0.1:$ApiPort/api/v1/servers/default"
    $checks['a request without the token is refused'] = ((Invoke-WebRequest "$base/status" -SkipCertificateCheck -TimeoutSec 10 -SkipHttpErrorCheck).StatusCode -eq 401)
    $status = Invoke-RestMethod "$base/status" -Headers $headers -SkipCertificateCheck -TimeoutSec 10
    $checks['with the token, the server status is served'] = ($null -ne $status.phase)

    if ($RunServer) {
        Say 'installing the Linux Palworld server inside the container (SteamCMD)…'
        $update = Invoke-RestMethod -Method Post "$base/server/distribution/update?validate=false" -Headers $headers -SkipCertificateCheck -TimeoutSec 3600 -SkipHttpErrorCheck
        Say "install: $($update.message)"
        $checks['SteamCMD installed the Linux server into /data/server'] = [bool]($update.success)
        # The clone's world (its save and settings), copied in: the Linux server reads Config/LinuxServer.
        InContainer 'mkdir -p /data/server/Pal/Saved/Config/LinuxServer && rm -rf /data/server/Pal/Saved/SaveGames' | Out-Null
        & docker cp (Join-Path $CloneServerRoot 'Pal\Saved\SaveGames') "${name}:/data/server/Pal/Saved/SaveGames" | Out-Null
        foreach ($ini in 'GameUserSettings.ini', 'PalWorldSettings.ini') {
            & docker cp (Join-Path $CloneServerRoot "Pal\Saved\Config\WindowsServer\$ini") "${name}:/data/server/Pal/Saved/Config/LinuxServer/$ini" | Out-Null
        }
        & docker exec -u 0 $name chown -R mysttiq:mysttiq /data/server/Pal/Saved | Out-Null
        $start = Invoke-RestMethod -Method Post "$base/server/start" -Headers $headers -SkipCertificateCheck -TimeoutSec 300 -SkipHttpErrorCheck
        Say "start: $($start.message)"
        $checks['the clone world runs in the container (server ready)'] = [bool]$start.snapshot.ready
        $players = Invoke-RestMethod "$base/players" -Headers $headers -SkipCertificateCheck -TimeoutSec 60
        $checks['its REST API answers inside the container'] = [bool]$players.available
        $stop = Invoke-RestMethod -Method Post "$base/server/stop" -Headers $headers -SkipCertificateCheck -TimeoutSec 180 -SkipHttpErrorCheck
        Say "stop: $($stop.message)"
        $after = Invoke-RestMethod "$base/status" -Headers $headers -SkipCertificateCheck
        $checks['and stops cleanly'] = (-not $after.ready -and @($after.processes).Count -eq 0)
    }
    foreach ($c in $checks.GetEnumerator()) {
        Write-Host ("[{0}] {1}" -f ($(if ($c.Value) { 'PASS' } else { 'FAIL' })), $c.Key)
        if (-not $c.Value) { $ok = $false }
    }
}
finally {
    & docker rm -f $name 2>$null | Out-Null
    & docker volume rm $volume 2>$null | Out-Null
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
if (-not $ok) { throw 'MystTiq v1.0.3.0 Docker check failed.' }
Write-Host "MystTiq v1.0.3.0 Docker check passed ($image)." -ForegroundColor Green
