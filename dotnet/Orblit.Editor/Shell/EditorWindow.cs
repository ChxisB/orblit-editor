using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using DockedPanel = Orblit.Editor.Core.DockPanel;
using Orblit.Editor.Core;

/// <summary>
/// The editor's window: menus, the rail of modes, the panels and a line of status. It draws what an
/// <see cref="EditorSession"/> holds and calls it. It keeps no state of its own that the session
/// does not.
/// </summary>
/// <remarks>
/// shortcut: a panel shows its name until it is ported (the viewport in task 15, the rest in task 16),
/// and the rail and tabs are words because no icon set is bundled yet (decision 84).
/// </remarks>
public sealed class EditorWindow : Window
{
    private readonly EditorSession _session;
    private readonly HostPlatform _platform;
    private readonly IReadOnlyDictionary<KeyChord, ShortcutAction> _shortcuts;
    private readonly DockHost _dock;
    private readonly TextBlock _title = new() { Name = "title", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { Name = "status", FontSize = OrblitText.Caption };
    private readonly Dictionary<string, Button> _rail = [];
    private readonly Menu _menu = new() { Name = "menu" };
    private readonly MenuItem _save;
    private readonly MenuItem _undo;
    private readonly MenuItem _redo;
    private readonly MenuItem _stats;
    private readonly MenuItem _focus;
    private readonly MenuItem _loadLayout;
    private readonly MenuItem _deleteLayout;
    private readonly MenuItem _lock;

    public EditorWindow(EditorSession session, HostPlatform? platform = null)
    {
        _session = session;
        _platform = platform ?? HostPlatforms.Current;
        _shortcuts = ShortcutMap.For(_platform);
        Width = 1440;
        Height = 900;
        Background = OrblitColors.Ground;

        _save = Item("menu_save", "Save", _session.Save, _platform.CommandShortcut(EditorKey.KeyS));
        _undo = Item("menu_undo", "Undo", _session.Undo, _platform.CommandShortcut(EditorKey.KeyZ));
        _redo = Item("menu_redo", "Redo", _session.Redo, _platform.CommandShortcut(EditorKey.KeyZ, shift: true));
        _stats = Item("menu_stats", "Performance stats", _session.ToggleStats);
        _stats.ToggleType = MenuItemToggleType.CheckBox;
        _focus = Item("menu_focus", "Focus view", _session.ToggleFocus);
        _loadLayout = new MenuItem { Name = "menu_load_layout", Header = "Load layout" };
        _deleteLayout = new MenuItem { Name = "menu_delete_layout", Header = "Delete layout" };
        _lock = Item("menu_lock", "Lock layout", ToggleLock);

        _menu.Items.Add(Menu("File", _save));
        _menu.Items.Add(Menu("Edit", _undo, _redo));
        _menu.Items.Add(Menu("View", _stats, _focus, Item("menu_save_layout", "Save layout…", AskLayoutName), _loadLayout, _deleteLayout, _lock, Item("menu_reset", "Reset panels", _session.ResetPanels), PanelsMenu()));

        _dock = new DockHost(_session.VisibleLayout, Placeholder);
        _dock.LayoutChanged += layout =>
        {
            // A focused view is only a hiding of the arrangement. Taking a change to it would save the hiding.
            if (!_session.IsFocused)
            {
                _session.Relayout(layout);
            }
        };

        Content = Frame();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        _session.Changed += Refresh;
        _session.History.Changed += Refresh;
        _session.Workspace.Changed += Refresh;
        _session.Said += Say;
        Closed += (_, _) =>
        {
            _session.Changed -= Refresh;
            _session.History.Changed -= Refresh;
            _session.Workspace.Changed -= Refresh;
            _session.Said -= Say;
        };

        if (_session.Last is { } last)
        {
            Say(last.Text, last.Level);
        }

        Refresh();
    }

    private Grid Frame()
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Background = OrblitColors.Surface };
        top.Children.Add(_menu);
        _title[Grid.ColumnProperty] = 1;
        top.Children.Add(_title);
        var search = new Button
        {
            Name = "search_button",
            Content = $"Search commands and objects  {_platform.CommandShortcutLabel("K")}",
            FontSize = OrblitText.Caption,
            Margin = new Thickness(Space.Xs),
        };
        search.Click += (_, _) => OpenPalette();
        search[Grid.ColumnProperty] = 2;
        top.Children.Add(search);

        var rail = new StackPanel { Name = "rail", Margin = new Thickness(Space.Sm), Spacing = Space.Xs };
        foreach (var mode in _session.Modes.All)
        {
            var button = new Button { Name = $"mode_{mode.Name}", Content = mode.Label, FontSize = OrblitText.Label, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => _session.EnterMode(mode);
            _rail[mode.Name] = button;
            rail.Children.Add(button);
        }

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        body.Children.Add(rail);
        _dock[Grid.ColumnProperty] = 1;
        _dock.Margin = new Thickness(0, 0, Space.Sm, Space.Sm);
        body.Children.Add(_dock);

        var bottom = new Border { Background = OrblitColors.Surface, Padding = new Thickness(Space.Md, Space.Xs), Child = _status };

        var frame = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        frame.Children.Add(top);
        body[Grid.RowProperty] = 1;
        frame.Children.Add(body);
        bottom[Grid.RowProperty] = 2;
        frame.Children.Add(bottom);
        return frame;
    }

