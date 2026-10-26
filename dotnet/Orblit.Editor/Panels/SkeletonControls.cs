using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// Bones and onion skin: whether the model's skeleton is drawn in the view, and the poses either side of
/// the playhead with it.
/// </summary>
/// <remarks>
/// Which ghosts to show is a menu that appears only while the skin is on.
/// </remarks>
internal sealed class SkeletonControls : StackPanel
{
    private static readonly int[] Counts = [1, 2, 3];
    private static readonly int[] Strides = [1, 2, 4];

    private readonly SkeletonLook _look;
    private readonly ToolbarChip _bones;
    private readonly ToolbarChip _onion;
    private readonly ToolbarMenu _ghosts;

    public SkeletonControls(SkeletonLook look)
    {
        _look = look;
        Orientation = Orientation.Horizontal;
        _bones = new ToolbarChip("timeline_bones", "Bones", "Draw the bones of the selected model in the view.", () => look.Value = look.Value.WithBonesToggled());
        _onion = new ToolbarChip("timeline_onion", "Onion", "Draw the pose before and after the playhead as ghosts.", () => look.Value = look.Value.WithOnionToggled());
        _ghosts = new ToolbarMenu("timeline_ghosts", "How many ghosts, and how many frames apart", GhostItems) { Margin = new Thickness(Space.Xs, 0, 0, 0) };
        Children.Add(_bones);
        Children.Add(_onion);
        Children.Add(_ghosts);
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _look.Changed += Refresh;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _look.Changed -= Refresh;
        base.OnDetachedFromVisualTree(e);
    }

    private void Refresh()
    {
        var now = _look.Value;
        _bones.On = now.Bones;
        _onion.On = now.Onion;
        _ghosts.IsVisible = now.Onion;
        _ghosts.Label = TimelineWords.Ghosts(now);
    }

    private IEnumerable<Control> GhostItems()
    {
        var now = _look.Value;
        foreach (var count in Counts)
        {
            yield return ToolbarMenu.Item($"timeline_ghosts_{count}", $"{count} each side", count == now.Before ? null : () => _look.Value = now with { Before = count, After = count });
        }

        foreach (var frames in Strides)
        {
            yield return ToolbarMenu.Item($"timeline_stride_{frames}", TimelineWords.Stride(frames), frames == now.Stride ? null : () => _look.Value = now with { Stride = frames });
        }
    }
}
