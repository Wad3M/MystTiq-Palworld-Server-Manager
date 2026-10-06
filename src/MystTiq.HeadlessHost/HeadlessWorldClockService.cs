// MystTiq v1.0.3.0: file reviewed for this release (2026-10-05).
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.0.4 (reported 2026-10-05: backup and restore "doesnt seem to work correctly. verify using the day of the game server
/// after a restoration"). The world's in-game day is GameTimeSaveData.GameDateTimeTicks inside Level.sav, which is
/// compressed (PlM/Oodle on current servers), so MystTiq reads it from a decoded Level.sav.json beside it. Nothing
/// refreshed that copy after normal play, only after a MystTiq world edit: on the owner's server it said Day 210 (decoded
/// 2026-10-01) while Level.sav was Day 248 (2026-10-04), and every backup since carried the same old copy, so the day
/// shown never changed after a restore either. Now the copy is re-decoded whenever Level.sav is newer (from a copy of
/// Level.sav, at most every two minutes per world), a restore reports the restored world's day, and each backup's day is
/// read from its own Level.sav.
/// </summary>
public sealed class HeadlessWorldClockService
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(2);
    private readonly HeadlessSaveCodecService codec;
    private readonly string workRoot;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string cacheRoot;
    private readonly Dictionary<string, DateTime> lastAttempt = new(StringComparer.OrdinalIgnoreCase);

    public HeadlessWorldClockService(IServerPathProfile paths, HeadlessSaveCodecService codec)
    {
        this.codec = codec;
        workRoot = Path.Combine(paths.ManagerRuntimeRoot, "world-clock");
        cacheRoot = CacheRootFor(paths);
    }

    /// <summary>
    /// Where MystTiq keeps its own decoded copy when the one beside Level.sav cannot be written (on the owner's server it
    /// belonged to administrators): runtime\world-clock\decoded\&lt;world id&gt;.Level.sav.json.
    /// </summary>
    public static string CacheRootFor(IServerPathProfile paths) => Path.Combine(paths.ManagerRuntimeRoot, "world-clock", "decoded");

    /// <summary>The last refresh error, for the Dashboard to say why the day may be old.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// The world's day from the newest decoded copy (beside Level.sav, or MystTiq's own in cacheRoot), and whether that copy
    /// is as new as Level.sav.
    /// </summary>
    public static WorldClockReading Read(string worldPath, string? cacheRoot = null)
    {
        var json = new[] { "Level.sav.json", "Level.json", "Level.sav.decoded.json" }.Select(n => Path.Combine(worldPath, n))
            .Append(cacheRoot is null ? string.Empty : Path.Combine(cacheRoot, Path.GetFileName(worldPath.TrimEnd(Path.DirectorySeparatorChar)) + ".Level.sav.json"))
            .Where(p => p.Length > 0 && File.Exists(p)).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (json is null) return WorldClockReading.None;
        var ticks = WorldClock.ReadTicks(json);
        if (ticks is null) return WorldClockReading.None;
        var jsonUtc = File.GetLastWriteTimeUtc(json);
        var level = Path.Combine(worldPath, "Level.sav");
        var levelUtc = File.Exists(level) ? File.GetLastWriteTimeUtc(level) : jsonUtc;
        return new WorldClockReading(ticks, jsonUtc, WorldClock.IsCurrent(jsonUtc, levelUtc));
    }

    /// <summary>Starts a refresh when the decoded copy is older than Level.sav (at most every two minutes per world).</summary>
    public void RefreshInBackgroundIfStale(string worldPath)
    {
        if (!File.Exists(Path.Combine(worldPath, "Level.sav")) || Read(worldPath, cacheRoot).Current) return;
        lock (lastAttempt)
        {
            if (lastAttempt.TryGetValue(worldPath, out var at) && DateTime.UtcNow - at < RefreshEvery) return;
            lastAttempt[worldPath] = DateTime.UtcNow;
        }
        _ = Task.Run(async () => { try { await RefreshAsync(worldPath, CancellationToken.None); } catch { } });
    }

    /// <summary>Decodes a copy of the world's Level.sav, writes Level.sav.json beside it, and returns its clock.</summary>
    public async Task<WorldClockResult> RefreshAsync(string worldPath, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        var work = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));
        try
        {
            var level = Path.Combine(worldPath, "Level.sav");
            if (!File.Exists(level)) return Fail("Level.sav was not found.");
            Directory.CreateDirectory(work);
            var copy = Path.Combine(work, "Level.sav");
            File.Copy(level, copy, true);
            var result = await DecodeClockAsync(copy, work, cancellationToken);
            if (result.Success && result.DecodedJsonPath is not null)
            {
                var sidecar = Path.Combine(worldPath, "Level.sav.json");
                if (SaveFolderAccess.CanWrite(sidecar)) HeadlessSaveCodecService.RefreshExplorerSidecar(level, result.DecodedJsonPath);
                // Beside Level.sav when allowed (the map and guild pages read it there); MystTiq's own copy otherwise, or when
                // the copy beside did not take, so the day is current either way.
                if (!File.Exists(sidecar) || !WorldClock.IsCurrent(File.GetLastWriteTimeUtc(sidecar), File.GetLastWriteTimeUtc(level)))
                {
                    Directory.CreateDirectory(cacheRoot);
                    File.Copy(result.DecodedJsonPath, Path.Combine(cacheRoot, Path.GetFileName(worldPath.TrimEnd(Path.DirectorySeparatorChar)) + ".Level.sav.json"), true);
                }
            }
            LastError = result.Success ? null : result.Error;
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            return Fail(ex.Message);
        }
        finally
        {
            TryDelete(work);
            gate.Release();
        }
    }

    /// <summary>The day inside a backup: its newest world Level.sav (not Palworld's own backup\ copies), decoded.</summary>
    public async Task<WorldClockResult> ReadBackupAsync(string zipPath, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        var work = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(work);
            var copy = Path.Combine(work, "Level.sav");
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var name = WorldClock.NewestWorldLevel(zip.Entries.Select(e => (e.FullName, e.LastWriteTime)));
                if (name is null) return Fail("The backup has no world Level.sav.");
                zip.GetEntry(name)!.ExtractToFile(copy, true);
            }
            return await DecodeClockAsync(copy, work, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Fail(ex.Message); }
        finally
        {
            TryDelete(work);
            gate.Release();
        }
    }

    private async Task<WorldClockResult> DecodeClockAsync(string levelCopy, string work, CancellationToken cancellationToken)
    {
        var (match, detail) = await codec.ResolveConverterAsync(levelCopy, cancellationToken);
        if (match is null) return Fail(detail);
        var json = await codec.DecodeAsync(match, levelCopy, Path.Combine(work, "Level.sav.json"), cancellationToken);
        var ticks = WorldClock.ReadTicks(json);
        return ticks is null ? Fail("The decoded save has no world clock (GameDateTimeTicks).") : new WorldClockResult(true, ticks, null, json);
    }

    private static WorldClockResult Fail(string error) => new(false, null, error, null);

    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
}

