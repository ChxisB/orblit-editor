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
    /// "is this face inside the texture" can be answered by looking.
    /// </summary>
    public static UvFrame Around(Mesh mesh, IReadOnlyList<Face> faces)
    {
        if (mesh.UvBoundsOf(faces) is not { } box)
        {
            return Start;
        }

        var wide = Math.Max(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y);
        var middle = (box.Min + box.Max) * 0.5;
        return new UvFrame(
            Math.Max(wide, 1.0) * 1.25,
            new Vector2((middle.X + 0.5) / 2, (middle.Y + 0.5) / 2));
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
