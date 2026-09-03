[CmdletBinding()]
param([string]$ProjectRoot='.',[switch]$RunBuild,[switch]$ExportJson)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
$results=[System.Collections.Generic.List[object]]::new()
$currentVersion=& (Join-Path $root 'scripts\Get-ProjectVersion.ps1')
$linuxAcceptanceRelative=('scripts\Test-v{0}-LinuxAcceptance.sh' -f $currentVersion)
$productionReadinessRelative=('scripts\Test-v{0}-ProductionReadiness.sh' -f $currentVersion)
function Check([string]$Area,[string]$Name,[bool]$Pass,[string]$Detail=''){
 $results.Add([pscustomobject]@{Area=$Area;Name=$Name;Pass=$Pass;Detail=$Detail})
 if($Pass){Write-Host "[PASS] $Area :: $Name" -ForegroundColor Green}else{Write-Host "[FAIL] $Area :: $Name" -ForegroundColor Red;if($Detail){Write-Host "       $Detail"}}
}
function Has([string]$File,[string]$Needle){$p=Join-Path $root $File;(Test-Path $p -PathType Leaf)-and((Get-Content $p -Raw).Contains($Needle))}
Write-Host "`nMystTiq v0.3.1.9 MOD & UE4SS Management Harness" -ForegroundColor Cyan
Check 'Versioning' 'Version is current' (Has 'Directory.Build.props' '<VersionPrefix>0.3.1.9</VersionPrefix>')
Check 'MOD API' 'Management service exists' (Test-Path (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs'))
Check 'MOD API' 'Inventory endpoint exists' (Has 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs' '/api/v1/mods')
Check 'MOD API' 'Verification endpoint exists' (Has 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs' '/api/v1/mods/verify')
Check 'MOD API' 'UE4SS endpoint exists' (Has 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs' '/api/v1/ue4ss')
Check 'Resolver' 'Modern UE4SS layout is preferred' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'Modern UE4SS layout')
Check 'Resolver' 'Runtime log evidence is supported' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'UE4SS runtime log')
Check 'Resolver' 'Root mismatch is surfaced' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'UE4SS Mod Root Mismatch')
Check 'Runtime' 'Lua load signature is parsed' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'Starting\\s+Lua\\s+mod')
Check 'State' 'PAK root is ~mods' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' '"~mods"')
Check 'State' 'mods.txt is authoritative' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'mods.txt is authoritative')
Check 'State' 'enabled.txt is neutralized' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'Neutralize enabled.txt')
Check 'Health' 'Active / Unverified exists' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'Active / Unverified')
Check 'Health' 'Confirmed Loaded exists' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'Confirmed Loaded')
Check 'Safety' 'Mutation requires stopped PalServer' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'Stop PalServer before enabling or disabling MOD files')
Check 'Safety' 'Mutation is serialized' (Has 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs' 'mutationGate.WaitAsync')
Check 'Desktop' 'MOD inventory client exists' (Has 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs' 'GetModsAsync')
Check 'Desktop' 'MOD page is functional' (Has 'src\MystTiq.Desktop\MainWindow.axaml' 'MOD &amp; UE4SS Management')
Check 'Desktop' 'Verify action exists' (Has 'src\MystTiq.Desktop\MainWindow.axaml' 'Verify &amp; Scan')
Check 'Desktop' 'Enable action exists' (Has 'src\MystTiq.Desktop\MainWindow.axaml' 'Enable Selected')
Check 'Desktop' 'Disable action exists' (Has 'src\MystTiq.Desktop\MainWindow.axaml' 'Disable Selected')
Check 'Acceptance' 'Linux acceptance uses current runner' (Test-Path (Join-Path $root $linuxAcceptanceRelative))
Check 'Acceptance' 'MOD inventory is checked' (Has $linuxAcceptanceRelative 'MOD inventory endpoint')
Check 'Acceptance' 'UE4SS status is checked' (Has $linuxAcceptanceRelative 'UE4SS status endpoint')
Check 'Regression' 'Player & Guild Explorer remains present' (Has 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs' '/api/v1/world/players-guilds')
Check 'Regression' 'World Explorer remains present' (Has 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs' '/api/v1/world/explorer')
Check 'Regression' 'Doctor remains present' (Has 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs' '/api/v1/doctor')
Check 'Documentation' 'README contains current version' (Has 'README.md' 'v0.3.1.9')
Check 'Documentation' 'Roadmap contains MOD milestone' (Has 'docs\roadmap\PRODUCT_ROADMAP.md' 'v0.3.1.9 — MOD & UE4SS Management')
if($RunBuild){
 try{& (Join-Path $root 'Build.ps1') Validate;Check 'Build' 'Release validation' $true}catch{Check 'Build' 'Release validation' $false $_.Exception.Message}
 try{& (Join-Path $root 'Build.ps1') DesktopWindows;Check 'Build' 'Windows desktop' $true}catch{Check 'Build' 'Windows desktop' $false $_.Exception.Message}
 try{& (Join-Path $root 'Build.ps1') DesktopLinux;Check 'Build' 'Linux desktop' $true}catch{Check 'Build' 'Linux desktop' $false $_.Exception.Message}
 try{& (Join-Path $root 'Build.ps1') LinuxHeadless;Check 'Build' 'Linux headless MOD/UE4SS' $true}catch{Check 'Build' 'Linux headless MOD/UE4SS' $false $_.Exception.Message}
}
$passed=@($results|Where-Object Pass).Count;$failed=@($results|Where-Object{-not $_.Pass}).Count
Write-Host "`n================ MystTiq v0.3.1.9 Summary ================";Write-Host "Passed : $passed";Write-Host "Failed : $failed"
if($ExportJson){$dir=Join-Path $root 'artifacts\logic-tests';New-Item -ItemType Directory -Force $dir|Out-Null;$path=Join-Path $dir "MystTiq_v0.3.1.9_$(Get-Date -Format yyyyMMdd-HHmmss).json";$results|ConvertTo-Json -Depth 5|Set-Content $path;Write-Host "JSON report: $path"}
if($failed){exit 1}
