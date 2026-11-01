namespace Orblit.Editor.Viewport;

using Orblit.VectorMath;

/// <summary>A box of the view's pixels.</summary>
public readonly record struct Box(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;

    public Box Inflate(double by) => new(Left - by, Top - by, Right + by, Bottom + by);

    public bool Contains(Offset point) =>
        point.Dx >= Left && point.Dx <= Right && point.Dy >= Top && point.Dy <= Bottom;
}

/// <summary>
/// Lines in the world, gathered as segments in the view's pixels.
/// </summary>
/// <remarks>
/// What a wireframe gizmo draws with: the body gizmo its shapes, the joint gizmo its frames and
/// limits. Held in one place so both project, clip and round a circle the same way.
/// </remarks>
public sealed class WorldLines(ViewportProjection projection)
{
    /// <summary>
    /// Segments to a full circle: round at any size a gizmo is drawn at, and few enough that a
    /// hundred selected balls cost nothing.
    /// </summary>
    public const int Segments = 32;

    private readonly List<(Offset From, Offset To)> _strokes = [];

    public ViewportProjection Projection => projection;

    /// <summary>Each line as two points of the view.</summary>
    public IReadOnlyList<(Offset From, Offset To)> Strokes => _strokes;

    /// <summary>The smallest box round every line, or null when nothing was drawn.</summary>
    public Box? Bounds
    {
        get
        {
            if (_strokes.Count == 0)
            {
                return null;
            }

            var ends = _strokes.SelectMany(stroke => new[] { stroke.From, stroke.To }).ToList();
            return new Box(
                ends.Min(point => point.Dx),
                ends.Min(point => point.Dy),
                ends.Max(point => point.Dx),
                ends.Max(point => point.Dy));
        }
    }

    /// <summary>
    /// Part of a circle about <paramref name="centre"/> in the plane of <paramref name="u"/> and
    /// <paramref name="v"/>, from angle <paramref name="from"/> to <paramref name="to"/> measured from
    /// <paramref name="u"/> towards <paramref name="v"/>.
    /// </summary>
    // F2: a centre, a plane, a radius and two angles are what a circle is.
    public void Arc(Vector3 centre, Vector3 u, Vector3 v, double radius, double from, double to)
    {
        var steps = Math.Max(2, (int)Math.Ceiling(Segments * Math.Abs(to - from) / (2 * Math.PI)));

        Vector3 At(int i)
        {
            var angle = from + ((to - from) * i / steps);
            return centre + (u * (radius * Math.Cos(angle))) + (v * (radius * Math.Sin(angle)));
        }

        for (var i = 0; i < steps; i++)
        {
            Line(At(i), At(i + 1));
        }
    }

    /// <summary>
    /// A circle round <paramref name="centre"/>, <paramref name="pixels"/> in radius however far off it
    /// is, as a handle is drawn.
    /// </summary>
    public void Ring(Vector3 centre, double pixels)
    {
        if (projection.Project(centre) is not { } at)
        {
            return;
        }

        Offset On(int i) => new(
            at.Dx + (pixels * Math.Cos(2 * Math.PI * i / Segments)),
            at.Dy + (pixels * Math.Sin(2 * Math.PI * i / Segments)));

        for (var i = 0; i < Segments; i++)
        {
            _strokes.Add((On(i), On(i + 1)));
        }
    }

    public void Line(Vector3 a, Vector3 b)
    {
        var from = projection.Project(a);
        var to = projection.Project(b);
        // With one end behind the eye the line would be flipped across the view, through somewhere
        // the thing drawn is not.
        if (from is { } start && to is { } end)
        {
            _strokes.Add((start, end));
        }
    }
}
