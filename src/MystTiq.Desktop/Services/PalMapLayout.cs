namespace MystTiq.Desktop.Services;

// v0.8.11.0: the Pals layer on the World Map. Pure, so the logic harness can compile this file directly.
//
// A base keeps its workers within a few dozen metres, which at the whole-map zoom is the same pixel, so Pals closer
// than ClusterRadius screen pixels are drawn as one marker with a count. Clustering runs on the current screen positions,
// so zooming in separates them. Species are the game's internal ids (e.g. PinkCat); English names are not in the save,
// but v0.8.13.0 reads them from the game's own files (SpeciesName, e.g. Cattiva) when it can.
public sealed record PalMapEntry(string Species, bool IsAlpha, int Level, string NickName, string OwnerName, string Placement, string GuildName,
    double WorldX = 0, double WorldY = 0, string SpeciesName = "");

public sealed record PalMapCluster(double ViewX, double ViewY, double FlatX, double FlatY, IReadOnlyList<PalMapEntry> Members);

public static class PalMapLayout
{
    public const string BaseWorker = "BaseWorker";
    public const string Party = "Party";
    public const double ClusterRadius = 10;
    public const int TooltipLines = 12;

    // Greedy and order-stable: each Pal joins the first cluster whose first member is within the radius. The marker
    // sits at the members' average position.
    public static IReadOnlyList<PalMapCluster> Cluster(
        IReadOnlyList<(double ViewX, double ViewY, double FlatX, double FlatY, PalMapEntry Pal)> points, double radius = ClusterRadius)
    {
        var seeds = new List<(double X, double Y)>();
        var groups = new List<List<(double ViewX, double ViewY, double FlatX, double FlatY, PalMapEntry Pal)>>();
        foreach (var point in points)
        {
            var index = seeds.FindIndex(s => Math.Sqrt((s.X - point.ViewX) * (s.X - point.ViewX) + (s.Y - point.ViewY) * (s.Y - point.ViewY)) <= radius);
            if (index < 0) { seeds.Add((point.ViewX, point.ViewY)); groups.Add([point]); }
            else groups[index].Add(point);
        }

        return groups.Select(g => new PalMapCluster(
            g.Average(p => p.ViewX), g.Average(p => p.ViewY), g.Average(p => p.FlatX), g.Average(p => p.FlatY),
            g.Select(p => p.Pal).ToArray())).ToArray();
    }

    // Base workers stand within a few dozen metres of their base, so until the map is zoomed far in their marker would
    // sit under the base's own square and could not be seen or clicked. A cluster that close to a base marker is drawn
    // as a badge on the base square's top-right corner instead; zooming in further separates it again.
    public const double BadgeSnapRadius = 12;
    public const double BadgeOffset = 9;

    public static (double X, double Y)? BadgePosition(double x, double y, IReadOnlyList<(double X, double Y)> baseMarkers, double snap = BadgeSnapRadius)
    {
        (double X, double Y)? nearest = null;
        var best = double.MaxValue;
        foreach (var b in baseMarkers)
        {
            var d = Math.Sqrt((b.X - x) * (b.X - x) + (b.Y - y) * (b.Y - y));
            if (d <= snap && d < best) { best = d; nearest = b; }
        }
        return nearest is { } at ? (at.X + BadgeOffset, at.Y - BadgeOffset) : null;
    }

    // v0.8.13.0: the display name ("Cattiva") when known, else the internal id.
    public static string Species(PalMapEntry pal) => string.IsNullOrWhiteSpace(pal.SpeciesName) ? pal.Species : pal.SpeciesName;

    public static string Name(PalMapEntry pal) =>
        (string.IsNullOrWhiteSpace(pal.NickName) ? Species(pal) : $"{pal.NickName} ({Species(pal)})") + (pal.IsAlpha ? ", alpha" : string.Empty);

    public static string Where(PalMapEntry pal) => pal.Placement switch
    {
        BaseWorker => string.IsNullOrWhiteSpace(pal.GuildName) ? "working at a base" : $"working at {pal.GuildName}'s base",
        Party => string.IsNullOrWhiteSpace(pal.OwnerName) ? "in a player's party" : $"in {pal.OwnerName}'s party",
        _ => "in the world"
    };

    public static string MemberLine(PalMapEntry pal) => $"{Name(pal)}, level {pal.Level}, {Where(pal)}";

    public static string Title(PalMapCluster cluster)
    {
        if (cluster.Members.Count == 1) return $"{Name(cluster.Members[0])}, level {cluster.Members[0].Level}";
        var places = cluster.Members.Select(Where).Distinct(StringComparer.Ordinal).ToArray();
        return places.Length == 1 ? $"{cluster.Members.Count} Pals {places[0]}" : $"{cluster.Members.Count} Pals";
    }

    public static string Tooltip(PalMapCluster cluster, string coordinateText, bool onBaseBadge = false)
    {
        var at = string.IsNullOrEmpty(coordinateText) ? string.Empty : $" {coordinateText}";
        var badge = onBaseBadge ? " Drawn on the base's corner at this zoom; zoom in to see where each one stands." : string.Empty;
        if (cluster.Members.Count == 1)
            return $"{MemberLine(cluster.Members[0])}{at}. Last position the save recorded.{badge} Click to zoom in.";
        var lines = cluster.Members.Take(TooltipLines).Select(MemberLine).ToList();
        if (cluster.Members.Count > TooltipLines) lines.Add($"…and {cluster.Members.Count - TooltipLines} more");
        return $"{Title(cluster)}{at}:\n" + string.Join("\n", lines) + $"\nLast positions the save recorded.{badge} Click to zoom in.";
    }

    // The line under the map: what is drawn, and what the save holds but is not drawn, and why.
    public static string Status(int workers, int party, int inPalbox, int withoutPosition, bool semanticAvailable, bool namesAvailable = false)
    {
        if (!semanticAvailable) return "No Pal positions: they need decoded Level.sav.json (Palworld Save Tools).";
        var notDrawn = new List<string>();
        if (inPalbox > 0) notDrawn.Add($"{inPalbox} in a Palbox (the save only keeps where each was last let out)");
        if (withoutPosition > 0) notDrawn.Add($"{withoutPosition} without a recorded position");
        var tail = notDrawn.Count > 0 ? $" Not drawn: {string.Join(", ", notDrawn)}." : string.Empty;
        if (workers + party == 0) return "No Pals are out in the world in the save." + tail;
        return $"{workers + party} Pal(s) on the map: {workers} working at bases, {party} in players' parties, at the last position " +
               "the save recorded. Wild Pals are not in the save." + (namesAvailable ? string.Empty : " Species are the game's internal names.") + tail;
    }
}
