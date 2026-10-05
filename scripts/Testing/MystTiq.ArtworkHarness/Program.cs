// MystTiq v1.0.0.2: file reviewed for this release (2026-10-05).
using Avalonia.LogicalTree;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Platform;
using MystTiq.Core.Services;
using MystTiq.Desktop;
// v0.9.7.0: Avalonia 12 has its own Avalonia.Controls.NavigationPage; this harness means the app's.
using NavigationPage = MystTiq.Desktop.Models.NavigationPage;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;
using MystTiq.Desktop.ViewModels;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

// v0.9.10.0: this harness started again as a stand-in helper for the slow-helper check below: it answers /healthz like a
// MystTiq helper of this version, after the given delay.
if (args is ["--slow-helper", var helperPortText, var helperDelayText, var helperVersion])
{
    var helperListener = new TcpListener(IPAddress.Loopback, int.Parse(helperPortText));
    helperListener.Start();
    var helperBody = $"{{\"status\":\"ok\",\"component\":\"mysttiq-headless\",\"api\":\"local\",\"apiVersion\":1,\"version\":\"{helperVersion}\",\"authentication\":false,\"tls\":false,\"serverProfileIds\":[\"default\"]}}";
    while (true)
    {
        var helperClient = helperListener.AcceptTcpClient();
        _ = Task.Run(async () =>
        {
            using (helperClient)
            {
                var stream = helperClient.GetStream();
                if (await stream.ReadAsync(new byte[4096]) == 0) return;
                await Task.Delay(int.Parse(helperDelayText));
                await stream.WriteAsync(Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(helperBody)}\r\nConnection: close\r\n\r\n{helperBody}"));
            }
        });
    }
}

// No live API, server discovery, bootstrap or Nexus client is used by this visual harness.
var output = Path.GetFullPath(args.FirstOrDefault() ?? "artwork-render-checks");
Directory.CreateDirectory(output);
AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var profiles = new MemoryProfiles();
var credentials = Isolate(new CredentialStore());
var tabs = Isolate(new TabSessionStore());
var vm = new MainWindowViewModel(OfflineProxy.Create<IMystTiqApiClient>(), profiles,
    OfflineProxy.Create<ILocalInstallationDiscoveryService>(), OfflineProxy.Create<IMystTiqServiceDiscoveryService>(),
    OfflineProxy.Create<ILocalManagementBootstrapper>(), credentialStore: credentials, tabSessionStore: tabs,
    nexusClient: OfflineProxy.Create<INexusModsClient>(), displayPreferencesStore: Isolate(new LocalDisplayPreferencesStore()));
var window = new MainWindow { DataContext = vm };
window.Show();
Dispatcher.UIThread.RunJobs();
var checks = 0;
void Check(bool ok, string description)
{
    if (!ok) throw new Exception(description);
    Console.WriteLine("PASS " + description); checks++;
}
T Isolate<T>(T store) where T : class
{
    typeof(T).GetField("<StoragePath>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(store, Path.Combine(output, typeof(T).Name + ".json"));
    return store;
}
void Render(string name)
{
    Dispatcher.UIThread.RunJobs();
    using var frame = window.CaptureRenderedFrame();
    Check(frame is not null, "Rendered " + name);
    frame!.Save(Path.Combine(output, name + ".png"));
}

// Check every page's actual embedded resource, independent of navigation side effects.
foreach (var page in Enum.GetValues<NavigationPage>())
{
    var dark = ArtworkCatalog.Page(page, false);
    var light = ArtworkCatalog.Page(page, true);
    Check(dark != light && dark.PixelSize.Width == 1600 && light.PixelSize.Width == 1600, page + " has distinct light/dark artwork");
    Check(ReferenceEquals(dark, ArtworkCatalog.Page(page, false)), page + " reuses its decoded artwork");
}
NavigationPage[] pages = [NavigationPage.Dashboard, NavigationPage.Configuration, NavigationPage.Inspector,
    NavigationPage.Backups, NavigationPage.ModDashboard, NavigationPage.DiagnosticsCenter, NavigationPage.Settings];
var selectedPage = typeof(MainWindowViewModel).GetProperty(nameof(vm.SelectedPage))!;
var notified = new HashSet<string>();
vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? "");
foreach (var light in new[] { false, true })
{
    vm.IsLightMode = light;
    foreach (var page in pages)
    {
        selectedPage.SetValue(vm, page);
        Render($"{ArtworkCatalog.Category(page)}-{(light ? "light" : "dark")}");
        Check(window.GetVisualDescendants().OfType<Image>().Any(i => i.IsEffectivelyVisible && ReferenceEquals(i.Source, vm.PageArtwork)), "Visible artwork bound for " + page);
    }
}
Check(notified.Contains(nameof(vm.PageArtwork)) && notified.Contains(nameof(vm.SetupArtwork)), "Theme/navigation notify both artwork bindings");
vm.SelectedProfile = profiles.Load()[0] with { ThemeVariant = "Dark" };
Check(ReferenceEquals(vm.PageArtwork, ArtworkCatalog.Page(vm.SelectedPage, false)), "Profile switch restores dark art");
vm.SelectedProfile = profiles.Load()[0] with { ThemeVariant = "Light" };
Check(ReferenceEquals(vm.PageArtwork, ArtworkCatalog.Page(vm.SelectedPage, true)), "Profile switch restores light art");
window.Width = 950; window.Height = 650;
selectedPage.SetValue(vm, NavigationPage.ModDashboard);
Render("mods-light-950x650");
Check(window.Bounds.Width == 950 && window.Bounds.Height == 650, "Artwork does not inflate minimum window size");
var header = window.FindControl<Border>("PageArtHeader")!;
var categories = window.FindControl<Border>("CategoryBar")!;
var ribbon = window.FindControl<StackPanel>("RibbonHost")!;
var headerTop = header.TranslatePoint(default, window)!.Value;
var categoriesTop = categories.TranslatePoint(default, window)!.Value;
var ribbonTop = ribbon.TranslatePoint(default, window)!.Value;
Check(headerTop.Y >= categoriesTop.Y + categories.Bounds.Height, "Header cannot cover the category navigation");
Check(ribbonTop.X + ribbon.Bounds.Width <= headerTop.X, "Header cannot cover Ribbon commands");
Check(vm.HasOverflowRibbonGroups, "Narrow-window commands remain reachable through Ribbon overflow");
// v0.9.8.0: the "+" (new tab) stays inside the window and the tab strip at every width (at the minimum width it was pushed
// off the right edge, reported by the user), and a tab that no longer fits goes to the "»" menu.
foreach (var titleWidth in new[] { 950.0, 1100.0, 1440.0 })
{
    window.Width = titleWidth;
    Render($"titlebar-{titleWidth:0}");
    var addTab = window.FindControl<Button>("AddTabButton")!;
    var strip = window.FindControl<Grid>("TabStripHost")!;
    var addAt = addTab.TranslatePoint(default, window)!.Value;
    var stripAt = strip.TranslatePoint(default, window)!.Value;
    Check(addTab.IsEffectivelyVisible && addAt.X >= stripAt.X && addAt.X + addTab.Bounds.Width <= stripAt.X + strip.Bounds.Width + 0.5 && addAt.X + addTab.Bounds.Width <= titleWidth,
        $"at {titleWidth:0} px wide the \"+\" is inside the tab strip and the window (at {addAt.X:0}+{addTab.Bounds.Width:0}, strip {stripAt.X:0}..{stripAt.X + strip.Bounds.Width:0})");
    Check(vm.ActiveTab is not null && vm.VisibleTabs.Contains(vm.ActiveTab) && vm.VisibleTabs.Count + vm.OverflowTabs.Count == vm.Tabs.Count && (vm.OverflowTabs.Count == 0 || vm.HasOverflowTabs),
        $"at {titleWidth:0} px wide the active tab is shown and every tab is either shown or in the \"»\" menu ({vm.VisibleTabs.Count} shown, {vm.OverflowTabs.Count} in the menu)");
}
Check(!vm.ShowBrandSubtitle || window.Width >= 1200, "the title bar drops its subtitle below 1200 px");
window.Width = 950;
vm.SelectedProfile = null;
Render("setup-wizard-light");
Check(window.GetVisualDescendants().OfType<Image>().Any(i => i.IsEffectivelyVisible && ReferenceEquals(i.Source, vm.SetupArtwork)), "Setup wizard uses themed Server artwork");
Check(vm.IsLightMode, "Wizard retains light theme without a profile");
ThemeApplier.Apply("Default", "Dark");
vm.SelectedProfile = profiles.Load()[0] with { ThemeVariant = "Dark" };
selectedPage.SetValue(vm, NavigationPage.ModDashboard);
Render("mods-dark-950x650");
vm.SelectedProfile = null;
Render("setup-wizard-dark");
Check(!vm.IsLightMode && ReferenceEquals(vm.SetupArtwork, ArtworkCatalog.Page(NavigationPage.ServerSetup, false)), "Wizard retains dark theme without a profile");

var iconUris = AssetLoader.GetAssets(new Uri("avares://MystTiq.Desktop/Assets/Icons/"), null).OrderBy(u => u.AbsolutePath).ToArray();
// v1.0.0.1: Launcher now has its own navigation illustration instead of reusing Workspace.
Check(iconUris.Length == 28, "All 28 navigation illustrations are embedded");
Check(ArtworkCatalog.Category(NavigationPage.Host) == "host" && iconUris.Any(u => u.AbsolutePath.EndsWith("/icon-host.png")) && !ReferenceEquals(ArtworkCatalog.Page(NavigationPage.Host, false), ArtworkCatalog.Page(NavigationPage.Settings, false)),
    "the HOST tab has its own art and navigation icon (no longer the System art and diagnostics icon)");
Check(iconUris.Any(u => u.AbsolutePath.EndsWith("/icon-launcher.png")),
    "the Launcher page has its own navigation icon instead of reusing Workspace");
foreach (var light in new[] { false, true })
{
    var panel = new WrapPanel { Width = 1040 };
    foreach (var uri in iconUris)
    {
        var name = Path.GetFileNameWithoutExtension(uri.AbsolutePath)[5..];
        var bitmap = ArtworkCatalog.Icon(name);
        Check(bitmap.PixelSize.Width == 112, name + " uses bounded decode size");
        panel.Children.Add(new StackPanel
        {
            Width = 130, Height = 128, Spacing = 5,
            Children =
            {
                new Image { Source = bitmap, Width = 56, Height = 56 },
                new Image { Source = bitmap, Width = 32, Height = 32 },
                new TextBlock { Text = name.Replace('_', ' '), FontSize = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Foreground = light ? Brushes.Black : Brushes.White }
            }
        });
    }
    var gallery = new Window { Width = 1040, Height = 512, Content = panel, Background = new SolidColorBrush(Color.Parse(light ? "#F2F6FA" : "#0A1624")), WindowDecorations = WindowDecorations.None };
    gallery.Show(); Dispatcher.UIThread.RunJobs();
    using var frame = gallery.CaptureRenderedFrame();
    frame!.Save(Path.Combine(output, $"icons-{(light ? "light" : "dark")}.png"));
    gallery.Close();
}
// v0.8.1.0: the user's vector Ribbon icons.
foreach (var key in RibbonIcons.Keys)
{
    var geometry = RibbonIcons.Geometry(key);
    Check(geometry is not null && ReferenceEquals(geometry, RibbonIcons.Geometry(key)), key + " ribbon icon parses once and is reused");
    var b = geometry!.Bounds;
    Check(b.Width > 4 && b.Height > 4 && b.X >= 0 && b.Y >= 0 && b.Right <= 24.01 && b.Bottom <= 24.01, key + " ribbon icon stays on its 24x24 grid");
}
Check(RibbonIcons.Keys.Count == 10, "All 10 supplied ribbon icons are present");
foreach (var (label, glyph, expected) in new (string, string, string?)[]
{
    ("Refresh", "↻", "refresh"), ("Start", "▶", "start"), ("Restart", "⟳", "restart"), ("Stop", "■", "stop"),
    ("Backup", "⇩", "backup"), ("Console", "▰", "console"), ("Doctor", "✚", "doctor"), ("Verify Files", "✓", "verify-files"),
    ("Install Missing", "⬇", "install-missing"), ("Force Stop", "⛔", "force-stop"),
    ("Refresh Map", "↻", "refresh"), ("Restart Server", "⟳", "restart"),
    ("Export", "⇩", null), ("Validate", "✓", null), ("Kill Processes", "⛔", null)
})
    Check(RibbonIcons.KeyFor(label, glyph) == expected, $"Ribbon '{label}' uses {(expected ?? "its text glyph")}");

