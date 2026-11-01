namespace Orblit.Editor.Viewport;

using System.Diagnostics;
using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>What a drag on the gizmo does.</summary>
public enum GizmoMode
{
    Move,
    Rotate,
}

public static class GizmoModes
{
    public static string Label(this GizmoMode mode) => mode switch
    {
        GizmoMode.Move => "Move",
        GizmoMode.Rotate => "Rotate",
        _ => throw new UnreachableException(),
    };

    /// <summary>The name of the glyph the toolbar shows. The shell maps it to a drawing.</summary>
    public static string Icon(this GizmoMode mode) => mode switch
    {
        GizmoMode.Move => "open_with",
        GizmoMode.Rotate => "threesixty",
        _ => throw new UnreachableException(),
    };
}

/// <summary>Which of the three the pointer has hold of.</summary>
public enum GizmoAxis
{
    X,
    Y,
    Z,
}

public static class GizmoAxes
{
    /// <summary>
    /// Red, green, blue for x, y, z. Not a house style. It is the convention every tool with three
    /// axes in it has used for decades, and an editor that picks its own colours makes somebody
    /// re-learn the one thing they already knew.
    /// </summary>
    public static Colour Colour(this GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => new Colour(0xFFE2564B),
        GizmoAxis.Y => new Colour(0xFF6FBF73),
        GizmoAxis.Z => new Colour(0xFF4C8FD6),
        _ => throw new UnreachableException(),
    };

    public static Vector3 Direction(this GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => new Vector3(1, 0, 0),
        GizmoAxis.Y => new Vector3(0, 1, 0),
        GizmoAxis.Z => new Vector3(0, 0, 1),
        _ => throw new UnreachableException(),
    };
}

/// <summary>
/// The three handles, where they are, and what dragging one does.
/// </summary>
/// <remarks>
/// Held apart from whatever draws it so the arithmetic, which axis a pointer is over and how far
/// along it a drag has gone, can be tested without a window, a frame or a renderer.
/// </remarks>
public sealed class Gizmo(GizmoMode mode, Vector3 pivot, ViewportProjection projection)
{
    /// <summary>
    /// How close the pointer has to be to a handle, in pixels.
    /// </summary>
    /// <remarks>
    /// Generous on purpose. A handle you have to hit exactly is one that fails often enough to be
    /// worth avoiding, and the cost of a near miss is only that the view orbits instead.
    /// </remarks>
    public const double GrabRadius = 9;

    // How many points a ring is drawn and measured with.
    private const int RingSteps = 48;

    public GizmoMode Mode { get; } = mode;

    /// <summary>Where the handles sit: the world position of what is selected.</summary>
    public Vector3 Pivot { get; } = pivot;

    public ViewportProjection Projection { get; } = projection;

    public double Length => Projection.HandleLength(Pivot);

    /// <summary>The far end of one arm.</summary>
    public Vector3 EndOf(GizmoAxis axis) => Pivot + (axis.Direction() * Length);

    /// <summary>The ring around one axis, in world space.</summary>
    public List<Vector3> RingOf(GizmoAxis axis)
    {
        // Two directions across the axis, so the circle lies in the plane the axis turns things in.
        var normal = axis.Direction();
        var across = Math.Abs(normal.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
        var u = across.Cross(normal).Normalized();
        var v = normal.Cross(u).Normalized();

        var ring = new List<Vector3>(RingSteps + 1);
        for (var step = 0; step <= RingSteps; step++)
        {
            var turn = (double)step / RingSteps * 2 * Math.PI;
            ring.Add(Pivot + (((u * Math.Cos(turn)) + (v * Math.Sin(turn))) * Length));
        }

        return ring;
    }

    /// <summary>The ring as it lands on screen, with anything behind the eye left out.</summary>
    public List<Offset> RingOnScreen(GizmoAxis axis) =>
        [.. RingOf(axis).Select(Projection.Project).OfType<Offset>()];

    /// <summary>
    /// Which handle is under <paramref name="pixel"/>, or null for none.
    /// </summary>
    /// <remarks>
    /// Nearest wins, so where two handles cross the one whose line the pointer is actually on is
    /// the one that answers.
    /// </remarks>
    public GizmoAxis? AxisAt(Offset pixel)
    {
        GizmoAxis? best = null;
        var nearest = GrabRadius;

        foreach (var axis in Enum.GetValues<GizmoAxis>())
        {
            var distance = Mode == GizmoMode.Move ? ToSegment(pixel, axis) : ToRing(pixel, axis);
            if (distance is null || distance >= nearest)
            {
                continue;
            }

            nearest = distance.Value;
            best = axis;
        }

        return best;
    }

    /// <summary>
    /// Where a pointer sits along one axis, in world space.
    /// </summary>
    /// <remarks>
    /// Read off a plane that holds the axis and faces the camera as squarely as it can. Using the
    /// axis alone would leave the depth of the grab undefined, and the object would jump the moment
    /// the drag began.
    /// </remarks>
    public Vector3? PointOnAxis(Offset pixel, GizmoAxis axis)
    {
        var direction = axis.Direction();
        var across = direction.Cross(Projection.Forward);
        if (across.Length2 < 1e-9)
        {
            return null;
        }

        var normal = across.Cross(direction).Normalized();

        var hit = Projection.RayThrough(pixel).Meets(Pivot, normal);
        if (hit is null)
        {
            return null;
        }

        // Only the part along the axis matters. The rest is where the pointer happened to be
        // across it.
        return Pivot + (direction * (hit - Pivot).Dot(direction));
    }

    /// <summary>Where a pointer sits on the disc an axis turns in.</summary>
    public Vector3? PointOnRing(Offset pixel, GizmoAxis axis) =>
        Projection.RayThrough(pixel).Meets(Pivot, axis.Direction());

    /// <summary>How far a drag has turned something, in radians, signed about the axis.</summary>
    public double AngleBetween(Vector3 from, Vector3 to, GizmoAxis axis)
    {
        var a = (from - Pivot).Normalized();
        var b = (to - Pivot).Normalized();
        // The sign has to come from the axis rather than from the shorter way round, or a drag
        // past a quarter turn would spring back.
        return Math.Atan2(a.Cross(b).Dot(axis.Direction()), a.Dot(b));
    }

    private static double DistanceToSegment(Offset point, Offset from, Offset to)
    {
        var line = to - from;
        var lengthSquared = (line.Dx * line.Dx) + (line.Dy * line.Dy);
        if (lengthSquared < 1e-9)
        {
            return (point - from).Distance;
        }

        var t = Doubles.Clamp(
            (((point.Dx - from.Dx) * line.Dx) + ((point.Dy - from.Dy) * line.Dy)) / lengthSquared,
            0.0,
            1.0);
        return (point - (from + (line * t))).Distance;
    }

    private double? ToSegment(Offset pixel, GizmoAxis axis)
    {
        var from = Projection.Project(Pivot);
        var to = Projection.Project(EndOf(axis));
        return from is null || to is null ? null : DistanceToSegment(pixel, from.Value, to.Value);
    }

    private double? ToRing(Offset pixel, GizmoAxis axis)
    {
        var points = RingOnScreen(axis);
        if (points.Count < 2)
        {
            return null;
        }

        var nearest = double.PositiveInfinity;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var distance = DistanceToSegment(pixel, points[i], points[i + 1]);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        return nearest;
    }
}
