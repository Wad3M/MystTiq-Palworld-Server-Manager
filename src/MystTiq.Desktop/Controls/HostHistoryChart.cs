using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.Controls;

// v0.8.20.0: the HOST tab's history chart. Processor and memory share one 0-100 % scale (blue, violet); the busiest
// adapter's upload is scaled to its own peak (green). A reading without a value (the first processor reading of a
// session has none) leaves a gap instead of a false zero. Drawn directly, like ResourceHistoryChart.
public sealed class HostHistoryChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<HostHistorySampleDto>?> SamplesProperty =
        AvaloniaProperty.Register<HostHistoryChart, IReadOnlyList<HostHistorySampleDto>?>(nameof(Samples));

    public IReadOnlyList<HostHistorySampleDto>? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    static HostHistoryChart() => AffectsRender<HostHistoryChart>(SamplesProperty);

    // v0.8.25.0: its colours are theme resources, so a theme change redraws it.
    public HostHistoryChart() => ResourcesChanged += (_, _) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width; var height = Bounds.Height;
        if (width <= 4 || height <= 4) return;
        using var clip = context.PushClip(new Rect(0, 0, width, height));
        const double inset = 2;
        var plot = new Rect(inset, inset, Math.Max(1, width - inset * 2), Math.Max(1, height - inset * 2));
        // v0.8.25.0: themed, as ResourceHistoryChart.
        context.FillRectangle(new SolidColorBrush(DecorativePalette.Resolve(this, "DecoBackground_08111A", "#08111A")), new Rect(0, 0, width, height));
        var grid = new Pen(new SolidColorBrush(DecorativePalette.Resolve(this, "DecoBorder_183047", "#183047")), 1);
        for (var i = 1; i <= 3; i++)
        {
            var y = plot.Y + plot.Height * i / 4d;
            context.DrawLine(grid, new Point(plot.X, y), new Point(plot.Right, y));
        }

        var samples = Samples ?? [];
        if (samples.Count == 0) return;
        var peakSent = samples.Max(s => s.SentBytesPerSecond ?? 0);
        double X(int i) => samples.Count == 1 ? plot.X + plot.Width / 2 : plot.X + i * plot.Width / (samples.Count - 1d);
        Point? At(int i, double? value, double max) => value is { } v && max > 0
            ? new Point(X(i), plot.Bottom - Math.Clamp(v / max, 0, 1) * plot.Height) : null;

        void Series(Func<HostHistorySampleDto, double?> value, double max, string key, string fallback)
        {
            var pen = new Pen(new SolidColorBrush(DecorativePalette.Resolve(this, key, fallback)), 2);
            for (var i = 1; i < samples.Count; i++)
                if (At(i - 1, value(samples[i - 1]), max) is { } a && At(i, value(samples[i]), max) is { } b) context.DrawLine(pen, a, b);
            if (samples.Count == 1 && At(0, value(samples[0]), max) is { } only) context.DrawEllipse(pen.Brush, null, only, 2, 2);
        }
        Series(s => s.MemoryUsedPercent, 100, "Violet", "#B58BFF");
        Series(s => s.CpuPercent, 100, "Blue", "#54B8FF");
        Series(s => s.SentBytesPerSecond, peakSent, "Green", "#63DF7B");
    }
}
