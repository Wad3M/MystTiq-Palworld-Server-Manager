namespace MystTiq.Desktop.Services;

// v0.7.106.0: markers standing close together on the Map page had their name labels draw directly on
// top of each other. Pure, deterministic, and takes the SCREEN position of every marker (bases and players
// together), so it reacts to zoom. Never moves a dot, only where its label goes.
//
// v0.7.115.0 (deficiency report, "map markers and labels can overlap -- reproduced using the application's
// actual code"): the v0.7.106.0 version decided by DISTANCE BETWEEN DOTS (a 30px circle) and pushed a label
// down one step per earlier dot inside that circle. That is not the same question as "do these two pieces of
// text overlap", and it failed both ways:
//   - a chain of markers 25px apart (A, B, C) gave B and C the same offset (each has exactly one earlier
//     neighbour inside 30px), so their text, far wider than 25px, overlapped;
//   - a dot slightly above and to the left of another put its pushed-down label exactly on the other's
//     unpushed one, because pushing was never checked against where labels actually ended up.
// Now each label is a rectangle (its own text width, one line high) placed greedily in draw order at the
// nearest candidate spot (below, right, above, left, then further out) that overlaps no label already placed
// and no other marker's dot. The harness scenarios check the actual rectangles for overlap, not the offsets.
public static class MapLabelLayout
{
    // How far, in pixels, a label moves per step when the nearby spots are taken: one line of text plus a gap.
    public const double LabelStep = 13;
    // One line of label text.
    public const double LabelHeight = 12;
    // The marker dot is 10px, drawn centred on its point; its label starts at the dot's left edge, just below it.
    public const double DotSize = 10;
    // How many lines out a label may move before giving up (a very dense cluster at whole-map zoom; zooming in
    // separates it). Keeps the layout bounded and deterministic.
    public const int MaximumSteps = 12;

    public readonly record struct MapLabel(double X, double Y, double Width);

    // Average glyph width of the map's label font is a little over half its size; slightly generous on purpose,
    // since an estimate that is too narrow is what lets text overlap.
    public static double EstimateLabelWidth(string? text, double fontSize) =>
        Math.Max(DotSize, (text?.Length ?? 0) * fontSize * 0.62 + 2);

    public readonly record struct Box(double Left, double Top, double Width, double Height)
    {
        public bool Overlaps(Box other) =>
            Left < other.Left + other.Width && other.Left < Left + Width &&
            Top < other.Top + other.Height && other.Top < Top + Height;
    }

    // (dx, dy) relative to the label's default spot (left edge of the dot, just below it).
    public static Box LabelBox(MapLabel label, (double X, double Y) offset) =>
        new(label.X - DotSize / 2 + offset.X, label.Y + DotSize / 2 + offset.Y, label.Width, LabelHeight);

    public static Box DotBox(double x, double y) => new(x - DotSize / 2, y - DotSize / 2, DotSize, DotSize);

    // Candidate spots, nearest first: below the dot, to its right, above it, to its left, then further down
    // and further right one line at a time. The first spot free of every placed label and every other dot wins,
    // so a label stays next to its own marker whenever there is any room nearby.
    private static IEnumerable<(double X, double Y)> Candidates(MapLabel label)
    {
        var rightX = DotSize + 2;                    // just past the dot's right edge
        var besideY = -(DotSize / 2 + LabelHeight / 2); // vertically centred on the dot
        yield return (0, 0);
        yield return (rightX, besideY);
        yield return (0, -(DotSize + LabelHeight + 1));
        yield return (-(label.Width + 2), besideY);
        for (var step = 1; step < MaximumSteps; step++)
        {
            yield return (0, step * LabelStep);
            yield return (rightX, besideY + step * LabelStep);
            yield return (rightX, besideY - step * LabelStep);
        }
    }

    // v0.8.11.0: obstacles are other things drawn on the map that a label must not cover (the Pals markers), placed
    // before any label so every label steps around them the same way it steps around another label.
    public static IReadOnlyList<(double X, double Y)> ComputeLabelOffsets(IReadOnlyList<MapLabel> labels, IReadOnlyList<Box>? obstacles = null)
    {
        var offsets = new (double X, double Y)[labels.Count];
        var placed = new List<Box>(labels.Count + (obstacles?.Count ?? 0));
        if (obstacles is not null) placed.AddRange(obstacles);
        var dots = labels.Select(l => DotBox(l.X, l.Y)).ToArray();
        for (var i = 0; i < labels.Count; i++)
        {
            (double X, double Y)? chosen = null;
            (double X, double Y) last = (0, 0);
            foreach (var candidate in Candidates(labels[i]))
            {
                last = candidate;
                var box = LabelBox(labels[i], candidate);
                var collides = placed.Any(box.Overlaps);
                for (var d = 0; !collides && d < dots.Length; d++)
                    if (d != i && box.Overlaps(dots[d])) collides = true;
                if (!collides) { chosen = candidate; break; }
            }
            // Too dense to separate at this zoom: take the furthest spot tried (zooming in separates the cluster).
            offsets[i] = chosen ?? last;
            placed.Add(LabelBox(labels[i], offsets[i]));
        }
        return offsets;
    }

    // v0.7.109.0: the residual gap v0.7.106.0's own doc named -- two dots at (or extremely near) the exact
    // same screen position rendered as a single visible icon, since only their LABELS were ever staggered.
    // Deliberately a tiny radius: this is about two icons occupying the same few pixels, and the true
    // position must stay meaningful for anything genuinely farther apart than a rounding error would produce.
    public const double CoincidentRadius = 3;
    // How far, in pixels, a coincident marker is fanned out from the shared point. Small enough that at
    // whole-map zoom it is not a meaningfully different position, since the two markers were already
    // indistinguishable at that resolution.
    public const double SpreadRadius = 6;

    // The first marker at a shared point is undisturbed (truest to its real position), and each one after it
    // fans out around a small ring so every marker in a coincident cluster stays visible. Positions farther
    // apart than CoincidentRadius are untouched -- this is strictly for the "exact same pixel" case.
    public static IReadOnlyList<(double X, double Y)> ComputeMarkerOffsets(IReadOnlyList<(double X, double Y)> points)
    {
        var offsets = new (double X, double Y)[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var rank = 0;
            for (var j = 0; j < i; j++)
            {
                var dx = points[i].X - points[j].X;
                var dy = points[i].Y - points[j].Y;
                if (dx * dx + dy * dy <= CoincidentRadius * CoincidentRadius) rank++;
            }
            if (rank == 0) continue;
            var angle = rank * (Math.PI * 2 / 6);
            offsets[i] = (Math.Cos(angle) * SpreadRadius, Math.Sin(angle) * SpreadRadius);
        }
        return offsets;
    }
}
