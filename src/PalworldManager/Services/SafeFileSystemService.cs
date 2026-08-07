namespace PalworldManager.Services;

public sealed class SafeFileSystemService
{
    public IReadOnlyList<string> EnumerateFiles(
        string root,
        string pattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return [];

        var results = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsTransientPath(file)) continue;
                    results.Add(file);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            if (searchOption != SearchOption.AllDirectories) continue;

            try
            {
                foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pending.Push(child);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return results;
    }

    public IReadOnlyList<string> EnumerateDirectories(
        string root,
        string pattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return [];

        var results = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            IReadOnlyList<string> children;

            try
            {
                children = Directory
                    .EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
                    .ToList();
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                        pattern,
                        Path.GetFileName(child),
                        ignoreCase: true))
                {
                    results.Add(child);
                }

                if (searchOption == SearchOption.AllDirectories)
                    pending.Push(child);
            }
        }

        return results;
    }

    public bool CanReadStableFile(string path, long minimumLength = 1)
    {
        try
        {
            if (IsTransientPath(path)) return false;

            var info = new FileInfo(path);
            if (!info.Exists || info.Length < minimumLength) return false;

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            return stream.Length >= minimumLength;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static bool IsTransientPath(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(name)
            || name.Contains("~RF", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".temp", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".myst-new", StringComparison.OrdinalIgnoreCase);
    }
}