/// <summary>The world-clock rules, pure so the logic harness covers them.</summary>
public static class WorldClock
{
    public const long TicksPerDay = 864_000_000_000L;
    private static readonly byte[] Needle = Encoding.UTF8.GetBytes("\"GameDateTimeTicks\"");
    private static readonly Regex WrappedValue = new("\"value\"\\s*:\\s*\"?(?<ticks>-?\\d+)\"?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DirectValue = new("^\\s*:\\s*\"?(?<ticks>-?\\d+)\"?", RegexOptions.Compiled);

    public static (long Day, string Time) Split(long ticks)
    {
        var time = TimeSpan.FromTicks(ticks % TicksPerDay);
        return (ticks / TicksPerDay, $"{(int)time.TotalHours:00}:{time.Minutes:00}");
    }

    public static string Describe(long ticks)
    {
        var (day, time) = Split(ticks);
        return $"Day {day} {time}";
    }

    /// <summary>The decoded copy counts as current when it is no older than Level.sav (two seconds of clock slack).</summary>
    public static bool IsCurrent(DateTime decodedUtc, DateTime levelUtc) => decodedUtc >= levelUtc.AddSeconds(-2);

    /// <summary>A backup ZIP's world Level.sav: the newest one under SaveGames\&lt;id&gt;\&lt;world&gt;\, never Palworld's backup\ copies.</summary>
    public static string? NewestWorldLevel(IEnumerable<(string Name, DateTimeOffset Time)> entries) =>
        entries.Where(e =>
            {
                var parts = e.Name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                return parts.Length >= 2 && parts[^1].Equals("Level.sav", StringComparison.OrdinalIgnoreCase) &&
                       !parts.Any(p => p.Equals("backup", StringComparison.OrdinalIgnoreCase));
            })
            .OrderByDescending(e => e.Time).Select(e => e.Name).FirstOrDefault();

    /// <summary>GameDateTimeTicks from a decoded Level.sav JSON, read as a stream (the file is several MB).</summary>
    public static long? ReadTicks(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[64 * 1024];
            var matched = 0;
            while (true)
            {
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) return null;
                for (var i = 0; i < read; i++)
                {
                    var value = buffer[i];
                    if (value == Needle[matched])
                    {
                        matched++;
                        if (matched != Needle.Length) continue;
                        var tail = new byte[4096];
                        var copied = Math.Min(tail.Length, read - (i + 1));
                        if (copied > 0) Buffer.BlockCopy(buffer, i + 1, tail, 0, copied);
                        if (copied < tail.Length) copied += stream.Read(tail, copied, tail.Length - copied);
                        var text = Encoding.UTF8.GetString(tail, 0, copied);
                        var wrapped = WrappedValue.Match(text);
                        if (wrapped.Success && long.TryParse(wrapped.Groups["ticks"].Value, out var wrappedTicks)) return wrappedTicks;
                        var direct = DirectValue.Match(text);
                        if (direct.Success && long.TryParse(direct.Groups["ticks"].Value, out var directTicks)) return directTicks;
                        return null;
                    }
                    matched = value == Needle[0] ? 1 : 0;
                }
            }
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}

public sealed record WorldClockReading(long? Ticks, DateTime? DecodedUtc, bool Current)
{
    public static WorldClockReading None { get; } = new(null, null, false);
}

public sealed record WorldClockResult(bool Success, long? Ticks, string? Error, string? DecodedJsonPath)
{
    public string? Text => Ticks is { } t ? WorldClock.Describe(t) : null;
}
