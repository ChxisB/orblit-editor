namespace Orblit.Editor.Core;

using Orblit.Mesh;

/// <summary>
/// One material a shape can wear, as the editor holds it.
/// </summary>
/// <remarks>
/// A slot on the object and not a file of its own. Somebody painting the treads of a staircase
/// grey is not making an asset, and asking them to name and save one first is the step that
/// stops them. It carries what a glb can carry and no more, because that is how it reaches the
/// renderer.
/// </remarks>
public sealed record Surface
{
    public string Name { get; init; } = "Material";

    /// <summary>
    /// sRGB, as somebody picked it. Converted on the way out, because the shading maths is
    /// linear.
    /// </summary>
    public Colour Colour { get; init; } = new(0xFFB9BEC6);

    /// <summary>
    /// How metal it is. Physically nought or one. The values between are for the edge of a
    /// scratch, where one pixel covers both.
    /// </summary>
    public double Metallic { get; init; }

    /// <summary>How scattered its reflections are, from a mirror at nought to diffuse at one.</summary>
    public double Roughness { get; init; } = 0.5;

    /// <summary>How much light it gives off, in its own colour.</summary>
    public double Emissive { get; init; }

    /// <summary>Whether it is lit from behind as well as in front, for anything one triangle thick.</summary>
    public bool DoubleSided { get; init; }

    /// <summary>This material as the file carries it.</summary>
    public GlbMaterial ToGlb()
    {
        var linear = Colour.ToLinear();
        return new GlbMaterial
        {
            Name = Name,
            Colour = [linear.X, linear.Y, linear.Z, Colour.A],
            Metallic = Metallic,
            Roughness = Roughness,
            Emissive = [linear.X * Emissive, linear.Y * Emissive, linear.Z * Emissive],
            DoubleSided = DoubleSided,
        };
    }

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["name"] = Name,
            ["colour"] = (long)Colour.Argb,
        };
        if (Metallic != 0)
        {
            json["metallic"] = Metallic;
        }

        if (Roughness != 0.5)
        {
            json["roughness"] = Roughness;
        }

        if (Emissive != 0)
        {
            json["emissive"] = Emissive;
        }

        if (DoubleSided)
        {
            json["twoSided"] = true;
        }

        return json;
    }

    public static Surface? FromJson(object? value)
    {
        if (value is not IReadOnlyDictionary<string, object?> map)
        {
            return null;
        }

        return new Surface
        {
            Name = map.GetValueOrDefault("name") as string ?? "Material",
            Colour = map.GetValueOrDefault("colour") is long argb ? new Colour((uint)argb) : new Surface().Colour,
            Metallic = Number(map, "metallic", 0),
            Roughness = Number(map, "roughness", 0.5),
            Emissive = Number(map, "emissive", 0),
            DoubleSided = map.GetValueOrDefault("twoSided") is true,
        };
    }

    private static double Number(IReadOnlyDictionary<string, object?> map, string key, double fallback) =>
        map.GetValueOrDefault(key) switch
        {
            long whole => whole,
            double real => real,
            _ => fallback,
        };
}
