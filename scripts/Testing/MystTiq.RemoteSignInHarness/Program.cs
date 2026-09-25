using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MystTiq.Core.Services;
using MystTiq.Desktop;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;
using MystTiq.Desktop.ViewModels;

// v0.8.15.0: a real remote sign-in. The Desktop's own ViewModel, window and API client (with TLS certificate pinning)
// sign in to a remote MystTiq over the network as each role, and what the role may see and do is checked end to end:
// the principal the server returns, the cards the Desktop shows, and the server refusing what the role may not do.
// Every local store is isolated in a temp folder or in memory; no local sidecar is started and nothing on this PC's
// real Desktop setup is read or written.
//
//   MystTiq.RemoteSignInHarness <settings.json> <output folder>
//   settings: { "baseUrl": "https://host:port/", "pin": "<sha256 hex of the server certificate>",
//               "accounts": [ { "role": "Viewer", "username": "...", "password": "..." }, ... ], "renderPrefix": "remote" }
// v0.8.22.0: an Owner account too, the v0.8.19.0 Ribbon gating checked in the signed-in window (and by the server),
// and the same harness published for linux-x64 and run on the Linux VM, so the Desktop itself signs in from Linux.

var settings = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
var output = Path.GetFullPath(args.Length > 1 ? args[1] : "remote-signin-checks");
Directory.CreateDirectory(output);
var baseUrl = new Uri(settings.GetProperty("baseUrl").GetString()!);
var pin = settings.GetProperty("pin").GetString()!;
var accounts = settings.GetProperty("accounts").EnumerateArray()
    .Select(a => (Role: a.GetProperty("role").GetString()!, User: a.GetProperty("username").GetString()!, Password: a.GetProperty("password").GetString()!))
    .ToArray();
// v0.8.22.0: optional; the Linux run names its renders "linux-...".
var renderPrefix = settings.TryGetProperty("renderPrefix", out var prefixValue) ? prefixValue.GetString()! : "remote";

AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var checks = 0; var failures = new List<string>();
void Check(bool ok, string description)
{
    checks++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + description);
    if (!ok) failures.Add(description);
}
T Isolate<T>(T store) where T : class
{
    typeof(T).GetField("<StoragePath>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(store, Path.Combine(output, typeof(T).Name + "-" + Guid.NewGuid().ToString("N") + ".json"));
    return store;
}
// Everything runs inside the dispatcher's own loop: the Desktop's commands and awaits resume on the UI thread, as in the
// real app. (Awaiting on the main thread with the loop not running deadlocks at the first network call.)
async Task Flush() => await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
async Task<bool> WaitFor(Func<bool> condition, int seconds)
{
    var until = DateTime.UtcNow.AddSeconds(seconds);
    while (DateTime.UtcNow < until)
    {
        await Flush();
        if (condition()) return true;
        await Task.Delay(200);
    }
    await Flush();
    return condition();
}

var exitCode = 1;
var finished = new CancellationTokenSource();
using var watchdog = new Timer(_ => { Console.WriteLine("FAIL the remote sign-in test did not finish within 10 minutes"); Environment.Exit(3); }, null, TimeSpan.FromMinutes(10), Timeout.InfiniteTimeSpan);
Dispatcher.UIThread.Post(async () =>
{
    try { exitCode = await RunAsync(); }
    catch (Exception ex) { Console.WriteLine("FAIL unexpected error: " + ex); exitCode = 1; }
    finally { finished.Cancel(); }
});
Dispatcher.UIThread.MainLoop(finished.Token);
return exitCode;

async Task<int> RunAsync()
{
var api = new MystTiqApiClient();
var profile = new ConnectionProfile("remote-signin", "Remote sign-in test", baseUrl, pin);

// Certificate pinning: the same address with a wrong pin must not even reach the sign-in route.
try
{
    await api.LoginAsync(profile with { ServerCertificateSha256 = new string('0', 64) }, accounts[0].User, accounts[0].Password);
    Check(false, "a wrong certificate pin is refused before signing in");
}
catch (Exception ex) { Check(ex is HttpRequestException, $"a wrong certificate pin is refused before signing in ({ex.GetType().Name})"); }

// (title, minimum rank to see it, minimum rank to use it) -- the v0.8.14.0 table.
var cardRules = new (string Title, int See, int Use)[]
{
    ("Teleport Points", RoleAccess.Operator, RoleAccess.Admin), ("Give items or Pals", RoleAccess.Admin, RoleAccess.Admin),
    ("Find an item or Pal", RoleAccess.Operator, RoleAccess.Operator), ("Whitelist", RoleAccess.Admin, RoleAccess.Admin),
    ("Starter Kits", RoleAccess.Admin, RoleAccess.Admin), ("Character / Account Migration", RoleAccess.Admin, RoleAccess.Admin),
    ("Mute Alerts", RoleAccess.Admin, RoleAccess.Admin), ("Discord Bot", RoleAccess.Admin, RoleAccess.Admin),
    ("Create Principal", RoleAccess.Owner, RoleAccess.Owner), ("User Accounts", RoleAccess.Owner, RoleAccess.Owner),
    ("Fleet Actions", RoleAccess.Operator, RoleAccess.Operator), ("Clone World", RoleAccess.Owner, RoleAccess.Owner),
};

async Task<int> Status(Func<Task> call)
{
    try { await call(); return 200; }
    catch (HttpRequestException ex) when (ex.StatusCode is not null) { return (int)ex.StatusCode; }
}

foreach (var account in accounts)
{
    var profiles = new MemoryProfiles(profile);
    var vm = new MainWindowViewModel(api, profiles,
        OfflineProxy.Create<ILocalInstallationDiscoveryService>(), OfflineProxy.Create<IMystTiqServiceDiscoveryService>(),
        OfflineProxy.Create<ILocalManagementBootstrapper>(), credentialStore: Isolate(new CredentialStore()), tabSessionStore: Isolate(new TabSessionStore()),
        nexusClient: OfflineProxy.Create<INexusModsClient>());
    var window = new MainWindow { DataContext = vm, Width = 1600, Height = 1000 };
    window.Show();
    await Flush();
    vm.SelectedProfile = profiles.Load()[0];
    await Flush();
    if (account == accounts[0])
    {
        // v0.8.22.0: the window's own controls draw their icons, so they show on Linux (and Windows 10), which lack the
        // Segoe Fluent Icons font; they used to be empty boxes there.
        var chrome = new[] { "Close tab", "Open Settings", "Minimize", "Maximize", "Close window" };
        var missing = chrome.Where(name => window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == name) is not { } b
            || !b.GetVisualDescendants().OfType<PathIcon>().Any(i => i.Data is not null && i.Bounds.Width > 0)).ToList();
        var fontGlyphs = window.GetVisualDescendants().OfType<TextBlock>().Count(t => t.FontFamily.ToString().Contains("Segoe Fluent Icons"));
        Check(missing.Count == 0 && fontGlyphs == 0,
            $"the window controls, settings and tab close buttons draw their icons (no icon-font glyphs: {fontGlyphs}) [{string.Join(", ", missing)}]");
    }

    // A wrong password first: refused, nobody signed in.
    vm.SignInUsername = account.User; vm.SignInPassword = account.Password + "-wrong";
    vm.SignInCommand.Execute(null);
    await WaitFor(() => !vm.IsBusy && vm.SignInStatusText.Length > 0, 30);
    // Straight to the route as well: the server itself answers "not signed in" (not merely a connection failure).
    var wrongPassword = await api.LoginAsync(profile, account.User, account.Password + "-wrong");
    Check(vm.CurrentPrincipal is null && string.IsNullOrEmpty(vm.BearerToken) && !wrongPassword.Success && string.IsNullOrEmpty(wrongPassword.Token),
        $"{account.Role}: a wrong password is refused by the server ({wrongPassword.Message}; {vm.SignInStatusText})");

    // The real sign-in, exactly as the Sign In button does it.
    vm.SignInUsername = account.User; vm.SignInPassword = account.Password;
    vm.SignInCommand.Execute(null);
    var signedIn = await WaitFor(() => vm.CurrentPrincipal is not null, 45);
    Check(signedIn && vm.CurrentPrincipal!.Role == account.Role && vm.CurrentPrincipal.Name.Length > 0,
        $"{account.Role}: signs in over TLS and the server reports the role ({vm.CurrentPrincipal?.Name} / {vm.CurrentPrincipal?.Role}; {vm.SignInStatusText})");
    if (!signedIn) { window.Close(); continue; }
    await Flush();

    var rank = RoleAccess.Rank(account.Role);
    var wrong = new List<string>();
    foreach (var card in cardRules)
    {
        // The logical tree: a collapsible card's title sits in its toggle button's content, which has no visual tree
        // until that page is shown.
        var text = window.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == card.Title);
        if (text is null) { wrong.Add(card.Title + " (not found)"); continue; }
        var (visible, enabled) = card.Title == "Give items or Pals" ? (text.IsVisible, text.IsEnabled)
            : text.GetLogicalAncestors().OfType<Border>().First(b => b.Classes.Contains("card") || b.Classes.Contains("statuscard")) is { } b ? (b.IsVisible, b.IsEnabled) : (false, false);
        if (visible != rank >= card.See || (visible && enabled != rank >= card.Use)) wrong.Add(card.Title);
    }
    Check(wrong.Count == 0, $"{account.Role}: the signed-in Desktop shows, disables and hides cards for this role [{string.Join(", ", wrong)}]");
    Check(window.FindControl<Border>("RoleHiddenNotice")!.IsVisible == rank < RoleAccess.Owner, $"{account.Role}: the role notice shows");

    // v0.8.22.0: the v0.8.19.0 Ribbon gating in the real signed-in window. Every page's Ribbon: a button whose route needs
    // a higher role is marked not allowed, disabled in the window, and its tooltip names the role; the rest are allowed.
    // The page buttons bound to the role (kick/ban/teleport/RCON need Admin, save-now Operator) follow the same rule.
    var gated = new Dictionary<string, int>();
    var ribbonWrong = new List<string>();
    // v0.8.23.0: every command follows the role too, so the page buttons and menu items bound to one the role may not
    // use are disabled as well.
    var commandRoles = (System.Collections.IDictionary)typeof(MainWindowViewModel).GetMethod("BuildCommandRoles", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null)!;
    var pageControls = new HashSet<Control>(); var pageWrong = new List<string>();
    foreach (var page in Enum.GetValues<NavigationPage>())
    {
        selectedPageSet(vm, page);
        await Flush();
        foreach (var control in window.GetVisualDescendants().OfType<Control>())
        {
            var command = control switch { Button b => b.Command, MenuItem m => m.Command, _ => null };
            if (command is null || !commandRoles.Contains(command)) continue;
            pageControls.Add(control);
            if (rank < RoleAccess.Rank((string)commandRoles[command]!) && control.IsEffectivelyEnabled) pageWrong.Add($"{page}/{(control as ContentControl)?.Content}");
        }
        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.DataContext is RibbonActionViewModel).ToList();
        foreach (var action in vm.VisibleRibbonGroups.Concat(vm.OverflowRibbonGroups).SelectMany(g => g.Actions).Where(a => a.RequiredRole is not null))
        {
            var needed = RoleAccess.Rank(action.RequiredRole);
            gated[action.RequiredRole!] = gated.GetValueOrDefault(action.RequiredRole!) + 1;
            if (action.RoleAllowed != rank >= needed) ribbonWrong.Add($"{page}/{action.Label} allowed={action.RoleAllowed}");
            if (!action.RoleAllowed && !action.ToolTipText.Contains($"needs the {action.RequiredRole} role")) ribbonWrong.Add($"{page}/{action.Label} tooltip");
            if (!action.RoleAllowed && buttons.FirstOrDefault(b => ReferenceEquals(b.DataContext, action)) is { IsEnabled: true }) ribbonWrong.Add($"{page}/{action.Label} enabled in the window");
        }
    }
    Check(ribbonWrong.Count == 0 && gated.GetValueOrDefault("Operator") > 0 && gated.GetValueOrDefault("Admin") > 0,
        $"{account.Role}: the Ribbon follows the role on every page (role-gated buttons seen: Operator {gated.GetValueOrDefault("Operator")}, Admin {gated.GetValueOrDefault("Admin")}) [{string.Join(", ", ribbonWrong.Take(8))}]");
    Check(pageControls.Count > 0 && pageWrong.Count == 0,
        $"{account.Role}: every page's buttons for commands the role may not use are disabled ({pageControls.Count} controls) [{string.Join(", ", pageWrong.Distinct().Take(8))}]");
    Check(vm.CanOperate == rank >= RoleAccess.Operator && vm.CanManageAdmin == rank >= RoleAccess.Admin && vm.CanManagePrincipals == rank >= RoleAccess.Owner,
        $"{account.Role}: the page buttons bound to the role follow it (operate {vm.CanOperate}, admin {vm.CanManageAdmin}, owner {vm.CanManagePrincipals})");
    selectedPageSet(vm, NavigationPage.Dashboard);
    await Flush();

    // The server agrees: what the role may not do is refused there too, and what it may do works.
    var readKits = await Status(() => api.GetKitsAsync(profile, vm.BearerToken));
    var readWhitelist = await Status(() => api.GetWhitelistAsync(profile, vm.BearerToken));
    var writeWhitelist = readWhitelist == 200
        ? await Status(async () => await api.SaveWhitelistAsync(profile, await api.GetWhitelistAsync(profile, vm.BearerToken), vm.BearerToken))
        : await Status(() => api.SaveWhitelistAsync(profile, new WhitelistConfigDto(), vm.BearerToken));
    var expectedRead = rank >= RoleAccess.Operator ? 200 : 403;
    var expectedWrite = rank >= RoleAccess.Admin ? 200 : 403;
    Check(readKits == expectedRead && readWhitelist == expectedRead && writeWhitelist == expectedWrite,
        $"{account.Role}: the server allows and refuses exactly what the Desktop shows (read kits {readKits}, read whitelist {readWhitelist}, save whitelist {writeWhitelist}; expected {expectedRead}/{expectedRead}/{expectedWrite})");

    // v0.8.22.0: one route behind each kind of gated button, harmless on this empty instance. Mark all read is an
    // Operator button, saving the priority policy unchanged an Admin one, the user list Owner only.
    var markRead = await Status(() => api.MarkAllNotificationsReadAsync(profile, vm.BearerToken));
    var host = await api.GetHostAsync(profile, vm.BearerToken);
    var savePolicy = await Status(() => api.SaveResourcePolicyAsync(profile, host.Resources.Policy, vm.BearerToken));
    var listUsers = await Status(() => api.GetUsersAsync(profile, vm.BearerToken));
    var expected = (Operator: rank >= RoleAccess.Operator ? 200 : 403, Admin: rank >= RoleAccess.Admin ? 200 : 403, Owner: rank >= RoleAccess.Owner ? 200 : 403);
    Check(markRead == expected.Operator && savePolicy == expected.Admin && listUsers == expected.Owner,
        $"{account.Role}: the server agrees with the gated buttons (mark all read {markRead}, save priority policy {savePolicy}, list users {listUsers}; expected {expected.Operator}/{expected.Admin}/{expected.Owner})");

    selectedPageSet(vm, NavigationPage.Security);
    await Flush();
    using (var frame = window.CaptureRenderedFrame()) frame?.Save(Path.Combine(output, $"{renderPrefix}-{account.Role.ToLowerInvariant()}-security.png"));
    selectedPageSet(vm, NavigationPage.Players);
    await Flush();
    using (var frame = window.CaptureRenderedFrame()) frame?.Save(Path.Combine(output, $"{renderPrefix}-{account.Role.ToLowerInvariant()}-players.png"));

    // Sign Out is unavailable while the app is busy (as its button is), so wait for the refresh after sign-in first.
    var idle = await WaitFor(() => !vm.IsBusy, 60);
    var token = vm.BearerToken;
    vm.SignOutCommand.Execute(null);
    var signedOut = await WaitFor(() => vm.CurrentPrincipal is null && !vm.IsBusy, 30);
    var afterSignOut = await Status(() => api.GetKitsAsync(profile, null));
    var oldToken = await Status(() => api.GetKitsAsync(profile, token));
    Check(idle && signedOut && string.IsNullOrEmpty(vm.BearerToken) && afterSignOut == 401 && oldToken == 401,
        $"{account.Role}: signing out forgets the role and ends the session on the server (no token {afterSignOut}, old token {oldToken}; idle {idle}, principal {vm.CurrentPrincipal?.Role ?? "none"}; {vm.SignInStatusText})");
    window.Close();
}

Console.WriteLine(failures.Count == 0 ? $"PASS {checks} checks against {baseUrl}" : $"FAILED {failures.Count} of {checks} checks");
return failures.Count == 0 ? 0 : 1;
}

static void selectedPageSet(MainWindowViewModel vm, NavigationPage page) =>
    typeof(MainWindowViewModel).GetProperty(nameof(MainWindowViewModel.SelectedPage))!.SetValue(vm, page);

public sealed class MemoryProfiles(ConnectionProfile profile) : IConnectionProfileStore
{
    private List<ConnectionProfile> profiles = [profile];
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
                .MakeGenericMethod(type.GenericTypeArguments[0]).Invoke(null, [new InvalidOperationException("Remote sign-in test: local services are not used")]);
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
