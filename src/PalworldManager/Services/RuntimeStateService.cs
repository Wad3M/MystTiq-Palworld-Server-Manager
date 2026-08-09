using System.Security.Cryptography;
using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Owns authoritative, session-scoped MOD runtime state.
/// Positive runtime evidence is latched only for the active PalServer session and
/// is cleared at the session boundary. Consumers receive immutable snapshots.
/// </summary>
public sealed class RuntimeStateService
{
    private static readonly Regex StartedLuaModPattern = new(
        "Starting\\s+Lua\\s+mod\\s+[\"'](?<name>[^\"']+)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RuntimeErrorPattern = new(
        "(failed\\s+to\\s+load|load\\s+failed|unhandled\\s+exception|fatal)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly object gate = new();
    private readonly Dictionary<string, RuntimeLogCursor> logCursors = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> loadedAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> runtimeErrors = [];
    private string sessionId = "";
    private bool sessionActive;
    private DateTime? sessionStartedAt;
    private DateTime? lastObservedAt;
    private string? runtimeLogPath;
    private string runtimeHealth = "Inactive";
    private string runtimeWarning = "";
    private long revision;

    public event Action<RuntimeStateSnapshot>? StateChanged;

    public RuntimeStateSnapshot Current
    {
        get { lock (gate) return SnapshotUnsafe(); }
    }

    public RuntimeStateSnapshot BeginSession(Ue4ssRuntimeInfo baseline)
    {
        RuntimeStateSnapshot snapshot;
        lock (gate)
        {
            sessionId = Guid.NewGuid().ToString("N")[..12];
            sessionActive = true;
            sessionStartedAt = DateTime.Now;
            lastObservedAt = null;
            runtimeLogPath = baseline.RuntimeLogPath;
            runtimeHealth = baseline.HealthState;
            runtimeWarning = baseline.WarningMessage;
            loadedAliases.Clear();
            runtimeErrors.Clear();
            logCursors.Clear();
            CaptureSessionBaselinesUnsafe(baseline);
            revision++;
            snapshot = SnapshotUnsafe();
        }
        StateChanged?.Invoke(snapshot);
        return snapshot;
    }

    public RuntimeStateSnapshot EndSession()
    {
        RuntimeStateSnapshot snapshot;
        lock (gate)
        {
            sessionActive = false;
            sessionId = "";
            sessionStartedAt = null;
            lastObservedAt = DateTime.Now;
            runtimeLogPath = null;
            runtimeHealth = "Inactive";
            runtimeWarning = "";
            loadedAliases.Clear();
            runtimeErrors.Clear();
            logCursors.Clear();
            revision++;
            snapshot = SnapshotUnsafe();
        }
        StateChanged?.Invoke(snapshot);
        return snapshot;
    }

    public RuntimeStateSnapshot Observe(Ue4ssRuntimeInfo info)
    {
        RuntimeStateSnapshot snapshot;
        var changed = false;
        lock (gate)
        {
            runtimeHealth = info.HealthState;
            runtimeWarning = info.WarningMessage;
            runtimeLogPath = info.RuntimeLogPath;
            lastObservedAt = DateTime.Now;

            if (sessionActive)
                changed = ReadCurrentSessionEvidenceUnsafe(info);

            if (changed) revision++;
            snapshot = SnapshotUnsafe();
        }

        if (changed) StateChanged?.Invoke(snapshot);
        return snapshot;
    }

    public void ApplyTo(IEnumerable<ModRow> mods)
    {
        HashSet<string> evidence;
        lock (gate)
            evidence = new HashSet<string>(loadedAliases, StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
        {
            if (!IsUe4ss(mod)) continue;
            var aliases = BuildAliases(mod);
            mod.LoadedByUe4ss = aliases.Any(evidence.Contains);
        }
    }

    public IReadOnlyList<string> BuildDiagnosticLines()
    {
        var state = Current;
        return
        [
            $"[RUNTIME STATE] Session: {(state.SessionActive ? state.SessionId : "Inactive")}",
            $"[RUNTIME STATE] Revision: {state.Revision}",
            $"[RUNTIME STATE] Started: {(state.SessionStartedAt?.ToString("s") ?? "N/A")}",
            $"[RUNTIME STATE] Last Observation: {(state.LastObservedAt?.ToString("s") ?? "N/A")}",
            $"[RUNTIME STATE] Runtime Log: {state.RuntimeLogPath ?? "Not reported"}",
            $"[RUNTIME STATE] Loaded Aliases: {state.LoadedCount}",
            $"[RUNTIME STATE] Runtime Errors: {state.ErrorCount}",
            $"[RUNTIME STATE] Health: {state.RuntimeHealth}" + (string.IsNullOrWhiteSpace(state.RuntimeWarning) ? "" : $" — {state.RuntimeWarning}")
        ];
    }

    private bool ReadCurrentSessionEvidenceUnsafe(Ue4ssRuntimeInfo info)
    {
        var changed = false;
        foreach (var path in DiscoverRuntimeLogsUnsafe(info))
            changed |= ReadNewRuntimeEvidenceUnsafe(path);
        return changed;
    }

    private bool ReadNewRuntimeEvidenceUnsafe(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var file = new FileInfo(path);
            var length = file.Length;
            var creationUtc = file.CreationTimeUtc;
            if (!logCursors.TryGetValue(path, out var cursor))
            {
                // A log first appearing after BeginSession belongs to this session only
                // when Windows reports that the file itself was created after the
                // session boundary. Older previously-undiscovered logs are baselined at
                // their current end so historical load lines cannot become false proof.
                var createdThisSession = sessionStartedAt.HasValue &&
                    creationUtc >= sessionStartedAt.Value.ToUniversalTime().AddSeconds(-2);
                var prefixLength = (int)Math.Min(length, 512);
                cursor = new RuntimeLogCursor(createdThisSession ? 0 : length, creationUtc, ReadPrefixFingerprint(path, prefixLength), prefixLength);
                logCursors[path] = cursor;
            }
            else if (cursor.CreationTimeUtc != creationUtc ||
                     (cursor.PrefixLength > 0 && !cursor.PrefixFingerprint.Equals(ReadPrefixFingerprint(path, cursor.PrefixLength), StringComparison.Ordinal)))
            {
                // UE4SS may replace/recreate UE4SS.log during startup. File length alone
                // is not a safe identity check because the replacement can already be
                // larger than the old baseline before MystTiq observes it.
                var prefixLength = (int)Math.Min(length, 512);
                cursor = new RuntimeLogCursor(0, creationUtc, ReadPrefixFingerprint(path, prefixLength), prefixLength);
                logCursors[path] = cursor;
            }

            var offset = cursor.Offset;
            if (length < offset) offset = 0; // truncation/rotation of the same file
            if (length == offset)
            {
                logCursors[path] = cursor with { Offset = offset };
                return false;
            }

            var changed = false;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, leaveOpen: true);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var started = StartedLuaModPattern.Match(line);
                if (started.Success)
                {
                    var name = started.Groups["name"].Value.Trim();
                    foreach (var alias in ExpandAlias(name))
                        changed |= loadedAliases.Add(alias);
                }

                if (RuntimeErrorPattern.IsMatch(line) && runtimeErrors.Count < 100)
                {
                    var compact = line.Trim();
                    if (!runtimeErrors.Contains(compact, StringComparer.OrdinalIgnoreCase))
                    {
                        runtimeErrors.Add(compact);
                        changed = true;
                    }
                }
            }
            logCursors[path] = cursor with { Offset = stream.Position };
            return changed;
        }
        catch
        {
            return false;
        }
    }

    private void CaptureSessionBaselinesUnsafe(Ue4ssRuntimeInfo info)
    {
        foreach (var path in DiscoverRuntimeLogsUnsafe(info))
        {
            try
            {
                var file = new FileInfo(path);
                var prefixLength = (int)Math.Min(file.Length, 512);
                logCursors[path] = new RuntimeLogCursor(file.Length, file.CreationTimeUtc, ReadPrefixFingerprint(path, prefixLength), prefixLength);
            }
            catch { }
        }
    }

    private static IReadOnlyList<string> DiscoverRuntimeLogsUnsafe(Ue4ssRuntimeInfo info)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(info.RuntimeLogPath))
            paths.Add(info.RuntimeLogPath!);

        foreach (var path in new[]
        {
            Path.Combine(info.Win64Root, "UE4SS.log"),
            Path.Combine(info.Ue4ssRoot, "UE4SS.log"),
            Path.Combine(info.Ue4ssRoot, "Logs", "UE4SS.log"),
            Path.Combine(info.Ue4ssRoot, "logs", "UE4SS.log")
        })
            if (File.Exists(path)) paths.Add(path);

        try
        {
            if (Directory.Exists(info.Win64Root))
                foreach (var path in Directory.EnumerateFiles(info.Win64Root, "UE4SS*.log", SearchOption.AllDirectories))
                    paths.Add(path);
        }
        catch { }

        return paths.Where(File.Exists).ToList();
    }

    private static string ReadPrefixFingerprint(string path, int prefixLength)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[Math.Min(prefixLength, (int)Math.Min(stream.Length, prefixLength))];
            if (buffer.Length == 0) return "EMPTY";
            var read = stream.Read(buffer, 0, buffer.Length);
            return Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, read)));
        }
        catch
        {
            return "UNREADABLE";
        }
    }

    private sealed record RuntimeLogCursor(long Offset, DateTime CreationTimeUtc, string PrefixFingerprint, int PrefixLength);

    private RuntimeStateSnapshot SnapshotUnsafe() => new(
        sessionId,
        sessionActive,
        sessionStartedAt,
        revision,
        lastObservedAt,
        runtimeLogPath,
        runtimeHealth,
        runtimeWarning,
        loadedAliases.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
        runtimeErrors.ToList());

    private static bool IsUe4ss(ModRow mod) =>
        mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
        mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildAliases(ModRow mod) =>
        new[] { mod.Package, mod.Name }
            .Concat(mod.RuntimeAliases ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(ExpandAlias)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IEnumerable<string> ExpandAlias(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 3) yield return trimmed;
        var compact = Regex.Replace(trimmed, "[^a-zA-Z0-9]", "");
        if (compact.Length >= 3 && !compact.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            yield return compact;
    }
}
