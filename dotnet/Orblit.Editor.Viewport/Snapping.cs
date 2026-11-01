namespace Orblit.Editor.Viewport;

using System.Diagnostics;
using Orblit.VectorMath;

/// <summary>
/// Which part of a thing is put on the line.
///
/// An object's own origin is rarely where somebody wants it measured from. A plane made in the
/// editor has its origin in the middle, so snapping it to the floor leaves it half above and half
/// below. That is exactly right for a wall through a floor and exactly wrong for a floor.
/// </summary>
public enum SnapTo
{
    /// <summary>The object's own origin, wherever the person who made it put it.</summary>
    Pivot,

    /// <summary>
    /// The bottom of it, along whichever way it is being dragged. What puts a floor on the ground
    /// rather than through it.
    /// </summary>
    Base,

    /// <summary>The middle of it.</summary>
    Centre,

    /// <summary>The top of it, for hanging something from a ceiling.</summary>
    Top,
}

public static class SnapTos
{
    public static string Label(this SnapTo to) => to switch
    {
        SnapTo.Pivot => "Origin",
        SnapTo.Base => "Base",
        SnapTo.Centre => "Middle",
        SnapTo.Top => "Top",
        _ => throw new UnreachableException(),
    };
}

/// <summary>
/// What a drag lands on.
///
/// Blocking out a level is mostly making things line up, and lining things up by eye at a hundred
/// places is where the time goes. Everything here is one idea: a drag reports where the pointer
/// is, and this decides which of the nearby round numbers that means.
/// </summary>
/// <param name="on">
/// Whether it applies at all. Held down rather than switched is the other way round: the key
/// suspends it, because somebody who wants a shelf half a millimetre off wants it for one drag
/// and not for the afternoon.
/// </param>
/// <param name="step">How far apart the grid lines are, in metres.</param>
/// <param name="angle">How many degrees a turn lands on.</param>
/// <param name="to">
/// Which part of the thing being dragged is put on the line. Its base by default. Almost
/// everything anybody drags is something that stands on something else, and a shape whose origin
/// is in the middle otherwise sinks half of itself into the floor the moment it snaps.
/// </param>
public sealed class Snapping(bool on = true, double step = 0.5, double angle = 15, SnapTo to = SnapTo.Base)
{
    private static readonly double[] StepValues = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1.0, 2.0];

    private bool _on = on;
    private double _step = step;
    private double _angle = angle;
    private SnapTo _to = to;

    /// <summary>
    /// Said when any of the four changes, so the panel that sets them and the view that draws the
    /// grid agree without either knowing the other.
    /// </summary>
    public event Action? Changed;

    /// <summary>Whether it applies at all.</summary>
    public bool On
    {
        get => _on;
        set => Set(ref _on, value);
    }

    /// <summary>How far apart the grid lines are, in metres.</summary>
    public double Step
    {
        get => _step;
        set => Set(ref _step, value);
    }

    /// <summary>How many degrees a turn lands on.</summary>
    public double Angle
    {
        get => _angle;
        set => Set(ref _angle, value);
    }

    /// <summary>Which part of the thing being dragged is put on the line.</summary>
    public SnapTo To
    {
        get => _to;
        set => Set(ref _to, value);
    }

    /// <summary>
    /// The steps offered: a millimetre up to two metres.
    ///
    /// Every one of them divides the next exactly, which is what makes changing the grid part-way
    /// through safe: everything placed on a fine grid is still on the coarse one. That property
    /// is why a quarter of a metre is not on the list: it would sit between a tenth and a half
    /// and divide neither.
    /// </summary>
    public static IReadOnlyList<double> Steps => StepValues;

    /// <summary>The next step up, stopping at the largest, for the key that cycles them.</summary>
    public double Coarser
    {
        get
        {
            var at = Array.IndexOf(StepValues, Step);
            return at < 0 || at == StepValues.Length - 1 ? StepValues[^1] : StepValues[at + 1];
        }
    }

    public double Finer
    {
        get
        {
            var at = Array.IndexOf(StepValues, Step);
            return at <= 0 ? StepValues[0] : StepValues[at - 1];
        }
    }

    /// <summary>
    /// A distance along an axis, rounded to the grid.
    ///
    /// The position rather than the movement, so things land on the grid rather than a round
    /// distance from wherever they happened to start. Somebody dragging two walls together wants
    /// them to meet, and two walls each moved exactly a quarter of a metre from two different
    /// places do not.
    /// </summary>
    public double Place(double at)
    {
        if (!On || Step <= 0)
        {
            return at;
        }

        return Math.Round(at / Step, MidpointRounding.AwayFromZero) * Step;
    }

    /// <summary>A whole position, each axis on the grid.</summary>
    public Vector3 PlaceAll(Vector3 at) =>
        On ? new Vector3(Place(at.X), Place(at.Y), Place(at.Z)) : at;

    /// <summary>A turn, in radians, rounded to <see cref="Angle"/> degrees.</summary>
    public double Turn(double radians)
    {
        if (!On || Angle <= 0)
        {
            return radians;
        }

        var degrees = radians * 180 / Math.PI;
        var landed = Math.Round(degrees / Angle, MidpointRounding.AwayFromZero) * Angle;
        return landed * Math.PI / 180;
    }

    /// <summary>
    /// How much to move something so that <paramref name="from"/> plus the move lands on the
    /// grid, given a pointer that has asked for <paramref name="wanted"/>.
    ///
    /// Written this way because a drag has two positions that matter: where the thing started,
    /// which decides what "on the grid" means for it, and where the pointer is, which decides
    /// which grid line. Snapping the difference instead would keep whatever fraction the thing
    /// started with for ever. <paramref name="anchor"/> is how far the part being put on the line
    /// is from the object's own origin, along the axis. Nought means the origin itself.
    /// </summary>
    public Vector3 Along(Vector3 from, Vector3 wanted, Vector3 axis, double anchor = 0)
    {
        if (!On || Step <= 0)
        {
            return wanted - from;
        }

        var direction = axis.Normalized();
        var reach = (wanted - from).Dot(direction);
        var start = from.Dot(direction);
        // The anchor lands on a line, and the origin goes wherever that puts it. Written from a
        // fixed origin rather than as a distance moved, so two things dragged along the same axis
        // land on the same lines.
        var landed = Place(start + reach + anchor) - anchor - start;
        return direction * landed;
    }

    /// <summary>
    /// How far the anchor is from an object's origin, along <paramref name="axis"/>, given the
    /// box the object occupies in the world.
    ///
    /// Positive means the anchor is further along the axis than the origin.
    /// </summary>
    public double AnchorFor(Vector3 axis, Vector3 origin, (Vector3 Min, Vector3 Max) box)
    {
        if (To == SnapTo.Pivot)
        {
            return 0;
        }

        var direction = axis.Normalized();
        var low = box.Min.Dot(direction);
        var high = box.Max.Dot(direction);
        // The box is axis-aligned in the world and the axis is one of the world's own, so its
        // ends along that axis are simply the two corners. Ordered, because dotting a minimum
        // with a negative axis gives the maximum.
        var from = low < high ? low : high;
        var upTo = low < high ? high : low;

        var at = To switch
        {
            SnapTo.Pivot => origin.Dot(direction),
            SnapTo.Base => from,
            SnapTo.Centre => (from + upTo) / 2,
            SnapTo.Top => upTo,
            _ => throw new UnreachableException(),
        };
        return at - origin.Dot(direction);
    }

    // Only a real change is announced, because the view redraws on it.
    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Changed?.Invoke();
    }
}
