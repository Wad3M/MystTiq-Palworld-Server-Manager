# MystTiq v0.9.8.0: file reviewed for this release (2026-09-29).
#requires -Version 7.0
[CmdletBinding()]
param(
    # The clone server only (user, 2026-09-27): never the main server.
    [string]$ServerId = 'second-local',
    [string]$BaseUrl = 'http://127.0.0.1:8213',
    # Needed only when the local API has authentication switched on.
    [string]$Token,
    [string]$TokenFile,
    # How long to wait for someone to join the clone server.
    [int]$WaitMinutes = 15,
    [string]$ItemId = 'Wood',
    [string]$ReportPath
)

# v0.8.26.0: the in-game checks a person used to do by hand, run against a real player on the clone server. You join the
# clone in Palworld; the script does the rest through the MystTiq API, the same calls the Desktop makes:
#   1. waits until a player is online and reads their live map position (the Players list and the live map use it);
#   2. gives them one item (Give Item), to show PalDefender-over-RCON delivery works;
#   3. asks PalDefender for their position (Teleport > Capture), recording the PalDefender/REST pair for the map;
#   4. adds a temporary teleport point at that position, sends the player to it, then puts the teleport settings back
#      exactly as they were, whatever happened;
#   5. writes a report (Markdown) with every request and answer.
# Nothing here touches the main server: every call is scoped to -ServerId.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$api = "$BaseUrl/api/v1/servers/$ServerId"
if (-not $Token -and $TokenFile -and (Test-Path -LiteralPath $TokenFile)) { $Token = (Get-Content -LiteralPath $TokenFile -Raw).Trim() }
$headers = @{}
if ($Token) { $headers['Authorization'] = "Bearer $Token" }
if (-not $ReportPath) { $ReportPath = Join-Path ([IO.Path]::GetTempPath()) "mysttiq-v0.8.26.0-ingame-$(Get-Date -Format 'yyyyMMdd-HHmmss').md" }

$results = [System.Collections.Generic.List[object]]::new()
$log = [System.Collections.Generic.List[string]]::new()
function Add-Result([string]$Name, [bool]$Ok, [string]$Detail) {
    $results.Add([pscustomobject]@{ Name = $Name; Ok = $Ok; Detail = $Detail })
    Write-Host ("[{0}] {1}{2}" -f ($(if ($Ok) { 'PASS' } else { 'FAIL' }), $Name, $(if ($Detail) { " -- $Detail" } else { '' }))) -ForegroundColor $(if ($Ok) { 'Green' } else { 'Red' })
}
function Invoke-Api([string]$Method, [string]$Path, $Body) {
    $uri = "$api$Path"
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 8 -Compress } else { $null }
    try {
        $response = if ($null -ne $json) {
            Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -Body $json -ContentType 'application/json' -SkipHttpErrorCheck -StatusCodeVariable status
        } else {
            Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -SkipHttpErrorCheck -StatusCodeVariable status
        }
    } catch {
        $log.Add("$Method $Path -> error: $($_.Exception.Message)")
        return [pscustomobject]@{ Status = 0; Body = $null; Error = $_.Exception.Message }
    }
    $log.Add("$Method $Path $(if ($json) { $json } else { '' }) -> $status $($response | ConvertTo-Json -Depth 6 -Compress)")
    return [pscustomobject]@{ Status = [int]$status; Body = $response; Error = $null }
}

Write-Host "==> MystTiq v0.8.26.0 in-game checks on '$ServerId' ($BaseUrl)" -ForegroundColor Cyan

# 0. The clone server is known, running and answering.
$servers = try { Invoke-RestMethod -Uri "$BaseUrl/api/v1/servers" -Headers $headers } catch { $null }
$serverList = if ($servers -and $servers.PSObject.Properties['servers']) { @($servers.servers) } else { @($servers) }
$clone = $serverList | Where-Object { $_ -and $_.PSObject.Properties['id'] -and $_.id -eq $ServerId } | Select-Object -First 1
Add-Result "Server '$ServerId' is in the fleet" ($null -ne $clone) $(if ($clone) { "$($clone.id)" } else { "no server '$ServerId' at $BaseUrl/api/v1/servers (is MystTiq running?)" })
if (-not $clone) { throw "Server '$ServerId' was not found; nothing was changed." }

# 1. Wait for a player.
Write-Host "Join the clone server in Palworld now. Waiting up to $WaitMinutes minute(s) for a player..." -ForegroundColor Yellow
$deadline = (Get-Date).AddMinutes($WaitMinutes)
$player = $null
while (-not $player -and (Get-Date) -lt $deadline) {
    $players = Invoke-Api GET '/players' $null
    if ($players.Status -eq 200 -and $players.Body -and @($players.Body.players).Count -gt 0) { $player = @($players.Body.players)[0]; break }
    Start-Sleep -Seconds 10
}
Add-Result 'A player is online' ($null -ne $player) $(if ($player) { "$($player.name) (playerId $($player.playerId), level $($player.level))" } else { "nobody joined within $WaitMinutes minute(s)" })
if (-not $player) { throw 'Nobody joined, so nothing was changed.' }
$playerId = [string]$player.playerId

