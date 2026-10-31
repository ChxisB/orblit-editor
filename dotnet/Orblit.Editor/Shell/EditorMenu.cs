using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Mesh;

/// <summary>The menu bar. What each entry says and whether it can be used follows the session.</summary>
internal sealed class EditorMenu : Menu
{
    private readonly EditorSession _session;
    private readonly MenuItem _file;
    private readonly MenuItem _save;
    private readonly MenuItem _undo;
    private readonly MenuItem _redo;
    private readonly MenuItem _cut;
    private readonly MenuItem _copy;
    private readonly MenuItem _paste;
    private readonly MenuItem _duplicate;
    private readonly MenuItem _delete;
    private readonly MenuItem _stats;
    private readonly MenuItem _focus;
    private readonly MenuItem _loadLayout;
    private readonly MenuItem _deleteLayout;
    private readonly MenuItem _lock;

    /// <param name="session">What the entries read and run.</param>
    /// <param name="platform">The host's keys, for the shortcuts shown beside each entry.</param>
    /// <param name="askLayoutName">Opens the dialog that names a layout, which the window owns.</param>
    /// <param name="project">Hands the project folder to the desktop, which the window owns.</param>
    public EditorMenu(EditorSession session, HostPlatform platform, Action askLayoutName, ProjectCommands project)
    {
        _session = session;
        Name = "menu";

        var newScene = Item("menu_new_scene", "New scene", session.NewScene, platform.CommandShortcut(EditorKey.KeyN));
        _save = Item("menu_save", "Save", session.Save, platform.CommandShortcut(EditorKey.KeyS));
        var saveAs = Item("menu_save_as", "Save as…", session.AskForName, platform.CommandShortcut(EditorKey.KeyS, shift: true));
        _undo = Item("menu_undo", "Undo", session.Undo, platform.CommandShortcut(EditorKey.KeyZ));
        _redo = Item("menu_redo", "Redo", session.Redo, platform.CommandShortcut(EditorKey.KeyZ, shift: true));
        _cut = Item("menu_cut", "Cut", session.Cut, platform.CommandShortcut(EditorKey.KeyX));
        _copy = Item("menu_copy", "Copy", session.Copy, platform.CommandShortcut(EditorKey.KeyC));
        _paste = Item("menu_paste", "Paste", session.Paste, platform.CommandShortcut(EditorKey.KeyV));
        _duplicate = Item("menu_duplicate", "Duplicate", session.Duplicate, platform.CommandShortcut(EditorKey.KeyD));
        _delete = Item("menu_delete", "Delete", session.DeleteSelection, new KeyChord(EditorKey.Delete));
        _stats = Item("menu_stats", "Performance stats", session.ToggleStats);
        _stats.ToggleType = MenuItemToggleType.CheckBox;
        _focus = Item("menu_focus", "Focus view", session.ToggleFocus);
        _loadLayout = new MenuItem { Name = "menu_load_layout", Header = "Load layout" };
        _deleteLayout = new MenuItem { Name = "menu_delete_layout", Header = "Delete layout" };
        _lock = Item("menu_lock", "Lock layout", ToggleLock);

        var export = Item("menu_export", "Export…", project.Export);
        var store = Item("menu_kit_store", "Kit store…", project.Store);
        var publish = Item("menu_publish_kit", "Publish kit…", project.Publish);
        var openInCode = Item("menu_open_in_code", $"Open project in {project.Editor ?? "VS Code"}", project.OpenInCode);
        var reveal = Item("menu_reveal", platform == HostPlatform.MacOS ? "Show in Finder" : "Show project folder", project.Reveal);

        _file = Menu("File", newScene, _save, saveAs, new Separator(), export, store, publish, openInCode, reveal);
        Items.Add(_file);
        Items.Add(Menu("Edit", _undo, _redo, new Separator(), _cut, _copy, _paste, _duplicate, _delete));
        Items.Add(AddItems());
        Items.Add(Menu("View", _stats, _focus, Item("menu_save_layout", "Save layout…", askLayoutName), _loadLayout, _deleteLayout, _lock, Item("menu_reset", "Reset panels", session.ResetPanels), PanelsMenu()));
    }

    /// <summary>Brings every entry's words and whether it is enabled up to date.</summary>
    public void Refresh()
    {
        var history = _session.History;
        var selected = _session.Selected.Count > 0;
        // The dot is the unsaved marker, in the menu somebody opens to save.
        _file.Header = _session.AnyUnsaved ? "File •" : "File";
        _save.IsEnabled = _session.Workspace.Loaded is not null;
        _undo.Header = history.UndoLabel is { } undo ? $"Undo {undo}" : "Undo";
        _undo.IsEnabled = history.CanUndo;
        _redo.Header = history.RedoLabel is { } redo ? $"Redo {redo}" : "Redo";
        _redo.IsEnabled = history.CanRedo;
        _cut.Header = _session.CutLabel;
        _cut.IsEnabled = selected;
        _copy.Header = _session.CopyLabel;
        _copy.IsEnabled = selected;
        _paste.Header = _session.PasteLabel;
        _duplicate.IsEnabled = selected;
        _delete.IsEnabled = selected;
        _stats.IsChecked = _session.ShowStats;
        _focus.Header = _session.IsFocused ? "Show panels" : "Focus view";
        _lock.Header = _session.Layout.Locked ? "Unlock layout" : "Lock layout";
        Fill(_loadLayout, "load", _session.LoadLayout);
        Fill(_deleteLayout, "delete", _session.DeleteLayout);
    }

    private static MenuItem Menu(string header, params Control[] items)
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

    // Shapes are their own submenu: there are seven of them and they are what somebody reaches for
    // most while blocking a level out.
    private MenuItem AddItems()
    {
        var shapes = Menu("Shape", [.. Enum.GetValues<ShapeKind>().Select(kind => Item($"add_shape_{kind}", kind.Label, () => _session.AddShape(kind)))]);
        shapes.Name = "menu_add_shape";
        var kinds = AddMenu.Items.Select(add => Item($"add_{add.Kind}", add.Label, () => _session.Add(add.Kind)));
        return Menu("Add", [shapes, new Separator(), .. kinds, Item("add_terrain", "Terrain", _session.NewTerrain)]);
    }

    private MenuItem PanelsMenu()
    {
        var panels = new MenuItem { Name = "menu_panels", Header = "Panels" };
        foreach (var type in _session.Panels.All)
        {
            // Through Relayout so the panel is kept with the project. OpenPanel is for the editor's own
            // asks, such as showing the timeline for a clip, which should not outlast the session.
            panels.Items.Add(Item($"panel_{type.Name}", type.Kind.Label, () => _session.Relayout(_session.Layout.Add(type.Panel))));
        }

        return panels;
    }

    private void ToggleLock() => _session.Relayout(_session.Layout.CopyWith(locked: !_session.Layout.Locked));

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

/// <summary>
/// The menu's entries that act on the whole project: its export, its kits, and handing it to programs
/// outside the editor.
/// </summary>
internal sealed record ProjectCommands
{
    /// <summary>The code editor found, to name on the menu, or null.</summary>
    public required string? Editor { get; init; }

    public required Action OpenInCode { get; init; }

    public required Action Reveal { get; init; }

    public required Action Export { get; init; }

    public required Action Store { get; init; }

    public required Action Publish { get; init; }
}
