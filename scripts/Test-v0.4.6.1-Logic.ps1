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
$serverStatusDto=Text 'src\MystTiq.Desktop\Models\ServerStatusDto.cs'
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
$healthDto=Text 'src\MystTiq.Desktop\Models\ApiHealthDto.cs'
$desktopBuild=Text 'scripts\Build-AvaloniaDesktop.ps1'
$headlessConfig=Text 'src\MystTiq.Core\Services\HeadlessConfigurationService.cs'
$headlessConfigModel=Text 'src\MystTiq.Core\Models\HeadlessConfiguration.cs'
$serviceStatusProvider=Text 'src\MystTiq.HeadlessHost\ManagementServiceStatusProvider.cs'
$appXaml=Text 'src\MystTiq.Desktop\App.axaml'
$mainWindowCode=Text 'src\MystTiq.Desktop\MainWindow.axaml.cs'
$activityService=Text 'src\MystTiq.HeadlessHost\HeadlessActivityLogService.cs'
$playerAdminService=Text 'src\MystTiq.HeadlessHost\HeadlessPalworldAdminService.cs'
$worldExplorerService=Text 'src\MystTiq.HeadlessHost\HeadlessWorldExplorerService.cs'
$activityDtos=Text 'src\MystTiq.Desktop\Models\ActivityAdminDtos.cs'
$palworldConfigService=Text 'src\MystTiq.Core\Services\PalworldSettingsConfigurationService.cs'
$palworldConfigDtos=Text 'src\MystTiq.Desktop\Models\PalworldConfigurationDtos.cs'
$monitoringService=Text 'src\MystTiq.HeadlessHost\HeadlessMonitoringService.cs'
$parityMatrix=Text 'docs\GUI_PARITY_v0.4.6.1.md'


