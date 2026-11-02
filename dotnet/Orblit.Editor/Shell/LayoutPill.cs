using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Scene;

/// <summary>Chooses whether the scene is edited as it is, or as it looks in one screen shape.</summary>
internal sealed class LayoutPill : Border
{
    private const string BaseTip = "Edit the scene itself. Layouts change how it looks in one screen shape.";
    private const string InLayoutTip = "Switch to Base to add or remove objects.";
    private const string PortraitTip = "Edit how the scene looks when the screen is taller than wide.";
    private const string LandscapeTip = "Edit how the scene looks on every other screen.";

    private readonly EditorSession _session;
    private readonly ToolbarChip _base;
    private readonly ToolbarChip _portrait;
    private readonly ToolbarChip _landscape;

    public LayoutPill(EditorSession session)
    {
        _session = session;
        Name = "layout_pill";
        Padding = new Thickness(Space.Xs, 0);
        Margin = new Thickness(0, 0, Space.Sm, 0);
        VerticalAlignment = VerticalAlignment.Center;
        CornerRadius = Radii.Control;
        Background = OrblitColors.Raised;
        _base = new ToolbarChip("layout_switch_base", "Base", BaseTip, () => session.ShowLayout(null));
        _portrait = new ToolbarChip("layout_switch_portrait", "Portrait", PortraitTip, () => session.ShowLayout(LayoutShape.Portrait));
        _landscape = new ToolbarChip("layout_switch_landscape", "Landscape", LandscapeTip, () => session.ShowLayout(LayoutShape.Landscape));
        Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _base, _portrait, _landscape } };
        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.Workspace.Changed += Sync;
        _session.History.Changed += Sync;
        _session.Changed += Sync;
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.Workspace.Changed -= Sync;
        _session.History.Changed -= Sync;
        _session.Changed -= Sync;
        base.OnDetachedFromVisualTree(e);
    }

    private void Sync()
    {
        var shown = _session.ActiveLayout;
        var hasScene = _session.Workspace.Loaded?.Scene is not null;
        foreach (var chip in new[] { _base, _portrait, _landscape })
        {
            chip.IsEnabled = hasScene;
        }

        _base.On = shown is null;
        _portrait.On = shown == LayoutShape.Portrait;
        _landscape.On = shown == LayoutShape.Landscape;
        ToolTip.SetTip(_base, shown is null ? BaseTip : InLayoutTip);
    }
}
