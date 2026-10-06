# MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '1.0.0.4' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>1\.0\.0\.4</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v1.0.0.4' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\MystTiq.Desktop\app.manifest' `
    'assemblyIdentity version="1\.0\.0\.4"' `
    'Versioning' 'the Desktop app.manifest reports v1.0.0.4' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v1.0.0.3-Logic.ps1' 'Regression' 'v1.0.0.3 logic gate remains available' -Severity High

$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
# v0.9.0.0: the window's texts are keys now ({services:Tr ui.*}); older checks read the XAML with each key's English text
# put back, XML-escaped as it was written, so they still check the same behaviour.
$enUi900 = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Assets\i18n\en.json') -Raw | ConvertFrom-Json -AsHashtable
$xaml = [regex]::Replace($xaml, '\{services:Tr (ui\.[\w.]+)\}', {
        param($m)
        $v = $enUi900[$m.Groups[1].Value]
        if ($null -eq $v) { return $m.Value }
        $v.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
    })
# v0.9.1.0: display bindings go through the message catalog ({services:TrText Path}); older checks read them as the
# {Binding Path} they were written as.
$xaml = $xaml -replace '\{services:TrText ([^}]+)\}', '{Binding $1}' -replace '\{services:TrText\}', '{Binding}'
# v0.9.7.0: Avalonia 12 renamed TextBox.Watermark to PlaceholderText; the contracts carried from earlier versions name Watermark.
$xaml = [regex]::Replace($xaml, '(?<=\s)PlaceholderText=', 'Watermark=')
# v0.8.17.0: 27 navigation icons with the HOST page.
Add-MystTiqCheck $ctx 'Regression' 'v0.8.0.0 artwork remains: themed page art, header art filling the card, 50px nav icons, v0.7.115.0 map label layer' `
    ($xaml -match 'Source="\{Binding PageArtwork\}"' -and $xaml -match 'x:Name="PageArtHeader"[^>]*Padding="0"' -and
     ([regex]::Matches($xaml, '\{services:IconArt [^}]+\}" Width="50" Height="50"')).Count -eq 28 -and
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
     ([regex]::Matches($xaml, '<TextBlock Grid\.Column="1" Text="\{services:Tr nav\.')).Count -eq 28 -and
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
    ($basesAt -gt 0 -and $basesAt -lt $palsAt -and $palsAt -lt $playersAt -and $xaml -match '<Ellipse Fill="(#9F7AEA|\{DynamicResource DecoBackground_9F7AEA\})"' -and
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
    ($names -match 'Path\.Combine\(paths\.ManagerRuntimeRoot, "game-names", \$"\{(Language|NormalizeLanguage\(language\))\}\.json"\)' -and
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
    (([regex]::Matches($harness, 'RunScenario\("Game names: ')).Count -ge 2 -and
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
     $themeApplier -match 'variant = (contrast is not null \? [^;]*: )?ThemeCatalog\.BaseVariant\(mode, SystemPrefersLight\(\)\);' -and
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
     $vm -match 'ThemeCatalog\.NormalizeMode\(profile\.ThemeVariant\) (!= "System"|is not \("System" or "HighContrast"\))\) return;' -and
     $vm -match '(ThemeCatalog\.BaseVariant\(profile\.ThemeVariant, ThemeApplier\.SystemPrefersLight\(\)\)|ThemeApplier\.ResolveVariant\(profile\.ThemeVariant\)) == "Light"' -and
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
    ($nav -match 'Map,\s*//[^\n]*\n\s*Host,\s*//[^\n]*\n\s*Launcher\s*\}' -and
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
     $artHarness817 -match 'all 8 category tabs fit the category bar' -and $artHarness817 -match '"Server priority(, eco mode and cores| and eco mode)", RoleAccess\.Viewer, RoleAccess\.Admin' -and
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

$systemdUnit = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\LinuxSystemdServiceManager.cs'
$program821 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\Program.cs'
$control821 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ProcessResourceControl.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.21.0: the systemd unit sets LimitNICE=-11 (no capability, NoNewPrivileges kept); service-unit prints it without installing; the Linux refusal says to reinstall the service' `
    ($systemdUnit -match '"LimitNICE=-11\\n"' -and $systemdUnit -match '"NoNewPrivileges=true\\n"' -and $systemdUnit -notmatch 'AmbientCapabilities' -and
     $systemdUnit -match 'public static string BuildUnitText\(string serviceUser, string configurationPath, ServerProfileId profileId\)' -and
     $program821 -match 'Console\.Write\(LinuxSystemdServiceManager\.BuildUnitText\(' -and $program821 -match '"service-unit"' -and
     $control821 -match 'needs root or CAP_SYS_NICE, or the MystTiq service installed by v0\.8\.21\.0 or later') -Severity Critical

$harness821 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$linux821 = Get-MystTiqText $ctx 'scripts\Test-v0.8.21.0-LinuxIsolated.sh'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.21.0: the harness checks the unit on any OS, and the Linux check has systemd itself verify it (installing nothing)' `
    ($harness821 -match 'RunScenario\("Linux service unit:' -and $linux821 -match 'systemd-analyze verify' -and $linux821 -match 'service-unit --config' -and
     $linux821 -notmatch '"\$APP" service-install') -Severity Critical
# v0.8.21.0: test runs keep the fleet's shared state out of the real fleet folder. --fleet-root overrides FleetRoot for
# one run; every smoke this gate starts api-run from passes it or writes its own FleetRoot into its config.
Add-MystTiqCheck $ctx 'Regression' 'v0.8.21.0: api-run takes --fleet-root (validated like the config value, never written back)' `
    ($program821 -match 'FleetRoot = GetOption\("--fleet-root"\) \?\? headlessConfiguration\.FleetRoot' -and $program821 -match '--fleet-root <path>') -Severity Critical
$gateSelf821 = Get-MystTiqText $ctx 'scripts\Test-v1.0.0.4-Logic.ps1'
$unisolated821 = @([regex]::Matches($gateSelf821, "scripts\\(Test-v[0-9.]+-[A-Za-z]+\.ps1)") | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique | Where-Object {
    $p = Join-Path $root "scripts\$_"
    if (-not (Test-Path $p)) { return $false }
    $s = Get-Content $p -Raw
    $s -match "'api-run'" -and $s -notmatch '--fleet-root' -and $s -notmatch '\.FleetRoot = '
})
Add-MystTiqCheck $ctx 'Regression' 'v0.8.21.0: every smoke this gate runs gives api-run its own fleet folder (none uses the real one)' ($unisolated821.Count -eq 0) -Severity Critical -Details ("Not isolated: " + ($unisolated821 -join ', '))
# v0.8.21.0: a generated gate once joined two commands on one line (a second Test-MystTiqCommand after Out-Null): it parses,
# then stops the gate at run time. No code line of this gate may continue after Out-Null.
$joined821 = @(($gateSelf821 -split "`n") | Where-Object { $_ -notmatch '^\s*#' -and $_ -match '\| Out-Null[ \t]+[^ \t\r#]' })
Add-MystTiqCheck $ctx 'Regression' 'v0.8.21.0: no line of this gate runs a second command after "| Out-Null"' ($joined821.Count -eq 0) -Severity Critical -Details ($joined821 -join ' / ')
Test-MystTiqFile $ctx 'scripts\Test-v0.8.21.0-RouteSmoke.ps1' 'Regression' 'v0.8.21.0: v0.8.21.0 fleet-root smoke is present' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.21.0-linux-priority.md' 'Regression' 'v0.8.21.0: v0.8.21.0 architecture doc is present' -Severity Critical

$rs822 = Get-MystTiqText $ctx 'scripts\Test-v0.8.22.0-RemoteSignIn.ps1'
$rsHarness822 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.RemoteSignInHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.22.0: the remote sign-in covers all four roles (Owner too), the Ribbon gating on every page in the signed-in window, and the server agreeing with each kind of gated button' `
    ($rs822 -match "Role = 'Owner'; Number = 3" -and $rsHarness822 -match 'the Ribbon follows the role on every page' -and
     $rsHarness822 -match 'Enum\.GetValues<NavigationPage>\(\)' -and $rsHarness822 -match 'enabled in the window' -and
     $rsHarness822 -match 'the page buttons bound to the role follow it' -and $rsHarness822 -match 'the server agrees with the gated buttons' -and
     $rsHarness822 -match 'MarkAllNotificationsReadAsync' -and $rsHarness822 -match 'SaveResourcePolicyAsync' -and $rsHarness822 -match 'GetUsersAsync') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.8.22.0: the Desktop itself runs on Linux: the harness is published for linux-x64 and run on the VM with its home in /tmp, the VM user''s settings checked untouched, and everything torn down' `
    ($rs822 -match 'MystTiq\.RemoteSignInHarness\.csproj.{0,40}-r linux-x64 --self-contained true' -and $rs822 -match 'HOME=\$dir/home XDG_CONFIG_HOME=' -and
     $rs822 -match "\`$dir = '/tmp/mysttiq-v0822-remote'" -and $rs822 -match 'the Linux run left the VM user''''s settings folders as they were' -and
     $rs822 -match 'finally \{\s*\$down = @\(ssh' -and @(($rs822 -split "`n") | Where-Object { $_ -notmatch '^\s*#' -and $_ -match '/etc/mysttiq' }).Count -eq 0) -Severity Critical
$window822 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$windowCode822 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.22.0: the window controls, settings and tab close icons are drawn (no Segoe Fluent Icons font, which Linux and Windows 10 lack), and the harness checks it' `
    ($window822 -notmatch 'FontFamily="Segoe Fluent Icons"' -and $window822 -match '<PathIcon x:Name="MaximizeGlyph"' -and
     $windowCode822 -match 'MaximizeGlyph\.Data = WindowState == WindowState\.Maximized \? RestoreIcon : MaximizeIcon' -and
     $rsHarness822 -match 'draw their icons') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.22.0-sign-in-tests.md' 'Regression' 'v0.8.22.0: v0.8.22.0 architecture doc is present' -Severity Critical

$vm823 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$table823 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.CommandRoles.cs'
$ribbon823 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.RibbonRoles.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.23.0: the command classes take a role gate that decides whether they can run; it is installed once every command exists and re-evaluated when the role changes; the Ribbon uses the same table' `
    ($vm823 -match 'internal interface IRoleGatedCommand' -and
     @([regex]::Matches($vm823, 'internal sealed class (RelayCommand|RelayCommand<T>|AsyncCommand) : ICommand, IRoleGatedCommand')).Count -eq 3 -and
     @([regex]::Matches($vm823, 'CanExecute\(object\? parameter\) => [^;]*\(RoleGate\?\.Invoke\(\) \?\? true\)')).Count -eq 3 -and
     $vm823 -match 'InstallCommandRoleGates\(\);\s*RebuildRibbonGroups\(\);' -and $vm823 -match 'RebuildRibbonGroups\(\);\s*// v0\.8\.23\.0: and so does every command, wherever its button is\.\s*RaiseCommandRoleGatesChanged\(\);' -and
     $table823 -match 'gated\.RoleGate = \(\) => RoleAccess\.Allows\(CurrentPrincipal is not null, CurrentPrincipal\?\.Role, minimum\)' -and
     $ribbon823 -match 'foreach \(var \(command, role\) in BuildCommandRoles\(\)\) roles\.TryAdd\(command, role\);') -Severity Critical

# The table must be exactly what the code needs: the audit follows every command to the routes it calls.
$audit823 = @(& (Join-Path $root 'scripts\Testing\Get-MystTiqCommandRoles.ps1') -ProjectRoot $root)
$unresolved823 = @(& (Join-Path $root 'scripts\Testing\Get-MystTiqCommandRoles.ps1') -ProjectRoot $root -ShowUnresolved)
$declared823 = @{}
foreach ($need in [regex]::Matches($table823, 'Need\("(\w+)",([^;]*)\);')) {
    foreach ($name in [regex]::Matches($need.Groups[2].Value, '\b(_?\w+Command)\b')) { $declared823[$name.Groups[1].Value] = $need.Groups[1].Value }
}
$derived823 = @{}; foreach ($row in $audit823 | Where-Object { $_.Kind -eq 'Command' -and $_.Role }) { $derived823[$row.Command] = $row.Role }
$tableWrong823 = @($derived823.Keys | Where-Object { $declared823[$_] -ne $derived823[$_] } | ForEach-Object { "$_ needs $($derived823[$_]), table: $($declared823[$_])" }) +
    @($declared823.Keys | Where-Object { -not $derived823.ContainsKey($_) } | ForEach-Object { "$_ is listed but needs no role" })
Add-MystTiqCheck $ctx 'Regression' 'v0.8.23.0: the role table matches the code: every command reaching a route above Viewer is listed with that route''s role, and nothing else is listed' `
    ($derived823.Count -ge 100 -and $tableWrong823.Count -eq 0) -Severity Critical -Details ($tableWrong823 -join ' / ')
Add-MystTiqCheck $ctx 'Regression' 'v0.8.23.0: every API client change is matched to a route (so no command''s role is guessed)' ($unresolved823.Count -eq 0) -Severity Critical -Details ($unresolved823 -join ' / ')

# Code-behind controls: their handler's role must be bound on every control using it (closing a tab stays open to all:
# it stops the server only if chosen in its dialog).
$xaml823 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$binding823 = @{ Operator = 'CanOperate'; Admin = 'CanManageAdmin'; Owner = 'CanManagePrincipals' }
$clickWrong823 = @()
foreach ($row in $audit823 | Where-Object { $_.Kind -eq 'Click' -and $_.Role -and $_.Command -ne 'CloseTabButton_OnClick' }) {
    $uses = @([regex]::Matches($xaml823, 'Click="' + $row.Command + '"( IsEnabled="\{Binding (\w+)\}")?'))
    if ($uses.Count -eq 0) { continue }
    foreach ($u in $uses) { if ($u.Groups[2].Value -ne $binding823[$row.Role]) { $clickWrong823 += "$($row.Command) ($($row.Role))" } }
}
Add-MystTiqCheck $ctx 'Regression' 'v0.8.23.0: every code-behind control whose handler needs a role is bound to that role' (@($audit823 | Where-Object { $_.Kind -eq 'Click' -and $_.Role }).Count -ge 10 -and $clickWrong823.Count -eq 0) -Severity Critical -Details ($clickWrong823 -join ' / ')

$art823 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
$rsHarness823 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.RemoteSignInHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.23.0: the harnesses check every gated command and every page''s controls for each role (offline, and signed in to the VM)' `
    ($art823 -match 'role-gated commands run only where the role allows' -and $art823 -match 'buttons for commands the role may not use are disabled' -and
     $rsHarness823 -match 'every page''s buttons for commands the role may not use are disabled') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.23.0-every-button-follows-the-role.md' 'Regression' 'v0.8.23.0: v0.8.23.0 architecture doc is present' -Severity Critical

$policy824 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ServerResourcePolicy.cs'
$control824 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ProcessResourceControl.cs'
$service824 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessResourcePolicyService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.24.0: the policy has cores, checked against the machine (at most 64); the service pins only what differs and gives every core back only where MystTiq pinned; Windows sets the mask, Linux every thread' `
    ($policy824 -match 'string\? Cores = null' -and $policy824 -match 'public static class CoreSelection' -and $policy824 -match 'public const int MaximumCores = 64;' -and
     $policy824 -match 'public static ulong\? TargetAffinity\(ServerResourcePolicy policy, int processorCount, bool pinnedByMystTiq\)' -and
     $policy824 -match 'return pinnedByMystTiq \? CoreSelection\.AllCoresMask\(processorCount\) : null;' -and
     $control824 -match 'ulong\? GetAffinity\(int processId\);' -and $control824 -match 'process\.ProcessorAffinity = new IntPtr' -and
     $control824 -match 'foreach \(var thread in threads\)\s*\{\s*if \(sched_setaffinity\(thread,' -and
     $service824 -match 'affinityPinnedByMystTiq' -and $service824 -match 'if \(currentCores is \{ \} coresNow && coresNow != wantedCores\)') -Severity Critical
$apiHost824 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$wrapper824 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\PolicyApplyingLifecycle.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.24.0: every start and restart through MystTiq applies the policy as soon as it succeeds (never failing the start); the systemd unit keeps no Nice=' `
    ($apiHost824 -match 'var policyLifecycle = new PolicyApplyingLifecycle\(lifecycleFactory\(serverConfig, paths\)\);\s*IServerLifecycleService lifecycle = policyLifecycle;' -and
     $apiHost824 -match 'policyLifecycle\.AfterStart = async token => await resourcePolicy\.ApplyAsync\(token\);' -and
     $wrapper824 -match 'if \(result\.Success && AfterStart is \{ \} hook\)' -and $wrapper824 -match 'catch \(Exception ex\) when \(ex is not OperationCanceledException\) \{ \}' -and
     (Get-MystTiqText $ctx 'src\MystTiq.Core\Services\LinuxSystemdServiceManager.cs') -notmatch '"Nice=') -Severity Critical
$desk824 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.Host.cs'
$xaml824 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.24.0: the HOST tab edits the cores (read-only below Admin, in the priority card) and shows each process''s cores' `
    ($xaml824 -match '<TextBox x:Name="HostCoresBox" Text="\{Binding CoresText\}"' -and $desk824 -match 'Cores = string\.IsNullOrWhiteSpace\(CoresText\) \? null : CoresText\.Trim\(\)' -and
     (Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\HostDtos.cs') -match 'cores \{\(Cores == "Unknown" \? "unknown" : Cores\)\}') -Severity Critical
$harness824 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$linux824 = Get-MystTiqText $ctx 'scripts\Test-v0.8.24.0-LinuxIsolated.sh'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.24.0: the harness, the Windows smoke (real process, a restart) and the Linux check (every thread) cover it' `
    (@([regex]::Matches($harness824, 'RunScenario\("Processor cores:')).Count -eq 2 -and $harness824 -match 'RunScenario\("Apply at start:' -and
     (Test-Path (Join-Path $root 'scripts\Test-v0.8.24.0-RouteSmoke.ps1')) -and $linux824 -match 'Cpus_allowed_list' -and $linux824 -match '--fleet-root') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.24.0-processor-cores.md' 'Regression' 'v0.8.24.0: v0.8.24.0 architecture doc is present' -Severity Critical

$design825 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Styles\DesignSystem.axaml'
$window825 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$palette825 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\DecorativePalette.cs'
$applier825 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\ThemeApplier.cs'
# No literal colour is left in the styles outside comments and the keyed resources ThemeApplier writes.
function Get-LiteralColours825([string]$Text) {
    $t = $Text -replace '<!--[\s\S]*?-->', ''
    $t = [regex]::Replace($t, '<(SolidColorBrush|LinearGradientBrush|RadialGradientBrush|Color) x:Key="[^"]+"(?:[^>]*/>|[^>]*>[\s\S]*?</\1>)', '')
    @([regex]::Matches($t, '[^\n]*#[0-9A-Fa-f]{6,8}\b[^\n]*') | ForEach-Object { $_.Value.Trim() })
}
$left825 = @(Get-LiteralColours825 $design825) + @(Get-LiteralColours825 $window825)
Add-MystTiqCheck $ctx 'Regression' 'v0.8.25.0: no literal colour is left in the styles or the window (outside comments and keyed resources): each is a decorative resource the mode derives, shadows included' `
    ($left825.Count -eq 0 -and @([regex]::Matches($palette825, '\["Deco\w+_[0-9A-F]{6,8}"\] = new\(DecorativeRole\.')).Count -ge 90 -and
     @([regex]::Matches($palette825, '\["DecoShadow_\d+"\] = "')).Count -ge 20 -and
     $applier825 -match 'DecorativePalette\.Apply\(app\.Resources, mode, variant, S\);' -and
     $applier825 -match 'app\.Resources\["WarningGlassGradient"\] =' -and $applier825 -match 'app\.Resources\["RestoreGradientHover"\] =' -and
     (Get-MystTiqText $ctx 'src\MystTiq.Desktop\Controls\HostHistoryChart.cs') -notmatch 'Color\.Parse\("#' -and
     (Get-MystTiqText $ctx 'src\MystTiq.Desktop\Controls\ResourceHistoryChart.cs') -notmatch 'Color\.Parse\("#') -Severity Critical -Details (($left825 | Select-Object -First 5) -join ' / ')
$contrast825 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\SystemContrastPalette.cs'
$vm825 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.25.0: Windows contrast themes are read from Windows (SPI_GETHIGHCONTRAST, GetSysColor) and used by High contrast and Follow the system, live; buttons use the theme''s button text' `
    ($contrast825 -match 'private const uint SpiGetHighContrast = 0x0042;' -and $contrast825 -match 'GetSysColor' -and $contrast825 -match 'if \(!OperatingSystem\.IsWindows\(\)\) return null;' -and
     $applier825 -match 'var contrast = mode is "HighContrast" or "System" \? SystemContrast\(\) : null;' -and
     $applier825 -match 'app\.Resources\["ButtonForegroundBrush"\] = new SolidColorBrush\(contrast\?\.ButtonText \?\? Colors\.White\);' -and
     $design825 -match '<Setter Property="Foreground" Value="\{DynamicResource ButtonForegroundBrush\}"/>' -and
     $vm825 -match 'ThemeApplier\.ResolveVariant\(profile\.ThemeVariant\) == "Light"' -and $vm825 -match 'is not \("System" or "HighContrast"\)\) return;') -Severity Critical
$art825 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.25.0: the ArtworkHarness checks the decorative colours in every mode and a dark and a light Windows contrast theme (this machine''s own setting pinned off)' `
    ($art825 -match 'Dark: every decorative colour and shadow is exactly the value it was tuned with' -and $art825 -match 'High contrast: decorative borders and text white, dark surfaces black, no glows or shadows' -and
     $art825 -match 'a light contrast theme \(Desert\), changed while open' -and $art825 -match 'ThemeApplier\.SystemContrast = \(\) => null;' -and
     $art825 -match 'Light: success buttons use dark text') -Severity Critical
# The user's own HOST art (2026-09-25), replacing the System art and diagnostics icon it borrowed since v0.8.17.0.
Add-MystTiqCheck $ctx 'Regression' 'v0.8.25.0: the HOST tab has its own page art (day and night files) and navigation icon' `
    ((Test-Path (Join-Path $root 'src\MystTiq.Desktop\Assets\Icons\icon-host.png')) -and (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Assets\Artwork\page-art-host-dark.png')) -and
     (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Assets\Artwork\page-art-host-light.png')) -and
     (Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\ArtworkCatalog.cs') -match 'NavigationPage\.Host => "host",' -and
     $window825 -match 'CommandParameter="Host"><Border Classes="navSurface"><Grid ColumnDefinitions="54,\*" ColumnSpacing="12"><Image Source="\{services:IconArt host\}"') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.25.0-theme-leftovers.md' 'Regression' 'v0.8.25.0: v0.8.25.0 architecture doc is present' -Severity Critical

# The legacy WPF app, its installer and the tools that only served it are gone; the solution is the product.
$slnx826 = Get-MystTiqText $ctx 'PalworldServerManager.slnx'
$manifest826 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\app.manifest'
$rootBuild826 = Get-MystTiqText $ctx 'Build.ps1'
$validate826 = Get-MystTiqText $ctx 'scripts\Validate-Release.ps1'
$gone826 = @(@('src\PalworldManager', 'installer', 'scripts\Build-Installer.ps1', 'scripts\Install-InnoSetup.ps1', 'scripts\Package-Portable.ps1',
    'Update-FromDownloads.ps1', 'scripts\Install-LatestFullSourceFromDownloads.ps1', 'docs\reconstruction') | Where-Object { Test-Path (Join-Path $root $_) })
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: the legacy WPF app, its installer and their tools are gone; the solution holds exactly Core, HeadlessHost and Desktop; the Desktop has its own manifest' `
    ($gone826.Count -eq 0 -and @([regex]::Matches($slnx826, '<Project Path="')).Count -eq 3 -and
     $slnx826 -match 'src/MystTiq\.Core/MystTiq\.Core\.csproj' -and $slnx826 -match 'src/MystTiq\.HeadlessHost/MystTiq\.HeadlessHost\.csproj' -and $slnx826 -match 'src/MystTiq\.Desktop/MystTiq\.Desktop\.csproj' -and
     $manifest826 -match 'requestedExecutionLevel level="asInvoker"' -and
     (Get-MystTiqText $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -match '<ApplicationManifest>app\.manifest</ApplicationManifest>' -and
     $rootBuild826 -notmatch "'Installer'" -and $rootBuild826 -match "Invoke-Script 'Package-GitHubRelease\.ps1' @\{ Runtime = 'linux-x64' \}" -and
     $validate826 -match "'src\\MystTiq\.Desktop\\app\.manifest'" -and $validate826 -notmatch 'PalworldManager') -Severity Critical -Details "still present: $($gone826 -join ', ')"
# Nothing current (code, build scripts, workflows, project files) refers to what was removed. The gates and the release
# history name them on purpose.
$refs826 = @(Get-ChildItem $root -File -Recurse -Include *.ps1, *.yml, *.slnx, *.csproj, *.props, *.cs, *.axaml, *.sh | Where-Object {
        $rel = $_.FullName.Substring($root.Length + 1)
        $rel -notmatch '^(artifacts|release-notes)\\' -and $rel -notmatch '\\(bin|obj)\\' -and $rel -notlike 'scripts\Test-v*-Logic.ps1'
    } | Where-Object { (Get-Content $_.FullName -Raw) -match 'PalworldManager[\\/]|Build-Installer\.ps1|Package-Portable\.ps1|Install-InnoSetup\.ps1|Update-FromDownloads\.ps1' } |
    ForEach-Object { $_.FullName.Substring($root.Length + 1) })
$readme826 = Get-MystTiqText $ctx 'README.md'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: no current file refers to the removed app or its tools, and the README, CONTRIBUTING and publishing guide no longer describe them as current' `
    ($refs826.Count -eq 0 -and $readme826 -notmatch 'remain the legacy Windows reference' -and $readme826 -notmatch 'legacy WPF installer is not' -and
     (Get-MystTiqText $ctx 'CONTRIBUTING.md') -notmatch 'Build-Installer|WPF UI thread' -and (Get-MystTiqText $ctx 'docs\release\README.md') -notmatch 'Package-Portable') -Severity Critical -Details "refers: $($refs826 -join ', ')"
# Every text file carries this version's review stamp (scripts\Set-MystTiqFileStamp.ps1 -Check); JSON, LICENSE and
# binaries are listed, not stamped.
$stampOut826 = & pwsh -NoProfile -File (Join-Path $root 'scripts\Set-MystTiqFileStamp.ps1') -ProjectRoot $root -Version '1.0.0.4' -Check 2>&1 | ForEach-Object { "$_" }
$stampExit826 = $LASTEXITCODE
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: every text file carries the current (v1.0.0.4) review stamp, in its own comment syntax' `
    ($stampExit826 -eq 0 -and ($stampOut826 -join "`n") -match 'Current\s+\d{3,}') -Severity Critical -Details (($stampOut826 | Select-Object -Last 6) -join ' / ')
# The file audit lists every file in the tree and every removal, and nothing it lists as removed has come back.
$auditCsv826 = Join-Path $root 'docs\release\FILE_AUDIT_v0.8.26.0.csv'
$audit826 = if (Test-Path $auditCsv826) { @(Import-Csv $auditCsv826) } else { @() }
$auditPaths826 = [System.Collections.Generic.HashSet[string]]::new([string[]]@($audit826 | Where-Object Action -ne 'Removed' | ForEach-Object Path), [StringComparer]::OrdinalIgnoreCase)
$skip826 = 'bin', 'obj', 'artifacts', '.git', '.claude', '.vs', '__pycache__', 'node_modules', 'TestResults'
$unlisted826 = @(Get-ChildItem $root -File -Recurse -Force | ForEach-Object { $_.FullName.Substring($root.Length + 1) } |
    Where-Object { -not @(($_.Split('\') | Select-Object -SkipLast 1) | Where-Object { $_ -in $skip826 }).Count } |
    ForEach-Object { $_.Replace('\', '/') } | Where-Object { -not $auditPaths826.Contains($_) })
$back826 = @($audit826 | Where-Object { $_.Action -eq 'Removed' -and (Test-Path (Join-Path $root $_.Path)) } | ForEach-Object Path)
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: the v0.8.26.0 file audit lists every file in the tree (stamped, JSON, binary) and the removals, and no removed file is back' `
    ((Test-Path (Join-Path $root 'docs\release\FILE_AUDIT_v0.8.26.0.md')) -and @($audit826 | Where-Object Action -eq 'Removed').Count -ge 1000 -and
     $back826.Count -eq 0) -Severity High -Details "unlisted: $(($unlisted826 | Select-Object -First 5) -join ', '); back: $(($back826 | Select-Object -First 5) -join ', ')"
# Line endings: Linux scripts stay LF on every checkout; Python caches stay out of git.
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: .gitattributes keeps .sh and .py files LF, .gitignore keeps Python caches out, and no .sh file has CRLF endings' `
    ((Get-MystTiqText $ctx '.gitattributes') -match '\*\.sh text eol=lf' -and (Get-MystTiqText $ctx '.gitattributes') -match '\*\.py text eol=lf' -and
     (Get-MystTiqText $ctx '.gitignore') -match '__pycache__/' -and
     @(Get-ChildItem (Join-Path $root 'scripts') -Recurse -File -Filter *.sh | Where-Object { ([IO.File]::ReadAllText($_.FullName)).Contains("`r`n") }).Count -eq 0) -Severity High
# The live acceptance scripts: what each one checks, and that the ones changing real state are careful.
$inGame826 = Get-MystTiqText $ctx 'scripts\Test-v0.8.26.0-InGame.ps1'
$alerts826 = Get-MystTiqText $ctx 'scripts\Test-v0.8.26.0-Alerts.ps1'
$contrastScript826 = Get-MystTiqText $ctx 'scripts\Test-v0.8.26.0-ContrastTheme.ps1'
$session826 = Get-MystTiqText $ctx 'scripts\Test-v0.8.26.0-LinuxDesktopSession.ps1'
$harness826 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.RemoteSignInHarness\Program.cs'
$rehearsal826 = Get-MystTiqText $ctx 'scripts\Test-v0.8.26.0-LiveScriptsRehearsal.ps1'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: the in-game script works on the clone server only by default, gives, captures and teleports through the Desktop''s own routes, and always restores the teleport settings' `
    ($inGame826 -match "\[string\]\`$ServerId = 'second-local'" -and $inGame826 -match '\$api = "\$BaseUrl/api/v1/servers/\$ServerId"' -and
     $inGame826 -match 'Invoke-Api POST "/players/\$playerId/give"' -and $inGame826 -match "Invoke-Api POST '/teleport/capture'" -and
     $inGame826 -match 'Invoke-Api POST "/teleport/points/\$pointName/send"' -and $inGame826 -match "\} finally \{\s+\`$restore = Invoke-Api PUT '/teleport' \`$original" -and
     $inGame826 -match 'Map calibration pair') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: the alerts script sends only through switched-on, unpaused channels, changes no setting, and reports failed sends from the Activity log' `
    ($alerts826 -match '/notifications/delivery' -and $alerts826 -match 'if \(\$delivery\.pausedUntilUtc\)' -and $alerts826 -match 'if \(\$channels\.Count -eq 0\)' -and
     $alerts826 -match 'Invoke-RestMethod -Method Post -Uri "\$api/notifications/test"' -and $alerts826 -match '\[Notifications\]' -and
     $alerts826 -notmatch '-Method Put') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: the contrast-theme script reads the theme from Windows like MystTiq does, captures the MystTiq window and compares its colours' `
    ($contrastScript826 -match 'SystemParametersInfo\(0x0042' -and $contrastScript826 -match 'Sys\(5\); WindowText = \[MystTiqContrastProbe\]::Sys\(8\)' -and
     $contrastScript826 -match 'PrintWindow\(hwnd, hdc, 2\)' -and $contrastScript826 -match "Get-Process -Name 'MystTiq\.Desktop'" -and
     $contrastScript826 -match "\`$mode -in 'HighContrast', 'System'") -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: the Linux desktop session runs the real window on the VM''s display (X11 state, maximize, clipboard, tray) with its own home folder, and checks the VM user''s settings untouched' `
    ($session826 -match 'DISPLAY=:0 XAUTHORITY=' -and $session826 -match 'HOME=\$dir/home' -and $session826 -match "the VM user's MystTiq settings folders are as they were" -and
     $harness826 -match 'if \(account == accounts\[0\] && realWindow\) await RealSessionChecksAsync\(window\);' -and $harness826 -match '_NET_WM_STATE' -and
     $harness826 -match 'org\.kde\.StatusNotifierItem-') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.8.26.0: the release gate rehearses the in-game and alerts scripts against stand-in RCON, REST and webhook servers, checking what RCON actually received' `
    ($rehearsal826 -match "'giveitems steam_1 Wood:1'" -and $rehearsal826 -match "'getpos steam_1'" -and $rehearsal826 -match "'tp steam_1 -358\.2 270\.5 1200'" -and
     $rehearsal826 -match 'the teleport settings are exactly as they were' -and $rehearsal826 -match 'pointed at a closed port, the alerts script fails') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.8.26.0-repository-cleanup.md' 'Regression' 'v0.8.26.0: v0.8.26.0 architecture doc is present' -Severity Critical

$localizer900 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\Localizer.cs'
$codes900 = @([regex]::Matches($localizer900, 'new\("([\w-]+)", "[^"]+"\)') | ForEach-Object { $_.Groups[1].Value })
$i18n900 = Join-Path $root 'src\MystTiq.Desktop\Assets\i18n'
$en900 = Get-Content (Join-Path $i18n900 'en.json') -Raw | ConvertFrom-Json -AsHashtable
function Get-Placeholders900([string]$Text) { (@([regex]::Matches($Text, '\{\d+\}') | ForEach-Object Value) | Sort-Object) -join ',' }
$langProblems900 = @()
foreach ($code in $codes900 | Where-Object { $_ -ne 'en' }) {
    $path = Join-Path $i18n900 "$code.json"
    if (-not (Test-Path $path)) { $langProblems900 += "${code}: no file"; continue }
    $t = Get-Content $path -Raw | ConvertFrom-Json -AsHashtable
    $missing = @($en900.Keys | Where-Object { -not $t.ContainsKey($_) -or [string]::IsNullOrWhiteSpace($t[$_]) })
    $extra = @($t.Keys | Where-Object { -not $en900.ContainsKey($_) })
    $holes = @($en900.Keys | Where-Object { $t.ContainsKey($_) -and (Get-Placeholders900 $t[$_]) -ne (Get-Placeholders900 $en900[$_]) })
    if ($missing.Count + $extra.Count + $holes.Count) { $langProblems900 += "${code}: missing $($missing.Count), extra $($extra.Count), placeholders $($holes -join ',')" }
}
Add-MystTiqCheck $ctx 'Regression' 'v0.9.0.0: 12 languages (English, Chinese, Spanish, Portuguese, Russian, German, French, Japanese, Korean, Italian, Polish, Turkish), each with every English text, none blank, the same {n} placeholders' `
    ($codes900.Count -eq 12 -and (($codes900 -join ',') -eq 'en,zh-Hans,es,pt-BR,ru,de,fr,ja,ko,it,pl,tr') -and $en900.Count -ge 990 -and $langProblems900.Count -eq 0) `
    -Severity Critical -Details (($langProblems900 | Select-Object -First 4) -join ' | ')
$uiCheck900 = & pwsh -NoProfile -File (Join-Path $root 'scripts\Update-MystTiqUiText.ps1') -ProjectRoot $root -Check 2>&1 | ForEach-Object { "$_" }
$uiExit900 = $LASTEXITCODE
$window900 = [IO.File]::ReadAllText((Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml'))
Add-MystTiqCheck $ctx 'Regression' 'v0.9.0.0: no hard-coded display text is left in the window, the dialogs or the tray menu (Update-MystTiqUiText -Check), and the window uses its ui.* keys' `
    ($uiExit900 -eq 0 -and ($uiCheck900 -join "`n") -match 'Hard-coded XAML texts: 0' -and @([regex]::Matches($window900, '\{services:Tr ui\.')).Count -ge 850 -and
     (Get-MystTiqText $ctx 'src\MystTiq.Desktop\App.axaml') -match 'Header="\{services:Tr ui\.show_mysttiq\}"') -Severity Critical -Details (($uiCheck900 | Select-Object -Last 3) -join ' / ')
$design900 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Styles\DesignSystem.axaml'
$app900 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\App.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.0.0: Chinese, Japanese and Korean get their own system fonts (Windows, then Noto CJK on Linux) through font resources the styles read; English stays Inter' `
    ($localizer900 -match '"zh-Hans" => "Microsoft YaHei UI' -and $localizer900 -match '"ja" => "Yu Gothic UI' -and $localizer900 -match '"ko" => "Malgun Gothic' -and
     $localizer900 -match 'Noto Sans CJK JP' -and $localizer900 -match 'ApplyFonts\(language\.Code\);' -and
     $design900 -match '<Setter Property="FontFamily" Value="\{DynamicResource UiFontFamily\}"/>' -and $design900 -notmatch 'Value="Inter, Segoe UI"' -and
     $app900 -match '<FontFamily x:Key="UiFontFamily">Inter, Segoe UI</FontFamily>') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.9.0.0: the title bar has a language picker right of Settings (the same setting as Settings > Language) and a bell for Notifications (the user, 2026-09-27)' `
    ($window900 -match 'x:Name="TitleLanguagePicker"[^>]*ItemsSource="\{Binding UiLanguages\}" SelectedItem="\{Binding SelectedUiLanguage\}"' -and
     $window900 -match 'CommandParameter="Notifications" ToolTip\.Tip="\{services:Tr ui\.open_notifications\}"[^>]*><PathIcon Width="16" Height="16" Data="M10,1\.5' -and
     $window900.IndexOf('x:Name="TitleLanguagePicker"') -gt $window900.IndexOf('CommandParameter="Settings" ToolTip.Tip=')) -Severity Critical
$art900 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.0.0: the ArtworkHarness takes every language through the layout checks and scans every page for untranslated English, the title-bar picker and the Japanese font' `
    ($art900 -match 'foreach \(var code in Localizer\.Languages\.Select\(l => l\.Code\)\)' -and $art900 -match 'no page shows an untranslated English label or button' -and
     $art900 -match 'Choosing 日本語 in the title bar switches the whole window' -and $art900 -match 'Japanese is drawn with a Japanese font') -Severity Critical
Test-MystTiqFile $ctx 'docs\i18n\UI_TEXT_INVENTORY.md' 'Regression' 'v0.9.0.0: the UI text inventory (the list of English texts and whether each follows the language) is published' -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v0.9.0.0-display-languages.md' 'Regression' 'v0.9.0.0: v0.9.0.0 architecture doc is present' -Severity Critical

$catalog910 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MessageCatalog.cs'
$localizer910 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\Localizer.cs'
$en910 = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Assets\i18n\en.json') -Raw | ConvertFrom-Json -AsHashtable
$msg910 = @($en910.Keys | Where-Object { $_ -like 'msg.*' })
Add-MystTiqCheck $ctx 'Regression' 'v0.9.1.0: the code''s status and error messages are a catalog of msg.* keys (over 1,100, in every language: the 12-language check above covers every key)' `
    ($msg910.Count -ge 1100 -and $en910['msg.stopped_not_ready'] -eq 'Stopped / Not ready' -and $en910['msg.status'] -eq 'Status: {0}') -Severity Critical -Details "msg keys: $($msg910.Count)"
Add-MystTiqCheck $ctx 'Regression' 'v0.9.1.0: data stays English: the server command, the date formats, the default server name and the place names are not in the catalog' `
    (-not ($en910.Values -contains 'Shutdown 60 Server restarting') -and -not ($en910.Values -contains 'yyyy-MM-dd HH:mm:ss') -and
     -not ($en910.Values -contains 'My Palworld Server') -and -not ($en910.Values -contains 'Pal Haven')) -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.9.1.0: messages are translated when shown, not in the view models: MessageCatalog (exact, templates with values carried and translated, open-ended messages, line by line), rebuilt on every language change; English passes through untouched' `
    ($catalog910 -match 'public static MessageCatalog Build\(' -and $catalog910 -match 'public string Translate\(string\? text\)' -and
     $catalog910 -match 'if \(ReferenceEquals\(english, chosen\)\) return Empty;' -and $catalog910 -match 'OrderByDescending\(t => t\.LiteralLength\)\.ThenBy\(t => t\.IsOpen\)' -and
     $catalog910 -match 'TimeSpan\.FromMilliseconds\(50\)' -and $catalog910 -match 'if \(literal < 4 \|\| letters < 2\) return null;' -and
     $localizer910 -match 'Messages = MessageCatalog\.Build\(english, current\);' -and $localizer910 -match 'public static string T\(string\? text\) => Instance\.Translate\(text\);') -Severity Critical
$uiCheck910 = & pwsh -NoProfile -File (Join-Path $root 'scripts\Update-MystTiqUiText.ps1') -ProjectRoot $root -Check 2>&1 | ForEach-Object { "$_" }
$uiExit910 = $LASTEXITCODE
$window910 = [IO.File]::ReadAllText((Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml'))
$allXaml910 = (@((Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml'), (Join-Path $root 'src\MystTiq.Desktop\App.axaml')) + @(Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop\Views') -Filter *.axaml | ForEach-Object FullName) | ForEach-Object { [IO.File]::ReadAllText($_) }) -join "`n"
Add-MystTiqCheck $ctx 'Regression' 'v0.9.1.0: every simple display binding shows its text through the catalog ({services:TrText}); editable fields never do (Update-MystTiqUiText -Check: 0 left)' `
    ($uiExit910 -eq 0 -and ($uiCheck910 -join "`n") -match 'Untranslated display bindings: 0' -and @([regex]::Matches($window910, '\{services:TrText[ }]')).Count -ge 450 -and
     $allXaml910 -notmatch '<TextBox\b[^>]*\{services:TrText' -and $localizer910 -match 'class TrTextExtension : MarkupExtension' -and
     $localizer910 -match 'string text => Localizer\.Instance\.Translate\(text\)') -Severity Critical -Details (($uiCheck910 | Select-Object -Last 2) -join ' / ')
$app910 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\App.axaml'
$mainCs910 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
$dialogs910 = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop\Views') -Filter 'Confirm*.axaml.cs' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
Add-MystTiqCheck $ctx 'Regression' 'v0.9.1.0: drop-down and list items, dialogs and file-picker titles follow the language too (a String data template; Localizer.T in the dialogs and pickers)' `
    ($app910 -match '<DataTemplate DataType="\{x:Type x:String\}"><TextBlock Text="\{services:TrText\}"/></DataTemplate>' -and
     @([regex]::Matches($mainCs910, 'Title = Localizer\.T\(')).Count -ge 15 -and $dialogs910 -match 'FindingsList\.ItemsSource = findings\.Select\(Localizer\.T\)\.ToList\(\);' -and
     $dialogs910 -match 'MessageText\.Text = Localizer\.T\(\$"\\"\{serverName\}\\" is currently running') -Severity Critical
$art910 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.1.0: the ArtworkHarness checks the catalog (exact, template, reordered values, open-ended, multi-line, unknown text, English unchanged, view models stay English), round-trips every template in every language and scans every page for untranslated messages' `
    ($art910 -match 'every message template round-trips' -and $art910 -match 'A translation may put the values in another order' -and
     $art910 -match 'The view model itself keeps English' -and $art910 -match 'kv\.Key\.StartsWith\("msg\."\) && !kv\.Value\.Contains' -and
     $art910 -match 'A multi-line status is translated line by line') -Severity Critical
$inventory910 = Get-MystTiqText $ctx 'docs\i18n\UI_TEXT_INVENTORY.md'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.1.0: the UI text inventory counts the code''s messages as translated when shown' `
    ($inventory910 -match 'translated when shown \(v0\.9\.1\.0\)' -and $inventory910 -match 'Hard-coded in XAML \(not yet translatable\) \| 0 \| 0') -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v0.9.1.0-translated-messages.md' 'Regression' 'v0.9.1.0: v0.9.1.0 architecture doc is present' -Severity Critical

$names920 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessGameNameService.cs'
$extractor920 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\Tools\extract_game_names.py'
$localizer920 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\Localizer.cs'
$nameCodes920 = if ($names920 -match 'Languages = \[([^\]]+)\]') { @([regex]::Matches($Matches[1], '"([\w-]+)"') | ForEach-Object { $_.Groups[1].Value }) } else { @() }
$uiCodes920 = @([regex]::Matches($localizer920, 'new\("([\w-]+)", "[^"]+"\)') | ForEach-Object { $_.Groups[1].Value })
Add-MystTiqCheck $ctx 'Regression' 'v0.9.2.0: item and Pal names come in each of the Desktop''s 12 languages, English the default and the fallback, one cache per language; Japanese from the game''s base tables' `
    ((($nameCodes920 | Sort-Object) -join ',') -eq (($uiCodes920 | Sort-Object) -join ',') -and $nameCodes920.Count -eq 12 -and
     $names920 -match 'public const string DefaultLanguage = "en";' -and
     $names920 -match 'Path\.Combine\(paths\.ManagerRuntimeRoot, "game-names", \$"\{NormalizeLanguage\(language\)\}\.json"\)' -and
     $names920 -match 'Shown in English: the names in this language could not be read from the game files\.' -and
     $names920 -match '4 => \(null, \$"the installed game has no \{code\} name table"\)' -and
     $extractor920 -match 'SOURCE_LANG, SOURCE_DIR = "ja", "Pal/Content/Pal/DataTable/Text/"' -and
     $extractor920 -match 'path = \(SOURCE_DIR if a\.lang == SOURCE_LANG else TABLE_DIR\.format\(lang=a\.lang\)\)') -Severity Critical -Details "name languages: $($nameCodes920 -join ',')"
$api920 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$client920 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs'
$vm920 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.2.0: the picker and map routes take ?lang=, the Desktop sends its display language, and a language change reads the Give Item picker again' `
    ($api920 -match 'routes\.MapGet\("/players/give/catalog", \(string\? lang\) => Results\.Ok\(p\.GameIds\.GetCatalog\(lang\)\)\)' -and
     $api920 -match 'routes\.MapGet\("/world/players-guilds", async \(string\? lang, CancellationToken token\) =>\s+Results\.Ok\(await p\.PlayerGuildExplorer\.ExploreAsync\(token, lang\)\)\);' -and
     @([regex]::Matches($client920, '\?lang=\{DisplayLanguage\}')).Count -eq 2 -and $client920 -match 'private static string DisplayLanguage => Uri\.EscapeDataString\(Localizer\.Instance\.LanguageCode\);' -and
     $vm920 -match 'if \(_gameIdCatalog\.Count > 0 && SelectedProfile is not null && !IsBusy\) _ = LoadGameIdsAsync\(\);') -Severity Critical
$catalog920 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MessageCatalog.cs'
$en920 = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Assets\i18n\en.json') -Raw | ConvertFrom-Json -AsHashtable
Add-MystTiqCheck $ctx 'Regression' 'v0.9.2.0: a text composed of sentences is translated sentence by sentence (preferred when every sentence is known), with the map, tooltip, alpha and uptime sentences in the catalog' `
    ($catalog920 -match 'private \(string\? Text, bool Complete\) TranslateSentences\(string text\)' -and
     $catalog920 -match 'var result = complete \? sentences : TranslateWhole\(trimmed, 0\) \?\? sentences;' -and
     ($en920.Values -contains '{0} player(s) online on the map.') -and ($en920.Values -contains '{0}, level {1}, working at {2}''s base {3}.') -and
     ($en920.Values -contains '{0}, alpha') -and ($en920.Values -contains 'Up {0} d {1} h') -and @($en920.Keys | Where-Object { $_ -like 'msg.*' }).Count -ge 1150) -Severity Critical
$logic920 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
$art920 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.2.0: the logic harness runs a stand-in extractor per language (separate caches, no re-read, English fallback) and the ArtworkHarness checks the composed texts in Japanese' `
    ($logic920 -match 'each display language gets the game''s own names, cached apart; a language the game lacks falls back to English' -and
     $logic920 -match 'a language already read is not read again' -and
     $art920 -match 'A summary built from several sentences is translated sentence by sentence' -and $art920 -match 'A Pal tooltip \(name, level, place, coordinates, then notes\) is fully translated' -and
     $art920 -match 'The host''s uptime is translated') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.9.2.0-game-names-in-your-language.md' 'Regression' 'v0.9.2.0: v0.9.2.0 architecture doc is present' -Severity Critical

$en930 = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Assets\i18n\en.json') -Raw | ConvertFrom-Json -AsHashtable
$msg930 = @($en930.Keys | Where-Object { $_ -like 'msg.*' })
$languages930 = @(Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop\Assets\i18n') -Filter *.json | Where-Object { $_.BaseName -ne 'en' })
$missing930 = @(foreach ($file in $languages930) {
    $text = Get-Content $file.FullName -Raw | ConvertFrom-Json -AsHashtable
    foreach ($key in $msg930) { if (-not $text.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$text[$key])) { "$($file.BaseName):$key" } }
})
Add-MystTiqCheck $ctx 'Regression' 'v0.9.3.0: the service''s messages (Doctor findings, operation results, crash explanations, network checks) are in the catalog in all 11 other languages' `
    ($msg930.Count -ge 2400 -and $languages930.Count -eq 11 -and $missing930.Count -eq 0 -and
     ($en930.Values -contains 'PalServer is not running.') -and ($en930.Values -contains 'Configuration did not validate: ') -and
     ($en930.Values -contains 'Your router did not report its internet address') -and ($en930.Values -contains 'Out of memory') -and
     ($en930.Values -contains 'Keep the crash report folder under Pal\Saved\Crashes (it holds a minidump) and include it when reporting the crash to Pocketpair.')) `
    -Severity Critical -Details "msg keys: $($msg930.Count); missing: $($missing930 | Select-Object -First 5)"
Add-MystTiqCheck $ctx 'Regression' 'v0.9.3.0: server commands, protocol text, log patterns and Windows service/firewall-rule names stay English (not in the catalog)' `
    (-not ($en930.Values -contains 'TeleportToMe {0}') -and -not ($en930.Values -contains 'KickPlayer {0}') -and
     -not ($en930.Values -contains 'MystTiq Palworld Server - Game {0} {1}') -and -not ($en930.Values -contains 'MystTiq Palworld Server Manager ({0})') -and
     -not ($en930.Values -contains 'Shutdown 1 MystTiq requested a graceful shutdown.') -and -not ($en930.Values -contains 'Trying to resize TArray to an invalid size')) -Severity Critical
$catalog930 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MessageCatalog.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.3.0: a template for the whole line is tried first and accepted only when no value spans a sentence break (a two-sentence message stays one message)' `
    ($catalog930 -match 'if \(TranslateWhole\(trimmed, 0, unsplitValues: true\) is \{ \} whole\) return Reedge\(line, trimmed, whole\);' -and
     $catalog930 -match 'if \(unsplitValues && values\.Any\(v => SentenceBreak\.IsMatch\(v\)\)\) continue;') -Severity Critical
$art930 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.3.0: the ArtworkHarness shows a message from the service in Japanese, with its value kept, and leaves a server command alone' `
    ($art930 -match 'Japanese: a message from the service is shown in Japanese' -and
     $art930 -match 'A service message with a value keeps the value' -and
     $art930 -match 'A server command the service reports stays as it is') -Severity Critical
$review930 = Get-MystTiqText $ctx 'scripts\Export-MystTiqTranslationReview.ps1'
$reviewDoc930 = Get-MystTiqText $ctx 'docs\i18n\TRANSLATION_REVIEW.md'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.3.0: reviewers get a sheet per language (every text, English beside it, where it is used) and a checklist; the review itself stays open until native speakers sign off' `
    ($review930 -match 'Export-Csv' -and $review930 -match '\[string\[\]\]\$Language' -and $review930 -match 'Kind' -and
     $reviewDoc930 -match 'Export-MystTiqTranslationReview\.ps1' -and $reviewDoc930 -match 'awaiting native review' -and
     $reviewDoc930 -match 'Plurals' -and $reviewDoc930 -match 'Dates and numbers') -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v0.9.3.0-the-service-s-messages.md' 'Regression' 'v0.9.3.0: v0.9.3.0 architecture doc is present' -Severity Critical

$hint940 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\RoleHint.cs'
$roles940 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.CommandRoles.cs'
$vm940 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.4.0: a button or menu item disabled for the signed-in role says which role it needs: tooltip shown while disabled, accessible help text, the control''s own tip kept and restored' `
    ($hint940 -match 'ToolTip\.SetShowOnDisabled\(control, true\);' -and
     $hint940 -match 'control\.SetValue\(ToolTip\.TipProperty, tip, BindingPriority\.Animation\)' -and
     $hint940 -match 'control\.SetValue\(AutomationProperties\.HelpTextProperty, hint, BindingPriority\.Animation\)' -and
     $hint940 -match 'if \(control\.Classes\.Contains\("ribbon"\)\) return;' -and
     $hint940 -match 'Needs the \{requiredRole\} role\. You are signed in as \{signedInRole\}\.' -and
     $vm940 -match 'string\? RequiredRole \{ get; set; \}' -and
     $roles940 -match 'gated\.RequiredRole = role;' -and $roles940 -match 'RoleHint\.RefreshAll\(\);' -and $roles940 -match 'RoleHint\.Install\(\);') -Severity Critical
# Every list, text box, drop-down, number box and slider in the Desktop's XAML has a name a screen reader can say
# (AutomationProperties.Name, a tooltip, a watermark or placeholder); a check box has its own text or a name.
$unnamed940 = @(foreach ($file in Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -Filter *.axaml) {
    $xaml = Get-Content $file.FullName -Raw
    foreach ($m in [regex]::Matches($xaml, '<(ListBox|TextBox|ComboBox|NumericUpDown|Slider|CheckBox)(?=[\s/>])([^<>]*?)(/?)>')) {
        $attrs = $m.Groups[2].Value
        if ($attrs -match 'AutomationProperties\.(Name|LabeledBy)=|ToolTip\.Tip=|Watermark=|PlaceholderText=') { continue }
        if ($m.Groups[1].Value -eq 'CheckBox' -and ($attrs -match '\bContent=' -or ($m.Groups[3].Value -ne '/' -and $xaml.Substring($m.Index + $m.Length, [Math]::Min(200, $xaml.Length - $m.Index - $m.Length)) -match '^\s*(<TextBlock|[^<\s])'))) { continue }
        "$($file.Name):$($m.Groups[1].Value)"
    }
})
Add-MystTiqCheck $ctx 'Regression' 'v0.9.4.0: every list, text box, drop-down, number box, slider and check box in the Desktop''s XAML has an accessible name' `
    ($unnamed940.Count -eq 0) -Severity Critical -Details "unnamed: $($unnamed940 | Select-Object -First 8)"
$en940 = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Assets\i18n\en.json') -Raw | ConvertFrom-Json -AsHashtable
$localizer940 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\Localizer.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.4.0: the role hints, the health states (STOPPED, DEGRADED, STARTING…) and the new accessible names are in the catalog; a text value inside a translated label is translated too' `
    (($en940.Values -contains 'Needs the {0} role. You are signed in as {1}.') -and ($en940.Values -contains 'Needs the {0} role.') -and
     ($en940.Values -contains 'STOPPED') -and ($en940.Values -contains 'DEGRADED') -and ($en940.Values -contains 'STARTING') -and
     @($en940.Keys | Where-Object { $_ -like 'ui.a11y_*' }).Count -ge 20 -and
     $localizer940 -match 'var text = value is string s \? Localizer\.T\(s\) : value\?\.ToString\(\) \?\? string\.Empty;') -Severity Critical
$art940 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.4.0: the ArtworkHarness checks, for every role on every page, that each disabled control names its role (and allowed ones do not), the hint in Japanese, and that every control has a name and can be reached with Tab' `
    ($art940 -match 'each of them says which role it needs, in its tooltip and accessible help text, and no allowed one does' -and
     $art940 -match 'Japanese: a Viewer''s disabled Kick button says, in Japanese, that it needs Admin' -and
     $art940 -match 'on every page every reachable control has an accessible name' -and
     $art940 -match 'on every page every enabled control can be reached with Tab' -and
     $art940 -match 'Japanese: health states and a value inside a label are translated') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.9.4.0-roles-explained-and-accessibility.md' 'Regression' 'v0.9.4.0: v0.9.4.0 architecture doc is present' -Severity Critical

$winLife950 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsServerLifecycleService.cs'
$linLife950 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs'
$inspector950 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsServerSessionInspector.cs'
$gamePort950 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ServerGamePort.cs'
$program950 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.5.0: a process whose path cannot be read belongs to a server only when that server started it (Windows and Linux), paths are read by image name, and readiness waits for the port the server binds (-port=, else 8211)' `
    ($winLife950 -match 'if \(string\.IsNullOrWhiteSpace\(process\.ExecutablePath\)\) return ownIds\.Contains\(process\.ProcessId\);' -and
     $winLife950 -notmatch 'if \(string\.IsNullOrWhiteSpace\(process\.ExecutablePath\)\) return true;' -and
     $linLife950 -match 'return process\.ProcessId == lastKnown;' -and
     $inspector950 -match 'QueryFullProcessImageName' -and $inspector950 -match 'var path = ImagePath\(process\.Id\);' -and
     $gamePort950 -match 'return invalid \? new PalworldSettingsConfigurationService\(paths\)\.GetConfiguredGamePort\(\) : port;' -and
     @([regex]::Matches($program950, 'ServerGamePort\.Expected\(profilePaths, serverProfile\.LaunchArguments\)')).Count -eq 3 -and
     $program950 -notmatch 'new PalworldSettingsConfigurationService\(profilePaths\)\.GetConfiguredGamePort\(\)') -Severity Critical
$updates950 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessComponentUpdateService.cs'
$distribution950 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessServerDistributionService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.5.0: the Palworld server is compared with Steam''s public build from SteamCMD (never UpToDateCheck), PalDefender has its own row, and a refused manifest is retried by checking every file against the new build' `
    ($updates950 -notmatch 'UpToDateCheck/v1' -and $updates950 -match '\+app_info_print \{PalworldDedicatedServerAppId\}' -and
     $updates950 -match 'public static SteamPublicBuild\? ParsePublicBuild\(string appInfo\)' -and
     $updates950 -match 'await CheckPalDefenderAsync\(now, cancellationToken\)' -and $updates950 -match 'hasn''t yet updated to the latest version' -and
     $distribution950 -match 'SteamCmdFailure\.IsManifestAccessDenied\(run\.Output, run\.ContentLog\)' -and
     $distribution950 -match 'var retry = await RunSteamCmdAsync\(workingDirectory, validate: true, cancellationToken\);' -and
     $distribution950 -match 'if \(retry\.ExitCode != 0 && !File\.Exists\(manifest\)\) File\.Move\(keptAs, manifest\);' -and
     $distribution950 -match 'SteamCmdFailure\.Describe\(run\.Output, run\.ContentLog\)') -Severity Critical
$doctor950 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiagnosticsService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.5.0: the Doctor warns when the game server is behind Steam''s public build (cached, never waiting on SteamCMD) and when PalDefender says it is not updated for the game' `
    ($doctor950 -match 'findings\.AddRange\(BuildVersionFindings\(\)\);' -and $doctor950 -match 'Id: "version-game-server"' -and
     $doctor950 -match 'Id: "version-paldefender"' -and $doctor950 -match 'componentUpdates\.PeekPublicBuild\(\)' -and
     $updates950 -match 'public SteamPublicBuild\? PeekPublicBuild\(\)') -Severity Critical
$logic950 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.5.0: the logic harness covers another server''s unreadable process, the expected port, SteamCMD''s app info and failures, and PalDefender''s warning; the upgrade and fleet-recovery smokes exist' `
    ($logic950 -match 'Another server''s process with an unreadable path is not taken for this server' -and
     $logic950 -match 'This server''s own game process still counts while its path cannot be read' -and
     $logic950 -match 'The expected game port is the -port= launch argument' -and
     $logic950 -match 'SteamCMD app info: the public branch''s build and date are read' -and
     $logic950 -match 'SteamCMD failure: a refused manifest is recognised' -and
     $logic950 -match 'PalDefender''s ''not updated for this game version'' warning' -and
     (Test-Path (Join-Path $root 'scripts\Test-v0.9.5.0-Upgrade.ps1')) -and (Test-Path (Join-Path $root 'scripts\Test-v0.9.5.0-FleetRecovery.ps1'))) -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.9.5.0-recovery-and-updates.md' 'Regression' 'v0.9.5.0: v0.9.5.0 architecture doc is present' -Severity Critical

$fwRules960 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\FirewallRules.cs'
$winNet960 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsNetworkDiagnosticsPlatformService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.6.0: the firewall is read through its COM API, and a rule counts only when it can reach the server (not another program, a service, Store apps or the PalServer.exe launcher)' `
    ($winNet960 -match 'Type\.GetTypeFromProgID\("HNetCfg\.FwPolicy2"\)' -and
     $winNet960 -match 'FirewallRules\.AppliesToServer\(r\.Application,r\.Service,r\.Package,r\.Owner\)' -and
     $winNet960 -match 'LocalAppPackageId' -and $winNet960 -match 'LocalUserOwner' -and
     $winNet960 -notmatch 'Get-NetFirewallPortFilter -All' -and
     $fwRules960 -match 'ServerExecutables = \["PalServer-Win64-Shipping-Cmd\.exe", "PalServer-Win64-Shipping\.exe"\]' -and
     $fwRules960 -match 'if \(!string\.IsNullOrWhiteSpace\(package\) \|\| !string\.IsNullOrWhiteSpace\(owner\)\) return false;') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.9.6.0: PowerShell and netstat start by their full paths with -EncodedCommand; the allow script tags the rule with the server id and removes only that server''s rules for other ports; a refusal returns the script for an elevated run' `
    ($winNet960 -match '"WindowsPowerShell","v1\.0","powershell\.exe"' -and $winNet960 -match 'SystemTool\("netstat\.exe"\)' -and
     $winNet960 -match '-EncodedCommand' -and $winNet960 -match 'NeedsElevation:true,Script:script' -and
     $fwRules960 -match 'private const string TagPrefix = "MystTiq server: ";' -and
     $fwRules960 -match '\$_\.Description -eq \$d -and \$_\.DisplayName -ne \$n' -and
     $fwRules960 -match '"\$ErrorActionPreference=''Stop'';') -Severity Critical
$api960 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$doctor960 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiagnosticsService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.6.0: the firewall route and repair use the port the server binds and its id; the Doctor reports the bound port against PublicPort and the firewall, with an Admin-only allow fix' `
    ($api960 -match 'routes\.MapGet\("/diagnostics/network/firewall"' -and
     @([regex]::Matches($api960, 'ServerGamePort\.Expected\(p\.Paths, p\.ServerProfile\.LaunchArguments\), p\.Id\.Value, token\)')).Count -eq 2 -and
     $api960 -match 'componentUpdates, networkDiagnostics, profileId\.Value, serverConfig\.LaunchArguments\)' -and
     $doctor960 -match 'Id: "configuration-game-port"' -and $doctor960 -match 'Id: "network-firewall"' -and
     $doctor960 -match 'case "allow-firewall":' -and $doctor960 -match 'Changing the firewall needs the Admin role\.') -Severity Critical
$vm960 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$xaml960 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$elevated960 = (Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\ElevatedFirewall.cs') + (Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\ElevatedPowerShell.cs')
Add-MystTiqCheck $ctx 'Regression' 'v0.9.6.0: Diagnostics, Settings and the wizard''s last step offer Allow through Firewall; the elevated fallback is only for a server on this computer; a second server gets its own -port=' `
    (@([regex]::Matches($xaml960, 'Command="\{Binding RepairFirewallCommand\}" IsVisible="\{Binding FirewallNeedsAction\}"')).Count -eq 3 -and
     $xaml960 -match 'ui\.clone_firewall_hint' -and
     $elevated960 -match 'profile\.BaseAddress\.IsLoopback' -and $elevated960 -match 'Verb = "runas"' -and $elevated960 -match 'NativeErrorCode == 1223' -and
     $vm960 -match 'if \(ElevatedFirewall\.CanRun\(profile\)\)' -and
     $vm960 -match 'LaunchArguments = LaunchArgumentsText\.WithPort\(ConfigLaunchArguments, SetupGamePort\),') -Severity Critical
$search960 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MystTiqServiceDiscoveryService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.6.0: the server search sweeps 256 addresses at a time with a TCP connect first, reports its ranges and progress, can be cancelled, skips virtual adapters unless ticked and takes typed ranges' `
    ($search960 -match 'private const int Parallelism = 256;' -and $search960 -match 'await socket\.ConnectAsync\(' -and
     $search960 -match 'if \(await AcceptsAsync\(address, port, token\)\.ConfigureAwait\(false\)\)' -and
     $search960 -match 'public static bool IsVirtualAdapter' -and $search960 -match 'IsVirtualAdapter\(a\.Adapter, a\.Description\) && !a\.HasGateway' -and $search960 -match '\.OrderBy\(a => a\.Virtual\)' -and $search960 -match 'public static IReadOnlyList<DiscoveryRange> ParseExtraRanges' -and
     $vm960 -match 'CancelDiscoveryCommand = new RelayCommand\(\(\) => _discoveryCancellation\?\.Cancel\(\)\);' -and
     $vm960 -match 'new DiscoveryOptions\(DiscoveryIncludeVirtualAdapters, DiscoveryExtraRanges\)' -and
     $vm960 -match 'if \(preferred is not null && ManagementApiConnected\)' -and
     $xaml960 -match 'Command="\{Binding CancelDiscoveryCommand\}"' -and $xaml960 -match '\{services:TrText DiscoveryRangesText\}') -Severity Critical
$logic960 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.6.0: the logic harness covers rule matching, the allow script, the rule state, a real firewall read, ranges, a timed search, Cancel and a second server''s arguments; the firewall route smoke exists' `
    ($logic960 -match 'Firewall: a rule covers the port by number, list or range' -and
     $logic960 -match 'Firewall: the allow script tags the rule with the server' -and
     $logic960 -match 'Firewall: the state says allowed, blocked, off or missing' -and
     $logic960 -match 'Firewall: this computer''s rules are read through the firewall''s COM API' -and
     $logic960 -match 'Server search: virtual adapters are left out unless ticked' -and
     $logic960 -match 'Server search: a service is found, and 255 addresses that never answer take seconds' -and
     $logic960 -match 'Server search: cancelling stops the search' -and
     $logic960 -match 'A second server''s launch arguments are the first one''s with its own -port=' -and
     (Test-Path (Join-Path $root 'scripts\Test-v0.9.6.0-FirewallRoute.ps1'))) -Severity Critical
$winLife960 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsServerLifecycleService.cs'
$linLife960 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs'
$inspector960 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsServerSessionInspector.cs'
$store960 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\ServerLifecycleStateStore.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.6.0: a process is listed until it has exited and never after, status reads and a stop''s state writes are serialised, and a read during a stop keeps the stop request (Windows and Linux), so the supervisor never restarts a stopped server as crashed; the logic harness replays the stop sequence' `
    (@([regex]::Matches($winLife960 + $linLife960, 'persisted is not \{ Phase: ServerLifecyclePhase\.Stopping, StopRequested: true \} &&')).Count -eq 2 -and
     @([regex]::Matches($winLife960 + $linLife960, 'lock \(stateGate\) return ReadStatus\(\);')).Count -eq 2 -and
     @([regex]::Matches($winLife960 + $linLife960, 'lock \(stateGate\) stateStore\.Write\(new PersistedServerLifecycleState\(')).Count -ge 2 -and
     $store960 -match 'lock \(writeGate\)' -and $store960 -match 'FileShare\.ReadWrite \| FileShare\.Delete' -and $store960 -match 'ex is IOException or UnauthorizedAccessException && attempt < 20' -and
     $inspector960 -match 'if \(HasExited\(process\.Id\)\)' -and $inspector960 -match 'try \{ responding = process\.Responding; \} catch \{ responding = false; \}' -and $inspector960 -match 'WaitForSingleObject\(handle, 0\) == WaitObject0' -and
     $logic960 -match 'A status read while a stopped server is still exiting does not turn the stop into a crash') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.9.6.0-firewall-and-search.md' 'Regression' 'v0.9.6.0: v0.9.6.0 architecture doc is present' -Severity Critical

$desktopProj970 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj'
$artProj970 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\MystTiq.ArtworkHarness.csproj'
$signInProj970 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.RemoteSignInHarness\MystTiq.RemoteSignInHarness.csproj'
$avaloniaRefs970 = @([regex]::Matches($desktopProj970, 'PackageReference Include="(Avalonia[\w.]*)" Version="([^"]+)"') | ForEach-Object { "$($_.Groups[1].Value)=$($_.Groups[2].Value)" })
Add-MystTiqCheck $ctx 'Regression' 'v0.9.7.0: the desktop uses Avalonia 12.1.3 for all four packages, pinned exactly, and both harnesses use Avalonia.Headless 12.1.3' `
    ($avaloniaRefs970.Count -eq 4 -and @($avaloniaRefs970 | Where-Object { $_ -notmatch '=12\.1\.3$' }).Count -eq 0 -and
     ($avaloniaRefs970 -join ',') -match 'Avalonia=12\.1\.3' -and ($avaloniaRefs970 -join ',') -match 'Avalonia\.Desktop=12\.1\.3' -and
     ($avaloniaRefs970 -join ',') -match 'Avalonia\.Fonts\.Inter=12\.1\.3' -and ($avaloniaRefs970 -join ',') -match 'Avalonia\.Themes\.Fluent=12\.1\.3' -and
     $artProj970 -match 'Include="Avalonia\.Headless" Version="12\.1\.3"' -and $signInProj970 -match 'Include="Avalonia\.Headless" Version="12\.1\.3"') -Severity Critical
$rawXaml970 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$toastXaml970 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Views\TrayReminderToast.axaml'
$windowCode970 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
$signIn970 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.RemoteSignInHarness\Program.cs'
$art970 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.7.0: no Avalonia 11 names are left: placeholders are PlaceholderText, windows use WindowDecorations, the clipboard uses the Avalonia.Input.Platform text extensions, and the harnesses mean the app''s NavigationPage' `
    ($rawXaml970 -notmatch '(?<=\s)Watermark=' -and ([regex]::Matches($rawXaml970, '(?<=\s)PlaceholderText=')).Count -ge 60 -and
     $rawXaml970 -notmatch 'SystemDecorations=' -and $toastXaml970 -notmatch 'SystemDecorations=' -and $toastXaml970 -match 'WindowDecorations="None"' -and
     $windowCode970 -match 'using Avalonia\.Input\.Platform;' -and
     $signIn970 -match 'await window\.Clipboard\.TryGetTextAsync\(\)' -and $signIn970 -match 'using NavigationPage = MystTiq\.Desktop\.Models\.NavigationPage;' -and
     $art970 -match 'using NavigationPage = MystTiq\.Desktop\.Models\.NavigationPage;' -and $art970 -match 'TextBox \{ PlaceholderText: string') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.9.7.0-avalonia-12.md' 'Regression' 'v0.9.7.0: v0.9.7.0 architecture doc is present' -Severity Critical

$rawXaml980 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$vm980 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$code980 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
$art980 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.8.0: the "»" and "+" have their own columns after the tabs (capped at TabListMaxWidth), one tab always stays, the brand narrows below 1200 px after the layout pass, and the ArtworkHarness checks the "+" at 950, 1100 and 1440 px' `
    ($rawXaml980 -match 'x:Name="TabStripHost" Grid\.Column="1" ColumnDefinitions="Auto,Auto,Auto,\*"' -and $rawXaml980 -match 'MaxWidth="\{Binding TabListMaxWidth\}"' -and
     $rawXaml980 -match 'x:Name="AddTabButton" Grid\.Column="2"' -and $rawXaml980 -match 'x:Name="TabOverflowButton" Grid\.Column="1"' -and
     $rawXaml980 -match 'IsVisible="\{Binding ShowBrandSubtitle\}"' -and $rawXaml980 -match 'MinWidth="\{Binding BrandMinWidth\}"' -and
     $vm980 -match 'maxVisible = Math\.Max\(1, \(int\)Math\.Floor\(\(available - overflowButtonWidth\) / perTabWidth\)\);' -and
     $vm980 -match 'TabListMaxWidth = Math\.Max\(0, _tabStripWidth - 52 - \(HasOverflowTabs \? 40 : 0\)\);' -and
     $code980 -match 'Dispatcher\.UIThread\.Post\(\(\) => vm\.UpdateWindowWidth\(width\)' -and
     $art980 -match 'foreach \(var titleWidth in new\[\] \{ 950\.0, 1100\.0, 1440\.0 \}\)' -and $art980 -match 'the active tab is shown and every tab is either shown or in the') -Severity Critical
$alerts980 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'
$api980 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$alertDto980 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\AlertCenterDtos.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.8.0: the Alert Center alerts when the game server is behind Steam''s build or PalDefender is out of date (on by default, kept on for old rules files, never deciding on an unknown build), and the Desktop shows the rule' `
    ($alerts980 -match 'public AlertSimpleRule\? ComponentOutdated \{ get; init; \} = new\(true, 1440\);' -and
     $alerts980 -match 'Track\("game-server-outdated"' -and $alerts980 -match 'Track\("paldefender-outdated"' -and
     $alerts980 -match 'ComponentOutdated = rules\.ComponentOutdated \?\? new AlertSimpleRule\(true, 1440\)' -and
     $alerts980 -match 'public static bool\? GameServerBehind' -and
     $api980 -match 'new HeadlessAlertCenterService\(paths, historicalMetrics, notifications, modManagement, componentUpdates\)' -and
     $alertDto980 -match 'public AlertSimpleRuleDto ComponentOutdated' -and $rawXaml980 -match 'IsChecked="\{Binding AlertRules\.ComponentOutdated\.Enabled\}"') -Severity Critical
$fw980 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\FirewallRules.cs'
$winNet980 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsNetworkDiagnosticsPlatformService.cs'
$search980 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MystTiqServiceDiscoveryService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.8.0: a firewall rule counts only on the network profile in use, and the server search covers each adapter''s own subnet up to a /22' `
    ($fw980 -match 'public static bool CoversNetwork\(int ruleProfiles, int currentProfiles\)' -and $fw980 -match 'var active = portRules\.Where\(r => r\.CoversCurrentNetwork\)' -and
     $winNet980 -match 'policy\.CurrentProfileTypes' -and $winNet980 -match 'CoversCurrentNetwork=FirewallRules\.CoversNetwork\(r\.Profiles,current\)' -and
     $search980 -match 'public const int WidestPrefix = 22;' -and $search980 -match 'unicast\.PrefixLength' -and
     $search980 -notmatch 'MaxCandidatesPerInterface') -Severity Critical
$build980 = Get-MystTiqText $ctx 'Build.ps1'
$installer980 = Get-MystTiqText $ctx 'scripts\Install-MystTiqDesktopLinux.ps1'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.8.0: the Linux installer puts the download in the user''s home with a trusted desktop launcher and an applications-menu entry, verifies the copy, touches nothing outside home, and Build.ps1 DeployDesktopLinux runs it; the stale v0.3.1.9 deploy script is gone' `
    ($build980 -match "'DeployDesktopLinux' \{ Invoke-Script 'Install-MystTiqDesktopLinux\.ps1' \}" -and
     -not (Test-Path (Join-Path $root 'scripts\Deploy-Test-MystTiqDesktopLinux.ps1')) -and
     $installer980 -match 'ln -sfn "\$V" current' -and $installer980 -match 'metadata::xfce-exe-checksum' -and
     $installer980 -match 'Exec=\$HOME/MystTiq/current/MystTiq\.Desktop' -and $installer980 -match '~/\.local/share/applications/mysttiq\.desktop' -and
     $installer980 -match 'The copied package does not match' -and $installer980 -notmatch 'sudo ') -Severity Critical
$logic980 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.8.0: the logic harness covers the component alert, firewall rules per network and each adapter''s subnet; the accounts upgrade smoke exists' `
    ($logic980 -match 'Component alert: the game server is behind only when both builds are known' -and
     $logic980 -match 'Firewall: a rule counts only on the networks it covers' -and
     $logic980 -match 'Server search: each adapter''s own subnet is searched' -and
     (Test-Path (Join-Path $root 'scripts\Test-v0.9.8.0-UpgradeAccounts.ps1'))) -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.9.8.0-small-screens-alerts-linux.md' 'Regression' 'v0.9.8.0: v0.9.8.0 architecture doc is present' -Severity Critical

$bootstrap990 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\LocalManagementBootstrapper.cs'
$sidecarState990 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\SidecarState.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.9.0: the desktop records the helper it started and reuses it, replaces its own unusable helper, never trusts a record that is not loopback, and stops a helper without its process tree (running game servers survive)' `
    ($bootstrap990 -match 'var owned = SidecarState\.Read\(GetLocalRuntimeRoot\(\)\);' -and $bootstrap990 -match 'Reusing the MystTiq helper this app started earlier' -and
     $bootstrap990 -match 'SidecarState\.Write\(GetLocalRuntimeRoot\(\), new SidecarState\(started\.Id, endpoint, executable, startedAt\)\);' -and
     @([regex]::Matches($bootstrap990, 'process\.Kill\(entireProcessTree: false\);')).Count -eq 2 -and $bootstrap990 -notmatch 'entireProcessTree: true' -and
     $sidecarState990 -match 'uri\.IsLoopback') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.9.9.0: a port that accepts connections counts as occupied even when the health probe gets no answer (a service with TLS on), and a helper that exited is not recorded' `
    ($bootstrap990 -match 'existing\.Reachable \|\| PortAcceptsConnections\(requestedEndpoint\)' -and
     $bootstrap990 -match '(?s)ownedSidecarProcessId is \{ \} failedId && !ProcessIsRunning\(failedId\).{0,200}SidecarState\.Delete') -Severity Critical
$catalog990 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\CrashSignatureCatalog.cs'
$crash990 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessCrashAndSaveToolsService.cs'
$api990 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.9.0: crash analysis names a PalDefender session that ended on a player joining: never the newest log of a running server, without the player''s address (v0.9.10.0: no time window, a stable evidence line)' `
    ($catalog990 -match 'new\(new\("exit-after-join", "Server session ended on a player joining", "Critical"' -and
     $catalog990 -match 'public static class ExitAfterJoinDetector' -and $catalog990 -match 'if \(i == 0 && serverRunning\) continue;' -and
     $catalog990 -notmatch 'TimeSpan Window' -and $catalog990 -match 'Address\.Replace\(' -and
     $crash990 -match 'ExitAfterJoinDetector\.Detect\(logs, running\)' -and $crash990 -match 'AddDays\(-7\)' -and
     $api990 -match 'new HeadlessCrashAndSaveToolsService\(paths, activity, \(\) => lifecycle\.GetStatusAsync\(\)\.GetAwaiter\(\)\.GetResult\(\)\.Processes\.Count > 0\)') -Severity Critical
$updates990 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessComponentUpdateService.cs'
$doctor990 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiagnosticsService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.9.0: UE4SS without a recorded install is matched by content against the three newest releases'' downloads (cached), and the Doctor''s port finding has an Admin-only fix that sets PublicPort to the bound port' `
    ($updates990 -match 'MatchInstalledUe4ssAsync\(' -and $updates990 -match 'public static class Ue4ssReleaseFiles' -and $updates990 -match 'ue4ss-release-hashes\.json' -and
     $updates990 -match '\.Take\(3\)\.ToArray\(\), ct\)' -and
     $doctor990 -match 'ActionKind: same \? null : "align-public-port"' -and $doctor990 -match 'case "align-public-port":' -and
     $doctor990 -match 'Changing the server''s settings needs the Admin role\.') -Severity Critical
$culture990 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\DisplayCulture.cs'
$localizer990 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\Localizer.cs'
$art990 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.9.0: numbers, dates and times follow the chosen language through a culture that keeps the system''s text rules, and the ArtworkHarness checks the formats' `
    ($culture990 -match 'var culture = \(CultureInfo\)original\.Clone\(\);' -and $culture990 -match 'culture\.NumberFormat = ' -and $culture990 -match 'culture\.DateTimeFormat = ' -and
     $localizer990 -match 'DisplayCulture\.Apply\(language\.Code\);' -and
     $art990 -match 'German formats: 1\.234,5 and 14:05' -and $art990 -match 'no dotless i in names and ids') -Severity Critical
$logic990 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.9.0: the logic harness covers the helper record, exit after join and the UE4SS match; the route smoke and the distribution check exist' `
    ($logic990 -match 'Desktop helper record: it is written, read back and removed' -and
     $logic990 -match 'Crash analysis: a PalDefender session log that ends on a player joining is reported' -and
     $logic990 -match 'UE4SS release match: the DLL inside a release download is hashed' -and
     (Test-Path (Join-Path $root 'scripts\Test-v0.9.9.0-RouteSmoke.ps1')) -and (Test-Path (Join-Path $root 'scripts\Test-v0.9.9.0-Distribution.ps1'))) -Severity Critical
$alerts990 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.9.0: the out-of-date alert also covers a UE4SS update (from the cached result, only on a definite answer) and installed MODs with an update, and the helper is not restarted over and over for a server it does not have' `
    ($alerts990 -match 'Track\("ue4ss-outdated"' -and $alerts990 -match 'Track\("mods-outdated"' -and $alerts990 -match 'public static bool\? Ue4ssBehind' -and
     $updates990 -match 'public ComponentVersionInfo\? PeekUe4ssStatus\(\)' -and $updates990 -match 'Ue4ssRefresh = TimeSpan\.FromHours\(6\)' -and
     $bootstrap990 -match 'StartedWithin\(owned\.ProcessId, TimeSpan\.FromSeconds\(30\)\)') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.9.9.0-helper-crash-formats.md' 'Regression' 'v0.9.9.0: v0.9.9.0 architecture doc is present' -Severity Critical

$bootstrap9100 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\LocalManagementBootstrapper.cs'
$sidecar9100 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\SidecarState.cs'
$art9100 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.10.0: a recorded helper that is alive but slow is waited for (each answer allowed 4 s) and reused; one that cannot be used is replaced only after it has exited; a record is matched by path and start time; the ArtworkHarness proves it with stand-in helpers' `
    ($bootstrap9100 -match 'var alive = IsRecordedHelper\(owned\);' -and
     $bootstrap9100 -match 'if \(alive && !ownedProbe\.Reachable\) ownedProbe = await WaitForAnswerAsync\(owned\.Endpoint, StartupTimeout, cancellationToken\);' -and
     $bootstrap9100 -match 'if \(alive && ownedProbe\.Compatible' -and $bootstrap9100 -match 'if \(!StopHelperProcess\(owned\)\)' -and
     $bootstrap9100 -match 'cannot be used and could not be stopped' -and $bootstrap9100 -match 'return process\.WaitForExit\(10000\);' -and
     $bootstrap9100 -match 'SlowProbeTimeout = TimeSpan\.FromSeconds\(4\)' -and
     $bootstrap9100 -match '\(process\.StartTime\.ToUniversalTime\(\) - recorded\.UtcDateTime\)\.Duration\(\) < TimeSpan\.FromSeconds\(5\)' -and
     $sidecar9100 -match 'DateTimeOffset\? StartedUtc = null' -and
     $art9100 -match 'A recorded helper that answers only after 2 s \(the probe waits 750 ms\) is waited for and reused, not replaced' -and
     $art9100 -match 'A recorded helper that never answers is stopped, and has exited before a replacement is tried' -and
     $art9100 -match 'a process id reused by another program\) is neither reused nor stopped') -Severity Critical
$catalogText9100 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MessageCatalog.cs'
$xaml9100 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.10.0: a name inside a translated message stays a name (quoted, or after server/world/player/guild and the like), the dashboard shows the server name and description untranslated, and the ArtworkHarness checks "Ready", "None" and "Backup"' `
    ($catalogText9100 -match 'if \(template\.IsVerbatim\(i\)\) continue;' -and $catalogText9100 -match 'private static bool HoldsName\(string english, Match placeholder\)' -and
     $catalogText9100 -match 'server\|world\|player\|guild\|profile\|tab\|kit\|mod\|rule\|channel\|account\|user\|member\|named' -and
     $xaml9100 -match 'Text="\{Binding DashboardServerName\}"' -and $xaml9100 -match 'Text="\{Binding DashboardServerDescriptionVerbatim\}"' -and $xaml9100 -notmatch 'TrText DashboardServer' -and
     $art9100 -match 'keeps its name inside a translated message' -and $art9100 -match 'new\[\] \{ "Ready", "None", "Backup" \}') -Severity Critical
$alerts9100 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'
$mods9100 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs'
$updates9100 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessComponentUpdateService.cs'
$logic9100 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.10.0: a MOD whose update state is unknown is not "up to date": the inventory carries UpdateChecked and the alert resolves only when every MOD it named is checked and current, or removed' `
    ($mods9100 -match 'UpdateChecked = check\.HasKnownSource && check\.Compared,' -and $mods9100 -match 'bool UpdateChecked = false\);' -and
     $mods9100 -match 'could not read file timestamps to compare\.", Compared: false\)' -and
     $alerts9100 -match 'public static bool\? ModsBehind\(IReadOnlyList<ModUpdateState> mods, IReadOnlyCollection<string> alertNames\)' -and
     $alerts9100 -match 'if \(ComponentAlerts\.ModsBehind\(modStates, modsAlertNames\) is \{ \} modsBehind\)' -and
     $alerts9100 -notmatch 'Every installed MOD is up to date' -and
     $logic9100 -match 'MOD update alert: an unknown update state never resolves the alert') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v0.9.10.0: the MystTiq update check reads the release list, counts prereleases while MystTiq is 0.x (stable only from 1.0), never drafts, and picks the newest by version' `
    ($updates9100 -match 'repos/\{MystTiqRepo\}/releases\?per_page=30' -and $updates9100 -notmatch 'repos/\{MystTiqRepo\}/releases/latest' -and
     $updates9100 -match 'public static class ReleaseChannel' -and $updates9100 -match '\[JsonPropertyName\("prerelease"\)\] public bool Prerelease' -and
     $updates9100 -match '\.Where\(r => !r\.Draft && \(prereleases \|\| !r\.Prerelease\)\)' -and
     $logic9100 -match 'MystTiq release check: a development version counts prereleases') -Severity Critical
$catalog9100 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\CrashSignatureCatalog.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.10.0: the join-crash evidence no longer depends on when it is read: no time window, the same line for the same session, and v0.9.9.0''s recorded lines still match' `
    ($catalog9100 -match 'IReadOnlyList<string> Detect\(IReadOnlyList<\(string Name, DateTimeOffset Started, DateTimeOffset LastWrite, IReadOnlyList<string> Lines\)> logsNewestFirst, bool serverRunning\)' -and
     $catalog9100 -match 'PalDefender session \(ended right after a player joined\|log ends on a player joining\)' -and
     $logic9100 -match 'the same session gives the same evidence line after the next one starts, whenever it is read' -and
     $logic9100 -match 'evidence recorded by v0\.9\.9\.0 is still claimed by the signature') -Severity Critical
$doctor9100 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiagnosticsService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.10.0: the port finding allows for a router that forwards another outside port on purpose, and Fix stays an offer' `
    ($doctor9100 -match 'Unless your router forwards outside port \{advertised\} to \{bound\}' -and
     $doctor9100 -match 'If your router forwards outside port \{advertised\} to \{bound\} on purpose, leave this as it is\.') -Severity Critical
$i18nRoot9100 = Join-Path $root 'src\MystTiq.Desktop\Assets\i18n'
$english9100 = Get-Content (Join-Path $i18nRoot9100 'en.json') -Raw | ConvertFrom-Json -AsHashtable
$labelLeft9100 = @(foreach ($code in 'zh-Hans', 'es', 'pt-BR', 'ru', 'de', 'fr', 'ja', 'ko', 'it', 'pl', 'tr') {
    $lang = Get-Content (Join-Path $i18nRoot9100 "$code.json") -Raw | ConvertFrom-Json -AsHashtable
    foreach ($key in 'msg.level_7a59bc', 'msg.gender', 'msg.nickname') { if ($lang[$key] -match '^(Level|Gender|NickName)') { "$code $key" } }
})
$de9100 = Get-Content (Join-Path $i18nRoot9100 'de.json') -Raw | ConvertFrom-Json -AsHashtable
Add-MystTiqCheck $ctx 'Regression' 'v0.9.10.0: the Pal-edit preview labels are translated in all 11 languages, German says "Stopp erzwingen", and the MOD alert wording is count-neutral' `
    ($labelLeft9100.Count -eq 0 -and $de9100['ribbon.ForceStop'] -eq 'Stopp erzwingen' -and $de9100['msg.alert_mods_behind'] -notmatch '\(s\)' -and
     $english9100['msg.alert_mods_behind'] -eq 'Installed MODs with an update: {0} ({1}). Update them from the MOD Library with the server stopped.') `
    -Severity High -Details ($labelLeft9100 -join ', ')
$notes9100 = Get-MystTiqText $ctx 'CHECKPOINT_NOTES.md'
Add-MystTiqCheck $ctx 'Regression' 'v0.9.10.0: the checkpoint notes shipped in the source carry the release''s review stamp (the v0.9.9.0 archive''s did not)' `
    ($notes9100 -match '^<!-- MystTiq v\d+\.\d+\.\d+\.\d+: file reviewed for this release') -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.9.10.0-review-fixes.md' 'Regression' 'v0.9.10.0: v0.9.10.0 architecture doc is present' -Severity Critical

$workflow1000 = Get-MystTiqText $ctx '.github\workflows\release.yml'
$logic1000 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.0: the release workflow makes a draft full release for v1 and later and a prerelease only for a v0.x tag, which the update check (full releases only from 1.0) needs' `
    ($workflow1000 -match "prerelease: \$\{\{ startsWith\(env\.RELEASE_TAG, 'v0\.'\) \}\}" -and $workflow1000 -notmatch 'prerelease: true' -and
     $workflow1000 -match 'draft: true' -and
     $logic1000 -match 'ReleaseChannel\.PickNewest\(list, "1\.0\.0\.0"\)\?\.Tag == "v0\.8\.25\.0"') -Severity Critical
$readme1000 = Get-MystTiqText $ctx 'README.md'
$site1000 = Get-MystTiqText $ctx 'docs\index.html'
$notesPublic1000 = Get-MystTiqText $ctx 'release-notes\v1.0.0.0.md'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.0: the README, site and release notes present the stable release; open items and verification notes are not in them (the owner keeps those in the internal docs)' `
    ($readme1000 -notmatch '## Known limitations' -and $readme1000 -notmatch 'pre--1\.0' -and $readme1000 -notmatch 'awaiting native' -and
     $readme1000 -match 'badge/release-1\.0' -and $readme1000 -match 'MystTiq 1\.0 is the first stable release' -and
     $site1000 -notmatch 'limits-title' -and $site1000 -match '<span class="tag">STABLE RELEASE</span>' -and $site1000 -notmatch 'awaiting native review' -and
     $notesPublic1000 -notmatch '## Known limitations') -Severity High
$internal1000 = Get-MystTiqText $ctx 'docs\architecture\v1.0.0.0-stable-release.md'
$roadmap1000 = Get-MystTiqText $ctx 'docs\roadmap\PRODUCT_ROADMAP.md'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.0: the internal docs keep what stays open after 1.0: translations, live integrations, a screen-reader pass, Linux and the known code limits' `
    ($internal1000 -match '## Open after 1\.0' -and $internal1000 -match 'no native-speaker review has been recorded' -and
     $internal1000 -match 'no pass with a real screen reader' -and $internal1000 -match 'The Linux download is not published' -and
     $roadmap1000 -match '## Open after 1\.0 — integration and stabilization') -Severity Critical
$props1000 = Get-MystTiqText $ctx 'Directory.Build.props'
$artifactConfig1000 = Get-MystTiqText $ctx '.signpath\artifact-configuration.xml'
$package1000 = Get-MystTiqText $ctx 'scripts\Package-GitHubRelease.ps1'
$proxyBuild1000 = Get-MystTiqText $ctx 'scripts\Build-ConsoleProxy.ps1'
$rc1000 = Get-MystTiqText $ctx 'native\MystTiqConsoleProxy\MystTiqConsoleProxy.rc'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.0: code signing through SignPath: the workflow stages, uploads, signs after approval, checks every MystTiq binary''s signature and zips the signed folder when the SignPath variables are set (unsigned otherwise); the artifact configuration signs the seven MystTiq binaries with the product name and version' `
    ($workflow1000 -match 'uses: signpath/github-action-submit-signing-request@v3' -and $workflow1000 -match "if: \$\{\{ vars\.SIGNPATH_ORGANIZATION_ID != '' \}\}" -and
     $workflow1000 -match 'github-artifact-id: \$\{\{ steps\.upload-unsigned\.outputs\.artifact-id \}\}' -and $workflow1000 -match 'api-token: \$\{\{ secrets\.SIGNPATH_API_TOKEN \}\}' -and
     $workflow1000 -match "if \(\`$signature\.Status -ne 'Valid'\)" -and $workflow1000 -match 'Package-GitHubRelease\.ps1 -Runtime win-x64 -FromFolder \$signed' -and
     $workflow1000 -match 'version: \$\{\{ toJSON\(env\.RELEASE_VERSION\) \}\}' -and
     $package1000 -match '\[switch\]\$StageOnly' -and $package1000 -match '\[string\]\$FromFolder' -and
     @([regex]::Matches($artifactConfig1000, '<pe-file path="[^"]+" product-name="MystTiq Palworld Server Manager" product-version="\$\{version\}">')).Count -eq 7 -and
     $props1000 -match '<Product>MystTiq Palworld Server Manager</Product>' -and $props1000 -match '<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>' -and
     $rc1000 -match 'VALUE "ProductName", "MystTiq Palworld Server Manager"' -and $proxyBuild1000 -match 'rc\.exe /nologo /i') -Severity Critical
$policy1000 = Get-MystTiqText $ctx 'CODE_SIGNING_POLICY.md'
$privacy1000 = Get-MystTiqText $ctx 'PRIVACY.md'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.0: the code signing policy (SignPath''s required wording, roles) and the privacy policy are published and linked from the README; the setup steps and the v1.1 MOD browser plan are in the internal docs' `
    ($policy1000 -match 'Free code signing provided by \[SignPath\.io\]\(https://about\.signpath\.io/\), certificate by\s+\[SignPath Foundation\]\(https://signpath\.org/\)' -and
     $policy1000 -match 'Approvers \(release signing\)' -and $privacy1000 -match 'collects no telemetry' -and
     $readme1000 -match '\[Code signing policy\]\(CODE_SIGNING_POLICY\.md\) · \[Privacy policy\]\(PRIVACY\.md\)' -and
     (Test-Path (Join-Path $root 'docs\release\CODE_SIGNING.md')) -and $roadmap1000 -match '## v1\.1 — MOD browser \(planned, requested 2026-09-30\)') -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v1.0.0.0-stable-release.md' 'Regression' 'v1.0.0.0: v1.0.0.0 architecture doc is present' -Severity Critical

$launcher1001 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.Launcher.cs'
$window1001 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$mainVm1001 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$nav1001 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\NavigationPage.cs'
$winLife1001 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsServerLifecycleService.cs'
$linLife1001 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs'
$config1001 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\HeadlessConfigurationService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.1: Server > Launcher is its own page with Default, No Mods and Show Window presets, and the editor stores MystTiq-only launcher metadata' `
    ($nav1001 -match '\bLauncher\b' -and $mainVm1001 -match 'public bool IsLauncherPage => SelectedPage == NavigationPage\.Launcher;' -and
     $window1001 -match 'CommandParameter="Launcher"' -and $window1001 -match 'IsVisible="\{Binding IsLauncherPage\}"' -and
     $window1001 -match 'CommandParameter="Default"' -and $window1001 -match 'CommandParameter="NoMods"' -and $window1001 -match 'CommandParameter="ShowWindow"' -and
     $launcher1001 -match '@mysttiq:launcherVersion=1' -and $launcher1001 -match 'ConfigLauncherExecutable = "PalServer\.exe";') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.1: a profile without saved Launcher choices starts like a double-click (the owner, 2026-10-04): no arguments at all, -port= only off 8211, no redirected console, PalServer.exe from the server root; the Like double-click preset starts it through Windows with a normal window; the smoke checks what reaches the server; launcher metadata never reaches PalServer' `
    ($winLife1001 -match '"-unattended",\s*"-useperfthreads",\s*"-NoAsyncLoadingThread",\s*"-UseMultithreadForDS"' -and
     $winLife1001 -match 'ReadBool\(values, "redirectStandardOutput", false\)' -and $winLife1001 -match 'ReadBool\(values, "createNoWindow", false\)' -and
     $winLife1001 -match 'Read\(values, "executable", "PalServer\.exe"\)' -and $winLife1001 -match '!argument\.StartsWith\(LauncherOptionPrefix' -and
     $linLife1001 -match '!argument\.Trim\(\)\.StartsWith\("@mysttiq:"' -and
     $config1001 -match 'server\.Runtime == ServerRuntimeKind\.WindowsNative' -and
     $winLife1001 -match 'private const int DefaultGamePort = 8211;' -and $winLife1001 -match 'new HashSet<string>\(StringComparer\.OrdinalIgnoreCase\) \{ "-log", "-stdout", "-FullStdOutLogOutput" \}' -and
     $winLife1001 -notmatch 'clean\.Add\("-log"\)' -and $winLife1001 -notmatch 'clean\.Add\($"-abslog=' -and
     $launcher1001 -match 'mode\.Equals\("DoubleClick"' -and $launcher1001 -match '(?s)mode\.Equals\("DoubleClick".{0,900}ConfigLauncherUseShellExecute = true;' -and $window1001 -match 'CommandParameter="DoubleClick"' -and
     (Get-MystTiqText $ctx 'scripts\Test-v1.0.0.1-RouteSmoke.ps1') -match 'expected only -port=') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.1: the Launcher page''s inputs are named for screen readers, the command line is shown untranslated, and its notes are translated' `
    ($window1001 -match 'AutomationProperties\.Name="\{services:Tr ui\.launcher_executable\}"' -and $window1001 -match 'AutomationProperties\.Name="\{services:Tr ui\.port\}" Value="\{Binding ConfigLauncherPort' -and
     $window1001 -match 'Text="\{Binding ConfigLauncherEffectiveCommand, Mode=OneWay\}"' -and $window1001 -notmatch 'TrText ConfigLauncherEffectiveCommand' -and
     $window1001 -match 'Text="\{services:TrText ConfigLauncherWarning\}"') -Severity High
$guard1001 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessIdentityGuardService.cs'
$uid1001 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\SteamPlayerUid.cs'
$api1001 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$logic1001 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.1: the identity guard: the expected player ID from a Steam ID (CityHash64, checked against the real characters), a player who did not get their existing character is caught on the player poll, recorded, notified and kicked by default' `
    ($uid1001 -match 'folded = unchecked\(\(uint\)\(\(hash & 0xFFFFFFFF\) \+ \(hash >> 32\) \* 23\)\)' -and $uid1001 -match 'Encoding\.Unicode\.GetBytes' -and
     $guard1001 -match 'public static IdentityVerdict\? Judge\(' -and $guard1001 -match 'characterExists\(expected\)' -and
     $guard1001 -match 'public static IdentityGuardConfig Default \{ get; \} = new\(true, true\);' -and $guard1001 -match 'notifications\.Create\("Critical"' -and
     $api1001 -match 'await p\.IdentityGuard\.EnforceAsync\(players, token\);' -and $api1001 -match 'routes\.MapGet\("/players/identity-guard"' -and
     $logic1001 -match 'Wade''s Steam ID gives 67D8D355, the ID of his real character') -Severity Critical
$app1001 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\App.axaml.cs'
$appXaml1001 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\App.axaml'
$mainCode1001 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
$exit1001 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.Exit.cs'
$boot1001 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\LocalManagementBootstrapper.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.1: closing the window always goes to the tray; Exit stops every server the owned helper runs, then the helper, then the app; there is no "Exit GUI only"' `
    ($mainCode1001 -match '(?s)private void MainWindow_Closing\(object\? sender, WindowClosingEventArgs e\)\s*\{\s*if \(Application\.Current is not App app \|\| app\.IsExplicitExitRequested\) return;\s*e\.Cancel = true;\s*app\.CloseToTray\(\);' -and
     $app1001 -match 'ShutdownAllForExitAsync\(force: false, localBootstrapper\?\.OwnedHelperEndpoint\)' -and $app1001 -match 'await localBootstrapper\.StopOwnedSidecarAsync\(\);' -and
     $exit1001 -match 'await _api\.GetServerProfilesAsync\(helper' -and $exit1001 -match 's\.Status\.IsProcessLive' -and
     $boot1001 -match 'public string\? OwnedHelperEndpoint' -and $appXaml1001 -notmatch 'ExitGui_OnClick' -and
     -not (Test-Path (Join-Path $root 'src\MystTiq.Desktop\Views\ConfirmMinimizeToTrayDialog.axaml'))) -Severity Critical
$watch1001 = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\StartupWatch.cs'
$safeStart1001 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessModSafeStartService.cs'
$address1001 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAddressService.cs'
$art1001 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
$fake1001 = Get-MystTiqText $ctx 'scripts\Testing\FakePalServer\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.1: the stuck-start protocol: a start is stuck after two minutes without its port and says how long; a test load with every MOD off or one MOD at a time, from a stuck start too, every switch checked and every start timed; the Dashboard offers it' `
    ($watch1001 -match 'StuckAfter = TimeSpan\.FromMinutes\(2\)' -and $winLife1001 -match 'StartupWatch\.NotReadyDetail\(expectedGamePort, startedAt, now\)' -and
     $linLife1001 -match 'StartupWatch\.NotReadyDetail\(expectedGamePort, startedAt, now\)' -and
     $safeStart1001 -match 'public enum SafeStartMode \{ OneAtATime, TestLoad \}' -and $safeStart1001 -match 'var stopStuckFirst = status\.Processes\.Count > 0 && status\.StartupStuck;' -and
     $safeStart1001 -match 'private async Task SwitchAsync\(HeadlessModItem mod, bool enabled\)' -and
     $api1001 -match 'string\.Equals\(mode, "testload", StringComparison\.OrdinalIgnoreCase\) \? SafeStartMode\.TestLoad' -and
     $window1001 -match 'x:Name="StuckStartPanel"' -and $window1001 -match 'Command="\{Binding BeginStuckTestLoadCommand\}"' -and
     $fake1001 -match 'HangsStartup:1' -and (Test-Path (Join-Path $root 'scripts\Test-v1.0.0.1-RouteSmoke.ps1'))) -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.1: the Dashboard lists the local addresses and the public address with the server''s port; the public one comes from the router first, then api.ipify.org, kept an hour; the privacy policy says so' `
    ($address1001 -match 'PublicRefresh = TimeSpan\.FromHours\(1\)' -and $address1001 -match 'wan\.GetRouterWanAddressAsync' -and $address1001 -match 'api\.ipify\.org' -and
     $api1001 -match 'routes\.MapGet\("/network/addresses"' -and $window1001 -match 'x:Name="DashboardAddresses"' -and $window1001 -match 'Text="\{Binding PublicAddressVerbatim\}"' -and
     $art1001 -match 'The addresses line shows every local address and the public one' -and
     (Get-MystTiqText $ctx 'PRIVACY.md') -match 'Dashboard''s public address') -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v1.0.0.1-launcher-identity-tray.md' 'Regression' 'v1.0.0.1: v1.0.0.1 architecture doc is present' -Severity Critical

$names1002 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessNameGuardService.cs'
$api1002 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$logic1002 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.2: unique names: names compare with case ignored; each belongs to the first account seen with it or to the account it is reserved for (none: blocked); another account using it is caught on the player poll, recorded, notified and kicked by default; on first use the known players own their names, the earliest first' `
    ($names1002 -match '\.Split\(\(char\[\]\?\)null, StringSplitOptions\.RemoveEmptyEntries\)\)\.ToUpperInvariant\(\)' -and
     $names1002 -match 'public static NameGuardConfig Default \{ get; \} = new\(true, true, \[\]\);' -and
     $names1002 -match 'public static NameGuardOutcome Judge\(' -and $names1002 -match 'public static NameGuardSeed Seed\(' -and
     $names1002 -match 'registry\.Snapshot\(\)' -and $names1002 -match 'notifications\.Create\("Warning", "Player name already taken"' -and
     $names1002 -match 'playerModeration\.ExecuteAsync\("kick", clash\.Player\.Account' -and
     $api1002 -match 'await p\.NameGuard\.EnforceAsync\(players, token\);' -and
     $api1002 -match '(?s)routes\.MapGet\("/players/name-guard".{0,200}RequireRole\(MystTiqRole\.Viewer' -and
     $api1002 -match '(?s)routes\.MapPut\("/players/name-guard".{0,200}RequireRole\(MystTiqRole\.Admin' -and
     $logic1002 -match 'Unique names: a name belongs to the first account \(case ignored\)') -Severity Critical
$window1002 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$roles1002 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.CommandRoles.cs'
$art1002 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.2: the Players page has the Unique player names card for admins: on/off and kick, reserve a name for a Steam ID or block it, release, save, and the players turned away; names and IDs are shown as typed, the rest translated' `
    ($window1002 -match 'x:Name="UniqueNamesCard"' -and $window1002 -match 'Command="\{Binding ReserveNameCommand\}"' -and
     $window1002 -match 'Command="\{Binding SaveNameGuardCommand\}"' -and $window1002 -match 'IsChecked="\{Binding NameGuardConfig\.KickDuplicates\}"' -and
     $window1002 -match 'Text="\{Binding NameVerbatim\}"' -and $window1002 -match 'Text="\{services:TrText Kind\}"' -and
     $roles1002 -match 'SaveNameGuardCommand' -and
     $art1002 -match 'German: the card''s status, the kinds and the service''s notice are translated') -Severity High
$vmMain1002 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.2: a button for a role-gated command is enabled again when the app stops being busy (seen live: Save names and Save Whitelist stayed disabled after a sign-in or connect while busy)' `
    ($roles1002 -match 'private void RaiseAllAsyncCommandStates\(\)' -and $vmMain1002 -match '(?s)private void RaiseIsBusyDependents\(bool value\).{0,1600}RaiseAllAsyncCommandStates\(\);' -and
     $art1002 -match 'Save names and Save Whitelist are disabled while busy and enabled again afterwards') -Severity High
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.2: the unique-names smoke covers first use from the known players, a saved list made consistent and a restart' `
    ((Get-MystTiqText $ctx 'scripts\Test-v1.0.0.2-RouteSmoke.ps1') -match 'the saved list survives a restart') -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v1.0.0.2-unique-player-names.md' 'Regression' 'v1.0.0.2: v1.0.0.2 architecture doc is present' -Severity Critical

$catalog1003 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\NativeModCatalog.cs'
$mods1003 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs'
$env1003 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessEnvironmentChecklistService.cs'
$logic1003 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.3: NATIVE MODs: PalDefender (d3d9.dll with d3d9_config.json, or version.dll) and the UE4SS loader (dwmapi.dll, xinput1_3.dll) are listed from their loaders; switched-off copies in any common form are recognised, MystTiq''s own restored first, else the newest; switching writes or restores <loader>.mysttiq-disabled; disable all, enable all and the stuck-start tests include them' `
    ($catalog1003 -match 'private static readonly string\[\] PalDefenderLoaders = \["d3d9\.dll", "version\.dll"\];' -and
     $catalog1003 -match 'private static readonly string\[\] Ue4ssLoaders = \["dwmapi\.dll", "xinput1_3\.dll"\];' -and
     $catalog1003 -match 'public const string DisableSuffix = "\.mysttiq-disabled";' -and $catalog1003 -match '"\.disabled-test"' -and
     $catalog1003 -match 'candidates\.OrderByDescending\(f => f\.LastWriteUtc\)' -and $catalog1003 -match 'public static bool ConfigLoads\(' -and
     $mods1003 -match 'mods\.AddRange\(natives\);' -and $mods1003 -match 'private int ToggleNative\(string package, bool enabled\)' -and
     $mods1003 -match 'item\.Type == "NATIVE" \? Math\.Max\(0, ToggleNative\(item\.Package, enabled\)\)' -and
     $mods1003 -match 'type\.Equals\("NATIVE", StringComparison\.OrdinalIgnoreCase\)\s*\?\s*ToggleNative\(package, enabled\)' -and
     $env1003 -match 'NativeModCatalog\.DisabledSuffixes' -and
     $logic1003 -match 'NATIVE MODs: PalDefender and the UE4SS loader are found from their loaders') -Severity Critical
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.3: with the UE4SS loader switched off an enabled UE4SS MOD is shown as not loading; a NATIVE MOD cannot be deleted, rolled back or repaired, and a PalDefender ZIP is not installed as a UE4SS folder' `
    ($mods1003 -match 'Attention = "UE4SS loader is off"' -and $mods1003 -match 'private static string NativeRefusal\(string type\)' -and
     $mods1003 -match 'Path\.GetFileName\(f\)\.Equals\("PalDefender\.dll", StringComparison\.OrdinalIgnoreCase\)' -and
     $mods1003 -match 'if \(type\.Equals\("NATIVE", StringComparison\.OrdinalIgnoreCase\)\) return HeadlessModMutationResult\.Failure\(NativeRefusal\(type\)\);') -Severity High
$window1003 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$windowCode1003 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
$art1003 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.3: a MOD ZIP dropped anywhere on the drop zone installs: the zone has a background (it took drops only on its button and caption), handles drag-enter, and the harness drops a ZIP on its empty corner' `
    ($window1003 -match '<Border x:Name="ZipInstallDropZone" Background="Transparent"' -and
     $windowCode1003 -match 'ZipInstallDropZone\.AddHandler\(DragDrop\.DragEnterEvent, ZipInstallDropZone_OnDragOver\);' -and
     $art1003 -match 'A ZIP dropped on the empty part of the MOD drop zone reaches the install' -and $art1003 -match 'RawDragEventType\.Drop') -Severity High
Add-MystTiqCheck $ctx 'Regression' 'v1.0.0.3: the NATIVE MOD smoke covers the owner''s layout: both loaders renamed by hand, the July copy left alone, MystTiq''s own copy first, disable/enable all and the refusals' `
    ((Get-MystTiqText $ctx 'scripts\Test-v1.0.0.3-RouteSmoke.ps1') -match 'the July copy must stay switched off') -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v1.0.0.3-native-mods.md' 'Regression' 'v1.0.0.3: v1.0.0.3 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 3. v1.0.0.4 Contracts -- backup and restore checked by the world's in-game day (reported 2026-10-05: "the backup and
#    restore function doesnt seem to work correctly. verify using the day of the game server after a restoration")
# ---------------------------------------------------------------------------
$clock1004 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessWorldClockService.cs'
$explorer1004 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessWorldExplorerService.cs'
$api1004 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$logic1004 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v1.0.0.4 Contracts' 'the world''s day: read from the decoded copy beside Level.sav with whether it is current; re-decoded (from a copy of Level.sav, at most every two minutes) when Level.sav is newer; both world views use the one reader' `
    ($clock1004 -match 'RefreshEvery = TimeSpan\.FromMinutes\(2\)' -and $clock1004 -match 'public void RefreshInBackgroundIfStale\(string worldPath\)' -and
     $clock1004 -match 'File\.Copy\(level, copy, true\);' -and $clock1004 -match 'public static bool IsCurrent\(DateTime decodedUtc, DateTime levelUtc\)' -and
     $clock1004 -match 'public static string\? NewestWorldLevel\(' -and
     $api1004 -match 'p\.WorldClock\.RefreshInBackgroundIfStale\(world\.ActiveWorldPath\)' -and
     ([regex]::Matches($explorer1004, 'HeadlessWorldClockService\.Read\(active\.WorldPath, HeadlessWorldClockService\.CacheRootFor\(paths\)\)')).Count -eq 2 -and $explorer1004 -notmatch 'TryReadGameDateTimeTicks' -and
     $logic1004 -match 'World clock: the day and time from GameDateTimeTicks') -Severity Critical
$backup1004 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessBackupService.cs'
$lockers1004 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\FileLockers.cs'
Add-MystTiqCheck $ctx 'v1.0.0.4 Contracts' 'a restore retries while the save folder is briefly held, then names the program holding it and changes nothing; refuses a PalServer started outside MystTiq; reports the restored world''s day against the backup''s; every restore, refused, failed or done, is in the activity log' `
    ($backup1004 -match 'if \(attempt < 10\) \{ await Task\.Delay\(500, cancellationToken\); continue; \}' -and $backup1004 -match 'FileLockers\.UnderFolder\(paths\.SaveRoot\)' -and
     $lockers1004 -match 'RmGetList' -and $backup1004 -match 'private IReadOnlyList<string> ServerProcessesOutsideMystTiq\(\)' -and
     $backup1004 -match 'DescribeRestoredWorldAsync\(fileName, cancellationToken\)' -and $backup1004 -match ', as in the backup\.' -and
     $backup1004 -match '"Restored backup"' -and $backup1004 -match '"Restore failed"' -and ([regex]::Matches($backup1004, '"Restore refused"')).Count -ge 2) -Severity Critical
$window1004 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
$vm1004 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$art1004 = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs'
Add-MystTiqCheck $ctx 'v1.0.0.4 Contracts' 'each backup''s world day is read from its own Level.sav in the background and listed; the Dashboard says when the day comes from an older decode; the restore message is translated with its day' `
    ($backup1004 -match 'private void QueueWorldDays\(\)' -and $backup1004 -match '"world-days\.json"' -and $backup1004 -match 'public long\? WorldDayNumber \{ get; init; \}' -and
     $window1004 -match 'Text="\{services:Tr ui\.world_day\}"' -and $window1004 -match 'Text="\{services:TrText WorldDayText\}"' -and
     $vm1004 -match 'world\.WorldClockCurrent \|\| world\.WorldClockAsOfUtc is null' -and
     $art1004 -match 'German: the restore message with the world''s day is translated') -Severity High
$access1004 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\SaveFolderAccess.cs'
$fix1004 = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\SaveFolderAccessFix.cs'
Add-MystTiqCheck $ctx 'v1.0.0.4 Contracts' 'a save folder MystTiq may not replace (found live: it belonged to administrators) is detected without changing anything, reported in the backups list and before a restore; Fix Save Folder Access grants this account Modify through Windows'' administrator prompt, only for a server on this computer; the day stays current through MystTiq''s own decoded copy' `
    ($access1004 -match 'CreateFile\(folder, Delete, ShareAll' -and $access1004 -match 'Marshal\.GetLastWin32Error\(\) != 5' -and
     $backup1004 -match 'if \(!SaveFolderAccess\.CanReplace\(paths\.SaveRoot\)\)' -and $backup1004 -match 'SaveFolderReplaceable = SaveFolderAccess\.CanReplace\(paths\.SaveRoot\)' -and
     $fix1004 -match '/grant ''\*\{accountSid' -and $fix1004 -match 'ElevatedPowerShell\.RunAsync' -and
     (Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.SaveAccess.cs') -match 'ElevatedFirewall\.CanRun\(SelectedProfile\)' -and
     $window1004 -match 'x:Name="SaveFolderAccessCard"' -and $clock1004 -match 'public static string CacheRootFor\(IServerPathProfile paths\)' -and
     $art1004 -match 'Fix Save Folder Access grants this account Modify on Pal') -Severity Critical
Add-MystTiqCheck $ctx 'v1.0.0.4 Contracts' 'the restore smoke restores real backups into an isolated server and checks the day after each restore, a held file, a PalServer outside MystTiq, a newer save and the activity log' `
    ((Get-MystTiqText $ctx 'scripts\Test-v1.0.0.4-RouteSmoke.ps1') -match 'restoring a backup makes the world that backup') -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v1.0.0.4-restore-world-day.md' 'v1.0.0.4 Contracts' 'v1.0.0.4 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v1.0.0.4 is documented' ([regex]::IsMatch($docText, 'v1\.0\.0\.4')) -Severity High
foreach ($doc in 'release-notes\v1.0.0.4.md', 'release-notes\BUILD_TEST_PLAN_v1.0.0.4.md', 'release-notes\APPLY_v1.0.0.4_CHANGED_FILES.md') {
    Test-MystTiqFile $ctx $doc 'Documentation' "$doc exists" -Severity High
}
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v1.0.0.4 entry' ($changelogText -match '## v1\.0\.0\.4') -Severity High
$readme1004 = Get-MystTiqText $ctx 'README.md'
Add-MystTiqCheck $ctx 'Documentation' 'README and the site name v1.0.0.4 as the current version and v1.0.0.0 as the accepted baseline, without open items' `
    ($readme1004 -match 'Current version: v1\.0\.0\.4 · Accepted baseline: v1\.0\.0\.0' -and $readme1004 -match '\[Release notes\]\(release-notes/v1\.0\.0\.4\.md\)' -and
     $readme1004 -match '\| v1\.0\.0\.4 \| Current \|' -and $readme1004 -notmatch '## Known limitations' -and
     (Get-MystTiqText $ctx 'docs\index.html') -match 'Current version: v1\.0\.0\.4') -Severity High
$publish1004 = Get-MystTiqText $ctx 'docs\release\README.md'
Add-MystTiqCheck $ctx 'Documentation' 'the publishing guide covers v1.0.0.4 as a full release' `
    ($publish1004 -match 'git tag -a v1\.0\.0\.4 -m "MystTiq v1\.0\.0\.4"' -and $publish1004 -match 'git push origin v1\.0\.0\.4' -and
     $publish1004 -match 'Leave \*\*Set as a pre-release\*\* unticked') -Severity High
$roadmap1004 = Get-MystTiqText $ctx 'docs\roadmap\PRODUCT_ROADMAP.md'
Add-MystTiqCheck $ctx 'Documentation' 'the roadmap records v1.0.0.4, plans v1.0.0.5 (distinct Bases and Guilds pages) and keeps the open items' `
    ($roadmap1004 -match '## v1\.0\.0\.4 — Restore checked by the world''s day \(' -and $roadmap1004 -match '## v1\.0\.0\.5 — ' -and
     $roadmap1004 -match '## Open after 1\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = if ($FrozenBaselineZip) { $FrozenBaselineZip } else { Join-Path $root '..\_Backups\MystTiqPalworldServer\v1.0.0.3\MystTiqPalworldServer_v1.0.0.3_FullSource.zip' }
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        # v0.8.1.0: reported, not fatal: the rest of the gate is still worth running on a machine without it.
        Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v1.0.0.3 checkpoint logic gate still passes' $false -Skipped -Severity Critical `
            -Details "Baseline archive not found at $frozenZip. Run on the release machine, or pass -FrozenBaselineZip <path>."
    }
    else {
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v1.0.0.3-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v1.0.0.3-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v1.0.0.3-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v1.0.0.3 checkpoint logic gate still passes' `
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
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.21.0 fleet-root smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.21.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v0.8.24.0: processor cores and the policy applied at start, on the real stand-in process in service mode.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.8.24.0 cores smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.8.24.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v0.9.5.0: upgrading from the accepted baseline keeps every setting and backup, a restore is byte for byte, rolling
    # back works, and a fresh setup starts; two servers in one service never start each other.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.9.5.0: the upgrade smoke still passes: v0.8.25.0 settings and data kept by this version, backup verified and restored byte for byte, rollback to v0.8.25.0 reads every value, a fresh setup starts' {
        & (Join-Path $root 'scripts\Test-v0.9.5.0-Upgrade.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.9.5.0: the fleet recovery smoke still passes: restarting or killing one server never starts a second server stopped on purpose, and the first is recovered' {
        & (Join-Path $root 'scripts\Test-v0.9.5.0-FleetRecovery.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v0.9.6.0: two servers in one service, one advertising a port it does not bind: the firewall route and the Doctor
    # report the port each server binds. Only GET routes are called, so the firewall is never changed.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.9.6.0: the firewall route smoke still passes: each server''s firewall state is for the port it binds, read in seconds, and the Doctor flags a -port= that differs from PublicPort' {
        & (Join-Path $root 'scripts\Test-v0.9.6.0-FirewallRoute.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v0.9.8.0: accounts, roles, a changed password and a disabled account from v0.8.25.0 to this version and back.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.9.8.0: the accounts upgrade smoke still passes: every account, role, changed password and disabled account from v0.8.25.0 carries over, and an account made by this version signs in after rolling back' {
        & (Join-Path $root 'scripts\Test-v0.9.8.0-UpgradeAccounts.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v0.9.9.0: the Doctor's port fix end to end, and crash analysis on the real log lines of a session that ended on a join.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.9.9.0: the route smoke still passes: the Doctor''s fix sets PublicPort to the bound port and the finding passes; crash analysis names a session that ended on a player joining and nothing for a clean end' {
        & (Join-Path $root 'scripts\Test-v0.9.9.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v1.0.0.1: the stuck-start protocol end to end with a stand-in that hangs while its "HangsStartup" MOD is on.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v1.0.0.1: the stuck-start smoke still passes: a hanging start says how long it has been starting, the test load starts without MODs and restores them, the one-at-a-time test finds the hanging MOD and leaves the server running; the addresses route and the identity guard answer' {
        & (Join-Path $root 'scripts\Test-v1.0.0.1-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v1.0.0.2: unique player names through the service's routes on isolated data.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v1.0.0.2: the unique-names smoke still passes: on first use the known players own their names (the earliest first, case ignored), a saved list is made consistent, and it survives a restart' {
        & (Join-Path $root 'scripts\Test-v1.0.0.2-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v1.0.0.3: NATIVE MODs through the service's routes, on the owner's layout (loaders renamed by hand).
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v1.0.0.3: the NATIVE MOD smoke still passes: PalDefender and the UE4SS loader are listed, switched on (the newest copy, not the July one) and off (MystTiq''s own copy restored first), included in disable/enable all, and protected from delete and ZIP install' {
        & (Join-Path $root 'scripts\Test-v1.0.0.3-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
    # v1.0.0.4: real backups restored into an isolated server, checked by the world's day after each restore.
    Test-MystTiqCommand $ctx 'v1.0.0.4 Runtime' 'the restore smoke passes: every backup''s day is read and listed, a restore makes the world that backup''s day (shown as current), a held file is waited for or named with nothing changed, a PalServer outside MystTiq blocks it, a newer save is read again, and the activity log records it all' {
        & (Join-Path $root 'scripts\Test-v1.0.0.4-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.1.0: the artwork is Desktop-only, so its runtime proof is the offline headless rendering harness
    # (every page's day/night art, visible bindings, header/ribbon/category layout at 950x650, all 26 icons).
    # v0.8.5.0: the display language is Desktop-only, so its runtime proof is this headless render: German and Spanish
    # applied live, the tabs fitting at 950x650, and back to English.
    Test-MystTiqCommand $ctx 'v1.0.0.4 Runtime' 'MystTiq.ArtworkHarness passes (with the backups'' world days, a ZIP dropped on the MOD drop zone, the Unique player names card, the Launcher page, the addresses line and the stuck-start panel) in all 12 languages on Avalonia 12, with the title bar checked at 950, 1100 and 1440 px, number and date formats per language, names kept inside translated messages and the slow-helper checks (disabled controls name their role, every control named and reachable with Tab, every template round-trips, no untranslated label or message on any page, layouts still fit)' {
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

    # v0.8.26.0: the in-game and alerts scripts, rehearsed against stand-in RCON, REST and webhook servers.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'the in-game and alerts live scripts still pass against stand-in servers (RCON receives exactly the give, getpos and tp; a closed webhook fails)' {
        & (Join-Path $root 'scripts\Test-v0.8.26.0-LiveScriptsRehearsal.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.15.0: the real remote sign-in, against an isolated instance on the Linux test VM (never its installed
    # service). The VM is a separate machine on DHCP: when it does not answer on SSH the check is reported as SKIP.
    $remoteHost = if ($env:MYSTTIQ_LINUX_HOST) { $env:MYSTTIQ_LINUX_HOST } else { '192.168.1.122' }
    $probe = [System.Net.Sockets.TcpClient]::new()
    $reachable = try { $probe.ConnectAsync($remoteHost, 22).Wait(3000) -and $probe.Connected } catch { $false } finally { $probe.Dispose() }
    if ($reachable) {
        # v0.8.21.0: the systemd unit, printed by service-unit and verified by systemd on the VM (installs nothing).
        # v0.8.24.0: cores on every thread of a stand-in on the VM.
        Test-MystTiqCommand $ctx 'Regression New Tests' 'processor cores on Linux still pinned on every thread (VM, isolated)' {
            & (Join-Path $root 'scripts\Test-v0.8.24.0-LinuxIsolated.ps1') -ProjectRoot $root -LinuxHost $remoteHost
        } -Severity Critical | Out-Null
        Test-MystTiqCommand $ctx 'Regression New Tests' 'the Linux service unit (LimitNICE) is still accepted by systemd on the VM' {
            & (Join-Path $root 'scripts\Test-v0.8.21.0-LinuxIsolated.ps1') -ProjectRoot $root -LinuxHost $remoteHost
        } -Severity Critical | Out-Null
        # v0.8.22.0: four roles, the Ribbon gating, and the Desktop running on Windows and on Linux (the VM).
        Test-MystTiqCommand $ctx 'Regression New Tests' 'the Desktop still signs in to a real remote MystTiq as all four roles from Windows and Linux (incl. every page''s role-gated controls)' {
            try { & (Join-Path $root 'scripts\Test-v0.8.22.0-RemoteSignIn.ps1') -ProjectRoot $root -LinuxHost $remoteHost }
            finally {
                Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.RemoteSignInHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
                Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.RemoteSignInHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
            }
        } -Severity Critical | Out-Null
        # v0.8.26.0: the Desktop in the VM's real XFCE session (needs the test user logged in on display :0).
        Test-MystTiqCommand $ctx 'Regression New Tests' 'the Desktop still runs in a real Linux desktop session: X11 window, maximize and restore, clipboard, tray icon (VM)' {
            try { & (Join-Path $root 'scripts\Test-v0.8.26.0-LinuxDesktopSession.ps1') -ProjectRoot $root -LinuxHost $remoteHost }
            finally {
                Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.RemoteSignInHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
                Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.RemoteSignInHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
            }
        } -Severity Critical | Out-Null
    }
    else {
        Add-MystTiqCheck $ctx 'Regression New Tests' 'the Desktop still runs in a real Linux desktop session: X11 window, maximize and restore, clipboard, tray icon (VM)' $false -Skipped -Severity High -Details "The Linux test VM ($remoteHost) does not answer on SSH."
        Add-MystTiqCheck $ctx 'Regression New Tests' 'processor cores on Linux still pinned on every thread (VM, isolated)' $false -Skipped -Severity High -Details "The Linux test VM ($remoteHost) does not answer on SSH."
        Add-MystTiqCheck $ctx 'Regression New Tests' 'the Linux service unit (LimitNICE) is still accepted by systemd on the VM' $false -Skipped -Severity High -Details "The Linux test VM ($remoteHost) does not answer on SSH."
        Add-MystTiqCheck $ctx 'Regression New Tests' 'the Desktop still signs in to a real remote MystTiq as all four roles from Windows and Linux (incl. every page''s role-gated controls)' $false -Skipped -Severity High `
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

    Test-MystTiqCommand $ctx 'Build' 'MystTiq.Desktop builds clean' {
        & dotnet build (Join-Path $root 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -c Release
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
