using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Panels;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>
/// The rows for each convex part of a compound body, edited through the same undo history as the body
/// that holds them.
/// </summary>
internal static class BodyPartRows
{
    /// <param name="rows">The body the parts are on.</param>
    /// <param name="body">The body as it is now, which says how many parts there are.</param>
    /// <param name="hull">The points a part starts with when it becomes a hull.</param>
    public static IEnumerable<Control> Build(ComponentRows<BodyComponent> rows, BodyComponent body, IReadOnlyList<double> hull)
    {
        var add = Fields.Button("Add part", "Add another convex part to this body.", () => rows.Fields.Host.Apply(() => rows.Change(BodyPartsModel.Added)));
        add.IsEnabled = body.Parts.Count < BodyPartsModel.MostParts;
        return [.. body.Parts.SelectMany((part, index) => new PartRows(rows, hull, index, part).Build()), add];
    }
}

file sealed class PartRows(ComponentRows<BodyComponent> rows, IReadOnlyList<double> hull, int index, BodyPart part)
{
    private static readonly DragOptions Positive = new() { Minimum = 0.01 };
    private static readonly DragOptions Degrees = new() { Step = 1 };

    private Fields Fields => rows.Fields;

    public List<Control> Build() =>
    [
        .. BodyPartsModel.ShapeRows().Select((labels, row) => Fields.Choice(
            row == 0 ? $"Part {index + 1}" : "",
            labels,
            BodyPartsModel.LabelOf(part),
            label => Edit(current => BodyPartsModel.Reshaped(current, BodyModel.ShapeOf(label), hull)))),
        .. Dimensions(),
        Drag("Part centre", current => Values(current.Centre), (current, values) => current.CopyWith(centre: Vector(values))),
        Drag("Part scale", current => Values(current.Scale), (current, values) => current.CopyWith(scale: Vector(values)), Positive),
        Drag("Part turn", current => BodyPartsModel.Angles(current.Rotation), (current, values) => current.CopyWith(rotation: BodyPartsModel.Rotation(values)), Degrees),
        Remove(),
    ];

    private Button Remove() => Fields.Button(
        $"Remove part {index + 1}",
        "Take this part off the body.",
        () => Fields.Host.Apply(() => rows.Change(current => BodyPartsModel.Removed(current, index))));

    private List<Control> Dimensions()
    {
        List<Control> dimensions = [];
        if (part.Shape == BodyShape.Box)
        {
            dimensions.Add(Drag("Part size", current => Values(current.Size), (current, values) => current.CopyWith(size: Vector(values)), Positive));
        }

        if (part.Shape is BodyShape.Sphere or BodyShape.Capsule or BodyShape.Cylinder)
        {
            dimensions.Add(Drag("Part radius", current => [current.Radius], (current, values) => current.CopyWith(radius: values[0]), Positive));
        }

        if (part.Shape is BodyShape.Capsule or BodyShape.Cylinder)
        {
            dimensions.Add(Drag("Part height", current => [current.Height], (current, values) => current.CopyWith(height: values[0]), Positive));
        }

        return dimensions;
    }

    private static double[] Values(Vector3 vector) => [vector.X, vector.Y, vector.Z];

    private static Vector3 Vector(double[] values) => new(values[0], values[1], values[2]);

    // The row keeps what it last showed, because taking the part off refreshes it once before it is rebuilt.
    private Control Drag(string label, Func<BodyPart, double[]> read, Func<BodyPart, double[], BodyPart> change, DragOptions? options = null)
    {
        var shown = read(part);
        return Fields.Drag(
            label,
            () =>
            {
                if (rows.Current is { } body && index < body.Parts.Count)
                {
                    shown = read(body.Parts[index]);
                }

                return shown;
            },
            values => Edit(current => change(current, values)),
            options);
    }

    // A part that is gone by the time a stale row fires has nothing to change.
    private void Edit(Func<BodyPart, BodyPart> change)
    {
        if (rows.Current is { } body && index < body.Parts.Count)
        {
            rows.Change(current => BodyPartsModel.Edited(current, index, change));
        }
    }
}