window.Width = 1440; window.Height = 880;
foreach (var light in new[] { false, true })
{
    ThemeApplier.Apply("Default", light ? "Light" : "Dark");
    vm.SelectedProfile = profiles.Load()[0] with { ThemeVariant = light ? "Light" : "Dark" };
    selectedPage.SetValue(vm, NavigationPage.Dashboard);
    Render($"ribbon-{(light ? "light" : "dark")}");
    var ribbonHost = window.FindControl<StackPanel>("RibbonHost")!;
    var vectorIcons = ribbonHost.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
        .Where(p => p.Classes.Contains("ribbonIcon") && p.IsEffectivelyVisible).ToArray();
    var visibleGlyphs = ribbonHost.GetVisualDescendants().OfType<TextBlock>()
        .Where(t => t.Classes.Contains("flatIcon") && t.IsEffectivelyVisible).Select(t => t.Text).ToArray();
    // v0.8.10.0: Backup and Doctor now show images, so 6 of the 8 Dashboard actions remain vectors.
    var imageIcons = ribbonHost.GetVisualDescendants().OfType<Image>().Count(i => i.Classes.Contains("ribbonImage") && i.IsEffectivelyVisible && i.Source is not null);
    // v0.8.13.0: with the last ten images every Dashboard action is an image; no vector or glyph is left.
    Check(vectorIcons.Length == 0 && imageIcons >= 8,
        $"Dashboard Ribbon draws all {imageIcons} actions as images, {vectorIcons.Length} as vectors ({(light ? "light" : "dark")})");
    Check(!visibleGlyphs.Any(g => g is "↻" or "▶" or "⟳" or "■" or "⇩" or "▰" or "✚" or "⛔"),
        $"No replaced text glyph is still visible on the Dashboard Ribbon ({(light ? "light" : "dark")}): [{string.Join(" ", visibleGlyphs)}]");
}

// v0.8.5.0: display language. The VM's language store is redirected into the output folder, so choosing a language
// here never touches the real Desktop preference.
Isolate((LocalLanguagePreferenceStore)typeof(MainWindowViewModel).GetField("_languageStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!);
Localizer.Instance.SetLanguage("en");
string ReadLanguage(string code) { using var s = AssetLoader.Open(new Uri($"avares://MystTiq.Desktop/Assets/i18n/{code}.json")); return new StreamReader(s).ReadToEnd(); }
var english = Localizer.Parse(ReadLanguage("en"));
Check(english.Count > 80 && english.Values.All(v => !string.IsNullOrWhiteSpace(v)), $"English holds all {english.Count} strings, none blank");
foreach (var page in Enum.GetValues<NavigationPage>())
    Check(english.ContainsKey($"page.{page}.title") && english.ContainsKey($"page.{page}.subtitle"), $"{page} has an English title and subtitle");
foreach (var language in Localizer.Languages.Where(l => l.Code != "en"))
{
    var translated = Localizer.Parse(ReadLanguage(language.Code));
    var missing = english.Keys.Except(translated.Keys).ToArray();
    var extra = translated.Keys.Except(english.Keys).ToArray();
    var blank = translated.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToArray();
    Check(missing.Length == 0 && extra.Length == 0 && blank.Length == 0,
        $"{language.NativeName} translates exactly English's {english.Count} keys (missing: {string.Join(",", missing)}; extra: {string.Join(",", extra)}; blank: {string.Join(",", blank)})");
}
Check(Localizer.Lookup(new Dictionary<string, string>(), english, "page.Dashboard.title") == "Dashboard" &&
      Localizer.Lookup(new Dictionary<string, string> { ["page.Dashboard.title"] = "" }, english, "page.Dashboard.title") == "Dashboard" &&
      Localizer.Lookup(new Dictionary<string, string>(), new Dictionary<string, string>(), "no.such.key") == "no.such.key",
    "A missing or blank translation falls back to English, never to a blank");

string[] VisibleTexts() => window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text is not null).Select(t => t.Text!).ToArray();
// The Ribbon section above left the tab connected to the preview profile, which is what these renders need.
// v0.9.0.0: every language, with its own texts (the pinned German and Spanish words are still checked below).
Check(Localizer.Parse(ReadLanguage("de"))["category.tools"] == "Werkzeuge" && Localizer.Parse(ReadLanguage("es"))["nav.Dashboard"] == "Panel",
    "de and es keep their reviewed words (Werkzeuge, Panel)");
foreach (var (code, tools, dashboard, alertCenter) in Localizer.Languages.Where(l => l.Code != "en")
    .Select(l => Localizer.Parse(ReadLanguage(l.Code))).Zip(Localizer.Languages.Where(l => l.Code != "en"))
    .Select(p => (p.Second.Code, p.First["category.tools"], p.First["nav.Dashboard"], p.First["nav.AlertCenter"])))
{
    window.Width = 1440; window.Height = 880;
    Localizer.Instance.SetLanguage(code);
    selectedPage.SetValue(vm, NavigationPage.Dashboard);
    Render($"language-{code}-dashboard");
    var texts = VisibleTexts();
    Check(texts.Contains(tools) && texts.Contains(dashboard), $"{code}: the category tabs and navigation switch at once ({tools}, {dashboard})");
    Check(vm.PageTitle == dashboard && texts.Contains(dashboard), $"{code}: the page header follows the language");
    selectedPage.SetValue(vm, NavigationPage.AlertCenter);
    Dispatcher.UIThread.RunJobs();
    Check(VisibleTexts().Contains(alertCenter) && vm.PageSubtitle == Localizer.Instance["page.AlertCenter.subtitle"] && vm.PageSubtitle != english["page.AlertCenter.subtitle"],
        $"{code}: another page's navigation, title and subtitle are translated too");

    // The longest translated tab labels must still fit the category bar at the minimum window size.
    window.Width = 950; window.Height = 650;
    selectedPage.SetValue(vm, NavigationPage.Dashboard);
    Render($"language-{code}-950x650");
    var bar = window.FindControl<Border>("CategoryBar")!;
    var barLeft = bar.TranslatePoint(default, window)!.Value.X;
    var categoryTabs = bar.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Where(t => t.Classes.Contains("categoryTab")).ToArray();
    // v0.8.17.0: eight with the HOST tab.
    Check(categoryTabs.Length == 8 && categoryTabs.All(t => t.TranslatePoint(new Point(t.Bounds.Width, 0), window)!.Value.X <= barLeft + bar.Bounds.Width + 0.5),
        $"{code}: all 8 category tabs fit the category bar at 950x650");
    var headerNow = window.FindControl<Border>("PageArtHeader")!;
    Check(headerNow.TranslatePoint(default, window)!.Value.Y >= bar.TranslatePoint(default, window)!.Value.Y + bar.Bounds.Height,
        $"{code}: the header still cannot cover the category tabs");
}
window.Width = 1440; window.Height = 880;
Localizer.Instance.SetLanguage("en");
selectedPage.SetValue(vm, NavigationPage.Dashboard);
Dispatcher.UIThread.RunJobs();
Check(VisibleTexts().Contains("Tools") && vm.PageTitle == "Dashboard" && !VisibleTexts().Contains("Werkzeuge"), "Switching back to English restores every translated text");
vm.SelectedUiLanguage = Localizer.Languages.Single(l => l.Code == "de");
var savedLanguage = File.ReadAllText(Path.Combine(output, nameof(LocalLanguagePreferenceStore) + ".json"));
Check(Localizer.Instance.LanguageCode == "de" && savedLanguage.Contains("\"de\""), "Choosing a language in Settings applies it and remembers it");
vm.SelectedUiLanguage = Localizer.Languages[0];
Check(Localizer.Instance.LanguageCode == "en", "Choosing English again switches back");

// v0.9.0.0: the title bar has the language picker (right of Settings) and a bell for Notifications; both pickers are
// the same setting.
var titlePicker = window.FindControl<ComboBox>("TitleLanguagePicker")!;
var bellButton = window.GetVisualDescendants().OfType<Button>().First(b => b.CommandParameter as string == "Notifications" && b.Classes.Contains("ghost"));
Check(titlePicker is not null && titlePicker.IsEffectivelyVisible && titlePicker.ItemCount == 12 && bellButton.Content is PathIcon,
    $"The title bar has the language picker with all 12 languages ({titlePicker?.ItemCount}) and a bell for Notifications");
titlePicker!.SelectedItem = Localizer.Languages.Single(l => l.Code == "ja");
Dispatcher.UIThread.RunJobs();
Check(Localizer.Instance.LanguageCode == "ja" && vm.SelectedUiLanguage.Code == "ja" && VisibleTexts().Contains(Localizer.Parse(ReadLanguage("ja"))["category.tools"]),
    "Choosing 日本語 in the title bar switches the whole window and the Settings choice follows");
titlePicker.SelectedItem = Localizer.Languages[0];
Dispatcher.UIThread.RunJobs();
Check(Localizer.Instance.LanguageCode == "en", "Choosing English in the title bar switches back");
// v0.9.9.0: numbers, dates and times follow the chosen language (they followed the operating system's before); how text is
// compared and cased does not change with it.
{
    var system = System.Globalization.CultureInfo.GetCultureInfo("en-US");
    var german = DisplayCulture.Build("de", system);
    Check((1234.5).ToString("N1", german) == "1.234,5" && new DateTime(2026, 9, 29, 14, 5, 0).ToString("t", german) == "14:05",
        $"German formats: 1.234,5 and 14:05 (got {(1234.5).ToString("N1", german)} and {new DateTime(2026, 9, 29, 14, 5, 0).ToString("t", german)})");
    var turkish = DisplayCulture.Build("tr", system);
    Check((0.5).ToString("F1", turkish) == "0,5" && "FILE".ToLower(turkish) == "file" && "file".ToUpper(turkish) == "FILE",
        "Turkish decimals use a comma, while upper- and lower-casing keep the system's rules (no dotless i in names and ids)");
    Check(ReferenceEquals(DisplayCulture.Build("en", system), system) && ReferenceEquals(DisplayCulture.Build("xx", system), system), "English, and an unknown code, keep the system's own formats");
    var before = System.Globalization.CultureInfo.CurrentCulture;
    Localizer.Instance.SetLanguage("fr");
    var french = $"{1.5:F1}";
    Localizer.Instance.SetLanguage("en");
    Dispatcher.UIThread.RunJobs();
    Check(french == "1,5" && $"{1.5:F1}" == (1.5).ToString("F1", before), $"choosing Français formats 1.5 as 1,5, and English puts the system's format back (got {french}, then {1.5:F1})");
}

// v0.9.1.0: status and error messages built in code are shown in the chosen language. The view models keep English;
// MessageCatalog translates on the way to the screen ({services:TrText} in XAML, Localizer.T in dialogs).
string MsgKey(string en) => english.First(kv => kv.Key.StartsWith("msg.", StringComparison.Ordinal) && kv.Value == en).Key;
Check(english.Count(kv => kv.Key.StartsWith("msg.", StringComparison.Ordinal)) >= 900, "The code's status and error messages are in the catalog (msg.* keys)");
Localizer.Instance.SetLanguage("en");
Check(Localizer.T("Stopped / Not ready") == "Stopped / Not ready" && Localizer.Instance.Messages.TemplateCount == 0,
    "English: messages are shown exactly as the code builds them");
var jaText = Localizer.Parse(ReadLanguage("ja"));
Localizer.Instance.SetLanguage("ja");
Check(Localizer.T("Stopped / Not ready") == jaText[MsgKey("Stopped / Not ready")] && Localizer.T("Stopped / Not ready") != "Stopped / Not ready",
    "Japanese: a status message is shown in Japanese");
Check(Localizer.T("Status: Stopped / Not ready") == jaText[MsgKey("Status: {0}")].Replace("{0}", jaText[MsgKey("Stopped / Not ready")]),
    "A message with a value is matched as a template, and the value is translated too when it is itself a message");
Check(Localizer.T("Sending Wood:1 for Bob…") == jaText[MsgKey("Sending {0} for {1}…")].Replace("{0}", "Wood:1").Replace("{1}", "Bob") &&
      jaText[MsgKey("Sending {0} for {1}…")].IndexOf("{1}", StringComparison.Ordinal) < jaText[MsgKey("Sending {0} for {1}…")].IndexOf("{0}", StringComparison.Ordinal),
    "A translation may put the values in another order");
Check(Localizer.T("The history could not be read: disk full") == jaText[MsgKey("The history could not be read: ")] + "disk full",
    "A message that begins a longer text (\"…could not be read: \" + the error) keeps the rest");
Check(Localizer.T("Stopped / Not ready\nStopped intentionally or awaiting start.") == Localizer.T("Stopped / Not ready") + "\n" + Localizer.T("Stopped intentionally or awaiting start.") &&
      Localizer.T("Stopped intentionally or awaiting start.") != "Stopped intentionally or awaiting start.",
    "A multi-line status is translated line by line");
// v0.9.2.0: a text composed of sentences is translated sentence by sentence (the map's summary line, Pal tooltips).
Check(Localizer.T(MapContentsText.Describe(3, 3, 2)) == jaText[MsgKey("{0} player(s) online on the map.")].Replace("{0}", "3") + " " + jaText[MsgKey("{0} base(s) shown from the world save.")].Replace("{0}", "2"),
    "A summary built from several sentences is translated sentence by sentence");
var palTip = Localizer.T("Moco, level 12, working at Crystal's base (248, -495). Last position the save recorded. Click to zoom in.");
Check(palTip.StartsWith(jaText[MsgKey("{0}, level {1}, working at {2}'s base {3}.")].Replace("{0}", "Moco").Replace("{1}", "12").Replace("{2}", "Crystal").Replace("{3}", "(248, -495)"), StringComparison.Ordinal) &&
      !palTip.Contains("level", StringComparison.Ordinal) && !palTip.Contains("Click", StringComparison.Ordinal),
    $"A Pal tooltip (name, level, place, coordinates, then notes) is fully translated, names and coordinates kept [{palTip}]");
Check(Localizer.T("Moco, alpha, level 12, in the world (1, 2).").Contains(jaText[MsgKey("{0}, alpha")].Replace("{0}", "Moco"), StringComparison.Ordinal),
    "An alpha Pal's name is marked in the language too");
Check(Localizer.T("Up 3 d 4 h") == jaText[MsgKey("Up {0} d {1} h")].Replace("{0}", "3").Replace("{1}", "4"), "The host's uptime is translated");
Check(Localizer.T("Version 1.2. Build 5") == "Version 1.2. Build 5", "Sentences that are no known message stay as they are");
Check(Localizer.T("Frostbound Frontier") == "Frostbound Frontier" && Localizer.T("") == "" && Localizer.T(null) == "",
    "Text that is no known message (a server name) is shown unchanged");
// v0.9.10.0 (external review): a name that is also a known message ("Ready", "None", "Backup") stays a name inside a
// message: quoted, or right after "server", "player", "guild" and the like. Other values are still translated.
foreach (var name in new[] { "Ready", "None", "Backup" })
{
    Check(Localizer.T($"Restarted. '{name}' is now online.") == jaText[MsgKey("Restarted. '{0}' is now online.")].Replace("{0}", name) &&
          Localizer.T($"This computer's MystTiq helper has no server '{name}'.") == jaText[MsgKey("This computer's MystTiq helper has no server '{0}'.")].Replace("{0}", name) &&
          Localizer.T($"Player {name} failed") == jaText[MsgKey("Player {0} failed")].Replace("{0}", name),
        $"A server or player named \"{name}\" keeps its name inside a translated message");
}
Check(Localizer.T("Ready") != "Ready" && Localizer.T("Status: Stopped / Not ready") != "Status: Stopped / Not ready",
    "A status value on its own, or in a status slot, is still translated");
Check(!string.IsNullOrEmpty(vm.ServerState) && !vm.ServerState.Any(c => c >= 0x2E80),
    "The view model itself keeps English while Japanese is shown (logic compares English)");
// v0.9.3.0: the service's own messages (sent in English over the API) are in the catalog too; commands it reports are not.
Check(english.Count(kv => kv.Key.StartsWith("msg.", StringComparison.Ordinal)) >= 2400 &&
      Localizer.T("PalServer is not running.") == jaText[MsgKey("PalServer is not running.")] && Localizer.T("PalServer is not running.") != "PalServer is not running.",
    "Japanese: a message from the service is shown in Japanese");
const string wrongPath = @"A PalServer process is running, but not at the configured path. Expected: C:\GameServers\Palworld\Server. Found: PalServer-Win64-Shipping-Cmd (PID 24072) at C:\GameServers\Palworld\Server-clone\PalServer.exe.";
Check(Localizer.T(wrongPath) == jaText[MsgKey("A PalServer process is running, but not at the configured path. Expected: {0}. Found: {1}.")]
          .Replace("{0}", @"C:\GameServers\Palworld\Server").Replace("{1}", @"PalServer-Win64-Shipping-Cmd (PID 24072) at C:\GameServers\Palworld\Server-clone\PalServer.exe"),
    $"A service message with a value keeps the value (paths and process ids) [{Localizer.T(wrongPath)}]");
Check(Localizer.T("TeleportToMe 76561198000000000") == "TeleportToMe 76561198000000000" && Localizer.T("KickPlayer steam_1") == "KickPlayer steam_1",
    "A server command the service reports stays as it is");
// v0.9.4.0: the health states the service and the Dashboard use, and a value inside a translated label (TrFormat).
Check(new[] { "STOPPED", "DEGRADED", "STARTING", "READY", "ATTENTION" }.All(w => Localizer.T(w) != w) &&
      TrFormatExtension.Fill(Localizer.Instance["dashboard.format.health"], "Healthy") == TrFormatExtension.Fill(Localizer.Instance["dashboard.format.health"], jaText[MsgKey("Healthy")]) &&
      !TrFormatExtension.Fill(Localizer.Instance["dashboard.format.health"], "Healthy").Contains("Healthy", StringComparison.Ordinal),
    $"Japanese: health states and a value inside a label are translated [{Localizer.T("DEGRADED")} / {TrFormatExtension.Fill(Localizer.Instance["dashboard.format.health"], "Healthy")}]");
// Every template, in every language: English filled with sample values comes back as that language's text with the
// same values, so no template is shadowed by a wrong one.
foreach (var language in Localizer.Languages.Where(l => l.Code != "en"))
{
    var translated = Localizer.Parse(ReadLanguage(language.Code));
    Localizer.Instance.SetLanguage(language.Code);
    var wrong = new List<string>();
    foreach (var (key, en) in english.Where(kv => kv.Key.StartsWith("msg.", StringComparison.Ordinal) && System.Text.RegularExpressions.Regex.IsMatch(kv.Value, @"\{\d+\}")))
    {
        string Fill(string template) => System.Text.RegularExpressions.Regex.Replace(template, @"\{(\d+)\}", m => $"«v{m.Groups[1].Value}»");
        // v0.9.3.0: a message that begins a longer text ("… already reported earlier. " + more) is tried with a rest.
        var rest = en.Length > en.TrimEnd().Length ? "«rest»" : "";
        var expected = Fill(translated[key]) + rest;
        var actual = Localizer.T(Fill(en) + rest);
        if (actual != expected && actual != Fill(en) + rest) wrong.Add($"{key}: {actual}");
        else if (actual == Fill(en) + rest && translated[key] != en && en.Count(char.IsLetter) >= 4) wrong.Add($"{key}: (English)");
    }
    Check(wrong.Count == 0, $"{language.NativeName}: every message template round-trips [{string.Join(" | ", wrong.Take(4))}]");
}
Localizer.Instance.SetLanguage("en");
Dispatcher.UIThread.RunJobs();

// v0.9.0.0: no page shows a hard-coded English text in another language. Every page is visited in every language and
// each visible text is compared with the English ui.* texts (translatable XAML texts) whose translation differs.
// v0.9.1.0: and with the code's messages (msg.* without values), which are now translated when shown.
window.Width = 1440; window.Height = 880;
foreach (var language in Localizer.Languages.Where(l => l.Code != "en"))
{
    var translated = Localizer.Parse(ReadLanguage(language.Code));
    var englishOnly = english.Where(kv => (kv.Key.StartsWith("ui.") || (kv.Key.StartsWith("msg.") && !kv.Value.Contains('{'))) && translated[kv.Key].Trim() != kv.Value.Trim())
        .Select(kv => kv.Value.Trim()).ToHashSet();
    Localizer.Instance.SetLanguage(language.Code);
    var leftovers = new List<string>();
    foreach (var page in Enum.GetValues<NavigationPage>())
    {
        selectedPage.SetValue(vm, page);
        Dispatcher.UIThread.RunJobs();
        // v0.9.1.0: each leftover names what shows it (the nearest named or typed parent), to find it quickly.
        leftovers.AddRange(window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text is not null && englishOnly.Contains(t.Text))
            .Select(t => $"{page}: {t.Text} [{string.Join("/", t.GetVisualAncestors().Take(3).Select(a => a is Control { Name: { Length: > 0 } n } ? n : a.GetType().Name))}]"));
        // A button's own text is drawn by a TextBlock inside it (checked above); its Content keeps the English it was given.
        leftovers.AddRange(window.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Content is string s && englishOnly.Contains(Localizer.T(s))).Select(b => $"{page}: {b.Content} [Button]"));
    }
    Check(leftovers.Count == 0, $"{language.NativeName}: no page shows an untranslated English label or button [{string.Join(" | ", leftovers.Distinct().Take(6))}]");
    if (language.Code is "zh-Hans" or "ja" or "ko" or "ru")
    {
        selectedPage.SetValue(vm, NavigationPage.Dashboard);
        Render($"language-{language.Code}-dashboard-full");
    }
}
Localizer.Instance.SetLanguage("en");
Dispatcher.UIThread.RunJobs();
Check(Application.Current!.Resources["UiFontFamily"] is Avalonia.Media.FontFamily enFont && string.Join(", ", enFont.FamilyNames) == "Inter, Segoe UI",
    "English is drawn with Inter, as before");
