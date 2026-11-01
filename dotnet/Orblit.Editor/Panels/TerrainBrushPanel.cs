using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Terrain;

/// <summary>
/// The brush, as a place: which terrain it is working on, what the tool does and lays, and the
/// settings every tool shares.
/// </summary>
/// <remarks>
/// Not undoable, any of it. The brush is how somebody is working, like the grid a drag snaps to, and
/// not part of anything that is saved.
/// </remarks>
internal sealed class TerrainBrushPanel : UserControl
{
    private readonly TerrainBench _bench;
    private readonly Func<OpenTerrain?> _target;
    private readonly Action? _onNew;
    private readonly FieldHost _host;
    private readonly Fields _fields;
    private readonly StackPanel _stack = new() { Name = "brush_fields", Margin = new Thickness(0, Space.Sm) };

    /// <param name="bench">Holds the brush this edits.</param>
    /// <param name="history">What a field settles, though none of these makes a step.</param>
    /// <param name="target">The terrain a stroke would land on, or null when there is none.</param>
    /// <param name="onNew">Makes a terrain. Null where there is no way to, which leaves the button out.</param>
    public TerrainBrushPanel(TerrainBench bench, History history, Func<OpenTerrain?> target, Action? onNew = null)
    {
        _bench = bench;
        _target = target;
        _onNew = onNew;
        _host = new FieldHost(history, Rebuild);
        _fields = new Fields(_host);
        Background = OrblitColors.Surface;
        Content = new ScrollViewer { Name = "brush_scroll", Content = _stack };
        Rebuild();
    }

    /// <summary>Shows the terrain <c>target</c> says now. The owner calls it when the selection moves.</summary>
    public void Refresh() => _host.Changed();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Changed += _host.Changed;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bench.Changed -= _host.Changed;
        // A field that was being dragged gets no release once it is off the tree, and a hold left over would stop every rebuild.
        _host.Settle();
        base.OnDetachedFromVisualTree(e);
    }

    private void Rebuild()
    {
        _host.Reset();
        _stack.Children.Clear();
        _stack.Children.Add(TerrainSection());
        _stack.Children.Add(ToolSection());
        _stack.Children.Add(BrushSection());
    }

    private Control TerrainSection()
    {
        var rows = new List<Control>();
        if (_target() is not { } open)
        {
            var saying = Fields.Note(BrushPanelModel.NothingToShape);
            saying.Name = "brush_nothing";
            rows.Add(saying);
            if (_onNew is { } onNew)
            {
                rows.Add(Fields.Button("Add terrain", "Make ground to shape and paint in this scene.", onNew));
            }
        }
        else
        {
            rows.Add(new TextBlock { Name = "brush_terrain", Text = open.Name, FontSize = OrblitText.Label, Foreground = OrblitColors.Ink });
            var extent = Fields.Note(BrushPanelModel.Extent(open.Terrain));
            extent.Name = "brush_extent";
            rows.Add(extent);
        }

        return _fields.Section("Terrain", Fields.Column(rows));
    }

    private Control ToolSection()
    {
        var tool = _bench.Tool;
        var words = BrushWords.Of(tool);
        var hint = Fields.Note(words.Hint);
        hint.Name = "brush_hint";
        var rows = new List<Control> { hint };
        switch (tool)
        {
            case BrushTool.Cover:
                rows.Add(Sets());
                break;
            case BrushTool.Colour:
                rows.Add(_fields.Palette(
                    "Tint",
                    BrushPanelModel.TintOf(_bench.Colour),
                    picked => _bench.Colour = BrushPanelModel.Painted(_bench.Colour, picked),
                    BrushPanelModel.Tints));
                break;
            case BrushTool.Roughness:
                rows.Add(_fields.Slider(
                    "Rougher by",
                    _bench.Roughness,
                    value => _bench.Roughness = value,
                    new SliderOptions { Min = -1, Max = 1, Decimals = 2 }));
                break;
        }

        return _fields.Section(words.Label, Fields.Column(rows));
    }

    // One row each: there can be thirty-two sets, which is too many to share a row the way a choice of three does.
    private Control Sets()
    {
        var sets = _target()?.Terrain.Sets ?? [];
        if (sets.Count == 0)
        {
            var none = Fields.Note(BrushPanelModel.NoSets);
            none.Name = "brush_no_sets";
            return none;
        }

        var chosen = Math.Clamp(_bench.Set, 0, sets.Count - 1);
        return Fields.Column(sets.Select((set, index) => SetRow(index, set, index == chosen)));
    }

    private Border SetRow(int index, TerrainSet set, bool selected)
    {
        var name = new TextBlock
        {
            Name = $"set_name_{index}",
            Text = set.Name,
            FontSize = OrblitText.Label,
            Foreground = selected ? OrblitColors.Ember : OrblitColors.Ink,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var detail = new TextBlock
        {
            Name = $"set_detail_{index}",
            Text = BrushPanelModel.SetDetail(set),
            FontSize = OrblitText.Caption,
            FontFamily = OrblitText.Mono,
            Foreground = OrblitColors.InkDim,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(detail, 1);
        var row = new Border
        {
            Name = $"set_row_{index}",
            Height = 24,
            Margin = new Thickness(0, 0, 0, 2),
            Padding = new Thickness(Space.Sm, 0),
            CornerRadius = Radii.Control,
            Background = selected ? OrblitColors.EmberWash : OrblitColors.Raised,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { name, detail } },
        };
        row.PointerPressed += (_, _) => _host.Apply(() => _bench.Set = index);
        return row;
    }

    private Control BrushSection() => _fields.Section(
        "Brush",
        Fields.Column(
        [
            Setting("Size", brush => brush.Size, (brush, value) => brush.CopyWith(size: value), new SliderOptions { Min = 1, Max = 128, Unit = " m" }),
            Setting("Strength", brush => brush.Strength, (brush, value) => brush.CopyWith(strength: value), Unit()),
            Setting("Falloff", brush => brush.Falloff, (brush, value) => brush.CopyWith(falloff: value), Unit()),
            Setting("Jitter", brush => brush.Jitter, (brush, value) => brush.CopyWith(jitter: value), Unit()),
            Setting("Spacing", brush => brush.Spacing, (brush, value) => brush.CopyWith(spacing: value), new SliderOptions { Min = Brush.MinSpacing, Max = 1, Decimals = 2 }),
        ]));

    private static SliderOptions Unit() => new() { Min = 0, Max = 1, Decimals = 2 };

    // Reads the brush as it stands at each change, so one slider's drag never puts back what another set.
    private Control Setting(string label, Func<Brush, double> read, Func<Brush, double, Brush> write, SliderOptions options) =>
        _fields.Slider(label, read(_bench.Brush), value => _bench.Brush = write(_bench.Brush, value), options);
}
