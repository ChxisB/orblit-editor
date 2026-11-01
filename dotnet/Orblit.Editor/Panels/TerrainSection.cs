using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;
using Orblit.Terrain;

/// <summary>
/// The inspector's section for a terrain: how it takes shadows, which regions of ground it has, the
/// sets it is painted with, and how bare ground chooses between them.
/// </summary>
/// <remarks>
/// The shaping itself is the terrain mode's. This is what a terrain is made of, which is changed now
/// and then rather than stroked all afternoon.
/// </remarks>
internal static class TerrainSection
{
    private const double CellSize = 20;

    public static bool AppliesTo(InspectorTarget target) => ComponentOf(target) is not null;

    public static Control Build(Fields fields, InspectorTarget target, TerrainBench bench)
    {
        if (ComponentOf(target) is not { } component)
        {
            return new StackPanel();
        }

        var open = bench.TerrainFor(component.File);
        var rows = new List<Control> { fields.Section("Terrain", About(fields, target, component, open)) };
        if (open is not null)
        {
            var sets = new TerrainSets(fields, bench, open);
            rows.Add(fields.Section("Regions", Regions(fields, target, bench, open)));
            rows.Add(fields.Section("Sets", sets.Cards()));
            rows.Add(fields.Section("Automatic cover", sets.Automatic()));
        }

        return Fields.Column(rows);
    }

    private static TerrainComponent? ComponentOf(InspectorTarget target) =>
        PhysicsComponents.Of<TerrainComponent>(target.Item, SceneComponents.Terrain);

    private static StackPanel About(Fields fields, InspectorTarget target, TerrainComponent component, OpenTerrain? open)
    {
        var rows = new List<Control> { Fields.Text("File", component.File ?? "None") };
        if (open is null)
        {
            var problem = Fields.Note(component.File is null
                ? "Names no terrain file, so there is no ground to show."
                : "Could not be read. The console says why.");
            problem.Name = "terrain_problem";
            rows.Add(problem);
        }

        rows.Add(Shadows(fields, target, component, "Cast shadows", component.CastShadows, on => new TerrainComponent(component.File, on, component.ReceiveShadows)));
        rows.Add(Shadows(fields, target, component, "Take shadows", component.ReceiveShadows, on => new TerrainComponent(component.File, component.CastShadows, on)));
        return Fields.Column(rows);
    }

    private static Control Shadows(Fields fields, InspectorTarget target, TerrainComponent from, string label, bool current, Func<bool, TerrainComponent> to) =>
        InspectorSections.OffOn(
            fields,
            label,
            current,
            on => new SetObjectComponent(target.SceneId, target.Item.Id, $"Set {target.Item.Name} shadows", SceneComponents.Terrain, from, to(on)));

    private static StackPanel Regions(Fields fields, InspectorTarget target, TerrainBench bench, OpenTerrain open)
    {
        var caption = Fields.Note(TerrainSectionModel.RegionsCaption(open.Terrain));
        caption.Name = "regions_caption";
        return Fields.Column([caption, Map(fields, target, bench, open)]);
    }

    // Scaled down rather than scrolled, so a wide terrain is still all in view.
    private static Viewbox Map(Fields fields, InspectorTarget target, TerrainBench bench, OpenTerrain open)
    {
        var span = TerrainSectionModel.SpanOf(open.Terrain.Regions.Select(region => region.Key));
        var grid = new Grid { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        for (var column = 0; column < span.Columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        for (var row = 0; row < span.Rows; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var column = 0; column < span.Columns; column++)
            {
                var cell = Cell(fields, target, bench, open, new RegionKey(span.X0 + column, span.Z0 + row));
                Grid.SetColumn(cell, column);
                Grid.SetRow(cell, row);
                grid.Children.Add(cell);
            }
        }

        return new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = grid };
    }

    private static Border Cell(Fields fields, InspectorTarget target, TerrainBench bench, OpenTerrain open, RegionKey key)
    {
        var had = open.Terrain.RegionAt(key);
        var cell = new Border
        {
            Name = $"region_{Coordinate(key.X)}_{Coordinate(key.Z)}",
            Width = CellSize,
            Height = CellSize,
            Margin = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            BorderThickness = new Thickness(1),
            // The origin is marked so the map can be told apart from its mirror image.
            BorderBrush = key is { X: 0, Z: 0 } ? OrblitColors.Ember : OrblitColors.LineSoft,
            Background = had is null ? OrblitColors.Raised : OrblitColors.InkDim,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        ToolTip.SetTip(cell, $"{key.X}, {key.Z}");
        cell.PointerPressed += (_, _) => fields.Host.Apply(() => fields.Host.History.Run(
            new TerrainRegionEdit(bench, open.File, had ?? new TerrainRegion(key, open.Terrain.RegionSize), adding: had is null)));
        return cell;
    }

    // A minus sign is not allowed in a control's name.
    private static string Coordinate(long value) => value < 0 ? $"n{-value}" : $"{value}";
}