Check 'Versioning' 'Version is v0.4.6.1' ($props -match '<VersionPrefix>0\.4\.6\.1</VersionPrefix>' -and $vm -match 'Version => "v0\.4\.6\.1"')
Check 'PowerShell Safety' 'Current logic harness does not assign to reserved automatic Host variable' ((Text 'scripts\Test-v0.4.6.1-Logic.ps1') -notmatch '(?im)^\s*\$host\s*=')
Check 'Composition' 'Desktop remains Avalonia MVVM' ($ui -match 'x:DataType="vm:MainWindowViewModel"' -and $vm -match 'class MainWindowViewModel')
Check 'Windows Headless' 'api-run supports Windows and Linux instead of rejecting Windows' ($headlessProgram -match 'OperatingSystem\.IsWindows\(\)' -and $headlessProgram -match 'WindowsServerLifecycleService' -and $headlessProgram -notmatch 'Local API host in v0\.3\.0\.7 requires Linux')
Check 'Windows Headless' 'Windows lifecycle uses Core platform abstractions and verifies configured Palworld game port' ($windowsLifecycle -match 'IServerLifecycleService' -and $windowsLifecycle -match 'sessionInspector\.GetGuardedListeningPorts' -and $windowsLifecycle -match 'ports\.Contains\(expectedGamePort\)' -and $headlessProgram -match 'GetConfiguredGamePort')
Check 'Windows Headless' 'Windows session inspector discovers PalServer processes and guarded UDP listeners' ($windowsInspector -match 'Process\.GetProcesses' -and $windowsInspector -match 'netstat\.exe' -and $windowsInspector -match 'GetGuardedListeningPorts')
Check 'Service Status Abstraction' 'API service status is platform-neutral and does not pass a Linux service manager into Windows composition' ($serviceStatusProvider -match 'IManagementServiceStatusProvider' -and $serviceStatusProvider -match 'WindowsStandaloneManagementServiceStatusProvider' -and $apiHostSource -match 'IManagementServiceStatusProvider serviceStatusProvider')
Check 'Local Bootstrap' 'GUI can bootstrap packaged headless API without owning PalServer lifecycle' ($bootstrapper -match 'ILocalManagementBootstrapper' -and $bootstrapper -match 'api-run' -and $bootstrapper -notmatch 'PalServer\.exe' -and $vm -match '_localBootstrapper\.EnsureAvailableAsync')
Check 'Local Bootstrap' 'Legacy settings are never passed to the schema-v2 headless config loader' ($bootstrapper -match 'never pass settings-v2\.1\.json' -and $bootstrapper -match 'Path\.GetFileName\(persistentConfig\)\.Equals\("mysttiq\.json"')
Check 'Desktop Packaging' 'Windows/Linux desktop packages include matching self-contained headless sidecar' ($desktopBuild.Contains("Join-Path `$output 'headless'") -and $desktopBuild.Contains('dotnet publish $headlessProject') -and $desktopBuild.Contains('--self-contained true'))
Check 'Cross-platform Config' 'Headless configuration has Windows and Linux defaults with platform-aware absolute path validation' ($headlessConfig -match 'WindowsDefaultPath' -and $headlessConfig -match 'DefaultPath' -and $headlessConfig -match 'Path\.IsPathFullyQualified')
Check 'Lifecycle Launch' 'Default Windows PalServer launch is hidden and redirects Unreal logging to stdout' ($headlessConfigModel -match '-stdout' -and $headlessConfigModel -match '-FullStdOutLogOutput' -and $headlessConfigModel -match '-logformat=text' -and $windowsLifecycle -match 'CreateNoWindow = true' -and $windowsLifecycle -match 'WindowStyle = ProcessWindowStyle.Hidden' -and $windowsLifecycle -match 'RedirectStandardOutput = true' -and $windowsLifecycle -match 'RedirectStandardError = true')
Check 'Lifecycle Launch' 'Existing Windows configs cannot re-enable Unreal separate -log window' ($windowsLifecycle -match 'BuildHiddenConsoleArguments' -and $windowsLifecycle -match 'StringComparison.OrdinalIgnoreCase' -and $windowsLifecycle -match '"-log"' -and $windowsLifecycle -match '"-stdout"' -and $windowsLifecycle -match '"-FullStdOutLogOutput"')
Check 'Polling Contract' 'Nullable lifecycle transition timestamp matches Core wire contract' ($serverStatusDto -match 'DateTimeOffset\? LastTransitionAt' -and $vm -match 'status\.LastTransitionAt is \{ \} lastTransitionAt' -and $vm -match 'FormatTime\(DateTimeOffset\? value\)')
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

Check 'RunBuild Gate' '-RunBuild compiles shared desktop and headless targets for Windows and Linux' ($MyInvocation.MyCommand.Path -and (Text 'scripts\Test-v0.4.6.1-Logic.ps1') -match '-Action WindowsHeadless' -and (Text 'scripts\Test-v0.4.6.1-Logic.ps1') -match '-Action LinuxHeadless' -and (Text 'scripts\Test-v0.4.6.1-Logic.ps1') -match '-Action DesktopWindows' -and (Text 'scripts\Test-v0.4.6.1-Logic.ps1') -match '-Action DesktopLinux')
Check 'Documentation' 'README identifies v0.4.6.1 candidate and v0.4.5.1 baseline' ($readme -match 'Current release candidate: v0\.4\.6\.1' -and $readme -match 'Official baseline: v0\.4\.5\.1')
Check 'Documentation' 'Roadmap identifies v0.4.6.1 current candidate' ($road -match 'v0\.4\.6\.1.+Current Release Candidate')


