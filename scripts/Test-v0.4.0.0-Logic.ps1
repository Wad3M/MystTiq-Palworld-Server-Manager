[CmdletBinding()]
param([string]$ProjectRoot='.',[switch]$RunBuild,[switch]$ExportJson)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
$checks=@()
function Check($Area,$Name,$Ok){ $script:checks += [pscustomobject]@{Area=$Area;Name=$Name;Passed=[bool]$Ok}; Write-Host "[$(if($Ok){'PASS'}else{'FAIL'})] $Area :: $Name" }
$props=Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$manager=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\WindowsServiceManager.cs') -Raw
$models=Get-Content (Join-Path $root 'src\MystTiq.Core\Models\WindowsServiceModels.cs') -Raw
$build=Get-Content (Join-Path $root 'Build.ps1') -Raw
Check 'Versioning' 'Version is v0.4.0.0' ($props -match '<VersionPrefix>0\.4\.0\.0</VersionPrefix>')
Check 'Windows Service' 'SCM manager exists' ($manager -match 'class WindowsServiceManager')
Check 'Windows Service' 'Service name is stable' ($manager -match 'MystTiqPalworld')
Check 'Windows Service' 'Automatic startup configured' ($manager -match '"start=", "auto"')
Check 'Windows Service' 'Recovery restart policy configured' ($manager -match 'restart/10000/restart/10000/restart/10000')
Check 'Windows Service' 'Status contract exists' ($models -match 'record WindowsServiceStatus')
Check 'Windows Service' 'Install result contract exists' ($models -match 'record WindowsServiceInstallResult')
Check 'Build' 'WindowsHeadless action exists' ($build -match "'WindowsHeadless'")
Check 'Regression' 'Linux systemd manager remains present' (Test-Path (Join-Path $root 'src\MystTiq.Core\Services\LinuxSystemdServiceManager.cs'))
if($RunBuild){
  & (Join-Path $root 'Build.ps1') Validate
  Check 'Build' 'Release validation' $true
  & (Join-Path $root 'Build.ps1') WindowsHeadless
  Check 'Build' 'Windows persistent host publishes' $true
}
$passed=@($checks|Where-Object Passed).Count; $failed=@($checks|Where-Object {-not $_.Passed}).Count
Write-Host "`n================ MystTiq v0.4.0.0 Summary ================"
Write-Host "Passed : $passed"; Write-Host "Failed : $failed"
if($ExportJson){ $dir=Join-Path $root 'artifacts\logic-tests'; New-Item $dir -ItemType Directory -Force|Out-Null; $path=Join-Path $dir "MystTiq_v0.4.0.0_$(Get-Date -Format yyyyMMdd-HHmmss).json"; $checks|ConvertTo-Json -Depth 4|Set-Content $path; Write-Host "JSON report: $path" }
if($failed -gt 0){ exit 1 }
