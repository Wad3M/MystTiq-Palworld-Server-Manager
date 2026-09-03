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
$temp=Join-Path $root ("artifacts\runtime-smoke\v0.4.13.0-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config=Join-Path $temp 'mysttiq.json'
$runtime=Join-Path $temp 'runtime'
$missingServer=Join-Path $temp 'missing-server'
$steamCmd=Join-Path $temp 'missing-steamcmd.exe'
$backupRoot=Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $missingServer -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$saveRoot=Join-Path $missingServer 'Pal\Saved\SaveGames\0\RuntimeSmokeWorld'
New-Item $saveRoot -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $saveRoot 'Level.sav') 'runtime-smoke-save'
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

    $createdBackup=Invoke-RestMethod "$base/api/v1/backups/create" -Method Post -TimeoutSec 10
    if(-not $createdBackup.success -or [string]::IsNullOrWhiteSpace([string]$createdBackup.fileName)){ throw 'Backup create route did not create a managed archive.' }
    $verifiedBackup=Invoke-RestMethod "$base/api/v1/backups/$($createdBackup.fileName)/verify" -Method Post -TimeoutSec 10
    if(-not $verifiedBackup.success -or [string]::IsNullOrWhiteSpace([string]$verifiedBackup.sha256) -or [int]$verifiedBackup.entryCount -lt 1){ throw 'Backup verification route did not deeply verify the created archive.' }
    if(-not (Test-Path (Join-Path $runtime 'backups\verification.json'))){ throw 'Backup verification result was not persisted.' }
    Write-Host '[PASS] Runtime Smoke :: Backup verification is deep, persisted and remote-safe' -ForegroundColor Green

    $preview=Invoke-RestMethod "$base/api/v1/backups/retention/preview" -Method Post -ContentType 'application/json' -Body (@{keepLatest=1;maxAgeDays=30}|ConvertTo-Json -Compress) -TimeoutSec 5
    if([string]::IsNullOrWhiteSpace([string]$preview.token)){ throw 'Retention preview did not return an immutable token.' }
    $applied=Invoke-RestMethod "$base/api/v1/backups/retention/apply" -Method Post -ContentType 'application/json' -Body (@{token=$preview.token}|ConvertTo-Json -Compress) -TimeoutSec 5
    if(-not $applied.success -or [int]$applied.deletedCount -ne 0){ throw 'Retention apply did not execute the exact empty preview.' }
    try {
        Invoke-RestMethod "$base/api/v1/backups/retention/apply" -Method Post -ContentType 'application/json' -Body (@{token=$preview.token}|ConvertTo-Json -Compress) -TimeoutSec 5 | Out-Null
        throw 'Consumed retention preview token was accepted twice.'
    } catch { if($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409){ throw } }
    Write-Host '[PASS] Runtime Smoke :: Retention preview token is single-use and apply executes exactly its preview' -ForegroundColor Green

    try {
        Invoke-RestMethod "$base/api/v1/backups/$($createdBackup.fileName)/restore" -Method Post -ContentType 'application/json' -Body (@{confirmed=$false}|ConvertTo-Json -Compress) -TimeoutSec 5 | Out-Null
        throw 'Unconfirmed restore was unexpectedly accepted.'
    } catch { if($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409){ throw } }
    Write-Host '[PASS] Runtime Smoke :: Restore route rejects requests without explicit confirmation' -ForegroundColor Green

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $validModRoot=Join-Path $temp 'valid-mod';New-Item $validModRoot -ItemType Directory -Force|Out-Null
    Set-Content (Join-Path $validModRoot 'Example.pak') 'pak-content'
    $validModZip=Join-Path $temp 'Example.zip';[IO.Compression.ZipFile]::CreateFromDirectory($validModRoot,$validModZip)
    $installedMod=Invoke-RestMethod "$base/api/v1/mods/PAK/RuntimeSmoke/install-zip" -Method Post -ContentType 'application/zip' -InFile $validModZip -TimeoutSec 10
    if(-not $installedMod.success){ throw 'Validated MOD ZIP install did not succeed.' }
    $modInventory=Invoke-RestMethod "$base/api/v1/mods" -TimeoutSec 5
    if(@($modInventory.mods|Where-Object package -eq 'RuntimeSmoke').Count -ne 1){ throw 'Installed MOD did not appear in server-side inventory.' }
    $bulk=Invoke-RestMethod "$base/api/v1/mods/all/enabled?enabled=false" -Method Post -TimeoutSec 5
    if(-not $bulk.success){ throw 'Bulk MOD disable route failed.' }
    $deletedMod=Invoke-RestMethod "$base/api/v1/mods/PAK/RuntimeSmoke" -Method Delete -TimeoutSec 5
    if(-not $deletedMod.success){ throw 'Selected MOD delete route failed.' }
    Write-Host '[PASS] Runtime Smoke :: Validated MOD install, inventory, bulk state, and delete routes execute server-side' -ForegroundColor Green

    $badZip=Join-Path $temp 'Traversal.zip';$badStream=[IO.File]::Open($badZip,[IO.FileMode]::CreateNew);try{$badArchive=[IO.Compression.ZipArchive]::new($badStream,[IO.Compression.ZipArchiveMode]::Create,$true);try{$entry=$badArchive.CreateEntry('../escape.pak');$writer=[IO.StreamWriter]::new($entry.Open());try{$writer.Write('blocked')}finally{$writer.Dispose()}}finally{$badArchive.Dispose()}}finally{$badStream.Dispose()}
    try { Invoke-RestMethod "$base/api/v1/mods/PAK/Traversal/install-zip" -Method Post -ContentType 'application/zip' -InFile $badZip -TimeoutSec 10|Out-Null;throw 'Traversal archive was unexpectedly accepted.' }
    catch { if($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409){throw} }
    if(Test-Path (Join-Path $runtime 'mod-staging\escape.pak')){throw 'Traversal archive escaped staging.'}
    Write-Host '[PASS] Runtime Smoke :: MOD ZIP traversal payload is rejected before installation' -ForegroundColor Green

    $palConfig=Invoke-RestMethod "$base/api/v1/palworld/config" -TimeoutSec 5
    if($null -eq $palConfig.configurationPath -or $null -eq $palConfig.settings){ throw 'Palworld configuration endpoint did not return a safe snapshot.' }
    Write-Host '[PASS] Runtime Smoke :: Palworld configuration endpoint returns a safe snapshot' -ForegroundColor Green

    $rconStatus=Invoke-RestMethod "$base/api/v1/rcon/status" -TimeoutSec 5
    if($null -eq $rconStatus.enabled -or $null -eq $rconStatus.port -or $null -eq $rconStatus.passwordConfigured){ throw 'RCON status endpoint did not return capability metadata.' }
    if($rconStatus.PSObject.Properties.Name -contains 'adminPassword' -or $rconStatus.PSObject.Properties.Name -contains 'password'){ throw 'RCON status endpoint exposed a credential field.' }
    Write-Host '[PASS] Runtime Smoke :: RCON status endpoint reports capability without exposing credentials' -ForegroundColor Green

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

    $playerId='runtime-smoke-user'
    $noteBody='private-note-body-must-not-enter-audit'
    $warningBody='private-warning-body-must-not-enter-audit'
    $initialMetadata=Invoke-RestMethod "$base/api/v1/players/$playerId/metadata" -TimeoutSec 5
    if($initialMetadata.playerId -ne $playerId -or $null -eq $initialMetadata.warnings){ throw 'Player metadata endpoint did not return the expected empty/default record.' }
    $savedMetadata=Invoke-RestMethod "$base/api/v1/players/$playerId/metadata" -Method Put -ContentType 'application/json' -Body (@{notes=$noteBody}|ConvertTo-Json -Compress) -TimeoutSec 5
    if($savedMetadata.notes -ne $noteBody){ throw 'Player notes did not persist through the headless route.' }
    $warnedMetadata=Invoke-RestMethod "$base/api/v1/players/$playerId/warnings" -Method Post -ContentType 'application/json' -Body (@{message=$warningBody}|ConvertTo-Json -Compress) -TimeoutSec 5
    if(@($warnedMetadata.warnings).Count -ne 1 -or $warnedMetadata.warnings[0].message -ne $warningBody){ throw 'Player warning did not persist through the headless route.' }
    $reloadedMetadata=Invoke-RestMethod "$base/api/v1/players/$playerId/metadata" -TimeoutSec 5
    if($reloadedMetadata.notes -ne $noteBody -or @($reloadedMetadata.warnings).Count -ne 1){ throw 'Player metadata did not survive a new request.' }
    Write-Host '[PASS] Runtime Smoke :: Player notes and warnings persist through the headless metadata service' -ForegroundColor Green

    $activity = Invoke-RestMethod "$base/api/v1/activity/tail?lines=40" -TimeoutSec 5
    $activityText=$activity.lines -join "`n"
    if($null -eq $activity.lines -or -not $activityText.Contains('Player promote unavailable') -or -not $activityText.Contains('Player notes updated') -or -not $activityText.Contains('Player warning added')){
        throw 'Persistent activity/audit log did not capture all player management events.'
    }
    if($activityText.Contains($noteBody) -or $activityText.Contains($warningBody)){ throw 'Player metadata audit leaked private note or warning text.' }
    Write-Host '[PASS] Runtime Smoke :: Persistent activity/audit log captures player mutations without private bodies' -ForegroundColor Green

    $guildBase=Invoke-RestMethod "$base/api/v1/world/players-guilds" -TimeoutSec 5
    if($null -eq $guildBase.players -or $null -eq $guildBase.guilds -or $null -eq $guildBase.warnings -or [string]::IsNullOrWhiteSpace([string]$guildBase.semanticSource)){
        throw 'Guild/base explorer endpoint did not return its complete read-only evidence envelope.'
    }
    Write-Host '[PASS] Runtime Smoke :: Guild/base explorer route returns a remote-safe read-only evidence envelope' -ForegroundColor Green

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
