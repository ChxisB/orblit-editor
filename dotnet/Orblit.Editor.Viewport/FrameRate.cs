namespace Orblit.Editor.Viewport;

/// <summary>
/// How fast the scene view draws, kept as two halves: the editor's own work on a frame and the
/// renderer's time on the graphics card. Which half is slow decides where to look, and a single
/// frames-a-second figure hides it.
/// </summary>
public sealed class FrameRate
{
    /// <summary>How many frames are averaged over. A second's worth at sixty, so the number is steady enough to read.</summary>
    public const int Window = 60;

    /// <summary>
    /// How often the reading changes. The chips over the view are rebuilt when it does, and rebuilding
    /// them sixty times a second to say how fast things are is its own answer.
    /// </summary>
    public static readonly TimeSpan Every = TimeSpan.FromMilliseconds(400);

    private readonly Queue<double> _spans = new();
    private double _build;
    private double _gpu;
    private TimeSpan? _said;

    /// <summary>The last reading, or null before the first frame.</summary>
    public FrameReading? Reading { get; private set; }

    /// <summary>One drawn frame: when it was drawn, and how long each half took.</summary>
    public void Took(TimeSpan now, TimeSpan build, TimeSpan gpu)
    {
        _spans.Enqueue(build.TotalMilliseconds + gpu.TotalMilliseconds);
        if (_spans.Count > Window)
        {
            _spans.Dequeue();
        }

        _build += (build.TotalMilliseconds - _build) / Window;
        _gpu += (gpu.TotalMilliseconds - _gpu) / Window;
        if (_said is { } said && now - said < Every)
        {
            return;
        }

        _said = now;
        var total = _spans.Sum();
        // From how long a frame takes, not how often one comes, so it is headroom. A still editor
        // draws nothing, and a rate that fell to zero then would say nothing about its speed.
        Reading = total <= 0 ? null : new FrameReading(_spans.Count * 1000 / total, _build, _gpu);
    }
}

/// <summary>Frames a second, and how long each half of an average frame takes, in milliseconds.</summary>
public readonly record struct FrameReading(double Fps, double BuildMs, double GpuMs)
{
    /// <summary>Whether the graphics card is the one holding things up.</summary>
    public bool GpuBound => GpuMs > BuildMs;
}
