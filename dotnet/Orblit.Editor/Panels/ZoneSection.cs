using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>
/// The section for a zone: gravity and drag for what is inside a body.
/// </summary>
internal static class ZoneSection
{
    private static readonly string[] Owners = ["Same", "Own"];

    /// <summary>
    /// A body that stays where it is, because the body is the region and the solver cannot hold a free
    /// one in place. A zone a file already put on a free body is still shown, so it can be seen and
    /// taken off.
    /// </summary>
    public static bool AppliesTo(InspectorTarget target) =>
        PhysicsComponents.ZoneOf(target.Item) is not null
        || (PhysicsComponents.PhysicsBodyOf(target.Item) is { } body && !PhysicsComponents.MovesFreely(body));

    public static Control Build(Fields fields, InspectorTarget target)
    {
        var name = target.Item.Name;
        var rows = new ComponentRows<ZoneComponent>(fields, target, SceneComponents.Zone);
        if (rows.Current is not { } zone)
        {
            return fields.Section(
                "Zone",
                rows.Add("Add zone", "Change gravity and drag for what is inside this body.", new ZoneComponent(), $"Add zone to {name}"));
        }

        var damping = new DragOptions { Minimum = 0, Step = 0.05 };
        return fields.Section(
            "Zone",
            Fields.Column(
            [
                .. Field(rows, zone, ZoneModel.Gravity, () => rows.Drags(
                    "Pull",
                    z => z.Gravity is { } pull ? [pull.X, pull.Y, pull.Z] : [0, 0, 0],
                    (z, values) => z.CopyWith(gravity: new Vector3(values[0], values[1], values[2])),
                    new DragOptions { Step = 0.1 })),
                .. Field(rows, zone, ZoneModel.Drag, () => rows.Drag("Amount", z => z.LinearDamping ?? 0, (z, value) => z.CopyWith(linearDamping: value), damping)),
                .. Field(rows, zone, ZoneModel.SpinDrag, () => rows.Drag("Amount", z => z.AngularDamping ?? 0, (z, value) => z.CopyWith(angularDamping: value), damping)),
                // A whole number, so a drag has to move by one to change anything: the row reads the
                // zone back at every step and rounds what it gets.
                rows.Drag(
                    "Priority",
                    z => z.Priority,
                    (z, value) => z.CopyWith(priority: (long)Math.Round(value, MidpointRounding.AwayFromZero)),
                    new DragOptions { Step = 1, Decimals = 0 }),
                rows.Remove("Remove zone", "Stop changing how bodies move inside this one.", $"Remove zone from {name}"),
            ]));
    }

    /// <summary>A field the zone may leave to the body inside, with <paramref name="value"/> under it when it is the zone's.</summary>
    private static List<Control> Field(ComponentRows<ZoneComponent> rows, ZoneComponent zone, ZoneField field, Func<Control> value)
    {
        var own = field.Owns(zone);
        List<Control> shown =
        [
            rows.Choice(field.Label, Owners, z => field.Owns(z) ? "Own" : "Same", (z, option) => option == "Own" ? field.Take(z) : field.Leave(z)),
        ];
        if (own)
        {
            shown.Add(value());
        }

        return shown;
    }
}
