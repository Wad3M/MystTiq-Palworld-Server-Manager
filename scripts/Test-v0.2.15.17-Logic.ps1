param([string]$ProjectRoot=".",[switch]$RunBuild,[switch]$ExportJson)
$ErrorActionPreference="Stop";$root=(Resolve-Path $ProjectRoot).Path;$results=@()
function Add-TestResult($a,$t,[bool]$o,$d){$script:results += [pscustomobject]@{Area=$a;Test=$t;Passed=$o;Details=$d};Write-Host ("[{0}] {1} :: {2}" -f $(if($o){"PASS"}else{"FAIL"}),$a,$t);if(!$o){Write-Host "       $d"}}
function Check-Literal($a,$t,$x,$v){$o=$x -match [regex]::Escape($v);Add-TestResult $a $t $o $(if($o){"Found expected contract."}else{"Missing: $v"})}
Write-Host "`nMystTiq v0.2.15.17 Live World Telemetry & Dashboard Pulse Harness`nRepository: $root`n"

$models=Get-Content (Join-Path $root "src\PalworldManager\Models\WorldTelemetryModels.cs") -Raw
$clock=Get-Content (Join-Path $root "src\PalworldManager\Services\WorldClockProvider.cs") -Raw
$telemetry=Get-Content (Join-Path $root "src\PalworldManager\Services\WorldTelemetryService.cs") -Raw
$server=Get-Content (Join-Path $root "src\PalworldManager\Services\ServerService.cs") -Raw
$composition=Get-Content (Join-Path $root "src\PalworldManager\Services\ApplicationServiceComposition.cs") -Raw
$dashboard=Get-Content (Join-Path $root "src\PalworldManager\MainWindow.DashboardModernization.cs") -Raw
$xaml=Get-Content (Join-Path $root "src\PalworldManager\MainWindow.xaml") -Raw
$readme=Get-Content (Join-Path $root "README.md") -Raw
$props=Get-Content (Join-Path $root "Directory.Build.props") -Raw

Check-Literal "Telemetry" "Immutable telemetry snapshot exists" $models "public sealed record WorldTelemetrySnapshot"
Check-Literal "Telemetry" "World clock snapshot exists" $models "public sealed record WorldClockSnapshot"
Check-Literal "Clock" "GameDateTimeTicks is authoritative source" $clock "GameDateTimeTicks"
Check-Literal "Clock" "Palworld day tick constant is explicit" $clock "864_000_000_000L"
Check-Literal "Clock" "Large decoded JSON is stream searched" $clock "new FileStream(path"
$estimated=[regex]::IsMatch($clock,'session|uptime|DateTime\.Now\s*[-+]',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
Add-TestResult "Clock" "World clock is not estimated from uptime" (-not $estimated) $(if($estimated){"WorldClockProvider contains an uptime/session estimate path."}else{"World clock is save-evidence only."})

Check-Literal "Session" "Server exposes active session id" $server "public long ActiveSessionId"
Check-Literal "Session" "Server exposes active session start" $server "public DateTime? ActiveSessionStartedAt"
Check-Literal "Session" "Telemetry resets on session id change" $telemetry "if (activeSessionId != sessionId)"
Check-Literal "Session" "Peak player counter exists" $telemetry "peakPlayers"
Check-Literal "Session" "Join counter exists" $telemetry "joins++"
Check-Literal "Session" "Leave counter exists" $telemetry "leaves++"
Check-Literal "Session" "Unique player set exists" $telemetry "uniqueKeys"
Check-Literal "Activity" "World-day change event exists" $telemetry "WorldDayChanged"
Check-Literal "Composition" "World telemetry is composed centrally" $composition "WorldTelemetry = new WorldTelemetryService()"

Check-Literal "Dashboard" "WORLD PULSE surface exists" $xaml 'Text="WORLD PULSE"'
Check-Literal "Dashboard" "World clock control exists" $xaml 'x:Name="DashboardWorldClockText"'
Check-Literal "Dashboard" "Pulse uptime control exists" $xaml 'x:Name="DashboardPulseUptimeText"'
Check-Literal "Dashboard" "Pulse player control exists" $xaml 'x:Name="DashboardPulsePlayersText"'
Check-Literal "Dashboard" "Pulse save control exists" $xaml 'x:Name="DashboardPulseSaveText"'
Check-Literal "Dashboard" "Dashboard consumes telemetry service" $dashboard "worldTelemetry.Update("
Check-Literal "Dashboard" "Dashboard uptime uses PalServer telemetry" $dashboard "DashboardUptimeText.Text = FormatDuration(pulse.SessionUptime)"
Check-Literal "Dashboard" "Unavailable clock is explicit" $dashboard "MystTiq will not estimate it"
Check-Literal "Activity" "Join/leave/day events feed audit" $dashboard 'RecordAudit("Information", "World Pulse"'

Check-Literal "README" "Live World Telemetry documented" $readme "## Live World Telemetry"
Check-Literal "README" "No extrapolation promise documented" $readme "does **not** extrapolate"
Check-Literal "Versioning" "Version is v0.2.15.17" $props "<VersionPrefix>0.2.15.17</VersionPrefix>"
Add-TestResult "Documentation" "Release notes present" (Test-Path (Join-Path $root "release-notes\v0.2.15.17.md")) "release notes"
Add-TestResult "Documentation" "Build plan present" (Test-Path (Join-Path $root "release-notes\BUILD_TEST_PLAN_v0.2.15.17.md")) "build plan"
Add-TestResult "Documentation" "Telemetry architecture note present" (Test-Path (Join-Path $root "LIVE_WORLD_TELEMETRY_v0.2.15.17.md")) "telemetry architecture"

if($RunBuild){
    foreach($step in @("Clean","Validate","All")){
        try{
            $global:LASTEXITCODE=0
            & (Join-Path $root "Build.ps1") $step
            if($LASTEXITCODE -ne 0){throw "Exit code: $LASTEXITCODE"}
            Add-TestResult "Build" "Build.ps1 $step" $true "Completed successfully."
        }catch{
            Add-TestResult "Build" "Build.ps1 $step" $false $_.Exception.Message
            break
        }
    }
}

$pass=@($results|? Passed).Count;$fail=@($results|?{-not $_.Passed}).Count
Write-Host "`n================ MystTiq v0.2.15.17 Summary ================";Write-Host "Passed : $pass";Write-Host "Failed : $fail"
if($ExportJson){$d=Join-Path $root "artifacts\logic-tests";New-Item -ItemType Directory -Force $d|Out-Null;$q=Join-Path $d ("MystTiq_v0.2.15.17_{0}.json"-f(Get-Date -Format "yyyyMMdd_HHmmss"));$results|ConvertTo-Json -Depth 5|Set-Content $q -Encoding UTF8;Write-Host "JSON report: $q"}
if($fail){exit 1}
