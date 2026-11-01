namespace Orblit.Editor.Core;

using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>A point being put down, with the plane it is on.</summary>
/// <param name="At">The point, in the world.</param>
/// <param name="Origin">A point on the plane.</param>
/// <param name="Normal">Which way the plane faces.</param>
/// <param name="Face">The face of the shape being edited that the plane is, or null for the ground.</param>
public sealed record DrawnPoint(Vector3 At, Vector3 Origin, Vector3 Normal, int? Face);

/// <summary>
/// An outline or a path being drawn.
/// </summary>
/// <remarks>
/// Held by the session rather than by a viewport, so the same outline is drawn in all four views of
/// a scene and can be finished in a different one from the one it was started in, which is the whole
/// reason for having four.
/// </remarks>
public sealed class Sketch
{
    private readonly List<Vector3> _points = [];

    public ViewportTool Tool { get; private set; }

    /// <summary>The points so far, in the world.</summary>
    public IReadOnlyList<Vector3> Points => _points;

    /// <summary>
    /// A point on the plane they are being drawn on, fixed by the first point.
    /// </summary>
    /// <remarks>
    /// Fixed rather than recomputed, because a plane worked out afresh from each click follows
    /// whatever is under the pointer, and a point placed over a wall behind would land on the wall
    /// instead of on the floor being drawn.
    /// </remarks>
    public Vector3? Origin { get; private set; }

    public Vector3? Normal { get; private set; }

    /// <summary>Which face is being cut, by its position in the mesh. Null for a shape, which cuts nothing.</summary>
    public int? Face { get; private set; }

    /// <summary>Where the pointer is now, so the line being drawn reaches it. Set by the view.</summary>
    public Vector3? Hovering { get; set; }

    /// <summary>Whether there is enough to finish with.</summary>
    public bool CanFinish => Tool switch
    {
        ViewportTool.None => false,
        // Three points enclose something. Two do not.
        ViewportTool.PolyShape => _points.Count >= 3,
        // A cut needs two ends, and both have to be on the boundary. That is checked when it runs,
        // because until then the second one is still moving.
        ViewportTool.Cut => _points.Count >= 2,
        _ => false,
    };

    public void Start(ViewportTool tool)
    {
        Clear();
        Tool = tool;
    }

    public void Clear()
    {
        Tool = ViewportTool.None;
        _points.Clear();
        Origin = null;
        Normal = null;
        Face = null;
        Hovering = null;
    }

    /// <summary>Puts the plane down, once. Later calls are ignored.</summary>
    public void PlaneAt(Vector3 at, Vector3 way, int? onFace = null)
    {
        Origin ??= at.Clone();
        Normal ??= way.Normalized();
        Face ??= onFace;
    }

    /// <summary>Where a ray meets the plane, or null before there is one.</summary>
    public DrawnPoint? PlaceOn(Vector3 rayOrigin, Vector3 direction) =>
        Origin is { } origin && Normal is { } normal && PolyShape.OnPlane(rayOrigin, direction, origin, normal) is { } at
            ? new DrawnPoint(at, origin, normal, Face)
            : null;

    /// <summary>
    /// Adds a point, unless it is the same one again.
    /// </summary>
    /// <remarks>
    /// Somebody clicking twice in the same place means one point. Without this a double-click leaves
    /// an edge of no length, which is a wall of no width that nothing can be done with afterwards.
    /// </remarks>
    public void Add(Vector3 at)
    {
        if (Drawing.IsNewPoint(_points, at))
        {
            _points.Add(at.Clone());
        }
    }

    /// <summary>Whether this point would close the outline rather than extend it.</summary>
    public bool WouldClose(Vector3 at) => Tool == ViewportTool.PolyShape && Drawing.ClosesOutline(_points, at);

    /// <summary>Whether the outline crosses itself, which is not a room.</summary>
    public bool Crosses() => Drawing.OutlineCrosses(_points, Normal ?? new Vector3(0, 1, 0));

    public void Undo()
    {
        if (_points.Count > 0)
        {
            _points.RemoveAt(_points.Count - 1);
        }

        if (_points.Count == 0)
        {
            // The plane belonged to the first point, so it goes with it. The next click is free to
            // choose a different surface.
            Origin = null;
            Normal = null;
            Face = null;
        }
    }
}
