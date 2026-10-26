namespace Orblit.Editor.Core;

using System.Diagnostics;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

/// <summary>
/// Every change the timeline makes to a clip, each a function from one clip to the next.
/// </summary>
/// <remarks>
/// Nothing here knows about widgets or the undo stack: a command holds the clip from before and the clip
/// from after, and these are how the second one is made.
/// </remarks>
public static class ClipEdits
{
    /// <summary>
    /// How close two moments have to be to be one frame. Far finer than any rate anybody animates at, and
    /// far coarser than what a double loses on its way through a file and back.
    /// </summary>
    public const double SameMoment = 1e-5;

    public static ChannelAddress AddressOf(ClipChannel channel) => new(channel.Target, channel.Bone, channel.Property);

    public static ClipChannel? ChannelAt(ClipDocument clip, ChannelAddress address) =>
        clip.ChannelFor(address.Target, address.Property, address.Bone);

    /// <summary>The key <paramref name="reference"/> names, or null when the clip no longer has it.</summary>
    public static Key<object>? KeyOf(ClipDocument clip, KeyRef reference)
    {
        var keys = ChannelAt(clip, reference.Channel)?.BoxedKeys;
        return keys is null || reference.Index < 0 || reference.Index >= keys.Count ? null : keys[reference.Index];
    }

    /// <summary>
    /// <paramref name="at"/> on the nearest of <paramref name="rate"/>'s frames. A half frame rounds away
    /// from zero, so a drag of the same size looks the same either way along the timeline.
    /// </summary>
    public static double Snapped(double at, double rate) =>
        rate <= 0 || !double.IsFinite(at) ? at : Math.Round(at * rate, MidpointRounding.AwayFromZero) / rate;

    /// <summary>
    /// The key of <paramref name="channel"/> on the frame at <paramref name="at"/>, or -1. The later of a cut,
    /// since that is the one the channel says from then on.
    /// </summary>
    public static int KeyAt(ClipChannel channel, double at)
    {
        var keys = channel.BoxedKeys;
        for (var i = keys.Count - 1; i >= 0; i--)
        {
            if (Math.Abs(keys[i].At - at) < SameMoment)
            {
                return i;
            }
        }

        return -1;
    }

    public static KeyMark MarkOf(ClipDocument clip, ChannelAddress address, double at)
    {
        if (ChannelAt(clip, address) is not { } channel)
        {
            return KeyMark.Unkeyed;
        }

        return KeyAt(channel, at) >= 0 ? KeyMark.Keyed : KeyMark.Animated;
    }

    /// <summary>
    /// The kind a field is keyed as, from how the scene writes it, or null when it is not something a key can
    /// hold.
    /// </summary>
    /// <remarks>
    /// Four numbers are not keyed as a turn: in a component they are as likely to be a colour with its alpha,
    /// and a colour mixed as a rotation is a colour nobody chose. A turn is keyed where a channel already says
    /// it carries one.
    /// </remarks>
    public static ChannelKind? KindOfField(object? raw) => raw switch
    {
        double or long => ChannelKind.Number,
        bool => ChannelKind.Flag,
        IReadOnlyList<object?> { Count: 3 } when ChannelKind.Vector.TryRead(raw, out _) => ChannelKind.Vector,
        _ => null,
    };

    /// <summary>
    /// <paramref name="raw"/>, a field as the scene writes it, as a key of <paramref name="kind"/>, or null
    /// when it cannot be one.
    /// </summary>
    /// <remarks>
    /// A transform's rotation is written in degrees, which is what a vector channel on it carries. A channel
    /// carrying a turn, as an imported one does, is keyed with the turn those degrees make.
    /// </remarks>
    public static object? KeyValueOf(ChannelKind kind, string property, object? raw)
    {
        if (kind == ChannelKind.Rotation && property == "transform.rotation")
        {
            return ChannelKind.Vector.TryRead(raw, out var degrees)
                ? Quaternion.FromRotation(Doc.TransformComponent.RotationOf(degrees))
                : null;
        }

