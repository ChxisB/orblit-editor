using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Profile;

/// <summary>Where a frame went, read from the engine's profiler.</summary>
/// <remarks>
/// Frame times here are the gaps between frames the scene view draws. A view that is sitting still
/// draws none, so a long gap is rest and not slowness. Drag in the scene for steady figures.
/// </remarks>
internal sealed class ProfilePanel : UserControl
{
    // Not every frame: a panel that rebuilt on each one would add to the cost it is there to show.
    private static readonly TimeSpan Every = TimeSpan.FromMilliseconds(500);

    private readonly Profiler _profiler;
    private readonly StackPanel _lines = new() { Name = "profile_lines", Margin = new Thickness(Space.Sm) };

    // Moved on at each attach and detach, so only the reading begun by the latest attach carries on.
    private int _shown;

    public ProfilePanel(Profiler profiler)
    {
        _profiler = profiler;
        Background = OrblitColors.Surface;
        Content = new ScrollViewer { Content = _lines };
        Read();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _ = ReadEvery(++_shown);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _shown++;
        base.OnDetachedFromVisualTree(e);
    }

    private static IReadOnlyDictionary<string, object?> Map(object? value) =>
        value as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>();

    private static string Ms(IReadOnlyDictionary<string, object?> from, string key) =>
        $"{(from.GetValueOrDefault(key) as double? ?? 0).ToString("0.0", CultureInfo.InvariantCulture)} ms";

    private static string FramesText(IReadOnlyDictionary<string, object?> frames) => frames.GetValueOrDefault("fps") is double fps
        ? $"{fps.ToString("0", CultureInfo.InvariantCulture)} fps · mean {Ms(frames, "meanMs")} · p95 {Ms(frames, "p95Ms")} · max {Ms(frames, "maxMs")}"
        : "none yet";

    private static string SpanText(IReadOnlyDictionary<string, object?> span) => $"mean {Ms(span, "meanMs")} · max {Ms(span, "maxMs")}";

    private static string Megabytes(double bytes) => (bytes / 1048576).ToString("0.0", CultureInfo.InvariantCulture);

    private static string HeapText(IReadOnlyDictionary<string, object?> heap)
    {
        if (heap.GetValueOrDefault("samples") is not IReadOnlyList<double[]> { Count: > 0 } samples)
        {
            return "not read";
        }

        var rate = heap.GetValueOrDefault("bytesPerMinute") is double slope
            ? $" · {(slope >= 0 ? "+" : "−")}{Megabytes(Math.Abs(slope))} MB/min"
            : "";
        return $"{Megabytes(samples[^1][1])} MB{rate}";
    }

    private static Grid Line(string label, string value) => new()
    {
        ColumnDefinitions = new ColumnDefinitions("96,*"),
        Margin = new Thickness(0, 2),
        Children =
        {
            new TextBlock { Text = label, FontSize = OrblitText.Caption, Foreground = OrblitColors.InkDim, TextTrimming = TextTrimming.CharacterEllipsis },
            new TextBlock { Text = value, FontFamily = OrblitText.Mono, FontSize = OrblitText.Label, Foreground = OrblitColors.Ink, TextWrapping = TextWrapping.Wrap, [Grid.ColumnProperty] = 1 },
        },
    };

    private async Task ReadEvery(int shown)
    {
        while (true)
        {
            await Task.Delay(Every);
            if (shown != _shown)
            {
                return;
            }

            Read();
        }
    }

    private void Read()
    {
        _lines.Children.Clear();
        var shot = _profiler.Snapshot();
        if (shot.GetValueOrDefault("frames") is not IReadOnlyDictionary<string, object?> frames)
        {
            var off = Fields.Note("Profiling is off.");
            off.HorizontalAlignment = HorizontalAlignment.Center;
            _lines.Children.Add(off);
            return;
        }

        _lines.Children.Add(Line("Frames", FramesText(frames)));
        var spans = Map(shot.GetValueOrDefault("spans")).Select(pair => (pair.Key, Times: Map(pair.Value)));
        foreach (var (name, times) in spans.OrderByDescending(span => span.Times.GetValueOrDefault("meanMs") as double? ?? 0))
        {
            _lines.Children.Add(Line(name, SpanText(times)));
        }

        foreach (var (name, value) in Map(shot.GetValueOrDefault("gauges")).Concat(Map(shot.GetValueOrDefault("counts"))))
        {
            _lines.Children.Add(Line(name, FormattableString.Invariant($"{value}")));
        }

        _lines.Children.Add(Line("Memory", HeapText(Map(shot.GetValueOrDefault("heap")))));
    }
}
