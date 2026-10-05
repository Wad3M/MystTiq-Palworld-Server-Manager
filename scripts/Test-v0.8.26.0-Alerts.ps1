# MystTiq v1.0.0.4: file reviewed for this release (2026-10-05).
#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://127.0.0.1:8213',
    # Needed only when the local API has authentication switched on.
    [string]$Token,
    [string]$TokenFile,
    # Limit the run to some servers; by default every server with an outside channel switched on.
    [string[]]$ServerId,
    # How long to give the background sends before reading the Activity log.
    [int]$SettleSeconds = 20,
    [string]$ReportPath
)

# v0.8.26.0: checks that alerts really leave MystTiq. For each server with an outside channel (Discord, email, webhook)
# switched on, it sends one test notification through the normal path (Notifications > Send test), waits for the
# background sends, and reads the Activity log for a failed send. THIS SENDS REAL MESSAGES to the configured channels
# (approved by the user, 2026-09-27). Servers with no outside channel, or with delivery paused, are reported and left
# alone. Nothing is changed: no channel, template or pause setting is written.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Token -and $TokenFile -and (Test-Path -LiteralPath $TokenFile)) { $Token = (Get-Content -LiteralPath $TokenFile -Raw).Trim() }
$headers = @{}
if ($Token) { $headers['Authorization'] = "Bearer $Token" }
if (-not $ReportPath) { $ReportPath = Join-Path ([IO.Path]::GetTempPath()) "mysttiq-v0.8.26.0-alerts-$(Get-Date -Format 'yyyyMMdd-HHmmss').md" }

$results = [System.Collections.Generic.List[object]]::new()
function Add-Result([string]$Server, [string]$Name, [string]$Outcome, [string]$Detail) {
    $results.Add([pscustomobject]@{ Server = $Server; Name = $Name; Outcome = $Outcome; Detail = $Detail })
    $colour = switch ($Outcome) { 'PASS' { 'Green' } 'SKIP' { 'Yellow' } default { 'Red' } }
    Write-Host "[$Outcome] $Server : $Name$(if ($Detail) { " -- $Detail" })" -ForegroundColor $colour
}
function Get-Api([string]$Uri) { Invoke-RestMethod -Uri $Uri -Headers $headers }

Write-Host "==> MystTiq v0.8.26.0 alert delivery checks ($BaseUrl)" -ForegroundColor Cyan
$servers = @(Get-Api "$BaseUrl/api/v1/servers")
if ($ServerId) { $servers = @($servers | Where-Object { $_.id -in $ServerId }) }
if ($servers.Count -eq 0) { throw 'No server to check (is MystTiq running, and is -ServerId right?).' }

$sent = [System.Collections.Generic.List[object]]::new()
foreach ($server in $servers) {
    $api = "$BaseUrl/api/v1/servers/$($server.id)"
    $delivery = Get-Api "$api/notifications/delivery"
    $channels = @($delivery.externalChannels)
    if ($delivery.pausedUntilUtc) { Add-Result $server.id 'Outside delivery' 'SKIP' "paused until $($delivery.pausedUntilUtc); not sending"; continue }
    if ($channels.Count -eq 0) { Add-Result $server.id 'Outside delivery' 'SKIP' 'no outside channel is switched on'; continue }
    $startedAt = [DateTimeOffset]::UtcNow
    try {
        $test = Invoke-RestMethod -Method Post -Uri "$api/notifications/test" -Headers $headers
        Add-Result $server.id "Test notification sent to $($channels -join ', ')" 'PASS' $test.message
        $sent.Add([pscustomobject]@{ Server = $server; Api = $api; Channels = $channels; StartedAt = $startedAt })
    } catch {
        Add-Result $server.id 'Test notification' 'FAIL' $_.Exception.Message
    }
}

if ($sent.Count -gt 0) {
    Write-Host "Waiting $SettleSeconds s for the background sends..." -ForegroundColor Yellow
    Start-Sleep -Seconds $SettleSeconds
}
foreach ($s in $sent) {
    # Activity lines look like "[2026-09-27T10:00:00.0000000+00:00] [WARNING] [Notifications] Discord dispatch failed ...".
    $tail = Get-Api "$($s.Api)/activity/tail?lines=200"
    $failures = @($tail.lines | Where-Object {
        $m = [regex]::Match($_, '^\[(?<t>[^\]]+)\] \[(?<sev>[A-Z]+)\] \[Notifications\] (?<rest>.*)$')
        $m.Success -and $m.Groups['rest'].Value -match 'failed' -and [DateTimeOffset]::Parse($m.Groups['t'].Value) -ge $s.StartedAt.AddSeconds(-1)
    })
    if ($failures.Count -eq 0) {
        Add-Result $s.Server.id 'No failed send in the Activity log' 'PASS' "channels: $($s.Channels -join ', ')"
    } else {
        foreach ($f in $failures) { Add-Result $s.Server.id 'Failed send in the Activity log' 'FAIL' $f }
    }
}

$failed = @($results | Where-Object Outcome -eq 'FAIL').Count
$passed = @($results | Where-Object Outcome -eq 'PASS').Count
$lines = @('# MystTiq v0.8.26.0 alert delivery checks', '', "API: $BaseUrl  ", "Run: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')", '',
    'Each test notification was really sent. Check that it arrived in every channel listed; this report only proves MystTiq sent it without an error.', '',
    '| Server | Check | Result | Detail |', '|---|---|---|---|')
$lines += $results | ForEach-Object { "| $($_.Server) | $($_.Name) | $($_.Outcome) | $(($_.Detail -replace '\|', '/') -replace "`r?`n", ' ') |" }
$lines | Set-Content -LiteralPath $ReportPath -Encoding utf8
Write-Host "Report: $ReportPath"
Write-Host "Passed: $passed, failed: $failed, skipped: $(@($results | Where-Object Outcome -eq 'SKIP').Count)" -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
if ($failed -gt 0) { exit 1 }
