namespace Orblit.Editor.Panels;

using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>
/// Where the coordinate view looks: how many coordinate units it is across, and which point is in
/// the middle of it.
/// </summary>
/// <remarks>
/// The view zooms rather than the coordinates being clamped. A tiling face covers eight units and a
/// stretched one covers one, and a view that only ever showed nought to one would show the first as
/// a smear over the whole square.
/// </remarks>
public sealed class UvFrame
{
    public UvFrame(double span, Vector2 centre)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(span);
        Span = span;
        Centre = centre.Clone();
    }

    /// <summary>What the view shows before it has anything to frame.</summary>
    public static UvFrame Start => new(1.4, new Vector2(0.5, 0.5));

    public double Span { get; }

    public Vector2 Centre { get; }

    /// <summary>
    /// The frame that shows these faces. The unit square is always in shot, whatever they cover, so
    /// "is this face inside the texture" can be answered by looking. It is the box that holds both,
    /// because a point between the two would cut one off when they are far apart.
    /// </summary>
    public static UvFrame Around(Mesh mesh, IReadOnlyList<Face> faces)
    {
        if (mesh.UvBoundsOf(faces) is not { } box)
        {
            return Start;
        }

        var low = new Vector2(Math.Min(box.Min.X, 0), Math.Min(box.Min.Y, 0));
        var high = new Vector2(Math.Max(box.Max.X, 1), Math.Max(box.Max.Y, 1));
        return new UvFrame(Math.Max(high.X - low.X, high.Y - low.Y) * 1.25, (low + high) * 0.5);
    }

    /// <summary>The coordinate under a point of a view <paramref name="width"/> across. Down the screen is down in V, which is the way a texture is read.</summary>
    public Vector2 ToUv(double x, double y, double width, double height)
    {
        var scale = Span / width;
        return new Vector2(Centre.X + ((x - (width / 2)) * scale), Centre.Y - ((y - (height / 2)) * scale));
    }

    /// <summary>Where a coordinate lands in a view of that size.</summary>
    public (double X, double Y) At(Vector2 uv, double width, double height)
    {
        var scale = width / Span;
        return ((width / 2) + ((uv.X - Centre.X) * scale), (height / 2) - ((uv.Y - Centre.Y) * scale));
    }
}
