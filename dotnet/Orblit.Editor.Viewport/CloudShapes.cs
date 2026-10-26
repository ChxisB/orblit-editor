namespace Orblit.Editor.Viewport;

using System.Diagnostics;
using Orblit.Filament;
using Orblit.Weather;

/// <summary>What each kind of cloud looks like to the renderer.</summary>
public static class CloudShapes
{
    public static Clouds Of(CloudKind kind) => kind switch
    {
        CloudKind.None => Clouds.None,
        CloudKind.Cumulus => Clouds.Cumulus,
        CloudKind.Stratocumulus => Clouds.Stratocumulus,
        CloudKind.Stratus => Clouds.Stratus,
        CloudKind.Cirrus => Clouds.Cirrus,
        CloudKind.Cumulonimbus => Clouds.Cumulonimbus,
        _ => throw new UnreachableException(),
    };

    /// <summary>
    /// Where a shape starts out, read off the shape and not written down twice. Null is automatic and
    /// starts where no shape is.
    /// </summary>
    public static double StartHeight(CloudKind? kind) => Of(kind ?? CloudKind.None).Altitude;
}