        return kind switch
        {
            ChannelKind<double> number => Read(number, raw),
            ChannelKind<Vector3> vector => Read(vector, raw),
            ChannelKind<Quaternion> rotation => Read(rotation, raw),
            ChannelKind<bool> flag => Read(flag, raw),
            _ => null,
        };
    }

    /// <summary>
    /// <paramref name="clip"/> with a value keyed on <paramref name="address"/>.
    /// </summary>
    /// <remarks>
    /// A key already on that frame takes the value and keeps how it travels. A new key travels the way the
    /// key before it does, so keying the middle of an eased span leaves both halves eased. A channel that does
    /// not exist yet is made, of <paramref name="kind"/>. One that does keeps its own, and the value has to be
    /// one of those.
    /// </remarks>
    public static ClipDocument Keyed(ClipDocument clip, ChannelAddress address, ChannelKind kind, NewKey key)
    {
        if (ChannelAt(clip, address) is not { } channel)
        {
            var made = ClipChannel.OfKind(
                address.Target,
                address.Property,
                kind,
                [new Key<object>(key.At, key.Value)],
                address.Bone);
            return clip.CopyWith(channels: [.. clip.Channels, made]);
        }

        var keys = channel.BoxedKeys.ToList();
        var index = KeyAt(channel, key.At);
        if (index >= 0)
        {
            keys[index] = Changed(keys[index], value: key.Value);
        }
        else
        {
            var before = keys.LastOrDefault(one => one.At < key.At) ?? keys[0];
            keys.Add(new Key<object>(key.At, key.Value, before.Hold, before.Shape));
        }

        return Swapped(clip, channel, channel.Rekeyed(keys));
    }

    /// <summary>
    /// <paramref name="clip"/> without the keys <paramref name="refs"/> name. A channel left with none goes
    /// too: a channel has to have a key, and one with nothing to say should not be in the file.
    /// </summary>
    public static ClipDocument WithoutKeys(ClipDocument clip, IReadOnlySet<KeyRef> refs) =>
        Rewritten(clip, refs, (channel, going) =>
        {
            var keys = channel.BoxedKeys.Where((_, index) => !going.Contains(index)).ToList();
            return keys.Count == 0 ? null : channel.Rekeyed(keys);
        });

    /// <summary>
    /// <paramref name="clip"/> with the keys <paramref name="refs"/> name moved <paramref name="by"/> seconds,
    /// and where they went.
    /// </summary>
    /// <remarks>
    /// Worked out from the clip as the drag began, every time the pointer moves, so a key dragged over another
    /// and back again leaves the other where it was. The move is in whole frames and stops at either end of the
    /// clip. A key landing on a frame another key of its channel is on replaces it, which is what dropping one
    /// key on another means.
    /// </remarks>
    public static KeysMoved MovedKeys(ClipDocument clip, IReadOnlySet<KeyRef> refs, double by)
    {
        var first = double.PositiveInfinity;
        var last = double.NegativeInfinity;
        var present = new HashSet<KeyRef>();
        foreach (var reference in refs)
        {
            if (KeyOf(clip, reference) is not { } key)
            {
                continue;
            }

            present.Add(reference);
            first = Math.Min(first, key.At);
            last = Math.Max(last, key.At);
        }

        if (present.Count == 0)
        {
            return new KeysMoved(clip, new HashSet<KeyRef>());
        }

        var delta = Math.Max(Math.Min(Snapped(by, clip.Rate), Math.Max(clip.Duration - last, 0)), -first);
        if (Math.Abs(delta) < SameMoment)
        {
            return new KeysMoved(clip, present);
        }

        var moved = new HashSet<KeyRef>();
        var shifted = Rewritten(clip, present, (channel, going) =>
        {
            var (made, landed) = Shifted(channel, going, delta);
            moved.UnionWith(landed.Select(index => new KeyRef(AddressOf(channel), index)));
            return made;
        });
        return new KeysMoved(shifted, moved);
    }

