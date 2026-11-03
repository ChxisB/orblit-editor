using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using DockedPanel = Orblit.Editor.Core.DockPanel;
using Orblit.Editor.Core;

/// <summary>
/// Draws a <see cref="DockLayout"/>: splits as rows and columns, groups as a strip of tabs over the
/// panel that is showing. It changes nothing itself. A tab, a fold or a drag raises
/// <see cref="LayoutChanged"/> with the new arrangement and waits to be given it back.
/// </summary>
/// <remarks>
/// A layout that says the same as the one drawn is not drawn again, because the window offers its
/// layout on every change to the scene and a panel that is rebuilt loses its pointer mid-drag.
/// shortcut: when the arrangement does change, the panel in each group is built again, so a panel
/// that keeps state must be given back by the content function (the viewport is, the rest in task 16).
/// </remarks>
public sealed class DockHost : ContentControl
{
    private readonly Func<DockedPanel, Control> _content;
    private DockLayout _layout;
    private string _drawn;

    public DockHost(DockLayout layout, Func<DockedPanel, Control> content)
    {
        _layout = layout;
        _drawn = layout.ToText();
        _content = content;
        Rebuild();
    }

    /// <summary>Raised with the arrangement somebody just made.</summary>
    public event Action<DockLayout>? LayoutChanged;

    public DockLayout Layout
    {
        get => _layout;
        set
        {
            _layout = value;
            var text = value.ToText();
            if (text != _drawn)
            {
                _drawn = text;
                Rebuild();
            }
        }
    }

    private void Rebuild() => Content = Draw(_layout.Root);

    private Control Draw(DockNode node) => node switch
    {
        DockGroup group => DrawGroup(group),
        DockSplit split => DrawSplit(split),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private Grid DrawSplit(DockSplit split)
    {
        var across = split.Axis == DockAxis.Horizontal;
        var shares = split.Shares;
        var folded = split.Folded;
        var panes = new List<Control>();
        var pieces = new List<(Control Control, GridLength Size)>();

        for (var i = 0; i < split.Children.Count; i++)
        {
            if (i > 0)
            {
                pieces.Add((Divider(split, i - 1, panes), GridLength.Auto));
            }

            var pane = Draw(split.Children[i]);
            panes.Add(pane);
            pieces.Add((pane, folded[i] ? GridLength.Auto : new GridLength(shares[i], GridUnitType.Star)));
        }

        var grid = new Grid { Name = $"split_{split.Id}" };
        for (var at = 0; at < pieces.Count; at++)
        {
            var (piece, size) = pieces[at];
            piece[Grid.ColumnProperty] = across ? at : 0;
            piece[Grid.RowProperty] = across ? 0 : at;
            grid.Children.Add(piece);
            if (across)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(size) { MinWidth = size.IsStar ? 120 : 0 });
            }
            else
            {
                grid.RowDefinitions.Add(new RowDefinition(size) { MinHeight = size.IsStar ? 80 : 0 });
            }
        }

