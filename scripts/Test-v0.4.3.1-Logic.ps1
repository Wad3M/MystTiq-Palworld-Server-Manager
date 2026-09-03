[CmdletBinding()]
param([string]$ProjectRoot='.',[switch]$RunBuild,[switch]$ExportJson)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
$checks=@()
function Check($area,$name,$ok){$script:checks += [pscustomobject]@{Area=$area;Check=$name;Passed=[bool]$ok};Write-Host ("[{0}] {1} :: {2}" -f ($(if($ok){'PASS'}else{'FAIL'}),$area,$name)) -ForegroundColor $(if($ok){'Green'}else{'Red'})}
function Text($rel){Get-Content (Join-Path $root $rel) -Raw}

$props=Text 'Directory.Build.props'
$core=Text 'src\MystTiq.Core\Services\LocalInstallationDiscoveryService.cs'
$desktopProject=Text 'src\MystTiq.Desktop\MystTiq.Desktop.csproj'
$app=Text 'src\MystTiq.Desktop\App.axaml.cs'
$vm=Text 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$ui=Text 'src\MystTiq.Desktop\MainWindow.axaml'
$api=Text 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs'
$build=Text 'Build.ps1'
$road=Text 'docs\roadmap\PRODUCT_ROADMAP.md'

Check 'Versioning' 'Version is v0.4.3.1' ($props -match '<VersionPrefix>0\.4\.0\.3</VersionPrefix>')
Check 'Discovery' 'Core local installation contract exists' ($core -match 'interface ILocalInstallationDiscoveryService' -and $core -match 'LocalInstallationSnapshot')
Check 'Discovery' 'Service and PalServer installation states are independent' ($core -match 'LocalMystTiqServiceState' -and $core -match 'LocalPalServerInstallationState')
Check 'Discovery' 'Windows legacy settings-v2.1.json is supported' ($core -match 'MystTiqPalworldServer' -and $core -match 'settings-v2\.1\.json')
Check 'Discovery' 'Known Windows PalServer default is a fallback' ($core -match 'C:\\GameServers\\Palworld\\Server')
Check 'Discovery' 'Linux server/config defaults remain supported' ($core -match '/opt/mysttiq/palserver' -and $core -match '/etc/mysttiq/mysttiq\.json')
Check 'Discovery' 'Explicit local server override is supported' ($core -match 'MYSTTIQ_SERVER_ROOT')
Check 'Discovery' 'Persistent-service API endpoint is discovered from config' ($core -match 'ApiBaseAddress' -and $core -match 'BindAddress' -and $core -match 'Tls')
Check 'Discovery' '0.0.0.0 API bind maps to local loopback for desktop use' ($core -match 'bind is "0\.0\.0\.0" or "::"' -and $core -match '127\.0\.0\.1')
Check 'Discovery' 'Discovery is read-only' ($core -notmatch 'WriteAllText|WriteAllBytes|File\.Delete|Directory\.Delete|File\.Move')

Check 'Composition' 'Avalonia references MystTiq.Core' ($desktopProject -match 'ProjectReference.+MystTiq\.Core')
Check 'Composition' 'App composes local discovery through Core factory' ($app -match 'LocalInstallationDiscoveryService\.ForCurrentPlatform' -and $app -match 'MainWindowViewModel\(api, profileStore, localDiscovery\)')
Check 'Composition' 'ViewModel receives local discovery dependency' ($vm -match 'ILocalInstallationDiscoveryService _localDiscovery' -and $vm -match 'ILocalInstallationDiscoveryService localDiscovery')
Check 'Behavior' 'Local discovery runs automatically at desktop startup' ($vm -match 'InitializeLocalDashboardAsync' -and $vm -match 'Dispatcher\.UIThread\.Post')
Check 'Behavior' 'Discovered local API endpoint is applied to local profile' ($vm -match 'ServerUrl = snapshot\.ApiBaseAddress')
Check 'Behavior' 'API unavailable does not erase local PalServer discovery' ($vm -match 'Local PalServer discovery:' -and $vm -match 'LocalApiStatus = "Unavailable"')
Check 'Behavior' 'Local PalServer process can populate PID without API' ($vm -match 'snapshot\.PalServerProcessDetected' -and $vm -match 'NativePidText = snapshot\.PalServerProcessId')
Check 'Behavior' 'Lifecycle mutations remain API routed' ($vm -match 'RunLifecycleAsync' -and $vm -match '_api\.StartServerAsync' -and $vm -match '_api\.StopServerAsync' -and $vm -match '_api\.RestartServerAsync')
Check 'Behavior' 'Destructive dashboard actions require an API connection' ($vm -match 'StartServerAsync, \(\) => !IsBusy && ConnectionState == "Connected"' -and $vm -match 'CreateBackupAsync, \(\) => !IsBusy && ConnectionState == "Connected"')

Check 'Dashboard Backend' 'Monitoring backend populates dashboard' ($vm -match 'RefreshMonitoringAsyncCore\(profile\)' -and $api -match 'GetMetricsAsync' -and $api -match 'GetLogTailAsync')
Check 'Dashboard Backend' 'Distribution backend populates dashboard' ($vm -match 'GetServerDistributionStatusAsync\(profile')
Check 'Dashboard Backend' 'Backup backend populates dashboard' ($vm -match 'GetBackupsAsync\(profile')
Check 'Dashboard Backend' 'World backend populates dashboard' ($vm -match 'GetWorldExplorerAsync\(profile')
Check 'Dashboard Backend' 'Player/guild backend populates dashboard' ($vm -match 'GetPlayerGuildExplorerAsync\(profile')
Check 'Dashboard Backend' 'MOD backend populates dashboard' ($vm -match 'GetModsAsync\(profile')
Check 'Dashboard Backend' 'Dashboard backend load is isolated from individual sub-check failures' ($vm -match 'Dashboard backup load:' -and $vm -match 'Dashboard world load:' -and $vm -match 'Dashboard MOD load:')

