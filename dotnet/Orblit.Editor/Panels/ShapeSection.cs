using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Mesh;

/// <summary>
/// What a shape was made from, where it was drawn, and where it begins and ends. What is done to its
/// geometry now is in the modelling panel, and this points there.
/// </summary>
/// <remarks>
/// In the inspector because these belong to this object, like its position. A crate and the room it
/// stands in want different answers, and neither is a thing somebody does fifty times in a row.
/// </remarks>
internal static class ShapeSection
{
    public static bool AppliesTo(InspectorTarget target) => target.Item.Kind == ObjectKind.Shape;

    public static Control Build(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        List<Control> parts = [];
        if (item.Outline is { } outline)
        {
            parts.Add(Drawn(fields, target, outline));
        }

        if (item.Shape is { } shape)
        {
            parts.Add(Parameters(fields, target, shape));
        }

        parts.Add(BoundarySection.Build(fields, target));
        parts.Add(Pointer(target));
        return Fields.Column(parts);
    }

    /// <summary>A sentence with a name a test can find it by.</summary>
    internal static TextBlock Say(string name, string text)
    {
        var note = Fields.Note(text);
        note.Name = name;
        return note;
    }

    private static Control Drawn(Fields fields, InspectorTarget target, PolyShape outline)
    {
        if (target.Item.Geometry is not null)
        {
            return fields.Section(
                "Drawn shape",
                Say("shape_edited", "Edited since it was drawn, so the outline no longer describes it. Its corners are still here if you undo back."));
        }

        var flip = Fields.Button(
            "Turn it over",
            "Reverse the direction this shape faces.",
            () => fields.Host.Apply(() => Redraw(target, new PolyShape(outline.Points, outline.Height, !outline.Flipped), live: false)));
        return fields.Section(
            "Drawn shape",
            Fields.Column(
            [
                Say("shape_corners", $"{outline.Points.Count} corners"),
                fields.Slider(
                    "Height",
                    outline.Height,
                    value => Redraw(target, new PolyShape(outline.Points, value, outline.Flipped), live: true),
                    new SliderOptions { Min = -8, Max = 8, Decimals = 2 }),
                flip,
            ]));
    }

    // A key per object and not per drag: a run ends when the host settles, which seals the history.
    private static void Redraw(InspectorTarget target, PolyShape to, bool live) => target.History.Run(
        new SetOutline(target.SceneId, target.Item.Id, target.Item.Name, to, live ? $"outline/{target.Item.Id}" : null));

    private static Control Parameters(Fields fields, InspectorTarget target, Shape shape)
    {
        var rows = ShapeParameters.For(shape).Select(row => Row(fields, target, shape, row)).ToList();
        if (!target.Item.IsParametric)
        {
            // Changing one now would throw the geometry away, so the fields are shown and do nothing.
            foreach (var row in rows)
            {
                row.IsEnabled = false;
            }

            rows.Insert(0, Say("shape_edited", "Edited. These are what it was made from. Changing one now would throw away the geometry."));
        }

        return fields.Section(shape.Kind.Label, Fields.Column(rows));
    }

    private static Control Row(Fields fields, InspectorTarget target, Shape shape, ShapeRow row) => row switch
    {
        ShapeNumber number => fields.Slider(
            number.Label,
            number.Read(shape),
            value => Reshape(target, current => number.Write(current, value)),
            number.Options),
        ShapeFlag flag => Flag(fields, target, shape, flag),
        _ => throw new UnreachableException(),
    };

    private static Control Flag(Fields fields, InspectorTarget target, Shape shape, ShapeFlag flag)
    {
        var (off, on) = flag.Names is [var first, var second] ? (first, second) : ("Off", "On");
        return fields.Choice(
            flag.Label,
            [off, on],
            flag.Read(shape) ? on : off,
            chosen => Reshape(target, current => flag.Write(current, chosen == on)));
    }

    // Read again at each step, so two rows changed before a rebuild do not undo each other.
    private static void Reshape(InspectorTarget target, Func<Shape, Shape> change)
    {
        var item = target.Item;
        if (item.Shape is { } was)
        {
            target.History.Run(new SetShape(target.SceneId, item.Id, item.Name, change(was)));
        }
    }

    // Worth a line and a button: somebody who used to find extrude here will look here for it.
    private static StackPanel Pointer(InspectorTarget target)
    {
        List<Control> parts = [Say("shape_pointer", "Extruding, cutting, materials and export are in the Modelling tab at the top.")];
        if (target.OnOpenTools is { } open)
        {
            parts.Add(Fields.Button("Go to Modelling", "Open the workspace for changing this shape.", open));
        }

        var pointer = Fields.Column(parts);
        pointer.Margin = new Thickness(Space.Lg, Space.Sm, Space.Lg, Space.Lg);
        return pointer;
    }
}
