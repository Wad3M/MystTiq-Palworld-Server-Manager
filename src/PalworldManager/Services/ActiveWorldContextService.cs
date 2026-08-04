using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class ActiveWorldContextService : IDisposable
{
    private readonly AppSettings settings;
    private readonly object gate = new();
    private ActiveWorldContext? cached;
    private long generation;
    private FileSystemWatcher? watcher;

    public ActiveWorldContextService(AppSettings settings)
    {
        this.settings = settings;
        ConfigureWatcher();
    }

    public event EventHandler<ActiveWorldContext>? Changed;

    public ActiveWorldContext Current(bool forceRefresh = false)
    {
        lock (gate)
        {
            var resolved = Resolve();
            if (!forceRefresh && cached is not null && SameIdentity(cached, resolved)) return cached;
            cached = resolved with { Generation = ++generation };
            return cached;
        }
    }

    public void Invalidate(string reason = "Manual invalidation")
    {
        ActiveWorldContext context;
        lock (gate)
        {
            cached = null;
            context = Current(forceRefresh: true) with { ResolutionSource = reason };
            cached = context;
        }
        Changed?.Invoke(this, context);
    }

    private ActiveWorldContext Resolve()
    {
        var root = settings.SaveRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(settings.ServerRoot ?? "", "Pal", "Saved", "SaveGames");
        var zero = Path.Combine(root, "0");
        if (!Directory.Exists(zero)) return Empty("Configured SaveRoot");

        var candidate = Directory.EnumerateDirectories(zero)
            .Select(path => new { Path = path, Level = Path.Combine(path, "Level.sav") })
            .Where(x => File.Exists(x.Level))
            .Select(x => new FileInfo(x.Level))
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .ThenByDescending(x => x.Length)
            .FirstOrDefault();
        if (candidate is null) return Empty("Configured SaveRoot");

        var worldPath = candidate.DirectoryName ?? "";
        return new ActiveWorldContext(
            worldPath,
            Path.GetFileName(worldPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            candidate.FullName,
            candidate.LastWriteTimeUtc,
            candidate.Length,
            "Newest active Level.sav",
            generation);
    }

    private ActiveWorldContext Empty(string source) => new("", "", "", DateTime.MinValue, 0, source, generation);

    private static bool SameIdentity(ActiveWorldContext left, ActiveWorldContext right) =>
        left.WorldPath.Equals(right.WorldPath, StringComparison.OrdinalIgnoreCase) &&
        left.LevelLastWriteUtc == right.LevelLastWriteUtc && left.LevelLength == right.LevelLength;

    private void ConfigureWatcher()
    {
        try
        {
            var root = string.IsNullOrWhiteSpace(settings.SaveRoot)
                ? Path.Combine(settings.ServerRoot ?? "", "Pal", "Saved", "SaveGames")
                : settings.SaveRoot;
            if (!Directory.Exists(root)) return;
            watcher = new FileSystemWatcher(root, "*.sav")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            FileSystemEventHandler changed = (_, e) => { if (e.FullPath.EndsWith("Level.sav", StringComparison.OrdinalIgnoreCase)) Invalidate("Level.sav changed"); };
            RenamedEventHandler renamed = (_, e) => { if (e.FullPath.EndsWith("Level.sav", StringComparison.OrdinalIgnoreCase) || e.OldFullPath.EndsWith("Level.sav", StringComparison.OrdinalIgnoreCase)) Invalidate("Level.sav renamed"); };
            watcher.Changed += changed;
            watcher.Created += changed;
            watcher.Deleted += changed;
            watcher.Renamed += renamed;
        }
        catch { watcher?.Dispose(); watcher = null; }
    }

    public void Dispose() => watcher?.Dispose();
}
