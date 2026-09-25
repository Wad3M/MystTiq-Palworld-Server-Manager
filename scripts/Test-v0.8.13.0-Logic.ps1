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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.8.13.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.8\.13\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.8.13.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.8\.13\.0"' `
    'Versioning' 'app.manifest reports v0.8.13.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.8.12.0-Logic.ps1' 'Regression' 'v0.8.12.0 logic gate remains available' -Severity High

$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.0.0 artwork remains: themed page art, header art filling the card, 50px nav icons, v0.7.115.0 map label layer' `
    ($xaml -match 'Source="\{Binding PageArtwork\}"' -and $xaml -match 'x:Name="PageArtHeader"[^>]*Padding="0"' -and
     ([regex]::Matches($xaml, '\{services:IconArt [^}]+\}" Width="50" Height="50"')).Count -eq 26 -and
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
     ([regex]::Matches($xaml, '<TextBlock Grid\.Column="1" Text="\{services:Tr nav\.')).Count -eq 26 -and
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
     $vm -match '_allRibbonGroups = LocalizeRibbon\(BuildRibbonGroupsForActivePage\(\)\);' -and
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

# ---------------------------------------------------------------------------
# 3. v0.8.13.0 Contracts -- item and Pal display names from the game pak, and the last ten Ribbon images
# ---------------------------------------------------------------------------
$icons813 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\RibbonIcons.cs'
$labels = @([regex]::Matches($vm, 'new\("[^"]*", "([^"]+)", "[^"]*", \w+') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
$imageBlock = $icons813.Substring($icons813.IndexOf('ImageByLabel = new')); $imageBlock = $imageBlock.Substring(0, $imageBlock.IndexOf('};'))
$imageLabels = @([regex]::Matches($imageBlock, '\["([^"]+)"\] = "') | ForEach-Object { $_.Groups[1].Value })
$noImage = @($labels | Where-Object { $_ -notin $imageLabels })
Add-MystTiqCheck $ctx 'v0.8.13.0 Contracts' 'every Ribbon label has an image: the last ten (batch 4) are mapped, the Refresh variants and Restart Server share theirs' `
    ($labels.Count -eq 50 -and $noImage.Count -eq 0 -and $icons813 -match '\["Refresh History"\] = "refresh"' -and $icons813 -match '\["Restart Server"\] = "restart"' -and
     $icons813 -match '\["Run Analysis"\] = "run_analysis", \["Preview Plan"\] = "preview_plan"') `
    -Severity Critical -Details "without an image: $($noImage -join ', ')"

$names = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessGameNameService.cs'
$extractor = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\Tools\extract_game_names.py'
$headlessProj = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj'
Add-MystTiqCheck $ctx 'v0.8.13.0 Contracts' 'the extractor is MystTiq''s own read-only script, shipped beside the executable; it needs a working ooz only for Oodle files' `
    ($headlessProj -match 'Include="Tools\\extract_game_names\.py" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" ExcludeFromSingleFile="true"' -and
     $extractor -match 'if version != 11: raise Fail\(3' -and $extractor -match 'if ooz is None or not hasattr\(ooz, "decompress"\):' -and
     $extractor -match 'ITEM_NAME_' -and $extractor -match 'PAL_NAME_' -and $extractor -match 'ensure_ascii=True') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.8.13.0 Contracts' 'names are cached per pak (path, size, time), a failure is retried after 10 minutes, and every reason is reported' `
    ($names -match 'Path\.Combine\(paths\.ManagerRuntimeRoot, "game-names", \$"\{Language\}\.json"\)' -and
     $names -match 'public static readonly TimeSpan RetryAfterFailure = TimeSpan\.FromMinutes\(10\);' -and
     $names -match '2 => \(null, "Python''s Oodle module \(ooz\) is not installed"\)' -and $names -match 'return \(null, "Python was not found"\);' -and
     $names -match 'if \(dir\.Contains\("WindowsApps", StringComparison\.OrdinalIgnoreCase\)\) continue;' -and $names -match 'private static string LastLine\(string text\)') `
    -Severity Critical

$catalogSvc = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessGameIdCatalogService.cs'
$explorer813 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessPlayerGuildExplorerService.cs'
Add-MystTiqCheck $ctx 'v0.8.13.0 Contracts' 'the Give Item catalogue names its rows and adds the game-only ids (marked), seen ones first; Pal positions carry the species name' `
    ($catalogSvc -match 'public static IReadOnlyList<GameIdCatalogEntry> WithNames' -and $catalogSvc -match 'InGameFiles: true' -and
     $catalogSvc -match '\.ThenBy\(r => r\.InGameFiles \? 1 : 0\)' -and $explorer813 -match 'names\.PalName\(pal\.Species\) \?\? string\.Empty' -and
     $apiHost -match 'var gameNames = new HeadlessGameNameService\(paths\);') `
    -Severity Critical

$search = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\GameIdSearch.cs'
$kitDtos = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\KitDtos.cs'
$palLayout813 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\PalMapLayout.cs'
Add-MystTiqCheck $ctx 'v0.8.13.0 Contracts' 'the Desktop searches and shows names ("Name (id)", "not yet seen on this server"), and the map uses species names' `
    ($search -match 'public static bool Matches\(string\? id, string\? name, string\? query\)' -and $vm -match 'GameIdSearch\.Matches\(e\.Id, e\.Name, GameIdFilterText\)' -and
     $kitDtos -match 'not yet seen on this server' -and $kitDtos -match '\$"\{Name\} \(\{Id\}\)"' -and
     $palLayout813 -match 'public static string Species\(PalMapEntry pal\)' -and $xaml -match 'Watermark="Search names or ids, e\.g\. pal sphere, lamball"' -and
     $vm -match ': _gameIdCatalogDetail;') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$artHarness813 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.8.13.0 Contracts' 'the harnesses cover names (lookups, catalogue order, search, the service''s cache and failures) and every page''s Ribbon images' `
    (([regex]::Matches($harness, 'RunScenario\("Game names: ')).Count -eq 2 -and
     $artHarness813 -match 'RibbonIcons\.ImageKeys\.Count == 40' -and $artHarness813 -match 'every Ribbon button has an image') -Severity Critical

$smoke = Get-MystTiqText $ctx 'scripts\Test-v0.8.13.0-RouteSmoke.ps1'
Add-MystTiqCheck $ctx 'v0.8.13.0 Contracts' 'the game names smoke uses its own FleetRoot and a synthetic pak (no game files), and the Linux check exists' `
    ($smoke -match '\$cfg\.FleetRoot = \$fleetRoot' -and $smoke -match 'make_test_pak\.py' -and
     (Test-Path (Join-Path $root 'scripts\Testing\make_test_pak.py')) -and (Test-Path (Join-Path $root 'scripts\Test-v0.8.13.0-LinuxIsolated.ps1'))) -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.13.0-game-names.md' 'v0.8.13.0 Contracts' 'v0.8.13.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.8.13.0 is documented' ([regex]::IsMatch($docText, 'v0\.8\.13\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.8.13.0.md' 'Documentation' 'v0.8.13.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.8.13.0 entry' ($changelogText -match '## v0\.8\.13\.0') -Severity High
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
    $frozenZip = if ($FrozenBaselineZip) { $FrozenBaselineZip } else { Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.8.12.0\MystTiqPalworldServer_v0.8.12.0_FullSource.zip' }
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        # v0.8.1.0: reported, not fatal: the rest of the gate is still worth running on a machine without it.
        Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.8.12.0 checkpoint logic gate still passes' $false -Skipped -Severity Critical `
            -Details "Baseline archive not found at $frozenZip. Run on the release machine, or pass -FrozenBaselineZip <path>."
    }
    else {
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.8.12.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.8.12.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.8.12.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.8.12.0 checkpoint logic gate still passes' `
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
    Test-MystTiqCommand $ctx 'v0.8.13.0 Runtime' 'v0.8.13.0 game names route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.8.13.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.1.0: the artwork is Desktop-only, so its runtime proof is the offline headless rendering harness
    # (every page's day/night art, visible bindings, header/ribbon/category layout at 950x650, all 26 icons).
    # v0.8.5.0: the display language is Desktop-only, so its runtime proof is this headless render: German and Spanish
    # applied live, the tabs fitting at 950x650, and back to English.
    Test-MystTiqCommand $ctx 'v0.8.13.0 Runtime' 'MystTiq.ArtworkHarness passes (offline headless rendering, incl. an image on every Ribbon button of every page)' {
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
