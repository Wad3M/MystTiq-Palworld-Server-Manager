// MystTiq v1.1.6.0: file reviewed for this release (2026-10-26).
namespace MystTiq.Core.Services;

/// <summary>
/// v1.1.5.0 (roadmap W-2, owner 2026-10-22: "a portable version for Windows"). A <c>portable.txt</c> file next to
/// MystTiq.Desktop.exe keeps everything MystTiq writes in a <c>data</c> folder beside it, instead of AppData and
/// ProgramData: desktop settings in <c>data\config</c>, the desktop's and helper's local state in <c>data\local</c>, and the
/// service configuration, fleet, secrets and certificates in <c>data\service</c>. The helper (in <c>headless\</c>) finds the
/// same marker one folder up. Moving the folder moves everything with it. A portable copy writes nothing to the registry:
/// starting with Windows, taking nxm:// links and installing the Windows service are refused, with the reason.
/// </summary>
public static class PortableMode
{
    public const string MarkerFileName = "portable.txt";
    public const string DataFolderName = "data";

    private static readonly Lazy<string?> Detected = new(() => Detect(AppContext.BaseDirectory));
    private static readonly AsyncLocal<TestOverride?> ForTests = new();

    /// <summary>The data folder, or null when this copy is not portable.</summary>
    public static string? DataRoot => ForTests.Value is { } test ? test.DataRoot : Detected.Value;

    public static bool IsActive => DataRoot is not null;

    /// <summary>The data folder when <paramref name="baseDirectory"/> (or, for the helper, its parent) holds the marker.</summary>
    public static string? Detect(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory)) return null;
        var directory = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (File.Exists(Path.Combine(directory, MarkerFileName))) return Path.Combine(directory, DataFolderName);
        // The helper runs from the desktop's headless\ folder.
        if (string.Equals(Path.GetFileName(directory), "headless", StringComparison.OrdinalIgnoreCase) &&
            Path.GetDirectoryName(directory) is { } parent && File.Exists(Path.Combine(parent, MarkerFileName)))
            return Path.Combine(parent, DataFolderName);
        return null;
    }

    /// <summary>Roaming settings: %APPDATA% normally.</summary>
    public static string RoamingAppData => DataRoot is { } root ? Path.Combine(root, "config") : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>Local state: %LOCALAPPDATA% normally.</summary>
    public static string LocalAppData => DataRoot is { } root ? Path.Combine(root, "local") : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>The service's machine-wide data: %ProgramData% normally.</summary>
    public static string CommonAppData => DataRoot is { } root ? Path.Combine(root, "service") : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    /// <summary>The settings root a desktop store uses: the portable config folder, else what the store would use.</summary>
    public static string ConfigRootOr(Func<string> normal) => DataRoot is { } root ? Path.Combine(root, "config") : normal();

    /// <summary>The refusal shown where a portable copy would write to the registry or install something.</summary>
    public const string RegistryRefusal = "This is a portable copy: it writes nothing to Windows (no registry entries, links or services), so this option is off.";

    /// <summary>Makes this call (and work it starts) see the given data folder; tests only.</summary>
    public static IDisposable UseForTests(string? dataRoot)
    {
        var previous = ForTests.Value;
        ForTests.Value = new TestOverride(dataRoot);
        return new Scope(previous);
    }

    private sealed record TestOverride(string? DataRoot);

    private sealed class Scope(TestOverride? previous) : IDisposable
    {
        public void Dispose() => ForTests.Value = previous;
    }
}
