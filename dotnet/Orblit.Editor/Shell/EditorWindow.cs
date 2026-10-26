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
/// shortcut: a panel other than the viewport and the hierarchy shows its name until it is ported
/// (task 16), and the rail and tabs are words because no icon set is bundled yet (decision 84).
/// </remarks>
public sealed class EditorWindow : Window
{
    private readonly EditorSession _session;
    private readonly bool _renderer;
    private readonly Dictionary<string, Control> _panels = [];
    private readonly HostPlatform _platform;
    private readonly IReadOnlyDictionary<KeyChord, ShortcutAction> _shortcuts;
    private readonly Dictionary<EditorVerb, Action> _verbs;
    private readonly DockHost _dock;
    private readonly TextBlock _title = new() { Name = "title", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { Name = "status", FontSize = OrblitText.Caption };
    private readonly Dictionary<string, Button> _rail = [];
    private readonly EditorMenu _menu;

    // Keys that mean something else while a text box has the keyboard: delete a letter, copy some words.
    private static readonly HashSet<EditorVerb> ObjectVerbs = [EditorVerb.Delete, EditorVerb.Copy, EditorVerb.Cut, EditorVerb.Paste, EditorVerb.Duplicate];

    /// <param name="session">What the window shows and changes.</param>
    /// <param name="platform">The host's keys, or the one this process runs on.</param>
    /// <param name="renderer">
    /// Whether a viewport draws the scene with the renderer. Off, it keeps a plain grid, which is what
    /// a test without a native library wants.
    /// </param>
    public EditorWindow(EditorSession session, HostPlatform? platform = null, bool renderer = false)
    {
        _session = session;
        _renderer = renderer;
        _platform = platform ?? HostPlatforms.Current;
        _shortcuts = ShortcutMap.For(_platform);
        Width = 1440;
        Height = 900;
        Background = OrblitColors.Ground;

        _menu = new EditorMenu(_session, _platform, AskLayoutName);
        _verbs = new Dictionary<EditorVerb, Action>
        {
            [EditorVerb.Undo] = _session.Undo,
            [EditorVerb.Redo] = _session.Redo,
            [EditorVerb.NewScene] = _session.NewScene,
            [EditorVerb.Save] = _session.Save,
            [EditorVerb.SaveAs] = _session.AskForName,
            [EditorVerb.Palette] = () => OpenPalette(),
            [EditorVerb.Delete] = _session.DeleteSelection,
            [EditorVerb.Copy] = _session.Copy,
            [EditorVerb.Cut] = _session.Cut,
            [EditorVerb.Paste] = _session.Paste,
            [EditorVerb.Duplicate] = _session.Duplicate,
        };

        _dock = new DockHost(_session.VisibleLayout, PanelFor);
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
        _session.NameWanted += AskSceneName;
        _session.ConfirmLeaving = entry => LeaveDialog.Ask(this, entry);
        Closed += (_, _) =>
        {
            _session.Changed -= Refresh;
            _session.History.Changed -= Refresh;
            _session.Workspace.Changed -= Refresh;
            _session.Said -= Say;
            _session.NameWanted -= AskSceneName;
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

    // A panel is kept for each id and handed to whichever group shows it next. Building a new one on a
    // change of layout would free a viewport's renderer and lose its camera, or an outliner's filter.
    private Control PanelFor(DockedPanel panel)
    {
        if (!_panels.TryGetValue(panel.Id, out var built))
        {
            built = Build(panel);
            _panels[panel.Id] = built;
        }

        (built.Parent as Panel)?.Children.Remove(built);
        return built;
    }

    private Control Build(DockedPanel panel)
    {
        if (panel.Kind == PanelKind.Viewport)
        {
            return new SceneViewport(_session, _renderer);
        }

        if (panel.Kind == PanelKind.Outliner)
        {
            return new OutlinerPanel(_session, _platform, () => OpenPalette("Add "));
        }

        return panel.Kind == PanelKind.Inspector ? new InspectorPanel(_session) : Placeholder(panel);
    }

    private static TextBlock Placeholder(DockedPanel panel) => new TextBlock
    {
        Text = panel.Label,
        Foreground = OrblitColors.InkDim,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private async void AskSceneName()
    {
        var initial = _session.Workspace.Loaded?.Title ?? "";
        if (await NamePrompt.Ask(this, "Save scene as", "Goes in scenes/, as .oscene", initial) is { } name)
        {
            _session.SaveAs(name);
        }
    }

    private async void AskLayoutName()
    {
        if (await NamePrompt.Ask(this, "Save layout", "Name this layout") is { } name)
        {
            _session.SaveLayout(name);
        }
    }

    // The dialog is modal, so a second key press cannot open a second palette. What was chosen runs
    // once the dialog is gone, so a command that opens its own dialog is not stacked under this one.
    private async void OpenPalette(string query = "")
    {
        if (await PaletteDialog.Choose(this, PaletteEntries.For(_session, _platform), query) is { } chosen)
        {
            chosen.OnRun();
        }
    }

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
        if (!_verbs.TryGetValue(verb, out var run) || (ObjectVerbs.Contains(verb) && IsTyping()))
        {
            return false;
        }

        run();
        return true;
    }

    private bool IsTyping() => GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox;

    private void Say(string text, SayLevel level)
    {
        _status.Text = text;
        _status.Foreground = level switch
        {
            SayLevel.Error => OrblitColors.Bad,
            SayLevel.Warning => OrblitColors.Warn,
            _ => OrblitColors.InkMid,
        };
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

        _menu.Refresh();
        var scene = _session.Workspace.Loaded;
        var unsaved = scene is not null && _session.IsUnsaved(scene) ? " •" : "";
        var heading = scene?.Title ?? "No scene";
        _title.Text = $"{heading}{unsaved}";
        _title.Foreground = OrblitColors.InkMid;
        Title = $"Orblit Editor: {heading}{unsaved}";
    }
}
