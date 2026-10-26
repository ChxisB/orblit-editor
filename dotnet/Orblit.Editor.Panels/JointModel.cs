namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>What the joint section decides about a joint, apart from drawing its rows.</summary>
public static class JointModel
{
    /// <summary>Where a spring starts when it is switched on: slow enough to see and stiff enough to hold a body up.</summary>
    public const double FirstHertz = 3;

    /// <summary>A strength for a motor that is asked to hold a place and has none.</summary>
    public const double FirstStrength = 50;

    public static readonly JointGive NewGive = new(FirstHertz);

    private static readonly string[] Measured = ["Free", "Locked", "Range"];
    private static readonly string[] Roped = ["As placed", "Set", "Range"];
    private static readonly JointAxis[] Turns = [JointAxis.AboutX, JointAxis.AboutY, JointAxis.AboutZ];

    public static IReadOnlyList<KeyValuePair<JointKind, string>> Kinds { get; } =
    [
        new(JointKind.Fixed, "Fixed"),
        new(JointKind.Point, "Point"),
        new(JointKind.Hinge, "Hinge"),
        new(JointKind.Slider, "Slider"),
        new(JointKind.Distance, "Distance"),
        new(JointKind.Cone, "Cone"),
        new(JointKind.SixAxis, "Six axes"),
        new(JointKind.Gear, "Gear"),
        new(JointKind.Rack, "Rack"),
        new(JointKind.Pulley, "Pulley"),
        new(JointKind.Path, "Path"),
    ];

    public static string LabelOf(JointKind kind) => Kinds.First(pair => pair.Key == kind).Value;

    /// <summary>
    /// A kind that needs points is given some to start from: a pulley with none is not one, and a
    /// path with one point has no direction.
    /// </summary>
    public static JointComponent Seeded(JointComponent joint) => joint.Kind switch
    {
        JointKind.Pulley when joint.Over.Count != 2 => joint.CopyWith(over: [new Vector3(0, 1, 0), new Vector3(0, 1, 1)]),
        JointKind.Path when joint.Path.Count < 2 => joint.CopyWith(path: [Vector3.Zero(), new Vector3(1, 0, 0)]),
        _ => joint,
    };

    /// <summary>
    /// Which body the joint on <paramref name="id"/> holds to which, in the names the outliner uses.
    /// The answer comes from where the entity sits, so it is invisible until the scene runs.
    /// </summary>
    /// <returns>What to say, and whether there is no body to hold at all.</returns>
    public static (string Text, bool Nothing) Holds(EditorScene scene, string id)
    {
        string NameOf(string found) => scene[found]?.Name ?? found;
        return PhysicsComponents.JointEndsIn(scene, id) switch
        {
            (null, _) => ("Nothing: no body at or above it", true),
            ({ } body, null) => ($"{NameOf(body)} to the world", false),
            ({ } body, { } holder) => ($"{NameOf(body)} to {NameOf(holder)}", false),
        };
    }

    /// <summary>
    /// The names of the three ways one limit goes. A distance joint with no limit is not free but a
    /// rod as long as it was placed, so it says so, and a set length is what locked means there.
    /// </summary>
    public static string[] Modes(JointKind kind) => kind == JointKind.Distance ? Roped : Measured;

    /// <summary>Which of <see cref="Modes"/> a limit is in: none, one value, or two.</summary>
    public static int ModeOf(JointRange? range) => range is null ? 0 : range.Locked ? 1 : 2;

    /// <summary><paramref name="joint"/> with <paramref name="axis"/> in the way that mode <paramref name="mode"/> of <see cref="Modes"/> names.</summary>
    /// <param name="placed">How far the joint is from the middle of its body, for a distance.</param>
    public static JointComponent Moded(JointComponent joint, JointAxis axis, int mode, double placed)
    {
        var distance = joint.Kind == JointKind.Distance;
        return mode switch
        {
            1 => joint.Limit(axis, JointRange.At(distance ? placed : 0)),
            2 => joint.Limit(axis, RangeFor(axis, distance, placed)),
            _ => joint.Free(axis),
        };
    }

    // A rope as long as it was placed, a quarter turn either way, or half a metre either way.
    private static JointRange RangeFor(JointAxis axis, bool distance, double placed)
    {
        if (distance)
        {
            return new JointRange(0, placed);
        }

        return JointAxes.Turns(axis) ? new JointRange(-45, 45) : new JointRange(-0.5, 0.5);
    }

    /// <summary>
    /// <paramref name="values"/> as a range, with whichever end was dragged stopped at the other
    /// rather than passing it.
    /// </summary>
    public static JointRange Ordered(JointComponent joint, JointAxis axis, IReadOnlyList<double> values)
    {
        var (low, high) = (values[0], values[1]);
        if (low <= high)
        {
            return new JointRange(low, high);
        }

        var lowMoved = low != (joint.Limits.TryGetValue(axis, out var range) ? range.Low : double.NaN);
        return lowMoved ? new JointRange(high, high) : new JointRange(low, low);
    }