# The REST API reports the position as text; empty means the server did not give one.
$x = 0.0; $y = 0.0
$hasPosition = [double]::TryParse([string]$player.locationX, [ref]$x) -and [double]::TryParse([string]$player.locationY, [ref]$y) -and -not ($x -eq 0 -and $y -eq 0)
Add-Result 'The player has a live map position' $hasPosition "locationX $($player.locationX), locationY $($player.locationY)"

# 2. Give one item. Whether the id is in the Give Item picker's catalogue is noted, not required: the catalogue lists
# only what this world's save, kits and earlier gives have seen (plus the game's name tables when set up).
$catalog = Invoke-Api GET '/players/give/catalog' $null
$catalogText = if ($catalog.Body) { $catalog.Body | ConvertTo-Json -Depth 6 -Compress } else { '' }
$catalogNote = if ($catalog.Status -ne 200) { "the catalogue answered $($catalog.Status)" } elseif ($catalogText -match "`"$([regex]::Escape($ItemId))`"") { "'$ItemId' is in the Give Item catalogue" } else { "'$ItemId' is not in this server's Give Item catalogue yet" }
Write-Host "Note: $catalogNote"
$give = Invoke-Api POST "/players/$playerId/give" @{ entries = @(@{ Type = 'Item'; Id = $ItemId; Amount = 1 }) }
Add-Result "Give Item: 1 x $ItemId" ($give.Status -eq 200 -and $give.Body.success) $(if ($give.Body) { [string]$give.Body.message } else { $give.Error })

# 3. Capture the position through PalDefender.
$capture = Invoke-Api POST '/teleport/capture' @{ playerId = $playerId }
$captured = $capture.Status -eq 200 -and $capture.Body.success
Add-Result 'Teleport capture (PalDefender position)' $captured $(if ($capture.Body) { "x $($capture.Body.x), y $($capture.Body.y), z $($capture.Body.z): $($capture.Body.message)" } else { $capture.Error })

# 4. Temporary point at the captured position; send the player there; always restore the teleport settings.
$before = Invoke-Api GET '/teleport' $null
if ($captured -and $before.Status -eq 200) {
    $original = $before.Body.config
    $pointName = 'mysttiq-test-0826'
    try {
        $points = @(@($original.points) | Where-Object { $_ -and $_.name -ne $pointName }) + @([pscustomobject]@{ name = $pointName; x = $capture.Body.x; y = $capture.Body.y; z = $capture.Body.z })
        $temp = [pscustomobject]@{ enabled = $original.enabled; commandPrefix = $original.commandPrefix; cooldownSeconds = $original.cooldownSeconds; points = $points }
        $save = Invoke-Api PUT '/teleport' $temp
        Add-Result 'Temporary teleport point saved' ($save.Status -eq 200) $(if ($save.Body -and $save.Body.PSObject.Properties['message']) { [string]$save.Body.message } else { '' })
        $send = Invoke-Api POST "/teleport/points/$pointName/send" @{ playerId = $playerId }
        Add-Result 'Teleport the player to the point' ($send.Status -eq 200 -and $send.Body.success) $(if ($send.Body) { "$($send.Body.command): $($send.Body.message)" } else { $send.Error })
    } finally {
        $restore = Invoke-Api PUT '/teleport' $original
        $after = Invoke-Api GET '/teleport' $null
        $same = ($after.Body.config | ConvertTo-Json -Depth 6 -Compress) -eq ($original | ConvertTo-Json -Depth 6 -Compress)
        Add-Result 'Teleport settings restored exactly' ($restore.Status -eq 200 -and $same) ''
    }
} else {
    Add-Result 'Teleport send (skipped)' $false 'skipped: the capture failed or the teleport settings could not be read'
}

# 5. Report.
$passed = @($results | Where-Object Ok).Count
$lines = @("# MystTiq v0.8.26.0 in-game checks", '', "Server: ``$ServerId`` at $BaseUrl  ", "Run: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  ",
    "Player: $(if ($player) { "$($player.name) (``$playerId``)" } else { 'none' })  ", "Catalogue: $catalogNote", '',
    "## Results ($passed/$($results.Count))", '', '| Check | Result | Detail |', '|---|---|---|')
$lines += $results | ForEach-Object { "| $($_.Name) | $(if ($_.Ok) { 'PASS' } else { 'FAIL' }) | $(($_.Detail -replace '\|', '/') -replace "`r?`n", ' ') |" }
if ($captured -and $hasPosition) {
    $lines += '', '## Map calibration pair', '', 'The same moment seen two ways (the live map converts the REST position to map space):', '',
        "- REST ``/players``: locationX $($player.locationX), locationY $($player.locationY)",
        "- PalDefender capture: x $($capture.Body.x), y $($capture.Body.y), z $($capture.Body.z)"
}
$lines += '', '## Requests', '', '```', ($log | ForEach-Object { $_ }), '```'
$lines | Set-Content -LiteralPath $ReportPath -Encoding utf8
Write-Host "Report: $ReportPath"
Write-Host "Passed: $passed/$($results.Count)" -ForegroundColor $(if ($passed -eq $results.Count) { 'Green' } else { 'Red' })
if ($passed -ne $results.Count) { exit 1 }
