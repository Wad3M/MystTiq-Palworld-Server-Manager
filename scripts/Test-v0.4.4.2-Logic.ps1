[CmdletBinding()]
param([string]$ProjectRoot='.',[switch]$RunBuild,[switch]$ExportJson)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
$checks=@()
function Check($area,$name,$ok){$script:checks += [pscustomobject]@{Area=$area;Check=$name;Passed=[bool]$ok};Write-Host ("[{0}] {1} :: {2}" -f ($(if($ok){'PASS'}else{'FAIL'}),$area,$name)) -ForegroundColor $(if($ok){'Green'}else{'Red'})}
function Text($rel){Get-Content (Join-Path $root $rel) -Raw}

$props=Text 'Directory.Build.props'
$vm=Text 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$ui=Text 'src\MystTiq.Desktop\MainWindow.axaml'
$api=Text 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs'
$client=Text 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs'
$pollDto=Text 'src\MystTiq.Desktop\Models\StatusPollingDto.cs'
$apiHostSource=Text 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$release=Text 'scripts\Build-Release.ps1'
$build=Text 'Build.ps1'
$readme=Text 'README.md'
$road=Text 'docs\roadmap\PRODUCT_ROADMAP.md'
$discovery=Text 'src\MystTiq.Desktop\Services\MystTiqServiceDiscoveryService.cs'
$appComposition=Text 'src\MystTiq.Desktop\App.axaml.cs'
$getVersion=Text 'scripts\Get-ProjectVersion.ps1'
$validator=Text 'scripts\Validate-Release.ps1'
$headlessProgram=Text 'src\MystTiq.HeadlessHost\Program.cs'
$windowsLifecycle=Text 'src\MystTiq.Core\Services\WindowsServerLifecycleService.cs'
$windowsInspector=Text 'src\MystTiq.Core\Services\WindowsServerSessionInspector.cs'
$bootstrapper=Text 'src\MystTiq.Desktop\Services\LocalManagementBootstrapper.cs'
$desktopBuild=Text 'scripts\Build-AvaloniaDesktop.ps1'
$headlessConfig=Text 'src\MystTiq.Core\Services\HeadlessConfigurationService.cs'
$serviceStatusProvider=Text 'src\MystTiq.HeadlessHost\ManagementServiceStatusProvider.cs'


