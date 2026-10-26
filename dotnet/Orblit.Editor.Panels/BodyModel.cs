namespace Orblit.Editor.Panels;

using Orblit.Scene;

/// <summary>What the body section decides about a body, apart from drawing its fields.</summary>
public static class BodyModel
{
    /// <summary>The most corners the physics world keeps of a hull.</summary>
    public const int KeptCorners = 255;

    /// <summary>Every shape, in the order the section lays them out.</summary>
    public static IReadOnlyList<KeyValuePair<BodyShape, string>> Shapes { get; } =
    [
        new(BodyShape.Box, "Box"),
        new(BodyShape.Sphere, "Ball"),
        new(BodyShape.Capsule, "Capsule"),
        new(BodyShape.Cylinder, "Cylinder"),
        new(BodyShape.Hull, "Hull"),
        new(BodyShape.Compound, "Compound"),
        new(BodyShape.Plane, "Ground"),
        new(BodyShape.Mesh, "Mesh"),
    ];

    public static IReadOnlyList<KeyValuePair<BodyMotion, string>> Motions { get; } =
    [
        new(BodyMotion.Fixed, "Fixed"),
        new(BodyMotion.Driven, "Driven"),
        new(BodyMotion.Free, "Free"),
        new(BodyMotion.Character, "Character"),
    ];

    /// <summary>The shape labels in rows of <paramref name="across"/>, because they do not fit in one across a narrow panel.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ShapeRows(int across = 3) =>
        [.. Shapes.Chunk(across).Select(row => (IReadOnlyList<string>)[.. row.Select(pair => pair.Value)])];

    public static string LabelOf(BodyShape shape) => Shapes.First(pair => pair.Key == shape).Value;

    public static BodyShape ShapeOf(string label) => Shapes.First(pair => pair.Value == label).Key;

    public static string LabelOf(BodyMotion motion) => Motions.First(pair => pair.Key == motion).Value;

    public static BodyMotion MotionOf(string label) => Motions.First(pair => pair.Value == label).Key;

    /// <summary>What a hull is made of. Its points are only in the file, so this says how many there are, or what is missing.</summary>
    public static string HullNote(int corners, bool hasVolume)
    {
        if (!hasVolume)
        {
            return "Needs four points that enclose a volume. Until then the body does nothing.";
        }

        return corners > KeptCorners
            ? $"{corners} corners. The simulation keeps the {KeptCorners} that stand out most."
            : $"{corners} corners.";
    }

    public static string MeshNote(int indices) => $"{indices / 3} triangles. Fixed, two-sided surface.";

    /// <summary><paramref name="body"/> with <paramref name="axis"/> held if it was free and free if it was held.</summary>
    public static BodyComponent Locked(BodyComponent body, BodyLock axis)
    {
        var locks = body.Locks.ToHashSet();
        if (!locks.Remove(axis))
        {
            locks.Add(axis);
        }

        return body.CopyWith(locks: locks);
    }
}
