using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// The properties of whatever is selected. The fields shown depend on what the thing is, which is the
/// point of components: a light and a mesh are not the same object with some fields greyed out, they
/// are different sets of components on an entity.
/// </summary>
/// <remarks>
/// Every field runs a command, so there is no edit that undo does not know about. Which fields come in
/// which order is the section list, and the panel keeps no list of its own.
/// </remarks>
internal sealed class InspectorPanel : UserControl
{
    private readonly EditorSession _session;
    private readonly InspectorLinks _links;
    private readonly FieldHost _host;
    private readonly Fields _fields;
    private readonly SceneFields _sceneFields;
    private readonly IReadOnlyList<InspectorSection<Control>> _sections;

    // Kept between rebuilds, and filled again, so that a scroll position survives one.
    private readonly StackPanel _bands = new() { Name = "inspector_bands" };
    private readonly StackPanel _stack = new() { Name = "inspector_fields", Margin = new Thickness(0, Space.Sm) };
    private readonly ScrollViewer _scroll;
    private string? _shown;

    /// <param name="session">What is selected and changed.</param>
    /// <param name="links">What the panel asks the rest of the editor to do. Null for a window with nothing to ask.</param>
    public InspectorPanel(EditorSession session, InspectorLinks? links = null)
    {
        _session = session;
        _links = links ?? new InspectorLinks();
        _host = new FieldHost(session.History, Rebuild);
        _fields = new Fields(_host);
        _sceneFields = new SceneFields(_fields);
        _sections = InspectorSections.BuiltIn(_fields, _links.Surfaces, _links.Terrain);

        _scroll = new ScrollViewer { Name = "inspector_scroll", Content = _stack };
        _scroll[Grid.RowProperty] = 1;
        var frame = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        frame.Children.Add(_bands);
        frame.Children.Add(_scroll);
        Background = OrblitColors.Surface;
        Content = frame;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.Changed += _host.Changed;
        _session.History.Changed += _host.Changed;
        _session.Workspace.Changed += _host.Changed;
        if (_links.Surfaces is { } surfaces)
        {
            surfaces.Changed += _host.Changed;
        }

        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.Changed -= _host.Changed;
        _session.History.Changed -= _host.Changed;
        _session.Workspace.Changed -= _host.Changed;
        if (_links.Surfaces is { } surfaces)
        {
            surfaces.Changed -= _host.Changed;
        }

        // A field that was being edited gets no LostFocus once it is off the tree, and a hold left over would stop every rebuild.
        _host.Settle();
        base.OnDetachedFromVisualTree(e);
    }

    private void Rebuild()
    {
        _host.Reset();
        _bands.Children.Clear();
        _stack.Children.Clear();

        if (_session.Inspected is not { } entry)
        {
            _stack.Children.Add(Nothing());
            return;
        }

        if (entry.Scene is not { } scene || _session.InspectedItem is not { } item)
        {
            // Nothing selected in it, or not open: what is left to edit is the scene itself.
            ScrollToTopFor($"scene/{entry.Id}");
            _stack.Children.Add(_sceneFields.Build(_host, entry, _session.LoadScene));
            return;
        }

        ScrollToTopFor(item.Id);
        var target = new InspectorTarget
        {
            SceneId = entry.Id,
            Scene = scene,
            Item = item,
            History = _session.History,
            OnOpenData = _links.OnOpenData,
            OnDetachData = _links.OnDetachData,
            OnOpenInterface = _links.OnOpenInterface,
            OnOpenTools = () => _session.EnterMode("modelling"),
            BoundsOf = _links.BoundsOf,
            CollisionOf = _links.CollisionOf,
            Keying = _session.Keying,
            OnOpenClip = path => _session.OpenClip(Path.Combine(_session.Workspace.ProjectDirectory, path)),
            OnAttachClip = _session.Clips.Shown is { } shown ? () => _session.AttachClip(shown.Path) : null,
        };
        Bands(scene, item);
        Body(target);
    }

    // A different thing starts at the top, as a new panel would. The same one stays where it was.
    private void ScrollToTopFor(string key)
    {
        if (_shown == key)
        {
            return;
        }

        _shown = key;
        _scroll.Offset = default;
    }

    private void Bands(EditorScene scene, SceneObject item)
    {
        if (_session.Selected.Count > 1)
        {
            _bands.Children.Add(InspectorBands.Multiple(_session.Selected.Count, item.Name));
        }

        if (InspectorModel.InstanceHolding(scene, item) is { } instance)
        {
            _bands.Children.Add(InspectorBands.Prefab(instance, item, _links));
        }
    }

    private void Body(InspectorTarget target)
    {
        var item = target.Item;
        _stack.Children.Add(InspectorBands.Header(_host, target));
        if (target.HiddenAbove)
        {
            _stack.Children.Add(Note("inspector_hidden_above", "Hidden anyway, because something it is inside is hidden."));
        }

        if (item.ParentId is { } parentId && target.Scene[parentId] is { } parent)
        {
            _stack.Children.Add(Note("inspector_parent", $"in {parent.Name}"));
        }

        _stack.Children.AddRange(InspectorModel.Shown(_sections, target));
    }

    private StackPanel Nothing()
    {
        var button = Fields.Button("New scene", "Create a scene to put objects in.", _session.NewScene);
        button.HorizontalAlignment = HorizontalAlignment.Center;
        return new StackPanel
        {
            Name = "inspector_nothing",
            Margin = new Thickness(Space.Lg),
            Spacing = Space.Sm,
            Children = { Note("inspector_empty", "Open or create a scene to edit its objects."), button },
        };
    }

    private static TextBlock Note(string name, string text)
    {
        var note = Fields.Note(text);
        note.Name = name;
        note.Margin = new Thickness(Space.Lg, 0, Space.Lg, Space.Sm);
        return note;
    }
}