    /// <summary>
    /// How far the joint on <paramref name="id"/> is from the middle of the body it holds, which is
    /// the length a distance joint with no limit keeps.
    /// </summary>
    public static double PlacedLength(EditorScene scene, string id)
    {
        var body = PhysicsComponents.JointEndsIn(scene, id).Body;
        if (body is null || PhysicsComponents.BodyMiddleIn(scene, body) is not { } middle)
        {
            return 1;
        }

        var length = (middle - scene.WorldOf(id).GetTranslation()).Length;
        // Rounded to the centimetre the field shows, so what is written is what is read.
        return length < 0.01 ? 1 : Math.Round(length * 100, MidpointRounding.AwayFromZero) / 100;
    }

    /// <summary>What a limit is called on this kind of joint: a hinge's turn, a slider's travel, a rope's length.</summary>
    public static string LimitLabel(JointKind kind, JointAxis axis) => (kind, axis) switch
    {
        (JointKind.Hinge, _) => "Turn",
        (JointKind.Slider, _) => "Travel",
        (JointKind.Distance, _) => "Length",
        (JointKind.Cone, _) => "Twist",
        (_, JointAxis.AlongX) => "Along x",
        (_, JointAxis.AlongY) => "Along y",
        (_, JointAxis.AlongZ) => "Along z",
        (_, JointAxis.AboutX) => "About x",
        (_, JointAxis.AboutY) => "About y",
        _ => "About z",
    };

    /// <summary>Whether any way is limited to a range, which is where the ends can be soft. A lock stays hard and a free way has no end.</summary>
    public static bool HasRange(JointComponent joint) =>
        joint.Axes.Any(axis => joint.Limits.TryGetValue(axis, out var range) && !range.Locked);

    /// <summary>
    /// The ways a spring may be put on: a joint's own limited ways, and for the kinds that limit none,
    /// all six for a weld, which then gives in every way, the turns of a ball, which nothing else
    /// holds, and the tie of a gear, a rack or a pulley.
    /// </summary>
    public static IReadOnlyList<JointAxis> GiveAxes(JointComponent joint) => joint.Kind switch
    {
        JointKind.Fixed => Enum.GetValues<JointAxis>(),
        JointKind.Point => Turns,
        JointKind.Gear or JointKind.Rack or JointKind.Pulley => [JointAxis.AlongX],
        _ => joint.Axes,
    };

    /// <summary>What a spring is called: one word where a joint has one way to give, and which way where it has several.</summary>
    public static string SpringLabel(JointComponent joint, JointAxis axis) =>
        GiveAxes(joint).Count == 1 ? "Spring" : $"Spring {LimitLabel(joint.Kind, axis).ToLowerInvariant()}";

    /// <summary>Whether the joint's body can be driven: a hinge turns it, a slider and a path move it.</summary>
    public static bool IsDriven(JointKind kind) => kind is JointKind.Hinge or JointKind.Slider or JointKind.Path;

    /// <summary>
    /// <paramref name="joint"/> driven to a place or at a speed. A motor with no strength does
    /// nothing, so a place is given one to start from rather than left to hold nothing.
    /// </summary>
    public static JointComponent Driven(JointComponent joint, bool toPlace) =>
        toPlace
            ? joint.CopyWith(goalHertz: FirstHertz, strength: joint.Strength > 0 ? joint.Strength : FirstStrength)
            : joint.CopyWith(goalHertz: 0);

    /// <summary>The next point of a path: as far past the last as the last is past the one before, a metre along x for a path of one, or the origin for none.</summary>
    public static Vector3 Beyond(IReadOnlyList<Vector3> points) => points.Count switch
    {
        0 => Vector3.Zero(),
        1 => points[0] + new Vector3(1, 0, 0),
        _ => (points[^1] * 2) - points[^2],
    };

    /// <summary>Point <paramref name="index"/> of <paramref name="points"/> as numbers, nought where there is no such point.</summary>
    public static double[] PointOf(IReadOnlyList<Vector3> points, int index) =>
        index < points.Count ? Numbers(points[index]) : [0, 0, 0];

    /// <summary>
    /// <paramref name="points"/> with point <paramref name="index"/> set to <paramref name="values"/>,
    /// and, when <paramref name="take"/> is given, padded with the origin or cut to that many points.
    /// </summary>
    public static IReadOnlyList<Vector3> WithPoint(IReadOnlyList<Vector3> points, int index, IReadOnlyList<double> values, int? take = null) =>
        [.. Enumerable.Range(0, take ?? points.Count).Select(at => at == index ? Vector(values) : Existing(points, at))];

    public static double[] Numbers(Vector3 vector) => [vector.X, vector.Y, vector.Z];

    public static Vector3 Vector(IReadOnlyList<double> values) => new(values[0], values[1], values[2]);

    private static Vector3 Existing(IReadOnlyList<Vector3> points, int index) => index < points.Count ? points[index] : Vector3.Zero();
}
