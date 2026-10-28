using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// Over the panels in the Interface workspace: a new interface, the one open, the size it is laid out
/// at and what is drawn over it.
/// </summary>
/// <remarks>
/// Listens to the undo stack as well as the bench, because whether the interface has changes is the
/// stack's answer.
/// </remarks>
internal sealed class InterfaceShelf : Border
{
    private readonly InterfaceBench _bench;
    // With nothing open the canvas offers the same button and says what an interface is. Two at once
    // read as two different things, so the whole row goes.
    private readonly StackPanel _row;
    private readonly TextBlock _name = new() { Name = "shelf_interface_name", FontSize = OrblitText.Label, Foreground = OrblitColors.Ink, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _laidOut = new() { Name = "shelf_laid_out", FontSize = 11, Foreground = OrblitColors.InkDim, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _breakpoint = new() { Name = "shelf_breakpoint", FontSize = 10.5, Foreground = OrblitColors.InkMid };
    private readonly StackPanel _showing;
    private readonly ToolbarChip _preview;
    private readonly ToolbarChip _outlines;
    private readonly ToolbarChip _guides;
    private readonly ToolbarChip _columns;

    public InterfaceShelf(InterfaceBench bench, Action onNew)
    {
        _bench = bench;
        Name = "interface_shelf";
        Background = OrblitColors.Surface;
        var make = new Button { Name = "shelf_new_interface", Content = "New interface", FontSize = OrblitText.Caption, Padding = new Thickness(Space.Sm, Space.Xs) };
        ToolTip.SetTip(make, InterfaceSideModel.NewTip);
        make.Click += (_, _) => onNew();
        _showing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Space.Xs,
            Children =
            {
                _laidOut,
                new Border
                {
                    Padding = new Thickness(5, 1),
                    Background = OrblitColors.Raised,
                    BorderBrush = OrblitColors.Line,
                    BorderThickness = new Thickness(1),
                    CornerRadius = Radii.Control,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = _breakpoint,
                },
            },
        };
        // What the game shows, with nothing over it. The one control that answers whether a guide is
        // going to be in a screenshot.
        _preview = new ToolbarChip("shelf_preview", "Preview", null, () => _bench.Previewing = !_bench.Previewing);
        // Named after what they take away. "Outlines" that left a canvas frame and a safe area on screen
        // would read as a toggle that did nothing.
        _outlines = new ToolbarChip("shelf_element_outlines", "Element outlines", null, () => _bench.Outlines = !_bench.Outlines);
        _guides = new ToolbarChip("shelf_canvas_guides", "Canvas guides", null, () => _bench.Guides = !_bench.Guides);
        _columns = new ToolbarChip("shelf_column_grid", "Column grid", null, () => _bench.Columns = !_bench.Columns);
        _row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Space.Md,
            Margin = new Thickness(Space.Md, Space.Xs),
            Children =
            {
                make,
                _name,
                _showing,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Xs, Children = { _preview, _outlines, _guides, _columns } },
            },
        };
        Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _row };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Changed += Sync;
        _bench.History.Changed += Sync;
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bench.Changed -= Sync;
        _bench.History.Changed -= Sync;
        base.OnDetachedFromVisualTree(e);
    }

    private void Sync()
    {
        _row.IsVisible = _bench.Shown is not null;
        if (_bench.Shown is not { } shown || _bench.LaidOut is not { } laidOut)
        {
            return;
        }

        _name.Text = shown.Name + (_bench.IsUnsaved(shown) ? " •" : "");
        _laidOut.Text = InterfaceSideModel.Size(laidOut);
        _breakpoint.Text = InterfaceSideModel.Breakpoint(laidOut.Width);
        ToolTip.SetTip(_showing, InterfaceSideModel.ShowingTip(laidOut));
        var previewing = _bench.Previewing;
        Toggle(_preview, "preview", previewing, enabled: true);
        Toggle(_outlines, "element outlines", _bench.Outlines && !previewing, enabled: !previewing);
        Toggle(_guides, "canvas guides", _bench.Guides && !previewing, enabled: !previewing);
        Toggle(_columns, "column grid", _bench.Columns && !previewing, enabled: !previewing);
    }

    private static void Toggle(ToolbarChip chip, string what, bool on, bool enabled)
    {
        chip.On = on;
        chip.IsEnabled = enabled;
        ToolTip.SetTip(chip, $"Turn {(on ? "off" : "on")} {what}.");
    }
}
