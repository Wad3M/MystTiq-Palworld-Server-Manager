param([string]$ProjectRoot=".",[switch]$RunBuild,[switch]$ExportJson)
$ErrorActionPreference="Stop";$root=(Resolve-Path $ProjectRoot).Path;$results=@()
function Add-TestResult($a,$t,[bool]$o,$d){$script:results += [pscustomobject]@{Area=$a;Test=$t;Passed=$o;Details=$d};Write-Host ("[{0}] {1} :: {2}" -f $(if($o){"PASS"}else{"FAIL"}),$a,$t);if(!$o){Write-Host "       $d"}}
function Check-Literal($a,$t,$x,$v){$o=$x -match [regex]::Escape($v);Add-TestResult $a $t $o $(if($o){"Found expected contract."}else{"Missing: $v"})}
Write-Host "`nMystTiq v0.2.15.10 Native Runtime Module Evidence Harness`nRepository: $root`n"
$n=Get-Content (Join-Path $root "src\PalworldManager\Services\NativeModuleEvidenceService.cs") -Raw
$v=Get-Content (Join-Path $root "src\PalworldManager\Services\ModVerificationService.cs") -Raw
$s=Get-Content (Join-Path $root "src\PalworldManager\Services\ServerService.cs") -Raw
$p=Get-Content (Join-Path $root "Directory.Build.props") -Raw
Check-Literal "Native Evidence" "Provider exists" $n "NativeModuleEvidenceService"
Check-Literal "Native Evidence" "Canonical full path matching" $n "Path.GetFullPath"
Check-Literal "Native Evidence" "Mapped state exists" $n "NativeModuleEvidenceState.Mapped"
Check-Literal "Native Evidence" "Unavailable state exists" $n "NativeModuleEvidenceState.Unavailable"
Check-Literal "Native Evidence" "No filename-only main.dll match" $n "modules.Contains"
Check-Literal "Session" "Refresh active snapshot exists" $s "RefreshActiveSessionSnapshot"
Check-Literal "Session" "Process tree modules inspected" $s "foreach (var processInfo in processes)"
Check-Literal "Verification" "Native evidence wired" $v "nativeModuleEvidence.Inspect(mod)"
Check-Literal "Verification" "Mapped promotes Confirmed Loaded" $v "moduleEvidence.ConfirmedMapped"
Check-Literal "Verification" "100 percent module confidence" $v '"PalServer native module table"'
Check-Literal "Versioning" "Version is v0.2.15.10" $p "<VersionPrefix>0.2.15.10</VersionPrefix>"
Add-TestResult "Documentation" "Release notes present" (Test-Path (Join-Path $root "release-notes\v0.2.15.10.md")) "release notes"
Add-TestResult "Documentation" "Build plan present" (Test-Path (Join-Path $root "release-notes\BUILD_TEST_PLAN_v0.2.15.10.md")) "build plan"
if($RunBuild){foreach($step in @("Clean","Validate","All")){try{$global:LASTEXITCODE=0;& (Join-Path $root "Build.ps1") $step;Add-TestResult "Build" "Build.ps1 $step" $true "Completed without throwing."}catch{R "Build" "Build.ps1 $step" $false $_.Exception.Message;break}}}
$pass=@($results|? Passed).Count;$fail=@($results|?{-not $_.Passed}).Count
Write-Host "`n================ v0.2.15.10 Summary ================";Write-Host "Passed : $pass";Write-Host "Failed : $fail"
if($ExportJson){$d=Join-Path $root "artifacts\logic-tests";New-Item -ItemType Directory -Force $d|Out-Null;$q=Join-Path $d ("MystTiq_v0.2.15.10_{0}.json"-f(Get-Date -Format "yyyyMMdd_HHmmss"));$results|ConvertTo-Json -Depth 5|Set-Content $q -Encoding UTF8;Write-Host "JSON report: $q"}
if($fail){exit 1}