Localizer.Instance.SetLanguage("ja");
Check(Application.Current!.Resources["UiFontFamily"] is Avalonia.Media.FontFamily jaFont && jaFont.FamilyNames.Contains("Yu Gothic UI") && !jaFont.FamilyNames.Any(f => f.Contains("YaHei")),
    "Japanese is drawn with a Japanese font (Yu Gothic / Meiryo / Noto CJK JP), not a Chinese one");
Localizer.Instance.SetLanguage("en");
Dispatcher.UIThread.RunJobs();

// v0.8.6.0: the Ribbon in every language. Its groups are page-specific, so every page is visited.
// v0.9.0.0: all 12 languages.
foreach (var code in Localizer.Languages.Select(l => l.Code))
{
    Localizer.Instance.SetLanguage(code);
    var raw = new List<string>();
    var overlaps = new List<string>();
    foreach (var size in new[] { (950, 650), (1440, 880) })
    {
        window.Width = size.Item1; window.Height = size.Item2;
        foreach (var page in Enum.GetValues<NavigationPage>())
        {
            selectedPage.SetValue(vm, page);
            Dispatcher.UIThread.RunJobs();
            foreach (var group in vm.VisibleRibbonGroups.Concat(vm.OverflowRibbonGroups))
            {
                if (group.DisplayTitle.StartsWith("ribbon.")) raw.Add(group.DisplayTitle);
                raw.AddRange(group.Actions.Where(a => a.DisplayLabel.StartsWith("ribbon.")).Select(a => a.DisplayLabel));
                if (code == "en" && (group.DisplayTitle != group.Title || group.Actions.Any(a => a.DisplayLabel != a.Label)))
                    raw.Add($"English changed in {group.Title}");
            }
            var host = window.FindControl<StackPanel>("RibbonHost")!;
            var pageHeader = window.FindControl<Border>("PageArtHeader")!;
            // Every shown button must end before the header starts; a button pushed past it would be cut off.
            var headerLeft = pageHeader.TranslatePoint(default, window)!.Value.X;
            var cut = host.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("ribbon") && b.IsEffectivelyVisible)
                .Where(b => b.TranslatePoint(new Point(b.Bounds.Width, 0), window)!.Value.X > headerLeft + 0.5).ToArray();
            if (cut.Length > 0) overlaps.Add($"{page}@{size.Item1}");
        }
    }
    Check(raw.Count == 0, $"{code}: every page's Ribbon groups and buttons show real text{(code == "en" ? ", identical to before" : "")} [{string.Join(", ", raw.Distinct())}]");
    Check(overlaps.Count == 0, $"{code}: on every page, at 950x650 and 1440x880, every shown Ribbon button ends before the page header (none cut off) [{string.Join(", ", overlaps)}]");
}
Localizer.Instance.SetLanguage("de");
window.Width = 1440; window.Height = 880;
selectedPage.SetValue(vm, NavigationPage.Dashboard);
Render("language-de-ribbon");
var deHost = window.FindControl<StackPanel>("RibbonHost")!;
var deTexts = deHost.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToArray();
var deIcons = deHost.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Count(p => p.Classes.Contains("ribbonIcon") && p.IsEffectivelyVisible);
Check(deTexts.Contains("Aktualisieren") && deTexts.Contains("Starten") && deTexts.Contains("Serversteuerung"), "de: the Dashboard Ribbon's buttons and group titles are German");
// v0.8.10.0: 6 vectors plus the Backup and Doctor images.
var deImages = deHost.GetVisualDescendants().OfType<Image>().Count(i => i.Classes.Contains("ribbonImage") && i.IsEffectivelyVisible && i.Source is not null);
Check(deIcons == 0 && deImages >= 8, $"de: the image icons ({deImages}) still appear, since they key off the English label (vectors: {deIcons})");
Localizer.Instance.SetLanguage("en");
Dispatcher.UIThread.RunJobs();