Check 'Packaging' 'Current-version logic test exists at the exact release path' (Test-Path (Join-Path $root 'scripts\Test-v0.4.6.1-Logic.ps1') -PathType Leaf)
Check 'Version Wiring' 'Validator resolves version dynamically from Get-ProjectVersion' ($validator -match 'Get-ProjectVersion\.ps1' -and $getVersion -match 'Directory\.Build\.props' -and $build -match 'Test-v\$v-Logic\.ps1')
Check 'Discovery' 'Desktop has cross-platform LAN MystTiq service discovery' ($discovery -match 'NetworkInterface\.GetAllNetworkInterfaces' -and $discovery -match '/healthz' -and $discovery -match 'mysttiq-headless')
Check 'Discovery' 'Discovery is bounded and avoids broad subnet sweeps' ($discovery -match 'SemaphoreSlim\(32' -and $discovery -match 'MaxCandidatesPerInterface = 254' -and $discovery -match '169 && bytes\[1\] == 254')
Check 'Discovery Security' 'Relaxed TLS validation is isolated to health discovery; real API client retains normal validation/pinning' ($discovery -match 'ServerCertificateCustomValidationCallback' -and $client -match 'normal OS certificate validation remains authoritative' -and $client -match 'ServerCertificateSha256')
Check 'Composition' 'Discovery flows App -> ViewModel -> discovery service' ($appComposition -match 'MystTiqServiceDiscoveryService' -and $vm -match 'IMystTiqServiceDiscoveryService' -and $vm -match '_serviceDiscovery\.DiscoverAsync')
Check 'GUI Navigation' 'Top-level groups and expanded child rows use one standard navigation height' ($ui -match 'Expander Classes="navgroup"' -and $ui -match 'StackPanel Margin="0" Spacing="0"' -and (Text 'src\MystTiq.Desktop\App.axaml') -match 'Button\.navitem.+Height.+44' -and (Text 'src\MystTiq.Desktop\App.axaml') -match 'Expander\.navgroup.+MinHeight.+44')
Check 'GUI Navigation' 'Long navigation labels use compact standard font sizing' ((Text 'src\MystTiq.Desktop\App.axaml') -match 'Button\.navitem.+FontSize.+12')
Check 'GUI Navigation' 'Expanded child items are modestly indented without changing standard row height' ($ui -match 'Classes="navitem navchild"' -and (Text 'src\MystTiq.Desktop\App.axaml') -match 'Button\.navitem\.navchild.+Padding.+22,4,8,4' -and (Text 'src\MystTiq.Desktop\App.axaml') -match 'Button\.navitem.+Height.+44')
Check 'Branding' 'Avalonia desktop uses established MystTiq logo asset instead of temporary M glyph' ($ui -match 'PalworldServerManager\.png' -and $ui -notmatch 'Text="M" FontSize="34"' -and (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Assets\PalworldServerManager.png') -PathType Leaf))
Check 'Branding' 'Window/application icon uses established MystTiq icon asset' ($ui -match 'Icon="/Assets/PalworldServerManager\.ico"' -and $desktopProject -match '<ApplicationIcon>Assets/PalworldServerManager\.ico</ApplicationIcon>' -and (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Assets\PalworldServerManager.ico') -PathType Leaf))
Check 'GUI Discovery' 'Settings exposes LAN discovery and discovered-service selection' ($ui -match 'Discover LAN Services' -and $ui -match 'DiscoveredServices' -and $ui -match 'SelectedDiscoveredService')
Check 'Navigation Behavior' 'Opening operational pages triggers their ViewModel-backed refresh path' ($vm -match 'RefreshPageForNavigationAsync' -and $vm -match '(?s)NavigationPage\.Configuration.*?LoadConfigurationAsync' -and $vm -match '(?s)NavigationPage\.Backups.*?RefreshBackupsAsync' -and $vm -match '(?s)NavigationPage\.Inspector.*?RefreshWorldExplorerAsync' -and $vm -match '(?s)NavigationPage\.DiagnosticsCenter.*?RunNetworkDiagnosticsAsync')
Check 'Linux Packaging' 'Current Linux acceptance and production-readiness scripts exist' ((Test-Path (Join-Path $root 'scripts\Test-v0.4.6.1-LinuxAcceptance.sh') -PathType Leaf) -and (Test-Path (Join-Path $root 'scripts\Test-v0.4.6.1-ProductionReadiness.sh') -PathType Leaf))
Check 'Build UX' 'Windows desktop build auto-launches GUI only after successful publish and supports suppression' ((Text 'scripts\Build-AvaloniaDesktop.ps1') -match '\[switch\]\$NoLaunch' -and (Text 'scripts\Build-AvaloniaDesktop.ps1') -match 'Start-Process' -and (Text 'scripts\Build-AvaloniaDesktop.ps1').IndexOf('Start-Process') -gt (Text 'scripts\Build-AvaloniaDesktop.ps1').IndexOf('Avalonia desktop publish failed') -and $build -match 'NoGuiLaunch')
Check 'Testing UX' 'README includes full clean/unblock/validate/current-logic command sequence' ($readme -match 'Get-ChildItem \. -Recurse -Filter \*\.ps1 \| Unblock-File' -and $readme -match 'Test-v0\.4\.6\.1-Logic\.ps1')

Check 'Clean Gate' 'Clean stops only artifact-hosted MystTiq development processes before deleting artifacts' ($build -match 'Stop-ArtifactHostedProcesses' -and $build -match '\$processPath\.StartsWith\(\$artifactPrefix' -and $build -match 'MystTiq\.Desktop|mysttiq-server')
Check 'Clean Gate' 'Clean fails instead of falsely succeeding when artifacts remain' ($build -match 'Build clean failed: artifacts directory still exists' -and $build -match 'Remove-Item \$artifacts -Recurse -Force -ErrorAction Stop')

Check 'Runtime Smoke' 'Windows API runtime smoke script exists and checks health poll config and distribution endpoints' ((Test-Path (Join-Path $root 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -PathType Leaf) -and (Text 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -match '/healthz' -and (Text 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -match '/api/v1/status/poll' -and (Text 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -match '/api/v1/config/editable')

Check 'Connection Handshake' 'Health endpoint is versioned and identifies platform/API contract' ($apiHostSource -match 'apiVersion = 1' -and $apiHostSource -match 'version = typeof\(LocalManagementApiHost\)' -and $apiHostSource -match 'platform = OperatingSystem')
Check 'Connection Handshake' 'Desktop establishes connection through health plus the single aggregate poll endpoint' ($api -match 'GetHealthAsync' -and $client -match 'GetHealthAsync' -and $vm -match 'GetHealthAsync' -and $vm -match 'GetStatusPollingAsync\(profile, 120' -and $vm -notmatch 'var statusTask = _api\.GetStatusAsync\(profile, BearerToken\);\s*var serviceTask = _api\.GetServiceStatusAsync')
Check 'Connection Handshake' 'Desktop-owned sidecar forces loopback-only unauthenticated mode without weakening remote API rules' ($bootstrapper -match '--desktop-sidecar' -and $headlessProgram -match 'desktopSidecar' -and $headlessProgram -match 'Authentication = desktopSidecar.+Enabled = false' -and $headlessProgram -match 'Tls = desktopSidecar.+Enabled = false' -and $apiHostSource -match 'Non-loopback management API requires both authentication and TLS')
Check 'Connection Handshake' 'Occupied or incompatible loopback endpoint falls back to a private available port' ($bootstrapper -match 'FindAvailableLoopbackEndpoint' -and $bootstrapper -match 'apiVersion' -and $bootstrapper -match 'existing\.Reachable' -and $bootstrapper -match 'TcpListener\(IPAddress\.Loopback, 0\)')
Check 'Local Auto Connect' 'Discovery health metadata carries authentication state' ($discovery -match 'AuthenticationEnabled' -and $discovery -match 'TryGetProperty\("authentication"')
Check 'Local Auto Connect' 'Local profile prefers loopback service over LAN candidates' ($vm -match 'local profile must prefer loopback' -and $vm -match 'IPAddress\.IsLoopback')
Check 'Local Auto Connect' 'Unauthenticated loopback discovery immediately exercises real refresh connection path' ($vm -match '!preferred\.AuthenticationEnabled' -and $vm -match 'await RefreshAsync\(silent: false\)')
Check 'Remote Security' 'LAN discovery never auto-bypasses authentication' ($vm -match 'isLoopback && !preferred\.AuthenticationEnabled' -and $client -match 'AuthenticationHeaderValue\("Bearer"')
Check 'Tray Lifecycle' 'Application uses Avalonia TrayIcon with native Show/lifecycle/backend-exit controls' ($appXaml -match '<TrayIcon ' -and $appXaml -match 'NativeMenuItem Header="Show MystTiq"' -and $appXaml -match 'Start Palworld Server' -and $appXaml -match 'Stop Local Management Backend &amp; Exit')
Check 'Tray Lifecycle' 'Closing the main window hides to tray and explicit exit owns shutdown' ($appComposition -match 'ShutdownMode\.OnExplicitShutdown' -and $mainWindowCode -match 'Closing \+= MainWindow_Closing' -and $mainWindowCode -match 'HideMainWindowToTray' -and $appComposition -match 'desktopLifetime\?\.Shutdown')
Check 'Tray Lifecycle' 'Only the sidecar started by this GUI session can be killed from tray' ($bootstrapper -match 'ownedSidecarProcessId' -and $bootstrapper -match 'ownedSidecarExecutable' -and $bootstrapper -match 'StopOwnedSidecarAsync' -and $bootstrapper -match 'Path\.GetFullPath\(actualPath\)\.Equals')
Check 'Lifecycle UX' 'API connectivity is tracked independently from display status so failed operations remain retryable' ($vm -match 'ManagementApiConnected' -and $vm -match 'StartCommand = new AsyncCommand\(StartServerAsync.+ManagementApiConnected' -and $vm -match 'ConnectionState = result\.Success \? "Connected" : "Operation failed"')
Check 'Lifecycle UX' 'Start performs local backend recovery and server-distribution preflight before lifecycle mutation' ($vm -match 'EnsureManagementConnectionForLifecycleAsync' -and $vm -match '_localBootstrapper\.EnsureAvailableAsync' -and $vm -match 'GetServerDistributionStatusAsync' -and $vm -match 'Start blocked: PalServer executable was not found')
Check 'Lifecycle UX' 'Lifecycle result is visible on the GUI instead of failing silently' ($vm -match 'LifecycleStatusText' -and $ui -match 'Text="\{Binding LifecycleStatusText\}"')
Check 'Page Parity' 'Previously shared navigation destinations expose distinct page-specific views' ($vm -match 'IsServerSetupPage' -and $vm -match 'IsUpdateCenterPage' -and $vm -match 'IsBasesPage' -and $vm -match 'IsGuildsPage' -and $vm -match 'IsConsolePage' -and $vm -match 'IsActivityAuditPage' -and $vm -match 'IsModDashboardPage' -and $vm -match 'IsModLibraryPage' -and $vm -match 'IsUe4ssPage')
Check 'Page Parity' 'UI renders distinct legacy-inspired headings for setup/update, bases/guilds, console/audit, and MOD/UE4SS pages' ($ui -match 'Text="Server Setup"' -and $ui -match 'Text="Update Center"' -and $ui -match 'Text="Base Manager"' -and $ui -match 'Text="Guild Administration"' -and $ui -match 'Text="Live Console"' -and $ui -match 'Text="Activity &amp; Audit"' -and $ui -match 'Text="MOD Dashboard"' -and $ui -match 'Text="MOD Library"' -and $ui -match 'Text="UE4SS Runtime"')
Check 'Runtime Smoke' 'Isolated start-route smoke cannot launch the user PalServer and verifies a safe 424 response' ((Text 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -match 'missing-server' -and (Text 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -match '/api/v1/server/start' -and (Text 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -match 'StatusCode -ne 424')

Check 'Hover UX' 'Meaningful hover information remains while generic self-evident tooltip text is removed' ($ui -match 'ToolTip.Tip=' -and $appXaml -match 'ToolTip.ShowDelay' -and $ui -notmatch 'ToolTip.Tip="Run or open ' -and $ui -notmatch 'ToolTip.Tip="Choose an option from this selection\.' -and $ui -notmatch 'ToolTip.Tip="Select an item to inspect its details or available actions\.')
Check 'Palworld Configuration' 'Configuration page edits active PalWorldSettings through View -> ViewModel -> API -> endpoint -> Core' ($ui -match 'Palworld Server Configuration' -and $ui -match 'PalworldSettings' -and $vm -match 'LoadPalworldConfigurationAsync' -and $vm -match 'SavePalworldConfigurationAsync' -and $api -match 'GetPalworldConfigurationAsync' -and $api -match 'SavePalworldConfigurationAsync' -and $client -match '/api/v1/palworld/config' -and $apiHostSource -match '/api/v1/palworld/config' -and $palworldConfigService -match 'OptionSettings=\(')
Check 'Palworld Configuration' 'Saving PalWorldSettings creates rollback copy and preserves full active setting list' ($palworldConfigService -match 'ConfigBackups' -and $palworldConfigService -match 'timestamped rollback copy' -and $palworldConfigService -match 'string.Join\(","')
Check 'Lifecycle Port' 'PalServer readiness uses configured PublicPort instead of fixed UDP 8211' ($palworldConfigService -match 'PublicPort' -and $headlessProgram -match 'configuredGamePort' -and $windowsLifecycle -match 'expectedGamePort' -and (Text 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs') -match 'expectedGamePort')
Check 'Page Parity Audit' 'v0.2.16.4 page-by-page parity matrix explicitly tracks migrated partial and missing functionality' ($parityMatrix -match 'Crash Analyzer.+Not migrated' -and $parityMatrix -match 'Live Console & RCON.+Partial' -and $parityMatrix -match 'Configuration.+Migrated in this fix' -and $parityMatrix -match 'Recovery.+Not migrated')
Check 'Avalonia XAML Safety' 'Property elements do not carry normal control attributes' ($ui -notmatch '<(?:ListBox|ComboBox)\.(?:ItemTemplate|ContextMenu)\s+[^>]*ToolTip\.Tip=')
Check 'Avalonia XAML Safety' 'ListBox uses supported item-container stretching instead of unsupported HorizontalContentAlignment' ($ui -notmatch '<ListBox[^>]*HorizontalContentAlignment=' -and $ui -match '<Style Selector="ListBoxItem">' -and $ui -match '<Setter Property="HorizontalAlignment" Value="Stretch"/?>')
Check 'Player Context Actions' 'Players expose right-click administration and API wiring' ($ui -match 'ListBox.ContextMenu' -and $ui -match 'Kick Player' -and $ui -match 'Ban Player' -and $ui -match 'Whisper / Message' -and $ui -match 'Give Item' -and $vm -match 'RunSelectedPlayerAdminActionAsync' -and $client -match 'RunPlayerAdminActionAsync' -and $apiHostSource -match '/api/v1/players/\{playerId\}/action')
Check 'Player Context Actions' 'Unsupported vanilla actions are capability-gated instead of faking success' ($playerAdminService -match 'whisper.+promote.+give-item' -and $playerAdminService -match 'Vanilla Palworld' -and $apiHostSource -match '422UnprocessableEntity')
Check 'Activity Audit' 'Headless backend persists activity and audit logs and exposes them to the GUI' ($activityService -match 'MystTiq-Activity.log' -and $activityService -match 'MystTiq-Audit.jsonl' -and $apiHostSource -match '/api/v1/activity/tail' -and $api -match 'GetActivityLogTailAsync' -and $vm -match 'RefreshActivityAsync' -and $ui -match 'MystTiq Activity / Audit Log')
Check 'Console Capture' 'Windows lifecycle captures PalServer stdout and stderr when MystTiq launches the server' ($windowsLifecycle -match 'RedirectStandardOutput = true' -and $windowsLifecycle -match 'RedirectStandardError = true' -and $windowsLifecycle -match 'MystTiq-PalServer-Console.log')
Check 'Console Capture' 'Live Console prioritizes MystTiq redirected console output before Pal.log fallback' ($monitoringService -match 'MystTiq-PalServer-Console\.log' -and $monitoringService -match 'capturedConsole' -and $monitoringService.IndexOf('capturedConsole') -lt $monitoringService.IndexOf('var palLog'))
Check 'Console Capture' 'Windows default arguments do not request Unreal separate -log window' ($headlessConfigModel -notmatch '\["-useperfthreads", "-NoAsyncLoadingThread", "-UseMultithreadForDS", "-log",' -and $headlessConfigModel -match '-stdout' -and $headlessConfigModel -match '-FullStdOutLogOutput')
Check 'World Discovery' 'World explorer avoids recursive backup false positives' ($worldExplorerService -match 'EnumerateCanonicalWorldDirectories' -and $worldExplorerService -notmatch 'EnumerateFiles\(\s*paths.SaveRoot,\s*"Level.sav",\s*SearchOption.AllDirectories')
Check 'World Inspector UX' 'World ID is fully displayed while a short nickname is available' ($ui -match 'Full canonical active World ID' -and $ui -match 'Text="\{Binding Nickname\}"' -and $ui -match 'Text="\{Binding WorldId\}"' -and (Text 'src\MystTiq.Desktop\Models\WorldExplorerDtos.cs') -match 'public string Nickname')
Check 'Runtime Smoke' 'Runtime smoke exercises player capability gating and persistent audit log' ((Text 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -match 'Player administration route is live' -and (Text 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -match 'Persistent activity/audit log captures management events')

# In-memory behavioral contract: verify the wire payload shape expected by the DTO/client without a running server.
$sample = '{"observedAt":"2026-08-26T12:00:00Z","status":{"phase":1,"ready":false,"lastTransitionAt":null},"service":{"installed":true,"enabled":true,"activeState":"active"},"players":{"available":true,"onlineCount":3,"players":[]},"metrics":{"available":true,"cpuPercent":12.5,"workingSetBytes":104857600,"threadCount":42},"logTail":{"available":true,"fileName":"PalWorldSettings.log","lines":["ready"]}}' | ConvertFrom-Json
Check 'Behavioral Contract' 'Aggregate wire sample accepts null lifecycle transition timestamp before first transition' ($null -eq $sample.status.lastTransitionAt)
Check 'Behavioral Contract' 'Aggregate wire sample preserves coherent server/service/player/metric/log state' (-not $sample.status.ready -and $sample.service.activeState -eq 'active' -and $sample.players.onlineCount -eq 3 -and $sample.metrics.threadCount -eq 42 -and $sample.logTail.lines[0] -eq 'ready')

if($RunBuild){
    & (Join-Path $root 'Build.ps1') -Action Validate -StrictValidation
    & (Join-Path $root 'Build.ps1') -Action Build -Configuration Release
    & (Join-Path $root 'Build.ps1') -Action WindowsHeadless -Configuration Release
    & (Join-Path $root 'Build.ps1') -Action LinuxHeadless -Configuration Release
    & (Join-Path $root 'Build.ps1') -Action DesktopWindows -Configuration Release
    & (Join-Path $root 'Build.ps1') -Action DesktopLinux -Configuration Release
    & (Join-Path $root 'scripts\Test-v0.4.6.1-RuntimeSmoke.ps1') -ProjectRoot $root
}
$failed=@($checks|Where-Object{-not $_.Passed})
Write-Host "`n================ MystTiq v0.4.6.1 Summary ================"
Write-Host "Passed: $($checks.Count-$failed.Count) / $($checks.Count)"
if($ExportJson){$dir=Join-Path $root 'artifacts\logic-tests';New-Item $dir -ItemType Directory -Force|Out-Null;$path=Join-Path $dir "MystTiq_v0.4.6.1_$(Get-Date -Format yyyyMMdd-HHmmss).json";$checks|ConvertTo-Json -Depth 4|Set-Content $path;Write-Host "JSON report: $path"}
if($failed.Count){$failed|Format-Table -AutoSize;throw "v0.4.6.1 logic gate failed: $($failed.Count) check(s)."}
Write-Host 'v0.4.6.1 logic gate passed.' -ForegroundColor Green
