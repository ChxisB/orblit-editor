namespace Orblit.Editor.Viewport;

using System.Runtime.CompilerServices;
using Orblit.VectorMath;

/// <summary>
/// The convex solid round a set of points, as the corners that stand out and the edges between them.
/// </summary>
/// <remarks>
/// What a hull body is drawn as, and what a hull is cut down to when it is taken from a mesh. The
/// simulation keeps at most 255 corners and this keeps every one, so a very detailed set is drawn a
/// little larger than it is simulated.
/// </remarks>
public sealed class ConvexOutline
{
    private static readonly ConditionalWeakTable<IReadOnlyList<double>, ConvexOutline> Made = [];

    private ConvexOutline(IReadOnlyList<Vector3> corners, IReadOnlyList<(int, int)> edges)
    {
        Corners = corners;
        Edges = edges;
    }

    /// <summary>What points that enclose no volume make.</summary>
    public static ConvexOutline None { get; } = new([], []);

    public IReadOnlyList<Vector3> Corners { get; }

    /// <summary>
    /// Each edge as two positions in <see cref="Corners"/>. A flat face is bounded by its edges only:
    /// the diagonals that split it into triangles are left out.
    /// </summary>
    public IReadOnlyList<(int, int)> Edges { get; }

    /// <summary>Whether the points enclose a volume. Fewer than four, or all in one plane or on one line, do not.</summary>
    public bool HasVolume => Corners.Count > 0;

    /// <summary>The outline round <paramref name="points"/>.</summary>
    public static ConvexOutline Around(IReadOnlyList<Vector3> points)
    {
        var solid = points.Count < 4 ? null : Solid.Start(points);
        if (solid is null)
        {
            return None;
        }

        for (var i = 0; i < points.Count; i++)
        {
            solid.Take(i);
        }

        return solid.Outline();
    }

    /// <summary>
    /// The outline round <paramref name="flat"/>, points written x, y, z one after another, or none
    /// when it is not a whole number of finite points.
    /// </summary>
    /// <remarks>
    /// Worked out once for each list, found by identity. A body's list is never changed, and a painter
    /// asks for it every frame.
    /// </remarks>
    public static ConvexOutline Of(IReadOnlyList<double> flat)
    {
        if (Made.TryGetValue(flat, out var known))
        {
            return known;
        }

        var made = Around(PointsOf(flat));
        Made.AddOrUpdate(flat, made);
        return made;
    }

    private static List<Vector3> PointsOf(IReadOnlyList<double> flat)
    {
        if (flat.Count % 3 != 0 || !flat.All(double.IsFinite))
        {
            return [];
        }

        var points = new List<Vector3>(flat.Count / 3);
        for (var i = 0; i < flat.Count; i += 3)
        {
            points.Add(new Vector3(flat[i], flat[i + 1], flat[i + 2]));
        }

        return points;
    }

    // The largest span of the points along any axis.
    private static double Extent(IReadOnlyList<Vector3> points)
    {
        var low = points[0].Clone();
        var high = points[0].Clone();
        foreach (var point in points)
        {
            low.SetValues(Math.Min(low.X, point.X), Math.Min(low.Y, point.Y), Math.Min(low.Z, point.Z));
            high.SetValues(Math.Max(high.X, point.X), Math.Max(high.Y, point.Y), Math.Max(high.Z, point.Z));
        }

        var span = high - low;
        return Math.Max(span.X, Math.Max(span.Y, span.Z));
    }

    // The index of the point that scores highest, and its score.
    private static (int Index, double Score) Farthest(IReadOnlyList<Vector3> points, Func<Vector3, double> score)
    {
        var best = 0;
        var most = -1.0;
        for (var i = 0; i < points.Count; i++)
        {
            var value = score(points[i]);
            if (value > most)
            {
                most = value;
                best = i;
            }
        }

        return (best, most);
    }

