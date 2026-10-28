using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;

/// <summary>
/// The modelling tools as a place rather than a section: where a drag lands, the tools that are
/// drawn, the operations on the geometry and the materials faces are painted with.
/// </summary>
/// <remarks>
/// A panel of its own because modelling is a job somebody settles into and the inspector is a place
/// they pass through. A tool used fifty times in a row should not be four scrolls down a list of
/// somebody's transform and light.
/// <para>
/// shortcut: buttons are words only, where the Dart panel draws an icon beside each, until the icon
/// pass (decision 84).
/// </para>
/// </remarks>
internal sealed class ModellingPanel : UserControl
{
    private static readonly ViewportTool[] DrawTools = [ViewportTool.PolyShape, ViewportTool.Cut];

    private readonly EditorSession _session;
    private readonly Snapping _snapping;
    private readonly FieldHost _host;
    private readonly Fields _fields;
    private readonly StackPanel _stack = new() { Name = "modelling_fields", Margin = new Thickness(0, Space.Sm) };

    public ModellingPanel(EditorSession session, Snapping snapping)
    {
        _session = session;
        _snapping = snapping;
        _host = new FieldHost(session.History, Rebuild);
        _fields = new Fields(_host);
        Background = OrblitColors.Surface;
        Content = new ScrollViewer { Name = "modelling_scroll", Content = _stack };
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.Changed += _host.Changed;
        _session.History.Changed += _host.Changed;
        _snapping.Changed += _host.Changed;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.Changed -= _host.Changed;
        _session.History.Changed -= _host.Changed;
        _snapping.Changed -= _host.Changed;
        // A slider that was held gets no release once it is off the tree, and a hold left over would stop every rebuild.
        _host.Settle();
        base.OnDetachedFromVisualTree(e);
    }

    private void Rebuild()
    {
        _host.Reset();
        _stack.Children.Clear();
        _stack.Children.Add(_fields.Section("Grid", Snap()));
        _stack.Children.Add(_fields.Section("Draw", Draw()));
        if (_session.ChosenShape is not { } shape)
        {
            var words = _session.NotShapeName is { } other ? ModellingWords.NotAShape(other) : ModellingWords.NothingSelected;
            _stack.Children.Add(Say(words, "modelling_empty", new Thickness(Space.Lg, Space.Sm)));
            return;
        }

        _stack.Children.Add(_fields.Section("Geometry", Geometry()));
        _stack.Children.Add(_fields.Section("Materials", Materials(shape)));
        _stack.Children.Add(_fields.Section("Export", Export()));
    }

    // Beside the drawing tools rather than in the inspector, because it is a property of how somebody
    // is working and not of any one object.
    private StackPanel Snap()
    {
        var rows = new List<Control> { _fields.Toggle("Snap to grid", _snapping.On, on => _snapping.On = on) };
        if (_snapping.On)
        {
            var targets = Enum.GetValues<SnapTo>();
            rows.Add(_fields.Choice(
                "Put on the line",
                [.. targets.Select(SnapTos.Label)],
                _snapping.To.Label(),
                picked => _snapping.To = targets.First(one => one.Label() == picked)));
            rows.Add(Say(ModellingWords.SnapTarget(_snapping.To, _snapping.Step), "modelling_snap_target"));
        }

        return Fields.Column(rows);
    }

    // At the top because they are the only tools that work without a shape selected: drawing one is
    // how somebody gets a shape in the first place.
    private StackPanel Draw()
    {
        var tools = new UniformGrid { Name = "modelling_tools", Rows = 1 };
        foreach (var tool in DrawTools)
        {
            tools.Children.Add(ToolButton(tool));
        }

        var rows = new List<Control> { tools };
        if (_session.Sketch.Tool.IsDrawing())
        {
            rows.Add(Say(ModellingWords.Drawing(_session.Sketch.Points.Count), "modelling_drawing"));
        }

        return Fields.Column(rows);
    }

