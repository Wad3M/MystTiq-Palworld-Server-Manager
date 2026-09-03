[CmdletBinding()]
param([string]$ProjectRoot='.',[switch]$RunBuild,[switch]$ExportJson)
$ErrorActionPreference='Stop';$root=(Resolve-Path $ProjectRoot).Path;$checks=@()
function Check($area,$name,$ok){$script:checks += [pscustomobject]@{Area=$area;Check=$name;Passed=[bool]$ok};Write-Host ("[{0}] {1} :: {2}" -f ($(if($ok){'PASS'}else{'FAIL'}),$area,$name)) -ForegroundColor $(if($ok){'Green'}else{'Red'})}
function Text($rel){Get-Content (Join-Path $root $rel) -Raw}
$props=Text 'Directory.Build.props';$ui=Text 'src\MystTiq.Desktop\MainWindow.axaml';$vm=Text 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs';$nav=Text 'src\MystTiq.Desktop\Models\NavigationPage.cs';$theme=Text 'src\MystTiq.Desktop\App.axaml';$build=Text 'Build.ps1';$api=Text 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs';$road=Text 'docs\roadmap\PRODUCT_ROADMAP.md'
Check 'Versioning' 'Version is v0.4.0.2' ($props -match '<VersionPrefix>0\.4\.0\.2</VersionPrefix>')
foreach($g in 'SERVER','WORLD','MODS','TOOLS','SYSTEM'){Check 'Navigation' "$g group exists" ($ui -match [regex]::Escape($g))}
$pages=@('Dashboard','ServerSetup','Configuration','Backups','Console','Workspace','Inspector','Players','Bases','Guilds','ModDashboard','ModLibrary','Ue4ss','UpdateCenter','Doctor','CrashAnalyzer','SaveTools','DiagnosticsCenter','Settings','ActivityAudit')
foreach($page in $pages){
 $commandParameter = 'CommandParameter="' + $page + '"'
 Check 'Navigation' "$page destination exists" ($nav -match ("\b" + [regex]::Escape($page) + "\b") -and $ui.Contains($commandParameter))
}
Check 'Navigation' 'Navigation is ViewModel command driven' ($ui -match 'Command="{Binding NavigateCommand}"' -and $vm -match 'NavigateCommand = new RelayCommand<string>\(Navigate\)')
Check 'Behavior' 'Unfinished pages are explicit placeholders, not fake data' ($vm -match 'IsPlaceholderPage' -and $ui -match 'No fake operational data is shown')
Check 'Composition' 'Server Setup routes to existing distribution backend' ($vm -match 'IsSetupUpdatePage => SelectedPage is NavigationPage.ServerSetup or NavigationPage.UpdateCenter' -and $api -match 'Distribution')
Check 'Composition' 'Console routes to existing monitoring backend' (
 $vm -match 'IsMonitoringPage => SelectedPage is NavigationPage.Console or NavigationPage.ActivityAudit' -and
 $vm -match 'GetPlayersAsync\(profile' -and
 $vm -match 'GetLogTailAsync\(profile' -and
 $vm -match 'GetMetricsAsync\(profile' -and
 $api -match 'GetPlayersAsync' -and
 $api -match 'GetLogTailAsync' -and
 $api -match 'GetMetricsAsync'
)
Check 'Composition' 'Inspector routes to existing world explorer backend' ($vm -match 'IsWorldExplorerPage => SelectedPage == NavigationPage.Inspector' -and $api -match 'World')
Check 'Composition' 'MOD destinations route to existing MOD backend' ($vm -match 'ModDashboard or NavigationPage.ModLibrary or NavigationPage.Ue4ss' -and $api -match 'Mod')
Check 'Composition' 'Diagnostics Center routes to network diagnostics backend' ($vm -match 'DiagnosticsCenter' -and $api -match 'NetworkDiagnostic')
Check 'Theme' 'Centralized navy application resources exist' ($theme -match 'MystTiqBackground' -and $theme -match '#08111D' -and $theme -match 'MystTiqSidebar')
Check 'Theme' 'Blue primary accent exists' ($theme -match 'MystTiqBlue' -and $theme -match '#2389D7')
Check 'Theme' 'Purple is not the primary accent' (-not ($theme -match '#7557E8|#A997FF'))
foreach($style in 'primary','success','warning','danger','navitem','card','statuscard'){Check 'Styles' "$style shared style exists" ($theme -match [regex]::Escape($style))}
Check 'Styles' 'Shared button sizing is centralized' ($theme -match 'Selector="Button"' -and $theme -match 'MinHeight')
Check 'Styles' 'Hover state is centralized' ($theme -match 'pointerover')
Check 'Build Quality' 'Desktop compiler warnings are promotion-blocking' ((Text 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -match '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>')
Check 'Build Quality' 'Diagnostics restart recommendation handles nullable API message' ($vm -match 'result\.Restart\.Message \?\?')
Check 'Local State' 'Service state distinguishes Running / Stopped / Not Installed / Unreachable' ((Text 'src\MystTiq.Desktop\Models\LocalInstallationState.cs') -match 'Running, Stopped, NotInstalled, Unreachable')
Check 'Local State' 'PalServer state distinguishes Found / Not Found / Unknown' ((Text 'src\MystTiq.Desktop\Models\LocalInstallationState.cs') -match 'Found, NotFound, Unknown')
Check 'Local State' 'API state distinguishes Connected / Unavailable / Authentication Required / Configuration Error' ((Text 'src\MystTiq.Desktop\Models\LocalInstallationState.cs') -match 'Connected, Unavailable, AuthenticationRequired, ConfigurationError')
Check 'Sidebar' 'Persistent server status card exists' ($ui -match 'SERVER STATUS' -and $ui -match 'ServerState' -and $ui -match 'ServiceState')
Check 'Regression' 'v0.4.0.1 network diagnostics remain present' (Test-Path (Join-Path $root 'src\MystTiq.Core\Services\NetworkDiagnosticsService.cs'))
Check 'Regression' 'MOD/UE4SS backend remains present' (Test-Path (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs'))
Check 'Build Gate' 'Root Build.ps1 exposes LogicTests action' ($build -match "'LogicTests'")
Check 'Build Gate' 'Release/All invokes current version logic harness' ($build -match 'Test-v\$v-Logic\.ps1')
Check 'Linux Packaging' 'Current Linux acceptance script exists' (Test-Path (Join-Path $root 'scripts\Test-v0.4.0.2-LinuxAcceptance.sh'))
Check 'Linux Packaging' 'Current production readiness script exists' (Test-Path (Join-Path $root 'scripts\Test-v0.4.0.2-ProductionReadiness.sh'))
Check 'Release Packaging' 'Changed-files apply instructions exist' (Test-Path (Join-Path $root 'release-notes\APPLY_v0.4.0.2_CHANGED_FILES.md'))
Check 'Release Packaging' 'v0.4.0.2 release notes exist' (Test-Path (Join-Path $root 'release-notes\v0.4.0.2.md'))
Check 'Release Packaging' 'v0.4.0.2 build/test plan exists' (Test-Path (Join-Path $root 'release-notes\BUILD_TEST_PLAN_v0.4.0.2.md'))
Check 'Roadmap' 'GUI migration sequence is captured' ($road -match 'v0\.4\.0\.3.+Dashboard & Local Server Backend Integration' -and $road -match 'v0\.4\.0\.9.+GUI Consistency')
Check 'Roadmap' 'Deferred save/freeze and MOD functional testing remain captured' ($road -match 'v0\.4\.0\.10.+Save / Client Freeze' -and $road -match 'v0\.4\.0\.13.+MOD Validation')
if($RunBuild){
 & (Join-Path $root 'Build.ps1') Validate;Check 'Build' 'Release validation' $true
 & (Join-Path $root 'Build.ps1') WindowsHeadless;Check 'Build' 'Windows persistent host publishes' $true
 & (Join-Path $root 'Build.ps1') DesktopWindows;Check 'Build' 'Windows Avalonia desktop publishes' $true
 & (Join-Path $root 'Build.ps1') LinuxHeadless;Check 'Build' 'Linux headless publishes' $true
 & (Join-Path $root 'Build.ps1') DesktopLinux;Check 'Build' 'Linux Avalonia desktop publishes' $true
}
$passed=@($checks|? Passed).Count;$failed=@($checks|?{-not $_.Passed}).Count
Write-Host "`n================ MystTiq v0.4.0.2 Summary ================";Write-Host "Passed : $passed";Write-Host "Failed : $failed"
if($ExportJson){$dir=Join-Path $root 'artifacts\logic-tests';New-Item $dir -ItemType Directory -Force|Out-Null;$path=Join-Path $dir "MystTiq_v0.4.0.2_$(Get-Date -Format yyyyMMdd-HHmmss).json";$checks|ConvertTo-Json -Depth 4|Set-Content $path;Write-Host "JSON report: $path"}
if($failed){exit 1}
