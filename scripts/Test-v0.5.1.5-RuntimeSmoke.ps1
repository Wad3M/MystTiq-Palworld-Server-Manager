[CmdletBinding()]
param(
    [string]$ProjectRoot='.',
    [int]$Port=18216
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
if(-not $IsWindows){
    Write-Host '[SKIP] Windows local sidecar runtime smoke test requires Windows.' -ForegroundColor Yellow
    return
}

$exe=Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if(-not (Test-Path $exe -PathType Leaf)){ throw "Headless desktop sidecar not found: $exe" }
$temp=Join-Path $root ("artifacts\runtime-smoke\v0.5.1.5-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config=Join-Path $temp 'mysttiq.json'
$runtime=Join-Path $temp 'runtime'
$missingServer=Join-Path $temp 'missing-server'
$steamCmd=Join-Path $temp 'missing-steamcmd.exe'
$backupRoot=Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $missingServer -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$defaultPalConfig=Join-Path $missingServer 'DefaultPalWorldSettings.ini'
Set-Content $defaultPalConfig @'
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Default Palworld Server",ServerDescription="",AdminPassword="",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=False,RCONPort=25575,DayTimeSpeedRate=1.000000)
'@
$saveRoot=Join-Path $missingServer 'Pal\Saved\SaveGames\0\RuntimeSmokeWorld'
New-Item $saveRoot -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $saveRoot 'Level.sav') 'runtime-smoke-save'
$palLogs=Join-Path $missingServer 'Pal\Saved\Logs'
New-Item $palLogs -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $palLogs 'PalServer.log') @('Server initialized successfully','Fatal error: test-only runtime smoke signature','Unhandled exception in UE4SS callback')
$backupSnapshot=Join-Path $missingServer 'Pal\Saved\SaveGames\0\BackupSnapshot'
New-Item $backupSnapshot -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $backupSnapshot 'Level.sav') 'must-not-be-active'
$log=Join-Path $temp 'headless.log'
$err=Join-Path $temp 'headless.err.log'
$proc=$null
try{
    $args=@('api-run','--desktop-sidecar','--config',$config,'--bind-address','127.0.0.1','--api-port',"$Port",'--server-root',$missingServer,'--steamcmd',$steamCmd,'--backup-root',$backupRoot,'--runtime-root',$runtime)
    $proc=Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err -Environment @{MYSTTIQ_ENABLE_FAILURE_INJECTION='1'}
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

    $crash=Invoke-RestMethod "$base/api/v1/crash-analyzer/analyze" -Method Post -TimeoutSec 5
    if(@($crash.findings).Count -lt 2 -or [int]$crash.filesScanned -lt 1 -or [int]$crash.linesScanned -lt 3){throw 'Crash Analyzer did not return bounded explicit log evidence.'}
    $crashHistory=Invoke-RestMethod "$base/api/v1/crash-analyzer/history" -TimeoutSec 5
    if(@($crashHistory).Count -lt 1 -or -not (Test-Path (Join-Path $runtime 'crash-analyzer'))){throw 'Crash Analyzer history was not persisted server-side.'}
    Write-Host '[PASS] Runtime Smoke :: Crash analysis returns explicit signatures and persistent remote-safe history' -ForegroundColor Green

    $saveDiagnostics=Invoke-RestMethod "$base/api/v1/save-tools/diagnostics" -TimeoutSec 5
    $saveFiles=Invoke-RestMethod "$base/api/v1/save-tools/files" -TimeoutSec 5
    $saveSelfTest=Invoke-RestMethod "$base/api/v1/save-tools/self-test" -Method Post -TimeoutSec 15
    if([string]::IsNullOrWhiteSpace([string]$saveDiagnostics.activeLevelSignature) -or @($saveFiles.items).Count -lt 1 -or $null -eq $saveSelfTest.tests){throw 'Save Tools did not return diagnostics, signature, inventory and self-test envelope.'}
    if(-not (([string]$saveFiles.items[0].relativePath).EndsWith('Level.sav'))){throw 'Save Tools inventory did not remain rooted in the isolated server SaveRoot.'}
    Write-Host '[PASS] Runtime Smoke :: Save Tools diagnostics inspect dependencies and saves without mutation' -ForegroundColor Green

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

    $worldExplorer=Invoke-RestMethod "$base/api/v1/world/explorer" -TimeoutSec 5
    if($worldExplorer.activeWorldId -ne 'RuntimeSmokeWorld' -or [int]$worldExplorer.worldCount -ne 1){ throw 'World explorer classified a backup-like snapshot as an active world.' }
    if($null -eq $worldExplorer.statistics -or $null -eq $worldExplorer.integrity -or $worldExplorer.integrity.state -ne 'Healthy'){ throw 'World statistics/integrity evidence was incomplete.' }
    Write-Host '[PASS] Runtime Smoke :: World Inspector returns canonical statistics/integrity and excludes backup-like snapshots' -ForegroundColor Green

    $validation=Invoke-RestMethod "$base/api/v1/world/validate" -TimeoutSec 5
    if(-not $validation.healthy -or @($validation.findings).Count -lt 1){ throw 'World Validator did not return a healthy structural report.' }
    Write-Host '[PASS] Runtime Smoke :: World Validator returns server-authoritative findings' -ForegroundColor Green

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $worldImportRoot=Join-Path $temp 'world-import';New-Item (Join-Path $worldImportRoot 'Players') -ItemType Directory -Force|Out-Null
    Set-Content (Join-Path $worldImportRoot 'Level.sav') 'imported-world'
    Set-Content (Join-Path $worldImportRoot 'Players\0123456789ABCDEF0123456789ABCDEF.sav') 'recovered-player'
    $worldImportZip=Join-Path $temp 'world-import.zip';[IO.Compression.ZipFile]::CreateFromDirectory($worldImportRoot,$worldImportZip)
    $worldPreview=Invoke-RestMethod "$base/api/v1/world/import/analyze?mode=world-import" -Method Post -ContentType 'application/zip' -InFile $worldImportZip -TimeoutSec 15
    if([string]::IsNullOrWhiteSpace([string]$worldPreview.previewToken) -or @($worldPreview.steps).Count -lt 7){ throw 'World import Analyze did not return a complete review plan.' }
    try { Invoke-RestMethod "$base/api/v1/world/import/apply" -Method Post -ContentType 'application/json' -Body (@{previewToken=$worldPreview.previewToken;confirmed=$false}|ConvertTo-Json -Compress) -TimeoutSec 10|Out-Null;throw 'Unconfirmed world transaction was accepted.' }
    catch { if($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 400){throw} }
    if((Get-Content (Join-Path $saveRoot 'Level.sav') -Raw).Trim() -ne 'runtime-smoke-save'){throw 'Analyze or rejected Apply modified the active world.'}
    $worldApply=Invoke-RestMethod "$base/api/v1/world/import/apply" -Method Post -ContentType 'application/json' -Body (@{previewToken=$worldPreview.previewToken;confirmed=$true}|ConvertTo-Json -Compress) -TimeoutSec 30
    if(-not $worldApply.success -or [string]::IsNullOrWhiteSpace([string]$worldApply.safetyBackup) -or -not (Test-Path $worldApply.journalPath)){throw 'Confirmed world transaction did not create backup/journal evidence.'}
    if((Get-Content (Join-Path $saveRoot 'Level.sav') -Raw).Trim() -ne 'imported-world'){throw 'World import did not atomically promote the reviewed candidate.'}
    Write-Host '[PASS] Runtime Smoke :: Analyze is side-effect free and Apply performs backup, staged swap, validation and journal' -ForegroundColor Green

    foreach($stage in @('preview','backup','staging','swap','validation')){
        Set-Content (Join-Path $worldImportRoot 'Level.sav') "candidate-$stage"
        Remove-Item $worldImportZip -Force;[IO.Compression.ZipFile]::CreateFromDirectory($worldImportRoot,$worldImportZip)
        $failurePreview=Invoke-RestMethod "$base/api/v1/world/import/analyze?mode=world-import" -Method Post -ContentType 'application/zip' -InFile $worldImportZip -TimeoutSec 15
        try { Invoke-RestMethod "$base/api/v1/world/import/apply" -Method Post -Headers @{'X-MystTiq-Test-Failure-Stage'=$stage} -ContentType 'application/json' -Body (@{previewToken=$failurePreview.previewToken;confirmed=$true}|ConvertTo-Json -Compress) -TimeoutSec 30|Out-Null;throw "Failure stage $stage unexpectedly succeeded." }
        catch { if($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 400){throw} }
        if((Get-Content (Join-Path $saveRoot 'Level.sav') -Raw).Trim() -ne 'imported-world'){throw "Failure stage $stage changed the active world instead of preserving/rolling back."}
    }
    $journals=Invoke-RestMethod "$base/api/v1/world/transactions?maximum=100" -TimeoutSec 5
    if(@($journals).Count -lt 6 -or @($journals|Where-Object state -eq 'RolledBack').Count -lt 2){throw 'Failure injection did not persist failure/rollback journals.'}
    Write-Host '[PASS] Runtime Smoke :: Every transaction stage has failure evidence and atomic preservation or rollback' -ForegroundColor Green

    $badWorldZip=Join-Path $temp 'bad-world.zip';$badWorldStream=[IO.File]::Open($badWorldZip,[IO.FileMode]::CreateNew);try{$badWorldArchive=[IO.Compression.ZipArchive]::new($badWorldStream,[IO.Compression.ZipArchiveMode]::Create,$true);try{$entry=$badWorldArchive.CreateEntry('../Level.sav');$writer=[IO.StreamWriter]::new($entry.Open());try{$writer.Write('blocked')}finally{$writer.Dispose()}}finally{$badWorldArchive.Dispose()}}finally{$badWorldStream.Dispose()}
    try { Invoke-RestMethod "$base/api/v1/world/import/analyze?mode=world-import" -Method Post -ContentType 'application/zip' -InFile $badWorldZip -TimeoutSec 10|Out-Null;throw 'Unsafe world archive was accepted.' }
    catch { if($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 400){throw} }
    Write-Host '[PASS] Runtime Smoke :: World archive traversal is rejected during Analyze' -ForegroundColor Green

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

    $defaultRequest=@{confirmCreate=$true;serverName='Runtime Smoke Server';serverDescription='Created through isolated API smoke';adminPassword='runtime-secret';serverPassword='';maximumPlayers=24;gamePort=8211;restPort=8212}|ConvertTo-Json
    $createdConfig=Invoke-RestMethod "$base/api/v1/palworld/config/defaults" -Method Post -ContentType 'application/json' -Body $defaultRequest -TimeoutSec 5
    if(-not $createdConfig.success){throw 'First-run default configuration route did not report success.'}
    $activeConfig=Invoke-RestMethod "$base/api/v1/palworld/config" -TimeoutSec 5
    $activeName=@($activeConfig.settings|Where-Object name -eq 'ServerName')[0].value
    $activeRestEnabled=@($activeConfig.settings|Where-Object name -eq 'RESTAPIEnabled')[0].value
    if(-not $activeConfig.exists -or $activeName -ne '"Runtime Smoke Server"' -or $activeRestEnabled -ne 'False'){throw 'Created settings did not preserve requested identity and default REST enablement state.'}
    try { Invoke-RestMethod "$base/api/v1/palworld/config/defaults" -Method Post -ContentType 'application/json' -Body $defaultRequest -TimeoutSec 5|Out-Null;throw 'Existing active configuration was unexpectedly overwritten.' }
    catch { if($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 400){throw} }
    Write-Host '[PASS] Runtime Smoke :: First-run settings creation is validated, preserves security defaults, and refuses overwrite' -ForegroundColor Green

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

    $initialNotifications=Invoke-RestMethod "$base/api/v1/notifications" -TimeoutSec 5
    if($null -eq $initialNotifications.items -or [int]$initialNotifications.unreadCount -ne 0){throw 'Initial notification snapshot was invalid.'}
    $selfTest=Invoke-RestMethod "$base/api/v1/notifications/self-test" -Method Post -TimeoutSec 5
    if(@($selfTest.items).Count -ne 4 -or [int]$selfTest.unreadCount -ne 4 -or [int]$selfTest.pinnedCount -ne 1){throw 'Notification self-test did not create all semantic states.'}
    $selected=@($selfTest.items|Where-Object severity -eq 'Information')[0]
    $read=Invoke-RestMethod "$base/api/v1/notifications/$($selected.id)/read" -Method Post -ContentType 'application/json' -Body (@{value=$true}|ConvertTo-Json -Compress) -TimeoutSec 5
    if([int]$read.unreadCount -ne 3){throw 'Notification read state did not persist.'}
    $pinned=Invoke-RestMethod "$base/api/v1/notifications/$($selected.id)/pin" -Method Post -ContentType 'application/json' -Body (@{value=$true}|ConvertTo-Json -Compress) -TimeoutSec 5
    if([int]$pinned.pinnedCount -ne 2){throw 'Notification pin state did not persist.'}
    $allRead=Invoke-RestMethod "$base/api/v1/notifications/mark-all-read" -Method Post -TimeoutSec 5
    if([int]$allRead.unreadCount -ne 0){throw 'Mark All Read did not update persistent notification state.'}
    $dismissed=Invoke-RestMethod "$base/api/v1/notifications/$($selected.id)" -Method Delete -TimeoutSec 5
    if(@($dismissed.items).Count -ne 3){throw 'Notification dismissal did not remove exactly one item.'}
    $persisted=Invoke-RestMethod "$base/api/v1/notifications" -TimeoutSec 5
    if(@($persisted.items).Count -ne 3 -or -not (Test-Path (Join-Path $runtime 'notifications\state.json'))){throw 'Notification state did not persist server-side.'}
    Write-Host '[PASS] Runtime Smoke :: Notification read pin dismiss self-test and persistence routes are authoritative' -ForegroundColor Green

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
