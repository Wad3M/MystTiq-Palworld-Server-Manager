[CmdletBinding()]
param([string]$ProjectRoot='.',[switch]$RunBuild,[switch]$ExportJson)
$ErrorActionPreference='Stop';$root=(Resolve-Path $ProjectRoot).Path;$checks=@()
function Check($area,$name,$ok){$script:checks += [pscustomobject]@{Area=$area;Check=$name;Passed=[bool]$ok};Write-Host ("[{0}] {1} :: {2}" -f ($(if($ok){'PASS'}else{'FAIL'}),$area,$name)) -ForegroundColor $(if($ok){'Green'}else{'Red'})}
function Text($rel){Get-Content (Join-Path $root $rel) -Raw}
$props=Text 'Directory.Build.props';$engine=Text 'src\MystTiq.Core\Services\NetworkDiagnosticsService.cs';$models=Text 'src\MystTiq.Core\Models\NetworkDiagnosticModels.cs';$contract=Text 'src\MystTiq.Core\Services\INetworkDiagnosticsPlatformService.cs';$win=Text 'src\MystTiq.Core\Services\WindowsNetworkDiagnosticsPlatformService.cs';$linux=Text 'src\MystTiq.Core\Services\LinuxNetworkDiagnosticsPlatformService.cs';$api=Text 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs';$vm=Text 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs';$ui=Text 'src\MystTiq.Desktop\MainWindow.axaml';$road=Text 'docs\roadmap\PRODUCT_ROADMAP.md'
Check 'Versioning' 'Version is v0.4.0.1' ($props -match '<VersionPrefix>0\.4\.0\.1</VersionPrefix>')
Check 'Architecture' 'Network platform abstraction exists' ($contract -match 'INetworkDiagnosticsPlatformService')
Check 'Process' 'PalServer PID-aware ownership is evaluated' ($engine -match 'OwningProcessId==proc\.ProcessId')
Check 'Port' 'Default UDP 8211 is centralized' ($engine -match 'DefaultGamePort=8211')
Check 'Port' 'Explicit -port launch argument is resolved' ($engine -match 'StartsWith\("-port="')
Check 'Binding' '0.0.0.0 is translated to All IPv4 interfaces' ($engine -match 'All IPv4 interfaces \(0\.0\.0\.0\)')
Check 'Binding' 'Wrong-process ownership is a distinct failure' ($engine -match 'already in use by')
Check 'Binding' 'Wrong PalServer port is a distinct warning' ($engine -match 'not expected UDP')
Check 'Startup' 'Startup grace returns STARTING semantics' ($engine -match 'DiagnosticState\.Starting' -and $engine -match 'DefaultStartupGrace')
Check 'Firewall' 'Windows rule inspection uses port filters' ($win -match 'Get-NetFirewallPortFilter')
Check 'Firewall' 'MystTiq-managed rule name is stable' ($win -match 'MystTiq Palworld Server - Game')
Check 'Firewall' 'Repair reuses existing managed rule' ($win -match 'Get-NetFirewallRule -DisplayName' -and $win -match 'Set-NetFirewallRule')
Check 'Firewall' 'Repair creates rule only when absent' ($win -match 'New-NetFirewallRule')
Check 'LAN' 'LAN IPv4 addresses use network abstraction' ($win -match 'NetworkInterface\.GetAllNetworkInterfaces')
Check 'Optional Services' 'RCON runs only when enabled' ($engine -match 'RCONEnabled' -and $engine -match 'if\(!enabled\)return')
Check 'Optional Services' 'REST runs only when enabled and records latency' ($engine -match 'RESTAPIEnabled' -and $engine -match 'ElapsedMilliseconds')
Check 'API' 'Network diagnostics endpoint exists' ($api -match '/api/v1/diagnostics/network')
Check 'Recovery' 'Controlled lifecycle RestartAsync is used' ($api -match 'network/restart' -and $api -match 'lifecycle\.RestartAsync')
Check 'Recovery' 'Post-restart diagnostics are re-run' ($api -match 'var verified = await networkDiagnostics\.RunAsync')
Check 'UI' 'Dedicated Diagnostics page exists' ($ui -match 'Diagnostics / Network Connectivity' -and $vm -match 'IsDiagnosticsPage')
Check 'UI' 'Runtime and Network are displayed separately' ($ui -match 'NetworkRuntime' -and $ui -match 'NetworkHealth')
Check 'Report' 'Support report omits secret fields by construction' ($models -match 'ToSupportText' -and $models -notmatch 'ToSupportText[\s\S]{0,1200}(Password|BearerToken|ServerPassword)')
Check 'Regression' 'Running-without-8211 recommends restart' ($engine -match 'running but is not listening on UDP' -and $engine -match 'Restart Palworld Server')
Check 'Regression' 'Linux network adapter remains available' ($linux -match 'LinuxNetworkDiagnosticsPlatformService')
Check 'Regression' 'MOD/UE4SS implementation remains present' (Test-Path (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs'))
Check 'Roadmap' 'Projected v0.4.0.2 save/freeze monitor captured' ($road -match 'v0\.4\.0\.2.+Save / Client Freeze')
Check 'Roadmap' 'Projected v0.4.0.3 auto-save/save health captured' ($road -match 'v0\.4\.0\.3.+Save Management')
Check 'Roadmap' 'Projected v0.4.0.4 incident bundles captured' ($road -match 'v0\.4\.0\.4.+Incident Bundles')
Check 'Roadmap' 'Projected v0.4.0.5 mod isolation/testing captured' ($road -match 'v0\.4\.0\.5.+MOD Validation')
if($RunBuild){
 & (Join-Path $root 'Build.ps1') Validate;Check 'Build' 'Release validation' $true
 & (Join-Path $root 'Build.ps1') WindowsHeadless;Check 'Build' 'Windows persistent host publishes' $true
 & (Join-Path $root 'Build.ps1') DesktopWindows;Check 'Build' 'Windows Avalonia desktop publishes' $true
 & (Join-Path $root 'Build.ps1') LinuxHeadless;Check 'Build' 'Linux headless regression publishes' $true
 & (Join-Path $root 'Build.ps1') DesktopLinux;Check 'Build' 'Linux Avalonia desktop regression publishes' $true
}
$passed=@($checks|? Passed).Count;$failed=@($checks|?{-not $_.Passed}).Count
Write-Host "`n================ MystTiq v0.4.0.1 Summary ================"
Write-Host "Passed : $passed";Write-Host "Failed : $failed"
if($ExportJson){$dir=Join-Path $root 'artifacts\logic-tests';New-Item $dir -ItemType Directory -Force|Out-Null;$path=Join-Path $dir "MystTiq_v0.4.0.1_$(Get-Date -Format yyyyMMdd-HHmmss).json";$checks|ConvertTo-Json -Depth 4|Set-Content $path;Write-Host "JSON report: $path"}
if($failed){exit 1}
