[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [switch]$RunBuild,
    # v0.8.1.0: where the previous checkpoint's FullSource ZIP is. Defaults to the release machine's backup
    # folder; when it is not there, the frozen-baseline check is reported as SKIP instead of crashing the gate.
    [string]$FrozenBaselineZip = '',
    [bool]$ExportJson = $true,
    [bool]$ExportJUnit = $true
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$root = (Resolve-Path $ProjectRoot).Path
$framework = Join-Path $root 'scripts\Testing\MystTiq.TestFramework.ps1'
if (-not (Test-Path $framework -PathType Leaf)) {
    throw "MystTiq test framework not found: $framework"
}
. $framework

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.8.21.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.8\.21\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.8.21.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.8\.21\.0"' `
    'Versioning' 'app.manifest reports v0.8.21.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.8.20.0-Logic.ps1' 'Regression' 'v0.8.20.0 logic gate remains available' -Severity High

$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
# v0.8.17.0: 27 navigation icons with the HOST page.
Add-MystTiqCheck $ctx 'Regression' 'v0.8.0.0 artwork remains: themed page art, header art filling the card, 50px nav icons, v0.7.115.0 map label layer' `
    ($xaml -match 'Source="\{Binding PageArtwork\}"' -and $xaml -match 'x:Name="PageArtHeader"[^>]*Padding="0"' -and
     ([regex]::Matches($xaml, '\{services:IconArt [^}]+\}" Width="50" Height="50"')).Count -eq 27 -and
     $xaml -match 'Margin="\{Binding LabelPosition\}"') `
    -Severity Critical

$icons = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\RibbonIcons.cs'
$package = Get-Content (Join-Path $root 'docs\ribbon-icons\icons.json') -Raw | ConvertFrom-Json
$mismatched = @($package | Where-Object { $icons -notmatch ('\["' + [regex]::Escape($_.key) + '"\] = "' + [regex]::Escape($_.path) + '"') } | ForEach-Object { $_.key })
Add-MystTiqCheck $ctx 'Regression' 'v0.8.1.0 Ribbon icons remain: all 10 use the supplied path data and the Ribbon draws them' `
    (@($package).Count -eq 10 -and $mismatched.Count -eq 0 -and
     $xaml -match '<Path Classes="ribbonIcon" Data="\{Binding VectorIcon\}" IsVisible="\{Binding HasVectorIcon\}"') `
    -Severity Critical -Details "mismatched: $($mismatched -join ', ')"

$program = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\Program.cs'
$apiHost = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.2.0 service mode remains: configured game port and a single supervisor' `
    ($program -match 'var lifecycle = WindowsServiceRunLifecycleFactory\(effectiveDefaultServerProfile, paths\);' -and
     $apiHost -match 'if \(!externallySupervised\.Contains\(p\.Id\)\) await p\.CrashRecovery\.StartAsync') `
    -Severity Critical

$catalog = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessGameIdCatalogService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.3.0 Give Item picker remains: world-save ids, the catalogue route and the Desktop picker' `
    ($catalog -match 'public static IReadOnlyList<GameIdCatalogEntry> Merge\(' -and
     $apiHost -match 'routes\.MapGet\("/players/give/catalog"' -and $xaml -match 'x:Name="GameIdPicker"') `
    -Severity Critical

$routing = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessNotificationRoutingService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.4.0 delivery pause remains: Dispatch skips outside channels while paused, and the Alert Center card' `
    ($routing -match 'if \(NotificationDeliveryPolicy\.IsPaused\(pausedUntil, DateTimeOffset\.UtcNow\)\)' -and $xaml -match 'x:Name="PauseDeliveryCard"') `
    -Severity Critical

$localizer = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\Localizer.cs'
$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$i18n = Join-Path $root 'src\MystTiq.Desktop\Assets\i18n'
$english = Get-Content (Join-Path $i18n 'en.json') -Raw | ConvertFrom-Json -AsHashtable
Add-MystTiqCheck $ctx 'Regression' 'v0.8.5.0 display language remains: English fallback, live Strings binding, translated tabs, navigation and page headers' `
    ($localizer -match 'public static string Lookup\(' -and $localizer -match 'new Binding\(\$"\{nameof\(Localizer\.Strings\)\}\[\{Key\}\]"\)' -and
     ([regex]::Matches($xaml, '<TextBlock Grid\.Column="1" Text="\{services:Tr nav\.')).Count -eq 27 -and
     $vm -match 'public string PageTitle => Localizer\.Instance\[\$"page\.\{SelectedPage\}\.title"\];') `
    -Severity Critical

$languageProblems = @()
foreach ($code in 'de', 'es') {
    $other = Get-Content (Join-Path $i18n "$code.json") -Raw | ConvertFrom-Json -AsHashtable
    $missing = @($english.Keys | Where-Object { -not $other.ContainsKey($_) })
    $extra = @($other.Keys | Where-Object { -not $english.ContainsKey($_) })
    $blank = @($other.Keys | Where-Object { [string]::IsNullOrWhiteSpace($other[$_]) })
    if ($missing.Count + $extra.Count + $blank.Count -gt 0) { $languageProblems += "${code}: missing $($missing -join ','); extra $($extra -join ','); blank $($blank -join ',')" }
}
Add-MystTiqCheck $ctx 'Regression' 'German and Spanish still translate exactly the keys English has, none blank' `
    ($languageProblems.Count -eq 0) -Severity Critical -Details ($languageProblems -join ' | ')

$ribbonModel = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\RibbonActionViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.6.0 Ribbon language remains: English identity for icons, translated DisplayLabel, text-based width estimate' `
    ($ribbonModel -match 'RibbonIcons\.KeyFor\(Label, Glyph\)' -and $ribbonModel -match 'public string DisplayLabel \{ get; init; \} = Label;' -and
     $vm -match '_allRibbonGroups = (GateRibbonByRole\()?LocalizeRibbon\(BuildRibbonGroupsForActivePage\(\)\)\)?;' -and
     $vm -match 'group\.Actions\.Sum\(a => Math\.Max\(68, a\.DisplayLabel\.Length \* 7\.2 \+ 16\)\)') `
    -Severity Critical

$dashStart = $xaml.IndexOf('<StackPanel IsVisible="{Binding IsDashboardPage}" Spacing="6">')
$dashboard = $xaml.Substring($dashStart, $xaml.IndexOf('<!-- SERVER SETUP', $dashStart) - $dashStart)
Add-MystTiqCheck $ctx 'Regression' 'v0.8.7.0 Dashboard language remains: no fixed English text, TrFormat templates, Auto-sized CPU/MEMORY columns' `
    (@([regex]::Matches($dashboard, '(?:Text|Content|ToolTip\.Tip)="([^"{][^"]*)"')).Count -eq 0 -and
     ([regex]::Matches($dashboard, '\{services:TrFormat dashboard\.format\.')).Count -eq 6 -and
     $dashboard -match '<Grid ColumnDefinitions="Auto,Auto,\*" ColumnSpacing="6" ClipToBounds="True">') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.8.8.0 Ribbon image icons remain: image before vector before glyph' `
    ($xaml -match 'Text="\{Binding Glyph\}" IsVisible="\{Binding ShowGlyph\}"' -and $xaml -match '<Image Classes="ribbonImage"') `
    -Severity Critical

$parser = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\UnrealCrashReport.cs'
$watcher = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessCrashReportWatcher.cs'
$automationText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAutomationService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.9.0 crash reports remain: the parser, the watcher and its automation tick' `
    ($parser -match 'public const string ContextFileName = "CrashContext\.runtime-xml";' -and
     $watcher -match 'CrashReportCheckOutcome\.Baseline' -and $automationText -match 'await crashReports\.CheckAsync\(DateTimeOffset\.UtcNow, token\);') `
    -Severity Critical

$imageRecord = Get-MystTiqText $ctx 'docs\ribbon-icons\images\README.md'
$imageDir = Join-Path $root 'src\MystTiq.Desktop\Assets\RibbonIcons'
$imageRows = @([regex]::Matches($imageRecord, '\| [1234] \| `(\w+)\.png` \| [^|]+ \| `([0-9A-F]{64})` \| `([0-9A-F]{64})` \|') | ForEach-Object { [pscustomobject]@{ Name = $_.Groups[1].Value; Hash = $_.Groups[2].Value } })
$imageBad = @($imageRows | Where-Object { -not (Test-Path (Join-Path $imageDir "$($_.Name).png")) -or (Get-FileHash (Join-Path $imageDir "$($_.Name).png") -Algorithm SHA256).Hash -ne $_.Hash } | ForEach-Object Name)
Add-MystTiqCheck $ctx 'Regression' 'v0.8.10.0 Ribbon images remain: all 40 (30 originals + batch 4) match their recorded hashes (shipped hashes match, original hashes recorded)' `
    ($imageRows.Count -eq 40 -and $imageBad.Count -eq 0 -and @(Get-ChildItem $imageDir -Filter *.png).Count -eq 40) `
    -Severity Critical -Details "mismatched: $($imageBad -join ', ')"

$wrongSize = @(Get-ChildItem $imageDir -Filter *.png | Where-Object {
    $bytes = [IO.File]::ReadAllBytes($_.FullName)
    $w = ([int]$bytes[16] -shl 24) -bor ([int]$bytes[17] -shl 16) -bor ([int]$bytes[18] -shl 8) -bor [int]$bytes[19]
    $h = ([int]$bytes[20] -shl 24) -bor ([int]$bytes[21] -shl 16) -bor ([int]$bytes[22] -shl 8) -bor [int]$bytes[23]
    $w -ne 512 -or $h -ne 512 } | ForEach-Object Name)
Add-MystTiqCheck $ctx 'Regression' 'v0.8.10.0: every Ribbon image is still 512x512, so the bundle stays small and decodes quickly' `
    ($wrongSize.Count -eq 0) -Severity High -Details "wrong size: $($wrongSize -join ', ')"

Add-MystTiqCheck $ctx 'Regression' 'v0.8.10.0: Quick Actions Backup and Doctor still share the Create and Run Doctor images; ImageKeys lists each file once' `
    ($icons -match '\["Backup"\] = "create_backup", \["Doctor"\] = "run_doctor"' -and
     $icons -match 'ImageKeys => ImageByLabel\.Values\.Distinct\(StringComparer\.Ordinal\)\.ToArray\(\);') `
    -Severity Critical

$palReader = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\SavePalLocationReader.cs'
$explorer = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessPlayerGuildExplorerService.cs'
$palLayout = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\PalMapLayout.cs'
$labelLayout = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MapLabelLayout.cs'
$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'

Add-MystTiqCheck $ctx 'Regression' 'v0.8.11.0: Pals are placed by their container: a base worker container, a small (party) container, or the Palbox, which is only counted' `
    ($palReader -match 'case "BaseCampSaveData": ReadWorkerContainers' -and $palReader -match 'case "CharacterContainerSaveData": ReadContainerSlots' -and
     $palReader -match 'TryFindExact\(director, "container_id", 0, out var container\)' -and $palReader -match 'private const int LargestCarriedContainer = 10;' -and
     $palReader -match 'if \(placement == Palbox\) \{ inPalbox\+\+; continue; \}' -and $palReader -match 'else \{ unplaced\+\+; continue; \}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.8.11.0: unset positions (0,0, placeholder altitude, within 20 m of the origin) are dropped, and property names match exactly (ID is not id)' `
    ($palReader -match 'if \(x == 0 && y == 0\) return false;' -and $palReader -match 'if \(Math\.Abs\(z\) >= PlaceholderAltitude\) return false;' -and
     $palReader -match 'private const double OriginRadius = 2000;' -and $palReader -match 'if \(element\.TryGetProperty\(name, out found\)\) return true;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.8.11.0: the explorer snapshot carries the Pals (named by owner and guild) and a summary, and a read failure degrades to none' `
    ($explorer -match 'try \{ palLocations = SavePalLocationReader\.Read\(document\.RootElement\); \}' -and
     $explorer -match 'IReadOnlyList<HeadlessPalLocation>\? PalLocations = null,' -and $explorer -match 'HeadlessPalSummary\? PalSummary = null\);' -and
     $explorer -match 'playerById\.GetValueOrDefault\(pal\.OwnerPlayerId\)\?\.PlayerName') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.8.11.0: the map clusters Pals within 10 px, badges a cluster beside a base on its corner, and zooms a group all the way in' `
    ($palLayout -match 'public const double ClusterRadius = 10;' -and $palLayout -match 'public const double BadgeSnapRadius = 12;' -and
     $vm -match 'var badge = PalMapLayout\.BadgePosition\(cluster\.ViewX, cluster\.ViewY, baseMarkers\);' -and
     $vm -match '_mapViewport\.CenterOn\(point\.UnzoomedX, point\.UnzoomedY, MapViewport\.MaxScale\);' -and
     $vm -match '(?s)BaseMapPoints\.Add\(new BaseMapPointDto.*?RebuildPalMapPoints\(\);') `
    -Severity Critical

$basesAt = $xaml.IndexOf('<ItemsControl ItemsSource="{Binding BaseMapPoints}">'); $palsAt = $xaml.IndexOf('<ItemsControl ItemsSource="{Binding PalMapPoints}">'); $playersAt = $xaml.IndexOf('<ItemsControl ItemsSource="{Binding PlayerMapPoints}">')
Add-MystTiqCheck $ctx 'Regression' 'v0.8.11.0: Pals draw above bases and under players, in a fixed violet, and name labels step around them' `
    ($basesAt -gt 0 -and $basesAt -lt $palsAt -and $palsAt -lt $playersAt -and $xaml -match '<Ellipse Fill="#9F7AEA"' -and
     $xaml -match 'IsChecked="\{Binding ShowPalsOnMap\}"' -and $labelLayout -match 'IReadOnlyList<Box>\? obstacles = null' -and
     $vm -match 'MapLabelLayout\.ComputeLabelOffsets\(labels, palBoxes\)') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$harnessProj = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\MystTiq.LogicHarness.csproj'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.11.0: the logic harness covers the reader and the map layer (clusters, badge, label obstacles, status text)' `
    (([regex]::Matches($harness, 'RunScenario\("Pal positions: ')).Count -eq 2 -and $harnessProj -match 'PalMapLayout\.cs') -Severity Critical

$smoke = Get-MystTiqText $ctx 'scripts\Test-v0.8.11.0-RouteSmoke.ps1'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.11.0: the Pal positions smoke uses its own FleetRoot and a synthetic save' `
    ($smoke -match '\$cfg\.FleetRoot = \$fleetRoot' -and $smoke -match 'PalPositionSmokeWorld') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.11.0-pal-positions.md' 'Regression' 'v0.8.11.0: v0.8.11.0 architecture doc is present' -Severity Critical

$nat = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\NatTopology.cs'
$wan = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WanReachabilityService.cs'
$wanModels = Get-MystTiqText $ctx 'src\MystTiq.Core\Models\WanReachabilityModels.cs'

Add-MystTiqCheck $ctx 'Regression' 'v0.8.12.0: the router WAN address decides: 100.64/10 is CGNAT (Fail), a private address is double NAT (Fail), a different public address is a Warning' `
    ($nat -match 'b\[0\] == 100 && b\[1\] >= 64 && b\[1\] <= 127' -and
     $nat -match 'b\[0\] == 10 \|\| \(b\[0\] == 172 && b\[1\] >= 16 && b\[1\] <= 31\) \|\| \(b\[0\] == 192 && b\[1\] == 168\)' -and
     $nat -match 'if \(IsCarrierGrade\(wan\)\)' -and $nat -match 'if \(IsPrivate\(wan\)\)' -and $nat -match '!pub\.Equals\(wan\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.8.12.0: without a router WAN address the route out is weak evidence: a private hop is "cannot tell" (it was the ISP on the real network), a silent hop 2 gives no Pass, never a Fail' `
    ($nat -match 'public static WanReachabilityCheck\? ClassifyRoute\(IReadOnlyList<string\?> hops, bool routerRefused = false\)' -and
     ([regex]::Matches(($nat.Substring($nat.IndexOf('ClassifyRoute('))), 'DiagnosticState\.Fail')).Count -eq 0 -and
     $nat -match 'if \(IsPrivate\(hop\)\)\s*return new\(TestName, DiagnosticState\.Skipped,' -and $nat -match 'if \(i > 1\) return null;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.8.12.0: UPnP works on Linux and on multi-interface Windows: http-only control URLs, a search from every LAN interface, default-payload pings, a refused WAN address asked twice' `
    ($wan -match 'public static Uri ResolveControlUrl\(Uri location,string controlUrlText\)' -and $wan -match 'abs\.Scheme==Uri\.UriSchemeHttp\|\|abs\.Scheme==Uri\.UriSchemeHttps' -and
     $wan -match 'SearchInterfaces\(\)\.Select\(s=>SsdpSearchFromAsync' -and $wan -match 'SocketOptionName\.MulticastInterface' -and
     $wan -match 'SendPingAsync\(target,TimeSpan\.FromSeconds\(1\),null,' -and $wan -match 'IPStatus\.TimeExceeded' -and
     $wan -match 'if\(routerRefusedWan\)\{await Task\.Delay\(500,token\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.8.12.0: the service asks the router for its WAN address (GetExternalIPAddress), falls back to the first 4 hops, and never claims outside reachability' `
    ($wan -match 'SoapCallAsync\(igd,"GetExternalIPAddress"' -and $wan -match 'if\(secondNat\.State==DiagnosticState\.Skipped\)' -and
     $wan -match 'TraceFirstHopsAsync\(CancellationToken token,int maxHops=4\)' -and
     $wan -match 'checks\.Add\(new\(OutsideInTestName,DiagnosticState\.Skipped,' -and $wan -match 'Palworld uses UDP' -and
     $wanModels -match 'string\? RouterWanIPv4=null\);') `
    -Severity Critical

$dtos = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\NetworkDiagnosticDtos.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.12.0: the Desktop shows the router''s internet address and explains what is and is not checked' `
    ($dtos -match 'string\? RouterWanIPv4=null\)' -and $vm -match 'internet address \{report\.RouterWanIPv4\}' -and
     $xaml -match 'a second NAT \(carrier-grade NAT or a router behind another router\) are checked automatically') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.12.0: the logic harness covers every WAN-address and route branch, including the range edges' `
    ($harness -match 'RunScenario\("Second NAT: ' -and $harness -match '"100\.128\.0\.1"' -and $harness -match '"172\.32\.4\.4"' -and
     $harness -match 'WanReachabilityService\.ResolveControlUrl\(location, "/ctl/IPConn"\)' -and $harness -match 'a silent hop 2 followed by a public hop is no verdict') -Severity Critical

$smoke = Get-MystTiqText $ctx 'scripts\Test-v0.8.12.0-RouteSmoke.ps1'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.12.0: the second-NAT smoke uses its own FleetRoot and checks the contract, not a network-dependent verdict' `
    ($smoke -match '\$cfg\.FleetRoot = \$fleetRoot' -and $smoke -match 'checks the contract, not a verdict') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.12.0-second-nat.md' 'Regression' 'v0.8.12.0: v0.8.12.0 architecture doc is present' -Severity Critical

$icons813 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\RibbonIcons.cs'
$labels = @([regex]::Matches($vm, 'new\("[^"]*", "([^"]+)", "[^"]*", \w+') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
$imageBlock = $icons813.Substring($icons813.IndexOf('ImageByLabel = new')); $imageBlock = $imageBlock.Substring(0, $imageBlock.IndexOf('};'))
$imageLabels = @([regex]::Matches($imageBlock, '\["([^"]+)"\] = "') | ForEach-Object { $_.Groups[1].Value })
$noImage = @($labels | Where-Object { $_ -notin $imageLabels })
Add-MystTiqCheck $ctx 'Regression' 'v0.8.13.0: every Ribbon label has an image: the last ten (batch 4) are mapped, the Refresh variants and Restart Server share theirs' `
    ($labels.Count -eq 50 -and $noImage.Count -eq 0 -and $icons813 -match '\["Refresh History"\] = "refresh"' -and $icons813 -match '\["Restart Server"\] = "restart"' -and
     $icons813 -match '\["Run Analysis"\] = "run_analysis", \["Preview Plan"\] = "preview_plan"') `
    -Severity Critical -Details "without an image: $($noImage -join ', ')"

$names = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessGameNameService.cs'
$extractor = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\Tools\extract_game_names.py'
$headlessProj = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.13.0: the extractor is MystTiq''s own read-only script, shipped beside the executable; it needs a working ooz only for Oodle files' `
    ($headlessProj -match 'Include="Tools\\extract_game_names\.py" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" ExcludeFromSingleFile="true"' -and
     $extractor -match 'if version != 11: raise Fail\(3' -and $extractor -match 'if ooz is None or not hasattr\(ooz, "decompress"\):' -and
     $extractor -match 'ITEM_NAME_' -and $extractor -match 'PAL_NAME_' -and $extractor -match 'ensure_ascii=True') `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.8.13.0: names are cached per pak (path, size, time), a failure is retried after 10 minutes, and every reason is reported' `
    ($names -match 'Path\.Combine\(paths\.ManagerRuntimeRoot, "game-names", \$"\{Language\}\.json"\)' -and
     $names -match 'public static readonly TimeSpan RetryAfterFailure = TimeSpan\.FromMinutes\(10\);' -and
     $names -match '2 => \(null, "Python''s Oodle module \(ooz\) is not installed"\)' -and $names -match 'return \(null, "Python was not found"\);' -and
     $names -match 'if \(dir\.Contains\("WindowsApps", StringComparison\.OrdinalIgnoreCase\)\) continue;' -and $names -match 'private static string LastLine\(string text\)') `
    -Severity Critical

$catalogSvc = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessGameIdCatalogService.cs'
$explorer813 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessPlayerGuildExplorerService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.13.0: the Give Item catalogue names its rows and adds the game-only ids (marked), seen ones first; Pal positions carry the species name' `
    ($catalogSvc -match 'public static IReadOnlyList<GameIdCatalogEntry> WithNames' -and $catalogSvc -match 'InGameFiles: true' -and
     $catalogSvc -match '\.ThenBy\(r => r\.InGameFiles \? 1 : 0\)' -and $explorer813 -match 'names\.PalName\(pal\.Species\) \?\? string\.Empty' -and
     $apiHost -match 'var gameNames = new HeadlessGameNameService\(paths\);') `
    -Severity Critical

$search = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\GameIdSearch.cs'
$kitDtos = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\KitDtos.cs'
$palLayout813 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\PalMapLayout.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.13.0: the Desktop searches and shows names ("Name (id)", "not yet seen on this server"), and the map uses species names' `
    ($search -match 'public static bool Matches\(string\? id, string\? name, string\? query\)' -and $vm -match 'GameIdSearch\.Matches\(e\.Id, e\.Name, GameIdFilterText\)' -and
     $kitDtos -match 'not yet seen on this server' -and $kitDtos -match '\$"\{Name\} \(\{Id\}\)"' -and
     $palLayout813 -match 'public static string Species\(PalMapEntry pal\)' -and $xaml -match 'Watermark="Search names or ids, e\.g\. pal sphere, lamball"' -and
     $vm -match ': _gameIdCatalogDetail;') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$artHarness813 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.13.0: the harnesses cover names (lookups, catalogue order, search, the service''s cache and failures) and every page''s Ribbon images' `
    (([regex]::Matches($harness, 'RunScenario\("Game names: ')).Count -eq 2 -and
     $artHarness813 -match 'RibbonIcons\.ImageKeys\.Count == 40' -and $artHarness813 -match 'every Ribbon button has an image') -Severity Critical

$smoke = Get-MystTiqText $ctx 'scripts\Test-v0.8.13.0-RouteSmoke.ps1'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.13.0: the game names smoke uses its own FleetRoot and a synthetic pak (no game files), and the Linux check exists' `
    ($smoke -match '\$cfg\.FleetRoot = \$fleetRoot' -and $smoke -match 'make_test_pak\.py' -and
     (Test-Path (Join-Path $root 'scripts\Testing\make_test_pak.py')) -and (Test-Path (Join-Path $root 'scripts\Test-v0.8.13.0-LinuxIsolated.ps1'))) -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.13.0-game-names.md' 'Regression' 'v0.8.13.0: v0.8.13.0 architecture doc is present' -Severity Critical

$roleAccess = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\RoleAccess.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.14.0: one role rule mirrors the server (Viewer < Operator < Admin < Owner); unknown roles get the least; local use keeps full access' `
    ($roleAccess -match '"Owner" => Owner,' -and $roleAccess -match '_ => Viewer' -and
     $roleAccess -match 'public static bool Allows\(bool signedIn, string\? role, int minimum\) => !signedIn \|\| Rank\(role\) >= minimum;' -and
     $vm -match 'public bool CanOperate => RoleAccess\.Allows' -and $vm -match 'RaisePropertyChanged\(nameof\(CanOperate\)\);') `
    -Severity Critical

$vis = @{
    'Teleport Points' = 'IsVisible="\{Binding CanOperate\}" IsEnabled="\{Binding CanManageAdmin\}"'
    'Fleet Actions' = 'IsVisible="\{Binding CanOperate\}" IsEnabled="\{Binding CanOperate\}"'
    'Clone World' = 'IsVisible="\{Binding CanManagePrincipals\}"'
}
Add-MystTiqCheck $ctx 'Regression' 'v0.8.14.0: cards a role cannot use are hidden (Give, Whitelist, Kits, Migration, New Rule, Mute, Pause, Discord Bot, principals, users, Clone World); Fleet Actions is Operator as on the server' `
    (([regex]::Matches($xaml, 'IsVisible="\{Binding CanManageAdmin\}" IsEnabled="\{Binding CanManageAdmin\}"')).Count -ge 7 -and
     ([regex]::Matches($xaml, 'IsVisible="\{Binding CanManagePrincipals\}" IsEnabled="\{Binding CanManagePrincipals\}"')).Count -eq 3 -and
     $xaml -match $vis['Teleport Points'] -and $xaml -match $vis['Fleet Actions'] -and
     $xaml -match '<TextBlock Text="Give items or Pals" IsVisible="\{Binding CanManageAdmin\}"' -and
     $xaml -match 'x:Name="GameIdPicker" Classes="statuscard accentWorld" IsVisible="\{Binding CanOperate\}"' -and
     $xaml -match 'x:Name="RoleHiddenNotice"[^>]*IsVisible="\{Binding HasRoleHiddenNotice\}"' -and
     ([regex]::Matches($vm, '\(BackupAllCommand as AsyncCommand\)\?\.RaiseCanExecuteChanged\(\);')).Count -eq 2) `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$artHarness814 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
$harnessProj = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\MystTiq.LogicHarness.csproj'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.14.0: the harnesses check the role order against the server enum, and every gated card for local, Viewer, Operator, Admin and Owner' `
    ($harness -match 'RunScenario\("Role cards: ' -and $harness -match 'foreach \(var role in Enum\.GetValues<MystTiqRole>\(\)\)' -and $harnessProj -match 'RoleAccess\.cs' -and
     $artHarness814 -match 'every role-gated card is shown, read-only or hidden as its role allows' -and
     $artHarness814 -match 'new string\?\[\] \{ null, "Viewer", "Operator", "Admin", "Owner" \}') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.14.0-role-cards.md' 'Regression' 'v0.8.14.0: v0.8.14.0 architecture doc is present' -Severity Critical

$rsOrchestrator = Get-MystTiqText $ctx 'scripts\Test-v0.8.15.0-RemoteSignIn.ps1'
$rsSetup = Get-MystTiqText $ctx 'scripts\Test-v0.8.15.0-RemoteSignIn.setup.sh'
$rsTeardown = Get-MystTiqText $ctx 'scripts\Test-v0.8.15.0-RemoteSignIn.teardown.sh'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.15.0: the remote instance is switched on the documented way (token, TLS certificate, api-remote-enable) in /tmp only, and is always torn down' `
    ($rsSetup -match 'api-token-create' -and $rsSetup -match 'api-tls-create' -and $rsSetup -match 'api-remote-enable' -and $rsSetup -match 'api-run' -and
     $rsSetup -match '/api/v1/security/users' -and @(($rsSetup -split "`n") | Where-Object { $_ -notmatch '^\s*#' -and $_ -match '/etc/mysttiq' }).Count -eq 0 -and $rsOrchestrator -match "\`$dir = '/tmp/mysttiq-v0815-remote'" -and
     $rsOrchestrator -match 'finally \{\s*\$down = @\(ssh' -and $rsTeardown -match 'TORN-DOWN' -and $rsOrchestrator -match 'New-Password') `
    -Severity Critical

$rsHarness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.RemoteSignInHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.15.0: the Desktop itself signs in as each role over pinned TLS: wrong pin and password refused, role, cards, server statuses and sign-out checked' `
    ($rsHarness -match 'a wrong certificate pin is refused before signing in' -and $rsHarness -match 'vm\.SignInCommand\.Execute' -and
     $rsHarness -match 'a wrong password is refused by the server' -and $rsHarness -match 'vm\.CurrentPrincipal!\.Role == account\.Role' -and
     $rsHarness -match 'the signed-in Desktop shows, disables and hides cards for this role' -and
     $rsHarness -match 'the server allows and refuses exactly what the Desktop shows' -and
     $rsHarness -match 'signing out forgets the role and ends the session on the server' -and
     $rsHarness -match 'Dispatcher\.UIThread\.MainLoop\(finished\.Token\)' -and $rsHarness -match 'Isolate\(new CredentialStore\(\)\)') `
    -Severity Critical

$refusal = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\AccessRefusal.cs'
$client = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs'
$artHarness815 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.15.0: a refusal by role, scope or sign-in (401/403 with "error") is thrown with its status, never read as the route result; the sign-in 401 result still is one' `
    ($refusal -match '"insufficient-role" =>' -and $refusal -match '"server-scope-mismatch" =>' -and $refusal -match '"missing-bearer-token" or "invalid-bearer-token" =>' -and
     $client -match 'if \(AccessRefusal\.Describe\(response\.StatusCode, refusedBody\) is \{ \} refusal\)' -and
     $client -match 'throw new HttpRequestException\(refusal, null, response\.StatusCode\)' -and
     $artHarness815 -match "the sign-in route's own 401 result, a 409 result and a 200 are not refusals") -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.15.0-remote-sign-in.md' 'Regression' 'v0.8.15.0: v0.8.15.0 architecture doc is present' -Severity Critical

$themeCatalog = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\ThemeCatalog.cs'
$themeApplier = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\ThemeApplier.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.16.0: five modes on the two hand-tuned palettes: Dark and Light as before, Midnight and High contrast on Dark, System follows the OS; unknown is Dark' `
    ($themeCatalog -match 'Modes = \["Dark", "Light", "Midnight", "HighContrast", "System"\]' -and
     $themeCatalog -match '\?\? "Dark";' -and $themeCatalog -match '"System" => systemPrefersLight \? "Light" : "Dark"' -and
     $themeCatalog -match '\["Midnight"\] = Palette\(\s*\("Bg0", "#000000"\), \("Bg1", "#000000"\)' -and
     $themeCatalog -match '\("Border", "#FFFFFF"\)' -and $themeCatalog -match '\("Text", "#FFFFFF"\)' -and
     $themeApplier -match 'var mode = ThemeCatalog\.NormalizeMode\(variant\);' -and
     $themeApplier -match 'variant = ThemeCatalog\.BaseVariant\(mode, SystemPrefersLight\(\)\);' -and
     $themeApplier -match 'PlatformThemeVariant\.Light') -Severity Critical

$design = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Styles\DesignSystem.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.16.0: card borders and the base control, card, box and list-row sizes are resources, so modes and density reach them; class-specific styles keep their own' `
    ($design -match '<Setter Property="BorderBrush" Value="\{DynamicResource CardBorderBrush\}"/>' -and
     ([regex]::Matches($design, 'Value="\{DynamicResource CardPadding\}"')).Count -ge 2 -and
     ([regex]::Matches($design, 'Value="\{DynamicResource ControlMinHeight\}"')).Count -ge 3 -and
     ([regex]::Matches($design, 'Value="\{DynamicResource InputPadding\}"')).Count -ge 2 -and
     $design -match 'Value="\{DynamicResource ListItemPadding\}"' -and
     $themeApplier -match 'app\.Resources\["ControlMinHeight"\] = compact \? 26d : 31d;' -and
     $themeApplier -match 'app\.Resources\["CardPadding"\] = compact \? new Thickness\(7\) : new Thickness\(11\);' -and
     $themeApplier -match 'app\.Resources\["CardBorderBrush"\]') -Severity Critical

$displayStore = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\LocalDisplayPreferencesStore.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.16.0: the mode picker replaces the Light mode checkbox (per tab); density applies to every tab and is remembered; a tab following the system changes with it' `
    ($xaml -match 'x:Name="ThemeModeCombo" ItemsSource="\{Binding ThemeModeLabels\}" SelectedIndex="\{Binding SelectedThemeModeIndex\}"' -and
     $xaml -match 'x:Name="DensityCombo" ItemsSource="\{Binding DensityLabels\}" SelectedIndex="\{Binding SelectedDensityIndex\}"' -and
     $xaml -notmatch 'CheckBox Content="Light mode"' -and
     $vm -match 'ApplyProfileTheme\(profile with \{ ThemeVariant = mode \}\);' -and
     $vm -match 'platformSettings\.ColorValuesChanged \+= \(_, _\) => Avalonia\.Threading\.Dispatcher\.UIThread\.Post\(OnSystemThemeChanged\);' -and
     $vm -match 'ThemeCatalog\.NormalizeMode\(profile\.ThemeVariant\) != "System"\) return;' -and
     $vm -match 'ThemeCatalog\.BaseVariant\(profile\.ThemeVariant, ThemeApplier\.SystemPrefersLight\(\)\) == "Light"' -and
     $displayStore -match '"display-preferences\.json"' -and $displayStore -match 'ThemeCatalog\.NormalizeDensity') -Severity Critical

$artHarness816 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.16.0: the ArtworkHarness checks every mode (stored per tab, contrast ratios, true black, white borders), following the system both ways, and density (isolated store)' `
    ($artHarness816 -match 'on a card meet' -and $artHarness816 -match 'Midnight: true black backgrounds' -and
     $artHarness816 -match 'High contrast: white card borders and borders on black' -and
     $artHarness816 -match 'Follow the system: the system switching to dark switches the tab to Dark' -and
     $artHarness816 -match 'a tab set to Light ignores the system switching' -and
     $artHarness816 -match 'Compact density: card padding 7' -and
     $artHarness816 -match 'displayPreferencesStore: Isolate\(new LocalDisplayPreferencesStore\(\)\)') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.16.0-theme-modes.md' 'Regression' 'v0.8.16.0: v0.8.16.0 architecture doc is present' -Severity Critical

$policyRules = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ServerResourcePolicy.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.17.0: the rules: Default leaves priority alone, eco runs below normal, eco when empty never starts on an unknown player count, efficiency is only undone where MystTiq set it, 1..240 minutes' `
    ($policyRules -match 'public enum ServerPriorityLevel \{ Default, BelowNormal, Normal, AboveNormal, High \}' -and
     $policyRules -match 'public enum ServerEcoMode \{ Off, On, WhenEmpty \}' -and
     $policyRules -match 'playersOnline == 0 \? previous \?\? now : null' -and $policyRules -match 'who is online cannot be read' -and
     $policyRules -match 'ecoActive\s*\?\s*ProcessPriorityClass\.BelowNormal' -and
     $policyRules -match 'ecoActive \? true : appliedByMystTiq \? false : null' -and
     $policyRules -match 'MinimumEmptyMinutes = 1;' -and $policyRules -match 'MaximumEmptyMinutes = 240;') -Severity Critical

$control = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ProcessResourceControl.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.17.0: the platform switches: Windows priority class and EcoQoS (ProcessPowerThrottling); Linux niceness on every thread, with the privilege needed to raise it named' `
    ($control -match 'private const int ProcessPowerThrottling = 4;' -and $control -match 'SetProcessInformation\(handle, ProcessPowerThrottling' -and
     $control -match 'GetProcessInformation\(handle, ProcessPowerThrottling' -and
     $control -match '/proc/\{processId\}/task' -and $control -match 'setpriority\(PrioProcess, thread, nice\)' -and
     $control -match 'needs root or CAP_SYS_NICE' -and $control -match 'public bool SupportsEfficiencyMode => false;') -Severity Critical

$policyService = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessResourcePolicyService.cs'
$automationText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAutomationService.cs'
$apiHost = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.17.0: the policy is kept per server, applied to every managed process on save and on every automation tick, changed only where it differs; reading is Viewer, saving Admin, both scoped' `
    ($policyService -match '"resource-policy\.json"' -and $policyService -match 'foreach \(var process in status\.Processes\)' -and
     $policyService -match 'current != wanted' -and $policyService -match 'var snapshot = await ApplyAsync\(token\);' -and
     $automationText -match 'await resourcePolicy\.ApplyAsync\(token\);' -and
     $apiHost -match 'routes\.MapGet\("/host"' -and $apiHost -match '\}\)\.RequireRole\(MystTiqRole\.Viewer, p\.Id\);' -and
     $apiHost -match 'routes\.MapPut\("/resources/policy"[\s\S]{0,400}\.RequireRole\(MystTiqRole\.Admin, p\.Id\);' -and
     $apiHost -match 'players\.Available \? players\.OnlineCount : null') -Severity Critical

$hostMonitor = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessHostMonitor.cs'
$hostReader = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\HostMetricsReader.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.17.0: the machine is read the way the OS counts it (GetSystemTimes/GlobalMemoryStatusEx, /proc/stat and /proc/meminfo), rates from the previous reading, disks marked with what of the server they hold' `
    ($hostReader -match 'GetSystemTimes\(out var idle, out var kernel, out var user\)' -and $hostReader -match 'GlobalMemoryStatusEx' -and
     $hostReader -match '"/proc/stat"' -and $hostReader -match '"MemAvailable:"' -and
     $hostMonitor -match 'HostMetricsReader\.CpuPercent\(previous\.Times, times\)' -and $hostMonitor -match 'NetworkInterfaceType\.Loopback or NetworkInterfaceType\.Tunnel' -and
     $hostMonitor -match 'if \(IsFilterLayer\(nic\.Name, names\)\) continue;' -and $hostMonitor -match 'stats\.BytesReceived == 0 && stats\.BytesSent == 0' -and
     $hostMonitor -match 'OrderByDescending\(d => roles\.ContainsKey\(d\.Name\)\)' -and
     $apiHost -match '\("install", p\.Paths\.ServerRoot\), \("saves", p\.Paths\.SaveRoot\), \("backups", p\.Paths\.BackupRoot\)') -Severity Critical

$nav = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\NavigationPage.cs'
$hostVm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.Host.cs'
$enJson = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Assets\i18n\en.json'
$deJson = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Assets\i18n\de.json'
$esJson = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Assets\i18n\es.json'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.17.0: the Desktop has a HOST tab (appended page, three languages), readings on the 5-second tick, a read-only priority card below Admin, and unsaved edits kept' `
    ($nav -match 'Map,\s*//[^\n]*\n\s*Host\s*\}' -and
     $xaml -match 'Classes="categoryTab host" IsChecked="\{Binding IsV5HostCategory, Mode=OneWay\}"' -and
     $xaml -match 'x:Name="HostPriorityCard" Classes="card accentSystem" IsEnabled="\{Binding CanManageAdmin\}"' -and
     $vm -match 'if \(IsHostPage\) await RefreshHostAsync\(silent: true\);' -and
     $xaml.IndexOf('x:Name="HostMachineCard"') -lt $xaml.IndexOf('x:Name="HostPriorityCard"') -and
     $xaml.IndexOf('x:Name="HostPriorityCard"') -lt $xaml.IndexOf('x:Name="HostNetworkCard"') -and
     $xaml.IndexOf('x:Name="HostNetworkCard"') -lt $xaml.IndexOf('x:Name="HostDisksCard"') -and
     $hostVm -match 'if \(!_resourcePolicyEdited\) LoadPolicyFields' -and
     (@($enJson, $deJson, $esJson) | Where-Object { $_ -match '"category\.host":' -and $_ -match '"nav\.Host":' -and $_ -match '"page\.Host\.title":' -and $_ -match '"page\.Host\.subtitle":' -and $_ -match '"ribbon\.group\.Host":' }).Count -eq 3) -Severity Critical

$harness817 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$artHarness817 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
$smoke817 = Get-MystTiqText $ctx 'scripts\Test-v0.8.17.0-RouteSmoke.ps1'
$linux817 = Get-MystTiqText $ctx 'scripts\Test-v0.8.17.0-LinuxIsolated.sh'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.17.0: the harnesses, the Windows smoke (real process priority and efficiency) and the Linux check (niceness on every thread) cover it' `
    ($harness817 -match 'RunScenario\("Priority and eco mode: the rules' -and $harness817 -match 'RunScenario\("Priority and eco mode: the service' -and
     $artHarness817 -match 'all 8 category tabs fit the category bar' -and $artHarness817 -match '"Server priority and eco mode", RoleAccess\.Viewer, RoleAccess\.Admin' -and
     $artHarness817 -match 'an unsaved choice is not overwritten by the next reading' -and
     $smoke817 -match 'eco mode on: efficiency mode on the real process' -and $smoke817 -match 'after a crash the restarted server gets the policy again' -and
     $linux817 -match 'below normal sets niceness 10 on every thread' -and $linux817 -notmatch '/etc/mysttiq/') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.17.0-host-tab-and-priority.md' 'Regression' 'v0.8.17.0: v0.8.17.0 architecture doc is present' -Severity Critical

$netPolicy = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ServerNetworkPolicy.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.18.0: the policy: game defaults or custom limits (0.25..100 Mbit/s per player, 10..120 updates), the game''s real defaults (64 Mbit/s, 60 updates, 20 on Linux), the planner with 80 % headroom' `
    ($netPolicy -match 'public enum ServerNetworkMode \{ GameDefault, Custom \}' -and
     $netPolicy -match 'MinimumPerPlayerMbps = 0\.25;' -and $netPolicy -match 'MaximumPerPlayerMbps = 100;' -and
     $netPolicy -match 'MinimumTickRate = 10;' -and $netPolicy -match 'MaximumTickRate = 120;' -and
     $netPolicy -match 'PerPlayerBytesPerSecond = 8_000_000;' -and $netPolicy -match 'TickRate\(bool linux\) => linux \? 20 : 60;' -and
     $netPolicy -match 'public const double Headroom = 0\.8;') -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.8.18.0: Engine.ini edits touch only MaxClientRate, MaxInternetClientRate and NetServerMaxTickRate in [/Script/OnlineSubsystemUtils.IpNetDriver] (any case), keep every other line and the line endings, and keep the original once' `
    ($netPolicy -match 'public const string Section = "\[/Script/OnlineSubsystemUtils\.IpNetDriver\]";' -and
     $netPolicy -match 'Keys = \["MaxClientRate", "MaxInternetClientRate", "NetServerMaxTickRate"\]' -and
     $netPolicy -match 'string\.Equals\(trimmed, Section, StringComparison\.OrdinalIgnoreCase\)' -and
     $netPolicy -match 'Contains\("\\r\\n", StringComparison\.Ordinal\) \? "\\r\\n" : "\\n"' -and
     $netPolicy -match '"\.mysttiq-original"' -and $netPolicy -match 'if \(string\.Equals\(before, after, StringComparison\.Ordinal\)\) return null;') -Severity Critical

$winLifecycle = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsServerLifecycleService.cs'
$linLifecycle = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs'
$netService = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessNetworkPolicyService.cs'
$apiHost818 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.18.0: the limits are written before every start on both platforms (every start path), never blocking it; saving while running waits for the next start; reading Viewer, saving Admin' `
    ($winLifecycle -match 'var networkLine = EngineNetworkSettings\.ApplyBeforeStart\(paths\);' -and
     $linLifecycle -match 'var networkLine = EngineNetworkSettings\.ApplyBeforeStart\(paths\);' -and
     $netPolicy -match 'could not be written, the server starts with what it has' -and
     $netService -match 'if \(!running\) written = EngineNetworkSettings\.ApplyBeforeStart\(paths\);' -and $netService -match 'it takes effect at the next start' -and
     $apiHost818 -match 'routes\.MapGet\("/network/policy"[\s\S]{0,200}\.RequireRole\(MystTiqRole\.Viewer, p\.Id\);' -and
     $apiHost818 -match 'routes\.MapPut\("/network/policy"[\s\S]{0,400}\.RequireRole\(MystTiqRole\.Admin, p\.Id\);') -Severity Critical

$bandwidthVm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.Bandwidth.cs'
$harness818 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$artHarness818 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
$smoke818 = Get-MystTiqText $ctx 'scripts\Test-v0.8.18.0-RouteSmoke.ps1'
$linux818 = Get-MystTiqText $ctx 'scripts\Test-v0.8.18.0-LinuxIsolated.sh'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.18.0: the HOST tab has a read-only-below-Admin bandwidth card with the estimate and suggestion; harnesses, the Windows smoke (supervisor restarts) and the Linux start path cover it' `
    ($xaml -match 'x:Name="HostBandwidthCard" Classes="card accentSystem" IsEnabled="\{Binding CanManageAdmin\}"' -and
     $bandwidthVm -match 'if \(!_bandwidthEdited\)' -and $bandwidthVm -match 'ApplyBandwidthSuggestionCommand' -and
     $harness818 -match 'RunScenario\("Bandwidth: Engine\.ini gets exactly' -and $harness818 -match 'RunScenario\("Bandwidth: validation' -and
     $artHarness818 -match '"Bandwidth limits", RoleAccess\.Viewer, RoleAccess\.Admin' -and $artHarness818 -match 'the worst case, the over-upload warning and a suggestion that fits' -and
     $smoke818 -match 'the next start \(a supervisor restart\) writes the limits' -and $smoke818 -match 'when the engine drops the limits' -and
     $linux818 -match 'the Linux start path writes the new limits before the server starts') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.18.0-bandwidth.md' 'Regression' 'v0.8.18.0: v0.8.18.0 architecture doc is present' -Severity Critical

$rbac = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\RbacEndpointExtensions.cs'
$apiHost819 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.19.0: secure by default: RequireRole records its role, and each server''s route group refuses a route that declares none below Viewer (reading) or Admin (changing), scoped to that server' `
    ($rbac -match 'builder\.WithMetadata\(new RequiredRoleMetadata\(minimum\)\)' -and
     $rbac -match 'HttpMethods\.IsGet\(method\) \|\| HttpMethods\.IsHead\(method\) \? MystTiqRole\.Viewer : MystTiqRole\.Admin' -and
     $rbac -match 'GetMetadata<RequiredRoleMetadata>\(\) is not null' -and
     $apiHost819 -match 'MapProfileRoutes\(app\.MapGroup\(\$"/api/v1/servers/\{profileHost\.Id\.Value\}"\)\.RequireRoleByDefault\(profileHost\.Id\)' -and
     $apiHost819 -match 'MapProfileRoutes\(app\.MapGroup\("/api/v1"\)\.RequireRoleByDefault\(profileHost\.Id\)') -Severity Critical

# Every fleet-level route (not in a server's group) must declare a role, except sign-in and health.
$appRoutes = [regex]::Matches($apiHost819, 'app\.Map(Get|Post|Put|Delete)\("([^"]+)"')
$unguarded = @()
for ($ri = 0; $ri -lt $appRoutes.Count; $ri++) {
    $start = $appRoutes[$ri].Index
    $next = [regex]::Match($apiHost819.Substring($start + 1), '(app|routes)\.Map(Get|Post|Put|Delete)\(')
    $chunk = if ($next.Success) { $apiHost819.Substring($start, $next.Index + 1) } else { $apiHost819.Substring($start) }
    if ($chunk -notmatch 'RequireRole\(MystTiqRole\.') { $unguarded += $appRoutes[$ri].Groups[2].Value }
}
$exempt = @('/healthz', '/api/v1/auth/login', '/api/v1/auth/logout', '/api/v1/auth/password', '/api/v1/security/whoami')
$extra = @($unguarded | Where-Object { $_ -notin $exempt })
Add-MystTiqCheck $ctx 'Regression' 'v0.8.19.0: no fleet-level route is left without a role: only sign-in, sign-out, password change, whoami and the health check' `
    ($extra.Count -eq 0 -and @($unguarded).Count -eq $exempt.Count) -Severity Critical -Details ($(if ($extra.Count) { "Unguarded: $($extra -join ', ')" } else { '' }))

Add-MystTiqCheck $ctx 'Regression' 'v0.8.19.0: the Operator-level changes say so (save now, notifications, backup checks, crash analysis, update), everything else changing needs Admin: RCON commands, kick/ban, PalWorldSettings.ini, mods' `
    ($apiHost819 -match 'routes\.MapPost\("/world/save-now"[\s\S]{0,400}\}\)\.RequireRole\(MystTiqRole\.Operator, p\.Id\);' -and
     $apiHost819 -match 'routes\.MapPost\("/notifications/mark-all-read", \(\) => Results\.Ok\(p\.Notifications\.MarkAllRead\(\)\)\)\.RequireRole\(MystTiqRole\.Operator, p\.Id\);' -and
     $apiHost819 -match 'routes\.MapPost\("/server/distribution/update"[\s\S]{0,300}\}\)\.RequireRole\(MystTiqRole\.Operator, p\.Id\);' -and
     $apiHost819 -notmatch 'routes\.MapPost\("/rcon/command"[^;]*RequireRole\(MystTiqRole\.(Viewer|Operator)' -and
     $apiHost819 -notmatch 'routes\.MapPut\("/palworld/config"[^;]*RequireRole\(MystTiqRole\.(Viewer|Operator)') -Severity Critical

$ribbonRoles = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.RibbonRoles.cs'
$ribbonModel = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\RibbonActionViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.19.0: the Ribbon follows the signed-in role (disabled with the role in its tooltip), rebuilt when the role changes; kick, ban, teleport and RCON buttons need Admin' `
    ($ribbonRoles -match 'Need\("Operator", StartCommand' -and $ribbonRoles -match 'Need\("Admin", SavePalworldConfigurationCommand' -and
     $ribbonModel -match '\(needs the \{RequiredRole\} role\)' -and
     $vm -match '_allRibbonGroups = GateRibbonByRole\(LocalizeRibbon\(BuildRibbonGroupsForActivePage\(\)\)\);' -and
     $vm -match '// v0\.8\.19\.0: the Ribbon follows the role too\.\s*RebuildRibbonGroups\(\);' -and
     $xaml -match 'IsEnabled="\{Binding RoleAllowed\}" ToolTip\.Tip="\{Binding ToolTipText\}"' -and
     $xaml -match 'Content="Kick" Command="\{Binding KickSelectedPlayerCommand\}" IsEnabled="\{Binding CanManageAdmin\}"' -and
     $xaml -match 'Command="\{Binding RconSendCommand\}" IsEnabled="\{Binding CanManageAdmin\}"') -Severity Critical

$harness819 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$artHarness819 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
$smoke819 = Get-MystTiqText $ctx 'scripts\Test-v0.8.19.0-RouteSmoke.ps1'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.19.0: the harnesses and the smoke (authentication on: Viewer, Operator, Admin and another server''s account) cover it' `
    ($harness819 -match 'RunScenario\("Route roles:' -and $artHarness819 -match 'Ribbon buttons are enabled exactly for the roles their routes allow' -and
     $smoke819 -match 'the routes that had no role are refused below Admin' -and $smoke819 -match 'an account limited to another server is refused on this one') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.19.0-route-roles.md' 'Regression' 'v0.8.19.0: v0.8.19.0 architecture doc is present' -Severity Critical

$hostHistory = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessHostHistoryService.cs'
$apiHost820 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.20.0: one reading a minute (the first at start), kept 7 days in the fleet folder, the busiest adapter only (never added up), thinned by averaging with peaks from every reading' `
    ($hostHistory -match 'Retention = TimeSpan\.FromDays\(7\);' -and $hostHistory -match 'interval \?\? TimeSpan\.FromMinutes\(1\)' -and
     $hostHistory -match 'await SampleOnceAsync\(token\);\s*using var timer' -and $hostHistory -match 'snapshot\.Network\.FirstOrDefault\(\)' -and
     $hostHistory -match 'Max\(s => s\.CpuPercent\)' -and $hostHistory -match 'Math\.Clamp\(maximumPoints, 30, 1200\)' -and
     $apiHost820 -match 'new HeadlessHostHistoryService\(Path\.Combine\(configuration\.FleetRoot, "host"\), hostMonitor\)' -and
     $apiHost820 -match 'routes\.MapGet\("/host/history"[\s\S]{0,200}\.RequireRole\(MystTiqRole\.Viewer, p\.Id\);' -and
     $apiHost820 -match 'HostHistory is \{ \} history\) await history\.StartAsync' -and $apiHost820 -match 'HostHistory is \{ \} history\) await history\.StopAsync') -Severity Critical

$historyVm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.HostHistory.cs'
$historyChart = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Controls\HostHistoryChart.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.20.0: the HOST tab shows the history (hour, day, week) with its summary; a reading without a value is a gap, not a zero; it reloads about once a minute' `
    ($xaml -match 'x:Name="HostHistoryCard"' -and $xaml -match 'controls:HostHistoryChart x:Name="HostHistoryChart" Samples="\{Binding HostHistorySamples\}"' -and
     $historyVm -match 'HistoryRangeHours = \[1, 24, 24 \* 7\]' -and $historyVm -match '_hostHistoryTick\+\+ % 12 != 0' -and
     $historyChart -match 'value is \{ \} v && max > 0' -and
     $xaml.IndexOf('x:Name="HostMachineCard"') -lt $xaml.IndexOf('x:Name="HostHistoryCard"') -and
     $xaml.IndexOf('x:Name="HostHistoryCard"') -lt $xaml.IndexOf('x:Name="HostPriorityCard"')) -Severity Critical

$harness820 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$artHarness820 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
$smoke820 = Get-MystTiqText $ctx 'scripts\Test-v0.8.20.0-RouteSmoke.ps1'
$linux820 = Get-MystTiqText $ctx 'scripts\Test-v0.8.20.0-LinuxIsolated.sh'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.20.0: the harnesses, the Windows smoke (a reading at start, the fleet folder, a restart) and the Linux check (/proc) cover it' `
    ($harness820 -match 'RunScenario\("Host history:' -and $artHarness820 -match 'the history summary reads plainly' -and
     $smoke820 -match 'the history survives a restart of MystTiq' -and $linux820 -match 'a reading is taken at start from /proc') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.20.0-host-history.md' 'Regression' 'v0.8.20.0: v0.8.20.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.8.21.0 Contracts -- the Linux service may raise the server's priority again
# ---------------------------------------------------------------------------
$systemdUnit = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\LinuxSystemdServiceManager.cs'
$program821 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\Program.cs'
$control821 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ProcessResourceControl.cs'
Add-MystTiqCheck $ctx 'v0.8.21.0 Contracts' 'the systemd unit sets LimitNICE=-11 (no capability, NoNewPrivileges kept); service-unit prints it without installing; the Linux refusal says to reinstall the service' `
    ($systemdUnit -match '"LimitNICE=-11\\n"' -and $systemdUnit -match '"NoNewPrivileges=true\\n"' -and $systemdUnit -notmatch 'AmbientCapabilities' -and
     $systemdUnit -match 'public static string BuildUnitText\(string serviceUser, string configurationPath, ServerProfileId profileId\)' -and
     $program821 -match 'Console\.Write\(LinuxSystemdServiceManager\.BuildUnitText\(' -and $program821 -match '"service-unit"' -and
     $control821 -match 'needs root or CAP_SYS_NICE, or the MystTiq service installed by v0\.8\.21\.0 or later') -Severity Critical

$harness821 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$linux821 = Get-MystTiqText $ctx 'scripts\Test-v0.8.21.0-LinuxIsolated.sh'
Add-MystTiqCheck $ctx 'v0.8.21.0 Contracts' 'the harness checks the unit on any OS, and the Linux check has systemd itself verify it (installing nothing)' `
    ($harness821 -match 'RunScenario\("Linux service unit:' -and $linux821 -match 'systemd-analyze verify' -and $linux821 -match 'service-unit --config' -and
     $linux821 -notmatch '"\$APP" service-install') -Severity Critical
# v0.8.21.0: test runs keep the fleet's shared state out of the real fleet folder. --fleet-root overrides FleetRoot for
# one run; every smoke this gate starts api-run from passes it or writes its own FleetRoot into its config.
Add-MystTiqCheck $ctx 'v0.8.21.0 Contracts' 'api-run takes --fleet-root (validated like the config value, never written back)' `
    ($program821 -match 'FleetRoot = GetOption\("--fleet-root"\) \?\? headlessConfiguration\.FleetRoot' -and $program821 -match '--fleet-root <path>') -Severity Critical
$gateSelf821 = Get-MystTiqText $ctx 'scripts\Test-v0.8.21.0-Logic.ps1'
$unisolated821 = @([regex]::Matches($gateSelf821, "scripts\\(Test-v[0-9.]+-[A-Za-z]+\.ps1)") | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique | Where-Object {
    $p = Join-Path $root "scripts\$_"
    if (-not (Test-Path $p)) { return $false }
    $s = Get-Content $p -Raw
    $s -match "'api-run'" -and $s -notmatch '--fleet-root' -and $s -notmatch '\.FleetRoot = '
})
Add-MystTiqCheck $ctx 'v0.8.21.0 Contracts' 'every smoke this gate runs gives api-run its own fleet folder (none uses the real one)' ($unisolated821.Count -eq 0) -Severity Critical -Details ("Not isolated: " + ($unisolated821 -join ', '))
# v0.8.21.0: a generated gate once joined two commands on one line (a second Test-MystTiqCommand after Out-Null): it parses,
# then stops the gate at run time. No code line of this gate may continue after Out-Null.
$joined821 = @(($gateSelf821 -split "`n") | Where-Object { $_ -notmatch '^\s*#' -and $_ -match '\| Out-Null[ \t]+[^ \t\r#]' })
Add-MystTiqCheck $ctx 'v0.8.21.0 Contracts' 'no line of this gate runs a second command after "| Out-Null"' ($joined821.Count -eq 0) -Severity Critical -Details ($joined821 -join ' / ')
Test-MystTiqFile $ctx 'scripts\Test-v0.8.21.0-RouteSmoke.ps1' 'v0.8.21.0 Contracts' 'v0.8.21.0 fleet-root smoke is present' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.21.0-linux-priority.md' 'v0.8.21.0 Contracts' 'v0.8.21.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.8.21.0 is documented' ([regex]::IsMatch($docText, 'v0\.8\.21\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.8.21.0.md' 'Documentation' 'v0.8.21.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.8.21.0 entry' ($changelogText -match '## v0\.8\.21\.0') -Severity High
$readmeText = Get-MystTiqText $ctx 'README.md'
Add-MystTiqCheck $ctx 'Documentation' 'README records the accepted baseline v0.8.1.0' ($readmeText -match 'Accepted baseline:\*\* v0\.8\.1\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = if ($FrozenBaselineZip) { $FrozenBaselineZip } else { Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.8.20.0\MystTiqPalworldServer_v0.8.20.0_FullSource.zip' }
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        # v0.8.1.0: reported, not fatal: the rest of the gate is still worth running on a machine without it.
        Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.8.20.0 checkpoint logic gate still passes' $false -Skipped -Severity Critical `
            -Details "Baseline archive not found at $frozenZip. Run on the release machine, or pass -FrozenBaselineZip <path>."
    }
    else {
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.8.20.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.8.20.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.8.20.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.8.20.0 checkpoint logic gate still passes' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))
    }

    # v0.8.1.0: -AllowBuildOutputs, because this gate has already built and published by now; repository
    # hygiene (no bin/obj/artifacts) is checked by the release pipeline after Clean instead. This check used to
    # fail in every gate for exactly that reason.
    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'scripts\Validate-Release.ps1') -Strict -AllowBuildOutputs
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release solution build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression Runtime' 'v0.5.1.5 isolated runtime smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.5.1.5-RuntimeSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.12.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.12.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.15.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.15.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.17.0 api-remote-enable smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.64.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.64.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.81.0 new-server wizard route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.81.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.97.0 crash analyzer route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.97.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.98.0 doctor route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.98.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.100.0 player locations route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.100.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.101.0 crash alerts route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.101.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.102.0 alert episodes route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.102.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.103.0 backup schedule route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.103.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.104.0 alert unpin route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.104.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.107.0 alert reminder route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.107.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.108.0 configured reminder route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.108.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.110.0 crash-recovery give-up/manual-recovery route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.110.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.111.0 alert mute route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.111.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.112.0 give item route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.112.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.113.0 teleport points route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.113.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.114.0 multi-user login route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.114.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.115.0 persistence and readiness route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.115.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.2.0 service mode route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.2.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.3.0 give item catalogue route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.3.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.4.0 delivery pause route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.4.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.9.0: Unreal crash reports read by the analyzer and announced by the watcher, on the published exe.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.9.0 crash report route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.9.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.11.0: owned Pals' positions in the explorer snapshot, on the published exe.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.11.0 Pal positions route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.11.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.12.0: the second-NAT check in the WAN report, on the published exe (read-only network lookups).
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.12.0 second NAT route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.12.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.13.0: game names from a synthetic pak, on the published exe.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.13.0 game names route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.13.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v0.8.17.0: the HOST route and process priority / eco mode on the published exe, checked on the real process.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.17.0 host and priority route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.17.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v0.8.18.0: bandwidth on the published exe in service mode; the restarts come from the supervisor.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.18.0 bandwidth route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.18.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v0.8.19.0: every route's role, with authentication really on, on the published exe.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.19.0 route roles smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.19.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v0.8.20.0: the host history on the published exe.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.20.0 host history smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.20.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    Test-MystTiqCommand $ctx 'v0.8.21.0 Runtime' 'v0.8.21.0 fleet-root smoke passes (the fleet state goes where --fleet-root says)' {
        & (Join-Path $root 'scripts\Test-v0.8.21.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.1.0: the artwork is Desktop-only, so its runtime proof is the offline headless rendering harness
    # (every page's day/night art, visible bindings, header/ribbon/category layout at 950x650, all 26 icons).
    # v0.8.5.0: the display language is Desktop-only, so its runtime proof is this headless render: German and Spanish
    # applied live, the tabs fitting at 950x650, and back to English.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'MystTiq.ArtworkHarness still passes (offline headless rendering)' {
        $renderOut = Join-Path ([System.IO.Path]::GetTempPath()) ('mysttiq-artwork-render-' + [guid]::NewGuid().ToString('N'))
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.ArtworkHarness')
        try { & dotnet run -c Release -- $renderOut; if ($LASTEXITCODE -ne 0) { throw "artwork harness exited $LASTEXITCODE" } }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.ArtworkHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.ArtworkHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item $renderOut -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null

    # v0.8.15.0: the real remote sign-in, against an isolated instance on the Linux test VM (never its installed
    # service). The VM is a separate machine on DHCP: when it does not answer on SSH the check is reported as SKIP.
    $remoteHost = if ($env:MYSTTIQ_LINUX_HOST) { $env:MYSTTIQ_LINUX_HOST } else { '192.168.1.122' }
    $probe = [System.Net.Sockets.TcpClient]::new()
    $reachable = try { $probe.ConnectAsync($remoteHost, 22).Wait(3000) -and $probe.Connected } catch { $false } finally { $probe.Dispose() }
    if ($reachable) {
        # v0.8.21.0: the systemd unit, printed by service-unit and verified by systemd on the VM (installs nothing).
        Test-MystTiqCommand $ctx 'v0.8.21.0 Runtime' 'the Linux service unit (LimitNICE) is accepted by systemd on the VM' {
            & (Join-Path $root 'scripts\Test-v0.8.21.0-LinuxIsolated.ps1') -ProjectRoot $root -LinuxHost $remoteHost
        } -Severity Critical | Out-Null
        Test-MystTiqCommand $ctx 'Regression New Tests' 'the Desktop still signs in to a real remote MystTiq (Linux VM) as Viewer, Operator and Admin' {
            try { & (Join-Path $root 'scripts\Test-v0.8.15.0-RemoteSignIn.ps1') -ProjectRoot $root -LinuxHost $remoteHost }
            finally {
                Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.RemoteSignInHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
                Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.RemoteSignInHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
            }
        } -Severity Critical | Out-Null
    }
    else {
        Add-MystTiqCheck $ctx 'v0.8.21.0 Runtime' 'the Linux service unit (LimitNICE) is accepted by systemd on the VM' $false -Skipped -Severity High -Details "The Linux test VM ($remoteHost) does not answer on SSH."
        Add-MystTiqCheck $ctx 'Regression New Tests' 'the Desktop still signs in to a real remote MystTiq (Linux VM) as Viewer, Operator and Admin' $false -Skipped -Severity High `
            -Details "The Linux test VM ($remoteHost) does not answer on SSH. Set MYSTTIQ_LINUX_HOST to its address."
    }
    Test-MystTiqCommand $ctx 'Regression New Tests' 'MystTiq.LogicHarness passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'MystTiq.Desktop builds clean (not part of PalworldServerManager.slnx)' {
        & dotnet build (Join-Path $root 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -c Release
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