// v0.8.7.0: the Dashboard's labels, buttons and number templates.
Check(TrFormatExtension.Fill("Stored: {0}", "36 MB") == "Stored: 36 MB" && TrFormatExtension.Fill("{1} broken", "x") == "x" &&
      TrFormatExtension.Fill(null, "x") == "x" && TrFormatExtension.Fill("{0} online", null) == " online",
    "Templates are filled with the value, and a broken or missing template shows the value alone");
foreach (var language in Localizer.Languages.Where(l => l.Code != "en"))
{
    var translated = Localizer.Parse(ReadLanguage(language.Code));
    var lost = english.Where(kv => kv.Key.Contains(".format.") && kv.Value.Contains("{0}") && !translated[kv.Key].Contains("{0}")).Select(kv => kv.Key).ToArray();
    Check(lost.Length == 0, $"{language.NativeName} keeps the {{0}} placeholder in every template [{string.Join(", ", lost)}]");
}
string[] DashboardTexts() => window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text is not null).Select(t => t.Text!).ToArray();
window.Width = 1440; window.Height = 880;
Localizer.Instance.SetLanguage("en");
selectedPage.SetValue(vm, NavigationPage.Dashboard);
Dispatcher.UIThread.RunJobs();
var enDash = DashboardTexts();
Check(new[] { "ACTIVE WORLD", "OVERALL HEALTH", "WORLD PULSE", "RESOURCE HISTORY", "LIVE SERVER / MANAGER LOG", "CURRENT OPERATION" }.All(enDash.Contains) &&
      enDash.Contains($"Stored: {vm.BackupTotalSizeText}") && enDash.Contains($"{vm.OnlinePlayerCountText} online") && enDash.Contains($"{vm.PlayerRecordCountText} known player(s)"),
    "en: the Dashboard reads exactly as before, templates included");
Localizer.Instance.SetLanguage("de");
Render("language-de-dashboard-labels");
var deDash = DashboardTexts();
Check(new[] { "AKTIVE WELT", "GESAMTZUSTAND", "WELT-PULS", "RESSOURCENVERLAUF", "AKTUELLER VORGANG" }.All(deDash.Contains) &&
      deDash.Contains($"Größe: {vm.BackupTotalSizeText}") && deDash.Contains($"{vm.PlayerRecordCountText} bekannte(r) Spieler") && !deDash.Contains("ACTIVE WORLD"),
    "de: the Dashboard's labels and filled templates switch live");
var openButtons = window.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Content as string == "ÖFFNEN").Count();
Check(openButtons >= 5, $"de: the Dashboard's OPEN buttons read ÖFFNEN ({openButtons})");
Localizer.Instance.SetLanguage("en");
Dispatcher.UIThread.RunJobs();
Check(DashboardTexts().Contains("ACTIVE WORLD") && DashboardTexts().Contains($"Stored: {vm.BackupTotalSizeText}"), "Back to English, the Dashboard's labels and templates follow");
// At the minimum window size no Dashboard label may be split mid-word or cut off, in any language. Labels are found by
// matching the visible text against that language's dashboard.* strings.
window.Width = 950; window.Height = 650;
foreach (var code in Localizer.Languages.Select(l => l.Code))
{
    Localizer.Instance.SetLanguage(code);
    selectedPage.SetValue(vm, NavigationPage.Dashboard);
    Render($"dashboard-{code}-950x650");
    var labelTexts = Localizer.Parse(ReadLanguage(code)).Where(kv => kv.Key.StartsWith("dashboard.") && !kv.Key.Contains(".tip")).Select(kv => kv.Value).ToHashSet();
    var bad = window.GetVisualDescendants().OfType<TextBlock>()
        .Where(t => t.IsEffectivelyVisible && t.Text is { Length: > 0 } && labelTexts.Contains(t.Text))
        .Where(t => (!t.Text!.Contains(' ') && t.TextLayout.TextLines.Count > 1) || t.DesiredSize.Width > t.Bounds.Width + 0.5)
        .Select(t => t.Text).Distinct().ToArray();
    Check(bad.Length == 0, $"{code}: at 950x650 no Dashboard label is split mid-word or cut off [{string.Join(", ", bad)}]");
}
Localizer.Instance.SetLanguage("en");
window.Width = 1440; window.Height = 880;
Dispatcher.UIThread.RunJobs();

// v0.8.8.0: the user's image icon batches 1-3, colour image tiles matched by exact label.
// v0.8.10.0: the files are the full framed originals; Backup and Doctor share the Create and Run Doctor images.
// v0.8.13.0: plus the last ten (palworld_missing_10_icons.zip): 40 images over all 50 Ribbon labels.
Check(RibbonIcons.ImageKeys.Count == 40 && RibbonIcons.ImageKeys.Distinct().Count() == 40 && RibbonIcons.ImageLabels.Count == 50,
    "All 40 image icons are mapped over the 50 Ribbon labels (shared: Backup, Doctor, the Refresh variants, Restart Server)");
foreach (var key in RibbonIcons.ImageKeys)
{
    var bitmap = RibbonIcons.Image(key);
    Check(bitmap is not null && bitmap.PixelSize.Width == RibbonIcons.ImageDecodeWidth && ReferenceEquals(bitmap, RibbonIcons.Image(key)),
        $"{key} image icon loads at {RibbonIcons.ImageDecodeWidth} px and is reused");
}
foreach (var (label, glyph, image) in new (string, string, string?)[]
{
    ("Detect Instances", "🔎", "detect_instances"), ("Discover Saves", "↻", "discover_saves"), ("Export CSV", "⇩", "export_csv"),
    ("Mark All Read", "👁", "mark_all_read"), ("Refresh Bases", "⌂", "refresh_bases"), ("Repair Firewall", "🛡", "repair_firewall"),
    ("Run Diagnostics", "⚡", "run_diagnostics"), ("Self-Test", "✓", "self_test"), ("Self-Tests", "✓", "self_tests"), ("Validate", "✓", "validate"),
    ("Clear", "✕", "clear"), ("Create", "⇩", "create_backup"), ("Export", "⇩", "export"), ("Import", "⇧", "import"), ("Open Root", "📁", "open_root"),
    ("Pause", "Ⅱ", "pause"), ("Reset", "↺", "reset"), ("Save", "✓", "save"), ("Verify All", "✓", "verify_all"), ("Verify & Scan", "✓", "verify_scan"),
    ("Confirm Install", "⬇", "confirm_install"), ("Disable All", "✕", "disable_all"), ("Enable All", "✓", "enable_all"), ("Export Report", "⇩", "export_report"),
    ("Kill Processes", "⛔", "kill_processes"), ("Preview Install", "👁", "preview_install"), ("Repair", "⚑", "repair"), ("Rollback", "↩", "rollback"),
    ("Run Doctor", "⚕", "run_doctor"), ("Safe-Start", "⚠", "safe_start"),
    ("Backup", "⇩", "create_backup"), ("Doctor", "✚", "run_doctor"), ("Start", "▶", "start"), ("Console", "▰", "console"),
    ("Run Analysis", "🔍", "run_analysis"), ("Preview Plan", "📋", "preview_plan"), ("Refresh Map", "↻", "refresh"), ("Refresh", "↻", "refresh"),
    ("Force Stop", "⛔", "force_stop"), ("Restart", "⟳", "restart"), ("Restart Server", "⟳", "restart"), ("Stop", "■", "stop"),
    ("Verify Files", "✓", "verify_files"), ("Install Missing", "⬇", "install_missing"), ("Refresh History", "↻", "refresh"), ("Refresh Bases", "⌂", "refresh_bases")
})
{
    var action = new RibbonActionViewModel(glyph, label, label, null, null, RibbonIconColor.Amber);
    var expectVector = image is null && RibbonIcons.KeyFor(label, glyph) is not null;
    Check(action.ImageIconKey == image && action.HasImageIcon == (image is not null) && action.HasVectorIcon == expectVector &&
          action.ShowGlyph == (image is null && !expectVector),
        $"Ribbon '{label}' shows {(image ?? (expectVector ? "its vector icon" : "its text glyph"))}");
}
window.Width = 1800; window.Height = 1100;
foreach (var light in new[] { false, true })
{
    ThemeApplier.Apply("Default", light ? "Light" : "Dark");
    vm.IsLightMode = light;
    foreach (var page in new[] { NavigationPage.Doctor, NavigationPage.DiagnosticsCenter, NavigationPage.Notifications, NavigationPage.Players,
                                 NavigationPage.Map, NavigationPage.WorldTransactions, NavigationPage.SaveTools, NavigationPage.Configuration,
                                 NavigationPage.Console, NavigationPage.Backups, NavigationPage.ModDashboard, NavigationPage.ModLibrary, NavigationPage.Ue4ss })
    {
        selectedPage.SetValue(vm, page);
        Render($"ribbon-images-{page}-{(light ? "light" : "dark")}");
        var host = window.FindControl<StackPanel>("RibbonHost")!;
        var buttons = host.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("ribbon") && b.IsEffectivelyVisible && b.DataContext is RibbonActionViewModel { HasImageIcon: true }).ToArray();
        var wrong = buttons.Where(b =>
            !b.GetVisualDescendants().OfType<Image>().Any(i => i.Classes.Contains("ribbonImage") && i.IsEffectivelyVisible && i.Source is not null) ||
            b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Classes.Contains("flatIcon") && t.IsEffectivelyVisible) ||
            b.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Any(p => p.Classes.Contains("ribbonIcon") && p.IsEffectivelyVisible))
            .Select(b => ((RibbonActionViewModel)b.DataContext!).Label).ToArray();
        Check(buttons.Length > 0 && wrong.Length == 0,
            $"{page} ({(light ? "light" : "dark")}): {buttons.Length} button(s) show their image icon, not a glyph or vector [{string.Join(", ", wrong)}]");
    }
}
ThemeApplier.Apply("Default", "Dark");
vm.IsLightMode = false;