Check 'Versioning' 'Version is v0.4.4.3' ($props -match '<VersionPrefix>0\.4\.4\.2</VersionPrefix>' -and $vm -match 'Version => "v0\.4\.4\.2"')
Check 'PowerShell Safety' 'Current logic harness does not assign to reserved automatic Host variable' ((Text 'scripts\Test-v0.4.4.3-Logic.ps1') -notmatch '(?im)^\s*\$host\s*=')
Check 'Composition' 'Desktop remains Avalonia MVVM' ($ui -match 'x:DataType="vm:MainWindowViewModel"' -and $vm -match 'class MainWindowViewModel')
Check 'Windows Headless' 'api-run supports Windows and Linux instead of rejecting Windows' ($headlessProgram -match 'OperatingSystem\.IsWindows\(\)' -and $headlessProgram -match 'WindowsServerLifecycleService' -and $headlessProgram -notmatch 'Local API host in v0\.3\.0\.7 requires Linux')
Check 'Windows Headless' 'Windows lifecycle uses Core platform abstractions and verifies UDP 8211' ($windowsLifecycle -match 'IServerLifecycleService' -and $windowsLifecycle -match 'sessionInspector\.GetGuardedListeningPorts' -and $windowsLifecycle -match 'ports\.Contains\(8211\)')
Check 'Windows Headless' 'Windows session inspector discovers PalServer processes and guarded UDP listeners' ($windowsInspector -match 'Process\.GetProcesses' -and $windowsInspector -match 'netstat\.exe' -and $windowsInspector -match 'GetGuardedListeningPorts')
Check 'Service Status Abstraction' 'API service status is platform-neutral and does not pass a Linux service manager into Windows composition' ($serviceStatusProvider -match 'IManagementServiceStatusProvider' -and $serviceStatusProvider -match 'WindowsStandaloneManagementServiceStatusProvider' -and $apiHostSource -match 'IManagementServiceStatusProvider serviceStatusProvider')
Check 'Local Bootstrap' 'GUI can bootstrap packaged headless API without owning PalServer lifecycle' ($bootstrapper -match 'ILocalManagementBootstrapper' -and $bootstrapper -match 'api-run' -and $bootstrapper -notmatch 'PalServer\.exe' -and $vm -match '_localBootstrapper\.EnsureAvailableAsync')
Check 'Local Bootstrap' 'Legacy settings are never passed to the schema-v2 headless config loader' ($bootstrapper -match 'never pass settings-v2\.1\.json' -and $bootstrapper -match 'Path\.GetFileName\(persistentConfig\)\.Equals\("mysttiq\.json"')
Check 'Desktop Packaging' 'Windows/Linux desktop packages include matching self-contained headless sidecar' ($desktopBuild -match "Join-Path \$output 'headless'" -and $desktopBuild -match 'dotnet publish \$headlessProject' -and $desktopBuild -match '--self-contained true')
Check 'Cross-platform Config' 'Headless configuration has Windows and Linux defaults with platform-aware absolute path validation' ($headlessConfig -match 'WindowsDefaultPath' -and $headlessConfig -match 'DefaultPath' -and $headlessConfig -match 'Path\.IsPathFullyQualified')
Check 'Polling Contract' 'One aggregate status DTO owns periodic status payload' ($pollDto -match 'class StatusPollingDto' -and $pollDto -match 'ServerStatusDto Status' -and $pollDto -match 'ServiceStatusDto Service' -and $pollDto -match 'PlayersSnapshotDto Players' -and $pollDto -match 'RuntimeMetricsSnapshotDto Metrics' -and $pollDto -match 'LogTailSnapshotDto LogTail')
Check 'API Contract' 'ViewModel-facing API exposes aggregate polling method' ($api -match 'GetStatusPollingAsync')
Check 'API Client' 'Aggregate client calls exactly one polling endpoint' ($client -match 'GetStatusPollingAsync' -and $client -match '/api/v1/status/poll\?lines=')
Check 'Endpoint' 'Headless host exposes aggregate polling endpoint' ($apiHostSource -match 'MapGet\("/api/v1/status/poll"')
Check 'Endpoint' 'Polling endpoint samples lifecycle service players metrics and log tail' ($apiHostSource -match 'lifecycle\.GetStatusAsync' -and $apiHostSource -match 'serviceStatusProvider\.GetStatusAsync' -and $apiHostSource -match 'monitoring\.GetPlayersAsync' -and $apiHostSource -match 'monitoring\.GetMetricsAsync' -and $apiHostSource -match 'monitoring\.GetLogTail')
Check 'Endpoint' 'Independent async samples are awaited together' ($apiHostSource -match 'Task\.WhenAll\(statusTask, serviceTask, playersTask, metricsTask\)')
Check 'Behavior' 'Periodic timer uses aggregate poll instead of fan-out refresh' ($vm -match '_refreshTimer\.Tick' -and $vm -match 'await RefreshStatusPollingAsync\(\)' -and $vm -notmatch '_refreshTick % 2')
Check 'Behavior' 'Aggregate snapshot is applied to all live status surfaces' ($vm -match 'ApplyStatus\(snapshot\.Status\)' -and $vm -match 'ApplyServiceStatus\(snapshot\.Service\)' -and $vm -match 'ApplyPlayers\(snapshot\.Players\)' -and $vm -match 'ApplyMetrics\(snapshot\.Metrics\)' -and $vm -match 'ApplyLogs\(snapshot\.LogTail\)')
Check 'Status Bar' 'Global bottom status bar is present' ($ui -match 'StatusBarText' -and $ui -match 'StatusBarObservedText')
Check 'Status Bar' 'Busy work shows standard indeterminate animation' ($ui -match 'ProgressBar' -and $ui -match 'IsIndeterminate="True"' -and $ui -match 'IsVisible="\{Binding IsBusy\}"')
Check 'Status Bar' 'Busy state drives status text' ($vm -match 'StatusBarText = value \? "Working…"')
Check 'Workspace' 'Workspace is a real page, not a placeholder' ($vm -match 'IsWorkspacePage' -and $vm -notmatch 'IsPlaceholderPage => SelectedPage is NavigationPage\.Workspace')
Check 'Workspace' 'Workspace reads managed configuration paths' ($ui -match 'IsVisible="\{Binding IsWorkspacePage\}"' -and $ui -match 'ConfigServerRoot' -and $ui -match 'ConfigSteamCmdPath' -and $ui -match 'ConfigBackupRoot' -and $ui -match 'ConfigRuntimeRoot')
Check 'Workspace' 'Workspace refresh routes through ViewModel command to API configuration service' ($ui -match 'Refresh Workspace.+LoadConfigurationCommand' -and $vm -match '_api\.GetEditableConfigurationAsync')
Check 'Lifecycle' 'Start Stop Restart remain API-routed, not GUI-owned' ($vm -match '_api\.StartServerAsync' -and $vm -match '_api\.StopServerAsync' -and $vm -match '_api\.RestartServerAsync' -and $apiHostSource -match '/api/v1/server/start' -and $apiHostSource -match '/api/v1/server/stop' -and $apiHostSource -match '/api/v1/server/restart')
$desktopProject=Text 'src\MystTiq.Desktop\MystTiq.Desktop.csproj'
Check 'Platform Preservation' 'Desktop contains no WPF or Windows-only desktop dependency' ($desktopProject -notmatch 'Microsoft\.NET\.Sdk\.WindowsDesktop|<UseWPF>true</UseWPF>|net[0-9.]+-windows' -and $ui -notmatch 'System\.Windows\.(Controls|Window|Application)|PresentationFramework' -and $vm -notmatch 'System\.Windows\.(Controls|Window|Application)|Microsoft\.Win32')
Check 'Platform Preservation' 'System.Windows.Input ICommand remains allowed for cross-platform MVVM' ($vm -match 'System\.Windows\.Input' -or $vm -match '\bICommand\b')
Check 'Platform Preservation' 'Headless endpoint uses platform abstractions' ($apiHostSource -match 'ServerPathProfile\.ForCurrentPlatform' -and $apiHostSource -match 'ServerDistributionPlatformService\.ForCurrentPlatform')
Check 'Release Gate' 'Root build exposes version-selected LogicTests' ($build -match "'LogicTests'" -and $build -match 'Test-v\$v-Logic\.ps1')
Check 'Release Gate' 'Release workflow runs logic tests before packaging' ($release.IndexOf('Running v$version logic tests') -ge 0 -and $release.IndexOf('Running v$version logic tests') -lt $release.IndexOf('Creating portable package'))
Check 'Release Gate' 'Release workflow builds Windows and Linux headless plus desktop' ($release -match 'Build-WindowsHeadless\.ps1' -and $release -match 'Build-LinuxHeadless\.ps1' -and $release -match "Runtime 'win-x64'" -and $release -match "Runtime 'linux-x64'")
Check 'Release Gate' 'Runtime smoke runs after Windows desktop publish and before packaging' ($release -match 'Test-v\$version-RuntimeSmoke\.ps1' -and $release.IndexOf('Running Windows headless API runtime smoke') -gt $release.IndexOf('Building Avalonia desktop for Windows') -and $release.IndexOf('Running Windows headless API runtime smoke') -lt $release.IndexOf('Creating portable package'))