    private Button ToolButton(ViewportTool tool)
    {
        var button = Fields.Button(tool.Label(), $"{tool.Label()} in the view. Enter finishes. Escape cancels.", () => _session.UseTool(tool));
        button.Margin = new Thickness(0, 0, Space.Xs, 0);

        // Cutting needs something to cut. Offered dimmed rather than hidden, so it is clear the tool
        // exists and what it is waiting for.
        button.IsEnabled = tool != ViewportTool.Cut || _session.ChosenShape is not null;
        if (_session.Sketch.Tool == tool)
        {
            button.Background = OrblitColors.EmberWash;
            button.Foreground = OrblitColors.Ember;
        }

        return button;
    }

    private StackPanel Geometry()
    {
        var editing = _session.Context == EditContext.Element;
        var contexts = Enum.GetValues<EditContext>();
        var rows = new List<Control>
        {
            _fields.Choice(
                "Editing",
                [.. contexts.Select(EditContexts.Label)],
                _session.Context.Label(),
                picked => _session.SetContext(contexts.First(one => one.Label() == picked))),
        };
        if (editing)
        {
            rows.AddRange(Parts());
        }

        var offered = MeshTools.AvailableIn(editing ? _session.ElementMode : null, _session.Elements);
        rows.AddRange(Enum.GetValues<ToolGroup>().SelectMany(group => Shelf(group, offered)));
        return Fields.Column(rows);
    }

    private IEnumerable<Control> Parts()
    {
        var modes = Enum.GetValues<ElementMode>();
        var mode = _session.ElementMode;
        yield return _fields.Choice(
            "Mode",
            [.. modes.Select(ElementModes.Label)],
            mode.Label(),
            picked => _session.SetElementMode(modes.First(one => one.Label() == picked)));
        yield return _fields.Toggle("See through", _session.SeeThrough, _session.SetSeeThrough);
        yield return Say(ModellingWords.Parts(mode, _session.Elements.CountIn(mode)), "modelling_parts");
    }

    // Grouped, because twenty-five buttons in a column is a list nobody reads. A button's shelf says
    // whether it changes what is selected, the part of the shape that is, or the whole of it.
    private IEnumerable<Control> Shelf(ToolGroup group, List<MeshAction> offered)
    {
        var actions = offered.Where(one => one.Group == group).ToList();
        if (actions.Count == 0)
        {
            return [];
        }

        var heading = SectionLabel.Of(group.Label());
        heading.Name = $"modelling_group_{Fields.Key(group.Label())}";
        return [heading, .. actions.Select(ActionRow)];
    }

    // The number sits under the button that uses it, so it is clear which one it belongs to. A panel
    // of sliders above a panel of buttons is a guess about which goes with which.
    private StackPanel ActionRow(MeshAction action)
    {
        var row = new StackPanel
        {
            Name = $"action_{Fields.Key(action.Label)}",
            Margin = new Thickness(0, 0, 0, 3),
            Children = { Fields.Button(action.Label, action.Hint, () => _session.RunMeshAction(action)) },
        };
        if (action.Amount is { } takes)
        {
            row.Children.Add(_fields.Slider(
                takes.Label,
                _session.AmountOf(action),
                value => _session.SetAmount(action, value),
                new SliderOptions { Min = takes.Min, Max = takes.Max, Decimals = takes.Max <= 2 ? 2 : 0 }));
        }

        return row;
    }

    private StackPanel Materials(SceneObject shape)
    {
        // How many faces wear each slot, so a slot can say how much of the shape it covers. The
        // fastest way to find the one somebody is looking for.
        var worn = (shape.CurrentMesh?.Faces ?? []).GroupBy(face => face.Material).ToDictionary(group => group.Key, group => group.Count());
        var painting = _session.Context == EditContext.Element && _session.ElementMode == ElementMode.Face && _session.Elements.Faces.Count > 0;
        var rows = new List<Control>();
        if (shape.Surfaces.Count == 0)
        {
            rows.Add(Say(ModellingWords.NoMaterials, "modelling_no_materials"));
        }

        for (var slot = 0; slot < shape.Surfaces.Count; slot++)
        {
            rows.Add(SurfaceRow(slot, shape.Surfaces[slot], worn.GetValueOrDefault(slot), painting));
        }

        rows.Add(Fields.Button("Add material", "Add another material for this shape's faces.", AddMaterial));
        return Fields.Column(rows);
    }

