using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MystTiq.Core.Automation;
using MystTiq.Core.Models;
using MystTiq.Core.Providers;
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
