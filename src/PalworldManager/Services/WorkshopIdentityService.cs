namespace PalworldManager.Services;

/// <summary>
/// Resolves friendly Steam Workshop identities from MystTiq's existing metadata
/// cache so rescans and verification do not regress to "Workshop Mod <id>".
/// </summary>
public sealed class WorkshopIdentityService
{
    private readonly string cacheRoot;

    public WorkshopIdentityService()
    {
        cacheRoot = Path.Combine(ApplicationPathService.Current.CacheRoot, "Mods");
    }

    public string ResolveDisplayName(string workshopId, string currentName)
    {
        if (string.IsNullOrWhiteSpace(workshopId))
            return currentName;

        var cached = TryReadCachedTitle(workshopId);
        if (!string.IsNullOrWhiteSpace(cached))
            return $"{cached} ({workshopId})";

        return currentName;
    }

    private string? TryReadCachedTitle(string workshopId)
    {
        var path = Path.Combine(cacheRoot, workshopId + ".json");
        if (!File.Exists(path))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Title", out var titleElement))
                return null;

            var title = System.Net.WebUtility.HtmlDecode(titleElement.GetString() ?? string.Empty).Trim();
            foreach (var prefix in new[] { "Steam Workshop::", "Steam Community :: Workshop :: " })
                if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    title = title[prefix.Length..].Trim();

            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        catch
        {
            return null;
        }
    }
}
