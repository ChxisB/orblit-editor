namespace Orblit.Editor.Panels;

using Orblit.Asset;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>What the body section decides about the parts of a compound body and a model collider, apart from drawing fields.</summary>
public static class BodyPartsModel
{
    /// <summary>The most parts a compound body keeps.</summary>
    public const int MostParts = 64;

    private static readonly BodyShape[] PartShapes = [BodyShape.Box, BodyShape.Sphere, BodyShape.Capsule, BodyShape.Cylinder, BodyShape.Hull];

    /// <summary>The labels of the shapes a part can be, in rows of <paramref name="across"/>.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ShapeRows(int across = 3) =>
        [.. PartShapes.Select(BodyModel.LabelOf).Chunk(across).Select(row => (IReadOnlyList<string>)row)];

    /// <summary>A part with a shape the editor does not offer reads as a box.</summary>
    public static string LabelOf(BodyPart part) => BodyModel.LabelOf(PartShapes.Contains(part.Shape) ? part.Shape : BodyShape.Box);

    /// <summary>
    /// <paramref name="part"/> with <paramref name="shape"/>. A hull with no points of its own starts
    /// as <paramref name="hull"/>, so that choosing one gives a part that works.
    /// </summary>
    public static BodyPart Reshaped(BodyPart part, BodyShape shape, IReadOnlyList<double> hull) =>
        part.CopyWith(shape: shape, hull: shape == BodyShape.Hull && part.Hull.Count == 0 ? hull : part.Hull);

    public static BodyComponent Added(BodyComponent body) => body.CopyWith(parts: [.. body.Parts, new BodyPart()]);

    public static BodyComponent Removed(BodyComponent body, int index) =>
        body.CopyWith(parts: [.. body.Parts.Where((_, at) => at != index)]);

    public static BodyComponent Edited(BodyComponent body, int index, Func<BodyPart, BodyPart> change) =>
        body.CopyWith(parts: [.. body.Parts.Select((part, at) => at == index ? change(part) : part)]);

    /// <summary>A part's turn about x, y and z in degrees, which is how somebody thinks of it.</summary>
    public static double[] Angles(Quaternion rotation)
    {
        var q = rotation.Normalized();
        double[] radians =
        [
            Math.Atan2(2 * ((q.W * q.X) + (q.Y * q.Z)), 1 - (2 * ((q.X * q.X) + (q.Y * q.Y)))),
            Math.Asin(Math.Clamp(2 * ((q.W * q.Y) - (q.Z * q.X)), -1.0, 1.0)),
            Math.Atan2(2 * ((q.W * q.Z) + (q.X * q.Y)), 1 - (2 * ((q.Y * q.Y) + (q.Z * q.Z)))),
        ];
        return [.. radians.Select(angle => angle * 180 / Math.PI)];
    }

    /// <summary>The turn that <paramref name="degrees"/> about x, y and z, in that order, add up to. The other direction from <see cref="Angles"/>.</summary>
    public static Quaternion Rotation(IReadOnlyList<double> degrees)
    {
        static Quaternion About(Vector3 axis, double degrees) => Quaternion.AxisAngle(axis, degrees * Math.PI / 180);
        return About(new Vector3(0, 0, 1), degrees[2]) * About(new Vector3(0, 1, 0), degrees[1]) * About(new Vector3(1, 0, 0), degrees[0]);
    }

    /// <summary>
    /// <paramref name="body"/> as a fixed surface of <paramref name="geometry"/>, in the model's own
    /// frame. A fresh body when there is none.
    /// </summary>
    public static BodyComponent Collider(BodyComponent? body, CollisionMesh geometry) =>
        (body ?? new BodyComponent()).CopyWith(
            shape: BodyShape.Mesh,
            motion: BodyMotion.Fixed,
            meshVertices: geometry.Vertices,
            meshIndices: [.. geometry.Indices.Select(index => (long)index)],
            centre: Vector3.Zero(),
            shapeScale: Vector3.All(1));
}