// v0.8.13.0: with the last ten images, every Ribbon button on every page shows an image.
foreach (var page in Enum.GetValues<NavigationPage>())
{
    selectedPage.SetValue(vm, page);
    Dispatcher.UIThread.RunJobs();
    window.UpdateLayout();
    var allPagesRibbon = window.FindControl<StackPanel>("RibbonHost")!;
    var without = allPagesRibbon.GetVisualDescendants().OfType<Button>()
        .Where(b => b.Classes.Contains("ribbon") && b.IsEffectivelyVisible && b.DataContext is RibbonActionViewModel { HasImageIcon: false })
        .Select(b => ((RibbonActionViewModel)b.DataContext!).Label).ToArray();
    Check(without.Length == 0, $"{page}: every Ribbon button has an image [{string.Join(", ", without)}]");
}
// v0.8.14.0: cards follow the signed-in role (RoleAccess): hidden when the role cannot use them, disabled when it can only
// read them. Each card is found by its title and checked on its own card Border, so a collapsed section does not matter.
var applyPrincipal = typeof(MainWindowViewModel).GetMethod("ApplyCurrentPrincipal", BindingFlags.Instance | BindingFlags.NonPublic)!;
(bool Visible, bool Enabled) CardState(string title)
{
    var text = window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == title)
        ?? throw new Exception($"card '{title}' not found");
    if (title == "Give items or Pals") return (text.IsVisible, text.IsEnabled);
    var card = text.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("card") || b.Classes.Contains("statuscard"));
    return (card.IsVisible, card.IsEnabled);
}
// (title, minimum rank to see it, minimum rank to use it)
var cardRules = new (string Title, int See, int Use)[]
{
    ("Teleport Points", RoleAccess.Operator, RoleAccess.Admin), ("Give items or Pals", RoleAccess.Admin, RoleAccess.Admin),
    ("Find an item or Pal", RoleAccess.Operator, RoleAccess.Operator), ("Whitelist", RoleAccess.Admin, RoleAccess.Admin),
    ("Starter Kits", RoleAccess.Admin, RoleAccess.Admin), ("Character / Account Migration", RoleAccess.Admin, RoleAccess.Admin),
    ("New Rule", RoleAccess.Admin, RoleAccess.Admin), ("Threshold Rules", RoleAccess.Viewer, RoleAccess.Admin),
    ("Mute Alerts", RoleAccess.Admin, RoleAccess.Admin), ("Pause Discord, Email & Webhooks", RoleAccess.Admin, RoleAccess.Admin),
    ("Discord Bot", RoleAccess.Admin, RoleAccess.Admin), ("Anti-Cheat & Save-Integrity Scanning", RoleAccess.Viewer, RoleAccess.Admin),
    ("Create Principal", RoleAccess.Owner, RoleAccess.Owner), ("User Accounts", RoleAccess.Owner, RoleAccess.Owner),
    ("Fleet Actions", RoleAccess.Operator, RoleAccess.Operator), ("Clone World", RoleAccess.Owner, RoleAccess.Owner),
    // v0.8.17.0: everyone reads the priority card; Admin changes it, as on the server.
    ("Server priority, eco mode and cores", RoleAccess.Viewer, RoleAccess.Admin),
    // v0.8.18.0: the same for the bandwidth card.
    ("Bandwidth limits", RoleAccess.Viewer, RoleAccess.Admin),
};
foreach (var role in new string?[] { null, "Viewer", "Operator", "Admin", "Owner" })
{
    applyPrincipal.Invoke(vm, [role is null ? null : new MystTiqPrincipalDto { Id = "p-" + role, Name = role + " user", Role = role }]);
    Dispatcher.UIThread.RunJobs();
    var rank = role is null ? RoleAccess.Owner : RoleAccess.Rank(role);
    var wrong = cardRules.Where(c =>
    {
        var (visible, enabled) = CardState(c.Title);
        return visible != rank >= c.See || (visible && enabled != rank >= c.Use);
    }).Select(c => c.Title).ToArray();
    Check(wrong.Length == 0, $"{role ?? "local (no sign-in)"}: every role-gated card is shown, read-only or hidden as its role allows [{string.Join(", ", wrong)}]");
    var notice = window.FindControl<Border>("RoleHiddenNotice")!;
    Check(notice.IsVisible == (role is not null && rank < RoleAccess.Owner), $"{role ?? "local"}: the role notice shows only when something is hidden");

    // v0.8.23.0: every command in the role table runs only when the role allows it (local use: all), and on every page no
    // button or menu item bound to one the role may not use is enabled.
    var commandRoles = (System.Collections.IDictionary)typeof(MainWindowViewModel).GetMethod("BuildCommandRoles", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null)!;
    var gateWrong = new List<string>();
    foreach (System.Collections.DictionaryEntry entry in commandRoles)
    {
        var gate = entry.Key.GetType().GetProperty("RoleGate")?.GetValue(entry.Key) as Func<bool>;
        if (gate is null || gate() != rank >= RoleAccess.Rank((string)entry.Value!)) gateWrong.Add($"{entry.Value}:{gate is null}");
    }
    Check(commandRoles.Count >= 100 && gateWrong.Count == 0, $"{role ?? "local"}: all {commandRoles.Count} role-gated commands run only where the role allows [{string.Join(", ", gateWrong.Take(6))}]");
    var pageWrong = new List<string>(); var pageChecked = new HashSet<Control>();
    var hintWrong = new List<string>(); var hintSeen = new HashSet<Control>();
    var previousPage = selectedPage.GetValue(vm);
    foreach (var page in Enum.GetValues<NavigationPage>())
    {
        selectedPage.SetValue(vm, page);
        Dispatcher.UIThread.RunJobs();
        foreach (var control in window.GetVisualDescendants().OfType<Control>())
        {
            var command = control switch { Button b => b.Command, MenuItem m => m.Command, _ => null };
            if (command is null || !commandRoles.Contains(command)) continue;
            pageChecked.Add(control);
            var needs = (string)commandRoles[command]!;
            if (rank < RoleAccess.Rank(needs) && control.IsEffectivelyEnabled) pageWrong.Add($"{page}/{(control as ContentControl)?.Content ?? (control as MenuItem)?.Header}");
            // v0.9.4.0: a control disabled for the role says which role it needs (tooltip shown while disabled, and the
            // accessible help text); one the role may use carries no such hint. The Ribbon has its own tooltip.
            if (control.Classes.Contains("ribbon")) continue;
            var hint = RoleHint.Text(needs, role);
            var roleTip = ToolTip.GetTip(control) as string;
            var hinted = roleTip is not null && roleTip.StartsWith(hint, StringComparison.Ordinal) && ToolTip.GetShowOnDisabled(control) &&
                         Avalonia.Automation.AutomationProperties.GetHelpText(control) == hint;
            if (rank < RoleAccess.Rank(needs) ? !hinted : roleTip?.StartsWith("Needs the ", StringComparison.Ordinal) == true)
                hintWrong.Add($"{page}/{(control as ContentControl)?.Content ?? (control as MenuItem)?.Header}");
            else if (rank < RoleAccess.Rank(needs)) hintSeen.Add(control);
        }
    }
    selectedPage.SetValue(vm, previousPage);
    Dispatcher.UIThread.RunJobs();
    Check(pageChecked.Count > 0 && pageWrong.Count == 0, $"{role ?? "local"}: on every page, buttons for commands the role may not use are disabled ({pageChecked.Count} controls checked) [{string.Join(", ", pageWrong.Distinct().Take(8))}]");
    Check(hintWrong.Count == 0 && (rank >= RoleAccess.Owner || hintSeen.Count > 0),
        $"{role ?? "local"}: each of them says which role it needs, in its tooltip and accessible help text, and no allowed one does ({hintSeen.Count} hinted) [{string.Join(", ", hintWrong.Distinct().Take(8))}]");
    if (role == "Operator")
    {
        selectedPage.SetValue(vm, NavigationPage.Security);
        Render("role-operator-security");
    }
}
applyPrincipal.Invoke(vm, [null]);
Dispatcher.UIThread.RunJobs();
window.Width = 1440; window.Height = 880;

// v0.8.15.0: a refusal by role, scope or sign-in is an error, never parsed as the route's result (a refused whitelist
// save used to look saved). The sign-in route's own 401 result (a message, no "error") is still a result.
Check(AccessRefusal.Describe(System.Net.HttpStatusCode.Forbidden, "{\"error\":\"insufficient-role\",\"required\":\"Admin\"}") == "MystTiq refused this: it needs the Admin role or higher.",
    "a 403 for too low a role is a refusal naming the role it needs");
Check(AccessRefusal.Describe(System.Net.HttpStatusCode.Forbidden, "{\"error\":\"server-scope-mismatch\",\"scopedTo\":\"a\"}")!.Contains("different server")
    && AccessRefusal.Describe(System.Net.HttpStatusCode.Unauthorized, "{\"error\":\"invalid-bearer-token\"}")!.Contains("Sign in again")
    && AccessRefusal.Describe(System.Net.HttpStatusCode.Unauthorized, "")!.Contains("Sign in again")
    && AccessRefusal.Describe(System.Net.HttpStatusCode.Forbidden, "<html>")!.Contains("cannot do this"),
    "a scope mismatch, an expired token, an empty 401 and a non-JSON 403 are refusals too");
Check(AccessRefusal.Describe(System.Net.HttpStatusCode.Unauthorized, "{\"success\":false,\"message\":\"The username or password is not right.\"}") is null
    && AccessRefusal.Describe(System.Net.HttpStatusCode.Conflict, "{\"error\":\"x\"}") is null
    && AccessRefusal.Describe(System.Net.HttpStatusCode.OK, "{\"error\":\"x\"}") is null,
    "the sign-in route's own 401 result, a 409 result and a 200 are not refusals");

// v0.8.16.0: theme modes (Dark, Light, Midnight, High contrast, Follow the system) and density.
static double Luminance(Color c)
{
    static double Channel(byte v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
    return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
}
static double Contrast(Color a, Color b)
{
    var (la, lb) = (Luminance(a), Luminance(b));
    return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
}
static Color Over(Color top, Color bottom)
{
    var a = top.A / 255.0;
    byte Mix(byte t, byte b) => (byte)Math.Round(t * a + b * (1 - a));
    return Color.FromRgb(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B));
}
Color Resource(string key) => Application.Current!.Resources[key] switch
{
    Color color => color,
    ISolidColorBrush brush => brush.Color,
    var other => throw new Exception($"resource {key} is {other?.GetType().Name ?? "missing"}"),
};

ThemeApplier.SystemContrast = () => null; // v0.8.25.0: this machine's own contrast setting must not decide the checks
ThemeApplier.SystemPrefersLight = () => false;
selectedPage.SetValue(vm, NavigationPage.Settings);
vm.SelectedProfile = profiles.Load()[0] with { ThemeVariant = "Dark" };
Check(vm.SelectedThemeModeIndex == 0, "a tab saved as Dark reads as Dark");
vm.SelectedProfile = profiles.Load()[0] with { ThemeVariant = "Light" };
Check(vm.SelectedThemeModeIndex == 1 && vm.IsLightMode, "a tab saved as Light reads as Light");
vm.SelectedProfile = profiles.Load()[0] with { ThemeVariant = "Neon" };
Check(vm.SelectedThemeModeIndex == 0 && !vm.IsLightMode, "an unknown mode reads as Dark");
Check(ThemeCatalog.Modes.Length == 5 && ThemeCatalog.ModeLabels.Length == 5 && window.FindControl<ComboBox>("ThemeModeCombo")!.ItemCount == 5,
    "the Settings mode picker offers Dark, Light, Midnight, High contrast and Follow the system");

for (var i = 0; i < ThemeCatalog.Modes.Length; i++)
{
    var mode = ThemeCatalog.Modes[i];
    vm.SelectedThemeModeIndex = i;
    Dispatcher.UIThread.RunJobs();
    var bg = Resource("Bg0");
    var card = Over(Resource("Card"), bg);
    var textContrast = Contrast(Resource("Text"), card);
    var mutedContrast = Contrast(Resource("Muted"), card);
    var hc = mode == "HighContrast";
    Check(vm.SelectedProfile?.ThemeVariant == mode && vm.SelectedThemeModeIndex == i, $"{mode}: picking it stores it on the tab");
    Check(textContrast >= (hc ? 15 : 7) && mutedContrast >= (hc ? 12 : 4.5),
        $"{mode}: text {textContrast:0.0}:1 and muted text {mutedContrast:0.0}:1 on a card meet {(hc ? "15 and 12" : "7 and 4.5")}");
    if (mode == "Midnight")
        Check(bg == Colors.Black && Resource("Bg1") == Colors.Black && Resource("Bg0Scrim").A >= 0xC0 && !vm.IsLightMode,
            "Midnight: true black backgrounds, the page art dimmed almost to black, dark art");
    if (hc)
    {
        Check(Resource("CardBorderBrush") == Colors.White && Contrast(Resource("Border"), bg) >= 15, "High contrast: white card borders and borders on black");
        Check(new[] { "Green", "Amber", "Red" }.All(k => Contrast(Resource(k), bg) >= 7), "High contrast: status colors reach 7:1 on black");
        Check(Resource("ButtonGradientStop0") != Resource("ButtonGradientHoverStop0"), "High contrast: a hovered button still changes");
    }
    if (mode is "Midnight" or "HighContrast")
        Render($"settings-{mode.ToLowerInvariant()}");
}
selectedPage.SetValue(vm, NavigationPage.Dashboard);
vm.SelectedThemeModeIndex = 2;
Render("dashboard-midnight");
vm.SelectedThemeModeIndex = 3;
Render("dashboard-highcontrast");

// Follow the system: the palette follows the operating system, and changes with it; other modes ignore it.
ThemeApplier.SystemPrefersLight = () => true;
vm.SelectedThemeModeIndex = 4;
Dispatcher.UIThread.RunJobs();
Check(vm.IsLightMode && Resource("Bg0") == ThemeCatalog.Structural["Bg0"]["Light"] && ReferenceEquals(vm.PageArtwork, ArtworkCatalog.Page(vm.SelectedPage, true)),
    "Follow the system: a light system shows the Light palette and art");
ThemeApplier.SystemPrefersLight = () => false;
vm.OnSystemThemeChanged();
Dispatcher.UIThread.RunJobs();
Check(!vm.IsLightMode && Resource("Bg0") == ThemeCatalog.Structural["Bg0"]["Dark"] && ReferenceEquals(vm.PageArtwork, ArtworkCatalog.Page(vm.SelectedPage, false)),
    "Follow the system: the system switching to dark switches the tab to Dark");
vm.SelectedThemeModeIndex = 1;
ThemeApplier.SystemPrefersLight = () => false;
vm.OnSystemThemeChanged();
Check(vm.IsLightMode && Resource("Bg0") == ThemeCatalog.Structural["Bg0"]["Light"], "a tab set to Light ignores the system switching");
vm.SelectedThemeModeIndex = 0;

// v0.8.25.0: the decorative colours (glows, shadows, fixed borders, a few surfaces) follow every mode.
object App(string key) => Avalonia.Application.Current!.Resources[key]!;
Color DecoColor(string key) => App(key) switch { SolidColorBrush b => b.Color, Color c => c, var x => throw new InvalidOperationException($"{key} is {x.GetType().Name}") };
BoxShadows DecoShadow(string key) => (BoxShadows)App(key);
static double Lum(Color c) => DecorativePalette.Luminance(c);
var decoKeys = DecorativePalette.Colors.Keys.ToArray();
Check(decoKeys.Length >= 80 && DecorativePalette.Shadows.Count >= 20, $"the styles' decorative colours are resources: {decoKeys.Length} colours and {DecorativePalette.Shadows.Count} shadows");
vm.SelectedThemeModeIndex = 0; Dispatcher.UIThread.RunJobs();
Check(((SolidColorBrush)App("ButtonForegroundBrush")).Color == Colors.White, "Dark: button text is white, as before");
Check(decoKeys.All(k => DecoColor(k) == Color.Parse(DecorativePalette.Colors[k].Dark)) && DecorativePalette.Shadows.All(s => DecoShadow(s.Key).Equals(BoxShadows.Parse(s.Value))),
    "Dark: every decorative colour and shadow is exactly the value it was tuned with");
