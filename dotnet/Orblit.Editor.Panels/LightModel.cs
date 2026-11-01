namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;
using Orblit.Light;

/// <summary>What the light section decides about its fields, apart from drawing them.</summary>
public static class LightModel
{
    /// <summary>
    /// What each light type is called, in the order they are offered: the two an artist reaches for
    /// first, then the two that need more said about them.
    /// </summary>
    public static readonly IReadOnlyList<KeyValuePair<LightType, string>> Names =
    [
        new(LightType.Point, "Point"),
        new(LightType.Sun, "Sun"),
        new(LightType.Spot, "Spot"),
        new(LightType.Area, "Area"),
    ];

    /// <summary>
    /// The same light, restated in the units of the type it is becoming.
    /// </summary>
    /// <remarks>
    /// Watts and watts per square metre are not interchangeable, and the ratio between them is the one
    /// the renderer already used: a point light's lumens spread over a sphere. Converting keeps the
    /// scene looking as it did, which is what somebody switching a type is expecting.
    /// </remarks>
    public static double PowerFor(LightType wanted, LightType from, double power)
    {
        const double Sphere = 4 * Math.PI;
        if (wanted == from)
        {
            return power;
        }

        if (wanted == LightType.Sun)
        {
            return power / Sphere;
        }

        return from == LightType.Sun ? power * Sphere : power;
    }

    /// <summary>The cone, its edge, the source size and the sun's width as the light has them now.</summary>
    public static LightShape ShapeOf(SceneObject light) =>
        new(light.SpotSize, light.SpotBlend, light.SourceRadius, light.SunAngle);

    /// <summary>The sentence under a light's fields, which says the one thing about its kind that the fields do not.</summary>
    public static string Note(LightType type) => type switch
    {
        LightType.Sun =>
            "Sun size is the width of the source in the sky. It is what makes a shadow crisp at your feet and soft at its end.",
        LightType.Area =>
            "An area light is drawn as a point of the same power at the centre of the shape. The falloff is right; the soft shadow its surface would cast is not.",
        _ =>
            "Radius is how big the source is, not how bright. Power stays the same and spreads over a wider surface, which is what widens the penumbra.",
    };
}