    // Four points that are not on one plane, spread wide, or null when there are none to find. The
    // first solid the rest are added to.
    private static int[]? SeedOf(IReadOnlyList<Vector3> points, double eps)
    {
        var first = 0;
        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].X < points[first].X)
            {
                first = i;
            }
        }

        var from = points[first];

        var (second, apart) = Farthest(points, p => p.DistanceTo(from));
        if (apart <= eps)
        {
            return null;
        }

        var along = (points[second] - from).Normalized();

        var (third, off) = Farthest(points, p => (p - from).Cross(along).Length);
        if (off <= eps)
        {
            return null;
        }

        var flat = along.Cross(points[third] - from).Normalized();

        var (fourth, high) = Farthest(points, p => Math.Abs(flat.Dot(p - from)));
        return high <= eps ? null : [first, second, third, fourth];
    }

    // A triangle of the solid, with the plane it lies in.
    private sealed class Face(int a, int b, int c, Vector3 normal, double offset)
    {
        public int A { get; } = a;

        public int B { get; } = b;

        public int C { get; } = c;

        // A unit vector pointing out of the solid.
        public Vector3 Normal { get; } = normal;

        // The three edges, each in the direction the face winds.
        public (int, int)[] Edges => [(A, B), (B, C), (C, A)];

        // The face on three points in the order given, which looks at whoever sees them wound
        // counter-clockwise.
        public static Face Through(IReadOnlyList<Vector3> points, int a, int b, int c)
        {
            var normal = (points[b] - points[a]).Cross(points[c] - points[a]);
            normal.Normalize();
            return new Face(a, b, c, normal, normal.Dot(points[a]));
        }

        // The face on the corners, ordered so that it looks away from inside.
        public static Face Outward(IReadOnlyList<Vector3> points, int[] corners, Vector3 inside)
        {
            var face = Through(points, corners[0], corners[1], corners[2]);
            return face.Above(inside) > 0 ? Through(points, corners[0], corners[2], corners[1]) : face;
        }

        // How far the point is out from the plane, which is negative behind it.
        public double Above(Vector3 point) => Normal.Dot(point) - offset;
    }

    // A convex solid grown one point at a time: each point outside it takes the faces it can see away
    // and joins the edge they leave to itself.
    private sealed class Solid
    {
        private readonly IReadOnlyList<Vector3> _points;
        private readonly int[] _seed;
        private readonly double _eps;
        private readonly List<Face> _faces;

        private Solid(IReadOnlyList<Vector3> points, int[] seed, double eps, List<Face> faces)
        {
            _points = points;
            _seed = seed;
            _eps = eps;
            _faces = faces;
        }

        // A tetrahedron on four well-spread points, or null for points that enclose no volume.
        public static Solid? Start(IReadOnlyList<Vector3> points)
        {
            // Within this of a plane counts as on it.
            var eps = Extent(points) * 1e-6;
            var seed = SeedOf(points, eps);
            if (seed is null)
            {
                return null;
            }

            // A point inside the solid for good: the middle of the first tetrahedron.
            var inside = (points[seed[0]] + points[seed[1]] + points[seed[2]] + points[seed[3]]).Scaled(0.25);
            int[][] sides =
            [
                [seed[0], seed[1], seed[2]],
                [seed[0], seed[1], seed[3]],
                [seed[0], seed[2], seed[3]],
                [seed[1], seed[2], seed[3]],
            ];
            return new Solid(points, seed, eps, [.. sides.Select(corners => Face.Outward(points, corners, inside))]);
        }

        // Adds the point at the index, if it is outside the solid.
        public void Take(int index)
        {
            if (_seed.Contains(index))
            {
                return;
            }

            var point = _points[index];
            var seen = _faces.Where(face => face.Above(point) > _eps).ToList();
            if (seen.Count == 0)
            {
                return;
            }

            var leaving = new List<(int, int)>();
            var known = new HashSet<(int, int)>();
            foreach (var edge in seen.SelectMany(face => face.Edges))
            {
                if (known.Add(edge))
                {
                    leaving.Add(edge);
                }
            }

            _faces.RemoveAll(seen.Contains);
            // An edge is on the rim of what was seen when no seen face runs it back the other way.
            foreach (var (a, b) in leaving)
            {
                if (!known.Contains((b, a)))
                {
                    _faces.Add(Face.Through(_points, a, b, index));
                }
            }
        }

        // The corners and edges where two faces meet at an angle.
        public ConvexOutline Outline()
        {
            var sharp = new List<(int, int)>();
            var beside = new Dictionary<(int, int), Face>();
            foreach (var face in _faces)
            {
                foreach (var (a, b) in face.Edges)
                {
                    var edge = a < b ? (a, b) : (b, a);
                    if (!beside.Remove(edge, out var other))
                    {
                        beside[edge] = face;
                    }
                    else if (other.Normal.Dot(face.Normal) < 1 - 1e-9)
                    {
                        sharp.Add(edge);
                    }
                }
            }

            var where = new Dictionary<int, int>();
            var corners = new List<Vector3>();
            int CornerOf(int point)
            {
                if (!where.TryGetValue(point, out var found))
                {
                    corners.Add(_points[point]);
                    found = corners.Count - 1;
                    where[point] = found;
                }

                return found;
            }

            var edges = sharp.Select(edge => (CornerOf(edge.Item1), CornerOf(edge.Item2))).ToList();
            return new ConvexOutline(corners, edges);
        }
    }
}