var darkWarning = ((LinearGradientBrush)App("WarningGlassGradient")).GradientStops[0].Color;
vm.SelectedThemeModeIndex = 1; Dispatcher.UIThread.RunJobs();
var lightCard = Over(Resource("Card"), Resource("Bg0"));
var lightWrong = decoKeys.Where(k => DecorativePalette.Colors[k] is var d && d.Role switch
{
    DecorativeRole.Foreground => Contrast(DecoColor(k), lightCard) < 4.5,
    DecorativeRole.Background => Lum(Color.Parse(d.Dark)) < 0.18 && Lum(DecoColor(k)) < 0.5,
    DecorativeRole.GlowColor => DecoColor(k).A > Color.Parse(d.Dark).A / 2 + 1,
    _ => false,
}).ToArray();
Check(lightWrong.Length == 0, $"Light: decorative text reads at 4.5:1 on a card, dark decorative surfaces turn light, glows soften [{string.Join(", ", lightWrong.Take(6))}]");
Check(Contrast(((SolidColorBrush)App("SuccessButtonForegroundBrush")).Color, Resource("SuccessGradientStop1")) >= 4.5, "Light: success buttons use dark text on their pale green (white read at under 2:1)");
Check(((LinearGradientBrush)App("WarningGlassGradient")).GradientStops[0].Color != darkWarning, "Light: the last three fixed gradients (warning glass, restore) follow the mode too");
vm.SelectedThemeModeIndex = 3; Dispatcher.UIThread.RunJobs();
var hcWrong = decoKeys.Where(k => DecorativePalette.Colors[k].Role switch
{
    DecorativeRole.Border or DecorativeRole.Foreground => DecoColor(k) != Colors.White,
    DecorativeRole.GlowColor => DecoColor(k).A != 0,
    _ => Lum(Color.Parse(DecorativePalette.Colors[k].Dark)) < 0.18 && Opaque(DecoColor(k)) != Colors.Black,
}).ToArray();
Check(hcWrong.Length == 0 && DecorativePalette.Shadows.Keys.All(s => DecoShadow(s).Count == 0),
    $"High contrast: decorative borders and text white, dark surfaces black, no glows or shadows [{string.Join(", ", hcWrong.Take(6))}]");
static Color Opaque(Color c) => Color.FromRgb(c.R, c.G, c.B);

// v0.8.25.0: Windows' own contrast themes. A stand-in for Windows gives a dark one (Aquatic's colours) and a light one
// (Desert's); High contrast and Follow the system use them, the other modes ignore them.
var aquatic = new SystemContrastPalette(Color.Parse("#202020"), Color.Parse("#FFFFFF"), Color.Parse("#8EE3F0"), Color.Parse("#263B50"),
    Color.Parse("#A6A6A6"), Color.Parse("#75E9FC"), Color.Parse("#202020"), Color.Parse("#FFFFFF"));
var desert = new SystemContrastPalette(Color.Parse("#FFFAEF"), Color.Parse("#3D3D3D"), Color.Parse("#903909"), Color.Parse("#FFF5E3"),
    Color.Parse("#676767"), Color.Parse("#1C5E75"), Color.Parse("#FFFAEF"), Color.Parse("#202020"));
ThemeApplier.SystemContrast = () => aquatic;
vm.SelectedThemeModeIndex = 0; vm.SelectedThemeModeIndex = 3; Dispatcher.UIThread.RunJobs();
Check(Resource("Bg1") == aquatic.Window && Resource("Text") == aquatic.WindowText && ((SolidColorBrush)App("CardBorderBrush")).Color == aquatic.WindowText
      && Resource("Blue") == aquatic.Highlight && Resource("ButtonGradientStop0") == aquatic.ButtonFace && DecoColor(decoKeys.First(k => DecorativePalette.Colors[k].Role == DecorativeRole.Border)) == aquatic.WindowText,
    "a Windows contrast theme on: High contrast uses its window, text, highlight and button colours, decorative borders included");
Check(Contrast(((SolidColorBrush)App("ButtonForegroundBrush")).Color, Resource("ButtonGradientStop0")) >= 4.5, "a dark contrast theme: button text reads at 4.5:1 on the theme's button face");
Render("settings-contrast-aquatic");
ThemeApplier.SystemContrast = () => desert;
vm.OnSystemThemeChanged(); Dispatcher.UIThread.RunJobs();
var desertCard = Over(Resource("Card"), Resource("Bg0"));
Check(Resource("Bg1") == desert.Window && vm.IsLightMode && Contrast(Resource("Text"), desertCard) >= 7 && new[] { "Green", "Amber", "Red" }.All(k => Contrast(Resource(k), desert.Window) >= 3),
    "a light contrast theme (Desert), changed while open: the light palette and day art, text at 7:1, status colours still readable");
Check(Contrast(((SolidColorBrush)App("ButtonForegroundBrush")).Color, Resource("ButtonGradientStop0")) >= 4.5, "a light contrast theme (found in its first render: white text on its light buttons): button text reads at 4.5:1 on the theme's button face");
Render("settings-contrast-desert");
vm.SelectedThemeModeIndex = 4; Dispatcher.UIThread.RunJobs();
Check(Resource("Bg1") == desert.Window, "Follow the system: a Windows contrast theme switches the tab to it");
vm.SelectedThemeModeIndex = 0; Dispatcher.UIThread.RunJobs();
Check(Resource("Bg1") == ThemeCatalog.Structural["Bg1"]["Dark"] && ThemeApplier.CurrentContrast is null, "a tab set to Dark keeps Dark under a Windows contrast theme");
ThemeApplier.SystemContrast = () => null;
vm.SelectedThemeModeIndex = 3; Dispatcher.UIThread.RunJobs();
Check(Resource("Bg1") == Colors.Black && Resource("Text") == Colors.White, "no Windows contrast theme: High contrast is MystTiq's own black and white again");
vm.SelectedThemeModeIndex = 0; Dispatcher.UIThread.RunJobs();

// Density: every tab, remembered on this computer.
selectedPage.SetValue(vm, NavigationPage.Settings);
Dispatcher.UIThread.RunJobs();
Border FirstCard() => window.GetVisualDescendants().OfType<Border>().First(b => b.IsEffectivelyVisible && b.Classes.Contains("card"));
// The buttons at the base size (a class-specific style, such as the Ribbon's, keeps its own size in either density).
var baseButtons = window.GetVisualDescendants().OfType<Button>().Where(b => b.MinHeight == 31).ToList();
var ribbonHeight = window.FindControl<StackPanel>("RibbonHost")!.Bounds.Height;
Check(vm.SelectedDensityIndex == 0 && FirstCard().Padding == new Thickness(11) && baseButtons.Count >= 5 && window.FindControl<ComboBox>("ThemeModeCombo")!.MinHeight == 31,
    $"Comfortable density is the default look (card padding 11, {baseButtons.Count} buttons and the boxes 31 high)");
vm.SelectedDensityIndex = 1;
Dispatcher.UIThread.RunJobs();
Render("settings-compact");
Check(FirstCard().Padding == new Thickness(7) && baseButtons.All(b => b.MinHeight == 26) && window.FindControl<ComboBox>("ThemeModeCombo")!.MinHeight == 26,
    "Compact density: card padding 7, those buttons and the boxes 26 high");
Check(Math.Abs(window.FindControl<StackPanel>("RibbonHost")!.Bounds.Height - ribbonHeight) < 0.5, "Compact density leaves the Ribbon's own buttons their size");
Check(File.ReadAllText(Path.Combine(output, nameof(LocalDisplayPreferencesStore) + ".json")).Contains("Compact"), "the density is remembered (isolated store)");
vm.SelectedDensityIndex = 0;
Dispatcher.UIThread.RunJobs();
Check(FirstCard().Padding == new Thickness(11) && baseButtons.All(b => b.MinHeight == 31), "back to Comfortable restores the default look");

// v0.8.17.0: the HOST tab, with a sample reading (this harness has no server). Everything shown comes from the snapshot.
Check(HostFormat.Bytes(1536) == "1.5 KB" && HostFormat.Bytes(34359738368) == "32 GB" && HostFormat.Rate(125_000) == "1.0 Mbit/s" && HostFormat.Rate(null) == "—" &&
      HostFormat.Uptime(90061) == "1 d 1 h" && HostFormat.Uptime(3700) == "1 h 1 min" && HostFormat.JoinList(["install", "saves", "backups"]) == "install, saves and backups",
    "HOST text: sizes, network rates in bits per second, uptime and lists read plainly");
