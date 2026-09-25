namespace MystTiq.Core.Services;

// v0.8.3.0: the item picker's search box. Palworld ids are CamelCase words joined by underscores ("PalSphere_Mega",
// "ExpBoost_03"), so a query matches when every word typed appears in the id, ignoring case, underscores and spaces:
// "sphere mega", "palsphere" and "SPHERE_MEGA" all find PalSphere_Mega. An empty query matches everything.
// Kept in Core so the logic harness covers it and the Desktop filters exactly the same way.
public static class GameIdSearch
{
    public static bool Matches(string? id, string? query)
    {
        var words = (query ?? string.Empty).Split([' ', '_', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return true;
        var flat = Flatten(id);
        return flat.Length > 0 && words.All(w => flat.Contains(Flatten(w), StringComparison.Ordinal));
    }

    // v0.8.13.0: search the display name as well: every word must be found in the id or the name ("pal sphere",
    // "sphere", "PalSphere" all find Pal Sphere; "lamball" finds SheepBall).
    public static bool Matches(string? id, string? name, string? query)
    {
        var words = (query ?? string.Empty).Split([' ', '_', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return true;
        var flatId = Flatten(id);
        var flatName = Flatten(name);
        if (flatId.Length == 0 && flatName.Length == 0) return false;
        return words.Select(Flatten).All(w => flatId.Contains(w, StringComparison.Ordinal) || flatName.Contains(w, StringComparison.Ordinal));
    }

    private static string Flatten(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