    private static MenuItem Menu(string header, params MenuItem[] items)
    {
        var menu = new MenuItem { Header = header, Name = $"menu_{header.ToLowerInvariant()}" };
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }

        return menu;
    }

    private static MenuItem Item(string name, string header, Action run, KeyChord? chord = null)
    {
        var item = new MenuItem { Name = name, Header = header };
        if (chord is { } shortcut)
        {
            item.InputGesture = KeyMapping.GestureOf(shortcut);
        }

        item.Click += (_, _) => run();
        return item;
    }

    private MenuItem PanelsMenu()
    {
        var panels = new MenuItem { Name = "menu_panels", Header = "Panels" };
        foreach (var type in _session.Panels.All)
        {
            panels.Items.Add(Item($"panel_{type.Name}", type.Kind.Label, () => _session.OpenPanel(type.Kind)));
        }

        return panels;
    }

    private static TextBlock Placeholder(DockedPanel panel) => new TextBlock
    {
        Text = panel.Label,
        Foreground = OrblitColors.InkDim,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private async void AskLayoutName()
    {
        if (await NamePrompt.Ask(this, "Save layout", "Name this layout") is { } name)
        {
            _session.SaveLayout(name);
        }
    }

    // The dialog is modal, so a second key press cannot open a second palette. What was chosen runs
    // once the dialog is gone, so a command that opens its own dialog is not stacked under this one.
    private async void OpenPalette()
    {
        if (await PaletteDialog.Choose(this, PaletteEntries.For(_session, _platform)) is { } chosen)
        {
            chosen.OnRun();
        }
    }

    private void ToggleLock() => _session.Relayout(_session.Layout.CopyWith(locked: !_session.Layout.Locked));

    private void OnKeyDown(object? sender, KeyEventArgs press)
    {
        if (KeyMapping.ChordOf(press) is { } chord
            && _shortcuts.TryGetValue(chord, out var action)
            && RunVerb(action.Verb))
        {
            press.Handled = true;
        }
    }

    // Only the verbs the session can do so far. The rest pass through, so a key is never swallowed by
    // something that does nothing with it.
    private bool RunVerb(EditorVerb verb)
    {
        switch (verb)
        {
            case EditorVerb.Undo:
                _session.Undo();
                return true;
            case EditorVerb.Redo:
                _session.Redo();
                return true;
            case EditorVerb.Save:
                _session.Save();
                return true;
            case EditorVerb.Palette:
                OpenPalette();
                return true;
            default:
                return false;
        }
    }

    private void Say(string text, SayLevel level)
    {
        _status.Text = text;
        _status.Foreground = level == SayLevel.Error ? OrblitColors.Bad : OrblitColors.InkMid;
    }

    private void Refresh()
    {
        _dock.Layout = _session.VisibleLayout;
        foreach (var (name, button) in _rail)
        {
            var active = name == _session.Mode.Name;
            button.Background = active ? OrblitColors.EmberWash : Brushes.Transparent;
            button.Foreground = active ? OrblitColors.Ember : OrblitColors.InkMid;
        }

        RefreshMenu();
        var scene = _session.Workspace.Loaded;
        var unsaved = scene is not null && _session.IsUnsaved(scene) ? " •" : "";
        var heading = scene?.Title ?? "No scene";
        _title.Text = $"{heading}{unsaved}";
        _title.Foreground = OrblitColors.InkMid;
        Title = $"Orblit Editor: {heading}{unsaved}";
    }

    private void RefreshMenu()
    {
        var history = _session.History;
        _save.IsEnabled = _session.Workspace.Loaded is not null;
        _undo.Header = history.UndoLabel is { } undo ? $"Undo {undo}" : "Undo";
        _undo.IsEnabled = history.CanUndo;
        _redo.Header = history.RedoLabel is { } redo ? $"Redo {redo}" : "Redo";
        _redo.IsEnabled = history.CanRedo;
        _stats.IsChecked = _session.ShowStats;
        _focus.Header = _session.IsFocused ? "Show panels" : "Focus view";
        _lock.Header = _session.Layout.Locked ? "Unlock layout" : "Lock layout";
        Fill(_loadLayout, "load", _session.LoadLayout);
        Fill(_deleteLayout, "delete", _session.DeleteLayout);
    }

    private void Fill(MenuItem menu, string verb, Action<string> run)
    {
        menu.Items.Clear();
        foreach (var name in _session.SavedLayouts)
        {
            menu.Items.Add(Item($"{verb}_layout_{name}", name, () => run(name)));
        }

        menu.IsEnabled = menu.Items.Count > 0;
    }
}
