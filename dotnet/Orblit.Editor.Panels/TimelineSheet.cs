namespace Orblit.Editor.Panels;

using Orblit.Motion;

/// <summary>One line of the timeline: what a clip moves, or one channel of it.</summary>
/// <param name="Target">What the line is about, by its id in the scene. Empty is whatever plays the clip.</param>
/// <param name="Channel">The channel the line shows, or null on a heading.</param>
public sealed record TimelineRow(string Target, ClipChannel? Channel)
{
    public bool IsHeading => Channel is null;

    /// <summary>
    /// <paramref name="clip"/>'s rows: each thing it moves, in the order it first moves it, with its
    /// channels under it.
    /// </summary>
    public static IReadOnlyList<TimelineRow> Of(ClipDocument clip) =>
    [
        .. clip.Channels.GroupBy(channel => channel.Target).SelectMany(group =>
            group.Select(channel => new TimelineRow(group.Key, channel)).Prepend(new TimelineRow(group.Key, null))),
    ];
}

/// <summary>
/// Where a moment of a clip is drawn across a strip of a given width.
/// </summary>
/// <remarks>
/// The whole clip always fits: a clip is seconds long, not minutes, and one that fits is one nobody has to
/// scroll to find the end of. Keys left past the end by a clip made shorter stretch it, so they can still be
/// seen and dragged back.
/// </remarks>
public sealed class TimeAxis
{
    /// <summary>Room at each end, so a key on the first or last frame is not cut in half.</summary>
    public const double Margin = 10;

    private readonly double _width;

    public TimeAxis(ClipDocument clip, double width)
    {
        _width = width;
        Duration = clip.Duration;
        Span = clip.Channels.Aggregate(clip.Duration, (last, channel) => Math.Max(last, channel.End));
    }

    public double Duration { get; }

    /// <summary>From the start to whichever is later, the end or the last key.</summary>
    public double Span { get; }

    public double PerSecond => Span <= 0 ? 0 : Math.Max(_width - Margin * 2, 1) / Span;

    public double XOf(double at) => Margin + at * PerSecond;

    public double AtOf(double x) => PerSecond == 0 ? 0 : Math.Clamp((x - Margin) / PerSecond, 0, Span);

    /// <summary>
    /// How many frames apart lines can be drawn at <paramref name="perSecond"/> pixels a second without
    /// coming closer than <paramref name="gap"/> pixels.
    /// </summary>
    public static int FrameStep(double perSecond, double rate, double gap = 8)
    {
        ReadOnlySpan<int> steps = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1200, 3000];
        if (rate <= 0 || perSecond <= 0)
        {
            return 1;
        }

        foreach (var step in steps)
        {
            if (step / rate * perSecond >= gap)
            {
                return step;
            }
        }

        return steps[^1];
    }
}
