namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;
using Orblit.Motion;

/// <summary>A point of the curve view, in pixels.</summary>
public readonly record struct Spot(double X, double Y)
{
    public double Distance(Spot other) => Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
}

/// <summary>
/// What a press on a curve took: a key's number, or one of its handles. A handle names the side it is on.
/// </summary>
public readonly record struct Grip(int Index, int Part, KeySide? Side);

/// <summary>Where a channel's curve is drawn: time across, value up.</summary>
/// <param name="Channel">The channel drawn.</param>
/// <param name="Axis">How time is spread across the width.</param>
/// <param name="Low">The value at the bottom, less the padding.</param>
/// <param name="High">The value at the top, less the padding.</param>
/// <param name="Height">How tall the view is.</param>
public sealed record CurveFrame(ClipChannel Channel, TimeAxis Axis, double Low, double High, double Height)
{
    /// <summary>Room above and below, so a value at either end is not cut in half.</summary>
    public const double Pad = 14;

    public double PerUnit => (Height - Pad * 2) / (High - Low);

    public double YOf(double value) => Pad + (High - value) * PerUnit;

    public double ValueOf(double y) => High - (y - Pad) / PerUnit;

    public Spot At(double time, double value) => new(Axis.XOf(time), YOf(value));
}

/// <summary>The lines behind a curve: a line at every round value, and what is written against it.</summary>
public static class CurveGrid
{
    /// <summary>The round number nearest above <paramref name="rough"/>: 1, 2 or 5 of a power of ten. Zero when there is none.</summary>
    public static double NiceStep(double rough)
    {
        if (rough <= 0 || !double.IsFinite(rough))
        {
            return 0;
        }

        var power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        foreach (var times in new[] { 1, 2, 5, 10 })
        {
            if (times * power >= rough)
            {
                return times * power;
            }
        }

        return 10 * power;
    }

    /// <summary>Every multiple of <paramref name="step"/> from <paramref name="low"/> to <paramref name="high"/>.</summary>
    public static IEnumerable<double> Lines(double low, double high, double step)
    {
        for (var count = (long)Math.Ceiling(low / step); count * step <= high; count++)
        {
            yield return count * step;
        }
    }

    /// <summary><paramref name="value"/> written with as many decimals as <paramref name="step"/> needs.</summary>
    public static string Label(double value, double step)
    {
        var decimals = step >= 1 ? 0 : (int)Math.Ceiling(-Math.Log10(step));
        var text = value.ToString("F" + Math.Clamp(decimals, 0, 6), System.Globalization.CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }
}

/// <summary>
/// What a press, a drag or a tap on a channel's curve does: pick a key, move it in time and in value, or bend
/// the curve by one of the handles of a picked key. Positions are in pixels on the view, so the model needs no
/// window.
/// </summary>
/// <remarks>
/// The two handles of a key turn together unless broken, which makes a corner. The values the view spans are
/// held still while something is dragged, so the curve does not rescale under the pointer.
/// </remarks>
public sealed class CurveModel(ClipBench bench)
{
    /// <summary>How close in pixels a press has to be to a key or a handle to take it.</summary>
    public const double Reach = 6;

    /// <summary>How far out from its key a handle is drawn, whatever its slope.</summary>
    public const double HandleLength = 40;

    private const int Samples = 64;

    private static readonly IReadOnlySet<KeyRef> None = new HashSet<KeyRef>();

    private (double Low, double High)? _held;
    private CurveDrag? _drag;

    private sealed record CurveDrag(ClipDocument Start, Grip Grip, IReadOnlySet<KeyRef> Keys, Spot From, object Gesture, bool Moved);

    /// <summary>The channel the bench says to draw, or null.</summary>
    public ClipChannel? Channel =>
        bench.Clip is { } clip && bench.Curve is { } address ? ClipEdits.ChannelAt(clip, address) : null;

    /// <summary>What to say in place of a curve, or null when there is one to draw.</summary>
    public string? Note => Channel switch
    {
        null => "Choose a number or a position on the left to draw its curve.",
        { } channel when ClipEdits.CurvesOf(channel.Kind) == 0 => "A turn or a flag has no curve to draw. Its keys are edited on the sheet.",
        _ => null,
    };

    /// <summary>How the curve fits a view of this size, or null when there is none to draw.</summary>
    public CurveFrame? FrameOf(double width, double height)
    {
        if (Note is not null || bench.Clip is not { } clip || Channel is not { } channel)
        {
            return null;
        }

        var axis = new TimeAxis(clip, width);
        var (low, high) = _held ?? RangeOf(channel, axis);
        return new CurveFrame(channel, axis, low, high, height);
    }

