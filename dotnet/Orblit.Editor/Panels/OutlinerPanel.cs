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
/// shortcut: rows show words and no kind icon (decision 84), so a scene row has no globe either.
/// </remarks>
internal sealed class OutlinerPanel : UserControl
{
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
        _session.History.Changed += OnHistoryChanged;
        _session.Workspace.Changed += Rebuild;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.Changed -= Rebuild;
        _session.History.Changed -= OnHistoryChanged;
        _session.Workspace.Changed -= Rebuild;
        base.OnDetachedFromVisualTree(e);
    }

    // A row shows no position, and a drag runs a move every frame.
    private void OnHistoryChanged()
    {
        if (!_session.History.LastOnlyMoved)
        {
            Rebuild();
        }
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
        var name = NameOf(row);
        var label = new TextBlock
        {
            Text = name,
            FontSize = OrblitText.Label,
            FontWeight = row.Item is null ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = row.Entry.Scene is null ? OrblitColors.InkDim : OrblitColors.Ink,
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
        line.Children.Add(Tail(row, name, view));
        if (row.Item is null)
        {
            ToolTip.SetTip(view, row.Entry.IsLoaded ? name : $"Double-click to load {name}");
            ToolTip.SetShowDelay(view, 700);
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

    // Through the scene rather than off the object, so a light that follows the sky says Moon here
    // at the same moment the viewport goes dark.
    private static string NameOf(OutlinerRow row)
    {
        if (row.Item is { } item)
        {
            return row.Entry.Scene!.DisplayNameOf(item);
        }

        return row.Entry.Id == Workspace.SharedSceneId ? "In every scene" : row.Entry.Title;
    }

    // What follows a name: that a scene is not loaded, that it was never written, and the button that
    // removes the row while the pointer is over it. The note gives way to the button.
    private StackPanel Tail(OutlinerRow row, string name, Border view)
    {
        var tail = new StackPanel { Orientation = Orientation.Horizontal };
        tail[Grid.ColumnProperty] = 2;
        var isScene = row.Item is null;
        var note = isScene && !row.Entry.IsLoaded ? Note("not loaded", OrblitText.Mono) : null;
        if (note is not null)
        {
            tail.Children.Add(note);
        }

        if (isScene && row.Entry.IsLoaded && row.Entry.NeverWritten)
        {
            tail.Children.Add(Note("•", OrblitText.Mono, OrblitColors.Ember));
        }

        if (InstanceMark(row) is { } mark)
        {
            tail.Children.Add(mark);
        }

        var remove = RemoveButton(row, name);
        if (remove is not null)
        {
            tail.Children.Add(remove);
        }

        view.PointerEntered += (_, _) => Hover(note, remove, true);
        view.PointerExited += (_, _) => Hover(note, remove, false);
        return tail;
    }

    // An instance names its prefab, so its parts read as the prefab's and not as loose objects.
    private static Glyph? InstanceMark(OutlinerRow row)
    {
        if (row.Item?.Prefab?.Asset is not { } asset)
        {
            return null;
        }

        var mark = new Glyph(AssetIcons.Of(AssetKind.Prefab), 11, OrblitColors.InkDim) { Name = $"instance_{row.Key}", Margin = new Thickness(0, 0, Space.Xs, 0) };
        ToolTip.SetTip(mark, $"Instance of {Path.GetFileName(asset)}");
        return mark;
    }

    private static void Hover(TextBlock? note, Button? remove, bool over)
    {
        if (note is not null)
        {
            note.IsVisible = !over;
        }

        if (remove is not null)
        {
            remove.IsVisible = over;
        }
    }

    private static TextBlock Note(string text, FontFamily font, IBrush? colour = null) => new()
    {
        Text = text,
        FontSize = 10,
        FontFamily = font,
        Foreground = colour ?? OrblitColors.InkDim,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, Space.Xs, 0),
    };

    // The shared set is never closed, so its row has none. The Dart editor offers one there that
    // forgets the set's undo steps and does nothing else.
    private Button? RemoveButton(OutlinerRow row, string name)
    {
        if (row.Item is { } item)
        {
            return HoverButton($"delete_{item.Id}", $"Delete {name}", () => _session.Delete(item.Id));
        }

        return row.Entry.Id == Workspace.SharedSceneId
            ? null
            : HoverButton($"scene_close_{row.Entry.Id}", $"Close {name}", () => _session.CloseScene(row.Entry));
    }

    private static Button HoverButton(string name, string tip, Action run)
    {
        var button = new Button
        {
            Name = name,
            Content = "×",
            Padding = new Thickness(Space.Xs, 0),
            IsVisible = false,
        };
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => run();
        return button;
    }

    private bool IsSelected(OutlinerRow row) => row.Item is { } one
        ? _session.Selected.Contains(one.Id)
        : _session.SelectedScene == row.Entry.Id;

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
            _session.SelectScene(row.Entry.Id);
            if (press.ClickCount == 2)
            {
                _session.LoadScene(row.Entry);
            }

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
        data.Add(DataTransferItem.Create(ObjectDrops.Format, id));
        await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Move);
    }

    // Null when the row refuses what is being dragged.
    private DropKind? Band(OutlinerRow row, string draggedId, double fraction) =>
        _model.Accepts(row, draggedId) ? OutlinerModel.BandAt(row, fraction) : null;

    private DropKind? Target(OutlinerRow row, Border view, DragEventArgs e) =>
        e.DataTransfer.TryGetValue(ObjectDrops.Format) is { } id && view.Bounds.Height > 0
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
        if (drop.DataTransfer.TryGetValue(ObjectDrops.Format) is { } id && view.Bounds.Height > 0)
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
