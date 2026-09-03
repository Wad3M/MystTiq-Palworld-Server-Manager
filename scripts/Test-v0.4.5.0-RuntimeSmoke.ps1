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
$temp=Join-Path $root ("artifacts\runtime-smoke\v0.4.5.0-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config=Join-Path $temp 'mysttiq.json'
$runtime=Join-Path $temp 'runtime'
New-Item $runtime -ItemType Directory -Force | Out-Null
$log=Join-Path $temp 'headless.log'
$err=Join-Path $temp 'headless.err.log'
$proc=$null
try{
    $args=@('api-run','--config',$config,'--bind-address','127.0.0.1','--api-port',"$Port",'--runtime-root',$runtime)
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

    $poll=Invoke-RestMethod "$base/api/v1/status/poll?lines=5" -TimeoutSec 5
    if($null -eq $poll.status -or $null -eq $poll.service -or $null -eq $poll.players -or $null -eq $poll.metrics -or $null -eq $poll.logTail){
        throw 'Aggregate status polling payload was incomplete.'
    }
    Write-Host '[PASS] Runtime Smoke :: Aggregate status endpoint works on Windows' -ForegroundColor Green

    $configDto=Invoke-RestMethod "$base/api/v1/config/editable" -TimeoutSec 5
    if($null -eq $configDto.server){ throw 'Editable configuration endpoint did not return server configuration.' }
    Write-Host '[PASS] Runtime Smoke :: Configuration endpoint works on Windows' -ForegroundColor Green

    $distribution=Invoke-RestMethod "$base/api/v1/server/distribution" -TimeoutSec 5
    if($null -eq $distribution.platform){ throw 'Server distribution endpoint did not return platform data.' }
    Write-Host '[PASS] Runtime Smoke :: Server distribution endpoint works on Windows' -ForegroundColor Green
}
finally{
    if($proc -and -not $proc.HasExited){ Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}
