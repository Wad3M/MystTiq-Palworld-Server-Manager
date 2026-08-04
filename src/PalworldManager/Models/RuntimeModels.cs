namespace PalworldManager.Models;

public sealed class Ue4ssReleaseInfo
{
    public string Source { get; set; } = "Palworld Fork";
    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public bool Prerelease { get; set; }
    public string HtmlUrl { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string AssetUrl { get; set; } = "";
    public string ReleaseKey => $"{Source}|{Tag}|{AssetName}";
    public string Display
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(Name) ? Tag : Name;
            var asset = string.IsNullOrWhiteSpace(AssetName) ? "" : $" • {AssetName}";
            return $"[{Source}] {label} ({Tag}){asset}{(Prerelease ? " • Pre-release" : "")}";
        }
    }
}

public sealed class StabilitySampleRow
{
    public string Elapsed { get; set; } = "00:00";
    public string Process { get; set; } = "Unknown";
    public string Responding { get; set; } = "Unknown";
    public string Memory { get; set; } = "—";
    public string PrivateMemory { get; set; } = "—";
    public int Handles { get; set; }
    public int Threads { get; set; }
    public string GamePort { get; set; } = "Unknown";
    public string SteamPort { get; set; } = "Unknown";
    public string RestPort { get; set; } = "Unknown";
    public string RconPort { get; set; } = "Unknown";
}
