using System.Globalization;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.8.18.0: bandwidth for one server (see ServerNetworkPolicy). The policy is kept in the server's runtime folder; the
// lifecycle writes it into Engine.ini before every start. Saving while the server is stopped writes it at once; while it
// runs, it waits for the next start (the engine would overwrite a change made under it), and the page says a restart is
// needed.
public sealed class HeadlessNetworkPolicyService(
    IServerPathProfile paths,
    IServerLifecycleService lifecycle,
    PalworldSettingsConfigurationService palworldSettings,
    HeadlessActivityLogService activity)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<HeadlessNetworkPolicySnapshot> GetSnapshotAsync(CancellationToken token)
    {
        var status = await lifecycle.GetStatusAsync(token);
        return Describe(status.Processes.Count > 0, null);
    }

    public async Task<HeadlessNetworkPolicySaveResult> SaveAsync(ServerNetworkPolicy requested, string? actor, CancellationToken token)
    {
        var errors = requested.Validate();
        if (errors.Count > 0)
            return new HeadlessNetworkPolicySaveResult(false, string.Join(" ", errors), await GetSnapshotAsync(token));
        await gate.WaitAsync(token);
        try
        {
            EngineNetworkSettings.SavePolicy(paths, requested);
            var running = (await lifecycle.GetStatusAsync(token)).Processes.Count > 0;
            string? written = null;
            if (!running) written = EngineNetworkSettings.ApplyBeforeStart(paths);
            activity.Record("Information", "Performance", "Bandwidth saved",
                (requested.Mode == ServerNetworkMode.Custom
                    ? $"{requested.PerPlayerMbps.ToString("0.##", CultureInfo.InvariantCulture)} Mbit/s per player, {requested.TickRate} network updates per second."
                    : "The game's own defaults.") + (running ? " Applies at the next start." : " " + (written ?? "Engine.ini already matched.")), actor);
            var snapshot = Describe(running, written);
            var message = running
                ? "Saved. The server is running: it takes effect at the next start (restart the server to apply it now)."
                : written is not null && written.Contains("could not be written", StringComparison.Ordinal) ? written : "Saved and written to Engine.ini.";
            return new HeadlessNetworkPolicySaveResult(true, message, snapshot);
        }
        finally { gate.Release(); }
    }

    private HeadlessNetworkPolicySnapshot Describe(bool running, string? lastWrite)
    {
        var policy = EngineNetworkSettings.LoadPolicy(paths);
        var linux = OperatingSystem.IsLinux();
        var file = EngineNetworkSettings.ReadEngineIni(paths);
        var enginePath = EngineNetworkSettings.EngineIniPath(paths);
        string text;
        try { text = File.Exists(enginePath) ? File.ReadAllText(enginePath) : string.Empty; } catch { text = string.Empty; }
        var inFile = string.Equals(EngineIniNetworkSection.Apply(text, policy), text, StringComparison.Ordinal);

        var defaultPerPlayer = PalworldNetworkDefaults.PerPlayerMbps;
        var defaultTick = PalworldNetworkDefaults.TickRate(linux);
        // What the server gets: the file's values when it sets them, else the game's.
        var effectiveRate = file.MaxInternetClientRate ?? file.MaxClientRate;
        var effectivePerPlayer = effectiveRate is { } bytes ? bytes * 8d / 1_000_000 : defaultPerPlayer;
        var effectiveTick = file.NetServerMaxTickRate ?? defaultTick;

        var maxPlayers = MaxPlayers();
        var worstCase = maxPlayers is { } n ? BandwidthPlanner.WorstCaseMbps(n, effectivePerPlayer) : (double?)null;
        var overBudget = worstCase is { } w && policy.UploadBudgetMbps is { } budget && w > budget * BandwidthPlanner.Headroom;
        var suggested = maxPlayers is { } p && policy.UploadBudgetMbps is { } up ? BandwidthPlanner.FitPerPlayerMbps(up, p) : (double?)null;

        return new HeadlessNetworkPolicySnapshot(
            policy, running, inFile, running && !inFile,
            linux ? "Linux" : "Windows", defaultPerPlayer, defaultTick,
            file.MaxClientRate, file.MaxInternetClientRate, file.NetServerMaxTickRate,
            effectivePerPlayer, effectiveTick, maxPlayers, worstCase, overBudget, suggested,
            enginePath, lastWrite ?? string.Empty, DateTimeOffset.UtcNow);
    }

    private int? MaxPlayers()
    {
        try
        {
            var setting = palworldSettings.Load().Settings.FirstOrDefault(s => s.Name == "ServerPlayerMaxNum");
            var value = setting is null ? null : string.IsNullOrWhiteSpace(setting.Value) ? setting.DefaultValue : setting.Value;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;
        }
        catch { return null; }
    }
}

public sealed record HeadlessNetworkPolicySnapshot(
    ServerNetworkPolicy Policy,
    bool Running,
    bool PolicyInEngineIni,
    bool RestartNeeded,
    string Platform,
    double GameDefaultPerPlayerMbps,
    int GameDefaultTickRate,
    long? EngineMaxClientRate,
    long? EngineMaxInternetClientRate,
    int? EngineTickRate,
    double EffectivePerPlayerMbps,
    int EffectiveTickRate,
    int? MaxPlayers,
    double? WorstCaseUploadMbps,
    bool OverUploadBudget,
    double? SuggestedPerPlayerMbps,
    string EngineIniPath,
    string LastWrite,
    DateTimeOffset ObservedAt);

public sealed record HeadlessNetworkPolicySaveResult(bool Success, string Message, HeadlessNetworkPolicySnapshot Snapshot);
