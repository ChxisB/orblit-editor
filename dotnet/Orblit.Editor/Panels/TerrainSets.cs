using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Terrain;

/// <summary>
/// The sets a terrain is painted with and how bare ground chooses between them, as the inspector's
/// terrain section shows them. Every change is one step on the undo stack, and a slider's drag is one.
/// </summary>
internal sealed class TerrainSets(Fields fields, TerrainBench bench, OpenTerrain open)
{
    private static readonly string[] WrapOptions = ["Stretch", "Wrap"];

    private List<TerrainSet> Held => open.Terrain.Sets;

    public Control Cards()
    {
        var rows = Held.Select((set, index) => (Control)Card(index, set)).ToList();
        var add = Fields.Button("Add set", "Add a ground material to paint with.", () => fields.Host.Apply(AddSet));
        add.IsEnabled = Held.Count < Cover.SetCount;
        // Only the last: the ground names a set by its place in the list.
        var remove = Fields.Button("Remove last", "Takes the last set away. Ground painted with it shows the one before.", () => fields.Host.Apply(RemoveLast));
        remove.IsEnabled = Held.Count > 1;
        Grid.SetColumn(remove, 1);
        rows.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Children = { add, remove } });
        return Fields.Column(rows);
    }

    public Control Automatic()
    {
        var cover = open.Terrain.AutoCover;
        var rows = new List<Control> { Fields.Note("What ground nobody has painted shows: one set where it is flat, another where it is steep.") };
        if (Held.Count > 0)
        {
            rows.Add(Choose("Flat", cover.Flat, index => EditCover("flat set", one => TerrainSectionModel.WithFlat(one, index))));
            rows.Add(Choose("Steep", cover.Steep, index => EditCover("steep set", one => TerrainSectionModel.WithSteep(one, index))));
        }

        rows.Add(fields.Slider(
            "Slope",
            cover.Slope,
            value => EditCover("slope", one => TerrainSectionModel.WithSlope(one, value), "slope"),
            new SliderOptions { Min = 0, Max = 4, Decimals = 2 }));
        rows.Add(fields.Slider(
            "By height",
            cover.HeightFalloff,
            value => EditCover("height", one => TerrainSectionModel.WithHeightFalloff(one, value), "height"),
            new SliderOptions { Min = 0, Max = 1, Decimals = 2 }));
        rows.Add(fields.Slider(
            "Sharpness",
            open.Terrain.BlendSharpness,
            value => bench.Edit(open, $"Set {open.Name} blend", settings => settings with { BlendSharpness = value }, "blend"),
            new SliderOptions { Min = 0, Max = 1, Decimals = 2 }));
        return Fields.Column(rows);
    }

    private Control Choose(string label, long current, Action<long> onSelect) => fields.Pick(
        label,
        TerrainSectionModel.CoverShown(Held, current),
        Held.Select((set, index) => KeyValuePair.Create((long)index, set.Name)),
        index =>
        {
            if (index != current)
            {
                onSelect(index);
            }
        });

    private void EditCover(string what, Func<AutoCover, AutoCover> change, object? gesture = null) =>
        bench.Edit(open, $"Set {open.Name} {what}", settings => settings with { AutoCover = change(settings.AutoCover) }, gesture);

    private void AddSet() => bench.Edit(open, $"Add a set to {open.Name}", TerrainSectionModel.AddSet);

    private void RemoveLast() => bench.Edit(open, $"Remove {Held[^1].Name} from {open.Name}", TerrainSectionModel.RemoveLast);

    private void EditSet(int index, string what, Func<TerrainSet, TerrainSet> change, object? gesture = null) =>
        bench.Edit(
            open,
            $"Set {Held[index].Name} {what}",
            settings => TerrainSectionModel.WithSet(settings, index, change(settings.Sets[index])),
            gesture);

    private Border Card(int index, TerrainSet set)
    {
        var heading = new TextBlock
        {
            Name = $"set_heading_{index}",
            Text = TerrainSectionModel.SetHeading(index, set),
            FontSize = OrblitText.Label,
            Foreground = OrblitColors.Ink,
            Margin = new Thickness(0, 0, 0, Space.Xs),
        };
        return new Border
        {
            Name = $"set_card_{index}",
            Margin = new Thickness(0, 0, 0, Space.Sm),
            Padding = new Thickness(Space.Sm),
            CornerRadius = Radii.Control,
            Background = OrblitColors.Surface,
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(1),
            Child = Fields.Column(
            [
                heading,
                Picture("Colour", index, set.Albedo, "colour picture", TerrainSectionModel.WithAlbedo),
                Picture("Surface", index, set.Normal, "surface picture", TerrainSectionModel.WithNormal),
                fields.Slider(
                    "Repeats",
                    set.TileSize,
                    value => EditSet(index, "repeat", one => TerrainSectionModel.WithTileSize(one, value), $"tileSize:{index}"),
                    new SliderOptions { Min = 0.5, Max = 32, Decimals = 1, Unit = " m" }),
                fields.Choice(
                    "On cliffs",
                    WrapOptions,
                    set.Triplanar ? "Wrap" : "Stretch",
                    chosen =>
                    {
                        if ((chosen == "Wrap") != set.Triplanar)
                        {
                            EditSet(index, "cliffs", one => TerrainSectionModel.WithTriplanar(one, chosen == "Wrap"));
                        }
                    }),
            ]),
        };
    }

    // Shows the picture and takes it off. Putting one on waits for the project browser to drag files from.
    private Grid Picture(string label, int index, string? path, string what, Func<TerrainSet, string?, TerrainSet> put)
    {
        var key = Fields.Key(label);
        var shown = new TextBlock
        {
            Name = $"picture_{key}_{index}",
            Text = path is null ? "None" : Path.GetFileName(path),
            FontSize = OrblitText.Caption,
            FontFamily = OrblitText.Mono,
            Foreground = OrblitColors.InkDim,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Button? clear = null;
        if (path is not null)
        {
            clear = Fields.Button("Clear", "Take the picture off.", () => fields.Host.Apply(() => EditSet(index, what, one => put(one, null))));
            clear.Name = $"picture_clear_{key}_{index}";
        }

        return Fields.Row(label, shown, clear);
    }
}
