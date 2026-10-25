using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// The hierarchy: every open scene with its objects under it. It draws what an
/// <see cref="OutlinerModel"/> lists and calls the session for every change.
/// </summary>
/// <remarks>
/// shortcut: rows show words and no kind icon (decision 84), and a scene row cannot be loaded or
/// closed from here yet (task 16 row 1b).
/// </remarks>
internal sealed class OutlinerPanel : UserControl
{
    private static readonly DataFormat<string> Dragged = DataFormat.CreateStringApplicationFormat("orblit.object");
    private const double DragStart = 4;
    private const double Indent = 14;

    private readonly EditorSession _session;
    private readonly OutlinerModel _model;
    private readonly KeyModifiers _command;
    private readonly Action _openAdd;
    private readonly StackPanel _rows = new() { Name = "outliner_rows", Margin = new Thickness(6, 0, 6, 8) };
    private readonly TextBlock _nothing = new()
    {
        Name = "outliner_nothing",
        Text = "Nothing here has that name.",
        FontSize = OrblitText.Caption,
        Foreground = OrblitColors.InkDim,
        Margin = new Thickness(Space.Lg),
    };

    private readonly TextBlock _count = new()
    {
        Name = "outliner_count",
        FontSize = 10.5,
        FontFamily = OrblitText.Mono,
        Foreground = OrblitColors.InkDim,
        Margin = new Thickness(Space.Md, Space.Xs),
    };

    // A press waits here until the pointer has moved far enough to be a drag. Selecting on the press
    // rebuilds the rows, so the row that was pressed is gone by then and cannot carry the drag.
    private (string Id, PointerPressedEventArgs Press, Point At)? _pending;
    private string? _selectedScene;

    /// <param name="session">What is selected and changed.</param>
    /// <param name="platform">The host's keys, for which modifier adds to a selection.</param>
    /// <param name="openAdd">Opens the palette on the commands that add something.</param>
    public OutlinerPanel(EditorSession session, HostPlatform platform, Action openAdd)
    {
        _session = session;
        _model = new OutlinerModel(session.Workspace);
        _command = KeyMapping.GestureOf(platform.CommandShortcut(EditorKey.KeyC)).KeyModifiers;
        _openAdd = openAdd;
        Focusable = true;

        var filter = new TextBox { Name = "outliner_filter", PlaceholderText = "Filter" };
        filter.TextChanged += (_, _) =>
        {
            _model.Filter = filter.Text ?? "";
            Rebuild();
        };
        var add = new Button { Name = "outliner_add", Content = "+", Margin = new Thickness(Space.Xs, 0, 0, 0) };
        ToolTip.SetTip(add, "Add an object");
        add.Click += (_, _) => _openAdd();
        add[Grid.ColumnProperty] = 1;
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(8, 2, 6, 8) };
        head.Children.Add(filter);
        head.Children.Add(add);

        var list = new ScrollViewer { Content = new StackPanel { Children = { _rows, _nothing } } };
        list[Grid.RowProperty] = 1;
        _count[Grid.RowProperty] = 2;
        var frame = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        frame.Children.Add(head);
        frame.Children.Add(list);
        frame.Children.Add(_count);
        Content = frame;