    /// <summary>The places of the picked keys of the frame's channel.</summary>
    public IReadOnlyList<int> Picked(CurveFrame frame)
    {
        var address = ClipEdits.AddressOf(frame.Channel);
        return
        [
            .. bench.Selection
                .Where(reference => reference.Channel == address && reference.Index < frame.Channel.BoxedKeys.Count)
                .Select(reference => reference.Index)
                .Order(),
        ];
    }

    /// <summary>Where the end of a key's handle is drawn: a fixed way out along its slope.</summary>
    public static Spot Handle(CurveFrame frame, int index, int part, KeySide side)
    {
        var key = frame.Channel.BoxedKeys[index];
        var arriving = side == KeySide.Arriving;
        var slope = ClipEdits.PartOf((arriving ? key.SlopeIn : key.SlopeOut)?.Value ?? ClipEdits.WorkedOutSlope(frame.Channel, index), part);
        var origin = frame.At(key.At, ClipEdits.PartOf(key.Value, part));

        // One second along the slope, on screen, made the length of a handle.
        var alongX = frame.Axis.PerSecond;
        var alongY = -slope * frame.PerUnit;
        var length = Math.Sqrt(alongX * alongX + alongY * alongY);
        var (unitX, unitY) = length == 0 ? (1.0, 0.0) : (alongX / length, alongY / length);
        var reach = arriving ? -HandleLength : HandleLength;
        return new Spot(origin.X + unitX * reach, origin.Y + unitY * reach);
    }

    /// <summary>What is under a point: a handle of a picked key first, since they sit over curves, then a key.</summary>
    public Grip? Hit(CurveFrame frame, Spot at) => Nearest(PickedHandles(frame), at) ?? Nearest(KeysOf(frame), at);

    /// <summary>A press and release in one place. Shift adds a key to the picked ones, or takes it off if it was picked.</summary>
    public void Tap(CurveFrame frame, Spot at, bool shift)
    {
        if (Hit(frame, at) is not { } grip)
        {
            if (!shift)
            {
                bench.Selection = None;
            }

            return;
        }

        if (grip.Side is not null)
        {
            return;
        }

        var reference = new KeyRef(ClipEdits.AddressOf(frame.Channel), grip.Index);
        bench.Selection = !shift ? new HashSet<KeyRef> { reference }
            : bench.Selection.Contains(reference) ? bench.Selection.Except([reference]).ToHashSet()
            : bench.Selection.Union([reference]).ToHashSet();
    }

    /// <summary>Begins a drag on a key or a handle. A drag that starts on nothing does nothing.</summary>
    public void Start(CurveFrame frame, Spot at, bool shift)
    {
        if (bench.Clip is not { } clip || Hit(frame, at) is not { } grip)
        {
            return;
        }

        var reference = new KeyRef(ClipEdits.AddressOf(frame.Channel), grip.Index);
        if (grip.Side is null && !bench.Selection.Contains(reference))
        {
            bench.Selection = shift ? bench.Selection.Union([reference]).ToHashSet() : new HashSet<KeyRef> { reference };
        }

        _held = (frame.Low, frame.High);
        _drag = new CurveDrag(clip, grip, bench.Selection, at, new object(), Moved: false);
    }

    /// <summary>
    /// Carries on the drag to <paramref name="at"/>. Shift keeps a key's drag to whichever way it went furthest,
    /// and Alt breaks the handles of a key so they turn apart.
    /// </summary>
    public void Update(CurveFrame frame, Spot at, bool shift, bool alt)
    {
        // A view too short to hold a value, or a clip with no length, has no scale to read a drag by. Written
        // to be false for NaN, which compares false either way round.
        if (_drag is not { } drag || frame.Axis.PerSecond <= 0 || !(frame.PerUnit > 0))
        {
            return;
        }

        if (drag.Grip.Side is { } side)
        {
            Bend(drag, frame, at, side, alt);
        }
        else
        {
            Carry(drag, frame, at, shift);
        }
    }

    /// <summary>Ends the drag. A drag that changed the clip is one step to undo.</summary>
    public void End()
    {
        if (_drag is { Moved: true })
        {
            bench.Seal();
        }

        _drag = null;
        _held = null;
    }

    private static (double Low, double High) RangeOf(ClipChannel channel, TimeAxis axis)
    {
        var parts = ClipEdits.CurvesOf(channel.Kind);
        var values = channel.BoxedKeys.Select(key => key.Value)
            .Concat(Enumerable.Range(0, Samples + 1).Select(step => channel.ValueAt(axis.Span * step / Samples)));
        var numbers = values.SelectMany(value => Enumerable.Range(0, parts).Select(part => ClipEdits.PartOf(value, part))).ToList();
        var low = numbers.Min();
        var high = numbers.Max();
        if (!double.IsFinite(low) || !double.IsFinite(high))
        {
            return (-1, 1);
        }

        var spread = high - low;
        if (spread < 1e-6)
        {
            spread = Math.Max(Math.Abs(low) * 0.2, 1);
            low -= spread / 2;
            high += spread / 2;
        }

        return (low - spread * 0.1, high + spread * 0.1);
    }

