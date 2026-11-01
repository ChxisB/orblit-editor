using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Motion;

/// <summary>
/// The shots of the open cutscene, one card for each: which camera, where it starts, how long it holds,
/// and a way to look through it or take it out.
/// </summary>
/// <remarks>
/// A card is built from the shots as they were when the panel last rebuilt, and holds the shot it
/// shows. Typing a start swaps that shot for a copy at the new time, so the card follows its shot and
/// the cards fall into their new order when the box lets go.
/// </remarks>
internal sealed partial class ShotListPanel : UserControl
{
    private const double Limit = 600;

    private readonly EditorSession _session;
    private readonly FieldHost _host;
    private readonly Fields _fields;
    private readonly StackPanel _stack = new() { Name = "shots_list", Margin = new Thickness(0, Space.Sm) };
    private string _key = "";

    public ShotListPanel(EditorSession session)
    {
        _session = session;
        _host = new FieldHost(session.History, Rebuild);
        _fields = new Fields(_host);
        Background = OrblitColors.Surface;
        Content = new ScrollViewer { Name = "shots_scroll", Content = _stack };
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.Cutscenes.Changed += OnChanged;
        _session.Changed += OnChanged;
        _session.History.Changed += OnChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.Cutscenes.Changed -= OnChanged;
        _session.Changed -= OnChanged;
        _session.History.Changed -= OnChanged;
        // A box that was being typed in gets no LostFocus once it is off the tree, and a hold left over would stop every rebuild.
        _host.Settle();
        base.OnDetachedFromVisualTree(e);
    }

    // The playhead moves the bench's Changed many times a second, and a rebuild would drop the focus
    // from a box nobody is typing in. So only a change to what the cards show rebuilds them.
    private void OnChanged()
    {
        if (KeyOfShots() != _key)
        {
            _host.Changed();
        }
    }

    private string KeyOfShots()
    {
        // The path is in the key because an empty cutscene looks like none, and two cutscenes can hold equal shots that are not the same ones.
        var shots = _session.Cutscenes.Shown?.Shots?.Select(shot => $"{shot.Camera}|{shot.Start}|{shot.Duration}") ?? [];
        var cameras = Cameras().Select(camera => $"{camera.Id}|{camera.Name}");
        return string.Join(';', shots.Append(_session.Piloting?.Camera ?? "").Append(_session.Cutscenes.Shown?.Path ?? "").Concat(cameras));
    }

    private void Rebuild()
    {
        _host.Reset();
        _key = KeyOfShots();
        _stack.Children.Clear();
        var shots = _session.Cutscenes.Shown?.Shots;
        if (shots is null || shots.Count == 0)
        {
            var saying = Fields.Note(shots is null ? NoCutscene : NoShots);
            saying.Name = shots is null ? "shots_none" : "shots_empty";
            saying.Margin = new Thickness(Space.Lg);
            _stack.Children.Add(saying);
            return;
        }

        for (var i = 0; i < shots.Count; i++)
        {
            _stack.Children.Add(CardOf(new Card(i, shots[i])));
        }
    }

    private IEnumerable<SceneObject> Cameras() =>
        _session.Workspace.Loaded?.Scene?.Objects.Where(one => one.Kind == ObjectKind.Camera) ?? [];

    private const string NoCutscene = "The shots of an open cutscene show here. Choose New cutscene on the shelf.";

    private const string NoShots =
        "No shots yet. Add shot cuts to the selected camera at the playhead, and Use this view puts a camera where the scene view is.";

    // The shot a card shows now, which is not the one it was built from once it has been typed in.
    private sealed class Card(int index, CutsceneShot shot)
    {
        public int Index { get; } = index;

        public CutsceneShot Shot { get; set; } = shot;

        // One for each box a number is typed into, so the keystrokes are one step to undo.
        public object Gesture { get; } = new();
    }
}