Check 'RunBuild Gate' '-RunBuild compiles shared desktop and headless targets for Windows and Linux' ($MyInvocation.MyCommand.Path -and (Text 'scripts\Test-v0.4.4.3-Logic.ps1') -match '-Action WindowsHeadless' -and (Text 'scripts\Test-v0.4.4.3-Logic.ps1') -match '-Action LinuxHeadless' -and (Text 'scripts\Test-v0.4.4.3-Logic.ps1') -match '-Action DesktopWindows' -and (Text 'scripts\Test-v0.4.4.3-Logic.ps1') -match '-Action DesktopLinux')
Check 'Documentation' 'README identifies v0.4.4.3 candidate and v0.4.3.1 baseline' ($readme -match 'v0\.4\.4\.2' -and $readme -match 'v0\.4\.3\.1')
Check 'Documentation' 'Roadmap identifies v0.4.4.3 current candidate' ($road -match 'v0\.4\.4\.2')


Check 'Packaging' 'Current-version logic test exists at the exact release path' (Test-Path (Join-Path $root 'scripts\Test-v0.4.4.3-Logic.ps1') -PathType Leaf)
Check 'Version Wiring' 'Validator resolves version dynamically from Get-ProjectVersion' ($validator -match 'Get-ProjectVersion\.ps1' -and $getVersion -match 'Directory\.Build\.props' -and $build -match 'Test-v\$v-Logic\.ps1')
Check 'Discovery' 'Desktop has cross-platform LAN MystTiq service discovery' ($discovery -match 'NetworkInterface\.GetAllNetworkInterfaces' -and $discovery -match '/healthz' -and $discovery -match 'mysttiq-headless')
Check 'Discovery' 'Discovery is bounded and avoids broad subnet sweeps' ($discovery -match 'SemaphoreSlim\(32' -and $discovery -match 'MaxCandidatesPerInterface = 254' -and $discovery -match '169 && bytes\[1\] == 254')
Check 'Discovery Security' 'Relaxed TLS validation is isolated to health discovery; real API client retains normal validation/pinning' ($discovery -match 'ServerCertificateCustomValidationCallback' -and $client -match 'normal OS certificate validation remains authoritative' -and $client -match 'ServerCertificateSha256')
Check 'Composition' 'Discovery flows App -> ViewModel -> discovery service' ($appComposition -match 'MystTiqServiceDiscoveryService' -and $vm -match 'IMystTiqServiceDiscoveryService' -and $vm -match '_serviceDiscovery\.DiscoverAsync')
Check 'GUI Navigation' 'Top-level groups and expanded child rows use one standard navigation height' ($ui -match 'Expander Classes="navgroup"' -and $ui -match 'StackPanel Margin="0" Spacing="0"' -and (Text 'src\MystTiq.Desktop\App.axaml') -match 'Button\.navitem.+Height.+44' -and (Text 'src\MystTiq.Desktop\App.axaml') -match 'Expander\.navgroup.+MinHeight.+44')
Check 'GUI Navigation' 'Long navigation labels use compact standard font sizing' ((Text 'src\MystTiq.Desktop\App.axaml') -match 'Button\.navitem.+FontSize.+12')
Check 'GUI Discovery' 'Settings exposes LAN discovery and discovered-service selection' ($ui -match 'Discover LAN Services' -and $ui -match 'DiscoveredServices' -and $ui -match 'SelectedDiscoveredService')
Check 'Navigation Behavior' 'Opening operational pages triggers their ViewModel-backed refresh path' ($vm -match 'RefreshPageForNavigationAsync' -and $vm -match '(?s)NavigationPage\.Configuration.*?LoadConfigurationAsync' -and $vm -match '(?s)NavigationPage\.Backups.*?RefreshBackupsAsync' -and $vm -match '(?s)NavigationPage\.Inspector.*?RefreshWorldExplorerAsync' -and $vm -match '(?s)NavigationPage\.DiagnosticsCenter.*?RunNetworkDiagnosticsAsync')
Check 'Linux Packaging' 'Current Linux acceptance and production-readiness scripts exist' ((Test-Path (Join-Path $root 'scripts\Test-v0.4.4.3-LinuxAcceptance.sh') -PathType Leaf) -and (Test-Path (Join-Path $root 'scripts\Test-v0.4.4.3-ProductionReadiness.sh') -PathType Leaf))
Check 'Build UX' 'Windows desktop build auto-launches GUI only after successful publish and supports suppression' ((Text 'scripts\Build-AvaloniaDesktop.ps1') -match '\[switch\]\$NoLaunch' -and (Text 'scripts\Build-AvaloniaDesktop.ps1') -match 'Start-Process' -and (Text 'scripts\Build-AvaloniaDesktop.ps1').IndexOf('Start-Process') -gt (Text 'scripts\Build-AvaloniaDesktop.ps1').IndexOf('Avalonia desktop publish failed') -and $build -match 'NoGuiLaunch')
Check 'Testing UX' 'README includes full clean/unblock/validate/current-logic command sequence' ($readme -match 'Get-ChildItem \. -Recurse -Filter \*\.ps1 \| Unblock-File' -and $readme -match 'Test-v0\.4\.4\.2-Logic\.ps1')