    // A shape blocked out here is often the start of something finished somewhere else, and a tool that
    // can only be a dead end is one people stop putting real work into.
    private StackPanel Export()
    {
        var formats = Enum.GetValues<MeshFormat>();
        var format = _session.ExportFormat;
        return Fields.Column(
        [
            _fields.Choice("Format", [.. formats.Select(one => one.Label)], format.Label, picked => _session.SetExportFormat(formats.First(one => one.Label == picked))),
            Say(ModellingWords.Format(format), "modelling_format"),
            Fields.Button("Export shape", "Save this shape as a model file.", AskExport),
        ]);
    }

    private async void AskExport()
    {
        if (_session.ChosenShape is not { } shape || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var prompt = new NamePrompt($"Export {shape.Name}", $"Goes in exports/, as .{_session.ExportFormat.Extension}.", shape.Name) { AcceptText = "Export" };
        if (await prompt.ShowDialog<string?>(owner) is { Length: > 0 } name)
        {
            _session.ExportShape(name);
        }
    }

    private void AddMaterial()
    {
        if (_session.ChosenShape is { } shape)
        {
            _session.ReplaceSurfaces([.. shape.Surfaces, new Surface { Name = $"Material {shape.Surfaces.Count + 1}" }], live: false);
        }
    }

    // What it looks like, how much of the shape wears it, and the button that paints more of it.
    private Border SurfaceRow(int slot, Surface surface, int faces, bool painting)
    {
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var swatch = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Avalonia.Media.Color.FromUInt32(surface.Colour.Argb)),
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, Space.Xs, 0),
        };
        var name = new TextBlock
        {
            Text = $"{surface.Name}  {ModellingWords.Used(faces)}",
            FontSize = OrblitText.Label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(name, 1);
        head.Children.AddRange([swatch, name]);
        if (painting)
        {
            // Only when there is something to paint. A button that does nothing teaches nobody when it would.
            var paint = Fields.Button("Paint", "Apply this material to the selected faces.", () => _session.PaintFaces(slot));
            paint.Name = $"paint_{slot}";
            Grid.SetColumn(paint, 2);
            head.Children.Add(paint);
        }

        return new Border
        {
            Name = $"surface_{slot}",
            Margin = new Thickness(0, 0, 0, Space.Xs),
            Padding = new Thickness(Space.Xs),
            Background = OrblitColors.Ground,
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(1),
            CornerRadius = Radii.Control,
            Child = Fields.Column(
            [
                head,
                _fields.Palette("Colour", surface.Colour, colour => Change(slot, one => one with { Colour = colour }, live: false)),
                Level("Metal", surface.Metallic, slot, (one, value) => one with { Metallic = value }),
                Level("Rough", surface.Roughness, slot, (one, value) => one with { Roughness = value }),
            ]),
        };
    }

    private Control Level(string label, double value, int slot, Func<Surface, double, Surface> set) =>
        _fields.Slider(label, value, to => Change(slot, one => set(one, to), live: true), new SliderOptions { Min = 0, Max = 1, Decimals = 2 });

    // Reads the shape again rather than holding the one this row was built for, which a rebuild may have replaced.
    private void Change(int slot, Func<Surface, Surface> change, bool live)
    {
        if (_session.ChosenShape is not { } shape || slot >= shape.Surfaces.Count)
        {
            return;
        }

        var all = shape.Surfaces.ToList();
        all[slot] = change(all[slot]);
        _session.ReplaceSurfaces(all, live);
    }

    private static TextBlock Say(string text, string name, Thickness? margin = null)
    {
        var note = Fields.Note(text);
        note.Name = name;
        if (margin is { } around)
        {
            note.Margin = around;
        }

        return note;
    }
}
