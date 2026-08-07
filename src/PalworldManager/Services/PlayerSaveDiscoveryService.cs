namespace PalworldManager.Services;

public sealed record PlayerSaveCandidate(string PlayerId, string Path, long SizeBytes, DateTime LastWriteTimeUtc);
public sealed record RejectedPlayerSave(string Path, string Reason);
public sealed record PlayerSaveDiscoveryResult(IReadOnlyList<PlayerSaveCandidate> Accepted, IReadOnlyList<RejectedPlayerSave> Rejected);

public sealed class PlayerSaveDiscoveryService
{
    private static readonly Regex PlayerIdPattern = new("^[A-Fa-f0-9]{32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly SafeFileSystemService fileSystem;

    public PlayerSaveDiscoveryService(SafeFileSystemService? fileSystem = null) =>
        this.fileSystem = fileSystem ?? new SafeFileSystemService();

    public PlayerSaveDiscoveryResult DiscoverFromPlayersDirectory(string playersDirectory, CancellationToken cancellationToken = default)
    {
        var accepted = new List<PlayerSaveCandidate>();
        var rejected = new List<RejectedPlayerSave>();

        foreach (var path in fileSystem.EnumerateFiles(playersDirectory, "*.sav", SearchOption.TopDirectoryOnly, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(path, out var playerId, out var reason))
            {
                rejected.Add(new RejectedPlayerSave(path, reason));
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                accepted.Add(new PlayerSaveCandidate(playerId, path, info.Length, info.LastWriteTimeUtc));
            }
            catch (IOException) { rejected.Add(new RejectedPlayerSave(path, "File changed during inspection.")); }
            catch (UnauthorizedAccessException) { rejected.Add(new RejectedPlayerSave(path, "Access denied.")); }
        }

        return new PlayerSaveDiscoveryResult(
            accepted.GroupBy(x => x.PlayerId, StringComparer.OrdinalIgnoreCase).Select(x => x.OrderByDescending(y => y.LastWriteTimeUtc).First()).ToList(),
            rejected);
    }

    public PlayerSaveDiscoveryResult DiscoverFromWorld(string worldPath, CancellationToken cancellationToken = default) =>
        DiscoverFromPlayersDirectory(Path.Combine(worldPath, "Players"), cancellationToken);

    public bool TryValidate(string path, out string playerId, out string reason)
    {
        playerId = Path.GetFileNameWithoutExtension(path)?.Trim() ?? "";
        if (!path.EndsWith(".sav", StringComparison.OrdinalIgnoreCase)) { reason = "Not a .sav file."; return false; }
        if (playerId.Equals("_dps", StringComparison.OrdinalIgnoreCase)) { reason = "Palworld DPS utility save."; return false; }
        if (SafeFileSystemService.IsTransientPath(path)) { reason = "Temporary or recovery file."; return false; }
        if (!PlayerIdPattern.IsMatch(playerId)) { reason = "Filename is not a 32-character hexadecimal player GUID."; return false; }
        if (!fileSystem.CanReadStableFile(path)) { reason = "File is empty, locked, missing, or unstable."; return false; }
        reason = "";
        return true;
    }

    public static bool IsValidPlayerId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && PlayerIdPattern.IsMatch(value.Trim());
}
