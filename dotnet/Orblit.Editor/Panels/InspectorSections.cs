using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.VectorMath;

/// <summary>
/// The sections the editor has always had, in the order they stack. Written against what a section is
/// given and not against the panel showing them, so each is registered the way a section from outside
/// would be.
/// </summary>
internal static class InspectorSections
{
    private static readonly string[] OffOnOptions = ["Off", "On"];

    public static IReadOnlyList<InspectorSection<Control>> BuiltIn(Fields fields, SurfaceCooks? surfaces = null, TerrainBench? terrain = null) =>
    [
        // Weather is everywhere at once, so it has no position to show.
        new("transform", IsObject, target => Transform(fields, target)),
        new("light", target => target.Item.Kind == ObjectKind.Light, target => LightSection.Build(fields, target)),
        new("mesh", target => target.Item.Kind == ObjectKind.Mesh, target => Mesh(fields, target)),
        new("weather", target => target.Item.Kind == ObjectKind.Weather, target => new WeatherSection(fields, target).Weather()),
        new("air", target => target.Item.Kind == ObjectKind.Weather, target => new WeatherSection(fields, target).Air()),
        new("body", BodySection.AppliesTo, target => BodySection.Build(fields, target)),
        new("zone", ZoneSection.AppliesTo, target => ZoneSection.Build(fields, target)),
        new("joint", JointSection.AppliesTo, target => JointSection.Build(fields, target)),
        new("vehicle", VehicleSection.AppliesToVehicle, target => VehicleSection.Vehicle(fields, target)),
        new("wheel", VehicleSection.AppliesToWheel, target => VehicleSection.Wheel(fields, target)),
        new("sound", IsObject, target => SoundSection.Build(fields, target)),
        new("particles", IsObject, target => ParticlesSection.Build(fields, target)),
        new("motion", MotionSection.AppliesTo, target => MotionSection.Build(fields, target)),
        .. ShaderSections(fields, surfaces),
        .. TerrainSections(fields, terrain),
        new("interface", target => target.Item.Kind == ObjectKind.Canvas, target => Interface(fields, target)),
        new("data", target => target.Item.Data.Count > 0, target => Data(fields, target)),
    ];

    // A window with no project has no shaders to cook, so it has no section for them.
    private static IEnumerable<InspectorSection<Control>> ShaderSections(Fields fields, SurfaceCooks? surfaces) =>
        surfaces is null
            ? []
            : [new("shader", ShaderSection.AppliesTo, target => ShaderSection.Build(fields, surfaces, target))];

    // A window with no project has no terrain files to open.
    private static IEnumerable<InspectorSection<Control>> TerrainSections(Fields fields, TerrainBench? terrain) =>
        terrain is null
            ? []
            : [new("terrain", TerrainSection.AppliesTo, target => TerrainSection.Build(fields, target, terrain))];

    /// <summary>Whether the target is something in the world, which the scene and the weather are not.</summary>
    private static bool IsObject(InspectorTarget target) => target.Item.Kind is not (ObjectKind.Scene or ObjectKind.Weather);

    /// <summary>A flag as Off and On side by side, which reads faster in a long list than a switch does.</summary>
    /// <param name="change">The command that sets the flag to what was chosen.</param>
    public static Control OffOn(Fields fields, string label, bool current, Func<bool, EditorCommand> change) =>
        fields.Choice(
            label,
            OffOnOptions,
            current ? "On" : "Off",
            chosen =>
            {
                var wanted = chosen == "On";
                if (wanted != current)
                {
                    fields.Host.History.Run(change(wanted));
                }
            });

    private static Control Transform(Fields fields, InspectorTarget target) => fields.Section(
        "Transform",
        Fields.Column(
        [
            Vector(fields, target, TransformField.Position, new DragOptions { Step = 0.02 }),
            Vector(fields, target, TransformField.Rotation, new DragOptions { Step = 0.5, Decimals = 1 }),
            // A floor, so scale cannot be dragged through zero into a matrix that cannot be inverted.
            Vector(fields, target, TransformField.Scale, new DragOptions { Step = 0.02, Minimum = 0.001 }),
        ]));

