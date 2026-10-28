using System.Globalization;
using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.VectorMath;

/// <summary>
/// Where an object begins and ends, as far as anything but the eye is concerned.
/// </summary>
internal static class BoundarySection
{
    private static readonly string[] Axes = ["X", "Y", "Z"];

    public static Control Build(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        var boundary = item.Boundary;
        var noGeometry = boundary.Kind == BoundaryKind.Mesh && item.CurrentMesh is null;
        List<Control> parts =
        [
            fields.Choice(
                "Shape",
                [.. Enum.GetValues<BoundaryKind>().Select(one => one.Label())],
                boundary.Kind.Label(),
                chosen => Change(target, item.Boundary.CopyWith(kind: Enum.GetValues<BoundaryKind>().First(one => one.Label() == chosen)), live: false)),
            ShapeSection.Say("boundary_hint", noGeometry ? "No geometry here, so this falls back to the box." : boundary.Kind.Hint()),
        ];

        if (!boundary.IsNothing)
        {
            parts.Add(fields.Slider(
                "Padding",
                boundary.Padding,
                value => Change(target, item.Boundary.CopyWith(padding: value), live: true),
                new SliderOptions { Min = -0.5, Max = 1, Decimals = 3 }));
            parts.AddRange(Axes.Select((axis, index) => fields.Slider(
                $"Offset {axis}",
                At(boundary.Offset, index),
                value => Change(target, item.Boundary.CopyWith(offset: With(item.Boundary.Offset, index, value)), live: true),
                new SliderOptions { Min = -5, Max = 5, Decimals = 3 })));
            parts.Add(Size(target, boundary));
        }

        return fields.Section("Boundary", Fields.Column(parts));
    }

    // A key per object and not per drag: a run ends when the host settles, which seals the history.
    private static void Change(InspectorTarget target, Boundary to, bool live) => target.History.Run(
        new SetBoundary(target.SceneId, target.Item.Id, target.Item.Name, to, live ? $"boundary/{target.Item.Id}" : null));

    private static TextBlock Size(InspectorTarget target, Boundary boundary)
    {
        var box = boundary.BoxFrom(target.Item.LocalBounds(target.BoundsOf?.Invoke(target.Item)));
        var size = box.Max - box.Min;
        string Metres(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
        return new TextBlock
        {
            Name = "boundary_size",
            Text = $"{Metres(size.X)} × {Metres(size.Y)} × {Metres(size.Z)} m",
            FontFamily = OrblitText.Mono,
            FontSize = 10.5,
            Foreground = OrblitColors.InkDim,
        };
    }

    private static double At(Vector3 at, int axis) => axis switch
    {
        0 => at.X,
        1 => at.Y,
        _ => at.Z,
    };

    private static Vector3 With(Vector3 at, int axis, double value) =>
        new(axis == 0 ? value : at.X, axis == 1 ? value : at.Y, axis == 2 ? value : at.Z);
}
