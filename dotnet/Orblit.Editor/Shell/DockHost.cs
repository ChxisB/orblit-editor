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
    private const double Gap = 4;

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
            return new Border { Width = Gap, Height = Gap };
        }

        var handle = new GridSplitter
        {
            Name = $"handle_{split.Id}_{before}",
            Background = OrblitColors.Ground,
            ResizeDirection = across ? GridResizeDirection.Columns : GridResizeDirection.Rows,
        };
        if (across)
        {
            handle.Width = Gap;
        }
        else
        {
            handle.Height = Gap;
        }

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
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Xs };
        foreach (var panel in group.Panels)
        {
            strip.Children.Add(Tab(group, panel));
        }

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(Space.Sm, Space.Xs) };
        header.Children.Add(strip);
        if (_layout.CanCollapse(group.Id))
        {
            header.Children.Add(Chevron(group));
        }

        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        body.Children.Add(header);
        if (!group.Collapsed && group.Current is { } current)
        {
            var content = _content(current);
            content[Grid.RowProperty] = 1;
            body.Children.Add(content);
        }

        return new Border
        {
            Name = $"group_{group.Id}",
            Background = OrblitColors.Surface,
            BorderBrush = OrblitColors.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = Radii.Panel,
            ClipToBounds = true,
            Child = body,
        };
    }

    private StackPanel Tab(DockGroup group, DockedPanel panel)
    {
        var showing = group.Current == panel;
        var active = showing && !group.Collapsed;
        var label = new Button
        {
            Name = $"tab_{panel.Id}",
            Content = panel.Label,
            FontSize = OrblitText.Label,
            Padding = new Thickness(Space.Sm, Space.Xs),
            Background = active ? OrblitColors.Raised : Brushes.Transparent,
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

        return tab;
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
