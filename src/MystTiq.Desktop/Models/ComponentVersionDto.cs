// MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

// v0.7.45.0: Update Center Overhaul -- mirrors MystTiq.HeadlessHost's ComponentVersionInfo.
public sealed class ComponentVersionDto
{
    [JsonPropertyName("group")] public string Group { get; init; } = string.Empty;
    [JsonPropertyName("component")] public string Component { get; init; } = string.Empty;
    [JsonPropertyName("installedVersion")] public string InstalledVersion { get; init; } = string.Empty;
    [JsonPropertyName("latestVersion")] public string LatestVersion { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("lastChecked")] public DateTimeOffset LastChecked { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;

    public string StatusText => Status switch
    {
        "UpToDate" => "Up to date",
        "UpdateAvailable" => "Update available",
        "NotInstalled" => "Not installed",
        "SelfUpdating" => "Self-updating",
        "CheckManually" => "Check manually",
        "NotApplicable" => "Not applicable",
        _ => "Unknown"
    };

    // v0.7.82.0: direct live feedback ("we have a couple that say unknown, we need to do better and
    // have a way to check"). Several components (Python, VC++ Runtime, MSVC Build Tools, PIM/Oodle)
    // genuinely have no reliable unattended "latest version" feed (see
    // HeadlessComponentUpdateService's own header comment) -- rather than fabricate one, this opens
    // the real, official page a human can check manually. Keyed primarily by Component (Source text
    // is shared verbatim across unrelated rows, e.g. both VC++ Runtime and MSVC Build Tools report
    // "Windows Registry (no reliable \"latest\" feed)"), falling back to parsing Source for the
    // GitHub:/PyPI: rows where the label itself already names the real target.
    public string? SourceUrl => Component switch
    {
        "Python Runtime" => "https://www.python.org/downloads/windows/",
        "Visual C++ Runtime" => "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist",
        "Microsoft C++ Build Tools" => "https://visualstudio.microsoft.com/visual-cpp-build-tools/",
        // v1.0.1.0: the row is "PlM/Oodle Decoder" (lower-case L); keyed "PIM" before, it never had its page. The page was a
        // folder this repository does not have (404); the decoder's install record names PalworldSaveTools as its source.
        "PlM/Oodle Decoder" => "https://github.com/deafdudecomputers/PalworldSaveTools",
        ".NET Runtime" => "https://dotnet.microsoft.com/en-us/download/dotnet",
        _ when Source.StartsWith("GitHub: ", StringComparison.Ordinal) => $"https://github.com/{Source["GitHub: ".Length..]}/releases",
        _ when Source.StartsWith("PyPI: ", StringComparison.Ordinal) => $"https://pypi.org/project/{Source["PyPI: ".Length..]}/",
        _ => null
    };
    public bool HasSourceUrl => !string.IsNullOrEmpty(SourceUrl);

    // v1.0.1.0 (asked 2026-10-05: "the update page should have a button to update every option. It can be greyed out if it
    // is self updating, but should always have the option to update"). Every row has Update; what it does depends on the
    // component. Greyed out only when there is nothing it could do: SteamCMD updates itself, a component that does not
    // apply here, or MystTiq with no newer release.
    public ComponentUpdateMethod UpdateMethod => Component switch
    {
        "MystTiq Server Manager" => Status == "UpdateAvailable" ? ComponentUpdateMethod.DownloadRelease : ComponentUpdateMethod.NothingNewer,
        "SteamCMD" => Status == "SelfUpdating" ? ComponentUpdateMethod.SelfUpdating : ComponentUpdateMethod.ServerFiles,
        "Palworld Dedicated Server" => ComponentUpdateMethod.ServerFiles,
        "UE4SS Runtime" => ComponentUpdateMethod.Ue4ssInstall,
        "PalDefender" => Status == "NotInstalled" ? ComponentUpdateMethod.OfficialPage : ComponentUpdateMethod.PalDefender,
        "pip" => ComponentUpdateMethod.Pip,
        "Palworld Save Tools" => ComponentUpdateMethod.SaveTools,
        _ when Status == "NotApplicable" => ComponentUpdateMethod.NotApplicable,
        _ when HasSourceUrl => ComponentUpdateMethod.OfficialPage,
        _ => ComponentUpdateMethod.NotApplicable
    };

    public bool CanUpdate => UpdateMethod is not (ComponentUpdateMethod.SelfUpdating or ComponentUpdateMethod.NotApplicable or ComponentUpdateMethod.NothingNewer);

    // What Update does, or why it is greyed out (its tooltip, and shown under a greyed-out button).
    public string UpdateHint => UpdateMethod switch
    {
        ComponentUpdateMethod.DownloadRelease => "Downloads the new MystTiq, checks it against the release's checksums and unpacks it into a new folder beside this one.",
        ComponentUpdateMethod.NothingNewer => Status == "UpToDate" ? "This is the newest MystTiq; there is nothing newer to update to." : "No newer MystTiq release was found; Refresh to check again.",
        ComponentUpdateMethod.SelfUpdating => "SteamCMD updates itself every time it runs.",
        ComponentUpdateMethod.ServerFiles => Component == "SteamCMD"
            ? "Installs SteamCMD and updates the server files with it. The server must be stopped."
            : "Updates the server files with SteamCMD. The server must be stopped.",
        ComponentUpdateMethod.Ue4ssInstall => "Opens the UE4SS page with the newest release selected and previews the install; you apply it there.",
        ComponentUpdateMethod.PalDefender => "Downloads the newest PalDefender and replaces PalDefender.dll and d3d9.dll, keeping your settings. The server must be stopped.",
        ComponentUpdateMethod.Pip => "Upgrades pip with pip itself.",
        ComponentUpdateMethod.SaveTools => "Upgrades the palworld-save-tools package with pip.",
        ComponentUpdateMethod.OfficialPage when Component == "Python Runtime" =>
            "Opens the official download page. MystTiq does not update Python itself: the save decoder is built for this Python version, so stay on the same 3.x release.",
        ComponentUpdateMethod.OfficialPage => "MystTiq cannot update this safely by itself, so Update opens the official download page.",
        _ => "Not used on this system."
    };

    // Open (the release or project page) stays beside Update, except where Update already opens that page.
    public bool ShowsOpen => HasSourceUrl && UpdateMethod != ComponentUpdateMethod.OfficialPage;
}

public enum ComponentUpdateMethod
{
    DownloadRelease, NothingNewer, SelfUpdating, ServerFiles, Ue4ssInstall, PalDefender, Pip, SaveTools, OfficialPage, NotApplicable
}

public sealed class ComponentVersionSnapshotDto
{
    [JsonPropertyName("components")] public IReadOnlyList<ComponentVersionDto> Components { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
}

public sealed class ComponentUpdateResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
