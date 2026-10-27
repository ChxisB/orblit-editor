namespace Orblit.Editor.Viewport;

using Orblit.Mesh;
using Orblit.VectorMath;
using MeshEdge = (int, int);

/// <summary>
/// Finding what a click landed on.
/// </summary>
/// <remarks>
/// Faces are found by casting a ray, because a face is a surface and that is what a surface
/// answers to. Vertices and edges are found in screen space, because they have no area: a ray
/// through a corner of a box misses it, and what somebody means by clicking near one is "that one,
/// the nearest". A picker is for one look at one mesh. It holds the mesh's points in the world
/// from the first question on, so one made before an edit must not be asked after it.
/// </remarks>
/// <param name="mesh">What is picked from.</param>
/// <param name="transform">Where the object is in the world.</param>
/// <param name="projection">The view the pixels belong to.</param>
/// <param name="seeThrough">
/// Whether what is behind the surface can be selected too. Off, a click or a marquee takes only
/// what somebody can actually see, which is nearly always what they meant. On, it takes the far
/// side of the shape as well, which is what is wanted for scaling a whole ring of a cylinder and
/// never what is wanted for anything else.
/// </param>
public sealed class MeshPicker(Mesh mesh, Matrix4 transform, ViewportProjection projection, bool seeThrough = false)
{
    /// <summary>How near a click has to be, in pixels.</summary>
    private const double Reach = 12;

    private readonly Lazy<List<Vector3>> _world = new(() => [.. mesh.Positions.Select(transform.Transformed3)]);

    /// <summary>
    /// Whether a point on the mesh can be seen from where the camera is: a ray from the eye to the
    /// point, hidden by anything the mesh puts in the way first. The margin matters, because a
    /// corner lies exactly on the faces that meet there and without it every vertex hides itself.
    /// </summary>
    public bool Visible(Vector3 world)
    {
        if (seeThrough)
        {
            return true;
        }

        var away = world - projection.Eye;
        var reach = away.Length;
        if (reach < 1e-6)
        {
            return true;
        }

        var direction = away / reach;
        return !Triangles().Any(
            piece => RayHitsTriangle(projection.Eye, direction, piece.A, piece.B, piece.C) is { } hit && hit < reach - 1e-3);
    }

    /// <summary>The face under a pixel, or null.</summary>
    public int? FaceAt(Offset pixel)
    {
        var ray = projection.RayThrough(pixel);
        var nearest = double.PositiveInfinity;
        int? found = null;

        foreach (var piece in Triangles())
        {
            if (RayHitsTriangle(ray.Origin, ray.Direction, piece.A, piece.B, piece.C) is not { } hit || hit >= nearest)
            {
                continue;
            }

            nearest = hit;
            found = piece.Face;
        }

        return found;
    }

    /// <summary>The vertex nearest a pixel, within reach.</summary>
    public int? VertexAt(Offset pixel)
    {
        var nearest = Reach;
        int? found = null;

        for (var index = 0; index < _world.Value.Count; index++)
        {
            var world = _world.Value[index];
            if (projection.Project(world) is not { } at)
            {
                continue;
            }

            var away = (at - pixel).Distance;
            if (away >= nearest || !Visible(world))
            {
                continue;
            }

            nearest = away;
            found = index;
        }

        return found;
    }

    /// <summary>Every vertex inside a box on screen.</summary>
    public List<int> VerticesIn(Rect box) =>
    [
        .. Enumerable.Range(0, _world.Value.Count).Where(
            index => projection.Project(_world.Value[index]) is { } at && box.Contains(at) && Visible(_world.Value[index])),
    ];

    /// <summary>
    /// Every edge inside a box, whole. Both ends in, not either: a marquee that took every edge it
    /// clipped would take half the shape behind whatever was being framed, and "everything I drew
    /// round" is what somebody means.
    /// </summary>
    public List<MeshEdge> EdgesIn(Rect box) => [.. mesh.AllEdges.Where(edge => IsWithin(box, edge))];

