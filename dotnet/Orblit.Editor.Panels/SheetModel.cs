namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;

/// <summary>A rectangle dragged out over the sheet to pick the keys inside it.</summary>
public readonly record struct SheetBox(double Left, double Top, double Right, double Bottom)
{
    public static SheetBox Between(double x1, double y1, double x2, double y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));
}

/// <summary>
/// What a press, a drag or a tap on the sheet of keys does: pick keys, pick them with a box, or move the
/// ones picked. Positions are in pixels on the sheet, so the model needs no window.
/// </summary>
/// <remarks>
/// A row's keys are its own channel's. A heading's are every channel of its thing, picked together by the
/// moment they are on, so one press on a heading picks everything keyed there.
/// </remarks>
/// <param name="bench">The clip and its selection.</param>
/// <param name="rowHeight">How tall a row of the sheet is, which is how a height finds its row.</param>
public sealed class SheetModel(ClipBench bench, double rowHeight)
{
    /// <summary>How far in pixels a press can be from a key and still be on it.</summary>
    public const double Reach = 7;

    private static readonly IReadOnlySet<KeyRef> None = new HashSet<KeyRef>();

    private BoxPick? _box;
    private KeyDrag? _drag;

    private sealed record BoxPick(double FromX, double FromY, IReadOnlySet<KeyRef> Before, SheetBox Now);

    private sealed record KeyDrag(ClipDocument Start, IReadOnlySet<KeyRef> Keys, double From, object Gesture, bool Moved);

    /// <summary>The box being dragged out, or null when none is.</summary>
    public SheetBox? Box => _box?.Now;

    /// <summary>The keys at a point of the sheet: the nearest within reach, and any others on its moment in the same row.</summary>
    public IReadOnlySet<KeyRef> Hit(TimeAxis axis, double x, double y)
    {
        if (bench.Clip is not { } clip)
        {
            return None;
        }

        var rows = TimelineRow.Of(clip);
        var index = (int)Math.Floor(y / rowHeight);
        if (index < 0 || index >= rows.Count)
        {
            return None;
        }

        var channels = ChannelsOf(rows, rows[index]);
        var best = Reach;
        double? time = null;
        foreach (var key in channels.SelectMany(channel => channel.BoxedKeys))
        {
            var gap = Math.Abs(axis.XOf(key.At) - x);
            if (gap <= best)
            {
                best = gap;
                time = key.At;
            }
        }

        return time is { } moment ? KeysAt(channels, moment) : None;
    }

    /// <summary>The keys of every row the box reaches that stand within its sides.</summary>
    public IReadOnlySet<KeyRef> Boxed(TimeAxis axis, SheetBox box)
    {
        var picked = new HashSet<KeyRef>();
        if (bench.Clip is not { } clip)
        {
            return picked;
        }

        var rows = TimelineRow.Of(clip);
        for (var index = 0; index < rows.Count; index++)
        {
            var top = index * rowHeight;
            if (top + rowHeight < box.Top || top > box.Bottom)
            {
                continue;
            }

            foreach (var channel in ChannelsOf(rows, rows[index]))
            {
                picked.UnionWith(KeysWhere(channel, key => axis.XOf(key.At) >= box.Left && axis.XOf(key.At) <= box.Right));
            }
        }

        return picked;
    }

    /// <summary>A press and release in one place. Shift adds to the picked keys, or takes them off if they were all picked.</summary>
    public void Tap(TimeAxis axis, double x, double y, bool shift)
    {
        var hit = Hit(axis, x, y);
        if (hit.Count == 0)
        {
            if (!shift)
            {
                bench.Selection = None;
            }

            return;
        }

        if (!shift)
        {
            bench.Selection = hit;
        }
        else if (bench.Selection.IsSupersetOf(hit))
        {
            bench.Selection = bench.Selection.Except(hit).ToHashSet();
        }
        else
        {
            bench.Selection = bench.Selection.Union(hit).ToHashSet();
        }

        if (hit.Count == 1)
        {
            bench.Curve = hit.Single().Channel;
        }
    }

    /// <summary>Begins a drag: a box when it starts on nothing, otherwise a move of the picked keys.</summary>
    public void Start(TimeAxis axis, double x, double y, bool shift)
    {
        if (bench.Clip is not { } clip)
        {
            return;
        }

        var hit = Hit(axis, x, y);
        if (hit.Count == 0)
        {
            _box = new BoxPick(x, y, shift ? bench.Selection : None, SheetBox.Between(x, y, x, y));
            return;
        }

        if (!bench.Selection.IsSupersetOf(hit))
        {
            bench.Selection = shift ? bench.Selection.Union(hit).ToHashSet() : hit;
        }

        _drag = new KeyDrag(clip, bench.Selection, x, new object(), Moved: false);
    }

    /// <summary>Carries on the drag to <paramref name="x"/>, <paramref name="y"/>.</summary>
    public void Update(TimeAxis axis, double x, double y)
    {
        if (_box is { } box)
        {
            var now = SheetBox.Between(box.FromX, box.FromY, x, y);
            _box = box with { Now = now };
            bench.Selection = box.Before.Union(Boxed(axis, now)).ToHashSet();
            return;
        }

        if (_drag is { } drag && axis.PerSecond != 0)
        {
            MoveKeys(drag, ClipEdits.MovedKeys(drag.Start, drag.Keys, (x - drag.From) / axis.PerSecond));
        }
    }

    /// <summary>Ends the drag. A drag that moved keys is one step to undo.</summary>
    public void End()
    {
        if (_drag is { Moved: true })
        {
            bench.Seal();
        }

        _box = null;
        _drag = null;
    }

    private void MoveKeys(KeyDrag drag, KeysMoved moved)
    {
        // Still on the frame it began on, and so nothing to undo yet.
        var back = ReferenceEquals(moved.Clip, drag.Start);
        if (back && !drag.Moved)
        {
            return;
        }

        _drag = drag with { Moved = true };
        bench.Edit(
            "Move keys",
            _ => moved.Clip,
            new EditOptions(back ? drag.Keys : moved.Keys, drag.Gesture, OnlyMoves: true));
    }

    private static List<ClipChannel> ChannelsOf(IReadOnlyList<TimelineRow> rows, TimelineRow row) =>
        [.. rows.Where(one => one.Target == row.Target && (row.IsHeading || ReferenceEquals(one, row))).Select(one => one.Channel).OfType<ClipChannel>()];

    private static HashSet<KeyRef> KeysAt(List<ClipChannel> channels, double time) =>
        [.. channels.SelectMany(channel => KeysWhere(channel, key => Math.Abs(key.At - time) < ClipEdits.SameMoment))];

    private static IEnumerable<KeyRef> KeysWhere(ClipChannel channel, Func<Key<object>, bool> test)
    {
        var address = ClipEdits.AddressOf(channel);
        for (var place = 0; place < channel.BoxedKeys.Count; place++)
        {
            if (test(channel.BoxedKeys[place]))
            {
                yield return new KeyRef(address, place);
            }
        }
    }
}
