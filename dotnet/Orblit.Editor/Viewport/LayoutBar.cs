using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Scene;

/// <summary>
/// Names the layout being edited over the view. A layout looks like the base with some things moved, so
/// without this nobody could tell which of the two a drag was about to change.
/// </summary>
internal sealed class LayoutBar : Border
{
    private readonly EditorSession _session;
    private readonly TextBlock _text = new() { FontSize = 11, Foreground = OrblitColors.Ember };

    public LayoutBar(EditorSession session)
    {
        _session = session;
        Name = "layout_bar";
        Padding = new Thickness(Space.Sm, 3);
        Margin = new Thickness(Space.Md);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Bottom;
        CornerRadius = Radii.Control;
        Background = OrblitColors.EmberWash;
        BorderBrush = OrblitColors.Ember;
        BorderThickness = new Thickness(1);
        // A press on the bar is a press on the scene behind it.
        IsHitTestVisible = false;
        Child = _text;
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
        IsVisible = shown is not null;
        _text.Text = shown switch
        {
            LayoutShape.Portrait => "Editing the Portrait layout. It shows when the screen is taller than wide.",
            LayoutShape.Landscape => "Editing the Landscape layout. It shows on every other screen.",
            _ => "",
        };
    }
}
