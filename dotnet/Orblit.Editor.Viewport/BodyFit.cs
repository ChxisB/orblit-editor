namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>
/// Sizing a body to the object it is on, and what choosing a shape starts the body as.
/// </summary>
/// <remarks>
/// In the object's own units, as a body is, so a fit is the mesh's size whatever the object is scaled
/// to.
/// </remarks>
public static class BodyFit
{
    /// <summary>
    /// <paramref name="body"/> reshaped round the object's mesh: the same shape, as big as the mesh and
    /// centred on it.
    /// </summary>
    /// <param name="body">The body to fit.</param>
    /// <param name="item">The object the body is on.</param>
    /// <param name="reported">How big a mesh the editor holds no geometry for is, from the file it came from.</param>
    public static BodyComponent Fitted(BodyComponent body, SceneObject item, (Vector3 Min, Vector3 Max)? reported = null)
    {
        var bounds = BoundsOf(item, reported);
        var size = bounds.Max - bounds.Min;
        var middle = (bounds.Min + bounds.Max) * 0.5;
        return body.Shape switch
        {
            BodyShape.Mesh => body,
            BodyShape.Compound => body.CopyWith(parts: [new BodyPart(size: size)], centre: middle),
            BodyShape.Box => body.CopyWith(size: size, centre: middle),
            BodyShape.Sphere => body.CopyWith(radius: Math.Max(size.X, Math.Max(size.Y, size.Z)) / 2, centre: middle),
            BodyShape.Capsule or BodyShape.Cylinder => body.CopyWith(radius: Math.Max(size.X, size.Z) / 2, height: size.Y, centre: middle),
            BodyShape.Hull => body.CopyWith(hull: HullAround(item, bounds, middle), centre: middle),
            // Ground is the top of the mesh: a floor tile is stood on, not in.
            BodyShape.Plane => body.CopyWith(centre: new Vector3(middle.X, bounds.Max.Y, middle.Z)),
            _ => throw new ArgumentOutOfRangeException(nameof(body), body.Shape, "A shape the fit does not know."),
        };
    }

    /// <summary>
    /// <paramref name="body"/> with <paramref name="shape"/>, and a hull or a compound that has nothing
    /// in it yet cut from the mesh, so that choosing one gives a body that works.
    /// </summary>
    public static BodyComponent Reshaped(BodyComponent body, BodyShape shape, SceneObject item, (Vector3 Min, Vector3 Max)? reported = null)
    {
        var next = body.CopyWith(shape: shape);
        var empty = (shape == BodyShape.Compound && next.Parts.Count == 0) || (shape == BodyShape.Hull && next.Hull.Count == 0);
        return empty ? Fitted(next, item, reported) : next;
    }

    /// <summary>
    /// How big the object is in its own space. A place with nothing drawn is the metre box a new body
    /// starts as, not the two metre placeholder.
    /// </summary>
    public static (Vector3 Min, Vector3 Max) BoundsOf(SceneObject item, (Vector3 Min, Vector3 Max)? reported = null) =>
        item.IsDrawable ? item.LocalBounds(reported) : (Vector3.All(-0.5), Vector3.All(0.5));

    /// <summary>
    /// The corners of the mesh's hull, written flat and measured from <paramref name="middle"/>. The
    /// corners of <paramref name="bounds"/> when the editor holds no geometry for the object, as for an
    /// imported model, or when its points enclose no volume.
    /// </summary>
    private static double[] HullAround(SceneObject item, (Vector3 Min, Vector3 Max) bounds, Vector3 middle)
    {
        var mesh = item.IsDrawable ? item.CurrentMesh : null;
        var fromMesh = mesh is null || mesh.IsEmpty ? ConvexOutline.None : ConvexOutline.Around(mesh.Positions);
        var corners = fromMesh.HasVolume ? fromMesh.Corners : BoxCorners(bounds);
        return [.. corners.SelectMany(corner => new[] { corner.X - middle.X, corner.Y - middle.Y, corner.Z - middle.Z })];
    }

    private static IEnumerable<Vector3> BoxCorners((Vector3 Min, Vector3 Max) bounds) =>
        from x in new[] { bounds.Min.X, bounds.Max.X }
        from y in new[] { bounds.Min.Y, bounds.Max.Y }
        from z in new[] { bounds.Min.Z, bounds.Max.Z }
        select new Vector3(x, y, z);
}
