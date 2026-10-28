namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>
/// Where a pixel puts a point while drawing: on the plane the drawing already has, else on the face
/// of the shape under the pointer, else on the ground.
/// </summary>
internal sealed class DrawingPlace(Sketch sketch, EditingMesh? editing, ViewportProjection projection, bool seeThrough)
{
    // F2: the arguments are one view of the world and one drawing, which change together.

    /// <summary>
    /// The point a click puts down. A cut has no ground to fall back on, because it can only
    /// divide a face that exists.
    /// </summary>
    public DrawnPoint? ForClick(Offset pixel)
    {
        var ray = projection.RayThrough(pixel);
        return OnSurface(pixel, ray) ?? (sketch.Tool == ViewportTool.Cut ? null : OnGround(ray));
    }

    /// <summary>Where the line being drawn reaches to, which can be anywhere a click could not.</summary>
    public Vector3? ForHover(Offset pixel)
    {
        var ray = projection.RayThrough(pixel);
        return (OnSurface(pixel, ray) ?? OnGround(ray))?.At;
    }

    // Once the plane is down every later point is on it. A plane worked out afresh each click would
    // follow whatever happened to be behind.
    private DrawnPoint? OnSurface(Offset pixel, ViewRay ray) =>
        sketch.PlaceOn(ray.Origin, ray.Direction) ?? OnFace(pixel, ray);

    private DrawnPoint? OnFace(Offset pixel, ViewRay ray)
    {
        if (editing is null)
        {
            return null;
        }

        var picker = new MeshPicker(editing.Mesh, editing.Transform, projection, seeThrough);
        if (picker.FaceAt(pixel) is not { } found)
        {
            return null;
        }

        var face = editing.Mesh.Faces[found];

        // Into the world, because the drawing is in the world and the object may be somewhere else
        // entirely.
        var centre = editing.Transform.Transformed3(editing.Mesh.CentreOf(face));
        var normal = editing.Transform.Rotated3(editing.Mesh.NormalOf(face).Clone()).Normalized();
        return PolyShape.OnPlane(ray.Origin, ray.Direction, centre, normal) is { } at
            ? new DrawnPoint(at, centre, normal, found)
            : null;
    }

    // The floor, at the height the grid is drawn at. A plan is drawn on the floor unless somebody
    // aimed at something.
    private static DrawnPoint? OnGround(ViewRay ray)
    {
        var up = new Vector3(0, 1, 0);
        return PolyShape.OnPlane(ray.Origin, ray.Direction, Vector3.Zero(), up) is { } at
            ? new DrawnPoint(at, Vector3.Zero(), up, null)
            : null;
    }
}