        _rows.PointerMoved += OnRowsMoved;
        _rows.PointerReleased += (_, _) => _pending = null;
        Rebuild();
    }

    /// <summary>Drops what is being dragged on a row, at the height given as a fraction of it. False when the row refuses it.</summary>
    internal bool Release(string rowKey, string draggedId, double fraction)
    {
        var row = _model.Rows().FirstOrDefault(one => one.Key == rowKey);
        if (row is null || Band(row, draggedId, fraction) is not { } kind)
        {
            return false;
        }

        _session.Move(draggedId, OutlinerModel.PlaceFor(row, kind));
        return true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.Changed += Rebuild;
        _session.History.Changed += Rebuild;
        _session.Workspace.Changed += Rebuild;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.Changed -= Rebuild;
        _session.History.Changed -= Rebuild;
        _session.Workspace.Changed -= Rebuild;
        base.OnDetachedFromVisualTree(e);
    }

    private void Rebuild()
    {
        var rows = _model.Rows();
        _rows.Children.Clear();
        foreach (var row in rows)
        {
            _rows.Children.Add(RowView(row));
        }

        _nothing.IsVisible = rows.Count == 0;
        var scenes = _model.SceneCount;
        _count.Text = $"{scenes} scene{(scenes == 1 ? "" : "s")} open";
    }

    private Border RowView(OutlinerRow row)
    {
        var scene = row.Entry.Scene;
        var label = new TextBlock
        {
            Text = row.Item is { } one ? scene!.DisplayNameOf(one) : row.Entry.Title,
            FontSize = OrblitText.Label,
            FontWeight = row.Item is null ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = scene is null ? OrblitColors.InkDim : OrblitColors.Ink,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        line.Children.Add(Fold(row));
        label[Grid.ColumnProperty] = 1;
        line.Children.Add(label);

        var view = new Border
        {
            Name = $"row_{row.Key}",
            Padding = new Thickness(Space.Xs + (row.Depth * Indent), 3, Space.Xs, 3),
            CornerRadius = Radii.Control,
            Background = IsSelected(row) ? OrblitColors.EmberWash : Brushes.Transparent,
            Child = line,
        };
        if (row.Item is { } item)
        {
            line.Children.Add(Remove(item.Id, view));
        }

        view.PointerPressed += (_, press) => OnPressed(row, press);
        DragDrop.SetAllowDrop(view, true);
        view.AddHandler(DragDrop.DragOverEvent, (_, over) => OnOver(row, view, over));
        view.AddHandler(DragDrop.DragLeaveEvent, (_, _) => Mark(view, null));
        view.AddHandler(DragDrop.DropEvent, (_, drop) => OnDrop(row, view, drop));
        return view;
    }

    // A fold is a toggle that takes the press, so closing a parent does not also select it.
    private TextBlock Fold(OutlinerRow row)
    {
        var fold = new TextBlock
        {
            Name = $"fold_{row.Key}",
            Text = row.HasChildren ? (_model.IsCollapsed(row.Key) ? "▸" : "▾") : "",
            Width = 16,
            Background = Brushes.Transparent,
            Foreground = OrblitColors.InkDim,
            VerticalAlignment = VerticalAlignment.Center,
        };
        fold.PointerPressed += (_, press) =>
        {
            _model.Toggle(row.Key);
            press.Handled = true;
            Rebuild();
        };
        return fold;
    }

    // Shown only while the pointer is over the row, as in the Dart editor.
    private Button Remove(string id, Border view)
    {
        var remove = new Button
        {
            Name = $"delete_{id}",
            Content = "×",
            Padding = new Thickness(Space.Xs, 0),
            IsVisible = false,
        };
        remove[Grid.ColumnProperty] = 2;
        remove.Click += (_, _) => _session.Delete(id);
        view.PointerEntered += (_, _) => remove.IsVisible = true;
        view.PointerExited += (_, _) => remove.IsVisible = false;
        return remove;
    }

    private bool IsSelected(OutlinerRow row) => row.Item is { } one
        ? _session.Selected.Contains(one.Id)
        : _session.Selected.Count == 0 && _selectedScene == row.Entry.Id;

    private void OnPressed(OutlinerRow row, PointerPressedEventArgs press)
    {
        if (!press.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Out of the filter box, or Delete and the copy keys would go on meaning its text.
        Focus();
        if (row.Item is not { } one)
        {
            _selectedScene = row.Entry.Id;
            _session.ClearSelection();
            Rebuild();
            return;
        }

        var keys = press.KeyModifiers;
        _pending = (one.Id, press, press.GetPosition(this));
        _session.SelectObject(one.Id, additive: (keys & _command) != 0, range: keys.HasFlag(KeyModifiers.Shift));
    }

    private void OnRowsMoved(object? sender, PointerEventArgs move)
    {
        if (_pending is not { } pending || !move.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var from = pending.At;
        var to = move.GetPosition(this);
        if (Math.Abs(to.X - from.X) > DragStart || Math.Abs(to.Y - from.Y) > DragStart)
        {
            _pending = null;
            Drag(pending.Press, pending.Id);
        }
    }

    private static async void Drag(PointerPressedEventArgs press, string id)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(Dragged, id));
        await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Move);
    }

    // Null when the row refuses what is being dragged.
    private DropKind? Band(OutlinerRow row, string draggedId, double fraction) =>
        _model.Accepts(row, draggedId) ? OutlinerModel.BandAt(row, fraction) : null;

    private DropKind? Target(OutlinerRow row, Border view, DragEventArgs e) =>
        e.DataTransfer.TryGetValue(Dragged) is { } id && view.Bounds.Height > 0
            ? Band(row, id, e.GetPosition(view).Y / view.Bounds.Height)
            : null;

    private void OnOver(OutlinerRow row, Border view, DragEventArgs over)
    {
        var kind = Target(row, view, over);
        over.DragEffects = kind is null ? DragDropEffects.None : DragDropEffects.Move;
        over.Handled = true;
        Mark(view, kind);
    }

    private void OnDrop(OutlinerRow row, Border view, DragEventArgs drop)
    {
        Mark(view, null);
        if (drop.DataTransfer.TryGetValue(Dragged) is { } id && view.Bounds.Height > 0)
        {
            Release(row.Key, id, drop.GetPosition(view).Y / view.Bounds.Height);
        }

        drop.Handled = true;
    }

    // A line above or below for a place between rows, and a ring for inside one. A shadow is drawn
    // outside the row, so showing it moves nothing.
    private static void Mark(Border view, DropKind? kind)
    {
        view.BoxShadow = kind switch
        {
            DropKind.Before => new BoxShadows(new BoxShadow { OffsetY = -2, Color = OrblitColors.EmberColor }),
            DropKind.After => new BoxShadows(new BoxShadow { OffsetY = 2, Color = OrblitColors.EmberColor }),
            DropKind.Inside => new BoxShadows(new BoxShadow { Spread = 1, Color = OrblitColors.EmberColor }),
            _ => default,
        };
    }
}
