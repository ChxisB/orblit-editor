namespace Orblit.Editor.Core;

using System.Diagnostics;
using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>What shape an object's boundary is.</summary>
public enum BoundaryKind
{
    /// <summary>
    /// The shape itself. Correct for anything, however odd. A doorway is a hole you can walk
    /// through and not a wall you cannot.
    /// </summary>
    Mesh,

    /// <summary>
    /// The smallest upright box the shape fits in. Cheaper to test against, and what somebody
    /// wants for a crate, a lift or a trigger volume.
    /// </summary>
    Box,

    /// <summary>None at all: the object is drawn and nothing else.</summary>
    None,
}

public static class BoundaryKinds
{
    public static string Label(this BoundaryKind kind) => kind switch
    {
        BoundaryKind.Mesh => "Mesh",
        BoundaryKind.Box => "Box",
        BoundaryKind.None => "None",
        _ => throw new UnreachableException(),
    };

    public static string Hint(this BoundaryKind kind) => kind switch
    {
        BoundaryKind.Mesh => "Follows the shape, holes and all.",
        BoundaryKind.Box => "The smallest box it fits in.",
        BoundaryKind.None => "Nothing to hit, and nothing to click.",
        _ => throw new UnreachableException(),
    };

    /// <summary>The word the file writes for it.</summary>
    public static string Name(this BoundaryKind kind) => kind.ToString().ToLowerInvariant();
}

/// <summary>
/// Where an object begins and ends, as far as anything but the eye is concerned.
/// </summary>
/// <remarks>
/// Separate from the geometry because the two answer different questions. What somebody sees can
/// be a railing of forty thin bars. What they walk into is one slab.
/// </remarks>
public sealed class Boundary(BoundaryKind kind = BoundaryKind.Mesh, double padding = 0.0, Vector3? offset = null)
{
    public BoundaryKind Kind { get; } = kind;

    /// <summary>
    /// How far outside the shape it sits, in metres. Negative pulls it inside, which is how
    /// somebody makes a boundary that ignores a decorative shell.
    /// </summary>
    public double Padding { get; } = padding;

    /// <summary>
    /// Where it sits relative to the object, for the times the shape is not centred on what it
    /// is meant to be.
    /// </summary>
    public Vector3 Offset { get; } = offset ?? Vector3.Zero();

    public bool IsNothing => Kind == BoundaryKind.None;

    public Boundary CopyWith(BoundaryKind? kind = null, double? padding = null, Vector3? offset = null) =>
        new(kind ?? Kind, padding ?? Padding, offset ?? Offset);

    /// <summary>
    /// The box this boundary occupies, given the shape it belongs to. <paramref name="natural"/> is
    /// what the object occupies with no boundary settings at all. It is the broad phase whichever
    /// kind this is, because most rays miss.
    /// </summary>
    public (Vector3 Min, Vector3 Max) BoxFrom((Vector3 Min, Vector3 Max) natural)
    {
        var grow = Vector3.All(Padding);
        return (natural.Min + Offset - grow, natural.Max + Offset + grow);
    }

    /// <summary>
    /// The geometry a ray is tested against, or null when the boundary is a box or there is
    /// nothing to build one from. Rebuilt and not cached, because whoever holds it knows when the
    /// shape changed.
    /// </summary>
    public Mesh? MeshFrom(Mesh? shape)
    {
        if (Kind != BoundaryKind.Mesh || shape is null || shape.IsEmpty)
        {
            return null;
        }

        var grown = Padding == 0 ? shape.Copy() : shape.Grown(Padding);
        if (Offset.Length2 > 0)
        {
            foreach (var at in grown.Positions)
            {
                at.Add(Offset);
            }
        }

        return grown;
    }

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>();
        if (Kind != BoundaryKind.Mesh)
        {
            json["kind"] = Kind.Name();
        }

        if (Padding != 0)
        {
            json["pad"] = Padding;
        }

        if (Offset.Length2 > 0)
        {
            json["at"] = new List<object?> { Offset.X, Offset.Y, Offset.Z };
        }

        return json;
    }

    public static Boundary? FromJson(object? value)
    {
        if (value is not IReadOnlyDictionary<string, object?> map)
        {
            return null;
        }

        return new Boundary(
            Enum.GetValues<BoundaryKind>().FirstOrDefault(one => one.Name() == map.GetValueOrDefault("kind") as string),
            ToDouble(map.GetValueOrDefault("pad")) ?? 0,
            At(map.GetValueOrDefault("at")));
    }

    private static Vector3? At(object? raw)
    {
        if (raw is not IReadOnlyList<object?> list || list.Count < 3)
        {
            return null;
        }

        var x = ToDouble(list[0]);
        var y = ToDouble(list[1]);
        var z = ToDouble(list[2]);
        return x is null || y is null || z is null || list.Skip(3).Any(one => ToDouble(one) is null)
            ? null
            : new Vector3(x.Value, y.Value, z.Value);
    }

    private static double? ToDouble(object? value) => value switch
    {
        long whole => whole,
        double real => real,
        _ => null,
    };
}
