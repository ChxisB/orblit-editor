namespace Orblit.Editor.Panels;

using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>
/// One field a zone may leave to the body inside: the body's own, or the zone's.
/// </summary>
/// <param name="Label">What the field is called in the inspector.</param>
/// <param name="Owns">Whether the zone decides it.</param>
/// <param name="Take">The zone deciding it, starting at what the body has, so nothing inside changes until it is dragged.</param>
/// <param name="Leave">The zone leaving it to the body again.</param>
public sealed record ZoneField(
    string Label,
    Func<ZoneComponent, bool> Owns,
    Func<ZoneComponent, ZoneComponent> Take,
    Func<ZoneComponent, ZoneComponent> Leave);

/// <summary>What the zone section decides about a zone, apart from drawing its fields.</summary>
public static class ZoneModel
{
    private static readonly BodyComponent Defaults = new();

    /// <summary>The world's gravity, which is where a zone's own starts.</summary>
    private static readonly Vector3 WorldGravity = new(0, -9.81, 0);

    // A field that is left out is left out of the file, so leaving one is a zone built without it:
    // copying cannot clear a field.
    public static ZoneField Gravity { get; } = new(
        "Gravity",
        zone => zone.Gravity is not null,
        zone => zone.CopyWith(gravity: WorldGravity.Clone()),
        zone => new ZoneComponent(
            linearDamping: zone.LinearDamping,
            angularDamping: zone.AngularDamping,
            priority: zone.Priority));

    public static ZoneField Drag { get; } = new(
        "Drag",
        zone => zone.LinearDamping is not null,
        zone => zone.CopyWith(linearDamping: Defaults.LinearDamping),
        zone => new ZoneComponent(
            gravity: zone.Gravity,
            angularDamping: zone.AngularDamping,
            priority: zone.Priority));

    public static ZoneField SpinDrag { get; } = new(
        "Spin drag",
        zone => zone.AngularDamping is not null,
        zone => zone.CopyWith(angularDamping: Defaults.AngularDamping),
        zone => new ZoneComponent(
            gravity: zone.Gravity,
            linearDamping: zone.LinearDamping,
            priority: zone.Priority));
}
