namespace MystTiq.Desktop.Models;

// v0.6.2.0 Multi-Server Fleet: mirrors the anonymous JSON shapes LocalManagementApiHost's
// GET /api/v1/servers and POST /api/v1/fleet/{backup,doctor,update}-all routes return.
public sealed class ServerLifecycleStatusDto
{
    public bool Ready { get; set; }
    public int? NativeProcessId { get; set; }
    public bool CrashDetected { get; set; }
}

public sealed class ServerProfileSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Runtime { get; set; } = string.Empty;
    public ServerLifecycleStatusDto Status { get; set; } = new();
    public string StatusText => Status.CrashDetected ? "Crash detected" : Status.Ready ? "Running" : "Stopped / Not ready";
    // Fleet's Server Profiles list previously showed only this text with no color cue -- every
    // other status surface in the app (tab bar, dashboard glow cards) uses a colored dot/glow, so
    // the fleet list was the odd one out. A semantic KEY, not a hex literal -- resolved to the live
    // theme brush by SemanticStatusColorConverter (v0.7.63.0 theme-system bugfix; this DTO is a
    // plain data model re-fetched on every fleet poll, so it needs no explicit theme-change refresh
    // the way a long-lived ViewModel property does -- the converter re-resolves on every fetch).
    public string StatusDotColorKey => Status.CrashDetected ? "Red" : Status.Ready ? "Green" : "Muted";
}

public sealed class FleetActionResultDto
{
    public string ServerProfileId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

// v0.6.10.0 Clone World: duplicates the selected profile's entire installation (binaries + world)
// into a brand-new profile, with ports offset to avoid colliding with the source when both run.
public sealed class WorldCloneRequestDto
{
    public string NewProfileId { get; set; } = string.Empty;
    public string? NewProfileName { get; set; }
    public string? NewServerRoot { get; set; }
    public string? NewBackupRoot { get; set; }
    public string? NewRuntimeRoot { get; set; }
    public int? PortOffset { get; set; }
}

public sealed class WorldCloneResultDto
{
    public bool Success { get; set; }
    public string? NewProfileId { get; set; }
    public string? NewServerRoot { get; set; }
    public int FilesCopied { get; set; }
    public long BytesCopied { get; set; }
    public string Message { get; set; } = string.Empty;
}

// v0.7.82.0: registers a blank (no file copying) new fleet profile at an arbitrary ServerRoot --
// mirrors HeadlessAddServerProfileRequest server-side (LocalManagementApiHost.cs POST /api/v1/servers).
// Used by the "Set Up New Server" wizard's Install Directory step when the default location is
// already occupied by another server, so a genuinely separate second server can be created.
public sealed class AddFleetProfileRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ServerRoot { get; set; } = string.Empty;
    public string SteamCmdPath { get; set; } = string.Empty;
    public string BackupRoot { get; set; } = string.Empty;
    public string RuntimeRoot { get; set; } = string.Empty;
    public IReadOnlyList<string> LaunchArguments { get; set; } = [];
    // ServerRuntimeKind is [JsonConverter(typeof(JsonStringEnumConverter))] on the server side
    // (HeadlessConfiguration.cs), so a plain string here ("WindowsNative"/"LinuxNative") round-trips
    // correctly without needing the enum type mirrored client-side.
    public string Runtime { get; set; } = "WindowsNative";
}

public sealed class AddFleetProfileResultDto
{
    public bool Success { get; set; }
    public bool RestartRequired { get; set; }
    public string? RollbackPath { get; set; }
    public IReadOnlyList<string> ValidationErrors { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}
