[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18217
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar concurrency check requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.7.0: live proof that HeadlessBackupService.RestoreAsync now actually rejects a concurrent
# lifecycle operation via IOperationCoordinator, not just that the code compiles/matches a regex.
# Fires POST /server/start and POST /backups/{file}/restore at nearly the same instant using an
# in-process HttpClient + Task.WhenAll (not separate job processes, which have enough scheduling
# overhead to make the race non-tight) and asserts exactly one of the two is rejected as a
# lock conflict -- proving the "world-mutation" resource key is now mutually exclusive between them.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.7.0-concurrency-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$missingServer = Join-Path $temp 'missing-server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $missingServer -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$saveRoot = Join-Path $missingServer 'Pal\Saved\SaveGames\0\ConcurrencyCheckWorld'
New-Item $saveRoot -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $saveRoot 'Level.sav') 'concurrency-check-save'

$log = Join-Path $temp 'headless.log'
$err = Join-Path $temp 'headless.err.log'
$proc = $null
try {
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $missingServer, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err
    $base = "http://127.0.0.1:$Port"
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Sidecar did not become healthy. stdout=$(Get-Content $log -Raw -ErrorAction SilentlyContinue) stderr=$(Get-Content $err -Raw -ErrorAction SilentlyContinue)" }

    $createdBackup = Invoke-RestMethod "$base/api/v1/backups/create" -Method Post -TimeoutSec 10
    if (-not $createdBackup.success) { throw "Could not create a real backup to race a restore against: $($createdBackup.message)" }
    $fileName = $createdBackup.fileName

    Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue
    $client = [System.Net.Http.HttpClient]::new()
    $client.BaseAddress = [Uri]$base

    $startTask = $client.PostAsync('/api/v1/server/start', [System.Net.Http.StringContent]::new(''))
    $restoreBody = [System.Net.Http.StringContent]::new((@{ confirmed = $true } | ConvertTo-Json -Compress), [System.Text.Encoding]::UTF8, 'application/json')
    $restoreTask = $client.PostAsync("/api/v1/backups/$fileName/restore", $restoreBody)
    [System.Threading.Tasks.Task]::WhenAll([System.Threading.Tasks.Task[]]@($startTask, $restoreTask)).GetAwaiter().GetResult()

    $startResponse = $startTask.Result
    $restoreResponse = $restoreTask.Result
    $startBody = $startResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $restoreBodyText = $restoreResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    $startConflict = [int]$startResponse.StatusCode -eq 409 -and $startBody -match 'lifecycle-operation-in-progress'
    $restoreConflict = [int]$restoreResponse.StatusCode -eq 409 -and $restoreBodyText -match 'world-mutating operation is in progress'

    Write-Host "server/start  -> $([int]$startResponse.StatusCode) $startBody"
    Write-Host "backup/restore -> $([int]$restoreResponse.StatusCode) $restoreBodyText"

    if ($startConflict -and $restoreConflict) {
        throw "Both requests were rejected as lock conflicts -- the lock should only block whichever request lost the race, not both (or the profile-scoping is wrong)."
    }
    if (-not $startConflict -and -not $restoreConflict) {
        throw "Neither request was rejected as a lock conflict -- HeadlessBackupService.RestoreAsync is not actually enforcing mutual exclusion against a concurrent lifecycle operation."
    }

    $winner = if ($restoreConflict) { 'server/start' } else { 'backup/restore' }
    Write-Host "[PASS] Concurrency Check :: exactly one of two concurrent world-mutation operations was rejected as a lock conflict (winner: $winner) -- HeadlessBackupService.RestoreAsync's new coordinator lock genuinely enforces mutual exclusion" -ForegroundColor Green
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}
