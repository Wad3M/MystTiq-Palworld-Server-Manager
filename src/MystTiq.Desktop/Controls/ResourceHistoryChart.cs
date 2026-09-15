using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Controls;

/// <summary>
/// Lightweight custom-drawn dual-series history chart. Drawing directly avoids
/// hundreds of child controls and keeps Windows/Linux rendering identical.
/// </summary>
public sealed class ResourceHistoryChart : Control
{
    public static readonly StyledProperty<IEnumerable<HistoricalMetricPointDto>?> ItemsSourceProperty =
        AvaloniaProperty.Register<ResourceHistoryChart, IEnumerable<HistoricalMetricPointDto>?>(nameof(ItemsSource));

    private INotifyCollectionChanged? observedCollection;

    public IEnumerable<HistoricalMetricPointDto>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    static ResourceHistoryChart()
    {
        AffectsRender<ResourceHistoryChart>(ItemsSourceProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ItemsSourceProperty) return;

        if (observedCollection is not null)
            observedCollection.CollectionChanged -= CollectionChanged;

        observedCollection = change.GetNewValue<IEnumerable<HistoricalMetricPointDto>?>() as INotifyCollectionChanged;
        if (observedCollection is not null)
            observedCollection.CollectionChanged += CollectionChanged;
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 4 || height <= 4) return;

        // Drawing strokes centered exactly on Bounds.Right/Bottom can antialias outside
        // the control. Clip first and keep all plotted geometry inside an inset plot rect.
        using var clip = context.PushClip(new Rect(0, 0, width, height));
        const double inset = 2;
        var plot = new Rect(inset, inset, Math.Max(1, width - inset * 2), Math.Max(1, height - inset * 2));

        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#183047")), 1);
        var cpuPen = new Pen(new SolidColorBrush(Color.Parse("#54B8FF")), 2);
        var memoryPen = new Pen(new SolidColorBrush(Color.Parse("#B58BFF")), 2);
        // v0.7.15.0: real in-game FPS series (Palworld's own /v1/api/metrics, same data v0.7.9.0
        // already showed live). Drawn only across consecutive samples that both actually have a
        // value -- ServerFps is null (not 0) whenever the REST API was disabled for that sample, so
        // a plain line-through-zero would misleadingly read as a real performance crash.
        var fpsPen = new Pen(new SolidColorBrush(Color.Parse("#63DF7B")), 2);
        var background = new SolidColorBrush(Color.Parse("#08111A"));
        context.FillRectangle(background, new Rect(0, 0, width, height));

        for (var i = 1; i <= 3; i++)
        {
            var y = plot.Y + plot.Height * i / 4d;
            context.DrawLine(gridPen, new Point(plot.X, y), new Point(plot.Right, y));
        }

        var samples = ItemsSource?.ToArray() ?? [];
        if (samples.Length == 0) return;

        var memoryMin = samples.Min(x => x.MemoryMb);
        var memoryMax = samples.Max(x => x.MemoryMb);
        if (Math.Abs(memoryMax - memoryMin) < 0.01)
            memoryMax = memoryMin + 1;

        Point CpuPoint(int index)
        {
            var x = samples.Length == 1 ? plot.X + plot.Width / 2d : plot.X + index * plot.Width / (samples.Length - 1d);
            var y = plot.Bottom - (Math.Clamp(samples[index].CpuPercent, 0, 100) / 100d * plot.Height);
            return new Point(Math.Clamp(x, plot.X, plot.Right), Math.Clamp(y, plot.Y, plot.Bottom));
        }

        Point MemoryPoint(int index)
        {
            var x = samples.Length == 1 ? plot.X + plot.Width / 2d : plot.X + index * plot.Width / (samples.Length - 1d);
            var ratio = (samples[index].MemoryMb - memoryMin) / (memoryMax - memoryMin);
            var y = plot.Bottom - ratio * plot.Height;
            return new Point(Math.Clamp(x, plot.X, plot.Right), Math.Clamp(y, plot.Y, plot.Bottom));
        }

        var fpsValues = samples.Where(x => x.ServerFps.HasValue).Select(x => x.ServerFps!.Value).ToArray();
        var fpsMin = fpsValues.Length == 0 ? 0d : fpsValues.Min();
        var fpsMax = fpsValues.Length == 0 ? 1d : fpsValues.Max();
        if (Math.Abs(fpsMax - fpsMin) < 0.01)
            fpsMax = fpsMin + 1;

        Point? FpsPoint(int index)
        {
            if (!samples[index].ServerFps.HasValue) return null;
            var x = samples.Length == 1 ? plot.X + plot.Width / 2d : plot.X + index * plot.Width / (samples.Length - 1d);
            var ratio = (samples[index].ServerFps!.Value - fpsMin) / (fpsMax - fpsMin);
            var y = plot.Bottom - ratio * plot.Height;
            return new Point(Math.Clamp(x, plot.X, plot.Right), Math.Clamp(y, plot.Y, plot.Bottom));
        }

        for (var i = 1; i < samples.Length; i++)
        {
            context.DrawLine(cpuPen, CpuPoint(i - 1), CpuPoint(i));
            context.DrawLine(memoryPen, MemoryPoint(i - 1), MemoryPoint(i));
            var fpsFrom = FpsPoint(i - 1);
            var fpsTo = FpsPoint(i);
            if (fpsFrom.HasValue && fpsTo.HasValue)
                context.DrawLine(fpsPen, fpsFrom.Value, fpsTo.Value);
        }

        if (samples.Length == 1)
        {
            context.DrawEllipse(cpuPen.Brush, null, CpuPoint(0), 2, 2);
            context.DrawEllipse(memoryPen.Brush, null, MemoryPoint(0), 2, 2);
            var fpsOnly = FpsPoint(0);
            if (fpsOnly.HasValue)
                context.DrawEllipse(fpsPen.Brush, null, fpsOnly.Value, 2, 2);
        }
    }
}