    /// <summary>The button that keys <paramref name="field"/>, or null where nothing is keying.</summary>
    public static KeyButton? KeyFor(InspectorTarget target, string field) =>
        target.Keying is { } keying ? new KeyButton(keying, target.Item, field) : null;

    private static Control Vector(Fields fields, InspectorTarget target, TransformField field, DragOptions options)
    {
        var item = target.Item;
        return fields.Drag(
            field.ToString(),
            () => Components(field.Of(item)),
            values => target.History.Run(new SetTransform(
                target.SceneId,
                item.Id,
                field,
                item.Name,
                field.Of(item),
                new Vector3(values[0], values[1], values[2]))),
            options with { Trailing = KeyFor(target, $"transform.{field.ToString().ToLowerInvariant()}") });
    }

    private static double[] Components(Vector3 vector) => [vector.X, vector.Y, vector.Z];

    private static Control Mesh(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        var history = target.History;
        return fields.Section(
            "Mesh renderer",
            Fields.Column(
            [
                fields.Palette("Base colour", item.Colour, value => history.Run(new SetColour(target.SceneId, item.Id, item.Name, item.Colour, value))),
                OffOn(fields, "Cast shadows", item.CastShadows, wanted => new SetCastShadows(target.SceneId, item.Id, item.Name, wanted)),
                OffOn(fields, "Receive shadows", item.ReceiveShadows, wanted => new SetReceiveShadows(target.SceneId, item.Id, item.Name, wanted)),
                // Nought for anything rigid, which is almost everything, so the row reads as off rather
                // than as a number somebody has to interpret. Only the weather decides how hard it
                // blows. This is how much this object answers.
                fields.Slider(
                    "Sway",
                    item.Sway,
                    value => history.Run(new SetSway(target.SceneId, item.Id, item.Name, item.Sway, value)),
                    new SliderOptions { Min = 0, Max = 1 }),
                Fields.Text("Mesh", item.MeshAsset ?? "cube (built in)"),
                Texture(fields, target),
                Fields.Note("A ground plane that casts shadows casts them onto itself, which is most of what makes a scene look dirty."),
            ]));
    }

    // Set by dropping a texture on the object, or opening one with the object selected, and only cleared
    // from here. A picker would be the obvious thing, and the browser is already a picker.
    private static Grid Texture(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        var shown = Fields.Text("Texture", item.MaterialAsset ?? "the mesh's own");
        if (item.MaterialAsset is null)
        {
            return shown;
        }

        var clear = Fields.Button(
            "Clear",
            "Go back to the texture the mesh came with.",
            () => fields.Host.Apply(() => target.History.Run(new SetMaterialAsset(target.SceneId, item.Id, item.Name, item.MaterialAsset, null))));
        clear.Name = "texture_clear";
        clear.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(clear, 2);
        clear.Margin = new Thickness(Space.Xs, 0, 0, 0);
        shown.Children.Add(clear);
        return shown;
    }

    // A reference and not a copy, which is the same rule as a mesh and a data object: the .oui is the
    // document, and two scenes showing the same one both change when it changes.
    private static Control Interface(Fields fields, InspectorTarget target)
    {
        var shown = target.Item.InterfaceAsset;
        if (shown is null)
        {
            return fields.Section("Interface", Fields.Note("Nothing yet. Drag a .oui from the project onto the viewport."));
        }

        var open = target.OnOpenInterface;
        return fields.Section(
            "Interface",
            Fields.Column(
            [
                InspectorBands.Link("interface_link", Path.GetFileName(shown), shown, open is null ? null : () => open(shown)),
                Fields.Note("Drawn over the scene. Hidden here hides it in the game too."),
            ]));
    }

    // Listed and not inlined: the values belong to the file, and showing them here as though they were
    // this object's own would invite somebody to change one and be surprised when thirty-nine other
    // objects changed with it. The link is what this object owns, and the values are edited where they
    // live, one click away.
    private static Control Data(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        var rows = item.Data.Select((path, index) => InspectorBands.DataLink(
            index,
            path,
            target.OnOpenData is { } open ? () => open(path) : null,
            target.OnDetachData is { } detach ? () => detach(item.Id, path) : null));
        return fields.Section(
            "Data",
            Fields.Column([.. rows, Fields.Note("Shared. Changing one of these changes it everywhere it is used.")]));
    }
}
