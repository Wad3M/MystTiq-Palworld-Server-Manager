// MystTiq v1.0.8.0: file reviewed for this release (2026-10-11).
using System.Globalization;
using System.Text.Json;

namespace MystTiq.HeadlessHost;

/// <summary>What happened when a saved list was read.</summary>
public enum SavedFileState
{
    /// <summary>The file could not be read, but its last good copy could, and was loaded.</summary>
    LoadedFromCopy,
    /// <summary>The file could not be read and there was no good copy: changes are refused until it is repaired.</summary>
    Unreadable,
    /// <summary>The file could not be read and the list is one MystTiq can rebuild (a log or a cache), so it started empty.</summary>
    StartedEmpty,
    /// <summary>The owner repaired an unreadable list by starting it empty.</summary>
    Repaired
}

public sealed record SavedFileHealth(
    string Name,
    string Path,
    SavedFileState State,
    bool ChangesRefused,
    string Detail,
    string? KeptCopy,
    DateTimeOffset ObservedUtc);

public sealed record SavedFileRepairResult(bool Success, string Message);

/// <summary>A change to a saved list that MystTiq could not read and has no good copy of.</summary>
public sealed class SavedFileRefusedException(string message, string path) : InvalidOperationException(message)
{
    public string SavedFilePath { get; } = path;
}

/// <summary>
/// v1.0.7.0 (roadmap Q-1, owner decision D-10): MystTiq's saved lists (accounts, automation rules, kits and the rest)
/// used to load as empty when their file could not be read, and the next change then wrote that empty list over the
/// file. Reproduced 2026-10-08: with users.json cut short, three accounts became none and creating one more left only
/// that one on disk. Now:
/// <list type="bullet">
/// <item>every save keeps the file it replaces as <c>.last-good</c>;</item>
/// <item>a file that cannot be read is copied aside (<c>.unreadable-{time}</c>), never overwritten unseen;</item>
/// <item>the last good copy is loaded when there is one;</item>
/// <item>otherwise a list that holds the owner's own data refuses changes until it is repaired (Server Doctor),
/// and the owner is alerted with a link there; a log or cache MystTiq rebuilds itself just starts empty.</item>
/// </list>
/// The state is kept per file, process-wide: users and roles belong to the fleet, the rest to each server.
/// </summary>
public static class SavedFiles
{
    public const string GoodCopySuffix = ".last-good";
    public const string UnreadableInfix = ".unreadable-";
    public const string RepairActionKind = "repair-saved-file";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly object Gate = new();
    private static readonly Dictionary<string, SavedFileHealth> Health = new(StringComparer.OrdinalIgnoreCase);
    // Files whose content on disk is known to be unreadable: a save must not keep such a file as the good copy.
    private static readonly HashSet<string> BadOnDisk = new(StringComparer.OrdinalIgnoreCase);
    // The bad file already copied aside (its size and time), so a list read on every call keeps one copy, not hundreds.
    private static readonly Dictionary<string, (long Length, DateTime WrittenUtc, string Copy)> Kept = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised when a file's health changes (for the alert and the activity log).</summary>
    public static event Action<SavedFileHealth>? Changed;

