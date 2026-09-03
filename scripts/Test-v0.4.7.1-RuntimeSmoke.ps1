[CmdletBinding()]
param(
    [string]$ProjectRoot='.',
    [int]$Port=18214
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
if(-not $IsWindows){
    Write-Host '[SKIP] Windows local sidecar runtime smoke test requires Windows.' -ForegroundColor Yellow
    return
}

$exe=Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if(-not (Test-Path $exe -PathType Leaf)){ throw "Headless desktop sidecar not found: $exe" }
$temp=Join-Path $root ("artifacts\runtime-smoke\v0.4.7.1-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config=Join-Path $temp 'mysttiq.json'
$runtime=Join-Path $temp 'runtime'
$missingServer=Join-Path $temp 'missing-server'
$steamCmd=Join-Path $temp 'missing-steamcmd.exe'
$backupRoot=Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $missingServer -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$log=Join-Path $temp 'headless.log'
$err=Join-Path $temp 'headless.err.log'
$proc=$null
try{
    $args=@('api-run','--desktop-sidecar','--config',$config,'--bind-address','127.0.0.1','--api-port',"$Port",'--server-root',$missingServer,'--steamcmd',$steamCmd,'--backup-root',$backupRoot,'--runtime-root',$runtime)
    $proc=Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err
    $base="http://127.0.0.1:$Port"
    $ready=$false
    for($i=0;$i -lt 40;$i++){
        Start-Sleep -Milliseconds 250
        if($proc.HasExited){ break }
        try{
            $health=Invoke-RestMethod "$base/healthz" -TimeoutSec 2
            if($health){$ready=$true;break}
        }catch{}
    }
    if(-not $ready){
        $stderr=if(Test-Path $err){Get-Content $err -Raw}else{''}
        throw "Windows headless API did not become healthy on $base. $stderr"
    }
    Write-Host '[PASS] Runtime Smoke :: Windows headless API starts and answers /healthz' -ForegroundColor Green

    if($health.component -ne 'mysttiq-headless' -or [int]$health.apiVersion -lt 1 -or [string]::IsNullOrWhiteSpace([string]$health.version)){
        throw 'Versioned health handshake did not identify a compatible MystTiq backend.'
    }
    if([bool]$health.authentication -or [bool]$health.tls){
        throw 'Desktop-owned loopback sidecar unexpectedly enabled authentication or TLS.'
    }
    Write-Host '[PASS] Runtime Smoke :: Versioned health handshake identifies the desktop-owned local backend' -ForegroundColor Green

    $poll=Invoke-RestMethod "$base/api/v1/status/poll?lines=5" -TimeoutSec 5
    if($null -eq $poll.status -or $null -eq $poll.service -or $null -eq $poll.players -or $null -eq $poll.metrics -or $null -eq $poll.logTail){
        throw 'Aggregate status polling payload was incomplete.'
    }
    Write-Host '[PASS] Runtime Smoke :: Aggregate status endpoint works on Windows' -ForegroundColor Green

    $history=Invoke-RestMethod "$base/api/v1/history?hours=1&maxSamples=60" -TimeoutSec 5
    if($null -eq $history.samples -or $null -eq $history.averageCpu -or $null -eq $history.averageMemoryMb){
        throw 'Historical metrics endpoint did not return the expected graph snapshot.'
    }
    Write-Host '[PASS] Runtime Smoke :: Historical metrics endpoint returns dashboard graph data' -ForegroundColor Green

    $configDto=Invoke-RestMethod "$base/api/v1/config/editable" -TimeoutSec 5
    if($null -eq $configDto.server){ throw 'Editable configuration endpoint did not return server configuration.' }
    Write-Host '[PASS] Runtime Smoke :: Configuration endpoint works on Windows' -ForegroundColor Green

    $palConfig=Invoke-RestMethod "$base/api/v1/palworld/config" -TimeoutSec 5
    if($null -eq $palConfig.configurationPath -or $null -eq $palConfig.settings){ throw 'Palworld configuration endpoint did not return a safe snapshot.' }
    Write-Host '[PASS] Runtime Smoke :: Palworld configuration endpoint returns a safe snapshot' -ForegroundColor Green

    $environment=Invoke-RestMethod "$base/api/v1/server/environment" -TimeoutSec 5
    if($null -eq $environment.items -or [int]$environment.totalCount -lt 10){ throw 'Server environment endpoint did not return the expected checklist.' }
    $missingCapability=@($environment.items | Where-Object { $_.PSObject.Properties.Name -notcontains 'actionSupported' })
    if($missingCapability.Count -gt 0){ throw 'Server environment capability truth contract is missing actionSupported metadata.' }
    $unsupported=@($environment.items | Where-Object { $_.actionSupported -eq $false })
    if(@($unsupported | Where-Object { [string]::IsNullOrWhiteSpace($_.unavailableReason) }).Count -gt 0){ throw 'Unsupported server environment actions must include unavailableReason.' }
    Write-Host '[PASS] Runtime Smoke :: Server Setup environment checklist returns real rows plus explicit backend capability truth' -ForegroundColor Green

    $distribution=Invoke-RestMethod "$base/api/v1/server/distribution" -TimeoutSec 5
    if($null -eq $distribution.platform){ throw 'Server distribution endpoint did not return platform data.' }
    Write-Host '[PASS] Runtime Smoke :: Server distribution endpoint works on Windows' -ForegroundColor Green

    $adminBody = @{ action='promote'; message='runtime-smoke'; item=$null } | ConvertTo-Json -Compress
    try {
        Invoke-RestMethod "$base/api/v1/players/runtime-smoke-user/action" -Method Post -ContentType 'application/json' -Body $adminBody -TimeoutSec 5 | Out-Null
        throw 'Capability-gated promote test unexpectedly reported success.'
    }
    catch {
        $response = $_.Exception.Response
        if($null -eq $response -or [int]$response.StatusCode -ne 422){ throw }
    }
    Write-Host '[PASS] Runtime Smoke :: Player administration route is live and unsupported vanilla actions are capability-gated' -ForegroundColor Green

    $activity = Invoke-RestMethod "$base/api/v1/activity/tail?lines=20" -TimeoutSec 5
    if($null -eq $activity.lines -or -not ($activity.lines -join "`n").Contains('Player promote unavailable')){
        throw 'Persistent activity/audit log did not capture the player administration event.'
    }
    Write-Host '[PASS] Runtime Smoke :: Persistent activity/audit log captures management events' -ForegroundColor Green

    try {
        Invoke-RestMethod "$base/api/v1/server/start" -Method Post -TimeoutSec 5 | Out-Null
        throw 'Lifecycle safety test unexpectedly started a server from the isolated missing-server root.'
    }
    catch {
        $response = $_.Exception.Response
        if($null -eq $response -or [int]$response.StatusCode -ne 424){ throw }
    }
    Write-Host '[PASS] Runtime Smoke :: Start endpoint is reachable and safely reports missing PalServer executable' -ForegroundColor Green
}
finally{
    if($proc -and -not $proc.HasExited){ Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}