        return grid;
    }

    /// <summary>
    /// What sits between two neighbours: a handle to drag, or just a gap when the layout is locked or
    /// either neighbour is folded down to its tabs and has no size to give.
    /// </summary>
    private Control Divider(DockSplit split, int before, List<Control> panes)
    {
        var across = split.Axis == DockAxis.Horizontal;
        if (_layout.Locked || split.Folded[before] || split.Folded[before + 1])
        {
            return new Border { Width = DockStyle.Gap, Height = DockStyle.Gap };
        }

        // The handle is wider than the gap and reaches over the panes on both sides, so a split is easy to
        // catch without being drawn wide. The negative margin makes the layout slot the gap's width. The
        // reach is whole pixels on each side, because layout rounding turns a half into a whole.
        var reachBack = Math.Floor((DockStyle.GrabWidth - DockStyle.Gap) / 2);
        var reachOn = DockStyle.GrabWidth - DockStyle.Gap - reachBack;
        var handle = new GridSplitter
        {
            Name = $"handle_{split.Id}_{before}",
            Background = Brushes.Transparent,
            ResizeDirection = across ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            ZIndex = 1,
        };
        if (across)
        {
            handle.Width = DockStyle.GrabWidth;
            handle.Margin = new Thickness(-reachBack, 0, -reachOn, 0);
        }
        else
        {
            handle.Height = DockStyle.GrabWidth;
            handle.Margin = new Thickness(0, -reachBack, 0, -reachOn);
        }

        handle.PointerEntered += (_, _) => handle.Background = OrblitColors.Hover;
        handle.PointerExited += (_, _) => handle.Background = Brushes.Transparent;

        // The neighbours are read when the drag ends, by which time both have been added.
        handle.DragCompleted += (_, _) =>
        {
            var a = across ? panes[before].Bounds.Width : panes[before].Bounds.Height;
            var b = across ? panes[before + 1].Bounds.Width : panes[before + 1].Bounds.Height;
            if (a + b > 0)
            {
                LayoutChanged?.Invoke(_layout.Resize(split.Id, before, a / (a + b)));
            }
        };
        return handle;
    }

    private Border DrawGroup(DockGroup group)
    {
        var strip = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var panel in group.Panels)
        {
            strip.Children.Add(Tab(group, panel));
        }

        // No margin under the strip, so the showing tab sits on its pane.
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(Space.Xs, Space.Xs, Space.Xs, 0) };
        header.Children.Add(strip);
        if (_layout.CanCollapse(group.Id))
        {
            header.Children.Add(Chevron(group));
        }

        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        body.Children.Add(header);
        if (!group.Collapsed && group.Current is { } current)
        {
            var pane = Pane(group, _content(current));
            pane[Grid.RowProperty] = 1;
            body.Children.Add(pane);
        }

        // The group is the strip's tone, and the pane is the card on it.
        return new Border { Name = $"group_{group.Id}", Background = OrblitColors.Ground, Child = body };
    }

    private static Border Pane(DockGroup group, Control content)
    {
        // A panel that keeps its state is handed back by the content function while the pane it was
        // drawn in is still on screen, and a control has one parent.
        if (content.Parent is Border drawn)
        {
            drawn.Child = null;
        }

        return new Border
        {
            Name = $"pane_{group.Id}",
            Background = OrblitColors.Surface,
            BorderBrush = OrblitColors.Line,
            BorderThickness = new Thickness(DockStyle.BorderWidth),
            CornerRadius = DockStyle.PaneCorners,
            ClipToBounds = true,
            Child = content,
        };
    }

    private Border Tab(DockGroup group, DockedPanel panel)
    {
        var showing = group.Current == panel;
        var active = showing && !group.Collapsed;
        var label = new Button
        {
            Name = $"tab_{panel.Id}",
            Content = panel.Label,
            FontSize = OrblitText.Label,
            Padding = new Thickness(DockStyle.TabPadX, DockStyle.TabPadY),
            Background = Brushes.Transparent,
            Foreground = active ? OrblitColors.Ink : OrblitColors.InkDim,
        };
        // The showing tab of a group that folds folds it, so the console opens and closes from its tab.
        label.Click += (_, _) => LayoutChanged?.Invoke(
            showing && _layout.CanCollapse(group.Id) ? _layout.Collapse(group.Id, !group.Collapsed) : _layout.Show(panel.Id));

        var close = new Button
        {
            Name = $"close_{panel.Id}",
            Content = "×",
            FontSize = OrblitText.Label,
            Padding = new Thickness(Space.Xs, Space.Xs),
            Background = Brushes.Transparent,
            Foreground = OrblitColors.InkDim,
        };
        close.Click += (_, _) => LayoutChanged?.Invoke(_layout.Close(panel.Id));

        var tab = new StackPanel { Orientation = Orientation.Horizontal };
        tab.Children.Add(label);
        if (!_layout.Locked)
        {
            tab.Children.Add(close);
        }

        // The shape is what is drawn, so the close button is inside the showing tab and not beside it.
        return new Border
        {
            Name = $"tabshape_{panel.Id}",
            Background = active ? OrblitColors.Surface : Brushes.Transparent,
            CornerRadius = DockStyle.TabCorners,
            Child = tab,
        };
    }

    private Button Chevron(DockGroup group)
    {
        var chevron = new Button
        {
            Name = $"fold_{group.Id}",
            Content = group.Collapsed ? "▴" : "▾",
            FontSize = OrblitText.Label,
            Background = Brushes.Transparent,
            Foreground = OrblitColors.InkDim,
            [Grid.ColumnProperty] = 1,
        };
        chevron.Click += (_, _) => LayoutChanged?.Invoke(_layout.Collapse(group.Id, !group.Collapsed));
        return chevron;
    }
}
