using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MystTiq.Core.Automation;
using MystTiq.Core.Models;
using MystTiq.Core.Providers;
using MystTiq.Core.Security;
using MystTiq.Core.Services;
using MystTiq.HeadlessHost;

// v0.7.12.0: real object-graph scenarios for HeadlessWhitelistService.EnforceAsync -- the one
// piece of business logic in the v0.7.10.0 whitelist release that had never actually executed
// (the live checks performed during that release only proved the GET/PUT config round-trip works,
// since testing the actual kick behavior needs a real online player, which no automated check in
// this environment can provide against a real Palworld server). This harness fakes the one thing
// that actually needs faking (IPlayerModerationProvider) and runs EnforceAsync for real.

var tempRoot = Path.Combine(Path.GetTempPath(), "MystTiq-WhitelistHarness-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);
var failures = new List<string>();

try
{
    RunScenario("disabled config takes no action", () =>
    {
        var (whitelist, fake) = Build(tempRoot, "scenario1");
        var players = Snapshot(("bad-actor", "Bad Actor"));
        whitelist.EnforceAsync(players, CancellationToken.None).GetAwaiter().GetResult();
        Assert(fake.Calls.Count == 0, "disabled whitelist must not call ExecuteAsync at all");
    }, failures);

    RunScenario("enabled config kicks a non-whitelisted online player, leaves an allowed one alone", () =>
    {
        var (whitelist, fake) = Build(tempRoot, "scenario2");
        whitelist.SaveConfig(new WhitelistConfig(true, [new WhitelistEntry("good-player", "Allowed")]));
        var players = Snapshot(("good-player", "Good Player"), ("bad-actor", "Bad Actor"));
        whitelist.EnforceAsync(players, CancellationToken.None).GetAwaiter().GetResult();
        Assert(fake.Calls.Count == 1, $"expected exactly 1 kick, got {fake.Calls.Count}");
        Assert(fake.Calls[0] == ("kick", "bad-actor"), $"expected kick(bad-actor), got {Describe(fake.Calls)}");
    }, failures);

    RunScenario("the same still-online non-whitelisted player is not re-kicked on a second poll", () =>
    {
        var (whitelist, fake) = Build(tempRoot, "scenario3");
        whitelist.SaveConfig(new WhitelistConfig(true, []));
        var players = Snapshot(("bad-actor", "Bad Actor"));
        whitelist.EnforceAsync(players, CancellationToken.None).GetAwaiter().GetResult();
        whitelist.EnforceAsync(players, CancellationToken.None).GetAwaiter().GetResult();
        Assert(fake.Calls.Count == 1, $"expected exactly 1 kick across two polls of the same still-online player, got {fake.Calls.Count}");
    }, failures);

    RunScenario("a player is re-kicked after leaving and rejoining (dedup resets per session)", () =>
    {
        var (whitelist, fake) = Build(tempRoot, "scenario4");
        whitelist.SaveConfig(new WhitelistConfig(true, []));
        var online = Snapshot(("bad-actor", "Bad Actor"));
        var empty = Snapshot();
        whitelist.EnforceAsync(online, CancellationToken.None).GetAwaiter().GetResult();
        whitelist.EnforceAsync(empty, CancellationToken.None).GetAwaiter().GetResult();
        whitelist.EnforceAsync(online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(fake.Calls.Count == 2, $"expected 2 kicks (once per join), got {fake.Calls.Count}");
    }, failures);

    RunScenario("saving a new config resets the dedup set (re-evaluates everyone fresh)", () =>
    {
        var (whitelist, fake) = Build(tempRoot, "scenario5");
        whitelist.SaveConfig(new WhitelistConfig(true, []));
        var players = Snapshot(("bad-actor", "Bad Actor"));
        whitelist.EnforceAsync(players, CancellationToken.None).GetAwaiter().GetResult();
        whitelist.SaveConfig(new WhitelistConfig(true, []));
        whitelist.EnforceAsync(players, CancellationToken.None).GetAwaiter().GetResult();
        Assert(fake.Calls.Count == 2, $"expected 2 kicks (dedup reset by SaveConfig), got {fake.Calls.Count}");
    }, failures);

    RunScenario("an unavailable players snapshot is ignored entirely", () =>
    {
        var (whitelist, fake) = Build(tempRoot, "scenario6");
        whitelist.SaveConfig(new WhitelistConfig(true, []));
        var unavailable = new HeadlessPlayersSnapshot(false, 0, [], DateTimeOffset.UtcNow, "unavailable");
        whitelist.EnforceAsync(unavailable, CancellationToken.None).GetAwaiter().GetResult();
        Assert(fake.Calls.Count == 0, "an unavailable snapshot must not be enforced against");
    }, failures);

    // v0.7.64.0: HeadlessAutomationService.ValidateTriggerAndAction -- CreateRule/UpdateRule used
    // to accept any int for IdleThresholdMinutes/JitterSeconds/Interval/
    // WarningCountdownSecondsBeforeAction and store it verbatim, even though ComputeNextDue and the
    // idle-tick path silently reinterpret negative/zero values (Math.Max(1, ...), a fallback of 1
    // hour) rather than honoring them -- the API's own returned value then lied about what was
    // actually enforced. Pure validation logic, no service graph needed.
    RunScenario("ValidateTriggerAndAction rejects a negative IdleThresholdMinutes on an IdleEmpty trigger", () =>
    {
        var trigger = new AutomationTrigger { Kind = AutomationTriggerKind.IdleEmpty, IdleThresholdMinutes = -5 };
        var action = new AutomationAction { Kind = AutomationActionKind.StopServer };
        AssertThrows<ArgumentException>(() => HeadlessAutomationService.ValidateTriggerAndAction(trigger, action),
            "a negative IdleThresholdMinutes must be rejected, not silently clamped later");
    }, failures);

    RunScenario("ValidateTriggerAndAction rejects a zero IdleThresholdMinutes on an IdleEmpty trigger", () =>
    {
        var trigger = new AutomationTrigger { Kind = AutomationTriggerKind.IdleEmpty, IdleThresholdMinutes = 0 };
        var action = new AutomationAction { Kind = AutomationActionKind.StopServer };
        AssertThrows<ArgumentException>(() => HeadlessAutomationService.ValidateTriggerAndAction(trigger, action),
            "a zero IdleThresholdMinutes must be rejected");
    }, failures);

    RunScenario("ValidateTriggerAndAction accepts a valid IdleEmpty trigger", () =>
    {
        var trigger = new AutomationTrigger { Kind = AutomationTriggerKind.IdleEmpty, IdleThresholdMinutes = 30 };
        var action = new AutomationAction { Kind = AutomationActionKind.StopServer };
        HeadlessAutomationService.ValidateTriggerAndAction(trigger, action); // must not throw
    }, failures);

    RunScenario("ValidateTriggerAndAction accepts a null IdleThresholdMinutes (defaults apply later)", () =>
    {
        var trigger = new AutomationTrigger { Kind = AutomationTriggerKind.IdleEmpty, IdleThresholdMinutes = null };
        var action = new AutomationAction { Kind = AutomationActionKind.StopServer };
        HeadlessAutomationService.ValidateTriggerAndAction(trigger, action); // must not throw
    }, failures);

    RunScenario("ValidateTriggerAndAction rejects negative JitterSeconds", () =>
    {
        var trigger = new AutomationTrigger { Kind = AutomationTriggerKind.DailyTime, JitterSeconds = -1 };
        var action = new AutomationAction { Kind = AutomationActionKind.CreateBackup };
        AssertThrows<ArgumentException>(() => HeadlessAutomationService.ValidateTriggerAndAction(trigger, action),
            "negative JitterSeconds must be rejected");
    }, failures);

    RunScenario("ValidateTriggerAndAction rejects a non-positive Interval on an Interval trigger", () =>
    {
        var trigger = new AutomationTrigger { Kind = AutomationTriggerKind.Interval, Interval = TimeSpan.Zero };
        var action = new AutomationAction { Kind = AutomationActionKind.CreateBackup };
        AssertThrows<ArgumentException>(() => HeadlessAutomationService.ValidateTriggerAndAction(trigger, action),
            "a zero/negative Interval must be rejected, not silently replaced with a 1-hour fallback");
    }, failures);

    RunScenario("ValidateTriggerAndAction rejects a negative entry in WarningCountdownSecondsBeforeAction", () =>
    {
        var trigger = new AutomationTrigger { Kind = AutomationTriggerKind.DailyTime };
        var action = new AutomationAction { Kind = AutomationActionKind.StopServer, WarningCountdownSecondsBeforeAction = [30, -10, 5] };
        AssertThrows<ArgumentException>(() => HeadlessAutomationService.ValidateTriggerAndAction(trigger, action),
            "a negative warning-countdown offset must be rejected");
    }, failures);

    RunScenario("ValidateTriggerAndAction accepts a fully-populated valid rule", () =>
    {
        var trigger = new AutomationTrigger { Kind = AutomationTriggerKind.DailyTime, JitterSeconds = 30 };
        var action = new AutomationAction { Kind = AutomationActionKind.StopServer, WarningCountdownSecondsBeforeAction = [60, 30, 0] };
        HeadlessAutomationService.ValidateTriggerAndAction(trigger, action); // must not throw
    }, failures);

    // v0.7.64.0: ConsoleLogRotation -- MystTiq-PalServer-Console.log previously had no size cap on
    // either of its two writers (HeadlessConsoleLogWriter, WindowsServerLifecycleService), growing
    // forever on a long-running production service.
    RunScenario("ConsoleLogRotation leaves an under-threshold file untouched", () =>
    {
        var dir = Path.Combine(tempRoot, "rotation1");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "MystTiq-PalServer-Console.log");
        File.WriteAllText(path, "small content");
        ConsoleLogRotation.RotateIfNeeded(path);
        Assert(File.Exists(path), "the original file must still exist");
        Assert(File.ReadAllText(path) == "small content", "an under-threshold file must be left byte-for-byte alone");
        Assert(!File.Exists(Path.Combine(dir, "MystTiq-PalServer-Console.1.log")), "no rotated file should be created below the threshold");
    }, failures);

    RunScenario("ConsoleLogRotation rotates a file at/over MaxBytes, preserving old content and starting fresh", () =>
    {
        var dir = Path.Combine(tempRoot, "rotation2");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "MystTiq-PalServer-Console.log");
        var rotatedPath = Path.Combine(dir, "MystTiq-PalServer-Console.1.log");
        File.WriteAllText(path, new string('x', (int)ConsoleLogRotation.MaxBytes));

        ConsoleLogRotation.RotateIfNeeded(path);

        Assert(!File.Exists(path), "the oversized file must have been moved away, not left in place");
        Assert(File.Exists(rotatedPath), "the oversized content must be preserved as the .1 generation");
        Assert(File.ReadAllText(rotatedPath).Length == (int)ConsoleLogRotation.MaxBytes, "rotated content must be preserved exactly, not truncated");

        // Simulate the very next append after rotation -- exactly what both real writers do.
        File.AppendAllText(path, "fresh line" + Environment.NewLine);
        Assert(File.Exists(path) && File.ReadAllText(path) == "fresh line" + Environment.NewLine,
            "the next append after rotation must start a small, fresh file");
    }, failures);

    RunScenario("ConsoleLogRotation overwrites a stale prior .1 generation rather than accumulating", () =>
    {
        var dir = Path.Combine(tempRoot, "rotation3");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "MystTiq-PalServer-Console.log");
        var rotatedPath = Path.Combine(dir, "MystTiq-PalServer-Console.1.log");
        File.WriteAllText(rotatedPath, "stale generation from last time");
        File.WriteAllText(path, new string('y', (int)ConsoleLogRotation.MaxBytes));

        ConsoleLogRotation.RotateIfNeeded(path);

        Assert(File.ReadAllText(rotatedPath) == new string('y', (int)ConsoleLogRotation.MaxBytes),
            "rotation must overwrite the stale .1 generation with the just-rotated content, not fail or accumulate a .2");
    }, failures);

    // v0.7.68.0: WindowsServerLifecycleService/LinuxServerLifecycleService.StopAsync now try RCON's
    // native Shutdown command before falling back to CloseMainWindow (Windows) / SIGTERM (Linux) --
    // added after finding CloseMainWindow silently does nothing once MystTiq's own
    // ApplyPostLaunchWindowPolicyAsync hides PalServer's window (which it does on every real
    // launch), explaining a previously-disclosed, never-diagnosed observation ("graceful shutdown
    // fell back to forced termination on EVERY stop"). This scenario doesn't exercise StopAsync
    // itself (that needs a real found "PalServer" process, out of scope for this harness), but does
    // exercise the exact real wire call the fix makes -- PalworldRconService.ExecuteAsync sending a
    // "Shutdown 1 ..." command over the real Source RCON protocol -- against a minimal stub server
    // that speaks just enough of that protocol to authenticate and echo back what command it
    // received, proving the client-side call the fix relies on actually reaches a real listener
    // with the right command text, not just that the C# compiles.
    RunScenarioAsync("PalworldRconService.ExecuteAsync sends a real Shutdown command over the wire and reads the response", async () =>
    {
        using var stub = new StubRconServer("test-password", "World save complete. Shutting down.");
        var configDir = Path.Combine(tempRoot, "rcon-stop-scenario");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "PalWorldSettings.ini"),
            "[/Script/Pal.PalGameWorldSettings]\n" +
            $"OptionSettings=(RCONEnabled=True,RCONPort={stub.Port},AdminPassword=\"test-password\")\n");

        var rconService = new PalworldRconService(new PalworldSettingsConfigurationService(new HarnessPathProfile(configDir)));
        var status = rconService.GetStatus();
        Assert(status.Enabled && status.PasswordConfigured, "GetStatus must report RCON enabled with a configured password from the ini the fix reads");

        var result = await rconService.ExecuteAsync("Shutdown 1 MystTiq requested a graceful shutdown.");
        Assert(result.Success, $"ExecuteAsync must succeed against a real (stub) RCON server, got: {result.Message}");
        Assert(stub.ReceivedCommand == "Shutdown 1 MystTiq requested a graceful shutdown.",
            $"the stub server must have received exactly the Shutdown command the fix sends, got: '{stub.ReceivedCommand}'");
        Assert(result.Response == "World save complete. Shutting down.",
            $"ExecuteAsync must return the server's real response body, got: '{result.Response}'");
    }, failures);

    // v0.7.69.0: WindowsServerLifecycleService.StopAsync's "nothing running" branch previously
    // never wrote to the state store at all -- so a previously-persisted Crashed state (written by
    // GetStatusAsync once a managed process disappears unexpectedly) stayed stuck forever, since
    // GetStatusAsync's own crash-detection re-check requires Phase is Running or Starting to fire
    // again, which is false once it's already Crashed. LinuxServerLifecycleService's equivalent
    // branch already wrote a fresh Stopped/StopRequested state here; this scenario proves Windows's
    // branch now does the same, using a real WindowsServerLifecycleService (not a stand-in) with a
    // fake IServerSessionInspector reporting no processes found, exactly the "PalServer already
    // gone" condition that triggers the bug.
    RunScenarioAsync("WindowsServerLifecycleService.StopAsync clears a stale Crashed state instead of leaving it stuck", async () =>
    {
        var scratchRoot = Path.Combine(tempRoot, "windows-stop-crash-clear");
        Directory.CreateDirectory(scratchRoot);
        var stateStore = new ServerLifecycleStateStore(scratchRoot);
        stateStore.Write(new PersistedServerLifecycleState(
            ServerLifecyclePhase.Crashed, 4242, DateTimeOffset.UtcNow.AddMinutes(-5), false,
            "Previously managed PalServer process is no longer present without a requested stop."));
        Assert(stateStore.Read()!.Phase == ServerLifecyclePhase.Crashed, "scenario setup: state must start Crashed");

        var lifecycle = new WindowsServerLifecycleService(
            ServerPlatformProfile.Windows,
            new HarnessPathProfile(scratchRoot),
            new EmptySessionInspector(),
            stateStore);

        var result = await lifecycle.StopAsync(TimeSpan.FromSeconds(1));

        Assert(result.ExitCode == HeadlessExitCode.NotRunning, $"expected NotRunning, got {result.ExitCode}");
        Assert(result.Snapshot.Phase == ServerLifecyclePhase.Stopped, $"the returned snapshot must report Stopped, not the stale Crashed, got {result.Snapshot.Phase}");
        Assert(!result.Snapshot.CrashDetected, "the returned snapshot must not still claim CrashDetected");

        var persisted = stateStore.Read();
        Assert(persisted is not null && persisted.Phase == ServerLifecyclePhase.Stopped,
            $"the state actually written to disk must now be Stopped, not left as stale Crashed, got {persisted?.Phase}");
        Assert(persisted!.StopRequested, "the persisted state must record that a stop was explicitly requested/acknowledged");
    }, failures);

    RunScenarioAsync("PalworldRconService.ExecuteAsync reports failure honestly when the RCON password is wrong", async () =>
    {
        using var stub = new StubRconServer("real-password", "unused");
        var configDir = Path.Combine(tempRoot, "rcon-stop-scenario-badpass");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "PalWorldSettings.ini"),
            "[/Script/Pal.PalGameWorldSettings]\n" +
            $"OptionSettings=(RCONEnabled=True,RCONPort={stub.Port},AdminPassword=\"wrong-password\")\n");

        var rconService = new PalworldRconService(new PalworldSettingsConfigurationService(new HarnessPathProfile(configDir)));
        var result = await rconService.ExecuteAsync("Shutdown 1 test");
        Assert(!result.Success, "ExecuteAsync must report failure when the configured password is rejected, not silently succeed");
    }, failures);

    // v0.7.70.0: HeadlessNotificationRoutingService's Email channel used to log "not implemented"
    // and drop the notification. Now dispatches via SmtpClient. A minimal, real SMTP server stub
    // (just enough of RFC 5321 -- greeting, EHLO, AUTH LOGIN, MAIL FROM, RCPT TO, DATA) proves the
    // real Dispatch(...) call actually sends a correctly-addressed, correctly-authenticated email
    // over the real wire protocol, not just that the surrounding C# compiles.
    RunScenarioAsync("HeadlessNotificationRoutingService dispatches a real email over SMTP with correct envelope, auth and content", async () =>
    {
        using var smtp = new StubSmtpServer("mystiq-notifier", "s3cret");
        var scenarioRoot = Path.Combine(tempRoot, "email-dispatch-scenario");
        Directory.CreateDirectory(scenarioRoot);
        var activityLog = new HeadlessActivityLogService(new HarnessPathProfile(scenarioRoot));
        var routing = new HeadlessNotificationRoutingService(new HarnessPathProfile(scenarioRoot), activityLog);
        routing.SaveChannels(new NotificationChannelConfiguration
        {
            Channels =
            [
                new(NotificationChannel.Desktop, true, null),
                new(NotificationChannel.Webhook, false, null),
                new(NotificationChannel.Discord, false, null),
                new(NotificationChannel.Email, true, null,
                    SmtpHost: "127.0.0.1", SmtpPort: smtp.Port, SmtpUseSsl: false,
                    SmtpUsername: "mystiq-notifier", SmtpPassword: "s3cret",
                    EmailFrom: "mysttiq@example.test", EmailTo: "admin@example.test")
            ]
        });

        routing.Dispatch("Critical", "PalServer crashed", "The managed PalServer process exited unexpectedly.");

        var received = await smtp.WaitForMessageAsync(TimeSpan.FromSeconds(15));
        Assert(received is not null, "the stub SMTP server must have received a real message within the timeout");
        Assert(received!.AuthenticatedUsername == "mystiq-notifier", $"expected AUTH LOGIN with the configured username, got '{received.AuthenticatedUsername}'");
        Assert(received.AuthenticatedPassword == "s3cret", "expected AUTH LOGIN with the configured password");
        Assert(received.MailFrom.Contains("mysttiq@example.test", StringComparison.OrdinalIgnoreCase), $"expected MAIL FROM to include the configured sender, got '{received.MailFrom}'");
        Assert(received.RcptTo.Contains("admin@example.test", StringComparison.OrdinalIgnoreCase), $"expected RCPT TO to include the configured recipient, got '{received.RcptTo}'");
        Assert(received.Subject.Contains("PalServer crashed", StringComparison.Ordinal), $"expected the subject to include the notification title, got '{received.Subject}'");
        Assert(received.Body.Contains("exited unexpectedly", StringComparison.Ordinal), $"expected the body to include the notification message, got '{received.Body}'");
    }, failures);

    // v0.7.93.0: Nexus Mods catalog helpers (pure parsing/validation, no network).
    RunScenario("NexusModsLinks parses a well-formed Palworld nxm link", () =>
    {
        var ok = NexusModsLinks.TryParseNxm("nxm://palworld/mods/3329/files/12345?key=AbC-1_x&expires=1893456000&user_id=99", out var link, out var error);
        Assert(ok && link is not null, $"expected a parse, got error '{error}'");
        Assert(link!.ModId == 3329 && link.FileId == 12345, $"expected mod 3329 / file 12345, got {link.ModId}/{link.FileId}");
        Assert(link.Key == "AbC-1_x" && link.Expires == 1893456000, "expected key and expiry to be carried through");
    }, failures);
    RunScenario("NexusModsLinks rejects nxm links for another game, missing key/expiry, wrong shape, or a non-nxm scheme", () =>
    {
        Assert(!NexusModsLinks.TryParseNxm("nxm://skyrimspecialedition/mods/1/files/2?key=a&expires=1", out _, out _), "another game's link must be rejected");
        Assert(!NexusModsLinks.TryParseNxm("nxm://palworld/mods/3329/files/12345", out _, out _), "a link with no key/expiry must be rejected");
        Assert(!NexusModsLinks.TryParseNxm("nxm://palworld/mods/abc/files/12345?key=a&expires=1", out _, out _), "a non-numeric mod id must be rejected");
        Assert(!NexusModsLinks.TryParseNxm("nxm://palworld/mods/1/collections/2?key=a&expires=1", out _, out _), "a wrong path shape must be rejected");
        Assert(!NexusModsLinks.TryParseNxm("https://palworld/mods/1/files/2?key=a&expires=1", out _, out _), "a non-nxm scheme must be rejected");
        Assert(!NexusModsLinks.TryParseNxm("   ", out _, out _), "blank input must be rejected");
    }, failures);
    RunScenario("NexusModsLinks reads a mod id from a bare number or a Palworld mod page URL only", () =>
    {
        Assert(NexusModsLinks.TryParseModReference("3329", out var a) && a == 3329, "a bare id must parse");
        Assert(NexusModsLinks.TryParseModReference("https://www.nexusmods.com/palworld/mods/3329?tab=files", out var b) && b == 3329, "a page URL with a query must parse");
        Assert(!NexusModsLinks.TryParseModReference("https://www.nexusmods.com/skyrim/mods/3329", out _), "another game's page must be rejected");
        Assert(!NexusModsLinks.TryParseModReference("https://evil.example/palworld/mods/3329", out _), "a non-Nexus host must be rejected");
        Assert(!NexusModsLinks.TryParseModReference("0", out _) && !NexusModsLinks.TryParseModReference("-5", out _), "non-positive ids must be rejected");
    }, failures);
    RunScenario("NexusModsLinks only allows https downloads from Nexus-owned hosts", () =>
    {
        Assert(NexusModsLinks.IsAllowedDownloadHost(new Uri("https://cf-files.nexusmods.com/cdn/x.zip")), "a nexusmods.com subdomain must be allowed");
        Assert(NexusModsLinks.IsAllowedDownloadHost(new Uri("https://supporter-files.nexus-cdn.com/x.zip")), "a nexus-cdn.com subdomain must be allowed");
        Assert(!NexusModsLinks.IsAllowedDownloadHost(new Uri("http://cf-files.nexusmods.com/x.zip")), "plain http must be refused");
        Assert(!NexusModsLinks.IsAllowedDownloadHost(new Uri("https://nexusmods.com.evil.example/x.zip")), "a lookalike host must be refused");
        Assert(!NexusModsLinks.IsAllowedDownloadHost(new Uri("https://evilnexusmods.com/x.zip")), "a host merely ending in the name must be refused");
        Assert(!NexusModsLinks.IsAllowedDownloadHost(null), "null must be refused");
    }, failures);
    RunScenario("NexusModsLinks recognizes zip, 7z and rar archive headers", () =>
    {
        Assert(NexusModsLinks.DescribeArchiveKind(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0, 0 }) == "zip", "PK\\x03\\x04 is a zip");
        Assert(NexusModsLinks.DescribeArchiveKind(new byte[] { 0x50, 0x4B, 0x05, 0x06, 0, 0 }) == "zip", "an empty zip is still a zip");
        Assert(NexusModsLinks.DescribeArchiveKind(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }) == "7z", "7z magic");
        Assert(NexusModsLinks.DescribeArchiveKind(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 }) == "rar", "rar magic");
        Assert(NexusModsLinks.DescribeArchiveKind(new byte[] { 1, 2, 3 }) == "unknown", "anything else is unknown");
    }, failures);

    // v0.7.94.0: starter kits.
    static KitDefinition Kit(string id, params KitEntry[] entries) => new(id, "Starter", entries);
    static KitConfig Cfg(bool auto, string kitId, params KitDefinition[] kits) => new(auto, kitId, null, kits);

    RunScenario("Kit validation accepts a good kit and rejects bad ids, amounts, levels, types, duplicates, empties and a missing auto-gift kit", () =>
    {
        Assert(HeadlessKitService.Validate(Cfg(true, "a", Kit("a", new KitEntry("Item", "PalSphere", 10), new KitEntry("Pal", "WeaselDragon", 5)))).Count == 0, "a good kit must validate");
        Assert(HeadlessKitService.Validate(Cfg(false, "", Kit("a", new KitEntry("Item", "Bad Id", 1)))).Count > 0, "a space in an id must be rejected");
        Assert(HeadlessKitService.Validate(Cfg(false, "", Kit("a", new KitEntry("Item", "X;drop", 1)))).Count > 0, "punctuation in an id must be rejected");
        Assert(HeadlessKitService.Validate(Cfg(false, "", Kit("a", new KitEntry("Item", "X", 0)))).Count > 0, "a zero amount must be rejected");
        Assert(HeadlessKitService.Validate(Cfg(false, "", Kit("a", new KitEntry("Item", "X", 1_000_001)))).Count > 0, "an oversized amount must be rejected");
        Assert(HeadlessKitService.Validate(Cfg(false, "", Kit("a", new KitEntry("Pal", "X", 101)))).Count > 0, "a Pal level over 100 must be rejected");
        Assert(HeadlessKitService.Validate(Cfg(false, "", Kit("a", new KitEntry("Egg", "X", 1)))).Count > 0, "an unknown entry type must be rejected");
        Assert(HeadlessKitService.Validate(Cfg(false, "", Kit("a"))).Count > 0, "an empty kit must be rejected");
        Assert(HeadlessKitService.Validate(Cfg(false, "", Kit("a", new KitEntry("Item", "X", 1)), Kit("A", new KitEntry("Item", "Y", 1)))).Count > 0, "duplicate kit ids must be rejected");
        Assert(HeadlessKitService.Validate(Cfg(true, "missing", Kit("a", new KitEntry("Item", "X", 1)))).Count > 0, "auto-gift pointing at no kit must be rejected");
    }, failures);
    RunScenario("Kit commands group items 20 per giveitems, give each Pal separately, and refuse an unsafe UserId", () =>
    {
        var items = Enumerable.Range(1, 25).Select(i => new KitEntry("Item", $"Item{i}", i)).Append(new KitEntry("Pal", "WeaselDragon", 7)).ToArray();
        var commands = HeadlessKitService.BuildCommands(Kit("a", items), "steam_76561198000000001");
        Assert(commands.Count == 3, $"expected 2 giveitems + 1 givepal, got {commands.Count}");
        Assert(commands[0].StartsWith("giveitems steam_76561198000000001 Item1:1 Item2:2"), $"unexpected first command '{commands[0]}'");
        Assert(commands[1].Split(' ').Length == 2 + 5, "the second giveitems must carry the remaining 5 items");
        Assert(commands[2] == "givepal steam_76561198000000001 WeaselDragon 7", $"unexpected givepal '{commands[2]}'");
        Assert(HeadlessKitService.BuildCommands(Kit("a", items), "bad id\nshutdown").Count == 0, "a UserId with whitespace or a newline must produce no commands");
        Assert(HeadlessKitService.BuildCommands(Kit("a", items), "").Count == 0, "a blank UserId must produce no commands");
    }, failures);
    RunScenario("Kit reply text that reads as an error counts as a failed delivery", () =>
    {
        Assert(HeadlessKitService.LooksLikeFailure("Unknown command: giveitems"), "'Unknown command' is a failure");
        Assert(HeadlessKitService.LooksLikeFailure("Error: item not found"), "'Error' is a failure");
        Assert(!HeadlessKitService.LooksLikeFailure(""), "an empty reply is not a failure");
        Assert(!HeadlessKitService.LooksLikeFailure("Gave 10 x PalSphere to Player"), "a normal reply is not a failure");
    }, failures);

    // v0.7.95.0: Discord bot live features (the pure half; the gateway itself needs a real bot token).
    static HeadlessPlayerSnapshot P(string id, string name) => new(name, id, id, id, "", "", "Steam", "1", "0");
    RunScenario("Discord status text reflects state, lists and escapes players, truncates long lists, and ignores time in its change key", () =>
    {
        var online = DiscordBotFormatting.BuildStatus(ServerLifecyclePhase.Running, true, true, ["Ann", "Bo_b*"]);
        Assert(online.StateKey == "Online" && online.Headline.Contains("online"), "running + ready is Online");
        Assert(online.Body.Contains("2 players online") && online.Body.Contains("Bo\\_b\\*"), $"names must be listed and escaped: '{online.Body}'");
        Assert(DiscordBotFormatting.BuildStatus(ServerLifecyclePhase.Running, false, true, []).StateKey == "Starting", "running without the port confirmed is Starting");
        Assert(DiscordBotFormatting.BuildStatus(ServerLifecyclePhase.Stopped, false, false, []).StateKey == "Offline", "stopped is Offline");
        Assert(DiscordBotFormatting.BuildStatus(ServerLifecyclePhase.Crashed, false, false, []).StateKey == "Crashed", "crashed is Crashed");
        Assert(DiscordBotFormatting.BuildStatus(ServerLifecyclePhase.Running, true, false, []).Body.Contains("unavailable"), "an unavailable player list must say so, not claim zero");
        var many = DiscordBotFormatting.BuildStatus(ServerLifecyclePhase.Running, true, true, Enumerable.Range(1, 25).Select(i => "P" + i).ToList());
        Assert(many.Body.Contains("25 players online") && many.Body.Contains("and 5 more") && !many.Body.Contains("P21"), $"a long list must be truncated with a remainder: '{many.Body}'");
        var again = DiscordBotFormatting.BuildStatus(ServerLifecyclePhase.Running, true, true, ["Ann", "Bo_b*"]);
        Assert(again.ChangeKey == online.ChangeKey, "an identical status must have an identical change key");
        Assert(DiscordBotFormatting.BuildStatus(ServerLifecyclePhase.Running, true, true, ["Ann"]).ChangeKey != online.ChangeKey, "a different player list must change the key");
    }, failures);
    RunScenario("Discord presence text and lifecycle transitions announce real changes only", () =>
    {
        Assert(DiscordBotFormatting.PresenceText("Online", 1, true) == "1 player online", "singular");
        Assert(DiscordBotFormatting.PresenceText("Online", 3, true) == "3 players online", "plural");
        Assert(DiscordBotFormatting.PresenceText("Offline", 0, false) == "Server offline", "offline");
        Assert(DiscordBotFormatting.DescribeTransition(null, "Online") is null, "the first observation is a baseline, not an announcement");
        Assert(DiscordBotFormatting.DescribeTransition("Online", "Online") is null, "no change, no announcement");
        Assert(DiscordBotFormatting.DescribeTransition("Starting", "Online")!.Contains("online"), "Starting to Online is announced");
        Assert(DiscordBotFormatting.DescribeTransition("Online", "Offline")!.Contains("stopped"), "Online to Offline is announced as stopped");
        Assert(DiscordBotFormatting.DescribeTransition("Online", "Crashed")!.Contains("crashed"), "a crash is announced");
        Assert(DiscordBotFormatting.DescribeTransition("Online", "Stopping") is null, "Stopping alone is not announced");
        Assert(DiscordBotFormatting.DescribeTransition("Offline", "Unknown") is null, "Unknown is never announced");
    }, failures);
    RunScenario("Discord join/leave diff baselines silently, then reports joins and leaves, and skips players with no id", () =>
    {
        var first = DiscordBotFormatting.DiffPresence(null, [P("a", "Ann"), P("b", "Bo")]);
        Assert(first.Joined.Count == 0 && first.Left.Count == 0 && first.Current.Count == 2, "the first observation must announce nothing");
        var second = DiscordBotFormatting.DiffPresence(first.Current, [P("b", "Bo"), P("c", "Cy")]);
        Assert(second.Joined.SequenceEqual(["Cy"]) && second.Left.SequenceEqual(["Ann"]), $"joined={string.Join(',', second.Joined)} left={string.Join(',', second.Left)}");
        var third = DiscordBotFormatting.DiffPresence(second.Current, [P("b", "Bo"), P("c", "Cy")]);
        Assert(third.Joined.Count == 0 && third.Left.Count == 0, "no change must announce nothing");
        var twins = DiscordBotFormatting.DiffPresence(first.Current, [P("a", "Ann"), P("b", "Bo"), P("x", "Twin"), P("y", "Twin")]);
        Assert(twins.Joined.Count == 2, "two different players with the same name are still two joins");
        var blank = DiscordBotFormatting.DiffPresence(first.Current, [new HeadlessPlayerSnapshot("Ghost", "", "", "", "", "", "", "", "0")]);
        Assert(blank.Joined.Count == 0, "a player with no id at all cannot be tracked and is skipped");
        var lines = DiscordBotFormatting.PresenceLines(new PresenceDiff(["A*B"], ["C_D"], new Dictionary<string, string>()));
        Assert(lines.Count == 2 && lines[0].Contains("A\\*B") && lines[1].Contains("C\\_D"), "names in join/leave lines must be escaped");
        Assert(!DiscordBotFormatting.EscapeMarkdown("line1\nline2").Contains('\n'), "a newline in a name must not survive");
    }, failures);
    RunScenario("Discord autocomplete filters by name or id prefix, caps at 25, and tells same-named players apart", () =>
    {
        var players = new List<HeadlessPlayerSnapshot> { P("steam_1111", "Anna"), P("steam_2222", "Annabel"), P("steam_3333", "Bob") };
        var byName = DiscordBotFormatting.SuggestPlayers(players, "ann");
        Assert(byName.Count == 2 && byName[0].Value == "steam_1111", "a case-insensitive name substring must match");
        Assert(DiscordBotFormatting.SuggestPlayers(players, "steam_3").Single().Name == "Bob", "an id prefix must match");
        Assert(DiscordBotFormatting.SuggestPlayers(players, "").Count == 3, "no text lists everyone");
        Assert(DiscordBotFormatting.SuggestPlayers(Enumerable.Range(0, 60).Select(i => P($"id{i:D3}", "P" + i)).ToList(), "").Count == 25, "at most 25 suggestions");
        var twins = DiscordBotFormatting.SuggestPlayers([P("aaaa1111", "Twin"), P("bbbb2222", "Twin")], "");
        Assert(twins[0].Name != twins[1].Name && twins[0].Name.Contains("1111"), $"same-named players need distinct labels: '{twins[0].Name}' / '{twins[1].Name}'");
        Assert(DiscordBotFormatting.SuggestPlayers([P(new string('x', 120), "Long")], "").Count == 0, "an id over Discord's 100-char value limit cannot be offered");
    }, failures);
    RunScenario("Discord command roles: read-only Viewer, server control and save/backup Operator, moderation Admin, unknown Owner", () =>
    {
        Assert(DiscordBotFormatting.RequiredRole("mysttiq-status") == MystTiqRole.Viewer && DiscordBotFormatting.RequiredRole("mysttiq-players") == MystTiqRole.Viewer, "status/players are Viewer");
        foreach (var operatorCommand in new[] { "mysttiq-start", "mysttiq-stop", "mysttiq-restart", "mysttiq-broadcast", "mysttiq-save", "mysttiq-backup" })
            Assert(DiscordBotFormatting.RequiredRole(operatorCommand) == MystTiqRole.Operator, $"{operatorCommand} must be Operator");
        foreach (var adminCommand in new[] { "mysttiq-kick", "mysttiq-ban", "mysttiq-unban" })
            Assert(DiscordBotFormatting.RequiredRole(adminCommand) == MystTiqRole.Admin, $"{adminCommand} must be Admin");
        Assert(DiscordBotFormatting.RequiredRole("mysttiq-something-new") == MystTiqRole.Owner, "an unrecognised command must default to the strictest role");
        Assert(DiscordBotFormatting.SupportedCommands.All(c => DiscordBotFormatting.RequiredRole(c) != MystTiqRole.Owner), "every supported command must have an explicit role");
    }, failures);
    RunScenario("Discord channel ids must be real snowflakes, and a config saved before v0.7.95.0 still loads with sensible defaults", () =>
    {
        Assert(DiscordSnowflake.TryParse("123456789012345678", out var id) && id == 123456789012345678UL, "an 18-digit id is valid");
        Assert(!DiscordSnowflake.TryParse("12345", out _) && !DiscordSnowflake.TryParse("12345678901234567x", out _) && !DiscordSnowflake.TryParse("", out _) && !DiscordSnowflake.TryParse("000000000000000", out _), "short, non-numeric, blank and zero ids are refused");
        var good = DiscordBotConfiguration.Default with { StatusChannelId = "123456789012345678" };
        Assert(HeadlessDiscordBotService.ValidateChannelIds(good) is null, "a valid channel id is accepted");
        Assert(HeadlessDiscordBotService.ValidateChannelIds(good with { EventsChannelId = "nope" }) is not null, "a bad events channel id is refused with a reason");
        Assert(HeadlessDiscordBotService.ValidateChannelIds(DiscordBotConfiguration.Default) is null, "leaving both blank is fine");
        var old = System.Text.Json.JsonSerializer.Deserialize<DiscordBotConfiguration>("{\"Enabled\":true,\"BotToken\":\"t\",\"GuildId\":\"1\",\"OwnerDiscordUserId\":null,\"RoleMappings\":[]}");
        Assert(old is not null && old.Enabled && old.ShowPresence && old.StatusChannelId is null && old.StatusMessageId is null && old.EventsChannelId is null, "an old config must load with presence on and no channels");
    }, failures);

    RunScenario("Map conversion: a real world's first base lands on the game's documented start location, with north up and east right", () =>
    {
        // A live world's first base (from its decoded Level.sav.json) converts to in-game map units
        // (248, -495); the game's default start, the Plateau of Beginnings, is documented at (240, -513).
        var (mapX, mapY) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToMapUnits(-351088.734237908, 271805.199808705);
        Assert(mapX == 248 && mapY == -495, $"expected map units (248, -495), got ({mapX}, {mapY})");

        // The start plateau at in-game (240, -513) in world coordinates: x = mapY*459 - 123888, y = mapX*459 + 158000.
        var (startFx, startFy) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToImageFraction(-513 * 459 - 123888, 240 * 459 + 158000);
        Assert(Math.Abs(startFx - 0.6864) < 0.002 && Math.Abs(startFy - 0.4898) < 0.002, $"start plateau should sit at about (0.686, 0.490) of the image, got ({startFx:F4}, {startFy:F4})");
        var (baseFx, baseFy) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToImageFraction(-351088.734237908, 271805.199808705);
        Assert(Math.Abs(baseFx - startFx) < 0.01 && Math.Abs(baseFy - startFy) < 0.01, "the base by the start location must be drawn within 1% of the image of the start plateau");

        var (_, northFy) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToImageFraction(-100000, 100000);
        var (_, southFy) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToImageFraction(-300000, 100000);
        Assert(northFy < southFy, "a larger world X is further north, which must be drawn nearer the top of the image");
        var (westFx, _) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToImageFraction(-100000, 0);
        var (eastFx, _) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToImageFraction(-100000, 200000);
        Assert(westFx < eastFx, "a larger world Y is further east, which must be drawn nearer the right of the image");

        // Feybreak's documented extent (map X -1456..-608, Y -1743..-798) belongs in the south-west corner.
        var (feyFx, feyFy) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToImageFraction(-1743 * 459 - 123888, -1456 * 459 + 158000);
        Assert(feyFx is > 0.10 and < 0.20 && feyFy is > 0.80 and < 0.95, $"Feybreak's corner should be in the south-west of the image, got ({feyFx:F3}, {feyFy:F3})");
    }, failures);
    RunScenario("Map conversion: canvas positions scale with the canvas and never leave it", () =>
    {
        var (fx, fy) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToImageFraction(-351088.7, 271805.2);
        var (cx, cy) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToCanvasPosition(-351088.7, 271805.2, 480);
        Assert(Math.Abs(cx - fx * 480) < 1e-9 && Math.Abs(cy - fy * 480) < 1e-9, "a canvas position is the image fraction times the canvas size");
        foreach (var (wx, wy) in new[] { (-9_000_000d, -9_000_000d), (9_000_000d, 9_000_000d), (0d, 0d) })
        {
            var (px, py) = MystTiq.Desktop.Services.PalworldMapCoordinates.ToCanvasPosition(wx, wy, 480);
            Assert(px is >= 0 and <= 480 && py is >= 0 and <= 480, $"({wx}, {wy}) must be clamped onto the canvas, got ({px}, {py})");
        }
    }, failures);
    RunScenario("Map coordinates are described the way the game's own map shows them, with no negative zero", () =>
    {
        Assert(MystTiq.Desktop.Services.PalworldMapCoordinates.Describe(-351088.734237908, 271805.199808705) == "(248, -495)", "a real base reads (248, -495)");
        Assert(MystTiq.Desktop.Services.PalworldMapCoordinates.Describe(-123888, 158000) == "(0, 0)", "the map origin reads (0, 0)");
        Assert(MystTiq.Desktop.Services.PalworldMapCoordinates.Describe(-123888 - 100, 158000 - 100) == "(0, 0)", "a point just west and south of the origin rounds to (0, 0), not (-0, -0)");
        Assert(MystTiq.Desktop.Services.PalworldMapCoordinates.Describe(-1743 * 459 - 123888, -1456 * 459 + 158000) == "(-1456, -1743)", "Feybreak's documented corner round-trips");
    }, failures);
    RunScenario("Map status text says whether players and bases are drawn, so an empty map is never a mystery", () =>
    {
        var stopped = MystTiq.Desktop.Services.MapContentsText.Describe(0, 0, 2);
        Assert(stopped.Contains("stopped") && stopped.Contains("2 base(s)"), $"nobody online with bases: '{stopped}'");
        Assert(MystTiq.Desktop.Services.MapContentsText.Describe(3, 3, 0) == "3 player(s) online on the map.", "everyone located, no bases");
        var partial = MystTiq.Desktop.Services.MapContentsText.Describe(3, 2, 1);
        Assert(partial.Contains("2 player(s) online on the map") && partial.Contains("1 without a reported position") && partial.Contains("1 base(s)"), $"partial: '{partial}'");
        Assert(MystTiq.Desktop.Services.MapContentsText.Describe(2, 0, 0).Contains("no position"), "online but no positions yet");
        var withOffline = MystTiq.Desktop.Services.MapContentsText.Describe(0, 0, 2, offline: 5);
        Assert(withOffline.Contains("stopped") && withOffline.Contains("5 offline player(s) shown at their last known position") && withOffline.Contains("2 base(s)"), $"stopped server with offline players and bases: '{withOffline}'");
        Assert(!MystTiq.Desktop.Services.MapContentsText.Describe(1, 1, 0).Contains("offline"), "no offline mention when there are none");
    }, failures);
    RunScenario("Map viewport: wheel zoom keeps the point under the cursor fixed, and scale and offset stay in bounds", () =>
    {
        var v = new MystTiq.Desktop.Services.MapViewport(480);
        Assert(v.Scale == 1 && v.OffsetX == 0 && v.OffsetY == 0 && !v.IsZoomed, "starts unzoomed");
        var (cx, cy) = v.FromView(300, 200);
        v.ZoomByWheel(300, 200, 3);
        var (vx, vy) = v.ToView(cx, cy);
        Assert(v.IsZoomed && Math.Abs(vx - 300) < 1e-6 && Math.Abs(vy - 200) < 1e-6, $"the point under the cursor must not move, it went to ({vx:F3}, {vy:F3})");
        Assert(Math.Abs(v.Scale - Math.Pow(1.25, 3)) < 1e-9, "three notches multiply the scale by 1.25 three times");
        for (var i = 0; i < 60; i++) v.ZoomByWheel(10, 10, 1);
        Assert(v.Scale == MystTiq.Desktop.Services.MapViewport.MaxScale, "zoom in stops at the maximum");
        Assert(v.OffsetX <= 0 && v.OffsetX >= 480 * (1 - v.Scale) && v.OffsetY <= 0 && v.OffsetY >= 480 * (1 - v.Scale), "the map never slides out of frame");
        for (var i = 0; i < 80; i++) v.ZoomByWheel(240, 240, -1);
        Assert(v.Scale == 1 && v.OffsetX == 0 && v.OffsetY == 0 && !v.IsZoomed, "zooming all the way out returns to the exact unzoomed view");
    }, failures);
    RunScenario("Map viewport: panning is clamped, and centring on a marker puts it in the middle unless it is near an edge", () =>
    {
        var v = new MystTiq.Desktop.Services.MapViewport(480);
        v.PanBy(50, 50);
        Assert(v.OffsetX == 0 && v.OffsetY == 0, "an unzoomed map cannot be dragged");
        v.CenterOn(240, 240, 4);
        var (mx, my) = v.ToView(240, 240);
        Assert(v.Scale == 4 && Math.Abs(mx - 240) < 1e-9 && Math.Abs(my - 240) < 1e-9, $"a central marker lands at the centre, got ({mx:F2}, {my:F2})");
        v.Reset();
        v.CenterOn(331, 232, 4);
        var (bx, by) = v.ToView(331, 232);
        Assert(Math.Abs(bx - 240) < 1e-9 && Math.Abs(by - 240) < 1e-9, "a marker in the middle of the map is centred exactly");
        v.Reset();
        v.CenterOn(5, 470, 4);
        var (ex, ey) = v.ToView(5, 470);
        Assert(v.OffsetX == 0 && ex > 0 && ex < 240 && ey > 240 && ey <= 480, "a marker near a corner stays in frame instead of showing empty space");
        v.PanBy(-100000, -100000);
        Assert(v.OffsetX == 480 * (1 - 4) && v.OffsetY == 480 * (1 - 4), "panning stops at the far edge");
        v.CenterOn(240, 240, 2);
        Assert(v.Scale == 4, "clicking a marker never zooms out from a closer view");
        v.Reset();
        Assert(v.Scale == 1 && !v.IsZoomed, "reset returns to the whole map");
    }, failures);
    RunScenario("Map label layout: labels are placed as real text rectangles, so no two labels overlap and no label covers another dot", () =>
    {
        // v0.7.115.0: the check is the one that matters on screen: do the placed rectangles overlap.
        static void NoOverlaps(IReadOnlyList<MystTiq.Desktop.Services.MapLabelLayout.MapLabel> labels, string what)
        {
            var offsets = MystTiq.Desktop.Services.MapLabelLayout.ComputeLabelOffsets(labels);
            var boxes = labels.Select((l, i) => MystTiq.Desktop.Services.MapLabelLayout.LabelBox(l, offsets[i])).ToArray();
            for (var i = 0; i < boxes.Length; i++)
                for (var j = i + 1; j < boxes.Length; j++)
                    Assert(!boxes[i].Overlaps(boxes[j]), $"{what}: labels {i} and {j} overlap (offsets [{string.Join(", ", offsets)}])");
            for (var i = 0; i < boxes.Length; i++)
                for (var d = 0; d < labels.Count; d++)
                    if (d != i) Assert(!boxes[i].Overlaps(MystTiq.Desktop.Services.MapLabelLayout.DotBox(labels[d].X, labels[d].Y)), $"{what}: label {i} covers dot {d}");
        }
        MystTiq.Desktop.Services.MapLabelLayout.MapLabel L(double x, double y, string name) => new(x, y, MystTiq.Desktop.Services.MapLabelLayout.EstimateLabelWidth(name, 10));
        var step = MystTiq.Desktop.Services.MapLabelLayout.LabelStep;

        var far = MystTiq.Desktop.Services.MapLabelLayout.ComputeLabelOffsets([L(0, 0, "Alice"), L(200, 200, "Bob"), L(400, 10, "Carol")]);
        Assert(far.All(o => o == (0, 0)), "labels nowhere near each other stay right under their dot");

        // The two shapes the v0.7.106.0 distance heuristic got wrong (the deficiency report's reproduction):
        // a chain of markers 25px apart, where the 2nd and 3rd got the same offset, and a dot up-left of another
        // whose pushed-down label landed on the other's.
        NoOverlaps([L(100, 100, "Longname One"), L(125, 100, "Longname Two"), L(150, 100, "Longname Three")], "a chain 25px apart");
        NoOverlaps([L(100, 100, "Guild Of Many"), L(110, 87, "Wanderer"), L(95, 113, "Someone")], "offset diagonal neighbours");

        NoOverlaps([L(100, 100, "A1"), L(100, 100, "A2"), L(100, 100, "A3")], "a fully stacked cluster");
        // Labels stay next to their own marker when there is room nearby (below, right, above, left) instead of
        // drifting down a long column, which is what made names hard to match to dots on the live map.
        var chain = MystTiq.Desktop.Services.MapLabelLayout.ComputeLabelOffsets([L(100, 100, "Longname One"), L(125, 100, "Longname Two"), L(150, 100, "Longname Three")]);
        Assert(chain.All(o => Math.Abs(o.Y) <= 2 * step + 1 && Math.Abs(o.X) <= 100), $"a small chain keeps every label within two lines of its dot, got [{string.Join(", ", chain)}]");
        var five = Enumerable.Range(0, 5).Select(k => L(300 + (k % 3) * 3, 300 + k * 2, "Player" + k)).ToArray();
        NoOverlaps(five, "five rings bunched together");
        var fiveOffsets = MystTiq.Desktop.Services.MapLabelLayout.ComputeLabelOffsets(five);
        Assert(fiveOffsets.All(o => Math.Abs(o.Y) <= 4 * step), $"five bunched markers keep their labels close, got [{string.Join(", ", fiveOffsets)}]");

        // Side by side with enough room for the text: no push at all (the old 30px circle pushed these).
        var roomy = MystTiq.Desktop.Services.MapLabelLayout.ComputeLabelOffsets([L(0, 0, "Al"), L(28, 0, "Bo")]);
        Assert(roomy[1] == (0, 0), $"two short names with room between them are not moved, got {roomy[1]}");

        var rng = new Random(115);
        for (var trial = 0; trial < 40; trial++)
        {
            var cluster = Enumerable.Range(0, 7).Select(k => L(200 + rng.Next(-35, 36), 200 + rng.Next(-35, 36), new string('x', rng.Next(2, 14)))).ToArray();
            NoOverlaps(cluster, $"random cluster {trial}");
        }

        Assert(MystTiq.Desktop.Services.MapLabelLayout.ComputeLabelOffsets([]).Count == 0, "no markers is not an error");
        var pile = Enumerable.Range(0, 40).Select(_ => L(50, 50, "Same")).ToArray();
        var pileOffsets = MystTiq.Desktop.Services.MapLabelLayout.ComputeLabelOffsets(pile);
        Assert(pileOffsets.All(o => Math.Abs(o.Y) <= MystTiq.Desktop.Services.MapLabelLayout.MaximumSteps * step), "a pile too dense to separate stays within the step limit instead of growing without bound");
    }, failures);    RunScenario("Map marker spread: two dots at the exact same pixel fan out around it instead of rendering as one icon, and real separation is left untouched", () =>
    {
        var coincidentRadius = MystTiq.Desktop.Services.MapLabelLayout.CoincidentRadius;
        var spreadRadius = MystTiq.Desktop.Services.MapLabelLayout.SpreadRadius;

        var farApart = MystTiq.Desktop.Services.MapLabelLayout.ComputeMarkerOffsets([(0, 0), (200, 200), (400, 10)]);
        Assert(farApart.All(o => o.X == 0 && o.Y == 0), "markers with real separation are never nudged off their true position");

        var exactSamePixel = MystTiq.Desktop.Services.MapLabelLayout.ComputeMarkerOffsets([(50, 50), (50, 50)]);
        Assert(exactSamePixel[0] == (0, 0), "the first marker at a shared point stays exactly on the real spot");
        var (dx, dy) = exactSamePixel[1];
        var distance = Math.Sqrt(dx * dx + dy * dy);
        Assert(Math.Abs(distance - spreadRadius) < 1e-9 && (dx != 0 || dy != 0), $"the second marker fans out exactly SpreadRadius away, got distance {distance:F3} at ({dx:F3},{dy:F3})");

        // Three exactly-coincident dots: first undisturbed, the other two fan out to DIFFERENT points around
        // the ring (not on top of each other either), each still exactly SpreadRadius from the shared centre.
        var threeStacked = MystTiq.Desktop.Services.MapLabelLayout.ComputeMarkerOffsets([(10, 10), (10, 10), (10, 10)]);
        Assert(threeStacked[0] == (0, 0), "first stays put");
        Assert(threeStacked[1] != threeStacked[2], "the second and third fan out to different points on the ring, not the same spot as each other");
        foreach (var (ox, oy) in new[] { threeStacked[1], threeStacked[2] })
            Assert(Math.Abs(Math.Sqrt(ox * ox + oy * oy) - spreadRadius) < 1e-9, "every fanned-out marker sits exactly SpreadRadius from the shared point");

        // Exactly at the coincident radius still counts; just past it does not (much tighter than the label
        // collision radius -- this is about two icons on the same pixel, not text width).
        var atRadius = MystTiq.Desktop.Services.MapLabelLayout.ComputeMarkerOffsets([(0, 0), (coincidentRadius, 0)]);
        Assert(atRadius[1] != (0, 0), "exactly at the coincident radius still fans out");
        var justPast = MystTiq.Desktop.Services.MapLabelLayout.ComputeMarkerOffsets([(0, 0), (coincidentRadius + 0.5, 0)]);
        Assert(justPast[1] == (0, 0), "just past the coincident radius is left at its real position");
        Assert(coincidentRadius < MystTiq.Desktop.Services.MapLabelLayout.DotSize, "the coincident radius is tighter than a dot, so only same-pixel markers fan out");
    }, failures);
    RunScenario("Saved player positions: only player characters with a real last location are read, and ids match the player list", () =>
    {
        var json = """
        { "properties": { "worldSaveData": { "value": { "CharacterSaveParameterMap": { "value": [
          { "key": { "PlayerUId": { "value": "67d8d355-0000-0000-0000-000000000000" }, "InstanceId": { "value": "aaaa" } },
            "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
              "IsPlayer": { "value": true }, "NickName": { "value": "Wade" },
              "LastJumpedLocation": { "value": { "x": -350789.1, "y": 270390.7, "z": 7160.9 } } } } } } } } },
          { "key": { "PlayerUId": { "value": "00000000-0000-0000-0000-000000000000" }, "InstanceId": { "value": "bbbb" } },
            "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
              "LastJumpedLocation": { "value": { "x": -351336.2, "y": 271490.8, "z": -999999.0 } } } } } } } } },
          { "key": { "PlayerUId": { "value": "1467c601-0000-0000-0000-000000000000" }, "InstanceId": { "value": "cccc" } },
            "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
              "IsPlayer": { "value": true }, "NickName": { "value": "M3llyM" },
              "LastJumpedLocation": { "value": { "x": 0.0, "y": 0.0, "z": 7062.1 } } } } } } } } },
          { "key": { "PlayerUId": { "value": "84544311-0000-0000-0000-000000000000" }, "InstanceId": { "value": "dddd" } },
            "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
              "IsPlayer": { "value": true }, "NickName": { "value": "Melly" },
              "LastJumpedLocation": { "value": { "x": -351859.4, "y": 279472.2, "z": -1000000000.0 } } } } } } } } },
          { "key": { "PlayerUId": { "value": "6f7eeee6-0000-0000-0000-000000000000" }, "InstanceId": { "value": "eeee" } },
            "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
              "IsPlayer": { "value": true }, "NickName": { "value": "Spiral" },
              "LastJumpedLocation": { "value": { "x": "-352764.5", "y": "269927.3", "z": "7150.0" } } } } } } } } }
        ] } } } } }
        """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var found = SavePlayerLocationReader.Read(doc.RootElement);
        Assert(found.Count == 2, $"only Wade and Spiral have a real location, got {found.Count}: {string.Join(", ", found.Select(f => f.Name))}");
        var wade = found.Single(f => f.Name == "Wade");
        Assert(wade.PlayerId == "67D8D355000000000000000000000000", $"the id must be normalised like the player list's ids, got '{wade.PlayerId}'");
        Assert(Math.Abs(wade.X - -350789.1) < 1e-6 && Math.Abs(wade.Y - 270390.7) < 1e-6, "coordinates are read as world units");
        Assert(found.Any(f => f.Name == "Spiral" && Math.Abs(f.X - -352764.5) < 1e-6), "numbers stored as strings are accepted");
        Assert(!found.Any(f => f.Name is "M3llyM" or "Melly"), "0,0 and placeholder altitudes are ignored, as is a pal with no player id");
        using var empty = System.Text.Json.JsonDocument.Parse("{}");
        Assert(SavePlayerLocationReader.Read(empty.RootElement).Count == 0, "a save with no character map yields nothing, not an error");
    }, failures);

    // v0.8.11.0: owned Pals' positions. The fixture mirrors the real save's shape: containers keyed by {"ID":...} with a
    // null "id" beside it, Level nested two "value"s deep, alpha Pals saved as BOSS_<id>.
    RunScenario("Pal positions: base workers and party Pals are placed, Palbox Pals and unset positions are only counted", () =>
    {
        static string Pal(string instance, string species, int level, string container, double x, double y, double z, string owner = "00000000-0000-0000-0000-000000000000", string nick = "") => $$"""
          { "key": { "PlayerUId": { "value": "00000000-0000-0000-0000-000000000000" }, "InstanceId": { "value": "{{instance}}" } },
            "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": {
              "CharacterID": { "value": "{{species}}" }, "Level": { "value": { "type": "None", "value": {{level}} } },
              {{(nick.Length > 0 ? $"\"NickName\": {{ \"value\": \"{nick}\" }}," : "")}}
              "OwnerPlayerUId": { "value": "{{owner}}" },
              "SlotId": { "value": { "ContainerId": { "id": null, "value": { "ID": { "id": null, "value": "{{container}}" } } }, "SlotIndex": { "value": 0 } } },
              "LastJumpedLocation": { "value": { "x": {{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "y": {{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "z": {{z.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } } } } } } } } }
        """;
        const string worker = "86d4a7a0-4298-7ee5-1dbd-cdbff9f5f22d", party = "79a83df4-42e1-5120-c940-0fb782bfa00a", box = "744e8384-449e-0dd3-c5e3-7c98bd50b9bd";
        const string owner = "a3835c7b-0000-0000-0000-000000000000";
        var json = $$"""
        { "properties": { "worldSaveData": { "value": {
          "BaseCampSaveData": { "value": [ { "key": "d04f779e-4ef8-578b-7c84-12b759e117e1", "value": {
            "RawData": { "value": { "spawn_transform": { "translation": { "x": -351088.7, "y": 271805.2, "z": 7422.7 } } } },
            "WorkerDirector": { "value": { "RawData": { "value": { "id": "d04f779e-4ef8-578b-7c84-12b759e117e1", "container_id": "{{worker}}" } } } } } } ] },
          "CharacterContainerSaveData": { "value": [
            { "key": { "ID": { "value": "{{worker}}" } }, "value": { "SlotNum": { "value": 9 } } },
            { "key": { "ID": { "value": "{{party}}" } }, "value": { "SlotNum": { "value": 5 } } },
            { "key": { "ID": { "value": "{{box}}" } }, "value": { "SlotNum": { "value": 960 } } } ] },
          "CharacterSaveParameterMap": { "value": [
            { "key": { "PlayerUId": { "value": "{{owner}}" }, "InstanceId": { "value": "p1" } },
              "value": { "RawData": { "value": { "object": { "SaveParameter": { "value": { "IsPlayer": { "value": true }, "NickName": { "value": "Keeper" },
                "LastJumpedLocation": { "value": { "x": -351000.0, "y": 270000.0, "z": 7000.0 } } } } } } } } },
            {{Pal("aaaa0001", "PinkCat", 15, worker, -355668.1, 272597.2, 7100)}},
            {{Pal("aaaa0002", "Boar", 10, worker, 0, 0, 7004)}},
            {{Pal("aaaa0003", "BOSS_WeaselDragon", 12, party, -351444.0, 270714.0, 7050, owner, "Zippy")}},
            {{Pal("aaaa0004", "Plesiosaur", 7, party, -5.4, -58.1, 7004.8, owner)}},
            {{Pal("aaaa0005", "Kitsunebi", 13, party, -312395.0, 245043.0, 999999.0, owner)}},
            {{Pal("aaaa0006", "Garm", 20, box, -250000.0, 200000.0, 6000, owner)}},
            {{Pal("aaaa0007", "Sheepball", 3, "ffffffff-0000-0000-0000-000000000000", -300000.0, 250000.0, 6000)}}
          ] } } } } }
        """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var result = SavePalLocationReader.Read(doc.RootElement);
        Assert(result.TotalPals == 7, $"the player character is not a Pal; 7 Pals expected, got {result.TotalPals}");
        Assert(result.OnMap.Count == 2, $"only the placed worker and the placed party Pal are drawn, got {result.OnMap.Count}: {string.Join(", ", result.OnMap.Select(p => p.Species))}");
        var cat = result.OnMap.Single(p => p.Species == "PinkCat");
        Assert(cat.Placement == SavePalLocationReader.BaseWorker && cat.BaseId == "D04F779E4EF8578B7C8412B759E117E1" && cat.Level == 15,
            $"a Pal in a base's worker container works at that base (placement {cat.Placement}, base {cat.BaseId}, level {cat.Level})");
        var alpha = result.OnMap.Single(p => p.Species == "WeaselDragon");
        Assert(alpha.IsAlpha && alpha.Placement == SavePalLocationReader.Party && alpha.NickName == "Zippy" && alpha.OwnerPlayerId == "A3835C7B000000000000000000000000",
            "BOSS_ is folded into IsAlpha; a 5-slot container is the owner's party; nickname and normalised owner id are kept");
        Assert(Math.Abs(alpha.X - -351444.0) < 1e-6 && Math.Abs(alpha.Y - 270714.0) < 1e-6, "world coordinates are kept as-is");
        Assert(result.InPalbox == 1, $"a 960-slot container is the Palbox and is only counted, got {result.InPalbox}");
        Assert(result.WithoutPosition == 3, $"0,0, within 20 m of the origin, and a placeholder altitude are all unset, got {result.WithoutPosition}");
        Assert(result.Unplaced == 1, $"a Pal in an unknown container is not guessed at, got {result.Unplaced}");
        using var empty = System.Text.Json.JsonDocument.Parse("{}");
        var none = SavePalLocationReader.Read(empty.RootElement);
        Assert(none.TotalPals == 0 && none.OnMap.Count == 0, "a save with no character map yields nothing, not an error");
    }, failures);

    RunScenario("Pal positions: the map layer clusters nearby Pals, names them honestly, and says what it does not draw", () =>
    {
        var worker = new MystTiq.Desktop.Services.PalMapEntry("PinkCat", false, 15, "", "", MystTiq.Desktop.Services.PalMapLayout.BaseWorker, "Crystal", -355668, 272597);
        var worker2 = worker with { Species = "Boar", Level = 10 };
        var partyPal = new MystTiq.Desktop.Services.PalMapEntry("WeaselDragon", true, 12, "Zippy", "Keeper", MystTiq.Desktop.Services.PalMapLayout.Party, "", -351444, 270714);
        var clusters = MystTiq.Desktop.Services.PalMapLayout.Cluster([(100, 100, 50, 50, worker), (104, 103, 52, 51, worker2), (160, 100, 80, 50, partyPal)]);
        Assert(clusters.Count == 2 && clusters[0].Members.Count == 2 && clusters[1].Members.Count == 1, $"Pals within 10 px share a marker, others do not (got {clusters.Count})");
        Assert(Math.Abs(clusters[0].ViewX - 102) < 1e-9 && Math.Abs(clusters[0].FlatY - 50.5) < 1e-9, "a cluster sits at its members' average position");
        Assert(MystTiq.Desktop.Services.PalMapLayout.Title(clusters[0]) == "2 Pals working at Crystal's base", MystTiq.Desktop.Services.PalMapLayout.Title(clusters[0]));
        Assert(MystTiq.Desktop.Services.PalMapLayout.Title(clusters[1]) == "Zippy (WeaselDragon), alpha, level 12", MystTiq.Desktop.Services.PalMapLayout.Title(clusters[1]));
        var tip = MystTiq.Desktop.Services.PalMapLayout.Tooltip(clusters[1], "(248, -495)");
        Assert(tip.Contains("in Keeper's party") && tip.Contains("(248, -495)") && tip.Contains("Last position the save recorded"), tip);
        var mixed = MystTiq.Desktop.Services.PalMapLayout.Cluster([(0, 0, 0, 0, worker), (1, 1, 0, 0, partyPal)]);
        Assert(MystTiq.Desktop.Services.PalMapLayout.Title(mixed[0]) == "2 Pals", "a cluster of different places does not claim one place");
        var many = MystTiq.Desktop.Services.PalMapLayout.Cluster(Enumerable.Range(0, 15).Select(i => ((double)i * 0.1, 0d, 0d, 0d, worker)).ToArray());
        Assert(many.Count == 1 && MystTiq.Desktop.Services.PalMapLayout.Tooltip(many[0], "").Contains("…and 3 more"), "a long tooltip is cut to 12 Pals and says how many more");
        Assert(MystTiq.Desktop.Services.PalMapLayout.Where(worker with { GuildName = "" }) == "working at a base", "an unnamed base is not given a name");
        // Next to a base marker the cluster becomes a badge on the base's corner (the nearest base wins); farther away it stays put.
        var badge = MystTiq.Desktop.Services.PalMapLayout.BadgePosition(103, 104, [(100, 100), (108, 108), (300, 300)]);
        Assert(badge is { } b && b.X == 100 + MystTiq.Desktop.Services.PalMapLayout.BadgeOffset && b.Y == 100 - MystTiq.Desktop.Services.PalMapLayout.BadgeOffset,
            $"a cluster within 12 px of a base is drawn on the nearest base's corner, got {badge}");
        Assert(MystTiq.Desktop.Services.PalMapLayout.BadgePosition(130, 100, [(100, 100)]) is null, "a cluster 30 px from any base stays where it is");
        Assert(MystTiq.Desktop.Services.PalMapLayout.Tooltip(clusters[0], "", onBaseBadge: true).Contains("Drawn on the base's corner"), "a badge's tooltip says it is drawn on the base");
        // A player's name label steps around a Pals marker instead of covering its count.
        var label = new MystTiq.Desktop.Services.MapLabelLayout.MapLabel(100, 100, 40);
        var obstacle = MystTiq.Desktop.Services.MapLabelLayout.LabelBox(label, (0, 0));
        var free = MystTiq.Desktop.Services.MapLabelLayout.ComputeLabelOffsets([label]);
        var moved = MystTiq.Desktop.Services.MapLabelLayout.ComputeLabelOffsets([label], [obstacle]);
        Assert(free[0] == (0, 0) && moved[0] != (0, 0) && !MystTiq.Desktop.Services.MapLabelLayout.LabelBox(label, moved[0]).Overlaps(obstacle),
            $"a label moves off a Pals marker (free {free[0]}, with marker {moved[0]})");
        var status = MystTiq.Desktop.Services.PalMapLayout.Status(8, 3, 65, 9, true);
        Assert(status.StartsWith("11 Pal(s) on the map: 8 working at bases, 3 in players' parties") && status.Contains("65 in a Palbox") &&
               status.Contains("9 without a recorded position") && status.Contains("internal names"), status);
        Assert(MystTiq.Desktop.Services.PalMapLayout.Status(0, 0, 0, 0, false).Contains("decoded Level.sav.json"), "without a decoded save it says what is needed");
        Assert(MystTiq.Desktop.Services.PalMapLayout.Status(0, 0, 4, 0, true).StartsWith("No Pals are out in the world"), "an all-Palbox save says nothing is out");
    }, failures);

    // v0.8.12.0: a second NAT between the router and the internet, from the router's own WAN address (UPnP) or, without
    // UPnP, from the first hops of the route out.
    RunScenario("Second NAT: the router's WAN address and the route out are classified honestly", () =>
    {
        var cgnat = NatTopology.Classify("203.0.113.7", "100.72.14.9");
        Assert(cgnat.State == DiagnosticState.Fail && cgnat.Details.Contains("carrier-grade NAT") && cgnat.Recommendation.Contains("public IPv4"), $"100.64/10 on the router is CGNAT: {cgnat.State} {cgnat.Details}");
        Assert(NatTopology.Classify(null, "100.127.255.1").State == DiagnosticState.Fail && NatTopology.Classify(null, "100.128.0.1").State == DiagnosticState.Pass,
            "the CGNAT range ends at 100.127.255.255");
        var dbl = NatTopology.Classify("203.0.113.7", "192.168.0.20");
        Assert(dbl.State == DiagnosticState.Fail && dbl.Details.Contains("double NAT") && dbl.Recommendation.Contains("bridge mode"), $"a private WAN address is double NAT: {dbl.Details}");
        Assert(NatTopology.Classify(null, "172.31.4.4").State == DiagnosticState.Fail && NatTopology.Classify(null, "172.32.4.4").State == DiagnosticState.Pass, "172.16/12 ends at 172.31");
        var mismatch = NatTopology.Classify("203.0.113.7", "198.51.100.3");
        Assert(mismatch.State == DiagnosticState.Warning && mismatch.Details.Contains("203.0.113.7") && mismatch.Details.Contains("198.51.100.3"), "a public WAN address that is not the public address is a warning naming both");
        var ok = NatTopology.Classify("203.0.113.7", "203.0.113.7");
        Assert(ok.State == DiagnosticState.Pass && ok.Details.Contains("forwarding the port on this router is enough"), ok.Details);
        Assert(NatTopology.Classify("203.0.113.7", null).State == DiagnosticState.Skipped && NatTopology.Classify("203.0.113.7", "not-an-ip").State == DiagnosticState.Skipped
               && NatTopology.Classify("203.0.113.7", "2001:db8::1").State == DiagnosticState.Skipped, "no usable IPv4 WAN address means it cannot tell");

        var routeCgnat = NatTopology.ClassifyRoute(["192.168.1.1", null, "100.65.0.1", "8.8.4.4"]);
        Assert(routeCgnat is { State: DiagnosticState.Warning } && routeCgnat.Details.Contains("hop 3") && routeCgnat.Details.Contains("carrier-grade"), $"a 100.64/10 hop after the router is likely CGNAT, never a Fail: {routeCgnat?.Details}");
        var routeDouble = NatTopology.ClassifyRoute(["192.168.1.1", "10.0.0.1", "203.0.113.1"]);
        // On the real network a private hop 2 was the ISP's own equipment (the router held the public address), so a private hop is "cannot tell", never a warning.
        Assert(routeDouble is { State: DiagnosticState.Skipped } && routeDouble.Details.Contains("cannot tell") && routeDouble.Recommendation.Contains("status page"), $"a private hop 2 cannot tell double NAT from the ISP's own network: {routeDouble?.State}");
        Assert(NatTopology.ClassifyRoute(["192.168.1.1", "203.0.113.1"]) is { State: DiagnosticState.Pass }, "a public hop right after the router: no second NAT seen");
        Assert(NatTopology.ClassifyRoute(["192.168.1.1", null, "203.0.113.1"]) is null,
            "a silent hop 2 followed by a public hop is no verdict: the silent hop could be the second NAT (seen on the test VM)");
        Assert(NatTopology.ClassifyRoute(["192.168.1.1", null, null]) is null && NatTopology.ClassifyRoute([]) is null && NatTopology.ClassifyRoute(["203.0.113.1", "10.0.0.1"]) is null,
            "no answering hop, no route, or a first hop that is not a home router gives no verdict");
        // The real router (miniupnpd) answers GetExternalIPAddress with UPnP error 501 when its own address is private.
        var refused = NatTopology.ClassifyRoute(["192.168.1.1", "10.17.46.1", "203.0.113.1"], routerRefused: true);
        Assert(refused is { State: DiagnosticState.Skipped } && refused.Details.Contains("would not report its internet address this time") && !refused.Details.Contains("itself private"),
            $"a router that refuses its WAN address is reported plainly, not read as evidence (the real router refused intermittently): {refused?.Details}");
        // Linux parses "/ctl/IPConn" as an absolute file URI; the control URL must still resolve against the router.
        var location = new Uri("http://192.168.1.1:5000/rootDesc.xml");
        Assert(WanReachabilityService.ResolveControlUrl(location, "/ctl/IPConn").ToString() == "http://192.168.1.1:5000/ctl/IPConn" &&
               WanReachabilityService.ResolveControlUrl(location, "http://192.168.1.1:5000/x").ToString() == "http://192.168.1.1:5000/x",
            $"control URLs resolve to http on every OS, got {WanReachabilityService.ResolveControlUrl(location, "/ctl/IPConn")}");
    }, failures);

    // v0.8.13.0: display names from the game's own name tables (HeadlessGameNameService), in the picker and on the map.
    RunScenario("Game names: lookups ignore case and fold alpha ids, the picker lists seen ids first with names, and search finds names", () =>
    {
        var names = GameNameCatalog.FromJson("""{"version":1,"lang":"en","items":{"PalSphere":"Pal Sphere","Wood":"Wood","Arrow_Poison":"Poison Arrow"},"pals":{"SheepBall":"Lamball","WeaselDragon":"Chillet","  ":"blank"}}""");
        Assert(names is { HasNames: true } && names.Items.Count == 3 && names.Pals.Count == 2, "the extractor's JSON parses, and a blank id is dropped");
        Assert(names!.PalName("Sheepball") == "Lamball" && names.PalName("BOSS_WeaselDragon") == "Chillet" && names.ItemName("palsphere") == "Pal Sphere",
            "ids match ignoring case (the save writes Sheepball), and BOSS_ is folded");
        Assert(names.ItemName("NoSuchThing") is null && names.PalName(null) is null, "an unknown id has no name, never a guess");
        Assert(GameNameCatalog.FromJson("not json") is null && GameNameCatalog.FromJson("[]") is null, "garbage is refused");

        var known = HeadlessGameIdCatalogService.Merge(new SaveGameIds(new Dictionary<string, int> { ["PalSphere"] = 12 }, new Dictionary<string, int> { ["Sheepball"] = 2 }, new HashSet<string>()),
            [new KitEntry("Item", "HandMadeThing", 1)], []);
        var rows = HeadlessGameIdCatalogService.WithNames(known, names);
        var sphere = rows.Single(r => r.Id == "PalSphere");
        Assert(sphere.Name == "Pal Sphere" && !sphere.InGameFiles && sphere.WorldCount == 12, "a seen item gets its name and keeps its counts");
        Assert(rows.Single(r => r.Id == "HandMadeThing").Name is null, "a kit id the game does not name keeps no name");
        Assert(rows.Count(r => r.Id.Equals("PalSphere", StringComparison.OrdinalIgnoreCase)) == 1 && rows.Count(r => r.Kind == "Pal" && r.Id.Equals("SheepBall", StringComparison.OrdinalIgnoreCase)) == 1,
            "an id already seen is not listed twice, whatever its case");
        var wood = rows.Single(r => r.Id == "Wood");
        Assert(wood.InGameFiles && wood.WorldCount == 0 && wood.Name == "Wood", "an item only the game lists is added and marked");
        var order = rows.Select(r => $"{r.Kind}:{r.Id}").ToArray();
        Assert(order.SequenceEqual(["Item:HandMadeThing", "Item:PalSphere", "Item:Arrow_Poison", "Item:Wood", "Pal:Sheepball", "Pal:WeaselDragon"]),
            $"items before Pals, seen before game-only, then by name: {string.Join(", ", order)}");

        Assert(GameIdSearch.Matches("SheepBall", "Lamball", "lamball") && GameIdSearch.Matches("PalSphere", "Pal Sphere", "pal sphere") &&
               GameIdSearch.Matches("Arrow_Poison", "Poison Arrow", "poison arrow") && !GameIdSearch.Matches("Wood", "Wood", "stone"),
            "search finds the name as well as the id");
        Assert(GameIdSearch.Matches("PalSphere", null, "sphere") && GameIdSearch.Matches("x", null, ""), "without a name the id alone is searched, and an empty search matches");

        var pal = new MystTiq.Desktop.Services.PalMapEntry("PinkCat", false, 15, "", "", MystTiq.Desktop.Services.PalMapLayout.BaseWorker, "Crystal", 0, 0, "Cattiva");
        Assert(MystTiq.Desktop.Services.PalMapLayout.Name(pal) == "Cattiva" && MystTiq.Desktop.Services.PalMapLayout.Name(pal with { NickName = "Kitty" }) == "Kitty (Cattiva)" &&
               MystTiq.Desktop.Services.PalMapLayout.Name(pal with { SpeciesName = "" }) == "PinkCat", "the map shows the species name, the id without one");
        Assert(!MystTiq.Desktop.Services.PalMapLayout.Status(1, 0, 0, 0, true, namesAvailable: true).Contains("internal names") &&
               MystTiq.Desktop.Services.PalMapLayout.Status(1, 0, 0, 0, true).Contains("internal names"), "the map only mentions internal names when it shows them");
    }, failures);

    RunScenario("Game names: the service runs the extractor once per pak, caches it, and says why names are missing", () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "mysttiq-names-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new FakePathProfile(root);
            var paks = Path.Combine(root, "Pal", "Content", "Paks");
            Assert(new HeadlessGameNameService(paths, findPython: () => null).Get().Status.Detail.Contains("no game pak"), "no pak: says so");
            Directory.CreateDirectory(paks);
            var pak = Path.Combine(paks, "Pal-TestServer.pak");
            File.WriteAllText(pak, "pak");
            var missingPython = new HeadlessGameNameService(paths, findPython: () => null).Get();
            Assert(!missingPython.Status.Available && missingPython.Status.Detail.Contains("Python was not found") && missingPython.Status.Detail.Contains("ooz") &&
                   !missingPython.Catalog.HasNames, "no Python: ids only, and the reason and what is needed are given");

            var python = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Where(d => !d.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
                .SelectMany(d => new[] { "python.exe", "python3", "python" }.Select(n => { try { return Path.Combine(d.Trim(), n); } catch (ArgumentException) { return ""; } }))
                .FirstOrDefault(File.Exists);
            if (python is null) { Console.WriteLine("    (no Python on PATH here: the extractor run is not exercised)"); return; }

            // A stand-in extractor: counts its runs and prints what the real one prints.
            var counter = Path.Combine(root, "runs.txt");
            var script = Path.Combine(root, "fake_extract.py");
            File.WriteAllText(script, "import sys\nopen(r'" + counter + "', 'a').write('x')\n" +
                "if '--pak' not in sys.argv: sys.exit(5)\n" +
                "print('{\"version\": 1, \"lang\": \"en\", \"items\": {\"PalSphere\": \"Pal Sphere\"}, \"pals\": {\"SheepBall\": \"Lamball\"}}')\n");
            var service = new HeadlessGameNameService(paths, script, () => python);
            var first = service.Get();
            Assert(first.Status.Available && first.Catalog.PalName("Sheepball") == "Lamball" && File.Exists(service.CachePath), $"the extractor runs and its names are cached: {first.Status.Detail}");
            service.Get();
            Assert(File.ReadAllText(counter).Length == 1, "a second read uses the loaded names, no second run");
            var restarted = new HeadlessGameNameService(paths, script, () => python).Get();
            Assert(restarted.Catalog.ItemName("PalSphere") == "Pal Sphere" && File.ReadAllText(counter).Length == 1, "after a restart the cache is used while the pak is unchanged");
            File.AppendAllText(pak, "updated");
            new HeadlessGameNameService(paths, script, () => python).Get();
            Assert(File.ReadAllText(counter).Length == 2, "a changed pak (a game update) runs the extractor again");

            var failing = Path.Combine(root, "fail_extract.py");
            File.WriteAllText(failing, "import sys\nsys.stderr.write('the Python module ooz is not installed')\nsys.exit(2)\n");
            File.AppendAllText(pak, "again");
            var noOoz = new HeadlessGameNameService(paths, failing, () => python).Get();
            Assert(!noOoz.Status.Available && noOoz.Status.Detail.Contains("Oodle module (ooz) is not installed"), $"exit code 2 names the missing ooz module: {noOoz.Status.Detail}");
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }, failures);

    // v0.8.14.0: the Desktop mirrors the server's role order when it hides or disables cards.
    RunScenario("Role cards: the Desktop's role order matches the server's, unknown roles get the least, and local use keeps full access", () =>
    {
        foreach (var role in Enum.GetValues<MystTiqRole>())
            Assert(MystTiq.Desktop.Services.RoleAccess.Rank(role.ToString()) == (int)role, $"{role} ranks as on the server ({(int)role})");
        Assert(MystTiq.Desktop.Services.RoleAccess.Rank("admin") == MystTiq.Desktop.Services.RoleAccess.Viewer && MystTiq.Desktop.Services.RoleAccess.Rank(null) == MystTiq.Desktop.Services.RoleAccess.Viewer &&
               MystTiq.Desktop.Services.RoleAccess.Rank("Superuser") == MystTiq.Desktop.Services.RoleAccess.Viewer, "an unknown or mis-cased role gets the least access, never more");
        Assert(MystTiq.Desktop.Services.RoleAccess.Allows(false, null, MystTiq.Desktop.Services.RoleAccess.Owner), "the local token-less connection keeps full access");
        Assert(MystTiq.Desktop.Services.RoleAccess.Allows(true, "Operator", MystTiq.Desktop.Services.RoleAccess.Operator) &&
               !MystTiq.Desktop.Services.RoleAccess.Allows(true, "Operator", MystTiq.Desktop.Services.RoleAccess.Admin) &&
               !MystTiq.Desktop.Services.RoleAccess.Allows(true, "Viewer", MystTiq.Desktop.Services.RoleAccess.Operator) &&
               MystTiq.Desktop.Services.RoleAccess.Allows(true, "Owner", MystTiq.Desktop.Services.RoleAccess.Owner), "each role reaches exactly its own level");
        Assert(MystTiq.Desktop.Services.RoleAccess.HiddenNotice(false, null) == "" && MystTiq.Desktop.Services.RoleAccess.HiddenNotice(true, "Owner") == "",
            "nothing is hidden for local use or the Owner, so no notice");
        var notice = MystTiq.Desktop.Services.RoleAccess.HiddenNotice(true, "Operator");
        Assert(notice.Contains("Signed in as Operator") && notice.Contains("hidden") && notice.Contains("read-only"), notice);
    }, failures);

    RunScenario("Crash signatures: every signature has a cause and fixes, ids are unique, and ordinary words never match", () =>
    {
        Assert(CrashSignatureCatalog.All.Count >= 10, "the catalog should hold the known signatures");
        Assert(CrashSignatureCatalog.All.Select(s => s.Id).Distinct().Count() == CrashSignatureCatalog.All.Count, "signature ids must be unique");
        Assert(CrashSignatureCatalog.All.All(s => s.Cause.Length > 40 && s.Fixes.Count >= 1 && s.Severity is "Critical" or "Warning"), "every signature needs a real cause, a fix and a known severity");
        // The old needle "oom" matched inside ordinary words.
        var harmless = CrashSignatureCatalog.Match(["Player entered the room", "zoom level changed", "Boom! a Pal exploded", "UE4SS mod loaded", "Starting UE4SS v3.0.1", "Steam appid is 2394010"]);
        Assert(harmless.Count == 0, $"ordinary lines must not match, got: {string.Join(", ", harmless.Select(m => m.Signature.Id))}");
        Assert(CrashSignatureCatalog.Match(["[Server] Out of memory while allocating"]).Single().Signature.Id == "out-of-memory", "out of memory");
        Assert(CrashSignatureCatalog.Match(["kernel: OOM killer invoked"]).Single().Signature.Id == "out-of-memory", "a standalone OOM word");
        Assert(CrashSignatureCatalog.Match(["LogWindows: Error: EXCEPTION_ACCESS_VIOLATION reading address 0x0"]).Single().Signature.Id == "access-violation", "access violation");
        Assert(CrashSignatureCatalog.Match(["There is not enough space on the disk."]).Single().Signature.Id == "disk-full", "disk full");
        Assert(CrashSignatureCatalog.Match(["Only one usage of each socket address is normally permitted"]).Single().Signature.Id == "port-in-use", "port in use");
        Assert(CrashSignatureCatalog.Match(["The code execution cannot proceed because VCRUNTIME140_1.dll was not found."]).Single().Signature.Id == "missing-runtime-dll", "missing runtime");
        Assert(CrashSignatureCatalog.Match(["Fatal error: something broke"]).Single().Signature.Id == "ue-fatal", "generic fatal error is the catch-all");
    }, failures);
    RunScenario("Crash signatures: UE4SS is only a finding when it reports an error, and one crash line is claimed once", () =>
    {
        Assert(CrashSignatureCatalog.Match(["UE4SS: Loaded mod PalGuide"]).Count == 0, "a plain UE4SS mention is not a finding");
        Assert(CrashSignatureCatalog.Match(["Unhandled exception in UE4SS callback"]).Single().Signature.Id == "ue4ss-error", "UE4SS with an exception is a UE4SS finding");
        Assert(CrashSignatureCatalog.Match(["[Lua] Error: attempt to index a nil value"]).Single().Signature.Id == "ue4ss-error", "a Lua error");
        var both = CrashSignatureCatalog.Match(["Unhandled Exception: EXCEPTION_ACCESS_VIOLATION in UE4SS.dll"]);
        Assert(both.Count == 1 && both[0].Signature.Id == "access-violation", "a native crash line naming UE4SS is one access-violation finding, not three");
        var fixture = CrashSignatureCatalog.Match(["Server initialized successfully", "Fatal error: test-only runtime smoke signature", "Unhandled exception in UE4SS callback"]);
        Assert(fixture.Count == 2, "the runtime-smoke fixture must still yield two findings");
    }, failures);
    RunScenario("Crash signatures: exit codes are translated, and clean or unavailable codes mean nothing", () =>
    {
        Assert(CrashSignatureCatalog.Match(["[2026-09-21 08:00:00.000] Server session #3 process exited with code -1073741819"]).Single().Signature.Id == "access-violation", "0xC0000005 as a signed code");
        Assert(CrashSignatureCatalog.Match(["Server session #3 process exited with code 3221225477"]).Single().Signature.Id == "access-violation", "0xC0000005 as an unsigned code");
        Assert(CrashSignatureCatalog.Match(["process exited with code 3221225781"]).Single().Signature.Id == "missing-runtime-dll", "0xC0000135");
        Assert(CrashSignatureCatalog.Match(["process exited with code 137"]).Single().Signature.Id == "out-of-memory", "137 is a SIGKILL, most often the OOM killer");
        Assert(CrashSignatureCatalog.Match(["process exited with code 0", "process exited with code -1", "process exited with code 42"]).Count == 0, "0, -1 and unknown codes carry no failure meaning");
        Assert(CrashSignatureCatalog.SignatureForExitCode(139) == "access-violation" && CrashSignatureCatalog.SignatureForExitCode(134) == "ue-fatal", "Linux signals");
    }, failures);
    RunScenario("Crash signatures: log timestamps in UE and MystTiq formats are read, and nonsense is not", () =>
    {
        var ue = CrashSignatureCatalog.TryParseTimestamp("[2026.09.21-08.12.33:456][123]LogX: hello");
        var own = CrashSignatureCatalog.TryParseTimestamp("[2026-09-21 08:12:33.456] Server session #3 exited");
        Assert(ue is not null && own is not null && ue.Value.Year == 2026 && ue.Value.Month == 9 && ue.Value.Hour == 8 && ue.Value.Second == 33, "UE format");
        Assert(own is not null && own.Value == ue, "the MystTiq format reads as the same instant");
        Assert(CrashSignatureCatalog.TryParseTimestamp("[2026.13.45-99.99.99] nonsense") is null && CrashSignatureCatalog.TryParseTimestamp("no stamp here") is null, "bad or missing stamps give null");
    }, failures);
    RunScenario("Crash analysis names mods in the evidence, ignores too-short mod names, and reports first and last seen", () =>
    {
        var lines = new[]
        {
            "[2026.09.21-08.00.00:000] LogX: normal line",
            "[2026.09.21-08.10.00:000] Fatal error: PalGuideMod crashed in OnTick",
            "continuation line with no stamp: Fatal error again",
            "[2026.09.21-08.20.00:000] Fatal error: PalGuideMod crashed in OnTick"
        };
        var findings = CrashAnalysisBuilder.Build(lines, ["Pal Guide Mod", "ab", "Unrelated Thing"], null);
        var f = findings.Single();
        Assert(f.SignatureId == "ue-fatal" && f.MatchCount == 3, $"three lines, one signature; got {f.SignatureId} x{f.MatchCount}");
        Assert(f.MentionedMods!.SequenceEqual(["Pal Guide Mod"]), "only the mod named in the evidence is reported, and 2-letter names are ignored");
        Assert(f.FirstSeen is not null && f.LastSeen is not null && f.FirstSeen < f.LastSeen, "first and last seen come from the stamped lines only");
        Assert(f.Cause.Length > 0 && f.Fixes is { Count: > 0 }, "the finding carries the catalog cause and fixes");
        Assert(CrashAnalysisBuilder.BuildIsolationPlan(findings).Any(step => step.Contains("\"Pal Guide Mod\"")), "the isolation plan names the mod first");
        Assert(CrashAnalysisBuilder.BuildSummary(findings).Contains("Pal Guide Mod") && CrashAnalysisBuilder.BuildSummary(findings).Contains("not proof"), "the summary names the mod and says it is a lead, not proof");
    }, failures);
    RunScenario("Crash analysis marks findings new or already reported, and a fresh crash is new again", () =>
    {
        var logs = new[] { "Fatal error: first crash", "Out of memory" };
        var first = CrashAnalysisBuilder.Build(logs, null, null);
        Assert(first.Count == 2 && first.All(x => x.IsNew), "with no history everything is new");
        var keys = first.Select(x => x.Key).ToHashSet();
        var second = CrashAnalysisBuilder.Build(logs, null, keys);
        Assert(second.All(x => !x.IsNew), "the same logs analysed again are all already reported");
        Assert(CrashAnalysisBuilder.BuildSummary(second).Contains("0 new") && CrashAnalysisBuilder.BuildSummary(second).Contains("2 already reported"), $"summary counts: {CrashAnalysisBuilder.BuildSummary(second)}");
        var third = CrashAnalysisBuilder.Build(logs.Append("Fatal error: second crash").ToArray(), null, keys);
        Assert(third.Single(x => x.SignatureId == "ue-fatal").IsNew && !third.Single(x => x.SignatureId == "out-of-memory").IsNew, "a new crash line makes only that signature new");
        Assert(third[0].IsNew, "new findings sort first");
        Assert(CrashAnalysisBuilder.BuildSummary([]).Contains("not proof"), "no findings still says absence of a signature is not proof");
    }, failures);
    RunScenario("A crash report saved before v0.7.97.0 still loads, with no cause and treated as new", () =>
    {
        var old = "{\"Id\":\"a\",\"ObservedAt\":\"2026-09-01T00:00:00+00:00\",\"FilesScanned\":1,\"LinesScanned\":2,\"Findings\":[{\"Title\":\"Fatal error\",\"Severity\":\"Critical\",\"MatchCount\":1,\"Evidence\":[\"x\"]}],\"IsolationPlan\":[],\"Summary\":\"s\"}";
        var row = System.Text.Json.JsonSerializer.Deserialize<HeadlessCrashAnalysisSnapshot>(old);
        Assert(row is not null && row.Findings.Count == 1 && row.Findings[0].IsNew && row.Findings[0].Key == "" && row.Findings[0].Fixes is null && row.NewFindings == 0, "old report shape must load with defaults");
    }, failures);

    RunScenario("Doctor disk space: fails under 2 GiB, warns under 5 GiB, and warns when the next backup would not fit", () =>
    {
        const long GiB = 1024L * 1024 * 1024;
        Assert(DoctorHealthRules.DiskSpace("the server (C:\\)", 10 * GiB, 100 * GiB).State == DiagnosticState.Pass, "10 GiB free passes");
        Assert(DoctorHealthRules.DiskSpace("x", 4 * GiB, 100 * GiB).State == DiagnosticState.Warning, "4 GiB free warns");
        Assert(DoctorHealthRules.DiskSpace("x", 1 * GiB, 100 * GiB).State == DiagnosticState.Fail, "1 GiB free fails");
        var tight = DoctorHealthRules.DiskSpace("backups", 20 * GiB, 500 * GiB, largestBackupBytes: 12 * GiB);
        Assert(tight.State == DiagnosticState.Warning && tight.Evidence.Contains("next backup may not fit"), "20 GiB free but a 12 GiB backup: the next one may not fit");
        Assert(DoctorHealthRules.DiskSpace("backups", 20 * GiB, 500 * GiB, largestBackupBytes: 5 * GiB).State == DiagnosticState.Pass, "20 GiB free with 5 GiB backups is fine");
        Assert(DoctorHealthRules.FormatSpan(TimeSpan.FromMinutes(5)) == "5 minute(s)" && DoctorHealthRules.FormatSpan(TimeSpan.FromHours(30)) == "1.3 day(s)", "spans read naturally");
    }, failures);
    RunScenario("Doctor backup freshness is judged against the world's changes, not the clock", () =>
    {
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        Assert(DoctorHealthRules.BackupFreshness(0, null, now.AddHours(-1), now).State == DiagnosticState.Warning, "a world with no backup warns");
        Assert(DoctorHealthRules.BackupFreshness(0, null, null, now).State == DiagnosticState.Skipped, "no world and no backup has nothing to protect");
        Assert(DoctorHealthRules.BackupFreshness(3, now.AddHours(-2), now.AddMinutes(-90), now).State == DiagnosticState.Pass, "a recent backup passes");
        Assert(DoctorHealthRules.BackupFreshness(3, now.AddDays(-30), now.AddDays(-31), now).State == DiagnosticState.Pass, "a 30-day-old backup is fine when the world has not changed since");
        var warn = DoctorHealthRules.BackupFreshness(3, now.AddDays(-5), now, now);
        Assert(warn.State == DiagnosticState.Warning && warn.Evidence.Contains("5 day(s)"), $"5 days of unprotected play warns: '{warn.Evidence}'");
        Assert(DoctorHealthRules.BackupFreshness(3, now.AddDays(-20), now, now).State == DiagnosticState.Fail, "20 days of unprotected play fails");
        Assert(DoctorHealthRules.BackupFreshness(3, now.AddHours(-30), now, now).State == DiagnosticState.Warning, "30 hours of unprotected play warns");
    }, failures);
    RunScenario("Doctor admin access: judged only when REST or RCON is on, and the password is never echoed", () =>
    {
        Assert(DoctorHealthRules.AdminSecurity("", false, false).State == DiagnosticState.Pass, "no remote admin interface, nothing to judge");
        Assert(DoctorHealthRules.AdminSecurity("", false, true).State == DiagnosticState.Warning, "RCON on with an empty password warns");
        Assert(DoctorHealthRules.AdminSecurity("\"\"", true, false).State == DiagnosticState.Warning, "a quoted empty password is empty");
        Assert(DoctorHealthRules.AdminSecurity("Admin123", true, false).State == DiagnosticState.Warning, "a very common password warns, case-insensitively");
        Assert(DoctorHealthRules.AdminSecurity("abc12", true, true).State == DiagnosticState.Warning, "a short password warns");
        const string strong = "Xk9!vT2#mQ8z";
        var ok = DoctorHealthRules.AdminSecurity($"\"{strong}\"", true, true);
        Assert(ok.State == DiagnosticState.Pass, "a long random password passes");
        foreach (var pw in new[] { strong, "Admin123", "abc12", "" })
        {
            var r = DoctorHealthRules.AdminSecurity(pw, true, true);
            Assert(pw.Length == 0 || (!r.Evidence.Contains(pw) && !r.Recommendation.Contains(pw)), "the password itself must never appear in a finding");
        }
    }, failures);
    RunScenario("Doctor memory: warns on a small machine or little free memory, and is skipped when unreadable", () =>
    {
        const long GiB = 1024L * 1024 * 1024;
        Assert(DoctorHealthRules.Memory(16 * GiB, 8 * GiB).State == DiagnosticState.Pass, "16 GiB with 8 free passes");
        Assert(DoctorHealthRules.Memory(4 * GiB, 3 * GiB).State == DiagnosticState.Warning, "a 4 GiB machine warns");
        Assert(DoctorHealthRules.Memory(32 * GiB, 1 * GiB).State == DiagnosticState.Warning, "1 GiB free warns");
        Assert(DoctorHealthRules.Memory(0, 0).State == DiagnosticState.Skipped, "unreadable memory is skipped, not failed");
        Assert(DoctorHealthRules.Memory(16 * GiB, -5).Evidence.Contains("0.0 GiB free"), "a negative available figure is clamped");
    }, failures);
    RunScenario("Doctor recent crashes: only new critical findings warn, and an older-format report cannot say", () =>
    {
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        static HeadlessCrashFinding F(string title, string severity, bool isNew, string key = "k") => new(title, severity, 1, ["x"], "sig", "cause", ["fix"], null, null, [], isNew, key);
        static HeadlessCrashAnalysisSnapshot S(DateTimeOffset at, params HeadlessCrashFinding[] f) => new("id", at, 1, 10, f, [], "s", f.Count(x => x.IsNew), f.Count(x => !x.IsNew));
        Assert(DoctorHealthRules.RecentCrashes(null, now).State == DiagnosticState.Skipped, "no analysis yet is skipped");
        var fresh = DoctorHealthRules.RecentCrashes(S(now.AddHours(-2), F("Out of memory", "Critical", true)), now);
        Assert(fresh.State == DiagnosticState.Warning && fresh.Evidence.Contains("Out of memory") && fresh.Evidence.Contains("2 hour(s)"), $"a new critical finding warns: '{fresh.Evidence}'");
        Assert(DoctorHealthRules.RecentCrashes(S(now, F("Out of memory", "Critical", false)), now).State == DiagnosticState.Pass, "an already-reported critical finding passes once reviewed");
        Assert(DoctorHealthRules.RecentCrashes(S(now, F("UE4SS error", "Warning", true)), now).State == DiagnosticState.Pass, "a new non-critical finding does not warn");
        Assert(DoctorHealthRules.RecentCrashes(S(now), now).State == DiagnosticState.Pass, "no findings passes");
        Assert(DoctorHealthRules.RecentCrashes(S(now, F("Fatal", "Critical", true, key: "")), now).State == DiagnosticState.Skipped, "a report from before the analyzer had keys cannot say what is new");
    }, failures);

    static (Task<int> Loop, CancellationTokenSource Cts) StartRecovery(ScriptedLifecycle lifecycle, ISupervisorObserver observer, int maxAttempts = 3)
    {
        var options = new HeadlessSupervisorOptions(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(5), maxAttempts, TimeSpan.FromMinutes(1));
        var supervisor = new HeadlessSupervisor(lifecycle, options, [], observer);
        var cts = new CancellationTokenSource();
        var loop = Task.Run(async () =>
        {
            try { return await supervisor.RunCrashRecoveryLoopAsync(cts.Token); }
            catch (OperationCanceledException) { return 0; }
        });
        return (loop, cts);
    }
    static bool WaitFor(Func<bool> condition, int milliseconds = 4000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until) { if (condition()) return true; Thread.Sleep(20); }
        return condition();
    }

    RunScenario("Crash recovery reports a crash and its recovery to the observer, and says nothing when the server was stopped on purpose", () =>
    {
        var crashed = new ScriptedLifecycle { Running = false, CrashFlag = true, Phase = ServerLifecyclePhase.Crashed };
        var observer = new RecordingObserver();
        var (loop, cts) = StartRecovery(crashed, observer);
        Assert(WaitFor(() => observer.Events.Count >= 2), $"expected a crash and a recovery event, got {observer.Events.Count}");
        cts.Cancel(); loop.GetAwaiter().GetResult();
        Assert(observer.Events[0].Kind == SupervisorEventKind.CrashDetected && observer.Events[0].Attempt == 1 && observer.Events[0].MaximumAttempts == 3, "first event is the crash, attempt 1 of 3");
        Assert(observer.Events[1].Kind == SupervisorEventKind.RecoverySucceeded && crashed.Starts == 1, "then a successful recovery after exactly one start");

        var stopped = new ScriptedLifecycle { Running = false, CrashFlag = false, Phase = ServerLifecyclePhase.Stopped };
        var quiet = new RecordingObserver();
        var (quietLoop, quietCts) = StartRecovery(stopped, quiet);
        Thread.Sleep(400);
        quietCts.Cancel(); quietLoop.GetAwaiter().GetResult();
        Assert(quiet.Events.Count == 0 && stopped.Starts == 0, "a server stopped by intent is neither restarted nor announced as a crash");
    }, failures);
    RunScenario("Crash recovery still restarts the server when the observer fails, and reports each failed restart", () =>
    {
        var crashed = new ScriptedLifecycle { Running = false, CrashFlag = true, Phase = ServerLifecyclePhase.Crashed };
        var (loop, cts) = StartRecovery(crashed, new RecordingObserver(throws: true));
        Assert(WaitFor(() => crashed.Starts >= 1 && crashed.Running), "an observer that throws must not stop the restart");
        cts.Cancel(); loop.GetAwaiter().GetResult();

        var failing = new ScriptedLifecycle { Running = false, CrashFlag = true, StartSucceeds = false, Phase = ServerLifecyclePhase.Crashed };
        var observer = new RecordingObserver();
        var (failLoop, failCts) = StartRecovery(failing, observer, maxAttempts: 2);
        Assert(failLoop.Wait(4000), "with every restart failing, the loop must give up and return");
        failCts.Cancel();
        var kinds = observer.Events.Select(e => e.Kind).ToArray();
        Assert(kinds.SequenceEqual([SupervisorEventKind.CrashDetected, SupervisorEventKind.RecoveryFailed, SupervisorEventKind.CrashDetected, SupervisorEventKind.RecoveryFailed, SupervisorEventKind.RecoverySuppressed]),
            $"expected crash/failed twice then suppressed, got: {string.Join(", ", kinds)}");
        Assert(failLoop.Result == (int)HeadlessExitCode.CrashDetected && failing.Starts == 2, "the loop returns the crash exit code after exactly two attempts");
    }, failures);
    RunScenario("HeadlessFleetCrashRecoveryService keeps watching after a give-up: reports the server coming back up, and resumes real crash monitoring afterward", () =>
    {
        // Every restart attempt fails, so the loop gives up quickly (maxAttempts: 1).
        var lifecycle = new ScriptedLifecycle { Running = false, CrashFlag = true, StartSucceeds = false, Phase = ServerLifecyclePhase.Crashed };
        var options = new HeadlessSupervisorOptions(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(5), 1, TimeSpan.FromMinutes(1));
        var observer = new RecordingObserver();
        var service = new HeadlessFleetCrashRecoveryService(lifecycle, options, [], observer);
        var cts = new CancellationTokenSource();
        service.StartAsync(cts.Token);

        Assert(WaitFor(() => observer.Events.Any(e => e.Kind == SupervisorEventKind.RecoverySuppressed)), "the loop must still give up exactly as before");
        var eventsAtGiveUp = observer.Events.Count;

        // Nothing has restarted it yet: a plain wait must not produce a ManualRecovery out of nowhere.
        Thread.Sleep(200);
        Assert(observer.Events.Count == eventsAtGiveUp, "watching after a give-up must not fabricate a recovery before the process is actually back");

        // An admin starts it manually -- NOT through the supervisor's own StartAsync, exactly like a
        // person pressing the real Start button or Automation's Restart action would look from here.
        lifecycle.Running = true;
        lifecycle.CrashFlag = false;
        lifecycle.Phase = ServerLifecyclePhase.Running;

        Assert(WaitFor(() => observer.Events.Any(e => e.Kind == SupervisorEventKind.ManualRecovery)), "watching must notice the process is running again and report ManualRecovery");

        // Prove monitoring genuinely resumed, not just that one more event fired: a fresh crash must be
        // detected and reported like any other, on the very same service instance.
        lifecycle.Running = false;
        lifecycle.CrashFlag = true;
        lifecycle.Phase = ServerLifecyclePhase.Crashed;
        Assert(WaitFor(() => observer.Events.Count(e => e.Kind == SupervisorEventKind.CrashDetected) >= 1), "crash-detect-and-restart monitoring resumed after the manual recovery, not just watched once and stopped");

        cts.Cancel();
        var kinds = observer.Events.Select(e => e.Kind).ToArray();
        Assert(kinds.Contains(SupervisorEventKind.RecoverySuppressed) && kinds.Contains(SupervisorEventKind.ManualRecovery) &&
               Array.IndexOf(kinds, SupervisorEventKind.ManualRecovery) > Array.IndexOf(kinds, SupervisorEventKind.RecoverySuppressed),
            $"give-up must come before the manual recovery it explains, got: {string.Join(", ", kinds)}");
    }, failures);
    // ---- v0.7.115.0 deficiency fixes: readiness, restart-surviving recovery state, operation history ----------
    RunScenario("Recovery only counts as success once the server is ready: a process whose game port never comes up is a failed restart", () =>
    {
        var lifecycle = new ScriptedLifecycle { Running = false, CrashFlag = true, Phase = ServerLifecyclePhase.Crashed, ReadyOverride = false };
        var observer = new RecordingObserver();
        var (loop, cts) = StartRecovery(lifecycle, observer, maxAttempts: 3);
        Assert(WaitFor(() => observer.Events.Any(e => e.Kind == SupervisorEventKind.RecoveryFailed), 5000), "a started-but-never-ready server must be reported as a failed restart");
        var failed = observer.Events.First(e => e.Kind == SupervisorEventKind.RecoveryFailed);
        Assert(failed.Detail.StartsWith("NotReady") && !observer.Events.Any(e => e.Kind == SupervisorEventKind.RecoverySucceeded), $"and never as recovered: '{failed.Detail}'");
        cts.Cancel(); loop.GetAwaiter().GetResult();

        var fine = new ScriptedLifecycle { Running = false, CrashFlag = true, Phase = ServerLifecyclePhase.Crashed };
        var ok = new RecordingObserver();
        var (okLoop, okCts) = StartRecovery(fine, ok);
        Assert(WaitFor(() => ok.Events.Any(e => e.Kind == SupervisorEventKind.RecoverySucceeded)), "a server that is ready at once still recovers normally");
        okCts.Cancel(); okLoop.GetAwaiter().GetResult();
    }, failures);
    RunScenario("After a give-up, 'back up' waits for the server to be ready, not just for a process to exist", () =>
    {
        var lifecycle = new ScriptedLifecycle { Running = false, CrashFlag = true, StartSucceeds = false, Phase = ServerLifecyclePhase.Crashed };
        var options = new HeadlessSupervisorOptions(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(5), 1, TimeSpan.FromMinutes(1));
        var observer = new RecordingObserver();
        var service = new HeadlessFleetCrashRecoveryService(lifecycle, options, [], observer);
        var cts = new CancellationTokenSource();
        service.StartAsync(cts.Token);
        Assert(WaitFor(() => observer.Events.Any(e => e.Kind == SupervisorEventKind.RecoverySuppressed)), "gives up first");

        lifecycle.Running = true; lifecycle.CrashFlag = false; lifecycle.Phase = ServerLifecyclePhase.Running; lifecycle.ReadyOverride = false;
        Thread.Sleep(300);
        Assert(!observer.Events.Any(e => e.Kind == SupervisorEventKind.ManualRecovery), "a process that is still loading (game port not up) is not 'back up' yet");
        lifecycle.ReadyOverride = null;
        Assert(WaitFor(() => observer.Events.Any(e => e.Kind == SupervisorEventKind.ManualRecovery)), "once the game port is up, the server is reported back up");
        cts.Cancel();
    }, failures);
    RunScenario("Crash recovery state survives a MystTiq restart: the restart window, the give-up, and the pinned DOWN notice", () =>
    {
        var root = Path.Combine(tempRoot, "recovery-state-" + Guid.NewGuid().ToString("N"));
        var paths = new FakePathProfile(root);
        var options = new HeadlessSupervisorOptions(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(5), 2, TimeSpan.FromMinutes(10));

        // 1. The restart window: two recent attempts recorded before a restart mean no third attempt now.
        var windowStore = SupervisorRecoveryStateStore.ForProfile(new FakePathProfile(Path.Combine(root, "window")));
        windowStore.Update(s => s with { RestartHistory = [DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(-3)] });
        var crashed = new ScriptedLifecycle { Running = false, CrashFlag = true, Phase = ServerLifecyclePhase.Crashed };
        var windowObserver = new RecordingObserver();
        var supervisor = new HeadlessSupervisor(crashed, options, [], windowObserver, windowStore);
        var exit = Task.Run(() => supervisor.RunCrashRecoveryLoopAsync(CancellationToken.None));
        Assert(exit.Wait(3000) && crashed.Starts == 0 && windowObserver.Events.Single().Kind == SupervisorEventKind.RecoverySuppressed,
            "attempts made before the restart still count: the window is full, so it gives up without restarting again");
        Assert(windowStore.Read().RestartHistory.Count == 2, "an attempt older than the window was aged out and the shorter history saved");

        // 2. The give-up: a MystTiq that starts after recovery gave up keeps watching instead of restarting the still-down server.
        var store = SupervisorRecoveryStateStore.ForProfile(paths);
        var down = new ScriptedLifecycle { Running = false, CrashFlag = true, StartSucceeds = false, Phase = ServerLifecyclePhase.Crashed };
        var giveUpOptions = options with { MaximumRestartAttempts = 1 };
        var first = new RecordingObserver();
        var cts1 = new CancellationTokenSource();
        new HeadlessFleetCrashRecoveryService(down, giveUpOptions, [], first, store).StartAsync(cts1.Token);
        Assert(WaitFor(() => first.Events.Any(e => e.Kind == SupervisorEventKind.RecoverySuppressed)) && WaitFor(() => store.Read().GaveUpAtUtc is not null), "the give-up is saved");
        cts1.Cancel();
        var startsBefore = down.Starts;

        var reopened = SupervisorRecoveryStateStore.ForProfile(paths);
        var second = new RecordingObserver();
        var cts2 = new CancellationTokenSource();
        new HeadlessFleetCrashRecoveryService(down, giveUpOptions, [], second, reopened).StartAsync(cts2.Token);
        Thread.Sleep(300);
        Assert(down.Starts == startsBefore && second.Events.Count == 0, "after the restart it does not treat the still-down server as a new crash or restart it");
        down.Running = true; down.CrashFlag = false; down.Phase = ServerLifecyclePhase.Running;
        Assert(WaitFor(() => second.Events.Any(e => e.Kind == SupervisorEventKind.ManualRecovery)), "it reports the server back up once someone starts it");
        Assert(WaitFor(() => reopened.Read().GaveUpAtUtc is null), "and the give-up is cleared");
        cts2.Cancel();

        // 3. The pinned DOWN notice: pinned by one observer, unpinned by a new one after a restart.
        var activity = new HeadlessActivityLogService(paths);
        var notifications = new HeadlessNotificationService(paths, activity);
        var crashTools = new HeadlessCrashAndSaveToolsService(paths, activity);
        var pinStore = SupervisorRecoveryStateStore.ForProfile(new FakePathProfile(Path.Combine(root, "pin")));
        SupervisorEvent E(SupervisorEventKind kind) => new(kind, 1, 1, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(10), "x", DateTimeOffset.UtcNow);
        new CrashAlertObserver("S", notifications, crashTools, stateStore: pinStore).OnEventAsync(E(SupervisorEventKind.RecoverySuppressed), CancellationToken.None).GetAwaiter().GetResult();
        var downNotice = notifications.GetSnapshot().Items.Single(i => i.Title.Contains("DOWN"));
        Assert(downNotice.Pinned && pinStore.Read().PinnedNotificationId == downNotice.Id, "the DOWN notice is pinned and its id saved");
        var afterRestart = new CrashAlertObserver("S", notifications, crashTools, stateStore: SupervisorRecoveryStateStore.ForProfile(new FakePathProfile(Path.Combine(root, "pin"))));
        afterRestart.OnEventAsync(E(SupervisorEventKind.ManualRecovery), CancellationToken.None).GetAwaiter().GetResult();
        Assert(!notifications.GetSnapshot().Items.Single(i => i.Id == downNotice.Id).Pinned, "a new observer after a restart still unpins the DOWN notice");
    }, failures);
    // ---- v0.8.2.0 service mode --------------------------------------------------------------------------------
    RunScenario("Service mode: the supervisor records its own give-up, and the next service start announces the recovery once the server is ready", () =>
    {
        var root = Path.Combine(tempRoot, "service-mode-" + Guid.NewGuid().ToString("N"));
        var options = new HeadlessSupervisorOptions(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(600), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(5), 1, TimeSpan.FromMinutes(10));

        // Give-up without the api-run wrapper (service-run calls RunAsync directly): the supervisor records it.
        var store = SupervisorRecoveryStateStore.ForProfile(new FakePathProfile(Path.Combine(root, "a")));
        var failing = new ScriptedLifecycle { Running = false, CrashFlag = true, StartSucceeds = false, Phase = ServerLifecyclePhase.Crashed };
        var exit = Task.Run(() => new HeadlessSupervisor(failing, options, [], new RecordingObserver(), store).RunCrashRecoveryLoopAsync(CancellationToken.None));
        Assert(exit.Wait(4000) && exit.Result == (int)HeadlessExitCode.CrashDetected, "gives up with the service exit code, unchanged");
        Assert(store.Read().GaveUpAtUtc is not null, "and records the give-up itself");

        // The OS restarts the service: RunAsync starts the server; once it is ready, the outage is announced as over.
        var restarted = new ScriptedLifecycle { Running = false, CrashFlag = false, Phase = ServerLifecyclePhase.Stopped };
        var observer = new RecordingObserver();
        var cts = new CancellationTokenSource();
        var run = Task.Run(async () => { try { return await new HeadlessSupervisor(restarted, options, [], observer, store).RunAsync(cts.Token); } catch (OperationCanceledException) { return 0; } });
        Assert(WaitFor(() => observer.Events.Any(e => e.Kind == SupervisorEventKind.ManualRecovery)), "a server that came back up after the service restart is announced as recovered");
        Assert(store.Read().GaveUpAtUtc is null && restarted.Starts == 1, "the give-up is cleared and the server was started exactly once");
        cts.Cancel(); run.GetAwaiter().GetResult();

        // Same, but the server never becomes ready: no recovery is announced and the give-up stands.
        var storeB = SupervisorRecoveryStateStore.ForProfile(new FakePathProfile(Path.Combine(root, "b")));
        storeB.Update(s => s with { GaveUpAtUtc = DateTimeOffset.UtcNow });
        var stuck = new ScriptedLifecycle { Running = true, Phase = ServerLifecyclePhase.Running, ReadyOverride = false };
        var quiet = new RecordingObserver();
        var ctsB = new CancellationTokenSource();
        var runB = Task.Run(async () => { try { return await new HeadlessSupervisor(stuck, options, [], quiet, storeB).RunAsync(ctsB.Token); } catch (OperationCanceledException) { return 0; } });
        Thread.Sleep(1200);
        Assert(!quiet.Events.Any(e => e.Kind == SupervisorEventKind.ManualRecovery) && storeB.Read().GaveUpAtUtc is not null, "a running but never-ready server is not announced as recovered");
        ctsB.Cancel(); runB.GetAwaiter().GetResult();

        // No recorded give-up: an ordinary service start announces nothing.
        var plain = new ScriptedLifecycle { Running = true, Phase = ServerLifecyclePhase.Running };
        var none = new RecordingObserver();
        var ctsC = new CancellationTokenSource();
        var runC = Task.Run(async () => { try { return await new HeadlessSupervisor(plain, options, [], none, SupervisorRecoveryStateStore.ForProfile(new FakePathProfile(Path.Combine(root, "c")))).RunAsync(ctsC.Token); } catch (OperationCanceledException) { return 0; } });
        Thread.Sleep(300);
        Assert(none.Events.Count == 0, "an ordinary service start sends no recovery notice");
        ctsC.Cancel(); runC.GetAwaiter().GetResult();
    }, failures);
    RunScenario("Service mode: a second give-up replaces the pinned DOWN notice instead of pinning another one", () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "service-pins-" + Guid.NewGuid().ToString("N")));
        var activity = new HeadlessActivityLogService(paths);
        var notifications = new HeadlessNotificationService(paths, activity);
        var observer = new CrashAlertObserver("S", notifications, new HeadlessCrashAndSaveToolsService(paths, activity), stateStore: SupervisorRecoveryStateStore.ForProfile(paths));
        SupervisorEvent GiveUp() => new(SupervisorEventKind.RecoverySuppressed, 1, 1, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(10), "Gave up.", DateTimeOffset.UtcNow);
        observer.OnEventAsync(GiveUp(), CancellationToken.None).GetAwaiter().GetResult();
        observer.OnEventAsync(GiveUp(), CancellationToken.None).GetAwaiter().GetResult();
        var downs = notifications.GetSnapshot().Items.Where(i => i.Title.Contains("DOWN")).ToArray();
        Assert(downs.Length == 2 && downs.Count(d => d.Pinned) == 1, $"two give-ups leave exactly one pinned DOWN notice, got {downs.Count(d => d.Pinned)} pinned");
    }, failures);
    RunScenarioAsync("Operation history is reloaded after a restart, and an operation cut off mid-run is closed as Interrupted instead of staying Running", async () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "operations-" + Guid.NewGuid().ToString("N")));
        var profile = new MystTiq.Core.Operations.ServerProfileId("default");
        var before = new MystTiq.Core.Operations.OperationCoordinator(paths);
        var done = await before.BeginAsync(profile, "backup", "harness", ["world"], CancellationToken.None);
        before.Complete(done.Id, "finished");
        done.Dispose();
        var cutOff = await before.BeginAsync(profile, "restore", "harness", ["world"], CancellationToken.None);
        before.Advance(cutOff.Id, "SafetyBackupCreated", "made a safety backup");
        // MystTiq stops here: the restore never completes.

        var after = new MystTiq.Core.Operations.OperationCoordinator(paths);
        var history = after.ListRecent(10);
        Assert(history.Count == 2, $"both operations are back after the restart, got {history.Count}");
        var finished = after.Find(done.Id)!;
        Assert(finished.Phase == MystTiq.Core.Operations.OperationPhase.Completed, "a completed operation stays completed");
        var interrupted = after.Find(cutOff.Id)!;
        Assert(interrupted.Phase == MystTiq.Core.Operations.OperationPhase.Failed && interrupted.Stages.Last().State == MystTiq.Core.Operations.OperationCoordinator.InterruptedState,
            "the cut-off one is closed as Failed with an Interrupted stage");
        Assert(interrupted.Stages.Any(s => s.State == "SafetyBackupCreated"), "and keeps the stages it reached");
        var third = new MystTiq.Core.Operations.OperationCoordinator(paths);
        Assert(third.Find(cutOff.Id)!.Stages.Count(s => s.State == MystTiq.Core.Operations.OperationCoordinator.InterruptedState) == 1, "the Interrupted stage was saved, so it is not added again on the next restart");
        var retry = await after.BeginAsync(profile, "restore", "harness", ["world"], CancellationToken.None);
        Assert(retry.Id.Value.Length > 0, "the dead operation's resource lock is not restored, so the same resource can be used again");
    }, failures);
    RunScenario("Crash alert text: says what happened, what is being done, the likely cause and the first thing to try", () =>
    {
        var now = DateTimeOffset.UtcNow;
        SupervisorEvent E(SupervisorEventKind k, int attempt = 1, string detail = "") => new(k, attempt, 3, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10), detail, now);
        HeadlessCrashAnalysisSnapshot A(params HeadlessCrashFinding[] f) => new("id", now, 1, 10, f, [], "s", f.Count(x => x.IsNew), f.Count(x => !x.IsNew));
        var oom = CrashAnalysisBuilder.Build(["Out of memory while allocating 4096 bytes", "PalGuideMod crashed"], ["Pal Guide Mod"], null);

        var crash = CrashAlertText.Build("My Server", E(SupervisorEventKind.CrashDetected), A(oom.ToArray()));
        Assert(crash.Severity == "Critical" && !crash.Pinned && crash.Title == "My Server: server crashed, restarting (attempt 1 of 3)", $"crash title: '{crash.Title}'");
        Assert(crash.Message.Contains("restarting it in 10 second(s)") && crash.Message.Contains("Out of memory") && crash.Message.Contains("First thing to try:"), $"crash message: '{crash.Message}'");
        var withMods = CrashAlertText.Build("S", E(SupervisorEventKind.CrashDetected), A(CrashAnalysisBuilder.Build(["Fatal error: PalGuideMod crashed"], ["Pal Guide Mod"], null).ToArray()));
        Assert(withMods.Message.Contains("Pal Guide Mod") && withMods.Message.Contains("a lead, not proof"), "a mod named in the evidence is mentioned as a lead");

        var repeated = CrashAnalysisBuilder.Build(["Out of memory"], null, oom.Select(x => x.Key).ToHashSet());
        var old = CrashAlertText.Build("S", E(SupervisorEventKind.CrashDetected), A(CrashAnalysisBuilder.Build(["Out of memory"], null, new HashSet<string>(CrashAnalysisBuilder.Build(["Out of memory"], null, null).Select(x => x.Key))).ToArray()));
        Assert(old.Message.Contains("no new crash evidence") && !old.Message.Contains("Likely cause"), $"an already-reported finding must not be presented as the cause: '{old.Message}'");
        Assert(CrashAlertText.Build("S", E(SupervisorEventKind.CrashDetected), A()).Message.Contains("no known crash signature"), "no findings says so");
        Assert(CrashAlertText.Build("S", E(SupervisorEventKind.CrashDetected), null).Message == "PalServer stopped unexpectedly. MystTiq is restarting it in 10 second(s).", "without an analysis the message is just the facts");

        var ok = CrashAlertText.Build("S", E(SupervisorEventKind.RecoverySucceeded, 2, "PalServer is running again (PID 5)."), null);
        Assert(ok.Severity == "Success" && ok.Title == "S: server is back up" && ok.Message.Contains("restart attempt 2 of 3"), "recovery is a success notice");
        var failedMore = CrashAlertText.Build("S", E(SupervisorEventKind.RecoveryFailed, 1, "LaunchFailed: x"), null);
        var failedLast = CrashAlertText.Build("S", E(SupervisorEventKind.RecoveryFailed, 3, "LaunchFailed: x"), null);
        Assert(failedMore.Severity == "Warning" && failedMore.Message.Contains("2 attempt(s) left") && failedLast.Message.Contains("last attempt"), "a failed restart says whether more attempts remain");
        var down = CrashAlertText.Build("S", E(SupervisorEventKind.RecoverySuppressed, 3, "Automatic recovery gave up."), null);
        Assert(down.Severity == "Critical" && down.Pinned && down.Title.Contains("DOWN") && down.Message.Contains("start it manually"), "giving up is a pinned critical alert that says the server is down");
        Assert(CrashAlertText.Build("", E(SupervisorEventKind.RecoverySucceeded), null).Title.StartsWith("Palworld server:"), "a blank server name falls back to a generic one");
    }, failures);
    RunScenarioAsync("Crash alert observer sends a real notification with the log analysis, and a second crash on the same logs reads as no new evidence", async () =>
    {
        var root = Path.Combine(tempRoot, "crash-alerts");
        var paths = new FakePathProfile(root);
        Directory.CreateDirectory(paths.LogsRoot);
        File.WriteAllLines(Path.Combine(paths.LogsRoot, "PalServer.log"), ["[2026.09.21-08.00.00:000] normal line", "[2026.09.21-08.10.00:000] Out of memory while allocating 4096 bytes"]);
        var activity = new HeadlessActivityLogService(paths);
        var notifications = new HeadlessNotificationService(paths, activity);
        var observer = new CrashAlertObserver("Harness Server", notifications, new HeadlessCrashAndSaveToolsService(paths, activity));
        var crash = new SupervisorEvent(SupervisorEventKind.CrashDetected, 1, 3, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(10), "", DateTimeOffset.UtcNow);

        await observer.OnEventAsync(crash, CancellationToken.None);
        var first = notifications.GetSnapshot().Items.Single();
        Assert(first.Severity == "Critical" && first.Title.Contains("Harness Server: server crashed") && first.Message.Contains("Out of memory") && first.Message.Contains("Likely cause"), $"first crash: '{first.Message}'");

        await observer.OnEventAsync(crash with { Attempt = 2 }, CancellationToken.None);
        var second = notifications.GetSnapshot().Items.First(i => i.Title.Contains("attempt 2 of 3"));
        Assert(second.Message.Contains("no new crash evidence"), $"the same logs again are not a new cause: '{second.Message}'");

        await observer.OnEventAsync(crash with { Kind = SupervisorEventKind.RecoverySuppressed, Attempt = 3, Detail = "Gave up." }, CancellationToken.None);
        var down = notifications.GetSnapshot().Items.Single(i => i.Title.Contains("DOWN"));
        Assert(down.Pinned && down.Severity == "Critical", "the give-up alert is pinned and critical");
        Assert(crashHistoryCount(paths) == 2, "only the two crash detections analysed the logs; giving up reuses the last analysis");
        static int crashHistoryCount(FakePathProfile p) => Directory.GetFiles(Path.Combine(p.ManagerRuntimeRoot, "crash-analyzer"), "crash-*.json").Length;

        // v0.7.110.0: the server comes back up later (an admin started it, not the loop, since the loop
        // itself had given up) -- the pinned "DOWN" notice must unpin itself, the same way an Alert
        // Center episode's pinned alert already does on recovery (v0.7.104.0), even though this is a
        // completely separate mechanism with no AlertEpisodeTracker involved.
        var downId = down.Id;
        await observer.OnEventAsync(new SupervisorEvent(SupervisorEventKind.ManualRecovery, 0, 3, TimeSpan.Zero, TimeSpan.FromMinutes(10), "PalServer is running again.", DateTimeOffset.UtcNow), CancellationToken.None);
        var backUp = notifications.GetSnapshot().Items.Single(i => i.Title.Contains("back up"));
        Assert(backUp.Severity == "Success" && !backUp.Pinned, "the back-up notice is a plain Success notice, not pinned");
        var downAfter = notifications.GetSnapshot().Items.Single(i => i.Id == downId);
        Assert(!downAfter.Pinned, "the original DOWN notice unpins itself once the server is confirmed back up");
        Assert(notifications.GetSnapshot().Items.Count(i => i.Title.Contains("DOWN")) == 1, "the DOWN notice is unpinned in place, not duplicated or removed");

        // A second ManualRecovery (e.g. a duplicate notification, or nothing left to unpin) must not throw.
        await observer.OnEventAsync(new SupervisorEvent(SupervisorEventKind.ManualRecovery, 0, 3, TimeSpan.Zero, TimeSpan.FromMinutes(10), "still up", DateTimeOffset.UtcNow), CancellationToken.None);
    }, failures);

    // Simulates exactly what an Avalonia Slider does when bound: clamp into range, snap to the tick, and
    // write the result back through the bound property.
    static void SliderBindsTo(MystTiq.Desktop.Models.PalworldSimpleSettingItem item)
    {
        var raw = item.SliderValue;
        var clamped = Math.Clamp(raw, item.Minimum, item.Maximum);
        var snapped = item.Step > 0 ? Math.Round(clamped / item.Step) * item.Step : clamped;
        if (Math.Abs(snapped - raw) > 1e-12) item.SliderValue = snapped;
    }
    static MystTiq.Desktop.Models.PalworldSimpleSettingItem RateItem(string value, double min, double max, double step, string defaultValue = "1.000000")
    {
        var dto = new MystTiq.Desktop.Models.PalworldSettingDto { Name = "Rate", Value = value, DefaultValue = defaultValue };
        dto.MarkClean();
        return new MystTiq.Desktop.Models.PalworldSimpleSettingItem(dto, "Rate", "d", min, max, step, "x");
    }

    RunScenario("Configuration sliders never rewrite a value the file already holds: out of range, off the tick, or oddly formatted", () =>
    {
        // The real cases from the test server: 0.05 under a 0.1 minimum, plus the other ways a hand-edited file differs.
        (string Value, double Min, double Max, double Step)[] cases =
        [
            ("0.05", 0.1, 10, 0.1),      // below the minimum (the bug that was seen)
            ("25", 0.1, 20, 0.1),        // above the maximum
            ("1.23", 0.1, 10, 0.1),      // between two ticks
            ("1.000000", 0.1, 5, 0.05),  // formatted with trailing zeros
            ("1.50", 0.1, 10, 0.1),      // trailing zero
            ("2.0", 0.1, 10, 0.1),
            ("0", 0.1, 10, 0.1),         // zero under a nonzero minimum
            ("0.333333", 0.1, 5, 0.05),
        ];
        foreach (var (value, min, max, step) in cases)
        {
            var item = RateItem(value, min, max, step);
            SliderBindsTo(item);
            Assert(item.Setting.Value == value && !item.IsDirty, $"binding the slider must not touch '{value}' (range {min}..{max}, step {step}), but the setting became '{item.Setting.Value}' dirty={item.IsDirty}");
        }
        // Repeated binding, as happens on every filter or category change, stays quiet too.
        var again = RateItem("0.05", 0.1, 10, 0.1);
        for (var i = 0; i < 5; i++) SliderBindsTo(again);
        Assert(!again.IsDirty && again.Setting.Value == "0.05", "rebinding repeatedly must stay clean");
        Assert(again.ValueText.StartsWith("0.05"),$"the readout must show the file's real 0.05, not the slider's minimum: '{again.ValueText}'");
    }, failures);
    RunScenario("Configuration sliders still record a genuine change, and only a genuine one", () =>
    {
        var item = RateItem("1.5", 0.1, 10, 0.1);
        item.SliderValue = 2.0;
        Assert(item.Setting.Value == "2" && item.IsDirty, $"a real drag to 2.0 must be written, got '{item.Setting.Value}' dirty={item.IsDirty}");
        item.SliderValue = 1.5;
        Assert(item.Setting.Value == "1.5" && !item.IsDirty, "dragging back to the original value leaves the setting clean again");

        var low = RateItem("0.05", 0.1, 10, 0.1);
        low.SliderValue = 0.7;
        Assert(low.Setting.Value == "0.7" && low.IsDirty, "moving an out-of-range value to a real position must be recorded");
        var snapped = RateItem("1.0", 0.1, 10, 0.1);
        snapped.SliderValue = 3.04;
        Assert(snapped.Setting.Value == "3" , $"a drag is still snapped to the tick, got '{snapped.Setting.Value}'");
        var beyond = RateItem("1.0", 0.1, 5, 0.05);
        beyond.SliderValue = 99;
        Assert(beyond.Setting.Value == "5", $"a drag past the end is still clamped to the maximum, got '{beyond.Setting.Value}'");
        Assert(RateItem("0.05", 0.1, 10, 0.1).IsCoercionEcho(0.1, 0.05) && !RateItem("0.05", 0.1, 10, 0.1).IsCoercionEcho(0.2, 0.05), "only the exact coerced value is an echo");
    }, failures);

    RunScenario("Alert episodes: a condition alerts once when it starts, stays silent while true, and says once when it clears", () =>
    {
        var tracker = new AlertEpisodeTracker();
        var t0 = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var guard = TimeSpan.FromMinutes(60);
        Assert(tracker.Observe("low-disk", false, t0, guard) == EpisodeAction.None, "a healthy condition says nothing");
        Assert(tracker.Observe("low-disk", true, t0.AddMinutes(1), guard) == EpisodeAction.Alert, "the first time it is true it alerts");
        // The old behaviour re-alerted every cooldown for as long as the condition held: 83 unread notifications.
        for (var minute = 2; minute <= 600; minute += 7)
            Assert(tracker.Observe("low-disk", true, t0.AddMinutes(minute), guard) == EpisodeAction.None, $"still true at +{minute} minutes must stay silent");
        Assert(tracker.Observe("low-disk", false, t0.AddMinutes(601), guard) == EpisodeAction.Recovered, "clearing sends exactly one recovery");
        Assert(tracker.Observe("low-disk", false, t0.AddMinutes(602), guard) == EpisodeAction.None, "and nothing after that");
        Assert(tracker.Observe("other-rule", true, t0.AddMinutes(603), guard) == EpisodeAction.Alert, "rules are tracked independently");
    }, failures);
    RunScenario("Alert episodes: a flapping condition is suppressed, a new episode after the guard alerts again, and switching a rule off closes it", () =>
    {
        var tracker = new AlertEpisodeTracker();
        var t0 = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var guard = TimeSpan.FromMinutes(30);
        Assert(tracker.Observe("r", true, t0, guard) == EpisodeAction.Alert, "first alert");
        Assert(tracker.Observe("r", false, t0.AddMinutes(5), guard) == EpisodeAction.Recovered, "recovery");
        Assert(tracker.Observe("r", true, t0.AddMinutes(10), guard) == EpisodeAction.None, "it returns inside the guard: no second alert");
        Assert(tracker.Observe("r", false, t0.AddMinutes(12), guard) == EpisodeAction.None, "and its clearing is not announced, since no alert was sent for that episode");
        Assert(tracker.Observe("r", true, t0.AddMinutes(90), guard) == EpisodeAction.Alert, "a genuinely new episode after the guard alerts again");
        tracker.Close("r");
        Assert(!tracker.IsActive("r"), "switching the rule off closes the episode");
        Assert(tracker.Observe("r", false, t0.AddMinutes(91), guard) == EpisodeAction.None, "so a closed episode never sends a recovery");
    }, failures);
    RunScenario("Alert episodes survive a restart: a condition that was already alerted does not alert again", () =>
    {
        var path = Path.Combine(tempRoot, "episodes-" + Guid.NewGuid().ToString("N") + ".json");
        var t0 = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var guard = TimeSpan.FromMinutes(60);
        var first = new AlertEpisodeTracker(path);
        Assert(first.Observe("low-disk", true, t0, guard) == EpisodeAction.Alert, "alerts once");
        var afterRestart = new AlertEpisodeTracker(path);
        Assert(afterRestart.IsActive("low-disk"), "the open episode was persisted");
        Assert(afterRestart.Observe("low-disk", true, t0.AddMinutes(200), guard) == EpisodeAction.None, "a restart must not re-alert a condition that is still true");
        Assert(afterRestart.Observe("low-disk", false, t0.AddMinutes(201), guard) == EpisodeAction.Recovered, "and the recovery still arrives");
        File.WriteAllText(path, "{ this is not json");
        Assert(new AlertEpisodeTracker(path).Observe("low-disk", true, t0, guard) == EpisodeAction.Alert, "a damaged state file starts fresh instead of failing");
    }, failures);
    RunScenario("Disk rule: the Doctor and the Alert Center now reach the same verdict for the same disk", () =>
    {
        const long GiB = 1024L * 1024 * 1024;
        // The real case: 51.9 GB free of 953 GB (5.4%). The Alert Center called it Critical, the Doctor called it Pass.
        var free = (long)(51.9 * GiB); var total = 953L * GiB;
        Assert(DiskSpaceRules.Evaluate(free, total, 10) == DiskLevel.Critical, "5.4% free is Critical under a 10% rule");
        Assert(DoctorHealthRules.DiskSpace("x", free, total, 0, 10).State == DiagnosticState.Fail, "and the Doctor now fails it too");
        Assert(DoctorHealthRules.DiskSpace("x", free, total).State == DiagnosticState.Pass, "with the percentage rule off, 51.9 GB free is fine");
        Assert(DiskSpaceRules.Evaluate(free, total, null) == DiskLevel.Ok, "a disabled rule does not raise a percentage verdict");
        Assert(DiskSpaceRules.Evaluate(1 * GiB, 500 * GiB, null) == DiskLevel.Critical, "under 2 GiB is Critical whatever the percentage");
        Assert(DiskSpaceRules.Evaluate(4 * GiB, 500 * GiB, null) == DiskLevel.Warning, "under 5 GiB is a Warning");
        Assert(DiskSpaceRules.Evaluate(320 * GiB, 953 * GiB, 10) == DiskLevel.Ok, "the Default Server's real 33.6% free is fine");
        Assert(DiskSpaceRules.Evaluate(100 * GiB, 0, 10) == DiskLevel.Ok, "an unknown size never raises a percentage alert");
        Assert(DoctorHealthRules.DiskSpace("x", 320 * GiB, 953 * GiB, 0, 10).Evidence.Contains("33.6%"), "the evidence states the percentage");
    }, failures);
    RunScenarioAsync("A stopped server is not a network error, but a crashed one is", async () =>
    {
        var service = new NetworkDiagnosticsService(NetworkDiagnosticsPlatformService.ForCurrentPlatform());
        ServerLifecycleSnapshot Snapshot(ServerLifecyclePhase phase, bool crash) => new(phase, null, [], [], false, crash, DateTimeOffset.UtcNow, null, "test");
        var stopped = await service.RunAsync(Snapshot(ServerLifecyclePhase.Stopped, false), [], TimeSpan.FromSeconds(1));
        Assert(stopped.NetworkHealth == NetworkHealthState.NotRunning, $"a server stopped on purpose is NotRunning, got {stopped.NetworkHealth}");
        Assert(stopped.Checks.First(c => c.Test == "PalServer Process").State == DiagnosticState.Skipped, "and its process row is informational, not a failure");
        var crashed = await service.RunAsync(Snapshot(ServerLifecyclePhase.Crashed, true), [], TimeSpan.FromSeconds(1));
        Assert(crashed.NetworkHealth == NetworkHealthState.Error && crashed.Checks.First(c => c.Test == "PalServer Process").State == DiagnosticState.Fail, "a crash is still an error");
        Assert(crashed.Checks.First(c => c.Test == "PalServer Process").Details.Contains("crashed"), "and says so");
        Assert((int)NetworkHealthState.Error == 4 && (int)NetworkHealthState.NotRunning == 5, "the new state is appended so the numbers the Desktop reads keep their meaning");
    }, failures);
    RunScenario("Update Center says when this install is newer than the latest published release", () =>
    {
        var now = DateTimeOffset.UtcNow;
        var ahead = HeadlessComponentUpdateService.Compare("Core Server", "MystTiq Server Manager", "0.7.101.0", "0.2.16.4", "GitHub", now, "Latest published release: v0.2.16.4.", true);
        Assert(ahead.Status == "UpToDate" && ahead.Detail.Contains("newer than the latest published release") && ahead.Detail.Contains("local or unreleased build"), $"ahead: '{ahead.Detail}'");
        var same = HeadlessComponentUpdateService.Compare("Core Server", "MystTiq Server Manager", "0.2.16.4", "0.2.16.4", "GitHub", now, "d", true);
        Assert(same.Status == "UpToDate" && !same.Detail.Contains("newer than"), "an install equal to the release is just up to date");
        var behind = HeadlessComponentUpdateService.Compare("Core Server", "MystTiq Server Manager", "0.2.0.0", "0.2.16.4", "GitHub", now, "d", true);
        Assert(behind.Status == "UpdateAvailable" && !behind.Detail.Contains("newer than"), "an older install still reports an update");
    }, failures);

    RunScenario("Doctor scheduled backups: no rule offers the one-click fix, and only then; a rule that exists never invites a duplicate", () =>
    {
        static BackupScheduleRule R(string name, bool enabled, AutomationTriggerKind kind, TimeSpan? interval = null, AutomationDayOfWeekMask days = AutomationDayOfWeekMask.All, string? lastState = null, string? lastDetail = null) =>
            new(name, enabled, kind, interval, days, new TimeOnly(3, 0), lastState, lastDetail);

        var none = DoctorHealthRules.ScheduledBackups([]);
        Assert(none.Result.State == DiagnosticState.Warning && none.CanCreateRule && none.Result.Evidence.Contains("only exists when someone presses Backup"), "no backup rule warns and offers the fix");

        var off = DoctorHealthRules.ScheduledBackups([R("Nightly", false, AutomationTriggerKind.DailyTime)]);
        Assert(off.Result.State == DiagnosticState.Warning && !off.CanCreateRule && off.Result.Evidence.Contains("switched off") && off.Result.Evidence.Contains("\"Nightly\""), "a switched-off rule warns but must not offer a second rule");

        var idle = DoctorHealthRules.ScheduledBackups([R("When empty", true, AutomationTriggerKind.IdleEmpty)]);
        Assert(idle.Result.State == DiagnosticState.Warning && !idle.CanCreateRule && idle.Result.Evidence.Contains("only when the server has been empty"), "an idle-only rule is not a schedule");
        var noDays = DoctorHealthRules.ScheduledBackups([R("Never", true, AutomationTriggerKind.DailyTime, days: AutomationDayOfWeekMask.None)]);
        Assert(noDays.Result.State == DiagnosticState.Warning && !noDays.CanCreateRule, "a daily rule on no days never runs");
        var monthly = DoctorHealthRules.ScheduledBackups([R("Slow", true, AutomationTriggerKind.Interval, TimeSpan.FromDays(30))]);
        Assert(monthly.Result.State == DiagnosticState.Warning && monthly.Result.Evidence.Contains("30 day(s)"), $"a 30-day interval is too slow: '{monthly.Result.Evidence}'");

        var daily = DoctorHealthRules.ScheduledBackups([R("Nightly backup", true, AutomationTriggerKind.DailyTime)]);
        Assert(daily.Result.State == DiagnosticState.Pass && !daily.CanCreateRule && daily.Result.Evidence.Contains("every day at 03:00 UTC"), $"a daily rule passes: '{daily.Result.Evidence}'");
        Assert(DoctorHealthRules.ScheduledBackups([R("Six-hourly", true, AutomationTriggerKind.Interval, TimeSpan.FromHours(6))]).Result.State == DiagnosticState.Pass, "a 6-hour interval passes");
        Assert(DoctorHealthRules.ScheduledBackups([R("Weekly", true, AutomationTriggerKind.Interval, TimeSpan.FromDays(7))]).Result.State == DiagnosticState.Pass, "exactly weekly passes");
        Assert(DoctorHealthRules.ScheduledBackups([R("Weekdays", true, AutomationTriggerKind.DailyTime, days: AutomationDayOfWeekMask.Monday | AutomationDayOfWeekMask.Thursday)]).Result.Evidence.Contains("selected days"), "a partial week is described as selected days");

        var mixed = DoctorHealthRules.ScheduledBackups([R("Off", false, AutomationTriggerKind.DailyTime), R("On", true, AutomationTriggerKind.DailyTime)]);
        Assert(mixed.Result.State == DiagnosticState.Pass && mixed.Result.Evidence.Contains("\"On\""), "one good enabled rule is enough, and it is the one named");
    }, failures);
    RunScenario("Doctor scheduled backups warn when the schedule exists but keeps failing", () =>
    {
        static BackupScheduleRule R(string name, string? state, string? detail) => new(name, true, AutomationTriggerKind.DailyTime, null, AutomationDayOfWeekMask.All, new TimeOnly(3, 0), state, detail);
        var failed = DoctorHealthRules.ScheduledBackups([R("Nightly", "Failed", "The disk is full.")]);
        Assert(failed.Result.State == DiagnosticState.Warning && !failed.CanCreateRule && failed.Result.Evidence.Contains("failed on its last run") && failed.Result.Evidence.Contains("The disk is full."), $"a failing rule warns with the reason: '{failed.Result.Evidence}'");
        Assert(DoctorHealthRules.ScheduledBackups([R("Nightly", "Completed", "ok")]).Result.State == DiagnosticState.Pass, "a completed last run passes");
        Assert(DoctorHealthRules.ScheduledBackups([R("Nightly", null, null)]).Result.State == DiagnosticState.Pass, "a rule that has not run yet passes");
        Assert(DoctorHealthRules.ScheduledBackups([R("Nightly", "Missed", "server was off")]).Result.State == DiagnosticState.Pass, "a missed run is not a failure");
        var oneOfTwo = DoctorHealthRules.ScheduledBackups([R("A", "Failed", "x"), R("B", "Completed", "ok")]);
        Assert(oneOfTwo.Result.State == DiagnosticState.Pass, "if another scheduled rule is healthy, one failing rule does not warn");
    }, failures);

    RunScenario("Alert episodes hand back the alert's notification id when the episode ends, so it can be unpinned", () =>
    {
        var tracker = new AlertEpisodeTracker();
        var t0 = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var guard = TimeSpan.FromMinutes(60);

        Assert(tracker.Observe("low-disk", true, t0, guard, out var onAlert) == EpisodeAction.Alert && onAlert is null, "no id comes back on the alert itself");
        tracker.SetAlertNotificationId("low-disk", "note-1");
        Assert(tracker.Observe("low-disk", true, t0.AddMinutes(5), guard, out var stillOpen) == EpisodeAction.None && stillOpen is null, "no id while the episode stays open");
        Assert(tracker.Observe("low-disk", false, t0.AddMinutes(10), guard, out var recovered) == EpisodeAction.Recovered && recovered == "note-1", $"recovery hands back the alert's own notification id, got '{recovered}'");
        Assert(tracker.Observe("low-disk", false, t0.AddMinutes(11), guard, out var again) == EpisodeAction.None && again is null, "nothing more to unpin once already recovered");

        Assert(tracker.Observe("cpu", true, t0, guard, out _) == EpisodeAction.Alert, "second rule alerts");
        tracker.SetAlertNotificationId("cpu", "note-2");
        Assert(tracker.Close("cpu") == "note-2", "switching a pinned rule off (no Resolved notice) still hands back its notification id to unpin");
        Assert(tracker.Close("cpu") is null, "closing an already-closed episode has nothing left to unpin");
        Assert(tracker.Close("never-alerted") is null, "a rule that never alerted has nothing to unpin either");

        Assert(tracker.Observe("silent", true, t0, guard, out _) == EpisodeAction.Alert, "third rule alerts");
        // SetAlertNotificationId is never called here: Close before it happens must not throw or fabricate an id.
        Assert(tracker.Close("silent") is null, "closing before the id was ever recorded returns null, not a stale value");
    }, failures);
    RunScenario("Alert episodes: a condition that stays true reminds once per interval, and a fresh episode or recovery resets the clock", () =>
    {
        var tracker = new AlertEpisodeTracker();
        var t0 = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var guard = TimeSpan.FromMinutes(60);
        var reminder = TimeSpan.FromHours(24);

        Assert(tracker.Observe("low-disk", true, t0, guard, out _, reminder) == EpisodeAction.Alert, "the alert itself is not a reminder");
        Assert(tracker.Observe("low-disk", true, t0.AddHours(1), guard, out _, reminder) == EpisodeAction.None, "well inside the interval: nothing yet");
        Assert(tracker.Observe("low-disk", true, t0.AddHours(23), guard, out _, reminder) == EpisodeAction.None, "still not a full interval since the alert");
        Assert(tracker.Observe("low-disk", true, t0.AddHours(24), guard, out _, reminder) == EpisodeAction.Reminder, "exactly one interval since the alert reminds");
        Assert(tracker.Observe("low-disk", true, t0.AddHours(25), guard, out _, reminder) == EpisodeAction.None, "right after a reminder: nothing yet");
        Assert(tracker.Observe("low-disk", true, t0.AddHours(48), guard, out _, reminder) == EpisodeAction.Reminder, "a full interval since the LAST reminder (not the original alert) reminds again");
        Assert(tracker.Observe("low-disk", true, t0.AddHours(49), guard, out _, reminder) == EpisodeAction.None, "and stays quiet again until the next interval");

        Assert(tracker.Observe("low-disk", false, t0.AddHours(50), guard, out _, reminder) == EpisodeAction.Recovered, "recovering ends the reminder cycle");
        Assert(tracker.Observe("low-disk", true, t0.AddHours(50.5), guard, out _, reminder) == EpisodeAction.Alert, "a genuinely new episode (well outside the flap guard) alerts fresh");
        Assert(tracker.Observe("low-disk", true, t0.AddHours(74), guard, out _, reminder) == EpisodeAction.None, "the new episode's reminder clock started over, not carried from the old one (24h since the OLD alert would have already elapsed)");
        Assert(tracker.Observe("low-disk", true, t0.AddHours(74.5), guard, out _, reminder) == EpisodeAction.Reminder, "and reminds a full interval after the NEW alert");

        var noReminders = new AlertEpisodeTracker();
        Assert(noReminders.Observe("r", true, t0, guard, out _) == EpisodeAction.Alert, "no reminderInterval argument at all (every pre-v0.7.107.0 call site): unaffected");
        Assert(noReminders.Observe("r", true, t0.AddYears(1), guard, out _) == EpisodeAction.None, "still nothing a year later with reminders never opted into");
        Assert(noReminders.Observe("r", true, t0.AddYears(1), guard, out _, null) == EpisodeAction.None, "and explicitly passing null (the disabled default) behaves identically");

        var disabled = new AlertEpisodeTracker();
        Assert(disabled.Observe("r", true, t0, guard, out _, TimeSpan.Zero) == EpisodeAction.Alert, "a zero reminder interval is treated as disabled, not an immediate reminder");
        Assert(disabled.Observe("r", true, t0.AddHours(1), guard, out _, TimeSpan.Zero) == EpisodeAction.None, "so it never reminds");
    }, failures);
    RunScenario("Alert episodes: switching a reminding rule off resets the reminder clock too", () =>
    {
        var tracker = new AlertEpisodeTracker();
        var t0 = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var guard = TimeSpan.FromMinutes(60);
        var reminder = TimeSpan.FromHours(24);

        Assert(tracker.Observe("r", true, t0, guard, out _, reminder) == EpisodeAction.Alert, "alerts");
        Assert(tracker.Observe("r", true, t0.AddHours(24), guard, out _, reminder) == EpisodeAction.Reminder, "reminds once (LastReminderAt = t0+24h if the clock were not about to be reset)");
        tracker.Close("r");
        Assert(tracker.Observe("r", true, t0.AddHours(24.5), guard, out _, reminder) == EpisodeAction.Alert, "switching off and back on, well outside the flap guard, starts a genuinely new episode (new LastAlertAt = t0+24.5h)");
        // If Close had NOT reset LastReminderAt, a stale t0+24h would make this fire a Reminder here (48h
        // is a full 24h past the stale value); the new episode's own clock (from t0+24.5h) puts that a full
        // interval away at t0+48.5h instead, so t0+48h must still be quiet.
        Assert(tracker.Observe("r", true, t0.AddHours(48), guard, out _, reminder) == EpisodeAction.None, "the reminder clock did not carry the old episode's stale timestamp into the new one");
        Assert(tracker.Observe("r", true, t0.AddHours(48.5), guard, out _, reminder) == EpisodeAction.Reminder, "and reminds a full interval after the NEW episode's own alert");
    }, failures);
    RunScenario("AlertRuleSet's reminder cadence defaults to 24 hours and round-trips through JSON like every other rule field", () =>
    {
        var fresh = new AlertRuleSet();
        Assert(fresh.ReminderMinutes == 1440, $"a brand new rule set defaults to 1440 minutes (24 hours), got {fresh.ReminderMinutes}");

        var configured = new AlertRuleSet { ReminderMinutes = 5 };
        var json = System.Text.Json.JsonSerializer.Serialize(configured);
        var reloaded = System.Text.Json.JsonSerializer.Deserialize<AlertRuleSet>(json);
        Assert(reloaded is not null && reloaded.ReminderMinutes == 5, $"a configured value round-trips through the same JSON persistence every other rule field uses, got {reloaded?.ReminderMinutes}");

        var savedBeforeThisVersion = """{"HighCpuSustained":{"Enabled":false,"ThresholdPercent":90,"CooldownMinutes":30},"HighMemorySustained":{"Enabled":false,"ThresholdMb":8192,"CooldownMinutes":30},"LowDiskSpace":{"Enabled":true,"ThresholdPercent":10,"CooldownMinutes":60},"DiskSpaceExhaustionPredicted":{"Enabled":true,"ThresholdDays":7,"CooldownMinutes":1440},"ModHealthDegraded":{"Enabled":true,"CooldownMinutes":60}}""";
        var upgraded = System.Text.Json.JsonSerializer.Deserialize<AlertRuleSet>(savedBeforeThisVersion);
        Assert(upgraded is not null && upgraded.ReminderMinutes == 1440, $"a rules.json saved before v0.7.108.0 (no reminderMinutes field at all) loads with the same 24-hour default, not zero/disabled, got {upgraded?.ReminderMinutes}");
        // v0.7.111.0: the same pre-existing file gets crash alerts ON and no mute, never silently muted.
        Assert(upgraded!.CrashAlerts is { Enabled: true, RecoveryNotices: true } && upgraded.MutedUntilUtc is null, "an older rules.json loads with crash alerts on and nothing muted");
    }, failures);
    RunScenario("Alert mute policy: a mute ends on its own, is capped at 30 days, and crash alert settings decide what is sent", () =>
    {
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        Assert(!AlertMutePolicy.IsMuted(new AlertRuleSet(), now), "no end time means not muted");
        Assert(AlertMutePolicy.IsMuted(new AlertRuleSet { MutedUntilUtc = now.AddMinutes(1) }, now), "an end time in the future is muted");
        Assert(!AlertMutePolicy.IsMuted(new AlertRuleSet { MutedUntilUtc = now }, now) && !AlertMutePolicy.IsMuted(new AlertRuleSet { MutedUntilUtc = now.AddMinutes(-1) }, now), "a mute whose end time has arrived or passed is over, with nothing to switch back on");
        Assert(AlertMutePolicy.MuteUntil(60, now) == now.AddHours(1), "Mute 1 hour ends an hour from now");
        Assert(AlertMutePolicy.MuteUntil(0, now) is null && AlertMutePolicy.MuteUntil(-5, now) is null, "0 or less unmutes");
        Assert(AlertMutePolicy.MuteUntil(int.MaxValue, now) == now.AddMinutes(AlertMutePolicy.MaximumMuteMinutes), "a huge value is capped at 30 days, not an overflow");

        var on = new AlertRuleSet();
        foreach (var kind in new[] { SupervisorEventKind.CrashDetected, SupervisorEventKind.RecoveryFailed, SupervisorEventKind.RecoverySuppressed, SupervisorEventKind.RecoverySucceeded, SupervisorEventKind.ManualRecovery })
            Assert(AlertMutePolicy.ShouldSendCrashAlert(on, kind, now), $"defaults send {kind}");
        var muted = new AlertRuleSet { MutedUntilUtc = now.AddHours(1) };
        Assert(!AlertMutePolicy.ShouldSendCrashAlert(muted, SupervisorEventKind.RecoverySuppressed, now), "a mute silences even the give-up alert");
        var off = new AlertRuleSet { CrashAlerts = new CrashAlertRule(false, true) };
        Assert(!AlertMutePolicy.ShouldSendCrashAlert(off, SupervisorEventKind.CrashDetected, now) && !AlertMutePolicy.ShouldSendCrashAlert(off, SupervisorEventKind.RecoverySucceeded, now), "crash alerts switched off sends nothing, back-up notices included");
        var noBackUp = new AlertRuleSet { CrashAlerts = new CrashAlertRule(true, false) };
        Assert(AlertMutePolicy.ShouldSendCrashAlert(noBackUp, SupervisorEventKind.CrashDetected, now) && AlertMutePolicy.ShouldSendCrashAlert(noBackUp, SupervisorEventKind.RecoverySuppressed, now), "with back-up notices off, crashes and give-ups still alert");
        Assert(!AlertMutePolicy.ShouldSendCrashAlert(noBackUp, SupervisorEventKind.RecoverySucceeded, now) && !AlertMutePolicy.ShouldSendCrashAlert(noBackUp, SupervisorEventKind.ManualRecovery, now), "with back-up notices off, neither kind of back-up notice is sent");

        var nulled = System.Text.Json.JsonSerializer.Deserialize<AlertRuleSet>("""{"CrashAlerts":null}""")!;
        Assert(AlertMutePolicy.ShouldSendCrashAlert(nulled, SupervisorEventKind.CrashDetected, now), "a client sending crashAlerts:null gets the defaults instead of an exception");
    }, failures);
    RunScenarioAsync("Crash alert observer honours this profile's mute and settings, and still unpins a DOWN notice when the back-up notice is muted", async () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "crash-mute-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(paths.LogsRoot);
        var activity = new HeadlessActivityLogService(paths);
        var notifications = new HeadlessNotificationService(paths, activity);
        var rules = new AlertRuleSet();
        var observer = new CrashAlertObserver("Harness Server", notifications, new HeadlessCrashAndSaveToolsService(paths, activity), rules: () => rules, activity: activity);
        SupervisorEvent E(SupervisorEventKind kind, string detail = "") => new(kind, 1, 3, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(10), detail, DateTimeOffset.UtcNow);

        rules = new AlertRuleSet { MutedUntilUtc = DateTimeOffset.UtcNow.AddHours(1) };
        await observer.OnEventAsync(E(SupervisorEventKind.CrashDetected), CancellationToken.None);
        Assert(notifications.GetSnapshot().Items.Count == 0, "a crash during a mute sends no notification");
        Assert(activity.GetTail(50).Lines.Any(l => l.Contains("Crash alert not sent") && l.Contains("server crashed")), "but the Activity log records the alert that was not sent");

        // Give up while alerts are on: pinned DOWN notice. Then mute, then the server comes back.
        rules = new AlertRuleSet();
        await observer.OnEventAsync(E(SupervisorEventKind.RecoverySuppressed, "Gave up."), CancellationToken.None);
        var down = notifications.GetSnapshot().Items.Single(i => i.Title.Contains("DOWN"));
        Assert(down.Pinned, "the give-up notice is pinned while alerts are on");
        rules = new AlertRuleSet { MutedUntilUtc = DateTimeOffset.UtcNow.AddHours(1) };
        await observer.OnEventAsync(E(SupervisorEventKind.ManualRecovery, "PalServer is running again."), CancellationToken.None);
        Assert(!notifications.GetSnapshot().Items.Any(i => i.Title.Contains("back up")), "the back-up notice is muted");
        Assert(!notifications.GetSnapshot().Items.Single(i => i.Id == down.Id).Pinned, "the DOWN notice pinned before the mute is still unpinned once the server is back");

        // Back-up notices switched off, crash alerts on: a crash alerts, a recovery does not.
        rules = new AlertRuleSet { CrashAlerts = new CrashAlertRule(true, false) };
        var before = notifications.GetSnapshot().Items.Count;
        await observer.OnEventAsync(E(SupervisorEventKind.RecoverySucceeded, "PalServer is responding."), CancellationToken.None);
        Assert(notifications.GetSnapshot().Items.Count == before, "no back-up notice when those are switched off");
        await observer.OnEventAsync(E(SupervisorEventKind.RecoveryFailed, "LaunchFailed: x"), CancellationToken.None);
        Assert(notifications.GetSnapshot().Items.Count == before + 1, "a failed restart still alerts");
    }, failures);
    RunScenario("Alert Center Mute persists in this profile's own rules.json, keeps every other setting, and 0 unmutes", () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "alert-mute-" + Guid.NewGuid().ToString("N")));
        var activity = new HeadlessActivityLogService(paths);
        var notifications = new HeadlessNotificationService(paths, activity);
        HeadlessAlertCenterService Open() => new(paths, new HeadlessHistoricalMetricsService(paths), notifications,
            new HeadlessModManagementService(paths, new ScriptedLifecycle(), activity, new HeadlessConsoleLogWriter(paths)));

        var center = Open();
        center.SaveRules(new AlertRuleSet { ReminderMinutes = 7, CrashAlerts = new CrashAlertRule(true, false) });
        var mutedAt = DateTimeOffset.UtcNow;
        var muted = center.Mute(60);
        Assert(muted.MutedUntilUtc is { } until && until > mutedAt.AddMinutes(59) && until <= DateTimeOffset.UtcNow.AddMinutes(60), $"muted for an hour from now, got {muted.MutedUntilUtc}");
        Assert(muted.ReminderMinutes == 7 && muted.CrashAlerts is { Enabled: true, RecoveryNotices: false }, "muting keeps every other saved setting");

        var reopened = Open().GetRules();
        Assert(reopened.MutedUntilUtc == muted.MutedUntilUtc && reopened.ReminderMinutes == 7, "the mute survives a MystTiq restart");

        var cleared = Open().Mute(0);
        Assert(cleared.MutedUntilUtc is null && !AlertMutePolicy.IsMuted(Open().GetRules(), DateTimeOffset.UtcNow), "Unmute clears it on disk too");

        var other = new FakePathProfile(Path.Combine(tempRoot, "alert-mute-other-" + Guid.NewGuid().ToString("N")));
        var otherCenter = new HeadlessAlertCenterService(other, new HeadlessHistoricalMetricsService(other), new HeadlessNotificationService(other, new HeadlessActivityLogService(other)),
            new HeadlessModManagementService(other, new ScriptedLifecycle(), new HeadlessActivityLogService(other), new HeadlessConsoleLogWriter(other)));
        center.Mute(60);
        Assert(otherCenter.GetRules().MutedUntilUtc is null, "muting one profile does not mute another");
    }, failures);
    RunScenario("Notification create hands back the new id, and unpinning a missing or already-unpinned one is a no-op", () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "notif-unpin-" + Guid.NewGuid().ToString("N")));
        var activity = new HeadlessActivityLogService(paths);
        var notifications = new HeadlessNotificationService(paths, activity);

        notifications.Create("Critical", "Low disk space", "5% free.", true, out var id);
        Assert(!string.IsNullOrEmpty(id), "Create hands back the new notification's id");
        var pinned = notifications.GetSnapshot().Items.Single(x => x.Id == id);
        Assert(pinned.Pinned, "the alert was created pinned");

        var unpinned = notifications.SetPinned(id, false);
        Assert(!unpinned.Items.Single(x => x.Id == id).Pinned, "unpinning by id clears Pinned");
        var unpinnedAgain = notifications.SetPinned(id, false);
        Assert(!unpinnedAgain.Items.Single(x => x.Id == id).Pinned, "unpinning an already-unpinned notification is a harmless no-op");

        AssertThrows<KeyNotFoundException>(() => notifications.SetPinned("does-not-exist", false),
            "unpinning a notification that was already dismissed throws KeyNotFoundException, which the Alert Center is expected to catch and ignore");
    }, failures);
    RunScenario("Doctor fix confirmation: a finding starts unconfirmed, only raises change notification on a real flip, and text is action-specific", () =>
    {
        var finding = new MystTiq.Desktop.Models.DiagnosticFindingDto { Id = "backups-schedule", ActionKind = "create-backup-rule" };
        Assert(finding.CanFix, "a finding with an ActionKind can be fixed");
        Assert(!finding.FixConfirmed, "a fresh finding always starts unconfirmed");
        Assert(finding.FixButtonText == "Create Nightly Backup Rule" && finding.FixConfirmText.Contains("nightly backup rule"), $"button and confirm text both name the actual action: '{finding.FixButtonText}' / '{finding.FixConfirmText}'");

        var raised = new List<string>();
        finding.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");
        finding.FixConfirmed = true;
        Assert(finding.FixConfirmed && raised.SequenceEqual(["FixConfirmed"]), $"ticking it raises exactly one change notification for FixConfirmed, got [{string.Join(", ", raised)}]");
        finding.FixConfirmed = true;
        Assert(raised.Count == 1, "setting it to the same value again must not raise a second notification");
        finding.FixConfirmed = false;
        Assert(!finding.FixConfirmed && raised.Count == 2, "unticking it raises another notification");

        var noAction = new MystTiq.Desktop.Models.DiagnosticFindingDto { Id = "healthy", ActionKind = null };
        Assert(!noAction.CanFix, "a finding with no ActionKind offers no fix");
        Assert(noAction.FixButtonText == "Fix Automatically" && noAction.FixConfirmText == "I understand this runs the fix", "an unrecognised or absent action kind still falls back to generic text rather than throwing");

        var root = new MystTiq.Desktop.Models.DiagnosticFindingDto { ActionKind = "create-backup-root" };
        var install = new MystTiq.Desktop.Models.DiagnosticFindingDto { ActionKind = "install-distribution" };
        Assert(root.FixConfirmText.Contains("folder") && install.FixConfirmText.Contains("install"), $"each recognised action kind gets its own confirm text: '{root.FixConfirmText}' / '{install.FixConfirmText}'");
    }, failures);

    RunScenario("KitEntryText parses items, Pals, shorthand, comments and blank lines, and round-trips through Format", () =>
    {
        var ok = KitEntryText.TryParse("# starter\nitem PalSphere 10\n\npal WeaselDragon 5\nMoney 500\nitem Wood:20\nPAL Anubis", out var entries, out var error);
        Assert(ok, $"expected a parse, got '{error}'");
        Assert(entries.Count == 5, $"expected 5 entries, got {entries.Count}");
        Assert(entries[0] == new KitTextEntry("Item", "PalSphere", 10), "item line");
        Assert(entries[1] == new KitTextEntry("Pal", "WeaselDragon", 5), "pal line");
        Assert(entries[2] == new KitTextEntry("Item", "Money", 500), "shorthand line is an item");
        Assert(entries[3] == new KitTextEntry("Item", "Wood", 20), "colon separator");
        Assert(entries[4] == new KitTextEntry("Pal", "Anubis", 1), "a missing level defaults to 1");
        Assert(KitEntryText.TryParse(KitEntryText.Format(entries), out var again, out _) && again.SequenceEqual(entries), "Format then Parse must round-trip");
    }, failures);
    RunScenario("KitEntryText reports the line number for a missing id, a non-number, or extra values", () =>
    {
        Assert(!KitEntryText.TryParse("item PalSphere 10\nitem", out _, out var e1) && e1.StartsWith("Line 2"), $"missing id: '{e1}'");
        Assert(!KitEntryText.TryParse("item PalSphere ten", out _, out var e2) && e2.Contains("not a whole number"), $"non-number: '{e2}'");
        Assert(!KitEntryText.TryParse("pal Anubis 5 9", out _, out var e3) && e3.Contains("too many"), $"extra values: '{e3}'");
        Assert(KitEntryText.TryParse("", out var none, out _) && none.Count == 0, "empty text is a valid empty list");
    }, failures);

    (HeadlessKitService Kits, HeadlessPlayerRegistryService Registry, FakeKitRunner Runner) BuildKits(string scenario, bool canDeliver = true, bool replyFailure = false)
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, scenario));
        var registry = new HeadlessPlayerRegistryService(paths);
        var runner = new FakeKitRunner { CanDeliver = canDeliver, Reply = replyFailure ? "Error: unknown item" : "ok" };
        return (new HeadlessKitService(paths, new HeadlessActivityLogService(paths), registry, runner), registry, runner);
    }
    var starter = Kit("starter", new KitEntry("Item", "PalSphere", 10));

    RunScenario("Kit auto-gift never gifts players who were already known, gifts a new one exactly once, and does nothing while disabled", () =>
    {
        var (kits, registry, runner) = BuildKits("kit-auto");
        registry.Observe(Snapshot(("steam_veteran", "Veteran")), DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert(kits.SaveConfig(Cfg(false, "starter", starter)).Success, "saving a disabled config must succeed");
        kits.EnforceAsync(Snapshot(("steam_veteran", "Veteran")), CancellationToken.None).GetAwaiter().GetResult();
        Assert(runner.Commands.Count == 0, "nothing may be given while auto-gift is off");

        Assert(kits.SaveConfig(Cfg(true, "starter", starter)).Success, "enabling auto-gift must succeed");
        var poll = Snapshot(("steam_veteran", "Veteran"), ("steam_newbie", "Newbie"));
        registry.Observe(poll, DateTimeOffset.UtcNow.AddSeconds(1));
        kits.EnforceAsync(poll, CancellationToken.None).GetAwaiter().GetResult();
        Assert(runner.Commands.Count == 1, $"only the new player may be gifted, got {runner.Commands.Count} command(s)");
        Assert(runner.Commands[0].Contains("steam_newbie") && !runner.Commands[0].Contains("steam_veteran"), $"wrong recipient: {runner.Commands[0]}");

        kits.EnforceAsync(poll, CancellationToken.None).GetAwaiter().GetResult();
        Assert(runner.Commands.Count == 1, "a claimed player must not be gifted again on the next poll");
        Assert(kits.GetSnapshot().Claims.Count == 1, "exactly one claim must be recorded");
    }, failures);
    RunScenario("Kit auto-gift stops after 3 failed attempts and a claim reset lets it try again", () =>
    {
        var (kits, registry, runner) = BuildKits("kit-retry", replyFailure: true);
        Assert(kits.SaveConfig(Cfg(true, "starter", starter)).Success, "enabling must succeed");
        var poll = Snapshot(("steam_newbie", "Newbie"));
        registry.Observe(poll, DateTimeOffset.UtcNow.AddSeconds(1));
        for (var i = 0; i < 6; i++) kits.EnforceAsync(poll, CancellationToken.None).GetAwaiter().GetResult();
        Assert(runner.Commands.Count == HeadlessKitService.MaximumDeliveryAttempts, $"expected {HeadlessKitService.MaximumDeliveryAttempts} attempts, got {runner.Commands.Count}");
        Assert(kits.GetSnapshot().Claims.Count == 0, "a failed delivery must not be recorded as claimed");
        runner.Reply = "ok";
        kits.ForgetClaims("steam_newbie");
        kits.EnforceAsync(poll, CancellationToken.None).GetAwaiter().GetResult();
        Assert(runner.Commands.Count == HeadlessKitService.MaximumDeliveryAttempts + 1, "after a reset the player must be attempted again");
        Assert(kits.GetSnapshot().Claims.Count == 1, "the retry that succeeds must be recorded");
    }, failures);
    RunScenario("Kit delivery is skipped entirely when the provider cannot deliver, and manual give refuses an offline player", () =>
    {
        var (kits, registry, runner) = BuildKits("kit-noprovider", canDeliver: false);
        Assert(kits.SaveConfig(Cfg(true, "starter", starter)).Success, "enabling must succeed");
        var poll = Snapshot(("steam_newbie", "Newbie"));
        registry.Observe(poll, DateTimeOffset.UtcNow.AddSeconds(1));
        kits.EnforceAsync(poll, CancellationToken.None).GetAwaiter().GetResult();
        Assert(runner.Commands.Count == 0, "no command may be sent without a working provider");
        var refused = kits.GiveAsync("starter", "steam_newbie", poll, "manual", CancellationToken.None).GetAwaiter().GetResult();
        Assert(!refused.Success && refused.Message.Length > 0, "a manual give must explain the missing provider");

        var (kits2, _, runner2) = BuildKits("kit-offline");
        kits2.SaveConfig(Cfg(false, "", starter));
        var offline = kits2.GiveAsync("starter", "steam_ghost", Snapshot(("steam_other", "Other")), "manual", CancellationToken.None).GetAwaiter().GetResult();
        Assert(!offline.Success && runner2.Commands.Count == 0, "a manual give to an offline player must fail without sending anything");
    }, failures);
    RunScenario("Kit auto-gift cutoff is stamped when enabled, kept on an unchanged re-save, and reset when the kit changes", () =>
    {
        var (kits, _, _) = BuildKits("kit-cutoff");
        var other = Kit("other", new KitEntry("Item", "Money", 100));
        var first = kits.SaveConfig(Cfg(true, "starter", starter, other));
        Assert(first.Config.AutoGiftEnabledAtUtc is not null, "enabling must stamp a cutoff");
        var stamp = first.Config.AutoGiftEnabledAtUtc;
        Thread.Sleep(20);
        var again = kits.SaveConfig(Cfg(true, "starter", starter, other));
        Assert(again.Config.AutoGiftEnabledAtUtc == stamp, "re-saving an unchanged auto-gift must keep the original cutoff");
        var switched = kits.SaveConfig(Cfg(true, "other", starter, other));
        Assert(switched.Config.AutoGiftEnabledAtUtc > stamp, "switching the auto-gift kit must move the cutoff forward");
        var off = kits.SaveConfig(Cfg(false, "other", starter, other));
        Assert(off.Config.AutoGiftEnabledAtUtc is null, "turning auto-gift off must clear the cutoff");
    }, failures);
    RunScenario("Give Item sends PalDefender give commands to the player's UserId, records no kit claim, and refuses bad input without sending", () =>
    {
        var (kits, _, runner) = BuildKits("give-item");
        var online = Snapshot(("steam_76561198000000001", "Alice"));
        var given = kits.GiveEntriesAsync([new KitEntry("Item", "PalSphere", 10), new KitEntry(" item ", " Wood ", 50), new KitEntry("Pal", "WeaselDragon", 5)],
            "steam_76561198000000001", online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(given.Success, $"a valid give succeeds: {given.Message}");
        Assert(runner.Commands.SequenceEqual(["giveitems steam_76561198000000001 PalSphere:10 Wood:50", "givepal steam_76561198000000001 WeaselDragon 5"]),
            $"items batch into one giveitems and each Pal is a givepal, with whitespace trimmed: {string.Join(" | ", runner.Commands)}");
        Assert(kits.GetSnapshot().Claims.Count == 0, "a one-off give is not a kit claim, so it never affects auto-gift");

        var before = runner.Commands.Count;
        var badId = kits.GiveEntriesAsync([new KitEntry("Item", "Pal Sphere; shutdown", 1)], "steam_76561198000000001", online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!badId.Success && badId.Message.Contains("not a valid id") && !badId.Message.StartsWith("Kit "), $"an id that could smuggle another RCON command is refused, in plain words: {badId.Message}");
        var badAmount = kits.GiveEntriesAsync([new KitEntry("Item", "PalSphere", 0)], "steam_76561198000000001", online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!badAmount.Success, "an amount of 0 is refused");
        var empty = kits.GiveEntriesAsync([], "steam_76561198000000001", online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!empty.Success, "nothing to give is refused");
        var offline = kits.GiveEntriesAsync([new KitEntry("Item", "PalSphere", 1)], "steam_somebody_else", online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!offline.Success && offline.Message.Contains("not online"), "an offline player is refused");
        Assert(runner.Commands.Count == before, "none of the refused gives sent anything");

        var (noProvider, _, silentRunner) = BuildKits("give-item-noprovider", canDeliver: false);
        var missing = noProvider.GiveEntriesAsync([new KitEntry("Item", "PalSphere", 1)], "steam_76561198000000001", online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!missing.Success && missing.Message == "no provider" && silentRunner.Commands.Count == 0, "without PalDefender/RCON it says why and sends nothing");

        var (erroring, _, errorRunner) = BuildKits("give-item-error", replyFailure: true);
        var failed = erroring.GiveEntriesAsync([new KitEntry("Item", "NotARealItem", 1)], "steam_76561198000000001", online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!failed.Success && failed.Message.Contains("looks like an error") && errorRunner.Commands.Count == 1, "an error-looking server reply is reported as a failure, not success");
    }, failures);

    // ---- v0.8.3.0 Give Item picker -------------------------------------------------------------------------
    // Shaped like the real decoded Level.sav.json (palworld-save-tools output): item slots carry "static_id", Pals a
    // CharacterID NameProperty object. Includes the traps: empty/None slots, an id that could smuggle an RCON command,
    // alpha Pals ("BOSS_"), and a sibling "value" right after CharacterID that is not a species.
    const string GameIdFixture = """
        {"properties":{"worldSaveData":{"value":{
          "CharacterSaveParameterMap":{"value":[
            {"key":{"PlayerUId":{"value":"00000000-0000-0000-0000-000000000001"}},"value":{"RawData":{"value":{"object":{"SaveParameter":{"value":{
              "CharacterID":{"id":null,"value":"Alpaca","type":"NameProperty"},"Level":{"value":"NotAPal","type":"IntProperty"}}}}}}}},
            {"value":{"RawData":{"value":{"object":{"SaveParameter":{"value":{"CharacterID":{"id":null,"value":"BOSS_Garm","type":"NameProperty"}}}}}}}},
            {"value":{"RawData":{"value":{"object":{"SaveParameter":{"value":{"CharacterID":{"id":null,"value":"Garm","type":"NameProperty"}}}}}}}},
            {"value":{"RawData":{"value":{"object":{"SaveParameter":{"value":{"CharacterID":{"id":null,"value":"Alpaca","type":"NameProperty"}}}}}}}},
            {"value":{"RawData":{"value":{"object":{"SaveParameter":{"value":{"IsPlayer":{"value":true},"NickName":{"value":"Alice"}}}}}}}}
          ]},
          "ItemContainerSaveData":{"value":[
            {"value":{"Slots":{"value":{"values":[
              {"RawData":{"value":{"slot_index":0,"count":757,"item":{"static_id":"Money","dynamic_id":{}}}}},
              {"RawData":{"value":{"slot_index":1,"count":10,"item":{"static_id":"PalSphere_Mega","dynamic_id":{}}}}},
              {"RawData":{"value":{"slot_index":2,"count":0,"item":{"static_id":"","dynamic_id":{}}}}},
              {"RawData":{"value":{"slot_index":3,"count":0,"item":{"static_id":"None","dynamic_id":{}}}}},
              {"RawData":{"value":{"slot_index":4,"count":1,"item":{"static_id":"Pal Sphere; shutdown","dynamic_id":{}}}}},
              {"RawData":{"value":{"slot_index":5,"count":3,"item":{"static_id":"Money","dynamic_id":{}}}}}
            ]}}}}
          ]}
        }}}}
        """;

    RunScenario("Give Item picker: the save reader lists the world's item and Pal ids, counts them, and skips empty, unsafe and non-species values", () =>
    {
        var ids = SaveGameIdReader.Read(System.Text.Encoding.UTF8.GetBytes(GameIdFixture));
        Assert(ids.Items.Count == 2 && ids.Items["Money"] == 2 && ids.Items["PalSphere_Mega"] == 1,
            $"items are the non-empty static_ids, counted per slot: {string.Join(", ", ids.Items.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Assert(!ids.Items.ContainsKey("None") && !ids.Items.Keys.Any(k => k.Contains(' ')), "empty and 'None' slots, and an id that could carry another RCON command, are not listed");
        Assert(ids.Pals.Count == 2 && ids.Pals["Alpaca"] == 2 && ids.Pals["Garm"] == 2,
            $"Pals are the CharacterIDs, an alpha counted as its species: {string.Join(", ", ids.Pals.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Assert(ids.AlphaPals.SetEquals(["Garm"]), "an alpha (BOSS_ prefix) is remembered as seen");
        Assert(!ids.Pals.ContainsKey("NotAPal") && !ids.Pals.ContainsKey("Alice"), "a 'value' after the CharacterID object, and a player's name, are not species");
        var empty = SaveGameIdReader.Read("{}"u8);
        Assert(empty.Items.Count == 0 && empty.Pals.Count == 0, "a save with nothing in it gives an empty list, not an error");
    }, failures);

    RunScenario("Give Item picker: world, kit and earlier-give ids merge into one row each, world casing wins, items first then alphabetical", () =>
    {
        var world = SaveGameIdReader.Read(System.Text.Encoding.UTF8.GetBytes(GameIdFixture));
        var rows = HeadlessGameIdCatalogService.Merge(world,
            [new KitEntry("Item", "palsphere_mega", 5), new KitEntry("Item", "Wood", 50), new KitEntry("Pal", "WeaselDragon", 5)],
            [new KitEntry("Item", "MONEY", 100), new KitEntry("Pal", "alpaca", 3)]);
        Assert(rows.Select(r => $"{r.Kind}:{r.Id}").SequenceEqual(["Item:Money", "Item:PalSphere_Mega", "Item:Wood", "Pal:Alpaca", "Pal:Garm", "Pal:WeaselDragon"]),
            $"one row per id, items first then alphabetical: {string.Join(", ", rows.Select(r => $"{r.Kind}:{r.Id}"))}");
        var sphere = rows.Single(r => r.Id == "PalSphere_Mega");
        Assert(sphere.WorldCount == 1 && sphere.InKit && !sphere.GivenBefore, "a kit id matching a world id ignoring case joins its row");
        var money = rows.Single(r => r.Id == "Money");
        Assert(money.WorldCount == 2 && money.GivenBefore && !money.InKit, "an earlier give joins its row too");
        Assert(rows.Single(r => r.Id == "Wood") is { WorldCount: 0, InKit: true }, "a kit id the world has never had is still offered");
        Assert(rows.Single(r => r.Id == "Garm").AlphaSeen && !rows.Single(r => r.Id == "Alpaca").AlphaSeen, "only the species seen as an alpha is marked");
        Assert(HeadlessGameIdCatalogService.Merge(null, [], []).Count == 0, "no world and no kits is an empty catalogue");
    }, failures);

    RunScenario("Give Item picker: search matches every typed word ignoring case and underscores; adding keeps typed text; amounts use the server's limits", () =>
    {
        Assert(GameIdSearch.Matches("PalSphere_Mega", "sphere mega") && GameIdSearch.Matches("PalSphere_Mega", "PALSPHERE") && GameIdSearch.Matches("PalSphere_Mega", "sphere_mega"),
            "words, case and underscores do not matter");
        Assert(!GameIdSearch.Matches("PalSphere_Mega", "sphere giga") && GameIdSearch.Matches("Anything", "  ") && !GameIdSearch.Matches(null, "a"), "every word must match; an empty query matches all");
        Assert(KitEntryText.Append("", new KitTextEntry("Item", "PalSphere", 10)) == "item PalSphere 10", "the first line");
        Assert(KitEntryText.Append("item Wood 5\n", new KitTextEntry("Pal", "Alpaca", 3)) == "item Wood 5\npal Alpaca 3", "a new line after existing ones");
        Assert(KitEntryText.Append("item Wo", new KitTextEntry("Item", "Stone", 1)) == "item Wo\nitem Stone 1", "a half-typed line is kept as typed, not thrown away");
        Assert(KitEntryText.TryParse(KitEntryText.Append("item Wood 5", new KitTextEntry("Pal", "Alpaca", 3)), out var parsed, out _) && parsed.Count == 2, "what the picker writes parses back");
        Assert(KitEntryText.TryParseAmount("Item", "1000000", out var max, out _) && max == 1_000_000 && !KitEntryText.TryParseAmount("Item", "1000001", out _, out var tooMany) && tooMany.Contains("1,000,000"),
            "items: 1 to 1,000,000");
        Assert(KitEntryText.TryParseAmount("Pal", "100", out _, out _) && !KitEntryText.TryParseAmount("Pal", "101", out _, out var tooHigh) && tooHigh.StartsWith("Level"), "Pals: level 1 to 100");
        Assert(!KitEntryText.TryParseAmount("Item", "0", out _, out _) && !KitEntryText.TryParseAmount("Item", "ten", out _, out var notNumber) && notNumber.Contains("whole number"), "0 and words are refused");
    }, failures);

    RunScenario("Give Item picker: a delivered give is remembered (newest first, one row per id, kept across a restart); a refused or failed one is not", () =>
    {
        var (kits, _, _) = BuildKits("give-history");
        var online = Snapshot(("steam_76561198000000001", "Alice"));
        var give = (IReadOnlyList<KitEntry> e) => kits.GiveEntriesAsync(e, "steam_76561198000000001", online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(give([new KitEntry("Item", "PalSphere", 10), new KitEntry("Item", "Wood", 50)]).Success, "first give");
        Assert(give([new KitEntry("Item", "PalSphere", 3)]).Success, "second give");
        Assert(!give([new KitEntry("Item", "Bad Id", 1)]).Success, "a refused give");
        var history = kits.RecentlyGiven();
        Assert(history.Select(e => $"{e.Id}:{e.Amount}").SequenceEqual(["PalSphere:3", "Wood:50"]), $"newest first, one row per id, last amount: {string.Join(", ", history.Select(e => $"{e.Id}:{e.Amount}"))}");
        var reloaded = new HeadlessKitService(new FakePathProfile(Path.Combine(tempRoot, "give-history")), new HeadlessActivityLogService(new FakePathProfile(Path.Combine(tempRoot, "give-history"))),
            new HeadlessPlayerRegistryService(new FakePathProfile(Path.Combine(tempRoot, "give-history"))), new FakeKitRunner());
        Assert(reloaded.RecentlyGiven().Count == 2, "the history survives a MystTiq restart");

        var (erroring, _, _) = BuildKits("give-history-error", replyFailure: true);
        erroring.GiveEntriesAsync([new KitEntry("Item", "NotARealItem", 1)], "steam_76561198000000001", online, CancellationToken.None).GetAwaiter().GetResult();
        Assert(erroring.RecentlyGiven().Count == 0, "an id the server answered with an error is not remembered as working");

        var many = Enumerable.Range(0, HeadlessKitService.MaximumGivenHistory + 50).Select(i => new KitEntry("Item", $"Item{i}", 1)).ToArray();
        var capped = HeadlessKitService.MergeGiven([], many);
        Assert(capped.Count == HeadlessKitService.MaximumGivenHistory && capped[0].Id == $"Item{many.Length - 1}", "the history is capped, keeping the newest");
    }, failures);

    RunScenario("Give Item picker: the catalogue reads the newest world's decoded save, says when there is none, and re-reads it when it changes", () =>
    {
        var root = Path.Combine(tempRoot, "game-id-catalog");
        var paths = new FakePathProfile(root);
        var kits = new HeadlessKitService(paths, new HeadlessActivityLogService(paths), new HeadlessPlayerRegistryService(paths), new FakeKitRunner());
        kits.SaveConfig(new KitConfig(false, string.Empty, null, [Kit("starter", new KitEntry("Item", "Wood", 50))]));
        var catalog = new HeadlessGameIdCatalogService(paths, kits);

        var none = catalog.GetCatalog();
        Assert(!none.WorldAvailable && none.Detail.Contains("No world save") && none.Entries.Single().Id == "Wood", $"no world yet: kit ids only, and it says so: {none.Detail}");

        var world = Path.Combine(paths.SaveRoot, "0", "WORLD1");
        Directory.CreateDirectory(world);
        File.WriteAllText(Path.Combine(world, "Level.sav"), "sav");
        var undecoded = catalog.GetCatalog();
        Assert(!undecoded.WorldAvailable && undecoded.Detail.Contains("no decoded save"), $"a world without Level.sav.json says why its items are unknown: {undecoded.Detail}");

        var json = Path.Combine(world, "Level.sav.json");
        File.WriteAllText(json, GameIdFixture);
        var full = catalog.GetCatalog();
        Assert(full.WorldAvailable && full.WorldId == "WORLD1" && full.ItemCount == 3 && full.PalCount == 2, $"world + kit ids: {full.ItemCount} items, {full.PalCount} Pals");
        Assert(full.Detail.Contains("2 item(s) and 2 Pal species seen in world WORLD1") && full.Detail.Contains("plus 1 from kits"), $"the detail says where the ids come from: {full.Detail}");

        File.WriteAllText(json, """{"x":{"static_id":"Stone"}}""");
        File.SetLastWriteTimeUtc(json, DateTime.UtcNow.AddMinutes(1));
        var changed = catalog.GetCatalog();
        Assert(changed.Entries.Any(e => e.Id == "Stone") && !changed.Entries.Any(e => e.Id == "Money"), "a changed save is read again, not served from the cache");

        File.WriteAllText(json, "{ not json");
        File.SetLastWriteTimeUtc(json, DateTime.UtcNow.AddMinutes(2));
        var broken = catalog.GetCatalog();
        Assert(!broken.WorldAvailable && broken.Detail.Contains("could not be read") && broken.Entries.Any(e => e.Id == "Wood"), $"an unreadable save is reported and kit ids still show: {broken.Detail}");
    }, failures);

    // ---- v0.8.4.0 Pause outside delivery -------------------------------------------------------------------
    RunScenario("Delivery pause: host-clock end time capped at 30 days, 0 resumes, survives a restart, and saving channels never clears it", () =>
    {
        var now = DateTimeOffset.UtcNow;
        Assert(!NotificationDeliveryPolicy.IsPaused(null, now) && !NotificationDeliveryPolicy.IsPaused(now.AddSeconds(-1), now) && NotificationDeliveryPolicy.IsPaused(now.AddMinutes(1), now),
            "paused only while the end time is in the future");
        var paths = new FakePathProfile(Path.Combine(tempRoot, "delivery-pause"));
        var routing = new HeadlessNotificationRoutingService(paths, new HeadlessActivityLogService(paths));
        Assert(routing.GetDeliveryState().PausedUntilUtc is null, "delivery is on by default");
        var paused = routing.PauseDelivery(60);
        Assert(paused.PausedUntilUtc is { } until && Math.Abs((until - DateTimeOffset.UtcNow.AddMinutes(60)).TotalSeconds) < 5 && paused.Detail.Contains("paused until"), $"pausing for an hour: {paused.Detail}");
        var capped = routing.PauseDelivery(10_000_000);
        Assert(capped.PausedUntilUtc!.Value <= DateTimeOffset.UtcNow.AddMinutes(AlertMutePolicy.MaximumMuteMinutes + 1), "capped at 30 days like the alert mute");
        routing.SaveChannels(new NotificationChannelConfiguration());
        var reloaded = new HeadlessNotificationRoutingService(paths, new HeadlessActivityLogService(paths));
        Assert(reloaded.GetDeliveryState().PausedUntilUtc is not null, "the pause survives saving the channel list and a MystTiq restart");
        var resumed = reloaded.PauseDelivery(0);
        Assert(resumed.PausedUntilUtc is null && resumed.Detail.StartsWith("Outside delivery is on"), $"0 resumes: {resumed.Detail}");
        File.WriteAllText(Path.Combine(paths.ManagerRuntimeRoot, "notifications", "delivery.json"), "{ broken");
        Assert(new HeadlessNotificationRoutingService(paths, new HeadlessActivityLogService(paths)).GetDeliveryState().PausedUntilUtc is null, "an unreadable pause file means not paused, not a crash");
    }, failures);

    RunScenario("Delivery pause: a real webhook receives nothing while paused (the notification is still kept and the skip logged), and receives again after resuming", () =>
    {
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0)) { probe.Start(); port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop(); }
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/hook/");
        listener.Start();
        var received = 0;
        var loop = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                try { var ctx = await listener.GetContextAsync(); Interlocked.Increment(ref received); ctx.Response.StatusCode = 200; ctx.Response.Close(); }
                catch { return; }
            }
        });

        var paths = new FakePathProfile(Path.Combine(tempRoot, "delivery-webhook"));
        var activity = new HeadlessActivityLogService(paths);
        var routing = new HeadlessNotificationRoutingService(paths, activity);
        routing.SaveChannels(new NotificationChannelConfiguration { Channels = [new(NotificationChannel.Desktop, true, null), new(NotificationChannel.Webhook, true, $"http://localhost:{port}/hook/")] });
        var notifications = new HeadlessNotificationService(paths, activity, routing);

        notifications.Create("Warning", "Before pause", "sent");
        Assert(WaitFor(() => Volatile.Read(ref received) == 1, 10000), "with delivery on, the webhook receives the notification");

        routing.PauseDelivery(60);
        notifications.Create("Critical", "During pause", "kept here only");
        Thread.Sleep(1500);
        Assert(Volatile.Read(ref received) == 1, "while paused, the webhook receives nothing");
        Assert(notifications.GetSnapshot().Items.Any(i => i.Title == "During pause"), "the notification is still on the Notifications page");
        Assert(activity.GetTail(50).Lines.Any(l => l.Contains("Not sent outside (delivery paused)") && l.Contains("During pause")), "the skipped send is written to the Activity log");

        routing.PauseDelivery(0);
        notifications.Create("Information", "After resume", "sent again");
        Assert(WaitFor(() => Volatile.Read(ref received) == 2, 10000), "after resuming, the webhook receives again, and the paused one is not sent late");
        Thread.Sleep(500);
        Assert(Volatile.Read(ref received) == 2, "nothing from the pause is sent afterwards");
        listener.Stop();
    }, failures);

    // ---- v0.8.9.0 Crash reports ------------------------------------------------------------------------------
    // Shaped like the real reports Palworld's Unreal Engine writes (FGenericCrashContext/RuntimeProperties); the messages
    // are the ones seen on this project's own server, the rest is minimal.
    static string CrashXml(string error, string type = "Crash") =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><FGenericCrashContext><RuntimeProperties><CrashVersion>3</CrashVersion>" +
        $"<ErrorMessage>{System.Security.SecurityElement.Escape(error)}</ErrorMessage><CrashType>{type}</CrashType>" +
        "<EngineVersion>5.1.1-0+++UE5+Release-5.1</EngineVersion></RuntimeProperties></FGenericCrashContext>";
    const string TArrayError = "LowLevelFatalError [File:C:\\works\\Pal-UE-EngineSource\\Engine\\Source\\Runtime\\Core\\Private\\Containers\\Array.cpp] [Line: 8] Trying to resize TArray to an invalid size of 1";
    const string AccessViolation = "Unhandled Exception: EXCEPTION_ACCESS_VIOLATION reading address 0xffffffffffffffff";
    static void WriteReport(string crashesRoot, string folder, string xml, DateTime writtenUtc)
    {
        var dir = Path.Combine(crashesRoot, folder);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, UnrealCrashReportParser.ContextFileName);
        File.WriteAllText(file, xml);
        File.SetLastWriteTimeUtc(file, writtenUtc);
        Directory.SetLastWriteTimeUtc(dir, writtenUtc);
    }

    RunScenario("Crash reports: an Unreal crash context parses to one stamped evidence line that the catalog classifies (access violation, the TArray engine check)", () =>
    {
        var at = new DateTimeOffset(2026, 9, 16, 17, 4, 54, TimeSpan.Zero);
        var report = UnrealCrashReportParser.Parse("UECC-Windows-8E15_0000", at, CrashXml(TArrayError + "\r\n  second line", "Assert"));
        Assert(report is { CrashType: "Assert", EngineVersion: "5.1.1-0+++UE5+Release-5.1" } && report.ErrorMessage.EndsWith("invalid size of 1 second line"), $"fields read, lines flattened: {report?.ErrorMessage}");
        var line = UnrealCrashReportParser.ToEvidenceLine(report!);
        Assert(line.StartsWith($"[{at.ToLocalTime():yyyy.MM.dd-HH.mm.ss}] Unreal crash report UECC-Windows-8E15_0000: LowLevelFatalError") && line.EndsWith("(Assert, engine 5.1.1-0+++UE5+Release-5.1)"), $"evidence line: {line}");
        Assert(CrashSignatureCatalog.TryParseTimestamp(line) == new DateTimeOffset(at.ToLocalTime().DateTime, at.ToLocalTime().Offset), "its stamp reads back as the report time");
        Assert(UnrealCrashReportParser.Parse("x", at, "<not xml") is null && UnrealCrashReportParser.Parse("x", at, "<Other/>") is null &&
               UnrealCrashReportParser.Parse("x", at, CrashXml("")) is null, "garbage, another document and an empty error are not reports");
        var matches = CrashSignatureCatalog.Match([
            UnrealCrashReportParser.ToEvidenceLine(report!),
            UnrealCrashReportParser.ToEvidenceLine(UnrealCrashReportParser.Parse("UECC-2", at, CrashXml(AccessViolation))!),
            UnrealCrashReportParser.ToEvidenceLine(UnrealCrashReportParser.Parse("UECC-3", at, CrashXml("LowLevelFatalError [File:X.cpp] [Line: 1] Something else", "Assert"))!)]);
        Assert(matches.Select(m => m.Signature.Id).OrderBy(x => x).SequenceEqual(["access-violation", "engine-array-size", "ue-fatal"]),
            $"the TArray check has its own signature, the access violation and another fatal error keep theirs: {string.Join(", ", matches.Select(m => m.Signature.Id))}");
        Assert(CrashSignatureCatalog.Find("engine-array-size") is { Severity: "Critical", Fixes.Count: 3 } sig && sig.Cause.Contains("does not say", StringComparison.Ordinal) == false && sig.Cause.Contains("not which part of the game hit it"),
            "its cause claims only what the report states");
    }, failures);

    RunScenario("Crash reports: the analyzer reads Pal\\Saved\\Crashes, counts the reports and names them in the summary; folders without a readable context are skipped", () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "crash-reports-analyze"));
        var tools = new HeadlessCrashAndSaveToolsService(paths, new HeadlessActivityLogService(paths));
        Assert(tools.Analyze().CrashReportsRead == 0, "no crash folder: nothing read, no error");
        WriteReport(tools.CrashReportsRoot, "UECC-Windows-A_0000", CrashXml(TArrayError, "Assert"), DateTime.UtcNow.AddHours(-2));
        WriteReport(tools.CrashReportsRoot, "UECC-Windows-B_0000", CrashXml(AccessViolation), DateTime.UtcNow.AddHours(-1));
        Directory.CreateDirectory(Path.Combine(tools.CrashReportsRoot, "UECC-Windows-empty_0000"));
        WriteReport(tools.CrashReportsRoot, "UECC-Windows-broken_0000", "<broken", DateTime.UtcNow);
        var analysis = tools.Analyze();
        Assert(analysis.CrashReportsRead == 2 && tools.ReadCrashReports().Select(r => r.Folder).SequenceEqual(["UECC-Windows-B_0000", "UECC-Windows-A_0000"]),
            $"two readable reports, newest first: {string.Join(", ", tools.ReadCrashReports().Select(r => r.Folder))}");
        Assert(analysis.Findings.Any(f => f.SignatureId == "engine-array-size" && f.Evidence.Any(e => e.Contains("UECC-Windows-A_0000"))) &&
               analysis.Findings.Any(f => f.SignatureId == "access-violation"), "both become findings, with the report named in the evidence");
        Assert(analysis.Summary.Contains("Read 2 Unreal crash report(s)"), $"the summary says so: {analysis.Summary}");
        Assert(tools.Analyze().NewFindings == 0, "running it again reports them as already seen, not new");
    }, failures);

    RunScenario("Crash reports: the watcher records old reports silently, alerts once per new report, waits out a mute, respects the crash-alert switch and a recent crash alert", () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "crash-reports-watch"));
        var activity = new HeadlessActivityLogService(paths);
        var tools = new HeadlessCrashAndSaveToolsService(paths, activity);
        var notifications = new HeadlessNotificationService(paths, activity);
        var rules = new AlertRuleSet();
        DateTimeOffset? lastCrashAlert = null;
        var watcher = new HeadlessCrashReportWatcher(paths, "Test Server", tools, notifications, _ => Task.FromResult<IReadOnlyCollection<string>>([]),
            () => rules, () => lastCrashAlert, activity);
        int Alerts() => notifications.GetSnapshot().Items.Count(i => i.Title == "Test Server: new crash report");
        CrashReportCheck Check() => watcher.CheckAsync(DateTimeOffset.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

        WriteReport(tools.CrashReportsRoot, "UECC-old_0000", CrashXml(AccessViolation), DateTime.UtcNow.AddDays(-3));
        Assert(Check().Outcome == CrashReportCheckOutcome.Baseline && Alerts() == 0, "the first check records the existing report without alerting");
        Assert(Check().Outcome == CrashReportCheckOutcome.NothingNew, "nothing new afterwards");

        WriteReport(tools.CrashReportsRoot, "UECC-new_0000", CrashXml(TArrayError, "Assert"), DateTime.UtcNow);
        var first = Check();
        Assert(first.Outcome == CrashReportCheckOutcome.Alerted && Alerts() == 1 && first.NewReports.SequenceEqual(["UECC-new_0000"]), $"a new report alerts once: {first.Outcome}");
        var message = notifications.GetSnapshot().Items.First(i => i.Title == "Test Server: new crash report").Message;
        Assert(message.Contains("UECC-new_0000") && message.Contains("Engine stopped on an invalid array size"), $"the alert names the report and the finding: {message}");
        Assert(Check().Outcome == CrashReportCheckOutcome.NothingNew && Alerts() == 1, "and not again");

        rules = new AlertRuleSet { MutedUntilUtc = DateTimeOffset.UtcNow.AddHours(1) };
        WriteReport(tools.CrashReportsRoot, "UECC-muted_0000", CrashXml(AccessViolation.Replace("reading", "writing")), DateTime.UtcNow);
        Assert(Check().Outcome == CrashReportCheckOutcome.Muted && Alerts() == 1, "while muted, nothing is sent and the report is not marked seen");
        rules = new AlertRuleSet();
        Assert(Check().Outcome == CrashReportCheckOutcome.Alerted && Alerts() == 2, "once the mute ends, it is announced");

        lastCrashAlert = DateTimeOffset.UtcNow.AddMinutes(-2);
        WriteReport(tools.CrashReportsRoot, "UECC-covered_0000", CrashXml(TArrayError.Replace("of 1", "of 7"), "Assert"), DateTime.UtcNow);
        Assert(Check().Outcome == CrashReportCheckOutcome.CoveredByCrashAlert && Alerts() == 2, "a crash the recovery loop just alerted about is not announced twice");
        lastCrashAlert = null;

        rules = new AlertRuleSet { CrashAlerts = new CrashAlertRule(false, true) };
        WriteReport(tools.CrashReportsRoot, "UECC-off_0000", CrashXml(AccessViolation + " again"), DateTime.UtcNow);
        Assert(Check().Outcome == CrashReportCheckOutcome.Disabled && Alerts() == 2 && Check().Outcome == CrashReportCheckOutcome.NothingNew,
            "with crash alerts off it is recorded (Activity log) but not sent, and not held back for later");
        rules = new AlertRuleSet();

        File.WriteAllText(Path.Combine(paths.ManagerRuntimeRoot, "crash-analyzer", "seen-reports.json"), "{ broken");
        Assert(Check().Outcome == CrashReportCheckOutcome.Baseline && Alerts() == 2, "an unreadable record is treated as a first run, never as every report being new");
    }, failures);

    // ---- v0.7.113.0 Teleport Points ------------------------------------------------------------------------
    RunScenario("Teleport point text parses names and PalDefender coordinates, rejects bad lines, and round-trips", () =>
    {
        Assert(MystTiq.Core.Services.TeleportPointText.TryParse("# comment\n spawn -358.5 270.25\n\nbase 120 -44 1500\r\n", out var points, out var error) && error.Length == 0, $"valid text parses: {error}");
        Assert(points.Count == 2 && points[0] == new MystTiq.Core.Services.TeleportPointEntry("spawn", -358.5, 270.25, null) && points[1].Z == 1500, "names, X, Y and the optional height are read");
        Assert(!MystTiq.Core.Services.TeleportPointText.TryParse("spawn -358", out _, out var tooFew) && tooFew.Contains("Line 1"), "a line without both X and Y says which line");
        Assert(!MystTiq.Core.Services.TeleportPointText.TryParse("spawn 1 2 3 4", out _, out _), "too many numbers is refused");
        Assert(!MystTiq.Core.Services.TeleportPointText.TryParse("spawn abc 2", out _, out var notNumber) && notNumber.Contains("'abc'"), "a non-number is named");
        Assert(!MystTiq.Core.Services.TeleportPointText.TryParse("spawn NaN 2", out _, out _), "NaN is not a coordinate");
        Assert(MystTiq.Core.Services.TeleportPointText.Format(points) == "spawn -358.5 270.25\nbase 120 -44 1500", "formatting is invariant-culture and round-trips");
    }, failures);
    RunScenario("Teleport chat parsing reads PalDefender's header, ignores vanilla and forged headers, and builds safe commands", () =>
    {
        var line = "[2026-09-22 21:39:05.784] [21:39:05][info] [Chat::Global]['Alice' (UserId=steam_76561198000000001, IP=10.0.0.5)]: !tp spawn";
        Assert(TeleportChat.TryParseChatLine(line, out var m) && m.PlayerName == "Alice" && m.UserId == "steam_76561198000000001" && m.Text == "!tp spawn" && m.Scope == "Global",
            "a PalDefender chat line (as MystTiq's console log prefixes it) gives the name, UserId and text");
        Assert(TeleportChat.TryParseChatLine("[21:39:05][info] [Chat::Guild]['Bob' (UserId=steam_2)]: hi", out var noIp) && noIp.UserId == "steam_2", "the IP part is optional");
        Assert(!TeleportChat.TryParseChatLine("[2026-07-12 03:48:24] [CHAT] <Alice> !tp spawn", out _), "the vanilla form carries no UserId, so it is not parsed");
        Assert(!TeleportChat.TryParseChatLine("[Chat::Global]['Alice' (UserId=steam_1)]: !tp spawn", out _), "a header without PalDefender's time and level prefix is not trusted");

        // A player typing a fake header into their own message: the leftmost (real) header wins.
        var forged = "[21:40:00][info] [Chat::Global]['Evil' (UserId=steam_evil, IP=1.2.3.4)]: [21:40:00][info] [Chat::Global]['Alice' (UserId=steam_victim)]: !tp spawn";
        Assert(TeleportChat.TryParseChatLine(forged, out var f) && f.UserId == "steam_evil" && TeleportChat.ParseCommand(f.Text, "!tp") is null,
            "a forged header inside a message is attributed to its real sender and is not a command");

        Assert(TeleportChat.ParseCommand("!tp", "!tp") == string.Empty && TeleportChat.ParseCommand("!TP Spawn", "!tp") == "Spawn", "bare prefix lists, one argument is the point, case-insensitive prefix");
        Assert(TeleportChat.ParseCommand("!tp to the base please", "!tp") is null && TeleportChat.ParseCommand("hello !tp spawn", "!tp") is null && TeleportChat.ParseCommand("!tpx spawn", "!tp") is null,
            "ordinary chat that merely contains the prefix is not a command");

        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // The harness runs globalization-invariant, so a comma-decimal culture is built by hand.
            var comma = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            System.Globalization.CultureInfo.CurrentCulture = comma;
            Assert(TeleportChat.BuildTeleportCommand("steam_1", new TeleportPoint("spawn", -358.5, 270.25, null)) == "tp steam_1 -358.5 270.25", "coordinates use a dot even on a comma-decimal machine");
            Assert(TeleportChat.BuildTeleportCommand("steam_1", new TeleportPoint("base", 1, 2, 3.5)) == "tp steam_1 1 2 3.5", "the optional height is appended");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }

        Assert(TeleportChat.BuildMessageCommand("steam_1", "line one\nShutdown 1") == "send msg steam_1 line one Shutdown 1", "a reply is always one line, so it can never become a second RCON command");
        Assert(TeleportChat.ParsePosition("steam_76561198000000001 is at X=-358.2, Y=270.5, Z=1200") is { X: -358.2, Y: 270.5, Z: 1200 }, "getpos numbers are read and the UserId digits are not");
        Assert(TeleportChat.ParsePosition("Player not found") is null, "no numbers, no position");

        Assert(TeleportChat.Validate(new TeleportConfig(true, "!tp", 60, [new TeleportPoint("spawn", 1, 2, null)])).Count == 0, "a normal config is valid");
        Assert(TeleportChat.Validate(new TeleportConfig(false, "/tp", 60, [])).Any(e => e.Contains("! or .")), "a / prefix would clash with PalDefender's own commands");
        Assert(TeleportChat.Validate(new TeleportConfig(false, "!tp", 60, [new TeleportPoint("my base", 1, 2, null)])).Count == 1, "a point name with a space cannot be typed as one argument");
        Assert(TeleportChat.Validate(new TeleportConfig(false, "!tp", 60, [new TeleportPoint("A", 1, 2, null), new TeleportPoint("a", 3, 4, null)])).Any(e => e.Contains("Two points")), "names are unique ignoring case");
        Assert(TeleportChat.Validate(new TeleportConfig(false, "!tp", 60, [new TeleportPoint("list", 1, 2, null)])).Any(e => e.Contains("reserved")), "'list' is reserved");
        Assert(TeleportChat.Validate(new TeleportConfig(true, "!tp", 60, [])).Any(e => e.Contains("at least one point")), "switching on with no points is refused");
        Assert(TeleportChat.Validate(new TeleportConfig(false, "!tp", -1, [])).Count == 1 && TeleportChat.Validate(new TeleportConfig(false, "!tp", 0, [new TeleportPoint("x", double.PositiveInfinity, 0, null)])).Count == 1, "cooldown and coordinate ranges are checked");
    }, failures);
    RunScenarioAsync("Teleport service: an online player's chat command teleports and replies, with cooldown, list, unknown point, impostor and off-switch handled", async () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "teleport-" + Guid.NewGuid().ToString("N")));
        var runner = new FakeKitRunner();
        var online = Snapshot(("steam_1", "Alice"));
        var service = new HeadlessTeleportService(paths, new HeadlessActivityLogService(paths), runner, new FakeChatSource(), _ => Task.FromResult(online));
        string Chat(string name, string uid, string text) => $"[21:39:05][info] [Chat::Global]['{name}' (UserId={uid}, IP=10.0.0.5)]: {text}";

        await service.ProcessLinesAsync([Chat("Alice", "steam_1", "!tp spawn")], CancellationToken.None);
        Assert(runner.Commands.Count == 0, "switched off (the default), nothing is sent");

        Assert(service.SaveConfig(new TeleportConfig(true, "!tp", 60, [new TeleportPoint("spawn", -358.5, 270, null), new TeleportPoint("base", 1, 2, 3)])).Success, "saving points and switching on works");
        await service.ProcessLinesAsync([Chat("Alice", "steam_1", "!tp Spawn")], CancellationToken.None);
        Assert(runner.Commands.SequenceEqual(["tp steam_1 -358.5 270", "send msg steam_1 Teleported to spawn."]), $"teleports then replies: {string.Join(" | ", runner.Commands)}");

        runner.Commands.Clear();
        await service.ProcessLinesAsync([Chat("Alice", "steam_1", "!tp base")], CancellationToken.None);
        Assert(runner.Commands.Count == 1 && runner.Commands[0].StartsWith("send msg steam_1 Please wait"), $"the cooldown blocks a second teleport and says how long: {string.Join(" | ", runner.Commands)}");

        runner.Commands.Clear();
        await service.ProcessLinesAsync([Chat("Alice", "steam_1", "!tp")], CancellationToken.None);
        Assert(runner.Commands.SequenceEqual(["send msg steam_1 Teleport points: spawn, base. Type !tp <name>."]), "the bare command lists the points");

        runner.Commands.Clear();
        await service.ProcessLinesAsync([Chat("Alice", "steam_1", "!tp moon")], CancellationToken.None);
        Assert(runner.Commands.Count == 1 && runner.Commands[0].Contains("No teleport point called 'moon'"), "an unknown point is answered, nothing teleports");

        runner.Commands.Clear();
        await service.ProcessLinesAsync([Chat("Alice", "steam_victim", "!tp spawn"), Chat("Mallory", "steam_1", "!tp spawn"), Chat("Bob", "steam_2", "!tp spawn")], CancellationToken.None);
        Assert(runner.Commands.Count == 0, "a name/UserId pair that is not an online player (forged or offline) is ignored entirely");

        // Duplicates: the same line arrives from both PalDefender's log and the console log.
        service.SaveConfig(new TeleportConfig(true, "!tp", 0, [new TeleportPoint("spawn", -358.5, 270, null)]));
        runner.Commands.Clear();
        await service.ProcessLinesAsync([Chat("Alice", "steam_1", "!tp spawn"), "[2026-09-22 21:39:06.001] " + Chat("Alice", "steam_1", "!tp spawn")], CancellationToken.None);
        Assert(runner.Commands.Count(c => c.StartsWith("tp ")) == 1, "the same command seen in two logs teleports once");

        var uses = service.GetSnapshot().RecentUses;
        Assert(uses.Count > 0 && uses[0].PlayerName == "Alice" && uses.Any(u => !u.Success && u.Detail.StartsWith("Cooldown")), "recent uses are recorded newest first, failures included");

        var noProvider = new FakeKitRunner { CanDeliver = false };
        var offline = new HeadlessTeleportService(new FakePathProfile(Path.Combine(tempRoot, "teleport-np-" + Guid.NewGuid().ToString("N"))), new HeadlessActivityLogService(paths), noProvider, new FakeChatSource(), _ => Task.FromResult(online));
        offline.SaveConfig(new TeleportConfig(true, "!tp", 0, [new TeleportPoint("spawn", 1, 2, null)]));
        await offline.ProcessLinesAsync([Chat("Alice", "steam_1", "!tp spawn")], CancellationToken.None);
        Assert(noProvider.Commands.Count == 0, "without PalDefender/RCON nothing is sent");

        // No live player list (REST API off): the sender cannot be confirmed, so nothing is done at all.
        var blindRunner = new FakeKitRunner();
        var unavailable = new HeadlessPlayersSnapshot(false, 0, [], DateTimeOffset.UtcNow, "REST off");
        var blind = new HeadlessTeleportService(new FakePathProfile(Path.Combine(tempRoot, "teleport-blind-" + Guid.NewGuid().ToString("N"))), new HeadlessActivityLogService(paths), blindRunner, new FakeChatSource(), _ => Task.FromResult(unavailable));
        blind.SaveConfig(new TeleportConfig(true, "!tp", 0, [new TeleportPoint("spawn", 1, 2, null)]));
        await blind.ProcessLinesAsync([Chat("Alice", "steam_1", "!tp spawn")], CancellationToken.None);
        Assert(blindRunner.Commands.Count == 0 && blind.GetSnapshot().RecentUses.Single().Detail.Contains("cannot be confirmed"), "without the live player list a chat command is recorded as unconfirmed and nothing is sent");
    }, failures);
    RunScenarioAsync("Teleport admin actions: Send uses a saved point for an online player, Capture reads PalDefender's getpos, both refuse honestly", async () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "teleport-admin-" + Guid.NewGuid().ToString("N")));
        var runner = new FakeKitRunner();
        var service = new HeadlessTeleportService(paths, new HeadlessActivityLogService(paths), runner, new FakeChatSource(), _ => Task.FromResult(Snapshot(("steam_1", "Alice"))));
        service.SaveConfig(new TeleportConfig(false, "!tp", 60, [new TeleportPoint("spawn", 5, 6, null)]));

        var sent = await service.SendToPointAsync("SPAWN", "steam_1", CancellationToken.None);
        Assert(sent.Success && runner.Commands.SequenceEqual(["tp steam_1 5 6"]), $"Send works even with the chat command off: {sent.Message}");
        Assert(!(await service.SendToPointAsync("nowhere", "steam_1", CancellationToken.None)).Success, "an unknown point is refused");
        Assert(!(await service.SendToPointAsync("spawn", "steam_9", CancellationToken.None)).Success && runner.Commands.Count == 1, "an offline player is refused without sending");

        runner.Reply = "Alice position: X=-358.2 Y=270.5 Z=1200.75";
        var captured = await service.CaptureAsync("steam_1", CancellationToken.None);
        Assert(captured.Success && captured.X == -358.2 && captured.Y == 270.5 && captured.Z == 1200.75 && runner.Commands.Last() == "getpos steam_1", $"Capture parses getpos: {captured.Message}");
        runner.Reply = "Error: player not found";
        Assert(!(await service.CaptureAsync("steam_1", CancellationToken.None)).Success, "an error reply is not a position");
    }, failures);
    // ---- v0.7.114.0 Multi-user login -----------------------------------------------------------------------
    RunScenario("User accounts: passwords are salted PBKDF2, and bad usernames, short passwords and duplicates are refused", () =>
    {
        var h1 = HeadlessUserAccountService.HashPassword("correct horse battery");
        var h2 = HeadlessUserAccountService.HashPassword("correct horse battery");
        Assert(h1.StartsWith("pbkdf2-sha256$210000$") && h1 != h2, "stored as pbkdf2-sha256 with the iteration count, and salted (same password, different hash)");
        Assert(HeadlessUserAccountService.VerifyPassword("correct horse battery", h1) && !HeadlessUserAccountService.VerifyPassword("correct horse batterY", h1), "verifies the right password only");
        Assert(!HeadlessUserAccountService.VerifyPassword("x", "plain-text") && !HeadlessUserAccountService.VerifyPassword("x", "pbkdf2-sha256$1$AA==$AA=="), "malformed or weak stored hashes never verify");

        var paths = new FakePathProfile(Path.Combine(tempRoot, "users-validate-" + Guid.NewGuid().ToString("N")));
        var users = new HeadlessUserAccountService(paths, new HeadlessActivityLogService(paths));
        Assert(!users.Create(new("a b", null, MystTiqRole.Viewer, "long enough pw", null), "t").Success, "a username with a space is refused");
        Assert(!users.Create(new("ab", null, MystTiqRole.Viewer, "long enough pw", null), "t").Success, "a 2-character username is refused");
        Assert(!users.Create(new("alice", null, MystTiqRole.Viewer, "short", null), "t").Success, "a password under 10 characters is refused");
        Assert(users.Create(new("alice", "Alice A", MystTiqRole.Operator, "long enough pw", null), "t").Success, "a valid account is created");
        Assert(!users.Create(new("ALICE", null, MystTiqRole.Viewer, "long enough pw", null), "t").Success, "usernames are unique ignoring case");
        var listed = users.List().Single();
        Assert(listed.Username == "alice" && listed.DisplayName == "Alice A" && listed.Role == MystTiqRole.Operator && listed.Enabled, "the account lists with its details");
        var onDisk = File.ReadAllText(Path.Combine(paths.ManagerRuntimeRoot, "security", "users.json"));
        Assert(!onDisk.Contains("long enough pw"), "the password is never written to disk");
    }, failures);
    RunScenario("User accounts: sign-in gives a session that carries the account's role live; wrong passwords, disabled accounts and lockout are refused alike", () =>
    {
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var paths = new FakePathProfile(Path.Combine(tempRoot, "users-login-" + Guid.NewGuid().ToString("N")));
        var users = new HeadlessUserAccountService(paths, new HeadlessActivityLogService(paths), () => now);
        var alice = users.Create(new("alice", "Alice A", MystTiqRole.Operator, "long enough pw", "second-local"), "owner").Account!;

        var login = users.Login("Alice", "long enough pw");
        Assert(login.Success && login.Token is { Length: 64 } && login.ExpiresUtc == now.AddHours(12), "sign-in is case-insensitive on the username and gives a 12-hour session");
        var principal = users.Authenticate(login.Token!);
        Assert(principal is { Name: "Alice A", Role: MystTiqRole.Operator, ScopedServerProfileId: "second-local" } && principal.Id == "user:" + alice.Id, "the session resolves to the account's name, role and server scope");
        Assert(!File.ReadAllText(Path.Combine(paths.ManagerRuntimeRoot, "security", "sessions.json")).Contains(login.Token!), "only the session token's hash is stored");

        users.Update(alice.Id, new("Alice A", MystTiqRole.Admin, null, true), "owner");
        Assert(users.Authenticate(login.Token!)?.Role == MystTiqRole.Admin, "a role change applies to an open session at once");

        var wrong = users.Login("alice", "not the password");
        var unknown = users.Login("nobody", "not the password");
        Assert(!wrong.Success && !unknown.Success && wrong.Message == unknown.Message, "a wrong password and an unknown username get the same message");

        for (var i = 0; i < 4; i++) users.Login("alice", "still wrong!!");
        var locked = users.Login("alice", "long enough pw");
        Assert(!locked.Success && locked.Message.Contains("Too many failed"), "5 failures lock the account, even against the right password");
        Assert(users.List().Single().LockedOut, "the lockout shows on the account");
        now = now.AddMinutes(16);
        Assert(users.Login("alice", "long enough pw").Success, "the lockout ends after 15 minutes");

        users.Update(alice.Id, new("Alice A", MystTiqRole.Admin, null, false), "owner");
        Assert(users.Authenticate(login.Token!) is null, "disabling the account ends its sessions at once");
        Assert(!users.Login("alice", "long enough pw").Success, "a disabled account cannot sign in");
        users.Update(alice.Id, new("Alice A", MystTiqRole.Admin, null, true), "owner");

        var second = users.Login("alice", "long enough pw").Token!;
        users.SetPassword(alice.Id, "a brand new password", "owner");
        Assert(users.Authenticate(second) is null && users.Login("alice", "a brand new password").Success, "a password reset signs the account out and the new password works");

        var third = users.Login("alice", "a brand new password").Token!;
        Assert(new HeadlessUserAccountService(paths, new HeadlessActivityLogService(paths), () => now).Authenticate(third) is not null, "a session survives a MystTiq restart");
        now = now.AddHours(12).AddSeconds(1);
        Assert(users.Authenticate(third) is null, "a session ends after 12 hours");

        now = now.AddMinutes(1);
        var fourth = users.Login("alice", "a brand new password").Token!;
        Assert(users.Logout(fourth) && users.Authenticate(fourth) is null, "sign-out ends the session");
        users.Delete(alice.Id, "owner");
        Assert(!users.Login("alice", "a brand new password").Success && users.List().Count == 0, "a deleted account is gone");
    }, failures);
    RunScenario("User accounts: changing your own password needs the current one, and only works for a signed-in account", () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "users-own-" + Guid.NewGuid().ToString("N")));
        var users = new HeadlessUserAccountService(paths, new HeadlessActivityLogService(paths));
        users.Create(new("bob", null, MystTiqRole.Viewer, "bobs password1", null), "owner");
        var me = users.Authenticate(users.Login("bob", "bobs password1").Token!)!;
        Assert(!users.ChangeOwnPassword(me, "wrong current", "bobs password2").Success, "the current password must be right");
        Assert(users.ChangeOwnPassword(me, "bobs password1", "bobs password2").Success && users.Login("bob", "bobs password2").Success, "with it, the password changes");
        Assert(!users.ChangeOwnPassword(MystTiqPrincipal.LegacyOwner, "x", "long enough pw").Success, "the shared token is not an account and has no password to change");
    }, failures);
    RunScenario("Teleport chat log source skips old history, reads only complete new lines, follows a new PalDefender log, and survives truncation", () =>
    {
        var paths = new FakePathProfile(Path.Combine(tempRoot, "teleport-tail-" + Guid.NewGuid().ToString("N")));
        var logs = Path.Combine(paths.RuntimeBinaryRoot, "PalDefender", "Logs");
        Directory.CreateDirectory(logs);
        var first = Path.Combine(logs, "22.09 10.00.00.log");
        File.WriteAllText(first, "old line from before MystTiq started\n");
        File.SetCreationTimeUtc(first, DateTime.UtcNow.AddHours(-1));
        var source = new PalDefenderChatLogSource(paths);
        Assert(source.ReadNewLines().Count == 0, "lines already in a pre-existing log are not replayed");

        File.AppendAllText(first, "new line\npartial");
        Assert(source.ReadNewLines().SequenceEqual(["new line"]), "only complete new lines are returned");
        File.AppendAllText(first, " line done\n");
        Assert(source.ReadNewLines().SequenceEqual(["partial line done"]), "a partial line is returned once it is complete");

        Thread.Sleep(20);
        var second = Path.Combine(logs, "22.09 11.00.00.log");
        File.WriteAllText(second, "fresh session line\n");
        File.SetLastWriteTimeUtc(second, DateTime.UtcNow.AddSeconds(5));
        Assert(source.ReadNewLines().SequenceEqual(["fresh session line"]), "a log created after MystTiq started (a new server session) is read from its start");

        File.WriteAllText(second, "after truncate\n");
        File.SetLastWriteTimeUtc(second, DateTime.UtcNow.AddSeconds(10));
        Assert(source.ReadNewLines().SequenceEqual(["after truncate"]), "a truncated or rotated log is read again from the start");
    }, failures);

    // v0.8.17.0: process priority and eco mode.
    RunScenario("Priority and eco mode: the rules (eco on/off/when empty, never on a guess), the targets, validation, niceness and the /proc parsers", () =>
    {
        var now = DateTimeOffset.Parse("2026-09-24T12:00:00Z");
        var whenEmpty = new ServerResourcePolicy(ServerPriorityLevel.Normal, ServerEcoMode.WhenEmpty, 10);
        Assert(!ResourcePolicyDecision.Eco(new ServerResourcePolicy(), 0, now.AddHours(-5), now).Active, "eco mode off never turns on, even long empty");
        Assert(ResourcePolicyDecision.Eco(new ServerResourcePolicy(EcoMode: ServerEcoMode.On), 5, null, now).Active, "eco mode on is on even with players online");
        var unknown = ResourcePolicyDecision.Eco(whenEmpty, null, now.AddHours(-1), now);
        Assert(!unknown.Active && unknown.Reason.Contains("cannot be read"), "when empty: an unknown player count never starts eco mode, and says why");
        Assert(!ResourcePolicyDecision.Eco(whenEmpty, 2, now.AddHours(-1), now).Active, "when empty: players online means full speed");
        var waiting = ResourcePolicyDecision.Eco(whenEmpty, 0, now.AddMinutes(-4), now);
        Assert(!waiting.Active && waiting.Reason.Contains("in 6 min"), $"when empty: 4 of 10 minutes empty waits 6 more ({waiting.Reason})");
        Assert(ResourcePolicyDecision.Eco(whenEmpty, 0, now.AddMinutes(-10), now).Active, "when empty: exactly the chosen minutes empty turns eco mode on");
        Assert(ResourcePolicyDecision.NextEmptySince(null, 0, now) == now && ResourcePolicyDecision.NextEmptySince(now.AddMinutes(-3), 0, now) == now.AddMinutes(-3),
            "the empty clock starts at the first empty reading and keeps running");
        Assert(ResourcePolicyDecision.NextEmptySince(now.AddMinutes(-3), 1, now) is null && ResourcePolicyDecision.NextEmptySince(now.AddMinutes(-3), null, now) is null,
            "a player joining, or an unknown count, resets the empty clock");
        Assert(ResourcePolicyDecision.TargetPriority(new ServerResourcePolicy(ServerPriorityLevel.High), true) == System.Diagnostics.ProcessPriorityClass.BelowNormal &&
               ResourcePolicyDecision.TargetPriority(new ServerResourcePolicy(ServerPriorityLevel.High), false) == System.Diagnostics.ProcessPriorityClass.High &&
               ResourcePolicyDecision.TargetPriority(new ServerResourcePolicy(), false) is null, "eco mode runs below normal; otherwise the chosen priority; Default leaves it alone");
        Assert(ResourcePolicyDecision.TargetEfficiency(true, false) == true && ResourcePolicyDecision.TargetEfficiency(false, true) == false &&
               ResourcePolicyDecision.TargetEfficiency(false, false) is null, "efficiency mode is only switched back on processes MystTiq put into it");
        Assert(new ServerResourcePolicy(EcoAfterEmptyMinutes: 0).Validate().Count == 1 && new ServerResourcePolicy(EcoAfterEmptyMinutes: 241).Validate().Count == 1 &&
               new ServerResourcePolicy(EcoAfterEmptyMinutes: 1).Validate().Count == 0 && new ServerResourcePolicy(EcoAfterEmptyMinutes: 240).Validate().Count == 0,
            "minutes before eco mode must be 1..240");
        foreach (var level in new[] { System.Diagnostics.ProcessPriorityClass.BelowNormal, System.Diagnostics.ProcessPriorityClass.Normal, System.Diagnostics.ProcessPriorityClass.AboveNormal, System.Diagnostics.ProcessPriorityClass.High })
            Assert(ProcessResourceControl.FromNiceness(ProcessResourceControl.Niceness(level)) == level, $"{level} maps to niceness {ProcessResourceControl.Niceness(level)} and back");
        var before = HostMetricsReader.ParseProcStatCpu("cpu  100 0 50 800 50 0 0 0 0 0");
        var after = HostMetricsReader.ParseProcStatCpu("cpu  200 0 100 900 100 0 0 0 0 0");
        Assert(before == new CpuTimes(850, 1000) && HostMetricsReader.CpuPercent(before!.Value, after!.Value) == 50d,
            "/proc/stat: idle includes iowait; busy share between two readings");
        Assert(HostMetricsReader.CpuPercent(after.Value, after.Value) is null, "counters that did not move give no reading, not 0 %");
        Assert(HostMetricsReader.ParseMemInfo(["MemTotal:       16318192 kB", "MemFree:  100 kB", "MemAvailable:    8000000 kB"]) == new HostMemory(16318192UL * 1024, 8000000UL * 1024),
            "/proc/meminfo: total and available");
        // Found live on the Hyper-V host: Windows lists each NDIS filter on an adapter as an adapter of its own.
        string[] adapters = ["Ethernet", "vEthernet (MystTiq External)", "vSwitch (MystTiq External)", "Ethernet 2", "Ethernet-WFP Native MAC Layer LightWeight Filter-0000",
            "vEthernet (MystTiq External)-QoS Packet Scheduler-0000", "vSwitch (MystTiq External)-Hyper-V Virtual Switch Extension Filter-0000", "Ethernet 2-WFP 802.3 MAC Layer LightWeight Filter-0000"];
        var shown = adapters.Where(a => !MystTiq.HeadlessHost.HeadlessHostMonitor.IsFilterLayer(a, adapters)).ToArray();
        Assert(shown.SequenceEqual(["Ethernet", "vEthernet (MystTiq External)", "vSwitch (MystTiq External)", "Ethernet 2"]),
            $"network adapters: filter layers of another adapter are not listed again ({string.Join(" | ", shown)})");
        Assert(!MystTiq.HeadlessHost.HeadlessHostMonitor.IsFilterLayer("Office-0001", ["Home"]) && !MystTiq.HeadlessHost.HeadlessHostMonitor.IsFilterLayer("eth0", ["eth0"]),
            "an adapter merely ending in digits, or with no adapter it belongs to, stays");
    }, failures);

    RunScenario("Priority and eco mode: the service applies only changes, turns eco on after the empty minutes and off when a player joins, logs each once, keeps the policy", () =>
    {
        var root = Path.Combine(tempRoot, "resource-policy");
        var paths = new FakePathProfile(root);
        Directory.CreateDirectory(paths.ManagerRuntimeRoot);
        var activity = new HeadlessActivityLogService(paths);
        var server = new ScriptedLifecycle { Running = true };
        var control = new FakeResourceControl();
        control.Priority[100] = System.Diagnostics.ProcessPriorityClass.Normal;
        int? online = 3;
        var now = DateTimeOffset.Parse("2026-09-24T12:00:00Z");
        var service = new HeadlessResourcePolicyService(paths, server, _ => Task.FromResult(online), activity, control, () => now);

        service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(control.PrioritySets == 0 && control.EfficiencySets == 0, "the default policy changes nothing");

        var saved = service.SaveAsync(new ServerResourcePolicy(ServerPriorityLevel.AboveNormal), "tester", CancellationToken.None).GetAwaiter().GetResult();
        Assert(saved.Success && control.Priority[100] == System.Diagnostics.ProcessPriorityClass.AboveNormal && control.PrioritySets == 1, "saving applies the priority at once");
        service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(control.PrioritySets == 1, "a process already at the wanted priority is not touched again");

        service.SaveAsync(new ServerResourcePolicy(ServerPriorityLevel.AboveNormal, ServerEcoMode.WhenEmpty, 10), null, CancellationToken.None).GetAwaiter().GetResult();
        online = 0;
        now = now.AddMinutes(1);
        var first = service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(!first.EcoActive && first.EmptySinceUtc == now && first.EcoReason.Contains("in 10 min"), $"the first empty reading starts the clock ({first.EcoReason})");
        now = now.AddMinutes(10);
        var eco = service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(eco.EcoActive && control.Priority[100] == System.Diagnostics.ProcessPriorityClass.BelowNormal && control.Efficiency[100] == EfficiencyState.On,
            "after 10 empty minutes: below normal and efficiency mode on");
        online = 1;
        now = now.AddSeconds(15);
        var back = service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(!back.EcoActive && control.Priority[100] == System.Diagnostics.ProcessPriorityClass.AboveNormal && control.Efficiency[100] == EfficiencyState.Default,
            "a player joining: back to the chosen priority, efficiency mode back to the system default");
        var log = File.ReadAllText(Path.Combine(paths.ManagerRuntimeRoot, "logs", "MystTiq-Activity.log"));
        Assert(System.Text.RegularExpressions.Regex.Matches(log, "Eco mode on").Count == 1 && System.Text.RegularExpressions.Regex.Matches(log, "Eco mode off").Count == 1,
            "eco mode on and off are each logged once");

        control.Efficiency[100] = EfficiencyState.On; // someone else switched it on
        service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(control.Efficiency[100] == EfficiencyState.On, "efficiency mode set outside MystTiq is left alone");

        control.Fail = true;
        control.Priority[100] = System.Diagnostics.ProcessPriorityClass.Normal;
        var failed = service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(failed.LastError.Contains("needs root") && failed.LastError.Contains("PID 100"), $"a refused change is reported, not thrown ({failed.LastError})");
        control.Fail = false;

        var linux = new FakeResourceControl { SupportsEfficiencyMode = false };
        linux.Priority[100] = System.Diagnostics.ProcessPriorityClass.Normal;
        var linuxRoot = new FakePathProfile(Path.Combine(root, "linux"));
        Directory.CreateDirectory(linuxRoot.ManagerRuntimeRoot);
        var onLinux = new HeadlessResourcePolicyService(linuxRoot, server, _ => Task.FromResult<int?>(0), new HeadlessActivityLogService(linuxRoot), linux, () => now);
        onLinux.SaveAsync(new ServerResourcePolicy(EcoMode: ServerEcoMode.On), null, CancellationToken.None).GetAwaiter().GetResult();
        Assert(linux.Priority[100] == System.Diagnostics.ProcessPriorityClass.BelowNormal && linux.EfficiencySets == 0, "without efficiency mode (Linux) eco mode lowers the priority only");

        var reloaded = new HeadlessResourcePolicyService(paths, server, _ => Task.FromResult(online), activity, control, () => now);
        Assert(reloaded.GetPolicy() == new ServerResourcePolicy(ServerPriorityLevel.AboveNormal, ServerEcoMode.WhenEmpty, 10), "the policy is kept across a restart of MystTiq");
        File.WriteAllText(Path.Combine(paths.ManagerRuntimeRoot, "resource-policy.json"), "{ not json");
        Assert(new HeadlessResourcePolicyService(paths, server, _ => Task.FromResult(online), activity, control, () => now).GetPolicy() == new ServerResourcePolicy(),
            "a damaged policy file falls back to changing nothing");
        server.Running = false;
        var stopped = service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(!stopped.Running && !stopped.EcoActive && stopped.EcoReason.Contains("not running"), "a stopped server has nothing to apply");
    }, failures);
    // v0.8.24.0: processor cores, and the policy applied as soon as a start succeeds.
    RunScenario("Processor cores: the list is parsed and checked against the machine, described back, and turned into the mask the operating system takes", () =>
    {
        Assert(CoreSelection.Parse("0-3, 6", 12) is ({ } cores, null) && cores.SequenceEqual([0, 1, 2, 3, 6]), "\"0-3, 6\" is cores 0, 1, 2, 3 and 6");
        Assert(CoreSelection.Parse(null, 12) == (null, null) && CoreSelection.Parse("  ", 12) == (null, null), "an empty list means every core");
        Assert(CoreSelection.Parse("3-1", 12).Error!.Contains("backwards") && CoreSelection.Parse("12", 12).Error!.Contains("cores 0-11")
            && CoreSelection.Parse("x", 12).Error!.Contains("not a core") && CoreSelection.Parse("-1", 12).Error is not null && CoreSelection.Parse(",", 12).Error!.Contains("at least one"),
            "backwards ranges, missing cores, words, negative numbers and an empty selection are refused with a reason");
        Assert(CoreSelection.Parse("0-63", 128).Error is null && CoreSelection.Parse("64", 128).Error!.Contains("0-63"),
            "at most 64 cores (one Windows processor group), even on a bigger machine");
        Assert(CoreSelection.Mask([0, 1, 2, 3, 6]) == 0b1001111UL && CoreSelection.AllCoresMask(12) == 0xFFFUL && CoreSelection.AllCoresMask(64) == ulong.MaxValue,
            "the mask has one bit per core");
        Assert(CoreSelection.Describe(0b1001111UL, 12) == "0-3, 6" && CoreSelection.Describe(0xFFFUL, 12) == "all 12" && CoreSelection.Describe(0b11UL, 12) == "0, 1",
            "a mask is described the way it is written");
        Assert(new ServerResourcePolicy(Cores: "0-20").Validate(12).Single().Contains("cores 0-11") && new ServerResourcePolicy(Cores: "0-3").Validate(12).Count == 0,
            "the policy checks its cores against the machine");
        Assert(ResourcePolicyDecision.TargetAffinity(new ServerResourcePolicy(Cores: "2-3"), 12, false) == 0b1100UL
            && ResourcePolicyDecision.TargetAffinity(new ServerResourcePolicy(), 12, true) == 0xFFFUL
            && ResourcePolicyDecision.TargetAffinity(new ServerResourcePolicy(), 12, false) is null,
            "the chosen cores; every core again only where MystTiq had pinned them; otherwise left alone");
    }, failures);

    RunScenario("Processor cores: the service pins only what differs, logs it, gives every core back when the list is cleared, and leaves pinning done elsewhere alone", () =>
    {
        var root = Path.Combine(tempRoot, "resource-cores");
        var paths = new FakePathProfile(root);
        Directory.CreateDirectory(paths.ManagerRuntimeRoot);
        var activity = new HeadlessActivityLogService(paths);
        var server = new ScriptedLifecycle { Running = true };
        var control = new FakeResourceControl { AllCores = 0xFFFUL };
        control.Priority[100] = System.Diagnostics.ProcessPriorityClass.Normal;
        var service = new HeadlessResourcePolicyService(paths, server, _ => Task.FromResult<int?>(1), activity, control, () => DateTimeOffset.UtcNow, processorCount: 12);

        var saved = service.SaveAsync(new ServerResourcePolicy(Cores: "0-3"), "tester", CancellationToken.None).GetAwaiter().GetResult();
        Assert(saved.Success && control.Affinity[100] == 0b1111UL && control.AffinitySets == 1 && control.PrioritySets == 0,
            "saving cores 0-3 pins the server to them and changes nothing else");
        var again = service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(control.AffinitySets == 1 && again.Processes.Single().Cores == "0-3" && again.ProcessorCount == 12, "a process already on those cores is not touched again, and the HOST tab sees them");
        var bad = service.SaveAsync(new ServerResourcePolicy(Cores: "0-40"), null, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!bad.Success && bad.Message.Contains("cores 0-11") && service.GetPolicy().Cores == "0-3", "cores the machine does not have are refused and the saved policy kept");

        service.SaveAsync(new ServerResourcePolicy(), null, CancellationToken.None).GetAwaiter().GetResult();
        Assert(control.Affinity[100] == 0xFFFUL && control.AffinitySets == 2, "clearing the list gives every core back to a process MystTiq pinned");
        control.Affinity[100] = 0b11UL; // pinned by someone else
        service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(control.Affinity[100] == 0b11UL && control.AffinitySets == 2, "pinning done outside MystTiq is left alone");
        var log = File.ReadAllText(Path.Combine(paths.ManagerRuntimeRoot, "logs", "MystTiq-Activity.log"));
        Assert(log.Contains("cores set to 0-3") && log.Contains("cores set to all 12") && log.Contains("cores 0-3."), "each change and the saved cores are in the activity log");

        control.FailAffinity = true;
        service.SaveAsync(new ServerResourcePolicy(Cores: "5"), null, CancellationToken.None).GetAwaiter().GetResult();
        var refused = service.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(refused.LastError.Contains("cores") && refused.LastError.Contains("PID 100"), $"a refused change is reported, not thrown ({refused.LastError})");
        Assert(new HeadlessResourcePolicyService(paths, server, _ => Task.FromResult<int?>(1), activity, control, () => DateTimeOffset.UtcNow, processorCount: 12).GetPolicy().Cores == "5",
            "the cores are kept across a restart of MystTiq");
        Assert(new HeadlessResourcePolicyService(paths, server, _ => Task.FromResult<int?>(1), activity, control, () => DateTimeOffset.UtcNow, processorCount: 4).GetPolicy() == new ServerResourcePolicy(),
            "a saved list naming cores this machine does not have falls back to changing nothing");
        Assert(System.Text.Json.JsonSerializer.Deserialize<ServerResourcePolicy>("{\"Priority\":\"High\",\"EcoMode\":\"Off\",\"EcoAfterEmptyMinutes\":10}")! == new ServerResourcePolicy(ServerPriorityLevel.High),
            "a policy saved before v0.8.24.0 (no cores) still loads, as every core");
    }, failures);

    RunScenario("Apply at start: a successful start or restart through MystTiq applies the policy at once; a failed one does not, and a failing apply never fails the start", () =>
    {
        var inner = new ScriptedLifecycle { Running = false };
        var wrapped = new MystTiq.HeadlessHost.PolicyApplyingLifecycle(inner);
        var applied = 0;
        wrapped.AfterStart = _ => { applied++; return Task.CompletedTask; };
        var started = wrapped.StartAsync([], TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        Assert(started.Success && applied == 1, "a successful start applies it once");
        inner.StartSucceeds = false;
        var failed = wrapped.StartAsync([], TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        Assert(!failed.Success && applied == 1, "a failed start does not");
        inner.StartSucceeds = true;
        wrapped.AfterStart = _ => throw new InvalidOperationException("apply failed");
        Assert(wrapped.StartAsync([], TimeSpan.FromSeconds(1)).GetAwaiter().GetResult().Success, "an apply that fails leaves the start successful");
        wrapped.AfterStart = _ => { applied++; return Task.CompletedTask; };
        var restarted = wrapped.RestartAsync([], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        Assert(restarted.Success && applied == 2, "a restart applies it too");
        Assert(wrapped.StopAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult() is not null && applied == 2, "a stop does not");
    }, failures);

    // v0.8.18.0: bandwidth.
    RunScenario("Bandwidth: Engine.ini gets exactly MystTiq's three keys (set or removed), every other line kept, the section found in any case", () =>
    {
        var engineWritten = "[Core.System]\r\nPaths=../../../Engine/Content\r\nPaths=%GAMEDIR%Content\r\n\r\n";
        var custom = new ServerNetworkPolicy(ServerNetworkMode.Custom, 4, 45);
        var added = EngineIniNetworkSection.Apply(engineWritten, custom);
        Assert(added.StartsWith(engineWritten.TrimEnd('\r', '\n'), StringComparison.Ordinal) && added.Contains("\r\n[/Script/OnlineSubsystemUtils.IpNetDriver]\r\nMaxClientRate=500000\r\nMaxInternetClientRate=500000\r\nNetServerMaxTickRate=45\r\n"),
            $"the engine's own lines stay; the section is added with CRLF (4 Mbit/s = 500000 bytes/s)");
        Assert(EngineIniNetworkSection.Read(added) == new EngineNetworkValues(500000, 500000, 45), "the values read back");
        Assert(EngineIniNetworkSection.Apply(added, custom) == added, "applying the same policy again changes nothing");
        var back = EngineIniNetworkSection.Apply(added, new ServerNetworkPolicy());
        Assert(!EngineIniNetworkSection.Read(back).Any && back.Contains("Paths=%GAMEDIR%Content"), "game defaults remove the three keys and nothing else");

        var guide = "[/script/onlinesubsystemutils.ipnetdriver]\nNetServerMaxTickRate=120\nMaxClientRate=104857600\nConnectionTimeout=30.0\n[/Script/Engine.Player]\nConfiguredInternetSpeed=104857600\n";
        Assert(EngineIniNetworkSection.Read(guide) == new EngineNetworkValues(104857600, null, 120), "a lower-case section from a hosting guide is read");
        var replaced = EngineIniNetworkSection.Apply(guide, custom);
        Assert(replaced.Contains("[/script/onlinesubsystemutils.ipnetdriver]\nConnectionTimeout=30.0\nMaxClientRate=500000\nMaxInternetClientRate=500000\nNetServerMaxTickRate=45\n[/Script/Engine.Player]") &&
               !replaced.Contains("\r\n") && !replaced.Contains("104857600\nConnection") && replaced.Contains("ConfiguredInternetSpeed=104857600"),
            "the existing section is reused: old values replaced, its other keys and the other sections kept, LF kept");
        var twice = guide + "[/Script/OnlineSubsystemUtils.IpNetDriver]\nMaxClientRate=1\n";
        Assert(EngineIniNetworkSection.Read(EngineIniNetworkSection.Apply(twice, custom)) == new EngineNetworkValues(500000, 500000, 45) &&
               System.Text.RegularExpressions.Regex.Matches(EngineIniNetworkSection.Apply(twice, custom), "MaxClientRate=").Count == 1,
            "a second copy of the section loses its old keys, so exactly one value of each is left");
        Assert(EngineIniNetworkSection.Apply("", new ServerNetworkPolicy()) == "" && EngineIniNetworkSection.Apply("", custom).StartsWith("[/Script/OnlineSubsystemUtils.IpNetDriver]\n"),
            "an empty file stays empty for game defaults and gets just the section for custom limits");
    }, failures);

    RunScenario("Bandwidth: validation, the game's real defaults, the planner, and the write before start (once, with the original kept)", () =>
    {
        Assert(new ServerNetworkPolicy(ServerNetworkMode.Custom, 0.2, 60).Validate().Count == 1 && new ServerNetworkPolicy(ServerNetworkMode.Custom, 101, 60).Validate().Count == 1 &&
               new ServerNetworkPolicy(ServerNetworkMode.Custom, 8, 9).Validate().Count == 1 && new ServerNetworkPolicy(ServerNetworkMode.Custom, 8, 121).Validate().Count == 1 &&
               new ServerNetworkPolicy(ServerNetworkMode.Custom, 0.25, 10, 1).Validate().Count == 0 && new ServerNetworkPolicy(UploadBudgetMbps: 0).Validate().Count == 1,
            "limits: 0.25..100 Mbit/s per player, 10..120 updates per second, an upload of at least 1 Mbit/s");
        Assert(PalworldNetworkDefaults.PerPlayerMbps == 64 && PalworldNetworkDefaults.TickRate(false) == 60 && PalworldNetworkDefaults.TickRate(true) == 20,
            "the game's own defaults (from its pak): 64 Mbit/s per player, 60 updates on Windows, 20 on Linux");
        Assert(BandwidthPlanner.WorstCaseMbps(32, 64) == 2048 && BandwidthPlanner.FitPerPlayerMbps(40, 32) == 1 && BandwidthPlanner.FitPerPlayerMbps(100, 16) == 5 &&
               BandwidthPlanner.FitPerPlayerMbps(1, 32) == 0.25 && BandwidthPlanner.FitPerPlayerMbps(100000, 1) == 100,
            "the planner: 32 players at the default could need 2048 Mbit/s; a 40 Mbit/s upload fits 1 Mbit/s each; within the limits");

        var root = Path.Combine(tempRoot, "bandwidth");
        var paths = new FakePathProfile(root);
        Directory.CreateDirectory(paths.ConfigRoot);
        var engine = EngineNetworkSettings.EngineIniPath(paths);
        File.WriteAllText(engine, "[Core.System]\r\nPaths=../../../Engine/Content\r\n");
        Assert(EngineNetworkSettings.ApplyBeforeStart(paths) is null && !File.Exists(EngineNetworkSettings.OriginalBackupPath(paths)), "no policy: nothing is written");
        EngineNetworkSettings.SavePolicy(paths, new ServerNetworkPolicy(ServerNetworkMode.Custom, 2, 30));
        var line = EngineNetworkSettings.ApplyBeforeStart(paths);
        Assert(line is not null && line.Contains("2 Mbit/s") && EngineNetworkSettings.ReadEngineIni(paths) == new EngineNetworkValues(250000, 250000, 30), $"the policy is written before start ({line})");
        Assert(File.ReadAllText(EngineNetworkSettings.OriginalBackupPath(paths)) == "[Core.System]\r\nPaths=../../../Engine/Content\r\n", "the Engine.ini as it was is kept once");
        Assert(EngineNetworkSettings.ApplyBeforeStart(paths) is null, "a second start with the same policy writes nothing");
        EngineNetworkSettings.SavePolicy(paths, new ServerNetworkPolicy(ServerNetworkMode.Custom, 3, 30));
        EngineNetworkSettings.ApplyBeforeStart(paths);
        Assert(File.ReadAllText(EngineNetworkSettings.OriginalBackupPath(paths)) == "[Core.System]\r\nPaths=../../../Engine/Content\r\n", "the kept original is not replaced by a later change");
        File.WriteAllText(EngineNetworkSettings.PolicyPath(paths), "{ damaged");
        Assert(EngineNetworkSettings.LoadPolicy(paths) == new ServerNetworkPolicy(), "a damaged policy file means game defaults");
        var locked = new FakePathProfile(Path.Combine(root, "no-config"));
        Directory.CreateDirectory(locked.ManagerRuntimeRoot);
        File.WriteAllText(Path.Combine(locked.ManagerRuntimeRoot, "network-policy.json"), "{\"Mode\":\"Custom\",\"PerPlayerMbps\":2,\"TickRate\":30}");
        Directory.CreateDirectory(Path.GetDirectoryName(locked.ConfigRoot)!);
        File.WriteAllText(locked.ConfigRoot, "a file where the config folder should be");
        var failed = EngineNetworkSettings.ApplyBeforeStart(locked);
        Assert(failed is not null && failed.Contains("could not be written"), $"a write that fails is reported, never thrown, so the server still starts ({failed})");
    }, failures);

    // v0.8.20.0: host history.
    RunScenario("Host history: one reading per minute kept 7 days, thinned by averaging with peaks from every reading, kept across a restart", () =>
    {
        var now = DateTimeOffset.Parse("2026-09-25T12:00:00Z");
        var snapshot = new MystTiq.HeadlessHost.HeadlessHostSnapshot("PC", "OS", "CPU", 8, 40, 16_000, 4_000, 100, [],
            [new("vEthernet", "switch", "Ethernet", null, 500_000, 2_000_000, 0, 0), new("Ethernet", "nic", "Ethernet", null, 500_000, 2_000_000, 0, 0)], now);
        var sample = MystTiq.HeadlessHost.HostHistoryMath.FromSnapshot(snapshot, now);
        Assert(sample.MemoryUsedPercent == 75 && sample.CpuPercent == 40 && sample.SentBytesPerSecond == 2_000_000 && sample.Adapter == "vEthernet",
            "a reading: memory used 75 %, the busiest adapter only (a virtual switch and its adapter are not added up)");

        var readings = Enumerable.Range(0, 1440).Select(i => new MystTiq.HeadlessHost.HostHistorySample(now.AddMinutes(i - 1440), i == 700 ? 99 : 10, 50, i == 900 ? 9_000_000 : 1000, 100, "Ethernet")).ToArray();
        var day = MystTiq.HeadlessHost.HostHistoryMath.Summarize(readings, TimeSpan.FromHours(24), 600, now);
        Assert(day.Samples.Count <= 600 && day.Samples.Count >= 480 && day.ReadingsInRange == 1440, $"a day of minute readings is thinned to at most 600 points ({day.Samples.Count})");
        Assert(day.PeakCpuPercent == 99 && day.PeakSentBytesPerSecond == 9_000_000 && Math.Abs(day.AverageCpuPercent!.Value - (10 * 1439 + 99) / 1440d) < 1e-9,
            "the peaks and averages come from every reading, not the thinned points");
        var none = MystTiq.HeadlessHost.HostHistoryMath.Summarize([], TimeSpan.FromHours(1), 600, now);
        Assert(none.ReadingsInRange == 0 && none.PeakCpuPercent is null && none.FirstReadingAt is null, "no readings: no numbers, not zeros");

        var root = Path.Combine(tempRoot, "host-history");
        var clock = now;
        var history = new MystTiq.HeadlessHost.HeadlessHostHistoryService(root, new MystTiq.HeadlessHost.HeadlessHostMonitor(), clock: () => clock);
        history.Add(sample with { ObservedAt = now.AddDays(-8) });
        history.Add(sample with { ObservedAt = now.AddHours(-2) });
        history.Add(sample with { ObservedAt = now.AddMinutes(-30) });
        Assert(history.Snapshot(1).ReadingsInRange == 1 && history.Snapshot(24).ReadingsInRange == 2 && history.Snapshot(24 * 30).ReadingsInRange == 2,
            "older than 7 days is dropped; the range picks the readings (and is capped at 7 days)");
        Assert(File.Exists(history.HistoryPath), "the history is written to the fleet folder");
        history.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        var reloaded = new MystTiq.HeadlessHost.HeadlessHostHistoryService(root, new MystTiq.HeadlessHost.HeadlessHostMonitor(), clock: () => clock);
        Assert(reloaded.Snapshot(24).ReadingsInRange == 2, "the history survives a restart of MystTiq");
        File.WriteAllText(reloaded.HistoryPath, "not json");
        Assert(new MystTiq.HeadlessHost.HeadlessHostHistoryService(root, new MystTiq.HeadlessHost.HeadlessHostMonitor(), clock: () => clock).Snapshot(24).ReadingsInRange == 0,
            "an unreadable history starts empty");
    }, failures);

    // v0.8.21.0: the systemd unit lets the service raise the server's priority again.
    RunScenario("Linux service unit: LimitNICE=-11 so eco mode can be undone, no capability added, the rest unchanged", () =>
    {
        var unit = LinuxSystemdServiceManager.BuildUnitText("palhost", "/etc/mysttiq/mysttiq.json", MystTiq.Core.Operations.ServerProfileId.Default);
        var lines = unit.Split('\n');
        Assert(lines.Contains("LimitNICE=-11") && lines.Contains("NoNewPrivileges=true") && lines.Contains("User=palhost"),
            "the unit allows niceness down to -11 (High), keeps NoNewPrivileges, runs as the given user");
        Assert(!unit.Contains("AmbientCapabilities", StringComparison.Ordinal) && !unit.Contains("CapabilityBoundingSet", StringComparison.Ordinal),
            "no capability is added");
        Assert(lines.Contains("ExecStart=/opt/mysttiq/bin/mysttiq-server service-run --config \"/etc/mysttiq/mysttiq.json\"") &&
               LinuxSystemdServiceManager.BuildUnitText("palhost", "/etc/mysttiq/mysttiq.json", new MystTiq.Core.Operations.ServerProfileId("second")).Contains("--server-id second\n"),
            "the default server runs without --server-id, another server with its own");
        Assert(ProcessResourceControl.Niceness(System.Diagnostics.ProcessPriorityClass.High) >= -11,
            "the limit covers every priority MystTiq offers (High is niceness -11)");
    }, failures);

    // v0.8.19.0: secure by default.
    RunScenario("Route roles: a route that declares no role needs Viewer to read and Admin to change anything", () =>
    {
        Assert(RbacEndpointExtensions.DefaultMinimum("GET") == MystTiqRole.Viewer && RbacEndpointExtensions.DefaultMinimum("HEAD") == MystTiqRole.Viewer,
            "reading (GET, HEAD) needs Viewer");
        Assert(new[] { "POST", "PUT", "DELETE", "PATCH" }.All(m => RbacEndpointExtensions.DefaultMinimum(m) == MystTiqRole.Admin),
            "every changing method needs Admin unless the route itself allows a lower role");
        Assert(new RequiredRoleMetadata(MystTiqRole.Operator).Minimum == MystTiqRole.Operator, "a route's own role is recorded on it");
    }, failures);
}
finally
{
    try { Directory.Delete(tempRoot, recursive: true); } catch { /* best effort */ }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"{failures.Count} scenario(s) FAILED:");
    foreach (var f in failures) Console.WriteLine($"  - {f}");
    return 1;
}

Console.WriteLine();
Console.WriteLine("All logic-harness scenarios passed.");
return 0;

static void RunScenario(string name, Action action, List<string> failures)
{
    try
    {
        action();
        Console.WriteLine($"[PASS] {name}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] {name}: {ex.Message}");
        failures.Add(name);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AssertThrows<TException>(Action action, string message) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"{message} (expected {typeof(TException).Name}, none was thrown)");
}

static void RunScenarioAsync(string name, Func<Task> action, List<string> failures) =>
    RunScenario(name, () => action().GetAwaiter().GetResult(), failures);

static string Describe(List<(string Action, string PlayerId)> calls) =>
    calls.Count == 0 ? "(no calls)" : string.Join(", ", calls.Select(c => $"{c.Action}({c.PlayerId})"));

static (HeadlessWhitelistService Whitelist, FakeModerationProvider Fake) Build(string tempRoot, string scenarioName)
{
    var paths = new FakePathProfile(Path.Combine(tempRoot, scenarioName));
    var activity = new HeadlessActivityLogService(paths);
    var fake = new FakeModerationProvider();
    var coordinator = new PlayerModerationCoordinator([fake]);
    return (new HeadlessWhitelistService(paths, activity, coordinator), fake);
}

static HeadlessPlayersSnapshot Snapshot(params (string Id, string Name)[] players) => new(
    true,
    players.Length,
    players.Select(p => new HeadlessPlayerSnapshot(p.Name, p.Id, p.Id, p.Id, "", "", "Steam", "1", "0")).ToList(),
    DateTimeOffset.UtcNow,
    "test");

sealed class FakePathProfile : IServerPathProfile
{
    private readonly string root;
    public FakePathProfile(string root)
    {
        this.root = root;
        Directory.CreateDirectory(root);
    }
    public string PlatformId => "test";
    public string ServerRoot => root;
    public string ServerExecutable => Path.Combine(root, "PalServer.exe");
    public string RuntimeBinaryRoot => root;
    public string Ue4ssRoot => root;
    public string Ue4ssModsRoot => root;
    public string LegacyUe4ssModsRoot => root;
    public string SaveRoot => Path.Combine(root, "Saved");
    public string ConfigRoot => Path.Combine(root, "Config");
    public string LogsRoot => Path.Combine(root, "Logs");
    public string SteamCmdExecutable => Path.Combine(root, "steamcmd.exe");
    public string BackupRoot => Path.Combine(root, "Backups");
    public string ManagerRuntimeRoot => Path.Combine(root, "Runtime");
}

// v0.7.101.0: a server whose state a scenario controls, so the crash-recovery loop can be run for real.
sealed class ScriptedLifecycle : IServerLifecycleService
{
    public bool Running;
    public bool CrashFlag;
    public bool StartSucceeds = true;
    public int Starts;
    public ServerLifecyclePhase Phase = ServerLifecyclePhase.Running;
    // v0.7.115.0: null = ready whenever running (the old behaviour); false = the process runs but its game
    // port never comes up.
    public bool? ReadyOverride;

    public Task<ServerLifecycleSnapshot> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ServerLifecycleSnapshot(Phase, Running ? 100 : null,
            Running ? [new ServerSessionProcessInfo(100, 1, "PalServer", "PalServer.exe", true)] : [],
            [], Running && (ReadyOverride ?? true), CrashFlag, DateTimeOffset.UtcNow, null, "scripted"));

    public async Task<ServerLifecycleOperationResult> StartAsync(IReadOnlyList<string> serverArguments, TimeSpan startupTimeout, CancellationToken cancellationToken = default)
    {
        Starts++;
        if (StartSucceeds) { Running = true; CrashFlag = false; Phase = ServerLifecyclePhase.Running; }
        var snapshot = await GetStatusAsync(cancellationToken);
        return new ServerLifecycleOperationResult(StartSucceeds ? HeadlessExitCode.Success : HeadlessExitCode.LaunchFailed, snapshot, false, StartSucceeds ? "started" : "the executable would not launch");
    }

    // v0.8.24.0: stop and restart, for the apply-at-start wrapper.
    public async Task<ServerLifecycleOperationResult> StopAsync(TimeSpan gracefulTimeout, CancellationToken cancellationToken = default)
    {
        Running = false;
        return new ServerLifecycleOperationResult(HeadlessExitCode.Success, await GetStatusAsync(cancellationToken), false, "stopped");
    }
    public async Task<ServerLifecycleOperationResult> RestartAsync(IReadOnlyList<string> serverArguments, TimeSpan startupTimeout, TimeSpan gracefulTimeout, CancellationToken cancellationToken = default)
    {
        await StopAsync(gracefulTimeout, cancellationToken);
        return await StartAsync(serverArguments, startupTimeout, cancellationToken);
    }
    public Task<IReadOnlyList<ServerInstanceInfo>> FindAllInstancesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ServerInstanceInfo>>([]);
    public Task<InstanceTerminationResult> TerminateUnmanagedInstanceAsync(int processId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

// v0.8.17.0: a stand-in for the operating system's priority and efficiency switches.
sealed class FakeResourceControl : IProcessResourceControl
{
    public Dictionary<int, System.Diagnostics.ProcessPriorityClass> Priority { get; } = [];
    public Dictionary<int, EfficiencyState> Efficiency { get; } = [];
    public int PrioritySets, EfficiencySets;
    public bool Fail;
    public bool SupportsEfficiencyMode { get; init; } = true;
    public System.Diagnostics.ProcessPriorityClass? GetPriority(int processId) => Priority.TryGetValue(processId, out var p) ? p : null;
    public EfficiencyState GetEfficiency(int processId) => SupportsEfficiencyMode ? Efficiency.GetValueOrDefault(processId, EfficiencyState.Default) : EfficiencyState.Unknown;
    public void SetPriority(int processId, System.Diagnostics.ProcessPriorityClass priority)
    {
        if (Fail) throw new UnauthorizedAccessException("raising a priority again needs root or CAP_SYS_NICE");
        Priority[processId] = priority; PrioritySets++;
    }
    public void SetEfficiency(int processId, bool on) { Efficiency[processId] = on ? EfficiencyState.On : EfficiencyState.Default; EfficiencySets++; }
    // v0.8.24.0: processor cores; a process not yet pinned runs on every core.
    public Dictionary<int, ulong> Affinity { get; } = [];
    public ulong AllCores { get; init; } = 0xFFFUL;
    public int AffinitySets;
    public bool FailAffinity;
    public ulong? GetAffinity(int processId) => Affinity.TryGetValue(processId, out var mask) ? mask : AllCores;
    public void SetAffinity(int processId, ulong mask)
    {
        if (FailAffinity) throw new UnauthorizedAccessException("it belongs to another user");
        Affinity[processId] = mask; AffinitySets++;
    }
}

sealed class RecordingObserver(bool throws = false) : ISupervisorObserver
{
    public List<SupervisorEvent> Events { get; } = [];
    public Task OnEventAsync(SupervisorEvent supervisorEvent, CancellationToken cancellationToken)
    {
        lock (Events) Events.Add(supervisorEvent);
        if (throws) throw new InvalidOperationException("the observer failed");
        return Task.CompletedTask;
    }
}

sealed class FakeChatSource : IChatLineSource
{
    public IReadOnlyList<string> ReadNewLines() => [];
    public string Describe() => "fake";
}

sealed class FakeKitRunner : IKitCommandRunner
{
    public bool CanDeliver { get; set; } = true;
    public string Reply { get; set; } = "ok";
    public List<string> Commands { get; } = [];
    public KitProviderStatus GetStatus() => new(CanDeliver, "fake", CanDeliver ? "ready" : "no provider");
    public Task<KitCommandResult> RunAsync(string command, CancellationToken cancellationToken)
    {
        Commands.Add(command);
        return Task.FromResult(new KitCommandResult(true, Reply, "sent"));
    }
}

sealed class FakeModerationProvider : IPlayerModerationProvider
{
    public List<(string Action, string PlayerId)> Calls { get; } = [];
    public string ProviderId => "fake";
    public string DisplayName => "Fake Provider";
    public Task<ProviderDescriptor> GetHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ProviderDescriptor(ProviderId, DisplayName, ProviderHealth.Healthy, "ok"));
    public bool SupportsAction(string action) => action == "kick";
    public Task<PlayerModerationResult> ExecuteAsync(string action, string playerId, string? message, CancellationToken cancellationToken)
    {
        Calls.Add((action, playerId));
        return Task.FromResult(new PlayerModerationResult(true, true, ProviderId, action, playerId, "kicked"));
    }
}

// Minimal IServerPathProfile stand-in for RCON scenarios: only ConfigRoot is actually read
// (PalworldSettingsConfigurationService.ConfigurationPath = Path.Combine(ConfigRoot,
// "PalWorldSettings.ini")); the rest are plausible-but-unused subfolders.
sealed class HarnessPathProfile(string configRoot) : IServerPathProfile
{
    public string PlatformId => "test";
    public string ServerRoot => configRoot;
    public string ServerExecutable => Path.Combine(configRoot, "PalServer.exe");
    public string RuntimeBinaryRoot => configRoot;
    public string Ue4ssRoot => configRoot;
    public string Ue4ssModsRoot => configRoot;
    public string LegacyUe4ssModsRoot => configRoot;
    public string SaveRoot => Path.Combine(configRoot, "Saved");
    public string ConfigRoot => configRoot;
    public string LogsRoot => Path.Combine(configRoot, "Logs");
    public string SteamCmdExecutable => Path.Combine(configRoot, "steamcmd.exe");
    public string BackupRoot => Path.Combine(configRoot, "Backups");
    public string ManagerRuntimeRoot => Path.Combine(configRoot, "Runtime");
}

// v0.7.68.0: a minimal, real Source RCON server -- just enough of the wire protocol
// (PalworldRconService's own WritePacketAsync/ReadPacketAsync framing: 4-byte LE length, then
// id(4)+type(4)+body+0x00+0x00) to authenticate one connection and echo back whatever EXECCOMMAND
// it receives, so the harness can assert the *exact* command text MystTiq's fix sends over the
// wire, not just that the surrounding C# compiles.
sealed class StubRconServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly string expectedPassword;
    private readonly string canedResponse;
    private readonly Task acceptTask;
    public int Port { get; }
    public string? ReceivedCommand { get; private set; }

    public StubRconServer(string expectedPassword, string canedResponse)
    {
        this.expectedPassword = expectedPassword;
        this.canedResponse = canedResponse;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        acceptTask = Task.Run(AcceptOnceAsync);
    }

    private async Task AcceptOnceAsync()
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();

            var auth = await ReadPacketAsync(stream);
            if (auth.Body != expectedPassword)
            {
                await WritePacketAsync(stream, -1, 2, string.Empty);
                return;
            }
            await WritePacketAsync(stream, auth.Id, 2, string.Empty);

            var exec = await ReadPacketAsync(stream);
            ReceivedCommand = exec.Body;
            await WritePacketAsync(stream, exec.Id, 0, canedResponse);
        }
        catch { /* connection may close once the harness scenario has what it needs */ }
    }

    private static async Task WritePacketAsync(NetworkStream stream, int id, int type, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var payloadSize = bodyBytes.Length + 10;
        var buffer = new byte[payloadSize + 4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), payloadSize);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), id);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8, 4), type);
        bodyBytes.CopyTo(buffer.AsSpan(12));
        await stream.WriteAsync(buffer);
        await stream.FlushAsync();
    }

    private static async Task<(int Id, int Type, string Body)> ReadPacketAsync(NetworkStream stream)
    {
        var lengthBytes = new byte[4];
        await ReadExactlyAsync(stream, lengthBytes);
        var size = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        var payload = new byte[size];
        await ReadExactlyAsync(stream, payload);
        var id = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4));
        var type = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));
        var bodyLength = Math.Max(0, size - 10);
        var body = Encoding.UTF8.GetString(payload, 8, bodyLength).TrimEnd('\0');
        return (id, type, body);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..]);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    public void Dispose()
    {
        listener.Stop();
        try { acceptTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* best effort */ }
    }
}