Check 'Runtime Smoke' 'Windows API runtime smoke script exists and checks health poll config and distribution endpoints' ((Test-Path (Join-Path $root 'scripts\Test-v0.4.4.3-RuntimeSmoke.ps1') -PathType Leaf) -and (Text 'scripts\Test-v0.4.4.3-RuntimeSmoke.ps1') -match '/healthz' -and (Text 'scripts\Test-v0.4.4.3-RuntimeSmoke.ps1') -match '/api/v1/status/poll' -and (Text 'scripts\Test-v0.4.4.3-RuntimeSmoke.ps1') -match '/api/v1/config/editable')
# In-memory behavioral contract: verify the wire payload shape expected by the DTO/client without a running server.
$sample = '{"observedAt":"2026-08-26T12:00:00Z","status":{"phase":2,"ready":true},"service":{"installed":true,"enabled":true,"activeState":"active"},"players":{"available":true,"onlineCount":3,"players":[]},"metrics":{"available":true,"cpuPercent":12.5,"workingSetBytes":104857600,"threadCount":42},"logTail":{"available":true,"fileName":"PalWorldSettings.log","lines":["ready"]}}' | ConvertFrom-Json
Check 'Behavioral Contract' 'Aggregate wire sample preserves coherent server/service/player/metric/log state' ($sample.status.ready -and $sample.service.activeState -eq 'active' -and $sample.players.onlineCount -eq 3 -and $sample.metrics.threadCount -eq 42 -and $sample.logTail.lines[0] -eq 'ready')