var hostSample = new HostPageSnapshotDto
{
    Host = new HostSnapshotDto
    {
        MachineName = "PALHOST", OperatingSystem = "Microsoft Windows 10.0.26100", ProcessorName = "Sample 16-Core Processor", LogicalProcessors = 32,
        CpuPercent = 37.4, MemoryTotalBytes = 68_719_476_736, MemoryAvailableBytes = 40_000_000_000, UptimeSeconds = 356_000, ObservedAt = DateTimeOffset.UtcNow,
        Disks = [new HostDiskDto { Name = @"C:\", Label = "System", Format = "NTFS", TotalBytes = 1_000_000_000_000, FreeBytes = 400_000_000_000, Holds = ["install", "saves", "backups"] },
                 new HostDiskDto { Name = @"D:\", Label = "Data", Format = "NTFS", TotalBytes = 2_000_000_000_000, FreeBytes = 150_000_000_000 }],
        Network = [new HostNetworkAdapterDto { Name = "Ethernet", Description = "Sample 2.5GbE", Kind = "Ethernet", SpeedBitsPerSecond = 2_500_000_000, ReceivedBytesPerSecond = 262_144, SentBytesPerSecond = 1_310_720 }],
    },
    Resources = new ResourcePolicySnapshotDto
    {
        Policy = new ResourcePolicyDto { Priority = "AboveNormal", EcoMode = "WhenEmpty", EcoAfterEmptyMinutes = 15 },
        Running = true, EcoActive = false, EcoReason = "3 players online: full speed.", PlayersOnline = 3, EfficiencyModeSupported = true,
        Processes = [new ProcessResourceStateDto { ProcessId = 4824, ProcessName = "PalServer", Priority = "AboveNormal", Efficiency = "Default", WorkingSetBytes = 30_000_000 },
                     new ProcessResourceStateDto { ProcessId = 4830, ProcessName = "PalServer-Win64-Shipping-Cmd", Priority = "AboveNormal", Efficiency = "Default", WorkingSetBytes = 9_800_000_000 }],
    },
    // v0.8.18.0: a server whose full player count would overfill a 30 Mbit/s upload at 2 Mbit/s each.
    Bandwidth = new BandwidthSnapshotDto
    {
        Policy = new NetworkPolicyDto { Mode = "Custom", PerPlayerMbps = 2, TickRate = 30, UploadBudgetMbps = 30 },
        Running = true, PolicyInEngineIni = true, Platform = "Windows", GameDefaultPerPlayerMbps = 64, GameDefaultTickRate = 60,
        EngineMaxClientRate = 250000, EngineMaxInternetClientRate = 250000, EngineTickRate = 30, EffectivePerPlayerMbps = 2, EffectiveTickRate = 30,
        MaxPlayers = 16, WorstCaseUploadMbps = 32, OverUploadBudget = true, SuggestedPerPlayerMbps = 1.5,
    },
};
var applyHost = typeof(MainWindowViewModel).GetMethod("ApplyHostSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;
applyPrincipal.Invoke(vm, [null]);
selectedPage.SetValue(vm, NavigationPage.Host);
Dispatcher.UIThread.RunJobs();
applyHost.Invoke(vm, [hostSample]);
Dispatcher.UIThread.RunJobs();
Render("host-dark");
var hostTexts = VisibleTexts();
Check(vm.IsV5HostCategory && vm.PageTitle == "Host & Performance" && vm.VisibleRibbonGroups.Any(g => g.Title == "Host"),
    "the HOST tab opens the Host page, with its own header and Ribbon group");
Check(hostTexts.Contains("37 % busy") && hostTexts.Contains("Up 4 d 2 h") && hostTexts.Any(t => t.Contains("Sample 16-Core Processor · 32 logical processors")),
    "the machine card shows the processor, its load and the uptime");
Check(hostTexts.Contains("Holds this server's install, saves and backups") && hostTexts.Contains("Nothing of this server") && hostTexts.Contains("Less than 10 % free"),
    "the disks card says what of this server lives on each disk, and warns when one is nearly full");
Check(hostTexts.Contains("↓ 2.1 Mbit/s   ↑ 10.5 Mbit/s") && hostTexts.Contains("Ethernet · link 2.5 Gbit/s"), "the network card shows each adapter's traffic and link speed");
Check(hostTexts.Any(t => t.StartsWith("PalServer-Win64-Shipping-Cmd (PID 4830)")) && hostTexts.Any(t => t.Contains("Priority above normal · efficiency mode system default")),
    "the server's processes are listed with their actual priority and efficiency mode");
Check(vm.EcoStateText == "3 players online: full speed.", $"the eco state reads once, without repeating itself ({vm.EcoStateText})");
Check(vm.SelectedPriorityIndex == 3 && vm.SelectedEcoModeIndex == 2 && vm.EcoAfterEmptyMinutesText == "15" && vm.IsEcoWhenEmpty,
    "the saved policy fills the pickers (above normal, eco when empty after 15 min)");
vm.SelectedPriorityIndex = 4;
applyHost.Invoke(vm, [hostSample]);
Check(vm.SelectedPriorityIndex == 4 && vm.HasPriorityWarning, "an unsaved choice is not overwritten by the next reading, and High shows its warning");
vm.EcoAfterEmptyMinutesText = "0";
vm.SaveResourcePolicyCommand.Execute(null);
Dispatcher.UIThread.RunJobs();
Check(vm.ResourcePolicyStatusText.Contains("1 to 240"), "minutes outside 1..240 are refused before anything is sent");
vm.EcoAfterEmptyMinutesText = "15";
Render("host-priority-high");

// v0.8.18.0: the bandwidth card, from the same sample.
Check(vm.BandwidthDefaultsText == "The game's own limits on this Windows server: 64 Mbit/s per player and 60 network updates per second." &&
      vm.BandwidthNowText == "Engine.ini now: 2 Mbit/s per player and 30 network updates per second (MystTiq's limits).",
    "the bandwidth card shows the game's own limits and what Engine.ini sets now");
Check(vm.BandwidthModeIndex == 1 && vm.IsBandwidthCustom && vm.PerPlayerMbpsText == "2" && vm.TickRateText == "30" && vm.UploadBudgetMbpsText == "30",
    "the saved policy fills the bandwidth fields");
Check(vm.BandwidthWorstCaseText == "With all 16 players at 2 Mbit/s each, the server could send up to 32 Mbit/s. Your upload: 30 Mbit/s." && vm.BandwidthOverBudget &&
      vm.BandwidthSuggestionText == "To fit 16 players into 80 % of your upload: 1.5 Mbit/s per player.",
    "the worst case, the over-upload warning and a suggestion that fits");
vm.ApplyBandwidthSuggestionCommand.Execute(null);
Check(vm.PerPlayerMbpsText == "1.5" && vm.IsBandwidthCustom, "Use It fills in the suggested limit");
applyHost.Invoke(vm, [hostSample]);
Check(vm.PerPlayerMbpsText == "1.5", "an unsaved bandwidth edit is not overwritten by the next reading");
vm.TickRateText = "5";
vm.SaveNetworkPolicyCommand.Execute(null);
Dispatcher.UIThread.RunJobs();
Check(vm.BandwidthStatusText.Contains("10 to 120"), "an update rate out of range is refused before anything is sent");
vm.TickRateText = "30";
Check(MainWindowViewModel.TryReadNumber("2.5", out var dot) && dot == 2.5 && !MainWindowViewModel.TryReadNumber("fast", out _), "limits are read as numbers, a word is refused");
var bandwidthCard = window.FindControl<Border>("HostBandwidthCard")!;
bandwidthCard.BringIntoView();
Render("host-bandwidth");
Check(!vm.BandwidthRestartNeeded, "no restart note while the limits in Engine.ini are the saved ones");

// v0.8.20.0: the history card, from a sample day (a processor peak in the afternoon, one reading without a value).
var applyHistory = typeof(MainWindowViewModel).GetMethod("ApplyHostHistory", BindingFlags.Instance | BindingFlags.NonPublic)!;
var historyStart = DateTimeOffset.UtcNow.AddHours(-24);
var historySamples = Enumerable.Range(0, 288).Select(i => new HostHistorySampleDto
{
    ObservedAt = historyStart.AddMinutes(i * 5),
    CpuPercent = i == 10 ? null : 20 + 60 * Math.Exp(-Math.Pow((i - 180) / 20d, 2)),
    MemoryUsedPercent = 60 + 10 * Math.Sin(i / 30d),
    SentBytesPerSecond = 200_000 + 1_000_000 * Math.Exp(-Math.Pow((i - 200) / 15d, 2)),
}).ToArray();
applyHistory.Invoke(vm, [new HostHistoryDto
{
    Samples = historySamples, RangeHours = 24, ReadingsInRange = 1440, AverageCpuPercent = 27.4, PeakCpuPercent = 80.2,
    AverageMemoryUsedPercent = 61, PeakMemoryUsedPercent = 70, PeakSentBytesPerSecond = 1_200_000, FirstReadingAt = historyStart,
}]);
Dispatcher.UIThread.RunJobs();
Check(vm.HostHistorySummaryText == "Processor 27 % on average, peak 80 % · memory 61 % used on average, peak 70 % · busiest upload 9.6 Mbit/s",
    $"the history summary reads plainly ({vm.HostHistorySummaryText})");
Check(vm.HostHistoryCoverageText.StartsWith("1440 readings since") && window.FindControl<MystTiq.Desktop.Controls.HostHistoryChart>("HostHistoryChart")!.Samples!.Count == 288,
    "the chart gets the points and the coverage line counts the readings");
window.FindControl<Border>("HostHistoryCard")!.BringIntoView();
Render("host-history");
applyHistory.Invoke(vm, [new HostHistoryDto()]);
Check(vm.HostHistorySummaryText.StartsWith("No readings yet") && vm.HostHistoryCoverageText == string.Empty, "no readings yet says so instead of showing zeros");
Check(vm.HostHistoryRangeOptions.Count == 3 && vm.HostHistoryRangeIndex == 1, "the history offers an hour, a day and a week, a day first");
var hostCard = window.FindControl<Border>("HostPriorityCard")!;
Check(hostCard.IsEnabled, "local use can change the priority");
applyPrincipal.Invoke(vm, [new MystTiqPrincipalDto { Id = "p-op", Name = "Operator user", Role = "Operator" }]);
Dispatcher.UIThread.RunJobs();
Check(hostCard.IsVisible && !hostCard.IsEnabled, "an Operator reads the priority card but cannot change it");
applyPrincipal.Invoke(vm, [null]);
Dispatcher.UIThread.RunJobs();
window.Width = 950; window.Height = 650;
Render("host-950x650");
var hostCards = new[] { "HostMachineCard", "HostDisksCard", "HostNetworkCard", "HostPriorityCard", "HostBandwidthCard" }.Select(n => window.FindControl<Border>(n)!).ToArray();
Check(hostCards.All(c => c.Bounds.Width > 300), $"at 950x650 every Host card keeps a usable width [{string.Join(", ", hostCards.Select(c => $"{c.Name} {c.Bounds.Width:0}"))}]");
window.Width = 1440; window.Height = 880;

// v0.8.19.0: the Ribbon follows the signed-in role. Every page is visited for each role; a button is enabled exactly when
// the role reaches what its route needs, and a disabled one names the role in its tooltip.
var ribbonWrong = new List<string>();
var ribbonSeen = new HashSet<string>();
foreach (var role in new string?[] { null, "Viewer", "Operator", "Admin" })
{
    applyPrincipal.Invoke(vm, [role is null ? null : new MystTiqPrincipalDto { Id = "p-" + role, Name = role + " user", Role = role }]);
    var rank = role is null ? RoleAccess.Owner : RoleAccess.Rank(role);
    foreach (var page in Enum.GetValues<NavigationPage>())
    {
        selectedPage.SetValue(vm, page);
        Dispatcher.UIThread.RunJobs();
        foreach (var action in vm.VisibleRibbonGroups.Concat(vm.OverflowRibbonGroups).SelectMany(g => g.Actions))
        {
            var needs = vm.RibbonRequiredRole(action);
            var expected = needs is null || rank >= (needs == "Admin" ? RoleAccess.Admin : RoleAccess.Operator);
            if (needs is not null) ribbonSeen.Add($"{action.Label}={needs}");
            if (action.RoleAllowed != expected || (!expected && !action.ToolTipText.EndsWith($"(needs the {needs} role)")))
                ribbonWrong.Add($"{role ?? "local"}/{page}/{action.Label}");
        }
    }
}
Check(ribbonWrong.Count == 0 && ribbonSeen.Contains("Start=Operator") && ribbonSeen.Contains("Save=Admin") && ribbonSeen.Contains("Enable All=Admin"),
    $"on every page, Ribbon buttons are enabled exactly for the roles their routes allow, and say which role they need [{string.Join(", ", ribbonWrong.Take(8))}] ({ribbonSeen.Count} gated)");
applyPrincipal.Invoke(vm, [new MystTiqPrincipalDto { Id = "p-op", Name = "Operator user", Role = "Operator" }]);
selectedPage.SetValue(vm, NavigationPage.Dashboard);
Dispatcher.UIThread.RunJobs();
var startButton = window.FindControl<StackPanel>("RibbonHost")!.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.DataContext is RibbonActionViewModel { Label: "Start" });
Check(startButton is not null && startButton.IsEnabled, "an Operator's Start button is enabled on the Dashboard Ribbon");
selectedPage.SetValue(vm, NavigationPage.Configuration);
Dispatcher.UIThread.RunJobs();
var saveButton = window.FindControl<StackPanel>("RibbonHost")!.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.DataContext is RibbonActionViewModel { Label: "Save" });
Check(saveButton is not null && !saveButton.IsEnabled && ToolTip.GetTip(saveButton) is string tip && tip.EndsWith("(needs the Admin role)"),
    "an Operator's Save settings button is disabled on the rendered Ribbon, and its tooltip says Admin");
Render("ribbon-operator-configuration");
selectedPage.SetValue(vm, NavigationPage.Players);
Dispatcher.UIThread.RunJobs();
var kick = window.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Content as string == "Kick");
Check(kick is not null && !kick.IsEnabled, "an Operator's Kick button is disabled (kick and ban need Admin on the server)");
applyPrincipal.Invoke(vm, [null]);
Dispatcher.UIThread.RunJobs();
Check(kick!.IsEnabled || !vm.KickSelectedPlayerCommand.CanExecute(null), "local use: Kick is enabled whenever its command can run");

// v0.9.4.0: the role hint is in the chosen language, and goes away when the role allows the command.
applyPrincipal.Invoke(vm, [new MystTiqPrincipalDto { Id = "p-viewer", Name = "Viewer user", Role = "Viewer" }]);
Localizer.Instance.SetLanguage("ja");
Dispatcher.UIThread.RunJobs();
var jaKickHint = jaText[MsgKey("Needs the {0} role. You are signed in as {1}.")].Replace("{0}", jaText[MsgKey("Admin")]).Replace("{1}", jaText[MsgKey("Viewer")]);
Check(!kick.IsEnabled && ToolTip.GetTip(kick) is string jaTip && jaTip.StartsWith(jaKickHint, StringComparison.Ordinal) && ToolTip.GetShowOnDisabled(kick),
    $"Japanese: a Viewer's disabled Kick button says, in Japanese, that it needs Admin [{ToolTip.GetTip(kick)}]");
Render("role-hint-viewer-players-ja");
Localizer.Instance.SetLanguage("en");
applyPrincipal.Invoke(vm, [new MystTiqPrincipalDto { Id = "p-admin", Name = "Admin user", Role = "Admin" }]);
Dispatcher.UIThread.RunJobs();
Check(ToolTip.GetTip(kick) is not string back || !back.StartsWith("Needs the ", StringComparison.Ordinal),
    "Signing in as Admin removes the hint from Kick (its own tooltip, if any, comes back)");
applyPrincipal.Invoke(vm, [null]);
Dispatcher.UIThread.RunJobs();

// v0.9.4.0: accessibility. On every page, every visible control a keyboard or screen-reader user can reach has a name:
// its text, an explicit accessible name, or a tooltip.
string? AccessibleName(Control c)
{
    if (Avalonia.Automation.AutomationProperties.GetName(c) is { Length: > 0 } name) return name;
    if (c is ContentControl { Content: string { Length: > 0 } text }) return text;
    if (c.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text)) is { } inner) return inner.Text;
    if (c is TextBox { PlaceholderText: string { Length: > 0 } mark }) return mark;
    return ToolTip.GetTip(c) as string is { Length: > 0 } tipName ? tipName : null;
}
var unnamed = new List<string>();
var unreachable = new List<string>();
var namedCount = 0;
foreach (var page in Enum.GetValues<NavigationPage>())
{
    selectedPage.SetValue(vm, page);
    Dispatcher.UIThread.RunJobs();
    foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible &&
                 c is Button or Avalonia.Controls.Primitives.ToggleButton or ComboBox or TextBox or Slider or NumericUpDown or ListBox))
    {
        // A part of another control's template (a number box's arrows, a drop-down's own text box) is named by that control.
        if (control.TemplatedParent is not null) continue;
        if (AccessibleName(control) is null)
        {
            var near = control.GetVisualAncestors().OfType<Panel>().Take(2).SelectMany(p => p.Children.OfType<TextBlock>()).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Text))?.Text;
            unnamed.Add($"{page}/{control.GetType().Name}{(control.Name is { Length: > 0 } n ? "#" + n : "")} near \"{near}\" ctx={control.DataContext?.GetType().Name}");
        }
        else namedCount++;
        // Keyboard: an enabled control can take focus and is in the Tab order (a list or a number box passes focus to its items
        // or its own text box, so those are reached through them).
        if (control is not (ListBox or NumericUpDown) && control.IsEffectivelyEnabled && (!control.Focusable || !Avalonia.Input.KeyboardNavigation.GetIsTabStop(control)))
            unreachable.Add($"{page}/{control.GetType().Name}{(control.Name is { Length: > 0 } k ? "#" + k : "")} {AccessibleName(control)}");
    }
}
Check(unreachable.Count == 0, $"on every page every enabled control can be reached with Tab [{string.Join(", ", unreachable.Distinct().Take(8))}]");
Check(unnamed.Count == 0, $"on every page every reachable control has an accessible name ({namedCount} named) [{string.Join(", ", unnamed.Distinct().Take(8))}]");

