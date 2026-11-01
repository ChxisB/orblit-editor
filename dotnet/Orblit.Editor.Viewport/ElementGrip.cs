namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>What a pointer took hold of: the handles, which of them, and the point on it.</summary>
internal sealed record Held(Gizmo Gizmo, GizmoAxis Axis, Vector3 Grabbed);

/// <summary>
/// A drag on the handles of the selected parts of a mesh.
/// </summary>
/// <remarks>
/// Every frame is worked out from the mesh as it was when the drag began, not from the last frame,
/// so snapping and rounding cannot add up to a drift and a drag back to the start lands exactly
/// there.
/// </remarks>
internal sealed class ElementGrip
{
    private readonly Mesh _start;
    private readonly List<int> _points;
    private readonly Matrix4 _intoObject;
    private readonly GizmoMode _mode;
    private readonly Vector3 _grabbed;
    private readonly Vector3 _pivot;
    private bool _reported;

    private ElementGrip(Mesh start, List<int> points, EditingMesh editing, Held held)
    {
        _start = start;
        _points = points;
        _intoObject = Matrix4.Inverted(editing.Transform);
        _mode = held.Gizmo.Mode;
        Axis = held.Axis;
        _grabbed = held.Grabbed;
        _pivot = held.Gizmo.Pivot.Clone();
    }

    public GizmoAxis Axis { get; }

    /// <summary>What the drag is working on: the selection it began with, or what an extrude made.</summary>
    public required ElementSelection Selection { get; init; }

    /// <summary>
    /// The extrude that came before the first movement, to be written straight away. Somebody who
    /// holds shift, pulls and lets go without moving has still made a face, and it should be there
    /// and be undoable.
    /// </summary>
    public ElementDrag? Extrusion { get; init; }

    /// <summary>
    /// Takes hold, or says there is nothing to take hold of. Extruding first and then dragging what
    /// came out is the move a modelling tool is built around: a doorway, a chimney and a ledge are
    /// all one face pulled out.
    /// </summary>
    public static ElementGrip? Begin(EditingMesh editing, ElementSelection selection, Held held, bool extrude)
    {
        var mesh = editing.Mesh.Copy();
        var chosen = selection.Copy();
        ElementDrag? extrusion = null;

        if (extrude && chosen.Faces.Count > 0 && Extruded(mesh, chosen) is { } made)
        {
            chosen = made;
            extrusion = new ElementDrag(mesh.Copy(), made, "Extrude", false);
        }

        var points = chosen.PointsIn(mesh).ToList();
        return points.Count == 0
            ? null
            : new ElementGrip(mesh, points, editing, held) { Selection = chosen, Extrusion = extrusion, _reported = extrusion is not null };
    }

    /// <summary>The drag as the pointer has taken it, or null when it points nowhere useful.</summary>
    public ElementDrag? Follow(Gizmo gizmo, Offset at, Snapping snap)
    {
        var next = _start.Copy();
        var done = _mode == GizmoMode.Move ? Move(next, gizmo, at, snap) : Turn(next, gizmo, at, snap);
        if (!done)
        {
            return null;
        }

        var drag = new ElementDrag(next, Selection, _mode == GizmoMode.Move ? "Move" : "Turn", _reported);
        _reported = true;
        return drag;
    }

    // Selected again by position, because an extrude drops the faces it was given and makes new
    // ones. Matched against the copy's own faces, since a copy's faces are other objects and the
    // extrude finds nothing by identity in the originals.
    private static ElementSelection? Extruded(Mesh mesh, ElementSelection selection)
    {
        var made = mesh.Extrude(selection.FacesIn(mesh), 0).ToHashSet();
        return made.Count == 0
            ? null
            : new ElementSelection(faces: Enumerable.Range(0, mesh.Faces.Count).Where(at => made.Contains(mesh.Faces[at])));
    }

    // Snapped in the world, where the grid is, and then taken into the object's frame. Snapping
    // after the conversion would put the grid at whatever angle and scale the object has.
    private bool Move(Mesh next, Gizmo gizmo, Offset at, Snapping snap)
    {
        if (gizmo.PointOnAxis(at, Axis) is not { } now)
        {
            return false;
        }

        var shift = snap.Along(_pivot, now - _grabbed + _pivot, Axis.Direction());
        next.MovePoints(_points, _intoObject.Rotated3(shift));
        return true;
    }

    // About the middle of the points, with the axis taken into the object's frame first, so the
    // ring somebody grabbed in the world is the one the corners turn about inside a turned object.
    private bool Turn(Mesh next, Gizmo gizmo, Offset at, Snapping snap)
    {
        if (gizmo.PointOnRing(at, Axis) is not { } now || _start.CentreOfPoints(_points) is not { } about)
        {
            return false;
        }

        var angle = snap.Turn(gizmo.AngleBetween(_grabbed, now, Axis));
        var axis = _intoObject.Rotated3(Axis.Direction()).Normalized();
        next.TurnPoints(_points, Quaternion.AxisAngle(axis, angle).AsRotationMatrix(), about);
        return true;
    }
}