    /// <summary>
    /// Reads a saved file. <paramref name="ownerData"/>: true for a list the owner made (refused when unreadable with no good
    /// copy), false for a log or cache MystTiq rebuilds (started empty).
    /// </summary>
    /// <paramref name="useGoodCopy"/>: false for a state that would mislead if an older copy came back (a delivery pause).
    public static T Load<T>(string path, string name, Func<T> empty, bool ownerData = true, JsonSerializerOptions? options = null, bool useGoodCopy = true)
    {
        var full = System.IO.Path.GetFullPath(path);
        if (!File.Exists(full))
        {
            // No file: a first start, or a list never saved. A good copy without its file means the file was removed by
            // hand or lost; that copy is the newest the owner has, so it is used.
            if (useGoodCopy && File.Exists(full + GoodCopySuffix) && TryRead<T>(full + GoodCopySuffix, options, out var orphan, out _))
            {
                Record(new(name, full, SavedFileState.LoadedFromCopy, false,
                    $"The saved {name} was missing, so MystTiq loaded its last good copy (saved {WrittenText(full + GoodCopySuffix)}).", null, DateTimeOffset.UtcNow));
                return orphan;
            }
            Clear(full);
            return empty();
        }

        if (TryRead<T>(full, options, out var value, out var error))
        {
            Clear(full);
            return value;
        }

        string? kept;
        lock (Gate)
        {
            BadOnDisk.Add(full);
            kept = KeepCopy(full);
            // A list already refused and not repaired stays refused (and is not alerted again) while the same bad file is there.
            if (Health.TryGetValue(full, out var known) && known.State is SavedFileState.Unreadable && known.KeptCopy == kept)
                return empty();
        }

        var keptText = kept is null ? "MystTiq could not keep a copy of it" : $"the unreadable file is kept as {System.IO.Path.GetFileName(kept)}";
        if (useGoodCopy && TryRead<T>(full + GoodCopySuffix, options, out var copy, out _))
        {
            Record(new(name, full, SavedFileState.LoadedFromCopy, false,
                $"The saved {name} could not be read ({error}). MystTiq loaded its last good copy (saved {WrittenText(full + GoodCopySuffix)}); {keptText}. Changes made since that copy are not in it.",
                kept, DateTimeOffset.UtcNow));
            return copy;
        }

        if (!ownerData)
        {
            Record(new(name, full, SavedFileState.StartedEmpty, false,
                $"The saved {name} could not be read ({error}) and had no good copy. MystTiq rebuilds it, so it started empty; {keptText}.",
                kept, DateTimeOffset.UtcNow));
            return empty();
        }

        Record(new(name, full, SavedFileState.Unreadable, true,
            $"The saved {name} could not be read ({error}) and there is no good copy. Changes to it are refused so it is not overwritten; {keptText}.",
            kept, DateTimeOffset.UtcNow));
        return empty();
    }

    /// <summary>Saves a file atomically, keeping the file it replaces as the good copy. Refused for an unrepaired unreadable list.</summary>
    public static void Save<T>(string path, T value, JsonSerializerOptions? options = null) =>
        SaveText(path, JsonSerializer.Serialize(value, options ?? Indented));

    public static void SaveText(string path, string json)
    {
        var full = System.IO.Path.GetFullPath(path);
        EnsureWritable(full);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        var partial = full + ".partial";
        File.WriteAllText(partial, json);
        bool bad;
        lock (Gate) bad = BadOnDisk.Contains(full);
        if (File.Exists(full) && !bad)
            FileRetry.Replace(partial, full, full + GoodCopySuffix);
        else
            FileRetry.Move(partial, full);
        lock (Gate) BadOnDisk.Remove(full);
    }

    /// <summary>Throws <see cref="SavedFileRefusedException"/> when changes to this file are refused. Call before changing a list held in memory.</summary>
    public static void EnsureWritable(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        SavedFileHealth? health;
        lock (Gate) Health.TryGetValue(full, out health);
        if (health is { ChangesRefused: true })
            throw new SavedFileRefusedException(RefusedMessage(health), full);
    }

    public static bool IsRefused(string path) => Refusal(path) is not null;

    /// <summary>The refusal message when changes to this file are refused, else null.</summary>
    public static string? Refusal(string path)
    {
        lock (Gate) return Health.TryGetValue(System.IO.Path.GetFullPath(path), out var h) && h.ChangesRefused ? RefusedMessage(h) : null;
    }

    public static string RefusedMessage(SavedFileHealth health) =>
        $"MystTiq could not read the saved {health.Name} and has no good copy, so changes to it are refused (the file is not overwritten). Repair it in Server Doctor.";

