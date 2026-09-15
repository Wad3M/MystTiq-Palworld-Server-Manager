using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessMonitoringService
{
    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly object metricsGate = new();

    private readonly Dictionary<int, (DateTimeOffset ObservedAt, TimeSpan CpuTime)> previousProcessSamples = new();

    public HeadlessMonitoringService(IServerPathProfile paths, IServerLifecycleService lifecycle)
    {
        this.paths = paths;
        this.lifecycle = lifecycle;
    }

    public async Task<HeadlessPlayersSnapshot> GetPlayersAsync(CancellationToken cancellationToken)
    {
        var settingsPath = Path.Combine(paths.ConfigRoot, "PalWorldSettings.ini");
        if (!File.Exists(settingsPath))
            return UnavailablePlayers($"PalWorldSettings.ini was not found at {settingsPath}.");

        string text;
        try
        {
            text = await File.ReadAllTextAsync(settingsPath, cancellationToken);
        }
        catch (Exception ex)
        {
            return UnavailablePlayers($"Unable to read PalWorldSettings.ini: {ex.Message}");
        }

        if (ReadBooleanOption(text, "RESTAPIEnabled") is not true)
            return UnavailablePlayers("Palworld REST API is disabled in PalWorldSettings.ini.");

        var restPort = ReadIntegerOption(text, "RESTAPIPort") ?? 8212;
        var adminPassword = ReadQuotedOption(text, "AdminPassword");

        if (string.IsNullOrWhiteSpace(adminPassword))
            return UnavailablePlayers("Palworld REST API is enabled, but AdminPassword is empty.");

        try
        {
            using var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            };

            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri($"http://127.0.0.1:{restPort}/v1/api/"),
                Timeout = TimeSpan.FromSeconds(10),
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
            };

            var raw = $"admin:{adminPassword}";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));

            using var request = new HttpRequestMessage(HttpMethod.Get, "players")
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            request.Headers.ConnectionClose = true;

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return UnavailablePlayers($"Palworld REST /players returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");

            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var players = new List<HeadlessPlayerSnapshot>();

            if (document.RootElement.TryGetProperty("players", out var array) &&
                array.ValueKind == JsonValueKind.Array)
            {
                foreach (var player in array.EnumerateArray())
                {
                    var userId = GetAny(player, "userId", "userid");
                    var steamId = GetAny(player, "steamId", "steamid");

                    players.Add(new HeadlessPlayerSnapshot(
                        GetAny(player, "name"),
                        userId,
                        steamId,
                        GetAny(player, "playerId", "playerid"),
                        GetAny(player, "ip"),
                        GetAny(player, "ping"),
                        DetectPlayerPlatform(userId, steamId),
                        GetAny(player, "level"),
                        GetAny(player, "buildingCount", "buildingcount", "building_count"),
                        GetAny(player, "location_x", "locationX", "x"),
                        GetAny(player, "location_y", "locationY", "y")));
                }
            }

            var ordered = players
                .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(p => p.UserId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new HeadlessPlayersSnapshot(
                true,
                ordered.Count,
                ordered,
                DateTimeOffset.UtcNow,
                ordered.Count == 0
                    ? "Palworld REST API is available; no players are online."
                    : $"{ordered.Count} player(s) online.");
        }
        catch (Exception ex)
        {
            return UnavailablePlayers($"Palworld REST player monitoring is unavailable: {ex.Message}");
        }
    }

    public HeadlessLogTailSnapshot GetLogTail(int requestedLines)
    {
        var maxLines = Math.Clamp(requestedLines, 10, 500);
        var sources = ResolveConsoleSources().ToList();
        if (sources.Count == 0)
        {
            return new HeadlessLogTailSnapshot(
                false, null, [], DateTimeOffset.UtcNow,
                $"No Palworld/MystTiq console log source was found beneath {paths.LogsRoot}.");
        }

        try
        {
            if (sources.Count == 1)
            {
                var only = sources[0];
                var lines = ReadTailLines(only.Path, maxLines, 1024 * 1024);
                return new HeadlessLogTailSnapshot(true, only.Label, lines, DateTimeOffset.UtcNow,
                    $"Showing the newest {lines.Count} line(s) from {only.Label}.");
            }

            // v0.6.14.0: a trailing merged.TakeLast(maxLines) here used to silently crop out an
            // entire earlier source once a later source's own chunk alone filled the requested
            // window -- reproduced live: MystTiq's own lifecycle/stdout log (added first, and the
            // one carrying every Start/Stop narrative line) was completely evicted by Pal.log's
            // chunk (added second) even though the MystTiq lines were the freshest content by far.
            // Each source is already independently bounded to perSource lines via ReadTailLines, so
            // the merged total is naturally bounded too (sources.Count * (perSource + 1 header)) --
            // no further global crop needed, and every source's own tail is now genuinely preserved.
            var merged = new List<string>();
            var perSource = Math.Max(12, maxLines / sources.Count);
            foreach (var source in sources)
            {
                merged.Add($"[{source.Label}]");
                merged.AddRange(ReadTailLines(source.Path, perSource, 1024 * 1024));
            }

            return new HeadlessLogTailSnapshot(
                true,
                string.Join(" + ", sources.Select(x => x.Label)),
                merged,
                DateTimeOffset.UtcNow,
                "Combined MystTiq lifecycle/stdout, Pal.log, and available server-mod log evidence into the in-app console.");
        }
        catch (Exception ex)
        {
            return new HeadlessLogTailSnapshot(
                false, (sources.Count > 0 ? sources[0].Label : null), [], DateTimeOffset.UtcNow,
                $"Unable to read Palworld log tail: {ex.Message}");
        }
    }

    private IEnumerable<(string Label, string Path)> ResolveConsoleSources()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(List<(string Label, string Path)> list, string label, string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var full = Path.GetFullPath(path);
                if (seen.Add(full)) list.Add((label, full));
            }
            catch { }
        }

        var result = new List<(string Label, string Path)>();
        Add(result, "MystTiq redirected stdout/stderr + lifecycle", Path.Combine(paths.LogsRoot, "MystTiq-PalServer-Console.log"));
        Add(result, "Pal.log", Path.Combine(paths.LogsRoot, "Pal.log"));
        // v0.7.50.0: Pal.log is where these lines assumed real PalServer/UE4SS activity would land,
        // but confirmed live against a real, actively-modded production install that this file is
        // never actually created by Palworld's Windows dedicated server build (consistent with
        // -ABSLOG also producing nothing, per the v0.7.46.0/v0.7.47.0 console-capture investigation)
        // -- so this source has silently never contributed anything on a real server. UE4SS DOES
        // write its own real, substantial log (hook registrations, hundreds of hundreds of lines per
        // session) to UE4SS.log, which this method never looked for at all -- confirmed present with
        // real content (1947 lines from the current session alone) on the same real install.
        Add(result, "UE4SS.log", Path.Combine(paths.Ue4ssRoot, "UE4SS.log"));
        Add(result, "UE4SS.log (legacy layout)", Path.Combine(paths.RuntimeBinaryRoot, "UE4SS.log"));

        var adminLogs = Path.Combine(paths.RuntimeBinaryRoot, "ue4ss", "Mods", "AdminCommands", "Scripts", "logs", "serverlogs");
        var latestAdmin = FindNewestTextLog(adminLogs);
        if (latestAdmin is not null) Add(result, "AdminCommands server log", latestAdmin);

        var legacyAdminLogs = Path.Combine(paths.RuntimeBinaryRoot, "Mods", "AdminCommands", "Scripts", "logs", "serverlogs");
        var latestLegacyAdmin = FindNewestTextLog(legacyAdminLogs);
        if (latestLegacyAdmin is not null) Add(result, "AdminCommands legacy log", latestLegacyAdmin);

        // v0.7.71.0: PalDefender (a UE4SS-loaded anti-cheat mod) writes its own timestamped
        // per-session log under its own Logs folder -- confirmed live against a real production
        // install to carry exactly the content a user-reported console screenshot showed (PalDefender
        // startup narration, its REST API port, and even PalServer's own "Running Palworld dedicated
        // server on :PORT" banner line, which apparently reaches PalDefender's own logger too) that
        // no other source here captures. Never looked for before this version.
        var palDefenderLogs = Path.Combine(paths.RuntimeBinaryRoot, "PalDefender", "Logs");
        var latestPalDefender = FindNewestTextLog(palDefenderLogs);
        if (latestPalDefender is not null) Add(result, "PalDefender log", latestPalDefender);

        if (result.Count == 0)
        {
            var fallback = ResolveActiveLogPath();
            if (fallback is not null) Add(result, Path.GetFileName(fallback), fallback);
        }
        return result;
    }

    private static string? FindNewestTextLog(string directory)
    {
        if (!Directory.Exists(directory)) return null;
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }

    // v0.7.9.0: Palworld's official /v1/api/metrics endpoint reports the game's own simulation
    // performance (serverfps/serverframetime), which degrades with base/Pal count independent of
    // host-level CPU% -- the thing operators actually watch for lag, which the process-level
    // sampling below cannot see. Reuses the exact REST client construction GetPlayersAsync already
    // uses (same config file, same port/password, same Basic auth) rather than duplicating a
    // second copy of that setup. Best-effort: any failure here must not affect the host-level
    // metrics GetMetricsAsync already reports, so failures are swallowed to (null, null) rather
    // than thrown.
    private async Task<(double? Fps, double? FrameTimeMs)> GetGamePerformanceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settingsPath = Path.Combine(paths.ConfigRoot, "PalWorldSettings.ini");
            if (!File.Exists(settingsPath)) return (null, null);
            var text = await File.ReadAllTextAsync(settingsPath, cancellationToken);
            if (ReadBooleanOption(text, "RESTAPIEnabled") is not true) return (null, null);

            var restPort = ReadIntegerOption(text, "RESTAPIPort") ?? 8212;
            var adminPassword = ReadQuotedOption(text, "AdminPassword");
            if (string.IsNullOrWhiteSpace(adminPassword)) return (null, null);

            using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri($"http://127.0.0.1:{restPort}/v1/api/"),
                Timeout = TimeSpan.FromSeconds(5),
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"admin:{adminPassword}")));

            using var request = new HttpRequestMessage(HttpMethod.Get, "metrics") { Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
            request.Headers.ConnectionClose = true;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return (null, null);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            double? fps = document.RootElement.TryGetProperty("serverfps", out var fpsElement) && fpsElement.TryGetDouble(out var fpsValue) ? fpsValue : null;
            double? frameTime = document.RootElement.TryGetProperty("serverframetime", out var frameTimeElement) && frameTimeElement.TryGetDouble(out var frameTimeValue) ? frameTimeValue : null;
            return (fps, frameTime);
        }
        catch
        {
            return (null, null);
        }
    }

    public async Task<HeadlessRuntimeMetricsSnapshot> GetMetricsAsync(CancellationToken cancellationToken)
    {
        var status = await lifecycle.GetStatusAsync(cancellationToken);
        var gamePerformance = await GetGamePerformanceAsync(cancellationToken);
        var processIds = status.Processes.Select(x => x.ProcessId)
            .Concat(status.NativeProcessId.HasValue ? [status.NativeProcessId.Value] : Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (processIds.Length == 0)
        {
            ResetMetricSample();
            return new HeadlessRuntimeMetricsSnapshot(
                false, null, null, 0, 0, DateTimeOffset.UtcNow,
                "PalServer is not currently running.",
                gamePerformance.Fps, gamePerformance.FrameTimeMs);
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            long workingSet = 0;
            var threads = 0;
            var totalCpuPercent = 0d;
            var cpuSamples = 0;
            var liveIds = new HashSet<int>();

            foreach (var processId in processIds)
            {
                try
                {
                    using var process = Process.GetProcessById(processId);
                    process.Refresh();
                    liveIds.Add(processId);
                    workingSet += Math.Max(0, process.WorkingSet64);
                    threads += process.Threads.Count;
                    var totalCpu = process.TotalProcessorTime;

                    lock (metricsGate)
                    {
                        if (previousProcessSamples.TryGetValue(processId, out var previous))
                        {
                            var wallMs = (now - previous.ObservedAt).TotalMilliseconds;
                            var cpuMs = (totalCpu - previous.CpuTime).TotalMilliseconds;
                            if (wallMs > 0 && cpuMs >= 0)
                            {
                                totalCpuPercent += cpuMs / wallMs / Math.Max(1, Environment.ProcessorCount) * 100d;
                                cpuSamples++;
                            }
                        }
                        previousProcessSamples[processId] = (now, totalCpu);
                    }
                }
                catch (ArgumentException)
                {
                    // Process exited between lifecycle discovery and metrics collection.
                }
                catch (InvalidOperationException)
                {
                    // Process exited while being sampled.
                }
            }

            lock (metricsGate)
            {
                foreach (var staleId in previousProcessSamples.Keys.Where(id => !liveIds.Contains(id)).ToArray())
                    previousProcessSamples.Remove(staleId);
            }

            if (liveIds.Count == 0)
            {
                ResetMetricSample();
                return new HeadlessRuntimeMetricsSnapshot(false, status.NativeProcessId, null, 0, 0, now,
                    "PalServer processes exited before runtime metrics could be sampled.",
                    gamePerformance.Fps, gamePerformance.FrameTimeMs);
            }

            var cpuPercent = cpuSamples > 0 ? Math.Clamp(totalCpuPercent, 0d, 100d) : (double?)null;
            return new HeadlessRuntimeMetricsSnapshot(
                true,
                status.NativeProcessId ?? liveIds.First(),
                cpuPercent,
                workingSet,
                threads,
                now,
                cpuPercent.HasValue
                    ? $"PalServer runtime metrics aggregated across {liveIds.Count} managed process(es)."
                    : $"PalServer runtime metrics baseline captured across {liveIds.Count} managed process(es); CPU percent will be available on the next sample.",
                gamePerformance.Fps,
                gamePerformance.FrameTimeMs);
        }
        catch (Exception ex)
        {
            ResetMetricSample();
            return new HeadlessRuntimeMetricsSnapshot(
                false, status.NativeProcessId, null, 0, 0, DateTimeOffset.UtcNow,
                $"Unable to sample PalServer runtime metrics: {ex.Message}",
                gamePerformance.Fps, gamePerformance.FrameTimeMs);
        }
    }

    private HeadlessPlayersSnapshot UnavailablePlayers(string detail) =>
        new(false, 0, [], DateTimeOffset.UtcNow, detail);

    private void ResetMetricSample()
    {
        lock (metricsGate)
            previousProcessSamples.Clear();
    }

    private string? ResolveActiveLogPath()
    {
        if (!Directory.Exists(paths.LogsRoot))
            return null;

        // MystTiq-owned stdout/stderr capture is the canonical Live Console source when present.
        // This keeps the Windows PalServer console hidden while preserving the same output in the GUI.
        var capturedConsole = Path.Combine(paths.LogsRoot, "MystTiq-PalServer-Console.log");
        if (File.Exists(capturedConsole))
            return capturedConsole;

        var palLog = Path.Combine(paths.LogsRoot, "Pal.log");
        if (File.Exists(palLog))
            return palLog;

        try
        {
            return Directory.EnumerateFiles(paths.LogsRoot, "*.log", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .FirstOrDefault()
                ?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadTailLines(string path, int maxLines, int maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        var bytesToRead = (int)Math.Min(stream.Length, maxBytes);
        stream.Seek(-bytesToRead, SeekOrigin.End);

        var buffer = new byte[bytesToRead];
        var read = stream.Read(buffer, 0, bytesToRead);
        var text = Encoding.UTF8.GetString(buffer, 0, read);

        if (stream.Length > bytesToRead)
        {
            var firstNewLine = text.IndexOf('\n');
            if (firstNewLine >= 0)
                text = text[(firstNewLine + 1)..];
        }

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(maxLines)
            .ToList();
    }

    private static bool? ReadBooleanOption(string text, string key)
    {
        var match = Regex.Match(
            text,
            $@"(?:^|[,(])\s*{Regex.Escape(key)}\s*=\s*(True|False)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? bool.Parse(match.Groups[1].Value) : null;
    }

    private static int? ReadIntegerOption(string text, string key)
    {
        var match = Regex.Match(
            text,
            $@"(?:^|[,(])\s*{Regex.Escape(key)}\s*=\s*(\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success &&
               int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? ReadQuotedOption(string text, string key)
    {
        var match = Regex.Match(
            text,
            $@"(?:^|[,(])\s*{Regex.Escape(key)}\s*=\s*""((?:\\.|[^""])*)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
            return null;

        return match.Groups[1].Value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static string GetAny(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => value.ToString()
            };
        }

        return string.Empty;
    }

    private static string DetectPlayerPlatform(string userId, string steamId)
    {
        if (!string.IsNullOrWhiteSpace(steamId))
            return "Steam";
        if (!string.IsNullOrWhiteSpace(userId))
            return "Xbox / Crossplay";
        return "Unknown";
    }
}

// v0.6.16.0: LocationX/LocationY come from the same real-time /v1/api/players response every
// other field here already reads -- Palworld's official REST API returns flat "location_x"/
// "location_y" per online player (confirmed against the official docs and an independently
// reverse-derived OpenAPI spec that lands on the same field set). No mod or save decode needed;
// this was simply never captured before. String, matching every sibling field's convention here
// (Ping/Level/BuildingCount), parsed downstream where a numeric value is actually needed.
public sealed record HeadlessPlayerSnapshot(
    string Name,
    string UserId,
    string SteamId,
    string PlayerId,
    string Ip,
    string Ping,
    string Platform,
    string Level,
    string BuildingCount,
    string LocationX = "",
    string LocationY = "");

public sealed record HeadlessPlayersSnapshot(
    bool Available,
    int OnlineCount,
    IReadOnlyList<HeadlessPlayerSnapshot> Players,
    DateTimeOffset ObservedAt,
    string Detail);

public sealed record HeadlessLogTailSnapshot(
    bool Available,
    string? FileName,
    IReadOnlyList<string> Lines,
    DateTimeOffset ObservedAt,
    string Detail);

public sealed record HeadlessRuntimeMetricsSnapshot(
    bool Available,
    int? ProcessId,
    double? CpuPercent,
    long WorkingSetBytes,
    int ThreadCount,
    DateTimeOffset ObservedAt,
    string Detail,
    // v0.7.9.0: real in-game simulation performance from Palworld's own REST /metrics endpoint --
    // null (not 0) when the REST API is disabled/misconfigured/unreachable, same convention as
    // CpuPercent above, distinguishing "not available" from "genuinely zero".
    double? ServerFps = null,
    double? ServerFrameTimeMs = null);
