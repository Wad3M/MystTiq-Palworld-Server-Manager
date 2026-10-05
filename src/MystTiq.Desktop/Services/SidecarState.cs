// MystTiq v1.0.0.2: file reviewed for this release (2026-10-05).
using System.Text.Json;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v0.9.9.0: the local helper (headless sidecar) this desktop started: its process id, the loopback endpoint it was given
/// and its executable. Kept in the desktop's local runtime folder so the next call, and the next app start, reuse that
/// helper instead of starting another one beside it.
/// v0.9.10.0: and when it started, so a reused process id is not taken for it.
/// </summary>
public sealed record SidecarState(int ProcessId, string Endpoint, string Executable, DateTimeOffset? StartedUtc = null)
{
    private const string FileName = "desktop-sidecar.json";

    public static SidecarState? Read(string runtimeRoot)
    {
        try
        {
            var path = Path.Combine(runtimeRoot, FileName);
            if (!File.Exists(path)) return null;
            var state = JsonSerializer.Deserialize<SidecarState>(File.ReadAllText(path));
            return state is { ProcessId: > 0 } && Uri.TryCreate(state.Endpoint, UriKind.Absolute, out var uri) && uri.IsLoopback && !string.IsNullOrWhiteSpace(state.Executable)
                ? state
                : null;
        }
        catch { return null; }
    }

    public static void Write(string runtimeRoot, SidecarState state)
    {
        try
        {
            Directory.CreateDirectory(runtimeRoot);
            File.WriteAllText(Path.Combine(runtimeRoot, FileName), JsonSerializer.Serialize(state));
        }
        catch { }
    }

    public static void Delete(string runtimeRoot)
    {
        try { File.Delete(Path.Combine(runtimeRoot, FileName)); } catch { }
    }
}