    /// <summary>Every saved file with something to report, optionally only those under one of the given folders.</summary>
    public static IReadOnlyList<SavedFileHealth> Snapshot(params string[] roots)
    {
        lock (Gate)
        {
            var all = Health.Values.ToList();
            if (roots.Length == 0) return all;
            var prefixes = roots.Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(r)) + System.IO.Path.DirectorySeparatorChar).ToList();
            return all.Where(h => prefixes.Any(p => h.Path.StartsWith(p, StringComparison.OrdinalIgnoreCase))).ToList();
        }
    }

    /// <summary>The diagnostics finding id for a saved file.</summary>
    public static string FindingId(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path)) ?? string.Empty;
        return "saved-file-" + new string($"{folder}-{name}".ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
    }

    /// <summary>
    /// The repair (D-10): when the file still cannot be read, the list starts empty (the unreadable file stays kept beside
    /// it) and changes are allowed again. When it reads correctly now (a good copy was put back by hand), nothing is
    /// changed and the owner is asked to restart the MystTiq service so it loads that file.
    /// </summary>
    public static SavedFileRepairResult Repair(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        SavedFileHealth? health;
        lock (Gate) Health.TryGetValue(full, out health);
        if (health is not { ChangesRefused: true })
            return new(true, "This saved list is not refused; there is nothing to repair.");

        if (File.Exists(full) && TryRead<JsonElement>(full, null, out var element, out _) && element.ValueKind is not JsonValueKind.Null)
            return new(false, $"The saved {health.Name} reads correctly now. Restart the MystTiq service so it loads that file; changes stay refused until then, so the file is not overwritten.");

        lock (Gate)
        {
            var kept = File.Exists(full) ? KeepCopy(full) : health.KeptCopy;
            if (File.Exists(full))
            {
                if (kept is null || !File.Exists(kept))
                    return new(false, $"MystTiq could not keep a copy of the unreadable {health.Name}, so it is left as it is. Copy {full} somewhere safe, delete it, and restart the MystTiq service.");
                File.Delete(full);
            }
            BadOnDisk.Remove(full);
            var repaired = new SavedFileHealth(health.Name, full, SavedFileState.Repaired, false,
                $"Repaired: the {health.Name} started empty. The unreadable file is kept as {System.IO.Path.GetFileName(kept)}.", kept, DateTimeOffset.UtcNow);
            Health[full] = repaired;
            Changed?.Invoke(repaired);
            return new(true, $"The {health.Name} started empty and can be changed again. The unreadable file is kept as {kept}.");
        }
    }

    /// <summary>For tests only.</summary>
    public static void ResetForTests()
    {
        lock (Gate) { Health.Clear(); BadOnDisk.Clear(); Kept.Clear(); }
    }

    private static bool TryRead<T>(string path, JsonSerializerOptions? options, out T value, out string error)
    {
        value = default!;
        error = string.Empty;
        try
        {
            var text = File.ReadAllText(path);
            var read = JsonSerializer.Deserialize<T>(text, options);
            if (read is null)
            {
                error = "the file holds no data";
                return false;
            }
            value = read;
            return true;
        }
        catch (JsonException ex) { error = $"not valid JSON at line {ex.LineNumber + 1}"; }
        catch (NotSupportedException ex) { error = ex.Message; }
        catch (IOException ex) { error = ex.Message; }
        catch (UnauthorizedAccessException) { error = "access to the file is denied"; }
        return false;
    }

    // Caller holds Gate. Copies the bad file aside once per distinct content.
    private static string? KeepCopy(string full)
    {
        try
        {
            var info = new FileInfo(full);
            if (Kept.TryGetValue(full, out var known) && known.Length == info.Length && known.WrittenUtc == info.LastWriteTimeUtc && File.Exists(known.Copy))
                return known.Copy;
            var copy = full + UnreadableInfix + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            for (var n = 2; File.Exists(copy); n++) copy = full + UnreadableInfix + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + n;
            File.Copy(full, copy);
            Kept[full] = (info.Length, info.LastWriteTimeUtc, copy);
            return copy;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private static string WrittenText(string path)
    {
        try { return File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return "at an unknown time"; }
    }

    private static void Record(SavedFileHealth health)
    {
        lock (Gate)
        {
            if (Health.TryGetValue(health.Path, out var known) && known.State == health.State && known.KeptCopy == health.KeptCopy)
                return;
            Health[health.Path] = health;
        }
        Changed?.Invoke(health);
    }

    // A file read correctly: whoever read it now holds its real content, so whatever was reported for it is closed (a good
    // file was put back by hand, or the list was saved since). A list read once at start-up never gets here until the next
    // start, so its empty copy in memory can never be saved over a file put back meanwhile.
    private static void Clear(string full)
    {
        lock (Gate)
        {
            BadOnDisk.Remove(full);
            Health.Remove(full);
        }
    }
}
