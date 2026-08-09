<#
.SYNOPSIS
    MystTiq v0.2.15.7 unified runtime-state regression harness.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$RunBuild,
    [switch]$ExportJson
)

$ErrorActionPreference = 'Stop'
$ExpectedVersion = '0.2.15.7'
$Root = (Resolve-Path $ProjectRoot).Path
$Results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Area,[string]$Test,[bool]$Passed,[string]$Details) {
    $Results.Add([pscustomobject]@{ Area=$Area; Test=$Test; Status=$(if($Passed){'PASS'}else{'FAIL'}); Details=$Details })
    Write-Host "[$(if($Passed){'PASS'}else{'FAIL'})] $Area :: $Test" -ForegroundColor $(if($Passed){'Green'}else{'Red'})
    if(-not $Passed){ Write-Host "       $Details" }
}
function Read-Text([string]$Relative) {
    $path=Join-Path $Root $Relative
    if(-not(Test-Path $path)){ Add-Result 'Structure' $Relative $false 'Required file missing.'; return '' }
    Get-Content $path -Raw
}
function Check([string]$Area,[string]$Name,[string]$Text,[string]$Pattern,[string]$Ok,[string]$Bad) {
    $hit=[regex]::IsMatch($Text,$Pattern,[System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Add-Result $Area $Name $hit $(if($hit){$Ok}else{$Bad})
}

Write-Host "`nMystTiq v$ExpectedVersion Unified Runtime State Harness" -ForegroundColor Cyan
Write-Host "Repository: $Root`n"

$state=Read-Text 'src\PalworldManager\Services\RuntimeStateService.cs'
$model=Read-Text 'src\PalworldManager\Models\RuntimeStateSnapshot.cs'
$scanner=Read-Text 'src\PalworldManager\Services\ModScannerService.cs'
$mods=Read-Text 'src\PalworldManager\Services\ModService.cs'
$main=Read-Text 'src\PalworldManager\MainWindow.xaml.cs'
$props=Read-Text 'Directory.Build.props'

Check 'Runtime State' 'Authoritative service exists' $state 'class\s+RuntimeStateService' 'RuntimeStateService found.' 'RuntimeStateService missing.'
Check 'Runtime State' 'Immutable snapshot exists' $model 'sealed\s+record\s+RuntimeStateSnapshot' 'Immutable snapshot model found.' 'RuntimeStateSnapshot missing.'
Check 'Session Boundary' 'BeginSession clears loaded evidence' $state 'BeginSession.*?loadedAliases\.Clear\(\).*?CaptureBaselineUnsafe' 'New-session boundary clears evidence and captures the current log offset.' 'Session reset/log baseline logic missing.'
Check 'Session Boundary' 'EndSession clears runtime evidence' $state 'EndSession.*?loadedAliases\.Clear\(\).*?logOffsets\.Clear\(\)' 'Server-exit reset is present.' 'EndSession reset logic missing.'
Check 'Session Boundary' 'Historical current-log bytes are excluded' $state 'CaptureBaselineUnsafe.*?new FileInfo\(path\)\.Length' 'Existing log length becomes the new-session read boundary.' 'Existing log boundary capture missing.'
Check 'Log Rotation' 'Rotation/truncation handled' $state 'length\s*<\s*offset\)\s*offset\s*=\s*0' 'Rotated/truncated logs can be consumed safely.' 'Rotation/truncation handling missing.'
Check 'Log Rotation' 'Replacement log identity is detected' $state 'cursor\.CreationTimeUtc\s*!=\s*creationUtc.*?new RuntimeLogCursor\(0,\s*creationUtc,' 'Recreated UE4SS logs reset to byte zero even when already larger than the old baseline.' 'Replacement-log identity handling missing.'
Check 'Log Rotation' 'In-place rewrite fingerprint is detected' $state 'PrefixFingerprint.*?ReadPrefixFingerprint.*?PrefixLength' 'In-place UE4SS log rewrites are distinguished from append-only growth.' 'Prefix-fingerprint rewrite guard missing.'
Check 'Runtime Evidence' 'All current-session UE4SS logs are observed' $state 'ReadCurrentSessionEvidenceUnsafe.*?DiscoverRuntimeLogsUnsafe.*?UE4SS\*\.log' 'Runtime state observes all current-session UE4SS log candidates.' 'Multi-log current-session observation missing.'
Check 'Runtime Evidence' 'Starting Lua mod is parsed centrally' $state 'Starting\\s\+Lua.*?StartedLuaModPattern' 'Central service parses UE4SS loaded evidence.' 'Central loaded-evidence parser missing.'
Check 'Runtime Evidence' 'Positive evidence is revisioned' $state 'if\s*\(changed\)\s*revision\+\+' 'State revision advances on runtime evidence changes.' 'Revision update missing.'
Check 'Runtime Evidence' 'Runtime errors are centralized' $state 'RuntimeErrorPattern.*?runtimeErrors' 'Runtime error evidence is retained centrally.' 'Runtime error collection missing.'
Check 'Scanner Integration' 'Scanner observes shared runtime state' $scanner 'runtimeState\.Observe\(info\)' 'Scanner feeds runtime observations into shared state.' 'Scanner does not observe shared runtime state.'
Check 'Scanner Integration' 'Scanner applies shared runtime state' $scanner 'runtimeState\.ApplyTo\(materialized\)' 'Scanner applies authoritative loaded state to inventory.' 'Scanner does not apply authoritative state.'
$directOld=[regex]::IsMatch($scanner,'new HashSet<string>\(info\.LoadedMods',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
Add-Result 'Scanner Integration' 'No direct LoadedMods assignment' (-not $directOld) $(if($directOld){'Old direct resolver runtime ownership remains.'}else{'Runtime-loaded ownership is centralized.'})
Check 'MainWindow' 'Shared service is constructed once' $main 'runtimeState\s*=\s*new\s+RuntimeStateService\(\)' 'MainWindow owns one runtime-state service.' 'Shared runtime service construction missing.'
Check 'MainWindow' 'ModService receives shared service' $main 'new\(settings,\s*ue4ssRuntimeResolver,\s*runtimeState\)' 'ModService and scanner share the same state service.' 'ModService is not wired to shared runtime state.'
Check 'Compile Regression' 'Two-argument ModService constructor uses its declared resolver parameter' $mods 'public\s+ModService\(AppSettings settings,\s*Ue4ssRuntimeResolver ue4ssResolver\)\s*:\s*this\(settings,\s*ue4ssResolver,\s*new RuntimeStateService\(\)\)' 'Two-argument constructor chains through ue4ssResolver correctly.' 'Constructor still references a stale resolver identifier.'
Check 'MainWindow' 'New server session starts runtime session' $main 'runtimeState\.BeginSession\(ue4ssRuntimeResolver\.Refresh\(\)\)' 'New-session preparation establishes the runtime boundary.' 'BeginSession is not wired to server-session preparation.'
Check 'MainWindow' 'Server exit ends runtime session' $main 'runtimeState\.EndSession\(\)' 'Server exit clears shared runtime state.' 'EndSession is not wired to server exit.'
Check 'Dashboard Sync' 'Positive shared evidence heals Runtime Unverified rows' $main 'mod\.LoadedByUe4ss.*?existing\.RuntimeStatus\s*=\s*"Loaded".*?runtimeChecked:\s*true' 'Dashboard consumes positive current-session runtime state during normal refresh.' 'Dashboard can remain stale after shared runtime evidence becomes Loaded.'
Check 'MOD Info' 'Refresh Info never falls through to online search' $main 'RefreshSelectedModInfo_Click.*?REFRESH INFO is a local/runtime refresh action.*?RefreshMods\(\)' 'Refresh Info performs a local/runtime refresh.' 'Refresh Info is not wired to a local/runtime refresh.'
$refreshBlock=[regex]::Match($main,'private async void RefreshSelectedModInfo_Click.*?\n    }\n\n    private void SearchSelectedModOnline_Click',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline).Value
$refreshLaunchesSearch=[regex]::IsMatch($refreshBlock,'SearchSelectedModOnline_Click\(')
Add-Result 'MOD Info' 'Refresh Info does not launch browser search' (-not $refreshLaunchesSearch) $(if($refreshLaunchesSearch){'Refresh Info still calls Search Online.'}else{'Browser search remains isolated to Search Online.'})
$oldLatch=[regex]::IsMatch($main,'sessionRuntimeLoadedAliases|ApplySessionRuntimeLoadedEvidence',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
Add-Result 'MainWindow' 'Old UI-side runtime latch removed' (-not $oldLatch) $(if($oldLatch){'Legacy UI-side runtime latch remains.'}else{'MOD Library no longer owns a private runtime-loaded latch.'})
Check 'Diagnostics' 'Runtime session is visible' $main 'Runtime session:.*?runtimeSnapshot\.SessionId' 'MOD Runtime diagnostics expose session identity.' 'Runtime session diagnostic missing.'
Check 'Diagnostics' 'Runtime revision is visible' $main 'revision \{runtimeSnapshot\.Revision\}' 'MOD Runtime diagnostics expose runtime revision.' 'Runtime revision diagnostic missing.'
Check 'Versioning' 'Build version is current' $props '<VersionPrefix>0\.2\.15\.7</VersionPrefix>' 'Directory.Build.props is v0.2.15.7.' 'Build version mismatch.'

if($RunBuild){
    Push-Location $Root
    try {
        Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
        foreach($step in 'Clean','Validate','All'){
            try { & .\Build.ps1 $step; Add-Result 'Build' "Build.ps1 $step" $true 'Completed without throwing.' }
            catch { Add-Result 'Build' "Build.ps1 $step" $false $_.Exception.Message; break }
        }
    } finally { Pop-Location }
}

$pass=@($Results|Where-Object Status -eq 'PASS').Count
$fail=@($Results|Where-Object Status -eq 'FAIL').Count
Write-Host "`n================ MystTiq v$ExpectedVersion Summary ================" -ForegroundColor Cyan
Write-Host "Passed : $pass" -ForegroundColor Green
Write-Host "Failed : $fail" -ForegroundColor $(if($fail){'Red'}else{'Green'})

if($ExportJson){
    $dir=Join-Path $Root 'artifacts\logic-tests'; New-Item -ItemType Directory -Force $dir | Out-Null
    $path=Join-Path $dir ("MystTiq_v0.2.15.7_LogicTests_{0}.json" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
    [pscustomobject]@{Version="v$ExpectedVersion";Passed=$pass;Failed=$fail;Results=$Results}|ConvertTo-Json -Depth 8|Set-Content $path -Encoding UTF8
    Write-Host "JSON report: $path"
}

Write-Host "`nManual runtime acceptance remains required: 5-minute Loaded persistence, repeated Library refreshes, Library/Dashboard/report agreement, stop reset, and clean reacquisition under a new session ID."
if($fail){exit 1}else{exit 0}