    /// <summary><paramref name="clip"/> with the key <paramref name="reference"/> names holding <paramref name="value"/> instead.</summary>
    public static ClipDocument WithKeyValue(ClipDocument clip, KeyRef reference, object value)
    {
        if (ChannelAt(clip, reference.Channel) is not { } channel || KeyOf(clip, reference) is null)
        {
            return clip;
        }

        var keys = channel.BoxedKeys.ToList();
        keys[reference.Index] = Changed(keys[reference.Index], value: value);
        return Swapped(clip, channel, channel.Rekeyed(keys));
    }

    /// <summary>
    /// <paramref name="clip"/> with every key <paramref name="refs"/> names travelling to the next by
    /// <paramref name="hold"/>.
    /// </summary>
    /// <remarks>
    /// A shaped hold with no <paramref name="shape"/> eases in and out, which is what smooth does, so choosing
    /// it changes nothing until a shape is chosen too.
    /// </remarks>
    public static ClipDocument WithHold(ClipDocument clip, IReadOnlySet<KeyRef> refs, Hold hold, Easing? shape = null) =>
        Rewritten(clip, refs, (channel, going) => Remade(
            channel,
            going,
            key => Changed(key, hold: hold, shape: hold == Hold.Shaped ? shape ?? Easing.InOut : key.Shape)));

    /// <summary>
    /// How many numbers a key of <paramref name="kind"/> is drawn as on a curve: one for a number, three for a
    /// vector, and none for a turn or a flag.
    /// </summary>
    /// <remarks>
    /// A turn's four numbers are not four things anybody can reason about one at a time, and a flag has no
    /// slope. Both are edited as keys, not curves.
    /// </remarks>
    public static int CurvesOf(ChannelKind kind) => kind switch
    {
        ChannelKind<double> => 1,
        ChannelKind<Vector3> => 3,
        _ => 0,
    };

    /// <summary>One of the numbers <paramref name="value"/> is drawn as.</summary>
    public static double PartOf(object value, int part) => value switch
    {
        double number => number,
        Vector3 vector => vector[part],
        _ => 0,
    };

    /// <summary><paramref name="value"/> with one of its numbers changed.</summary>
    public static object WithPart(object value, int part, double to)
    {
        if (value is not Vector3 vector)
        {
            return value is double ? to : value;
        }

        var copy = vector.Clone();
        copy[part] = to;
        return copy;
    }

    /// <summary>
    /// <paramref name="clip"/> with a slope of the key <paramref name="reference"/> names set, from dragging a
    /// handle.
    /// </summary>
    /// <remarks>
    /// The span the handle belongs to becomes a curve, since a handle on a span that is not one would move
    /// nothing anybody could see.
    /// </remarks>
    public static ClipDocument WithSlope(ClipDocument clip, KeyRef reference, SlopeDrag drag)
    {
        if (ChannelAt(clip, reference.Channel) is not { } channel
            || KeyOf(clip, reference) is not { } key
            || CurvesOf(channel.Kind) == 0)
        {
            return clip;
        }

        var index = reference.Index;
        var keys = channel.BoxedKeys.ToList();
        keys[index] = Dragged(key, WorkedOutSlope(channel, index), drag);
        if (drag.Arrives && index > 0)
        {
            keys[index - 1] = Changed(keys[index - 1], hold: Hold.Curve);
        }

        return Swapped(clip, channel, channel.Rekeyed(keys));
    }

    /// <summary>
    /// <paramref name="clip"/> with the slopes of every key <paramref name="refs"/> names worked out again from
    /// their neighbours, the way a key nobody has dragged a handle of is.
    /// </summary>
    public static ClipDocument WithAutoSlopes(ClipDocument clip, IReadOnlySet<KeyRef> refs) =>
        Rewritten(clip, refs, (channel, going) => Remade(
            channel,
            going,
            key => new Key<object>(key.At, key.Value, key.Hold, key.Shape)));

    private static object? Read<T>(ChannelKind<T> kind, object? raw)
        where T : notnull =>
        kind.TryRead(raw, out var value) ? value : null;

