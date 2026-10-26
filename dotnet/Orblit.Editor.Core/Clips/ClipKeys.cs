namespace Orblit.Editor.Core;

using Orblit.Motion;

/// <summary>What a channel moves, which is what a clip has one channel for.</summary>
public readonly record struct ChannelAddress(string Target, string? Bone, string Property);

/// <summary>
/// One key: the channel it is in, and where in that channel's keys.
/// </summary>
/// <remarks>
/// By place rather than by time, because two keys at one moment are a cut and a time cannot tell them
/// apart. Every edit that moves keys about says where they went, so a selection follows its keys.
/// </remarks>
public readonly record struct KeyRef(ChannelAddress Channel, int Index);

/// <summary>Keys after a move, and the clip they were moved in.</summary>
public sealed record KeysMoved(ClipDocument Clip, IReadOnlySet<KeyRef> Keys);

/// <summary>A value to key at a moment, which is all a new key is told. How it travels comes from its neighbours.</summary>
public readonly record struct NewKey(double At, object Value);

/// <summary>A curve handle dragged: which side of the key, which of a vector's numbers, and the slope it was dragged to.</summary>
/// <remarks>
/// Aligned unless broken: both handles turn together, so the curve passes through the key smoothly, and
/// breaking them is what makes a corner.
/// </remarks>
public readonly record struct SlopeDrag(KeySide Side, int Part, double Slope, bool Broken = false)
{
    /// <summary>Whether the slope arriving at the key is set.</summary>
    public bool Arrives => Side == KeySide.Arriving || !Broken;

    /// <summary>Whether the slope leaving the key is set.</summary>
    public bool Leaves => Side == KeySide.Leaving || !Broken;
}

/// <summary>How a property looks from a clip, at the moment the playhead is on.</summary>
public enum KeyMark
{
    /// <summary>It cannot be keyed here: no clip is open, or it belongs to something the clip cannot name.</summary>
    None,

    /// <summary>It could be, and has no channel yet.</summary>
    Unkeyed,

    /// <summary>The clip moves it, with no key on this frame.</summary>
    Animated,

    /// <summary>There is a key on this frame.</summary>
    Keyed,
}

/// <summary>Which side of a key a curve handle is on.</summary>
public enum KeySide
{
    Arriving,
    Leaving,
}