// v1.0.0.1: the Dashboard's addresses line and stuck-start panel, and the text they show.
{
    var addressesDto = new HostAddressesDto
    {
        GamePort = 8211,
        Local = [new LocalAddressDto { Adapter = "Ethernet", Address = "192.168.1.50", HasGateway = true }, new LocalAddressDto { Adapter = "vEthernet", Address = "172.20.0.1" }],
        PublicAddress = "203.0.113.7", PublicSource = "router"
    };
    var (localText, publicText, noteText) = HostAddressText.Describe(addressesDto);
    Check(localText == "192.168.1.50:8211  ·  172.20.0.1:8211" && publicText == "203.0.113.7:8211" && noteText == "from your router",
        $"The addresses line shows every local address and the public one with the server's port, and where it came from [{localText} | {publicText} | {noteText}]");
    var missing = HostAddressText.Describe(new HostAddressesDto { GamePort = 8211, PublicError = "The public address could not be found: timeout" });
    Check(missing.Local == "—" && missing.Public == "—" && missing.Note.StartsWith("The public address could not be found", StringComparison.Ordinal), "Without addresses the line says why");
    var stuckText = StuckStartText.Describe(new SafeStartStatusDto
    {
        Completed = true, Success = true, Mode = "OneAtATime", FinalMessage = "Left off: HangsStartup. The server is running with the other MODs.",
        Results = [new SafeStartModResultDto { Package = "(no MODs)", Ok = true, Detail = "Ready in 6 s." }, new SafeStartModResultDto { Package = "HangsStartup", Ok = false, Detail = "Timed out without becoming ready (hang, not a crash)." }]
    });
    Check(stuckText == "Without MODs: Ready in 6 s.\nHangsStartup: Timed out without becoming ready (hang, not a crash).\nLeft off: HangsStartup. The server is running with the other MODs.", $"The stuck-start panel lists each start and the outcome [{stuckText}]");
    Localizer.Instance.SetLanguage("de");
    Check(Localizer.T("Without MODs: Ready in 6 s.") != "Without MODs: Ready in 6 s." && Localizer.T("from your router") == Localizer.Parse(ReadLanguage("de"))["msg.from_your_router"],
        "German: the panel's and the addresses line's texts are translated");
    Localizer.Instance.SetLanguage("en");
    vm.NavigateCommand.Execute(NavigationPage.Dashboard);
    Dispatcher.UIThread.RunJobs();
    var panel = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "StuckStartPanel");
    var addressesCard = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "DashboardAddresses");
    Check(panel is { IsVisible: false } && addressesCard is { IsVisible: true }, "The Dashboard has the addresses line, and the stuck-start panel stays hidden while no start is stuck");
}
// v1.0.0.2: the Players page's Unique player names card and its text.
{
    var config = new NameGuardConfigDto { Enabled = true, KickDuplicates = true, Claims = [new NameClaimDto { Name = "Wade", OwnerId = "steam_1", OwnerName = "Wade" }] };
    Check(NameGuardText.Describe(config) == "Unique names are on: 1 names are taken. A player using another account's name is kicked.", $"The card says the guard is on and kicks [{NameGuardText.Describe(config)}]");
    var (reserved, reserveMessage) = NameGuardText.Reserve(config.Claims, "  wade ", "76561197962020201", DateTimeOffset.UtcNow);
    Check(reserved is [{ Name: "wade", OwnerId: "steam_76561197962020201", Reserved: true }] && reserveMessage == "Reserved once you save.", "Reserving a listed name (any case) gives it to the new owner; a bare Steam ID gains steam_");
    var (blocked, _) = NameGuardText.Reserve(config.Claims, "Admin", "", DateTimeOffset.UtcNow);
    Check(blocked is [{ Name: "Admin", OwnerId: "", Kind: "Blocked for everyone" }, { Name: "Wade", Kind: "First to use it" }] && NameGuardText.Reserve(config.Claims, " ", "", DateTimeOffset.UtcNow).Claims is null,
        "An empty owner blocks the name for everyone; an empty name is refused");
    Localizer.Instance.SetLanguage("de");
    Check(Localizer.T(NameGuardText.Describe(config)) != NameGuardText.Describe(config) && Localizer.T("First to use it") == Localizer.Parse(ReadLanguage("de"))["msg.name_kind_first"] &&
          Localizer.T("steam_2 joined as \"Wade\", a name that belongs to Wade (steam_1). They were kicked.").StartsWith("steam_2 ist als \"Wade\" beigetreten", StringComparison.Ordinal),
        "German: the card's status, the kinds and the service's notice are translated (the names are not)");
    Localizer.Instance.SetLanguage("en");
    vm.NavigateCommand.Execute(NavigationPage.Players);
    Dispatcher.UIThread.RunJobs();
    var card = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "UniqueNamesCard");
    Check(card is { IsVisible: true }, "The Players page has the Unique player names card");
    // v1.0.0.2 (seen live): a card's Save button was disabled while the card loaded (busy) and stayed disabled afterwards.
    var isBusy = typeof(MainWindowViewModel).GetProperty("IsBusy")!;
    isBusy.SetValue(vm, true);
    // As at sign-in or connect: the role gates are re-evaluated while the app is busy, so every gated button reads disabled.
    typeof(MainWindowViewModel).GetMethod("RaiseCommandRoleGatesChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(vm, null);
    Dispatcher.UIThread.RunJobs();
    var blockedWhileBusy = !vm.SaveNameGuardCommand.CanExecute(null) && !vm.SaveWhitelistCommand.CanExecute(null);
    isBusy.SetValue(vm, false);
    Dispatcher.UIThread.RunJobs();
    var saveButtons = window.GetLogicalDescendants().OfType<Button>().Where(b => ReferenceEquals(b.Command, vm.SaveNameGuardCommand) || ReferenceEquals(b.Command, vm.SaveWhitelistCommand)).ToList();
    Check(blockedWhileBusy && saveButtons.Count == 2 && saveButtons.All(b => b.IsEffectivelyEnabled),
        $"Save names and Save Whitelist are disabled while busy and enabled again afterwards [{string.Join(", ", saveButtons.Select(b => b.IsEffectivelyEnabled))}]");
}
// v0.9.10.0 (external review): a recorded helper that is alive but slow to answer was forgotten and a second one started.
// Stand-in helpers (this harness, started again) answer late or never; the bootstrapper uses its own runtime folder here.
{
    var helperRoot = Path.Combine(Path.GetTempPath(), "mysttiq-helper-check-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(helperRoot);
    var appVersion = typeof(LocalManagementBootstrapper).Assembly.GetName().Version!.ToString(4);
    var self = Environment.ProcessPath!;
    int FreePort() { var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }
    Process StartHelper(int port, int delayMs)
    {
        var info = new ProcessStartInfo(self) { UseShellExecute = false, CreateNoWindow = true };
        // Started through the dotnet host (not the apphost), the harness's own assembly is the first argument.
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(typeof(MemoryProfiles).Assembly.Location);
        foreach (var a in new[] { "--slow-helper", port.ToString(), delayMs.ToString(), appVersion }) info.ArgumentList.Add(a);
        var p = Process.Start(info)!;
        for (var i = 0; i < 40; i++) { try { using var c = new TcpClient(); c.Connect(IPAddress.Loopback, port); break; } catch { Thread.Sleep(250); } }
        return p;
    }
    LocalInstallationSnapshot Snapshot(int port) => new(DateTimeOffset.UtcNow, "test", LocalMystTiqServiceState.NotInstalled, false, null, LocalPalServerInstallationState.NotFound,
        null, null, false, false, null, null, false, null, null, null, null, $"http://127.0.0.1:{port}", false, false, "test", "test");
    // Off the UI thread: the bootstrapper's awaits would otherwise wait for the dispatcher this thread is blocking.
    LocalManagementBootstrapResult Ensure() { var port = FreePort(); return Task.Run(() => new LocalManagementBootstrapper(helperRoot).EnsureAvailableAsync(Snapshot(port))).GetAwaiter().GetResult(); }
    var slowPort = FreePort();
    using var slow = StartHelper(slowPort, 2000);
    var stuckPort = FreePort();
    using var stuck = StartHelper(stuckPort, 600000);
    try
    {
        SidecarState.Write(helperRoot, new SidecarState(slow.Id, $"http://127.0.0.1:{slowPort}", self, slow.StartTime.ToUniversalTime()));
        var reuse = Ensure();
        Check(reuse.Available && !reuse.Started && reuse.Endpoint == $"http://127.0.0.1:{slowPort}" && !slow.HasExited,
            $"A recorded helper that answers only after 2 s (the probe waits 750 ms) is waited for and reused, not replaced [{reuse.Detail}]");

        SidecarState.Write(helperRoot, new SidecarState(slow.Id, $"http://127.0.0.1:{slowPort}", self, slow.StartTime.ToUniversalTime().AddMinutes(-10)));
        var reused = Ensure();
        Check(!slow.HasExited && reused.Endpoint != $"http://127.0.0.1:{slowPort}",
            "A record whose process started at another time (a process id reused by another program) is neither reused nor stopped");
        if (SidecarState.Read(helperRoot) is { } startedByCheck && startedByCheck.ProcessId != slow.Id) { try { Process.GetProcessById(startedByCheck.ProcessId).Kill(); } catch { } }

        SidecarState.Write(helperRoot, new SidecarState(stuck.Id, $"http://127.0.0.1:{stuckPort}", self, stuck.StartTime.ToUniversalTime()));
        var replaced = Ensure();
        Check(stuck.HasExited && replaced.Endpoint != $"http://127.0.0.1:{stuckPort}",
            $"A recorded helper that never answers is stopped, and has exited before a replacement is tried [{replaced.Detail}]");
        if (SidecarState.Read(helperRoot) is { } replacement) { try { Process.GetProcessById(replacement.ProcessId).Kill(); } catch { } }
    }
    finally
    {
        foreach (var p in new[] { slow, stuck }) { try { if (!p.HasExited) p.Kill(); } catch { } }
        try { Directory.Delete(helperRoot, true); } catch { }
    }
}

Console.WriteLine($"PASS {checks} checks. Offline headless rendering only; no live server acceptance.");

public sealed class MemoryProfiles : IConnectionProfileStore
{
    private List<ConnectionProfile> profiles = [new("artwork-review", "Artwork Preview", new Uri("http://127.0.0.1:1"))];
    public IReadOnlyList<ConnectionProfile> Load() => profiles;
    public void Save(IEnumerable<ConnectionProfile> values) => profiles = values.ToList();
    public string StoragePath => "in-memory";
}
public class OfflineProxy : DispatchProxy
{
    public static T Create<T>() where T : class => DispatchProxy.Create<T, OfflineProxy>();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var type = method!.ReturnType;
        if (type == typeof(void)) return null;
        if (type == typeof(Task)) return Task.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            return typeof(Task).GetMethods().Single(m => m.Name == nameof(Task.FromException) && m.IsGenericMethod)
                .MakeGenericMethod(type.GenericTypeArguments[0]).Invoke(null, [new InvalidOperationException("Offline artwork test: services unavailable")]);
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
