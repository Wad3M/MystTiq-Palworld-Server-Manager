namespace PalworldManager.Services;

/// <summary>
/// Shared persisted manifest for mods installed and managed by MystTiq.
/// Kept internal because it is an implementation detail shared by the mod facade
/// and the extracted scanner service.
/// </summary>
internal sealed class InstallManifest
{
    public string Package { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string SourceZip { get; set; } = "";
    public DateTime InstalledUtc { get; set; }
    public List<string> Files { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
    public string Type { get; set; } = "";
    public string EnableMethod { get; set; } = "";
    public bool LastKnownEnabled { get; set; }
}