    /// <summary>The ends of the handles of the picked keys, each with what a press on it takes.</summary>
    public IEnumerable<(Spot Point, Grip Grip)> PickedHandles(CurveFrame frame)
    {
        var last = frame.Channel.BoxedKeys.Count - 1;
        foreach (var index in Picked(frame))
        {
            for (var part = 0; part < ClipEdits.CurvesOf(frame.Channel.Kind); part++)
            {
                if (index > 0)
                {
                    yield return (Handle(frame, index, part, KeySide.Arriving), new Grip(index, part, KeySide.Arriving));
                }

                if (index < last)
                {
                    yield return (Handle(frame, index, part, KeySide.Leaving), new Grip(index, part, KeySide.Leaving));
                }
            }
        }
    }

    private static IEnumerable<(Spot Point, Grip Grip)> KeysOf(CurveFrame frame)
    {
        for (var index = 0; index < frame.Channel.BoxedKeys.Count; index++)
        {
            for (var part = 0; part < ClipEdits.CurvesOf(frame.Channel.Kind); part++)
            {
                var key = frame.Channel.BoxedKeys[index];
                yield return (frame.At(key.At, ClipEdits.PartOf(key.Value, part)), new Grip(index, part, null));
            }
        }
    }

    // The nearest within reach. Of those equally near, the last.
    private static Grip? Nearest(IEnumerable<(Spot Point, Grip Grip)> candidates, Spot at)
    {
        var best = Reach;
        Grip? found = null;
        foreach (var (point, grip) in candidates)
        {
            var distance = point.Distance(at);
            if (distance <= best)
            {
                best = distance;
                found = grip;
            }
        }

        return found;
    }

    // A handle is dragged to where the pointer is, which sets the slope the curve has through its key.
    private void Bend(CurveDrag drag, CurveFrame frame, Spot at, KeySide side, bool alt)
    {
        var reference = new KeyRef(ClipEdits.AddressOf(frame.Channel), drag.Grip.Index);
        if (ClipEdits.KeyOf(drag.Start, reference) is not { } key)
        {
            return;
        }

        // A handle cannot cross over its key: past it, a slope would point the curve back in time.
        var least = 2 / frame.Axis.PerSecond;
        var seconds = frame.Axis.AtOf(at.X) - key.At;
        seconds = side == KeySide.Arriving ? Math.Min(seconds, -least) : Math.Max(seconds, least);
        var rise = frame.ValueOf(at.Y) - ClipEdits.PartOf(key.Value, drag.Grip.Part);
        var bent = ClipEdits.WithSlope(drag.Start, reference, new SlopeDrag(side, drag.Grip.Part, rise / seconds, alt));
        Push(drag, "Bend curve", bent, drag.Keys);
    }

    // The keys follow the pointer in time, and in value those of the channel on view.
    private void Carry(CurveDrag drag, CurveFrame frame, Spot at, bool shift)
    {
        var across = at.X - drag.From.X;
        var up = at.Y - drag.From.Y;
        if (shift)
        {
            (across, up) = Math.Abs(across) >= Math.Abs(up) ? (across, 0.0) : (0.0, up);
        }

        var valued = Raised(drag, ClipEdits.AddressOf(frame.Channel), -up / frame.PerUnit);
        var moved = ClipEdits.MovedKeys(valued, drag.Keys, across / frame.Axis.PerSecond);
        Push(drag, "Move keys", moved.Clip, moved.Keys);
    }

    // The clip from before the drag, with the picked keys of one channel raised by rise.
    private static ClipDocument Raised(CurveDrag drag, ChannelAddress address, double rise)
    {
        var valued = drag.Start;
        if (rise == 0)
        {
            return valued;
        }

        foreach (var reference in drag.Keys.Where(one => one.Channel == address))
        {
            if (ClipEdits.KeyOf(drag.Start, reference) is { } key)
            {
                var part = drag.Grip.Part;
                valued = ClipEdits.WithKeyValue(valued, reference, ClipEdits.WithPart(key.Value, part, ClipEdits.PartOf(key.Value, part) + rise));
            }
        }

        return valued;
    }

    private void Push(CurveDrag drag, string label, ClipDocument to, IReadOnlySet<KeyRef> keys)
    {
        // Still where it began, and so nothing to undo yet.
        if (ReferenceEquals(to, drag.Start) && !drag.Moved)
        {
            return;
        }

        _drag = drag with { Moved = true };
        bench.Edit(label, _ => to, new EditOptions(keys, drag.Gesture, OnlyMoves: true));
    }
}
