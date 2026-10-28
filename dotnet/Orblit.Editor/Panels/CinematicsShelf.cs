using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// Over the panels in the cinematics mode: make a cutscene, and cut it to a camera. The two cuts wait
/// for a cutscene to be open, because there is nothing to cut before then.
/// </summary>
internal sealed class CinematicsShelf : Border
{
    private readonly ClipBench _cutscenes;
    private readonly Button _add;
    private readonly Button _use;

    public CinematicsShelf(ClipBench cutscenes, Action onNew, Action onAdd, Action onUse)
    {
        _cutscenes = cutscenes;
        Name = "cinematics_shelf";
        Background = OrblitColors.Surface;
        Padding = new Thickness(Space.Md, Space.Xs);
        _add = Quiet("shelf_add_shot", "Add shot", "Cut to the selected camera at the playhead.", onAdd);
        _use = Quiet("shelf_use_view", "Use this view", "Put a camera where the scene view is, and cut to it at the playhead.", onUse);
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Space.Xs,
            Children = { Quiet("shelf_new_cutscene", "New cutscene", "Create a cutscene for this scene.", onNew), _add, _use },
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _cutscenes.Changed += Sync;
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _cutscenes.Changed -= Sync;
        base.OnDetachedFromVisualTree(e);
    }

    private void Sync() => _add.IsEnabled = _use.IsEnabled = _cutscenes.Shown?.Shots is not null;

    private static Button Quiet(string name, string label, string tooltip, Action onPressed)
    {
        var button = new Button { Name = name, Content = label, FontSize = OrblitText.Caption, Padding = new Thickness(Space.Sm, Space.Xs) };
        ToolTip.SetTip(button, tooltip);
        button.Click += (_, _) => onPressed();
        return button;
    }
}
