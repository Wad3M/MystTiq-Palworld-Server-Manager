using MystTiq.Core.Models;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.10.0 "Clone World": duplicates this profile's entire server installation (game binaries +
// Pal/Saved world/config data) into a brand-new, independent server profile the admin can run
// alongside the original -- a full, real copy rather than a shared-junction shortcut, so future
// SteamCMD updates to one instance never silently affect the other. Reuses the existing v0.6.2.0
// fleet-registration path (HeadlessFleetConfigurationService.AddServerAsync) unchanged: like every
// other fleet-membership change, a new profile only takes effect after a MystTiq process restart --
// this service does not attempt to hot-construct a ServerProfileHost, matching that service's own
// documented constraint.
public sealed class HeadlessWorldCloneService
{
    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessServerProfileConfiguration serverProfile;
    private readonly HeadlessFleetConfigurationService fleetConfiguration;
    private readonly PalworldSettingsConfigurationService palworldConfiguration;
    private readonly HeadlessActivityLogService activity;

    public HeadlessWorldCloneService(
        IServerPathProfile paths,
        IServerLifecycleService lifecycle,
        HeadlessServerProfileConfiguration serverProfile,
        HeadlessFleetConfigurationService fleetConfiguration,
        PalworldSettingsConfigurationService palworldConfiguration,
        HeadlessActivityLogService activity)
    {
        this.paths = paths;
        this.lifecycle = lifecycle;
        this.serverProfile = serverProfile;
        this.fleetConfiguration = fleetConfiguration;
        this.palworldConfiguration = palworldConfiguration;
        this.activity = activity;
    }