// v0.7.69.0: reports no processes and no listening ports at all -- exactly "PalServer is not
// running" from WindowsServerLifecycleService's point of view, the condition that exercises its
// StopAsync "nothing to stop" branch.
sealed class EmptySessionInspector : IServerSessionInspector
{
    public ServerSessionSnapshot Capture(long sessionId, int rootPid) =>
        new(sessionId, rootPid, DateTime.UtcNow, [], [], []);
    public IReadOnlySet<int> GetDescendantProcessIds(int rootPid) => new HashSet<int>();
    public IReadOnlyList<ServerSessionProcessInfo> FindProcessesByName(IEnumerable<string> names) => [];
    public IReadOnlyList<int> GetGuardedListeningPorts() => [];
}

sealed record ReceivedEmail(string AuthenticatedUsername, string AuthenticatedPassword, string MailFrom, string RcptTo, string Subject, string Body);

// v0.7.70.0: a minimal, real SMTP server -- just enough of RFC 5321 (greeting, EHLO advertising
// AUTH LOGIN, the AUTH LOGIN base64 username/password exchange, MAIL FROM/RCPT TO/DATA, and QUIT)
// to let the real System.Net.Mail.SmtpClient inside HeadlessNotificationRoutingService complete a
// full plaintext (EnableSsl=false) submission against localhost, so the harness can assert exactly
// what envelope/auth/content the fix actually sent, not just that the surrounding C# compiles.
sealed class StubSmtpServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly string expectedUsername;
    private readonly string expectedPassword;
    private readonly TaskCompletionSource<ReceivedEmail> received = new();
    private readonly Task acceptTask;
    public int Port { get; }

    public StubSmtpServer(string expectedUsername, string expectedPassword)
    {
        this.expectedUsername = expectedUsername;
        this.expectedPassword = expectedPassword;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        acceptTask = Task.Run(AcceptOnceAsync);
    }

    public async Task<ReceivedEmail?> WaitForMessageAsync(TimeSpan timeout)
    {
        var completed = await Task.WhenAny(received.Task, Task.Delay(timeout));
        return completed == received.Task ? received.Task.Result : null;
    }

    private async Task AcceptOnceAsync()
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
            await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };

            await writer.WriteLineAsync("220 stub.local ESMTP");

            string? username = null, password = null, mailFrom = "", rcptTo = "", subject = "", body = "";

            while (await reader.ReadLineAsync() is { } line)
            {
                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250-stub.local greets you");
                    await writer.WriteLineAsync("250 AUTH LOGIN PLAIN");
                }
                else if (line.StartsWith("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
                {
                    // SmtpClient sends the base64 username as a SASL "initial response" on the same
                    // line ("AUTH LOGIN <base64-username>") rather than waiting for a separate
                    // 334-Username: prompt -- only the password comes as a genuinely separate line.
                    // Support both shapes (inline and the classic two-step) for robustness.
                    var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        username = Encoding.ASCII.GetString(Convert.FromBase64String(parts[2]));
                    }
                    else
                    {
                        await writer.WriteLineAsync("334 " + Convert.ToBase64String(Encoding.ASCII.GetBytes("Username:")));
                        username = Encoding.ASCII.GetString(Convert.FromBase64String((await reader.ReadLineAsync())!));
                    }
                    await writer.WriteLineAsync("334 " + Convert.ToBase64String(Encoding.ASCII.GetBytes("Password:")));
                    password = Encoding.ASCII.GetString(Convert.FromBase64String((await reader.ReadLineAsync())!));
                    var ok = username == expectedUsername && password == expectedPassword;
                    await writer.WriteLineAsync(ok ? "235 Authentication successful" : "535 Authentication failed");
                }
                else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase))
                {
                    mailFrom = line;
                    await writer.WriteLineAsync("250 OK");
                }
                else if (line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
                {
                    rcptTo = line;
                    await writer.WriteLineAsync("250 OK");
                }
                else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("354 Start mail input; end with <CRLF>.<CRLF>");
                    var inHeaders = true;
                    var bodyLines = new List<string>();
                    while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                    {
                        if (inHeaders && dataLine.Length == 0) { inHeaders = false; continue; }
                        if (inHeaders && dataLine.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
                            subject = dataLine["Subject:".Length..].Trim();
                        else if (!inHeaders)
                            bodyLines.Add(dataLine);
                    }
                    body = string.Join('\n', bodyLines);
                    await writer.WriteLineAsync("250 OK: queued");
                    received.TrySetResult(new ReceivedEmail(username ?? "", password ?? "", mailFrom, rcptTo, subject, body));
                }
                else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 Bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 OK");
                }
            }
        }
        catch { /* connection may close once the harness scenario has what it needs */ }
    }

    public void Dispose()
    {
        listener.Stop();
        try { acceptTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* best effort */ }
    }
}