    /// <summary>
    /// Every face whose middle is inside a box. The middle rather than every corner, so a face
    /// bigger than the box is still caught by drawing over the part of it somebody can see. A face
    /// only partly inside is not: the middle decides, which is one rule rather than an argument
    /// about how much counts.
    /// </summary>
    public List<int> FacesIn(Rect box)
    {
        var found = new List<int>();
        for (var index = 0; index < mesh.Faces.Count; index++)
        {
            var face = mesh.Faces[index];
            if (face.Vertices.Count < 3)
            {
                continue;
            }

            var middle = transform.Transformed3(mesh.CentreOf(face));
            if (projection.Project(middle) is { } at && box.Contains(at) && Visible(middle))
            {
                found.Add(index);
            }
        }

        return found;
    }

    /// <summary>The edge nearest a pixel, within reach.</summary>
    public MeshEdge? EdgeAt(Offset pixel)
    {
        var nearest = Reach;
        MeshEdge? found = null;

        foreach (var edge in mesh.AllEdges)
        {
            var (a, b) = (_world.Value[edge.Item1], _world.Value[edge.Item2]);
            if (projection.Project(a) is not { } pa || projection.Project(b) is not { } pb)
            {
                continue;
            }

            var away = PixelToSegment(pixel, pa, pb);
            // Either end being visible is enough: an edge running away from the camera can have
            // its far corner behind the shape and still be an edge somebody is looking straight at.
            if (away >= nearest || (!Visible(a) && !Visible(b)))
            {
                continue;
            }

            nearest = away;
            found = edge;
        }

        return found;
    }

    private bool IsWithin(Rect box, MeshEdge edge)
    {
        var (a, b) = (_world.Value[edge.Item1], _world.Value[edge.Item2]);
        return projection.Project(a) is { } pa
            && projection.Project(b) is { } pb
            && box.Contains(pa)
            && box.Contains(pb)
            && (Visible(a) || Visible(b));
    }

    // A fan from the first corner, the same way a face is triangulated for drawing, so what is
    // clicked is what is on screen.
    private IEnumerable<(int Face, Vector3 A, Vector3 B, Vector3 C)> Triangles()
    {
        for (var at = 0; at < mesh.Faces.Count; at++)
        {
            var points = mesh.Faces[at].Vertices
                .Where(index => index >= 0 && index < _world.Value.Count)
                .Select(index => _world.Value[index])
                .ToList();

            for (var i = 1; i + 1 < points.Count; i++)
            {
                yield return (at, points[0], points[i], points[i + 1]);
            }
        }
    }

    /// <summary>How far a point is from a line between two others.</summary>
    private static double PixelToSegment(Offset at, Offset a, Offset b)
    {
        var along = b - a;
        var length2 = (along.Dx * along.Dx) + (along.Dy * along.Dy);
        if (length2 < 1e-9)
        {
            return (at - a).Distance;
        }

        var t = Math.Clamp((((at - a).Dx * along.Dx) + ((at - a).Dy * along.Dy)) / length2, 0.0, 1.0);
        return (at - (a + (along * t))).Distance;
    }

    /// <summary>
    /// Where a ray meets a triangle, or null. Möller–Trumbore. Both sides count as a hit: clicking
    /// the far wall of a room from inside it should select the far wall, and a back-face cull here
    /// would mean nothing was selectable from inside anything.
    /// </summary>
    private static double? RayHitsTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        var edge1 = b - a;
        var edge2 = c - a;
        var h = direction.Cross(edge2);
        var det = edge1.Dot(h);
        if (Math.Abs(det) < 1e-12)
        {
            return null;
        }

        var f = 1 / det;
        var s = origin - a;
        var u = f * s.Dot(h);
        if (u is < 0 or > 1)
        {
            return null;
        }

        var q = s.Cross(edge1);
        var v = f * direction.Dot(q);
        if (v < 0 || u + v > 1)
        {
            return null;
        }

        var t = f * edge2.Dot(q);
        return t > 1e-6 ? t : null;
    }
}