    public async Task<HeadlessWorldCloneResult> CloneAsync(HeadlessWorldCloneRequest request, CancellationToken cancellationToken)
    {
        var newId = (request.NewProfileId ?? string.Empty).Trim();
        var newName = string.IsNullOrWhiteSpace(request.NewProfileName) ? $"{serverProfile.Name} (Clone)" : request.NewProfileName.Trim();
        if (string.IsNullOrWhiteSpace(newId) || newId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return HeadlessWorldCloneResult.Failure("A valid new profile ID is required.");
        if (newId.Equals(serverProfile.Id, StringComparison.OrdinalIgnoreCase))
            return HeadlessWorldCloneResult.Failure("The new profile ID must be different from the source profile.");

        var status = await lifecycle.GetStatusAsync(cancellationToken);
        if (status.NativeProcessId.HasValue || status.Ready)
            return HeadlessWorldCloneResult.Failure("Stop this server before cloning its world -- files cannot be safely copied while PalServer has them open.");

        var sourceRoot = Path.GetFullPath(paths.ServerRoot);
        if (!Directory.Exists(sourceRoot))
            return HeadlessWorldCloneResult.Failure($"Source server root was not found: {sourceRoot}");

        var parent = Path.GetDirectoryName(sourceRoot) ?? sourceRoot;
        var newServerRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(request.NewServerRoot)
            ? Path.Combine(parent, $"{Path.GetFileName(sourceRoot)}-clone-{newId}")
            : request.NewServerRoot);
        var newBackupRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(request.NewBackupRoot)
            ? Path.Combine(newServerRoot, "Backups") : request.NewBackupRoot);
        var newRuntimeRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(request.NewRuntimeRoot)
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(serverProfile.RuntimeRoot)) ?? newServerRoot, $"clone-{newId}")
            : request.NewRuntimeRoot);

        if (Directory.Exists(newServerRoot) && Directory.EnumerateFileSystemEntries(newServerRoot).Any())
            return HeadlessWorldCloneResult.Failure($"Target directory already exists and is not empty: {newServerRoot}");
        var newRoot = Path.GetFullPath(newServerRoot);
        if (newRoot.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            sourceRoot.StartsWith(newRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return HeadlessWorldCloneResult.Failure("The target directory cannot be nested inside, or contain, the source server root.");

        activity.Record("Information", "Fleet", "World clone started", $"source={sourceRoot}; target={newServerRoot}");

        long copiedBytes = 0;
        int copiedFiles = 0;
        try
        {
            var files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories).ToArray();
            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, async (file, token) =>
            {
                var relative = Path.GetRelativePath(sourceRoot, file);
                var destination = Path.Combine(newServerRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await source.CopyToAsync(target, token);
                Interlocked.Add(ref copiedBytes, source.Length);
                Interlocked.Increment(ref copiedFiles);
            });
        }
        catch (Exception ex)
        {
            activity.Record("Error", "Fleet", "World clone failed", ex.Message);
            TryDeleteDirectory(newServerRoot);
            return HeadlessWorldCloneResult.Failure($"Clone failed after copying {copiedFiles} file(s): {ex.Message}");
        }

        // Assign distinct ports so the clone can run alongside the source without a real,
        // silent port collision -- exactly the misconfiguration v0.6.5.0's Configuration
        // Intelligence port-conflict finding exists to catch, avoided proactively here instead.
        //
        // Real finding from live verification: editing PalWorldSettings.ini's PublicPort alone is
        // NOT sufficient to change PalServer's actual UDP bind port -- a live test with the ini
        // correctly rewritten still bound the OS-default port (falling back to the next free port
        // when the primary game port was already taken by the source instance). The game port is
        // actually controlled by the `-port=` launch argument, so it must be injected explicitly.
        var portOffset = request.PortOffset ?? 100;
        int? newGamePort = null;
        try
        {
            var cloneConfigService = new PalworldSettingsConfigurationService(
                ServerPathProfile.ForCurrentPlatform(new ServerRuntimeConfiguration(newServerRoot, serverProfile.SteamCmdPath, newBackupRoot, newRuntimeRoot)));
            var snapshot = cloneConfigService.Load();
            if (snapshot.Exists)
            {
                var updated = snapshot.Settings.Select(row => row.Name switch
                {
                    "PublicPort" => row with { Value = CaptureOffsetPort(row.Value, portOffset, out newGamePort) },
                    "RESTAPIPort" => row with { Value = OffsetPort(row.Value, portOffset) },
                    "RCONPort" => row with { Value = OffsetPort(row.Value, portOffset) },
                    _ => row
                }).ToArray();
                var saveResult = cloneConfigService.Save(updated);
                if (!saveResult.Success)
                    activity.Record("Warning", "Fleet", "World clone port reassignment failed",
                        $"{string.Join(" ", saveResult.ValidationErrors)} -- the clone was created with the SOURCE's ports and must be repointed manually before running both instances together.");
            }
        }
        catch (Exception ex)
        {
            activity.Record("Warning", "Fleet", "World clone port reassignment failed", ex.Message);
        }

        // The ini edit above keeps PublicPort internally consistent (REST API reports the right
        // port, Configuration Intelligence's port-conflict check sees the right values); the
        // `-port=` launch argument is what actually makes PalServer bind the offset port instead
        // of silently falling back to the next free one.
        var launchArguments = serverProfile.LaunchArguments
            .Where(a => !a.StartsWith("-port=", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (newGamePort is > 0) launchArguments.Add($"-port={newGamePort}");

        var addResult = await fleetConfiguration.AddServerAsync(new HeadlessAddServerProfileRequest(
            newId, newName, newServerRoot, serverProfile.SteamCmdPath, newBackupRoot, newRuntimeRoot,
            launchArguments, serverProfile.Runtime), cancellationToken);

        if (!addResult.Success)
        {
            activity.Record("Error", "Fleet", "World clone registration failed", addResult.Message);
            return HeadlessWorldCloneResult.Failure($"World copied ({copiedFiles} files, {copiedBytes} bytes) but registering the new profile failed: {addResult.Message}");
        }

        activity.Record("Information", "Fleet", "World cloned", $"newProfile={newId}; files={copiedFiles}; bytes={copiedBytes}; portOffset={portOffset}");
        return new HeadlessWorldCloneResult(true, newId, newServerRoot, copiedFiles, copiedBytes,
            $"Cloned {copiedFiles} file(s) into profile '{newId}' with ports offset by +{portOffset}. Restart the MystTiq service to bring the new profile online.");
    }

    private static string OffsetPort(string rawValue, int offset)
    {
        var trimmed = rawValue.Trim().Trim('"');
        return int.TryParse(trimmed, out var port) && port is > 0 and <= 65535
            ? Math.Clamp(port + offset, 1, 65535).ToString()
            : rawValue;
    }

    private static string CaptureOffsetPort(string rawValue, int offset, out int? newPort)
    {
        var result = OffsetPort(rawValue, offset);
        newPort = int.TryParse(result, out var parsed) ? parsed : null;
        return result;
    }

    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
}

public sealed record HeadlessWorldCloneRequest(
    string NewProfileId,
    string? NewProfileName,
    string? NewServerRoot,
    string? NewBackupRoot,
    string? NewRuntimeRoot,
    int? PortOffset);

public sealed record HeadlessWorldCloneResult(bool Success, string? NewProfileId, string? NewServerRoot, int FilesCopied, long BytesCopied, string Message)
{
    public static HeadlessWorldCloneResult Failure(string message) => new(false, null, null, 0, 0, message);
}
