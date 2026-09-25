[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18303
)
$ErrorActionPreference = 'Stop'
# The logic gate runs every smoke under strict mode; run strict here too so a standalone run reproduces it.
# Helpers return plain items and every call site wraps with @() (never return a comma-wrapped array).
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path
if (-not $IsWindows) {
    Write-Host '[SKIP] Windows local sidecar route smoke test requires Windows.' -ForegroundColor Yellow
    return
}

# v0.7.103.0: live coverage for the Doctor's "Scheduled backups" finding and its one-click fix. Isolated
# sidecar over a fixture with no automation rules. Checks: the finding warns and offers the fix; two
# simultaneous clicks create exactly one rule; the rule is the documented nightly backup; the finding then
# passes and offers nothing; another fix attempt creates nothing; switching the rule off warns again but does
# NOT offer a second rule. Runs against the PUBLISHED headless exe.

$exe = Join-Path $root 'artifacts\publish\desktop-win-x64\headless\mysttiq-server.exe'
if (-not (Test-Path $exe -PathType Leaf)) { throw "Headless desktop sidecar not found: $exe" }
$temp = Join-Path $root ("artifacts\runtime-smoke\v0.7.103.0-backupschedule-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$config = Join-Path $temp 'mysttiq.json'
$runtime = Join-Path $temp 'runtime'
$serverRoot = Join-Path $temp 'server'
$steamCmd = Join-Path $temp 'missing-steamcmd.exe'
$backupRoot = Join-Path $temp 'backups'
New-Item $runtime -ItemType Directory -Force | Out-Null
New-Item $backupRoot -ItemType Directory -Force | Out-Null
$configDir = Join-Path $serverRoot 'Pal\Saved\Config\WindowsServer'
New-Item $configDir -ItemType Directory -Force | Out-Null
Set-Content (Join-Path $configDir 'PalWorldSettings.ini') @'
[/Script/Pal.PalGameWorldSettings]
OptionSettings=(ServerName="Schedule Smoke",ServerDescription="",AdminPassword="a-long-random-admin-secret",ServerPassword="",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=False,RESTAPIPort=8212,RCONEnabled=False,RCONPort=25575,DayTimeSpeedRate=1.000000)
'@
$log = Join-Path $temp 'headless.log'
$err = Join-Path $temp 'headless.err.log'
$proc = $null
$failures = @()

function Test-RouteSmoke([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Route Smoke :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Route Smoke :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
function Get-StateName($value) {
    $names = @('Pass', 'Warning', 'Fail', 'Starting', 'Skipped', 'Unknown')
    if ($value -is [string] -and $value -notmatch '^\d+$') { return $value }
    return $names[[int]$value]
}
function Get-ScheduleFinding([string]$base) {
    $report = Invoke-RestMethod "$base/api/v1/diagnostics/report" -TimeoutSec 60
    @($report.findings | Where-Object { $_.id -eq 'backups-schedule' })[0]
}
# Invoke-RestMethod hands a JSON array back as ONE nested object, so a helper that returned it directly made
# @(Get-Rules).Count always 1 (which would make a count check pass vacuously). Enumerate explicitly.
function Get-Rules([string]$base) {
    $rules = Invoke-RestMethod "$base/api/v1/automation/rules" -TimeoutSec 10
    foreach ($rule in $rules) { $rule }
}
function Get-RuleId($rule) { if ($rule.id -is [string]) { $rule.id } else { $rule.id.value } }

try {
    $args = @('api-run', '--desktop-sidecar', '--config', $config, '--fleet-root', (Join-Path $temp 'fleet'), '--bind-address', '127.0.0.1', '--api-port', "$Port", '--server-root', $serverRoot, '--steamcmd', $steamCmd, '--backup-root', $backupRoot, '--runtime-root', $runtime)
    $proc = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe -Parent) -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err
    $base = "http://127.0.0.1:$Port"
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($proc.HasExited) { break }
        try { if (Invoke-RestMethod "$base/healthz" -TimeoutSec 2) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Sidecar did not become healthy. $(Get-Content $err -Raw -ErrorAction SilentlyContinue)" }

    Test-RouteSmoke 'with no rules the Doctor warns that nothing is scheduled and offers the one-click fix' {
        if (@(Get-Rules $base).Count -ne 0) { throw 'the fixture should start with no automation rules' }
        $f = Get-ScheduleFinding $base
        if ((Get-StateName $f.state) -ne 'Warning') { throw "expected Warning, got $(Get-StateName $f.state): $($f.evidence)" }
        if ($f.actionKind -ne 'create-backup-rule') { throw "expected the create-backup-rule action, got '$($f.actionKind)'" }
        if ($f.evidence -notmatch 'only exists when someone presses Backup') { throw "unexpected evidence: $($f.evidence)" }
    }

    Test-RouteSmoke 'two simultaneous clicks create exactly one rule' {
        Add-Type -AssemblyName System.Net.Http
        $client = [System.Net.Http.HttpClient]::new()
        try {
            $tasks = 1..2 | ForEach-Object { $client.PostAsync("$base/api/v1/diagnostics/backups-schedule/fix", [System.Net.Http.StringContent]::new('')) }
            [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks)
            $codes = @($tasks | ForEach-Object { [int]$_.Result.StatusCode })
            if (@($codes | Where-Object { $_ -eq 200 }).Count -lt 1) { throw "at least one click should succeed, got: $($codes -join ', ')" }
        }
        finally { $client.Dispose() }
        $rules = @(Get-Rules $base)
        if ($rules.Count -ne 1) { throw "expected exactly 1 rule after two simultaneous clicks, found $($rules.Count)" }
    }

    Test-RouteSmoke 'the rule created is the documented nightly backup, enabled' {
        $rule = @(Get-Rules $base)[0]
        if ($rule.name -ne 'Nightly backup (added by Doctor)') { throw "unexpected rule name '$($rule.name)'" }
        if ($rule.enabled -ne $true) { throw 'the rule should be enabled' }
        if ([string]$rule.action.kind -ne 'CreateBackup') { throw "expected a CreateBackup action, got $($rule.action.kind)" }
        if ([string]$rule.trigger.kind -ne 'DailyTime') { throw "expected a DailyTime trigger, got $($rule.trigger.kind)" }
        if ([string]$rule.trigger.timeOfDayUtc -notmatch '^03:00') { throw "expected 03:00 UTC, got $($rule.trigger.timeOfDayUtc)" }
        if ($null -eq $rule.nextDueUtc) { throw 'the rule should have a next due time' }
    }

    Test-RouteSmoke 'afterwards the finding passes and offers no fix, and another attempt creates nothing' {
        $f = Get-ScheduleFinding $base
        if ((Get-StateName $f.state) -ne 'Pass') { throw "expected Pass, got $(Get-StateName $f.state): $($f.evidence)" }
        if ($f.evidence -notmatch 'every day at 03:00 UTC') { throw "evidence should describe the schedule: $($f.evidence)" }
        if ($f.PSObject.Properties['actionKind'] -and $f.actionKind) { throw "no fix should be offered, got '$($f.actionKind)'" }
        try { $null = Invoke-RestMethod "$base/api/v1/diagnostics/backups-schedule/fix" -Method Post -TimeoutSec 30 } catch { }
        if (@(Get-Rules $base).Count -ne 1) { throw 'a further fix attempt must not create another rule' }
    }

    Test-RouteSmoke 'switching the rule off warns again but does not invite a duplicate rule' {
        $rule = @(Get-Rules $base)[0]
        $null = Invoke-RestMethod "$base/api/v1/automation/rules/$(Get-RuleId $rule)/enabled" -Method Post -ContentType 'application/json' -Body '{"enabled":false}' -TimeoutSec 10
        $f = Get-ScheduleFinding $base
        if ((Get-StateName $f.state) -ne 'Warning') { throw "expected Warning, got $(Get-StateName $f.state): $($f.evidence)" }
        if ($f.evidence -notmatch 'switched off') { throw "evidence should say the rule is switched off: $($f.evidence)" }
        if ($f.PSObject.Properties['actionKind'] -and $f.actionKind) { throw "no fix may be offered while a rule exists, got '$($f.actionKind)'" }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    throw "MystTiq v0.7.103.0 backup schedule smoke gate failed: $($failures.Count) check(s): $($failures -join ', ')"
}
Write-Host "MystTiq v0.7.103.0 backup schedule smoke gate passed." -ForegroundColor Green
