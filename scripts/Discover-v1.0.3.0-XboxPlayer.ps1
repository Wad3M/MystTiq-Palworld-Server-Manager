# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    # The MystTiq service running the clone (the desktop's helper or a service), and the clone's server id there.
    [string]$Api = 'http://127.0.0.1:8213',
    [string]$ServerId = 'second-local',
    [string]$Token = '',
    [int]$WaitMinutes = 20,
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
# v1.0.3.0 (roadmap X-1, Xbox player discovery). Read-only. While the owner's Xbox account is in the clone's world, records
# what MystTiq can see of that player before anything is built for Xbox players:
#   - the REST API's /players entry (every field, IP address removed),
#   - after the world has been saved, the explorer's record of the same player from the save (identity, name, level,
#     guild, Pals) and the player registry entry.
# Nothing is changed on the server. Writes a JSON capture for docs/architecture/v1.0.3.0-xbox-player-discovery.md.
$headers = @{}; if ($Token) { $headers.Authorization = "Bearer $Token" }
$base = "$Api/api/v1/servers/$ServerId"
if (-not $Out) { $Out = Join-Path (Get-Location) ("xbox-discovery-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json') }
Write-Host "Waiting up to $WaitMinutes minutes for a player without a Steam ID on $ServerId…"
$deadline = (Get-Date).AddMinutes($WaitMinutes)
$found = @()
while ((Get-Date) -lt $deadline -and $found.Count -eq 0) {
    $players = Invoke-RestMethod "$base/players" -Headers $headers -TimeoutSec 20
    $found = @($players.players | Where-Object { -not $_.steamId })
    if ($found.Count -eq 0) { Start-Sleep 15 }
}
if ($found.Count -eq 0) { throw 'No player without a Steam ID joined in time.' }
$rest = @($found | ForEach-Object { $copy = $_ | Select-Object * -ExcludeProperty ip; $copy })
Write-Host "Found: $(@($rest | ForEach-Object { "$($_.name) (userId $($_.userId), playerId $($_.playerId))" }) -join '; ')"
Write-Host 'Save the world (in game or MystTiq''s Save Now), then press Enter to read the save.'
[void](Read-Host)
$explorer = Invoke-RestMethod "$base/world/players-guilds" -Headers $headers -TimeoutSec 180
$registry = Invoke-RestMethod "$base/players/registry" -Headers $headers -TimeoutSec 20
$ids = @($rest | ForEach-Object { $_.playerId; $_.userId } | Where-Object { $_ } | ForEach-Object { "$_".ToUpperInvariant() })
function Mentions($item) { $text = ($item | ConvertTo-Json -Depth 6 -Compress).ToUpperInvariant(); [bool]($ids | Where-Object { $text.Contains($_) }) }
$capture = [ordered]@{
    capturedUtc = (Get-Date).ToUniversalTime().ToString('o')
    server = $ServerId
    rest = $rest
    save = @(@($explorer.players) | Where-Object { Mentions $_ })
    registry = @(@($registry) + @($registry.records) + @($registry.players) | Where-Object { $_ -and (Mentions $_) })
}
$capture | ConvertTo-Json -Depth 8 | Set-Content $Out -Encoding utf8
Write-Host "Capture written: $Out" -ForegroundColor Green