    // Slopes are only asked for on a channel CurvesOf has already said has some.
    private static object WorkedOutSlope(ClipChannel channel, int index) => channel switch
    {
        ClipChannel<double> number => number.Curve.SlopeAt(index),
        ClipChannel<Vector3> vector => vector.Curve.SlopeAt(index),
        _ => throw new UnreachableException(),
    };

    private static Key<object> Dragged(Key<object> key, object worked, SlopeDrag drag)
    {
        var slope = new Slope<object>(WithPart(
            (drag.Side == KeySide.Arriving ? key.SlopeIn : key.SlopeOut)?.Value ?? worked,
            drag.Part,
            drag.Slope));
        return new Key<object>(
            key.At,
            key.Value,
            drag.Leaves ? Hold.Curve : key.Hold,
            key.Shape,
            drag.Arrives ? slope : key.SlopeIn,
            drag.Leaves ? slope : key.SlopeOut);
    }

    // The places in channel of the keys refs names there.
    private static HashSet<int> IndicesIn(ClipChannel channel, IReadOnlySet<KeyRef> refs)
    {
        var address = AddressOf(channel);
        return
        [
            .. refs
                .Where(reference => reference.Channel == address && reference.Index >= 0 && reference.Index < channel.BoxedKeys.Count)
                .Select(reference => reference.Index),
        ];
    }

    // F2: five parameters, all but the first optional and called by name.
    private static Key<object> Changed(
        Key<object> key,
        double? at = null,
        object? value = null,
        Hold? hold = null,
        Easing? shape = null) => new(
        at ?? key.At,
        value ?? key.Value,
        hold ?? key.Hold,
        shape ?? key.Shape,
        key.SlopeIn,
        key.SlopeOut);

    private static ClipDocument Swapped(ClipDocument clip, ClipChannel was, ClipChannel now) =>
        clip.CopyWith(channels: [.. clip.Channels.Select(channel => ReferenceEquals(channel, was) ? now : channel)]);

    // Each channel refs reach is made again by remake, which drops it by answering null. The rest stay.
    private static ClipDocument Rewritten(
        ClipDocument clip,
        IReadOnlySet<KeyRef> refs,
        Func<ClipChannel, HashSet<int>, ClipChannel?> remake)
    {
        var channels = new List<ClipChannel>();
        foreach (var channel in clip.Channels)
        {
            var reached = IndicesIn(channel, refs);
            if ((reached.Count == 0 ? channel : remake(channel, reached)) is { } made)
            {
                channels.Add(made);
            }
        }

        return clip.CopyWith(channels: channels);
    }

    private static ClipChannel Remade(ClipChannel channel, HashSet<int> going, Func<Key<object>, Key<object>> change) =>
        channel.Rekeyed(channel.BoxedKeys.Select((key, index) => going.Contains(index) ? change(key) : key));

    // The channel with the keys at going moved by delta, and the places those keys ended up in.
    private static (ClipChannel Channel, List<int> Landed) Shifted(ClipChannel channel, HashSet<int> going, double delta)
    {
        var keys = channel.BoxedKeys;
        var arriving = going.Order().Select(index => Changed(keys[index], at: keys[index].At + delta)).ToList();
        var entries = keys
            .Where((key, index) => !going.Contains(index) && !arriving.Any(one => Math.Abs(one.At - key.At) < SameMoment))
            .Select(key => (Key: key, Moved: false))
            .Concat(arriving.Select(key => (Key: key, Moved: true)))
            .ToList();

        // By time, and by place among equal times, so a cut stays the way round it was. The channel sorts the
        // same way, which is what makes the places worked out here the places it ends up with.
        var order = Enumerable.Range(0, entries.Count).OrderBy(entry => entries[entry].Key.At).ToList();
        var landed = Enumerable.Range(0, order.Count).Where(place => entries[order[place]].Moved).ToList();
        return (channel.Rekeyed(order.Select(entry => entries[entry].Key)), landed);
    }
}
