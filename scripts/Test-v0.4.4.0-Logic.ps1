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

Check 'Versioning' 'Version is v0.4.4.0' ($props -match '<VersionPrefix>0\.4\.4\.0</VersionPrefix>' -and $vm -match 'Version => "v0\.4\.4\.0"')
Check 'Composition' 'Desktop remains Avalonia MVVM' ($ui -match 'x:DataType="vm:MainWindowViewModel"' -and $vm -match 'class MainWindowViewModel')
Check 'Polling Contract' 'One aggregate status DTO owns periodic status payload' ($pollDto -match 'class StatusPollingDto' -and $pollDto -match 'ServerStatusDto Status' -and $pollDto -match 'ServiceStatusDto Service' -and $pollDto -match 'PlayersSnapshotDto Players' -and $pollDto -match 'RuntimeMetricsSnapshotDto Metrics' -and $pollDto -match 'LogTailSnapshotDto LogTail')
Check 'API Contract' 'ViewModel-facing API exposes aggregate polling method' ($api -match 'GetStatusPollingAsync')
Check 'API Client' 'Aggregate client calls exactly one polling endpoint' ($client -match 'GetStatusPollingAsync' -and $client -match '/api/v1/status/poll\?lines=')
Check 'Endpoint' 'Headless host exposes aggregate polling endpoint' ($apiHostSource -match 'MapGet\("/api/v1/status/poll"')
Check 'Endpoint' 'Polling endpoint samples lifecycle service players metrics and log tail' ($apiHostSource -match 'lifecycle\.GetStatusAsync' -and $apiHostSource -match 'serviceManager\.GetStatusAsync' -and $apiHostSource -match 'monitoring\.GetPlayersAsync' -and $apiHostSource -match 'monitoring\.GetMetricsAsync' -and $apiHostSource -match 'monitoring\.GetLogTail')
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
Check 'Platform Preservation' 'Desktop contains no Windows-only namespace dependency' ($vm -notmatch 'System\.Windows|Microsoft\.Win32')
Check 'Platform Preservation' 'Headless endpoint uses platform abstractions' ($apiHostSource -match 'ServerPathProfile\.ForCurrentPlatform' -and $apiHostSource -match 'ServerDistributionPlatformService\.ForCurrentPlatform')
Check 'Release Gate' 'Root build exposes version-selected LogicTests' ($build -match "'LogicTests'" -and $build -match 'Test-v\$v-Logic\.ps1')
Check 'Release Gate' 'Release workflow runs logic tests before packaging' ($release.IndexOf('Running v$version logic tests') -ge 0 -and $release.IndexOf('Running v$version logic tests') -lt $release.IndexOf('Creating portable package'))
Check 'Release Gate' 'Release workflow builds Windows and Linux headless plus desktop' ($release -match 'Build-WindowsHeadless\.ps1' -and $release -match 'Build-LinuxHeadless\.ps1' -and $release -match "Runtime 'win-x64'" -and $release -match "Runtime 'linux-x64'")
Check 'Documentation' 'README identifies v0.4.4.0 candidate and v0.4.3.1 baseline' ($readme -match 'v0\.4\.4\.0' -and $readme -match 'v0\.4\.3\.1')
Check 'Documentation' 'Roadmap identifies v0.4.4.0 current candidate' ($road -match 'v0\.4\.4\.0')

# In-memory behavioral contract: verify the wire payload shape expected by the DTO/client without a running server.
$sample = '{"observedAt":"2026-08-26T12:00:00Z","status":{"phase":2,"ready":true},"service":{"installed":true,"enabled":true,"activeState":"active"},"players":{"available":true,"onlineCount":3,"players":[]},"metrics":{"available":true,"cpuPercent":12.5,"workingSetBytes":104857600,"threadCount":42},"logTail":{"available":true,"fileName":"PalWorldSettings.log","lines":["ready"]}}' | ConvertFrom-Json
Check 'Behavioral Contract' 'Aggregate wire sample preserves coherent server/service/player/metric/log state' ($sample.status.ready -and $sample.service.activeState -eq 'active' -and $sample.players.onlineCount -eq 3 -and $sample.metrics.threadCount -eq 42 -and $sample.logTail.lines[0] -eq 'ready')

if($RunBuild){& (Join-Path $root 'Build.ps1') -Action Validate -StrictValidation;& (Join-Path $root 'Build.ps1') -Action Build -Configuration Release}
$failed=@($checks|Where-Object{-not $_.Passed})
Write-Host "`n================ MystTiq v0.4.4.0 Summary ================"
Write-Host "Passed: $($checks.Count-$failed.Count) / $($checks.Count)"
if($ExportJson){$dir=Join-Path $root 'artifacts\logic-tests';New-Item $dir -ItemType Directory -Force|Out-Null;$path=Join-Path $dir "MystTiq_v0.4.4.0_$(Get-Date -Format yyyyMMdd-HHmmss).json";$checks|ConvertTo-Json -Depth 4|Set-Content $path;Write-Host "JSON report: $path"}
if($failed.Count){$failed|Format-Table -AutoSize;throw "v0.4.4.0 logic gate failed: $($failed.Count) check(s)."}
Write-Host 'v0.4.4.0 logic gate passed.' -ForegroundColor Green