Check 'Dashboard UI' 'Local installation card is present' ($ui -match 'LOCAL INSTALLATION' -and $ui -match 'LocalServerRootText')
Check 'Dashboard UI' 'Service PalServer and API states are shown independently' ($ui -match 'LocalServiceStatus' -and $ui -match 'LocalPalServerStatus' -and $ui -match 'LocalApiStatus')
Check 'Dashboard UI' 'Old-style information density includes world backup mods players guilds resources and logs' ($ui -match 'ACTIVE WORLD' -and $ui -match 'BACKUP' -and $ui -match 'MOD PLATFORM' -and $ui -match 'PLAYERS' -and $ui -match 'GUILDS &amp; IDENTITIES' -and $ui -match 'LIVE SERVER / MANAGER LOG')
Check 'Dashboard UI' 'Operational quick actions bind real commands' ($ui -match 'Command="\{Binding StartCommand\}"' -and $ui -match 'Command="\{Binding RestartCommand\}"' -and $ui -match 'Command="\{Binding StopCommand\}"' -and $ui -match 'Command="\{Binding CreateBackupCommand\}"')
Check 'Dashboard UI' 'Leading placeholder StringFormat values are Avalonia-escaped' (
    $ui -notmatch "StringFormat='\{0\}" -and
    $ui -match "StringFormat='\{\}\{0\} player save record\(s\)'" -and
    $ui -match "StringFormat='\{\}\{0\} player record\(s\)'"
)
Check 'Placeholder Policy' 'Dashboard contains no fabricated sample player/world/server values' ($ui -notmatch 'Player2|Shadow|Nova|Day 347|18%|6\.2 GB')

Check 'Build Quality' 'Desktop warnings remain promotion-blocking' ($desktopProject -match '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>')
Check 'Build Gate' 'Root Build.ps1 exposes LogicTests action' ($build -match "'LogicTests'")
Check 'Build Gate' 'Release/All invokes current version logic harness' ($build -match 'Test-v\$v-Logic\.ps1')
Check 'Linux Packaging' 'Current Linux acceptance script exists' (Test-Path (Join-Path $root 'scripts\Test-v0.4.3.1-LinuxAcceptance.sh'))
Check 'Linux Packaging' 'Current production readiness script exists' (Test-Path (Join-Path $root 'scripts\Test-v0.4.3.1-ProductionReadiness.sh'))
Check 'Release Packaging' 'Changed-files apply instructions exist' (Test-Path (Join-Path $root 'release-notes\APPLY_v0.4.3.1_CHANGED_FILES.md'))
Check 'Release Packaging' 'v0.4.3.1 release notes exist' (Test-Path (Join-Path $root 'release-notes\v0.4.3.1.md'))
Check 'Release Packaging' 'v0.4.3.1 build/test plan exists' (Test-Path (Join-Path $root 'release-notes\BUILD_TEST_PLAN_v0.4.3.1.md'))
Check 'Roadmap' 'v0.4.3.1 is current dashboard/local backend candidate' ($road -match 'v0\.4\.0\.3.+Dashboard & Local Server Backend Integration \(Current Candidate\)')
Check 'Roadmap' 'Server/config/console integration remains next' ($road -match 'v0\.4\.0\.4.+Server / Configuration / Console / Workspace Integration')
Check 'Roadmap' 'Deferred save/freeze and MOD functional testing remain captured' ($road -match 'v0\.4\.0\.10.+Save / Client Freeze' -and $road -match 'v0\.4\.0\.13.+MOD Validation')

if($RunBuild){
 & (Join-Path $root 'Build.ps1') Validate; Check 'Build' 'Release validation' $true
 & (Join-Path $root 'Build.ps1') WindowsHeadless; Check 'Build' 'Windows persistent host publishes' $true
 & (Join-Path $root 'Build.ps1') DesktopWindows; Check 'Build' 'Windows Avalonia desktop publishes warning-free' $true
 & (Join-Path $root 'Build.ps1') LinuxHeadless; Check 'Build' 'Linux headless publishes' $true
 & (Join-Path $root 'Build.ps1') DesktopLinux; Check 'Build' 'Linux Avalonia desktop publishes warning-free' $true
}
$passed=@($checks|Where-Object Passed).Count
$failed=@($checks|Where-Object{-not $_.Passed}).Count
Write-Host "`n================ MystTiq v0.4.3.1 Summary ================"
Write-Host "Passed : $passed"
Write-Host "Failed : $failed"
if($ExportJson){$dir=Join-Path $root 'artifacts\logic-tests';New-Item $dir -ItemType Directory -Force|Out-Null;$path=Join-Path $dir "MystTiq_v0.4.3.1_$(Get-Date -Format yyyyMMdd-HHmmss).json";$checks|ConvertTo-Json -Depth 4|Set-Content $path;Write-Host "JSON report: $path"}
if($failed){exit 1}
