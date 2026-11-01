using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// Where the texture of the selected faces sits, with the buttons that move it, project it and
/// freeze it into coordinates.
/// </summary>
/// <remarks>
/// Every change is a step on the undo stack. A drag in the square is one step however long it runs,
/// and a press of a button is one each.
/// </remarks>
internal sealed class UvPanel : UserControl
{
    private readonly EditorSession _session;
    private readonly FieldHost _host;
    private readonly Fields _fields;
    private readonly StackPanel _stack = new() { Name = "uv_fields", Margin = new Thickness(0, Space.Sm) };

    public UvPanel(EditorSession session)
    {
        _session = session;
        _host = new FieldHost(session.History, Rebuild);
        _fields = new Fields(_host);
        Background = OrblitColors.Surface;
        Content = new ScrollViewer { Name = "uv_scroll", Content = _stack };
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.Changed += _host.Changed;
        _session.History.Changed += _host.Changed;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.Changed -= _host.Changed;
        _session.History.Changed -= _host.Changed;
        // A drag that was under way gets no release once it is off the tree, and a hold left over would stop every rebuild.
        _host.Settle();
        base.OnDetachedFromVisualTree(e);
    }

    private void Rebuild()
    {
        _host.Reset();
        _stack.Children.Clear();
        _stack.Children.Add(_fields.Section("Texture", Coordinates()));
        if (Rule() is { } rule)
        {
            _stack.Children.Add(_fields.Section("Rule", rule));
        }
    }

    private StackPanel Coordinates()
    {
        var faces = FacesShown();
        var rows = new List<Control>();
        if (_session.UvWrongMode)
        {
            rows.Add(Say(UvWords.WrongMode, "uv_wrong_mode"));
        }

        var gestures = Enum.GetValues<UvGesture>();
        rows.Add(_fields.Choice(
            "Drag",
            [.. gestures.Select(UvTools.Label)],
            _session.UvGesture.Label(),
            picked => _session.UseUvGesture(gestures.First(one => one.Label() == picked))));
        rows.Add(faces.Count == 0 ? Say(UvWords.NothingToShow, "uv_nothing") : new UvCanvas(_session, _host));
        rows.Add(Say(UvWords.Status(faces), "uv_status"));
        rows.Add(Actions(faces.Count > 0));
        return Fields.Column(rows);
    }

    private WrapPanel Actions(bool any)
    {
        var buttons = new WrapPanel { Name = "uv_actions" };
        foreach (var action in Enum.GetValues<UvAction>())
        {
            var button = Fields.Button(action.Label(), $"{action.Label()} the selected texture coordinates.", () => _session.RunUvAction(action));
            button.IsEnabled = any;
            button.Margin = new Thickness(0, 0, Space.Xs, Space.Xs);
            buttons.Children.Add(button);
        }

        return buttons;
    }

    // Only a face that is following a rule has numbers to change. One drawn by hand has coordinates instead.
    private Control? Rule()
    {
        var faces = FacesShown();
        return faces is [{ Uv.IsManual: false } face]
            ? UvRules.Build(_fields, face.Uv, (rule, live) => _session.SetUvRule(rule, live))
            : null;
    }

    private List<Orblit.Mesh.Face> FacesShown() =>
        _session.UvMesh is { } mesh ? _session.UvSelection.FacesIn(mesh) : [];

    private static TextBlock Say(string text, string name)
    {
        var note = Fields.Note(text);
        note.Name = name;
        return note;
    }
}
