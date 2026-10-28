using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using DockedPanel = Orblit.Editor.Core.DockPanel;
using Orblit.Asset;
using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Profile;
using Orblit.VectorMath;

/// <summary>
/// The editor's window: menus, the rail of modes, the panels and a line of status. It draws what an
/// <see cref="EditorSession"/> holds and calls it. It keeps no state of its own that the session
/// does not.
/// </summary>
/// <remarks>
/// shortcut: a panel not ported yet shows its name (task 16), and the rail and tabs are words because
/// no icon set is bundled yet (decision 84).
/// </remarks>
public sealed class EditorWindow : Window
{
    private readonly EditorSession _session;
    private readonly bool _renderer;

    // One for the window, so the modelling panel and every view agree on where a drag lands.
    private readonly Snapping _snapping = new();

    // One for the window, so the profile panel reads the frames the scene view draws.
    private readonly Profiler _profiler = Profiler.Create(heap: () => Environment.WorkingSet);
    private readonly Dictionary<string, Control> _panels = [];
    private readonly Dictionary<PanelKind, Func<Control>> _builders;
    private readonly HostPlatform _platform;
    private readonly CodeEditor _code;
    private readonly IReadOnlyDictionary<KeyChord, ShortcutAction> _shortcuts;
    private readonly Dictionary<EditorVerb, Action> _verbs;
    private readonly DockHost _dock;
    private readonly TextBlock _title = new() { Name = "title", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { Name = "status", FontSize = OrblitText.Caption };
    private readonly Dictionary<string, Button> _rail = [];
    private readonly EditorMenu _menu;
    private readonly TerrainToolShelf _shelf;
    private readonly CinematicsShelf _cinematics;
    private readonly InterfaceShelf _interfaces;
    private CookStatusIndex? _cooks;
    private TerrainBrushPanel? _brush;

    // The scene view last built, which Use this view asks where it is looking.
    // shortcut: a window with two scene views answers for the newer. Ask the focused one when that happens.
    private SceneViewport? _view;

    // Keys that mean something else while a text box has the keyboard: delete a letter, copy some words, finish a field, type a g or a bracket, move the caret.
    private static readonly HashSet<EditorVerb> ObjectVerbs =
    [
        EditorVerb.Delete, EditorVerb.Copy, EditorVerb.Cut, EditorVerb.Paste, EditorVerb.Duplicate, EditorVerb.LeaveEdit, EditorVerb.FinishDraw,
        EditorVerb.CycleMode, EditorVerb.GridCoarser, EditorVerb.GridFiner, EditorVerb.NudgeX, EditorVerb.NudgeY, EditorVerb.NudgeZ,
    ];

    // The kinds kept as text somebody writes by hand, which open in their code editor and not in a panel.
    private static readonly HashSet<AssetKind> EditedAsText = [AssetKind.Script, AssetKind.Style, AssetKind.Native, AssetKind.Data, AssetKind.Material, AssetKind.Surface];

    /// <summary>The grid the window's views and keys share. Read by tests, which cannot reach the field.</summary>
    internal Snapping Snapping => _snapping;

    /// <param name="session">What the window shows and changes.</param>
    /// <param name="platform">The host's keys, or the one this process runs on.</param>
    /// <param name="renderer">
    /// Whether a viewport draws the scene with the renderer. Off, it keeps a plain grid, which is what
    /// a test without a native library wants.
    /// </param>
    public EditorWindow(EditorSession session, HostPlatform? platform = null, bool renderer = false)
        : this(session, platform, renderer, CodeEditor.Here)
    {
    }

    /// <param name="session">What the window shows and changes.</param>
    /// <param name="platform">The host's keys, or the one this process runs on.</param>
    /// <param name="renderer">Whether a viewport draws the scene with the renderer.</param>
    /// <param name="code">What scripts and the project folder are handed to. A test's starts nothing.</param>
    internal EditorWindow(EditorSession session, HostPlatform? platform, bool renderer, CodeEditor code)
    {
        _session = session;
        _renderer = renderer;
        _code = code;
        _platform = platform ?? HostPlatforms.Current;
        _shortcuts = ShortcutMap.For(_platform);
        _builders = BuildersOfPanels();
        Width = 1440;
        Height = 900;
        Background = OrblitColors.Ground;

        var project = new ProjectCommands
        {
            Editor = _code.Available,
            OpenInCode = () => OpenInCode(),
            Reveal = () => Reveal(_session.Workspace.ProjectDirectory),
            Export = OpenExport,
            Store = OpenStore,
            Publish = OpenPublish,
        };
        _menu = new EditorMenu(_session, _platform, AskLayoutName, project);
        _verbs = new Dictionary<EditorVerb, Action>
        {
            [EditorVerb.Undo] = _session.Undo,
            [EditorVerb.Redo] = _session.Redo,
            [EditorVerb.NewScene] = _session.NewScene,
            [EditorVerb.Save] = _session.Save,
            [EditorVerb.SaveAs] = _session.AskForName,
            [EditorVerb.Palette] = () => OpenPalette(),
            [EditorVerb.Delete] = _session.Backspace,
            [EditorVerb.Copy] = _session.Copy,
            [EditorVerb.Cut] = _session.Cut,
            [EditorVerb.Paste] = _session.Paste,
            [EditorVerb.Duplicate] = _session.Duplicate,
            [EditorVerb.LeaveEdit] = _session.LeaveEdit,
            [EditorVerb.FinishDraw] = _session.FinishDrawing,
            [EditorVerb.CycleMode] = _session.CycleMode,
            [EditorVerb.GridCoarser] = GridCoarser,
            [EditorVerb.GridFiner] = GridFiner,
        };

        _shelf = new TerrainToolShelf(_session.Terrains);
        _cinematics = new CinematicsShelf(_session.Cutscenes, _session.NewCutscene, _session.AddShot, UseThisView);
        _interfaces = new InterfaceShelf(_session.Interfaces, _session.NewInterface);
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
        _session.Changed += RefreshBrush;
        _session.History.Changed += Refresh;
        _session.Workspace.Changed += Refresh;
        // Not the whole refresh. Pointing at an element changes the bench, and that happens on every move.
        _session.Interfaces.Changed += Retitle;
        _session.Said += Say;
        _session.NameWanted += AskSceneName;
        _session.ConfirmLeaving = entry => LeaveDialog.Ask(this, entry);
        Closed += (_, _) =>
        {
            _session.Changed -= Refresh;
            _session.Changed -= RefreshBrush;
            _session.History.Changed -= Refresh;
            _session.Workspace.Changed -= Refresh;
            _session.Interfaces.Changed -= Retitle;
            _session.Said -= Say;
            _session.NameWanted -= AskSceneName;
            // The session made the tree, and the window is what outlives its file watcher.
            _session.Assets.Dispose();
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

        // The shelf of brush tools is over the panels and not the rail, and takes no room when it is hidden.
        var centre = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        _dock[Grid.RowProperty] = 1;
        _dock.Margin = new Thickness(0, 0, Space.Sm, Space.Sm);
        centre.Children.Add(_shelf);
        centre.Children.Add(_cinematics);
        centre.Children.Add(_interfaces);
        centre.Children.Add(_dock);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        body.Children.Add(rail);
        centre[Grid.ColumnProperty] = 1;
        body.Children.Add(centre);

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

    private Control Build(DockedPanel panel) => _builders.TryGetValue(panel.Kind, out var make) ? make() : Placeholder(panel);

    // One line for each panel that is ported. A kind with no line shows its name.
    private Dictionary<PanelKind, Func<Control>> BuildersOfPanels() => new()
    {
        [PanelKind.Viewport] = SceneView,
        [PanelKind.Modelling] = () => new ModellingPanel(_session, _snapping),
        [PanelKind.Outliner] = () => new OutlinerPanel(_session, _platform, () => OpenPalette("Add ")),
        [PanelKind.Project] = Project,
        [PanelKind.Console] = () => new ConsolePanel(_session.Log, _session.TextClipboard),
        [PanelKind.Profile] = () => new ProfilePanel(_profiler),
        [PanelKind.Timeline] = Timeline,
        [PanelKind.CutsceneTimeline] = CutsceneTimeline,
        [PanelKind.ShotPreview] = () => new ShotPreviewPanel(_session, _renderer),
        [PanelKind.ShotList] = () => new ShotListPanel(_session),
        [PanelKind.BlendGraph] = BlendGraph,
        [PanelKind.TerrainBrush] = Brush,
        [PanelKind.Uvs] = () => new UvPanel(_session),
        [PanelKind.Inspector] = Inspector,
        [PanelKind.InterfaceCanvas] = () => new InterfaceCanvas(_session.Interfaces, _session.NewInterface),
        [PanelKind.InterfaceElements] = () => new InterfaceElements(_session.Interfaces),
        [PanelKind.InterfaceDesign] = () => new InterfaceDesign(_session.Interfaces),
    };

    // A canvas names its interface relative to the project.
    private InspectorPanel Inspector() => new(_session, new InspectorLinks
    {
        Terrain = _session.Terrains,
        OnOpenData = _session.ShowData,
        OnDetachData = _session.DetachData,
        OnOpenInterface = relative => _session.OpenInterface(Path.Combine(_session.Workspace.ProjectDirectory, relative)),
        OnApplyPrefab = _session.ApplyPrefab,
        OnRevertPrefab = _session.RevertPrefab,
        OnUnpackPrefab = _session.UnpackPrefab,
    });

    private SceneViewport SceneView()
    {
        _view = new SceneViewport(_session, _renderer, snapping: _snapping) { Profiler = _profiler };
        return _view;
    }

    // Kept so the window can ask it to look again when the selection moves a stroke to another terrain.
    private TerrainBrushPanel Brush()
    {
        _brush = new TerrainBrushPanel(_session.Terrains, _session.History, _session.TerrainTarget, _session.NewTerrain);
        return _brush;
    }

    private TimelinePanel Timeline()
    {
        var links = new TimelineLinks
        {
            OnNew = _session.NewClip,
            OnProblem = problem => _session.Report([problem]),
            OnRetarget = RetargetClip,
            Selected = () => _session.InspectedItem,
        };
        return new TimelinePanel(_session.Clips, links);
    }

    // A cutscene has no bones, so it has nothing to retarget.
    private TimelinePanel CutsceneTimeline()
    {
        var links = new TimelineLinks
        {
            OnNew = _session.NewCutscene,
            OnProblem = problem => _session.Report([problem]),
            Selected = () => _session.InspectedItem,
        };
        return new TimelinePanel(_session.Cutscenes, links);
    }

    private void UseThisView()
    {
        if (_view is not null && _session.Workspace.Loaded?.Scene is { } scene)
        {
            _session.UseThisView(CameraViews.PlaceFor(_view.State.Camera, scene));
        }
    }

    // The made clip is a clip of its own, so the shown one is left as it was.
    private async void RetargetClip()
    {
        if (_session.Clips.Shown is not { } shown)
        {
            return;
        }

        var files = new ProjectRetargetFiles(_session.Assets, _session.Skeletons);
        if (await RetargetDialog.Choose(this, shown.Clip, files, _session.OwnerModel) is { } made)
        {
            _session.SaveRetargeted(made.Clip, made.Model, made.Problems);
        }
    }

    private BlendGraphPanel BlendGraph() => new(_session.Blends, _session.History, _session.ClipNames, _session.NewBlend);

    // The session's tree and one cook index for the window, made when the first project panel is. Two
    // panels on one project look at the same cache, and hashing it twice would say nothing new.
    private AssetBrowserPanel Project()
    {
        var directory = _session.Workspace.ProjectDirectory;
        _cooks ??= new CookStatusIndex(directory);
        var links = new AssetBrowserLinks
        {
            OnOpenAsset = OpenAsset,
            OnSelectAsset = _session.PickAsset,
            OnProblem = problem => _session.Report([problem]),
            OnBuild = BuildScript,
            OnMakePrefab = _session.MakePrefab,
        };
        return new AssetBrowserPanel(_session.Assets, _cooks, links, _renderer);
    }

    // The other kinds open in the panels that edit them, and those arrive with their rows (5 to 13).
    private void OpenAsset(Asset asset)
    {
        if (EditedAsText.Contains(asset.Kind))
        {
            OpenInCode(asset.Path);
            return;
        }

        if (asset.Kind == AssetKind.Clip)
        {
            _session.OpenClip(asset.Path);
            return;
        }

        if (asset.Kind == AssetKind.Blend)
        {
            _session.OpenBlend(asset.Path);
            return;
        }

        if (asset.Kind == AssetKind.Cutscene)
        {
            _session.OpenCutscene(asset.Path);
            return;
        }

        if (asset.Kind == AssetKind.Canvas)
        {
            _session.OpenInterface(asset.Path);
            return;
        }

        var scene = _session.Workspace.Entries.FirstOrDefault(entry => entry.Path == asset.Path);
        if (asset.Kind == AssetKind.Scene && scene is not null)
        {
            _session.LoadScene(scene);
        }
    }

    private void OpenInCode(string? file = null)
    {
        if (_code.Open(_session.Workspace.ProjectDirectory, file) is { } problem)
        {
            _session.Say(problem, SayLevel.Info);
            return;
        }

        var editor = _code.Available ?? "your editor";
        _session.Say(file is null ? $"Opened the project in {editor}." : $"Opened {Path.GetFileName(file)} in {editor}.", SayLevel.Info);
    }

    private void Reveal(string path)
    {
        if (_code.Reveal(path) is { } problem)
        {
            _session.Say(problem, SayLevel.Info);
        }
    }

    private async void OpenExport() =>
        await ExportDialog.Open(this, new ExportSession(_session.Workspace.ProjectDirectory, new DiskExports()), Reveal);

    private async void OpenStore()
    {
        var directory = _session.Workspace.ProjectDirectory;
        var support = HostPaths.SupportFolder();
        // A project file with no engine reads as unknown, and the store then lists kits for any engine.
        var engine = ProjectStore.Open(directory)?.EngineVersion ?? "unknown";
        var store = new StoreSession(new StoreSettings(Path.Join(support, "store.json")), engine, () => new HttpTransport());
        await StoreDialog.Open(this, store, (row, release) => new InstallSession(row, release, directory, Path.Join(support, "kit-downloads"), new HttpTransport()));
    }

    private async void OpenPublish() =>
        await PublishDialog.Open(this, new PublishSession(_session.Workspace.ProjectDirectory), _session.TextClipboard);

    // Internal for tests, which cannot open the tile's menu that runs it. The compile is off the UI
    // thread, because a big file takes seconds and the window would stop drawing.
    internal async void BuildScript(Asset asset)
    {
        var name = Path.GetFileName(asset.Path);
        var directory = _session.Workspace.ProjectDirectory;
        _session.Say($"Building {name}…", SayLevel.Info);
        var built = await Task.Run(() => new ScriptBuilder(directory).Build(asset.Path));
        if (!IsVisible)
        {
            return;
        }

        if (built.Ok)
        {
            var warned = built.Output.Length > 0;
            _session.Say(warned ? $"Built {name}. With warnings." : $"Built {name}.", warned ? SayLevel.Warning : SayLevel.Info, built.Output);
            if (!warned)
            {
                return;
            }
        }
        else
        {
            _session.Say($"{name} did not build.", SayLevel.Error, built.Output);
        }

        await BuildDialog.Tell(this, built.Ok ? $"{name} built, with warnings" : $"{name} did not build", built.Output);
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
        // Here and not in PaletteEntries, because handing a file to another program and opening a dialog
        // are the window's job.
        PaletteEntry openInCode = new() { Group = PaletteGroup.Commands, Label = "Open in code editor", Icon = "code", OnRun = () => OpenInCode() };
        PaletteEntry export = new() { Group = PaletteGroup.Commands, Label = "Export…", Icon = "inventory_2_outlined", OnRun = OpenExport };
        PaletteEntry store = new() { Group = PaletteGroup.Commands, Label = "Kit store…", Icon = "storefront_outlined", OnRun = OpenStore };
        PaletteEntry publish = new() { Group = PaletteGroup.Commands, Label = "Publish kit…", Icon = "upload_outlined", OnRun = OpenPublish };
        if (await PaletteDialog.Choose(this, [.. PaletteEntries.For(_session, _platform), openInCode, export, store, publish], query) is { } chosen)
        {
            chosen.OnRun();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs press)
    {
        if (KeyMapping.ChordOf(press) is { } chord
            && _shortcuts.TryGetValue(chord, out var action)
            && RunVerb(action))
        {
            press.Handled = true;
        }
    }

    // Only the verbs the session can do so far. The rest pass through, so a key is never swallowed by
    // something that does nothing with it.
    private bool RunVerb(ShortcutAction action)
    {
        if (ObjectVerbs.Contains(action.Verb) && IsTyping())
        {
            return false;
        }

        if (AxisOf(action.Verb) is { } axis)
        {
            // By squares even with the grid off: an arrow key asks for a definite amount, and a square is it.
            _session.Nudge(axis * (_snapping.Step * action.Squares));
            return true;
        }

        if (!_verbs.TryGetValue(action.Verb, out var run))
        {
            return false;
        }

        run();
        return true;
    }

    private static Vector3? AxisOf(EditorVerb verb) => verb switch
    {
        EditorVerb.NudgeX => new Vector3(1, 0, 0),
        EditorVerb.NudgeY => new Vector3(0, 1, 0),
        EditorVerb.NudgeZ => new Vector3(0, 0, 1),
        _ => null,
    };

    // Asking for a grid is asking to use it, so the key switches snapping on as well.
    private void GridCoarser()
    {
        _snapping.Step = _snapping.Coarser;
        _snapping.On = true;
    }

    private void GridFiner()
    {
        _snapping.Step = _snapping.Finer;
        _snapping.On = true;
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

    // Which terrain the brush works on follows the selection, so a move of the selection is when to look again.
    private void RefreshBrush() => _brush?.Refresh();

    private void Refresh()
    {
        _dock.Layout = _session.VisibleLayout;
        _shelf.IsVisible = _session.Mode.UsesBrush;
        _cinematics.IsVisible = _session.Mode.UsesCutscenes;
        _interfaces.IsVisible = _session.Mode.UsesInterfaces;
        foreach (var (name, button) in _rail)
        {
            var active = name == _session.Mode.Name;
            button.Background = active ? OrblitColors.EmberWash : Brushes.Transparent;
            button.Foreground = active ? OrblitColors.Ember : OrblitColors.InkMid;
        }

        _menu.Refresh();
        Retitle();
    }

    // The file being worked on: the interface shown in the Interface workspace, and the scene everywhere else.
    private void Retitle()
    {
        string heading;
        bool changed;
        if (_session.Mode.UsesInterfaces && _session.Interfaces.Shown is { } shown)
        {
            heading = _session.Assets.Relative(shown.Path);
            changed = _session.Interfaces.IsUnsaved(shown);
        }
        else
        {
            var scene = _session.Workspace.Loaded;
            heading = scene?.Title ?? "No scene";
            // A stroke is a step on a terrain's own history, which the scene's marker does not see.
            changed = scene is not null && (_session.IsUnsaved(scene) || _session.Terrains.AnyUnsaved);
        }

        var unsaved = changed ? " •" : "";
        _title.Text = $"{heading}{unsaved}";
        _title.Foreground = OrblitColors.InkMid;
        Title = $"Orblit Editor: {heading}{unsaved}";
    }
}
