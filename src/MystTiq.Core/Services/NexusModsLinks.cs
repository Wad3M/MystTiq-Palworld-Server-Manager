namespace MystTiq.Core.Services;

// v0.7.93.0: pure parsing/validation helpers for the Nexus Mods catalog feature. Kept in Core (not
// the Desktop client) so they are unit-testable in the logic harness without any network or UI.
public sealed record NexusNxmLink(string GameDomain, int ModId, int FileId, string Key, long Expires);

public static class NexusModsLinks
{
    public const string PalworldGameDomain = "palworld";

    // nxm://palworld/mods/3329/files/12345?key=AbC&expires=1700000000&user_id=99 -- the link Nexus
    // hands to a mod manager from its "Mod Manager Download" button. Carries the one-time key and
    // expiry a non-premium account needs to request a download link from the API.
    public static bool TryParseNxm(string? text, out NexusNxmLink? link, out string error)
    {
        link = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Paste an nxm:// link first.";
            return false;
        }

        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("nxm", StringComparison.OrdinalIgnoreCase))
        {
            error = "That is not an nxm:// link.";
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 4 ||
            !segments[0].Equals("mods", StringComparison.OrdinalIgnoreCase) ||
            !segments[2].Equals("files", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(segments[1], out var modId) || modId <= 0 ||
            !int.TryParse(segments[3], out var fileId) || fileId <= 0)
        {
            error = "The link is not in the expected nxm://game/mods/{id}/files/{id} form.";
            return false;
        }

        if (!uri.Host.Equals(PalworldGameDomain, StringComparison.OrdinalIgnoreCase))
        {
            error = $"That link is for '{uri.Host}', not Palworld.";
            return false;
        }

        string? key = null;
        string? expiresText = null;
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0) continue;
            var name = pair[..separator];
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
            if (name.Equals("key", StringComparison.OrdinalIgnoreCase)) key = value;
            else if (name.Equals("expires", StringComparison.OrdinalIgnoreCase)) expiresText = value;
        }

        if (string.IsNullOrWhiteSpace(key) || !long.TryParse(expiresText, out var expires) || expires <= 0)
        {
            error = "The link is missing its key or expiry. Copy the whole link from the Mod Manager Download button.";
            return false;
        }

        link = new NexusNxmLink(PalworldGameDomain, modId, fileId, key, expires);
        return true;
    }

    // Accepts a bare id ("3329") or a Palworld mod page URL
    // (https://www.nexusmods.com/palworld/mods/3329, optionally with ?tab=files etc).
    public static bool TryParseModReference(string? text, out int modId)
    {
        modId = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        if (int.TryParse(trimmed, out modId) && modId > 0) return true;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            !(uri.Host.Equals("nexusmods.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.EndsWith(".nexusmods.com", StringComparison.OrdinalIgnoreCase)))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3 &&
            segments[0].Equals(PalworldGameDomain, StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("mods", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(segments[2], out modId) && modId > 0)
            return true;

        modId = 0;
        return false;
    }

    public static string ModPageUrl(int modId) => $"https://www.nexusmods.com/{PalworldGameDomain}/mods/{modId}";

    // The download URI comes from Nexus's own API response, but is still only followed when it is an
    // https URL on a Nexus-owned domain, so a malformed or tampered response can never send the
    // desktop app to an arbitrary host.
    public static bool IsAllowedDownloadHost(Uri? uri)
    {
        if (uri is null || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        var host = uri.Host;
        return host.Equals("nexusmods.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".nexusmods.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("nexus-cdn.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".nexus-cdn.com", StringComparison.OrdinalIgnoreCase);
    }

    // Archive kind from the first bytes of a downloaded file. MystTiq's validated install only
    // accepts ZIP; 7z and RAR are common on Nexus and need a clear message rather than a confusing
    // server-side failure.
    public static string DescribeArchiveKind(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B &&
            (header[2] == 0x03 || header[2] == 0x05) && (header[3] == 0x04 || header[3] == 0x06))
            return "zip";
        if (header.Length >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC && header[3] == 0xAF)
            return "7z";
        if (header.Length >= 6 && header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 && header[3] == 0x21)
            return "rar";
        return "unknown";
    }
}