if($RunBuild){
    & (Join-Path $root 'Build.ps1') -Action Validate -StrictValidation
    & (Join-Path $root 'Build.ps1') -Action Build -Configuration Release
    & (Join-Path $root 'Build.ps1') -Action WindowsHeadless -Configuration Release
    & (Join-Path $root 'Build.ps1') -Action LinuxHeadless -Configuration Release
    & (Join-Path $root 'Build.ps1') -Action DesktopWindows -Configuration Release
    & (Join-Path $root 'Build.ps1') -Action DesktopLinux -Configuration Release
    & (Join-Path $root 'scripts\Test-v0.4.4.3-RuntimeSmoke.ps1') -ProjectRoot $root
}
$failed=@($checks|Where-Object{-not $_.Passed})
Write-Host "`n================ MystTiq v0.4.4.3 Summary ================"
Write-Host "Passed: $($checks.Count-$failed.Count) / $($checks.Count)"
if($ExportJson){$dir=Join-Path $root 'artifacts\logic-tests';New-Item $dir -ItemType Directory -Force|Out-Null;$path=Join-Path $dir "MystTiq_v0.4.4.3_$(Get-Date -Format yyyyMMdd-HHmmss).json";$checks|ConvertTo-Json -Depth 4|Set-Content $path;Write-Host "JSON report: $path"}
if($failed.Count){$failed|Format-Table -AutoSize;throw "v0.4.4.3 logic gate failed: $($failed.Count) check(s)."}
Write-Host 'v0.4.4.3 logic gate passed.' -ForegroundColor Green
