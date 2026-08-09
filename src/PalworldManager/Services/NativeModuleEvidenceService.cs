using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Read-only native UE4SS module evidence provider.
/// Matches canonical DLL paths from a MOD's active-root folder against modules
/// mapped into the current MystTiq-owned PalServer process session.
/// </summary>
public sealed class NativeModuleEvidenceService
{
    private readonly AppSettings settings;
    private readonly ServerService server;
    private readonly Ue4ssRuntimeResolver runtimeResolver;

    public NativeModuleEvidenceService(AppSettings settings, ServerService server)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.server = server ?? throw new ArgumentNullException(nameof(server));
        runtimeResolver = new Ue4ssRuntimeResolver(settings);
    }

    public NativeModuleEvidence Inspect(ModRow mod)
    {
        if (!IsNativeCapable(mod))
            return NativeModuleEvidence.NotApplicable("MOD is not classified as native/hybrid UE4SS.");

        var snapshot = server.RefreshActiveSessionSnapshot() ?? server.GetActiveSessionSnapshot();
        if (snapshot is null || snapshot.RootProcessId <= 0)
            return NativeModuleEvidence.Unavailable("No active PalServer session snapshot is available yet.");

        var modFolder = FindModFolder(mod);
        if (modFolder is null)
            return NativeModuleEvidence.Unavailable("The MOD's active UE4SS folder could not be resolved.");

        var dlls = SafeDlls(modFolder)
            .Select(Canonical)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (dlls.Count == 0)
            return NativeModuleEvidence.Unavailable("No native DLL payload was found under the resolved active MOD folder.");

        var modules = snapshot.LoadedModules
            .Select(Canonical)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matched = dlls.FirstOrDefault(modules.Contains);
        if (!string.IsNullOrWhiteSpace(matched))
            return new NativeModuleEvidence(
                NativeModuleEvidenceState.Mapped,
                snapshot.SessionId,
                snapshot.RootProcessId,
                matched,
                snapshot.CapturedAt,
                $"Native module is mapped into PalServer PID {snapshot.RootProcessId} for server session #{snapshot.SessionId}.");

        // Empty module capture can mean access/inspection was unavailable. Fail open
        // to Unavailable rather than claiming a native MOD failed to load.
        if (snapshot.LoadedModules.Count == 0)
            return new NativeModuleEvidence(
                NativeModuleEvidenceState.Unavailable,
                snapshot.SessionId,
                snapshot.RootProcessId,
                "",
                snapshot.CapturedAt,
                "PalServer is running, but Windows module enumeration returned no readable module paths.");

        return new NativeModuleEvidence(
            NativeModuleEvidenceState.NotObserved,
            snapshot.SessionId,
            snapshot.RootProcessId,
            "",
            snapshot.CapturedAt,
            $"No exact canonical DLL path for this MOD was observed in the captured module table for PalServer PID {snapshot.RootProcessId}.");
    }

    private string? FindModFolder(ModRow mod)
    {
        var root = runtimeResolver.GetActiveModsRoot();
        if (!Directory.Exists(root)) return null;
        var aliases = ModRuntimeEvidenceEngine.BuildAliases(mod);
        try
        {
            return Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(folder =>
                {
                    var name = Path.GetFileName(folder);
                    return aliases.Any(alias => Normalize(alias).Equals(Normalize(name), StringComparison.OrdinalIgnoreCase));
                });
        }
        catch { return null; }
    }

    private static IEnumerable<string> SafeDlls(string folder)
    {
        try { return Directory.EnumerateFiles(folder, "*.dll", SearchOption.AllDirectories); }
        catch { return []; }
    }

    private static string Canonical(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return path.Trim(); }
    }

    private static bool IsNativeCapable(ModRow mod) =>
        mod.Type.Contains("Native", StringComparison.OrdinalIgnoreCase) ||
        mod.Type.Contains("Hybrid", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) => Regex.Replace(value ?? "", "[^a-zA-Z0-9]", "");
}

public enum NativeModuleEvidenceState { NotApplicable, Mapped, NotObserved, Unavailable }

public sealed record NativeModuleEvidence(
    NativeModuleEvidenceState State,
    long ServerSessionId,
    int ProcessId,
    string MatchedPath,
    DateTime? CapturedAt,
    string Detail)
{
    public bool ConfirmedMapped => State == NativeModuleEvidenceState.Mapped;
    public static NativeModuleEvidence NotApplicable(string detail) => new(NativeModuleEvidenceState.NotApplicable, 0, 0, "", null, detail);
    public static NativeModuleEvidence Unavailable(string detail) => new(NativeModuleEvidenceState.Unavailable, 0, 0, "", null, detail);
}
