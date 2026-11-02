using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using Orblit.Filament.Avalonia;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Filament;
using Orblit.Game.Avalonia;

/// <summary>
/// The scene as the game shows it: through the scene's first camera, with nothing of the editor drawn
/// over it. While the scene plays it shows the playing copy, and at rest it shows the scene as edited.
/// </summary>
/// <remarks>
/// While a scene plays the picture gives the game the keys and the pointer, and the game's own control is
/// shown over it. A key pressed in the view is the game's and never reaches the editor's shortcuts.
/// <para>
/// shortcut: this draws the scene and not the game's sound. Terrain is not drawn in a C# viewport yet, so
/// it is missing here as it is in the scene view.
/// </para>
/// </remarks>
internal sealed class GameViewPanel : Border
{
    private const string NoScene = "No scene is open.";
    private const string NoCamera = "This scene has no camera, so the game has nothing to look through.";
    private const KeyModifiers EditorKeys = KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt;

    private readonly EditorSession _session;
    private readonly GeometryStore _geometry;
    private readonly RendererViewport? _renderer;
    private readonly TextBlock _note = new()
    {
        Name = "game_view_note",
        FontSize = OrblitText.Label,
        Foreground = OrblitColors.InkDim,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly Button _addCamera = new()
    {
        Name = "game_view_add_camera",
        Content = "Add camera",
        FontSize = OrblitText.Caption,
        Padding = new Thickness(Space.Sm, Space.Xs),
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private readonly GameViewBar _bar;
    private readonly DeviceFrameHost _host = new() { Name = "game_view_frame" };
    private readonly UnsafeEdgesOverlay _edges = new();

    // The control of an interface game sits over the picture and beside it, so a click on the game's own
    // button is not a click on the world.
    private readonly Border _picture;
    private readonly Grid _interfaceHost = new() { Name = "game_view_interface" };
    private readonly GameInterface _interface;
    private readonly GameViewInput _input;

    private bool _dirty = true;
    private bool _wasPlaying;
    private bool _requested;
    private int _loop;
    private TimeSpan? _origin;

    /// <param name="session">What is open, and what is playing.</param>
    /// <param name="gameInterface">The control the playing game made, if it made one.</param>
    /// <param name="renderer">Whether to draw with the renderer. Off, a plain grid takes its place, which is what a test wants.</param>
    public GameViewPanel(EditorSession session, GameInterface gameInterface, bool renderer = true)
    {
        _session = session;
        _interface = gameInterface;
        _geometry = new GeometryStore(session.Workspace.ProjectDirectory);
        Name = "game_view";
        Margin = new Thickness(Space.Sm);
        Background = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF14181F));
        CornerRadius = Radii.Card;
        ClipToBounds = true;
        _addCamera.Click += (_, _) => _session.Add(ObjectKind.Camera);
        _renderer = renderer ? new RendererViewport() : null;
        _picture = new Border
        {
            Name = "game_view_picture",
            Background = Brushes.Transparent,
            Child = (Control?)_renderer ?? new ViewportPlaceholder(),
        };
        _input = new GameViewInput(new GameInputSource(_picture), () => Subject.Loaded?.Scene is { } scene ? CameraOf(scene) : null);
        _bar = new GameViewBar(session.Device);
        var screen = Screen();
        screen.KeyDown += TakeKey;
        _host.Child = screen;
        _host.Fitted += OnFitted;
        var body = new Grid { Children = { _host, Words() } };
        var column = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(body, 1);
        column.Children.Add(_bar);
        column.Children.Add(body);
        Child = column;
        SyncFrame();
    }

    /// <summary>The words over the picture, or null when the picture is showing. Read by tests.</summary>
    internal string? Saying => _note.IsVisible ? _note.Text : null;

    // What the frame holds: the picture, the game's own control over it, and the unsafe edges over both.
    private Grid Screen() => new()
    {
        Background = OrblitColors.Ground,
        ClipToBounds = true,
        Children = { _picture, _interfaceHost, _edges },
    };

    private StackPanel Words() => new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Spacing = Space.Sm,
        Children = { _note, _addCamera },
    };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.Device.Changed += SyncFrame;
        _session.PlaybackChanged += MarkDirty;
        _session.History.Changed += MarkDirty;
        _session.Workspace.Changed += MarkDirty;
        _session.Changed += MarkDirty;
        _session.Surfaces.Changed += MarkDirty;
        _session.PlaybackChanged += TakeFocusOnPlay;
        _interface.Changed += ShowInterface;
        _session.InputFeed = _input;
        ShowInterface();
        MarkDirty();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.Device.Changed -= SyncFrame;
        _session.PlaybackChanged -= MarkDirty;
        _session.History.Changed -= MarkDirty;
        _session.Workspace.Changed -= MarkDirty;
        _session.Changed -= MarkDirty;
        _session.Surfaces.Changed -= MarkDirty;
        _session.PlaybackChanged -= TakeFocusOnPlay;
        _interface.Changed -= ShowInterface;
        if (ReferenceEquals(_session.InputFeed, _input))
        {
            _session.InputFeed = null;
        }

        _interfaceHost.Children.Clear();
        // Opened again while the game plays, the view takes the keys on its first frame.
        _wasPlaying = false;
        // A frame already asked for belongs to the loop that ended here.
        _loop++;
        _requested = false;
        base.OnDetachedFromVisualTree(e);
    }

    // A playing scene is a copy with a workspace of its own, which is where its shared set lives.
    private Workspace Subject => _session.Playback?.Workspace ?? _session.Workspace;

    private RenderCamera? CameraOf(EditorScene scene) => _session.Playback?.Camera ?? SceneCameras.GameCamera(scene);

    // Pressing Play means the person wants to play, so the keys go to the picture. A click or a press made
    // before Play is not carried into the game.
    private void TakeFocusOnPlay()
    {
        if (_session.Playing && !_wasPlaying)
        {
            _input.EndFrame();
            _picture.Focus();
        }

        _wasPlaying = _session.Playing;
    }

    // While the scene plays, a key pressed in the view is the game's. The picture and the game's control
    // both bubble to here, after the game's own control has had its say. A key held with the command,
    // control or alt key is a command of the editor's, such as the palette, and Tab is how the keyboard
    // leaves the view, so neither is taken. The game still reads them from its own feed.
    private void TakeKey(object? sender, KeyEventArgs press)
    {
        if (_session.Playing && press.Key != Key.Tab && (press.KeyModifiers & EditorKeys) == KeyModifiers.None)
        {
            press.Handled = true;
        }
    }

    private void ShowInterface()
    {
        _interfaceHost.Children.Clear();
        if (_interface.Control is { } control)
        {
            _interfaceHost.Children.Add(control);
        }
    }

    // A new shape redraws the picture, because the renderer sizes itself to the frame.
    private void SyncFrame()
    {
        var frame = _session.Device.Frame;
        _host.Show(frame);
        _edges.Show(frame);
        _bar.Refresh();
        MarkDirty();
    }

    private void OnFitted()
    {
        _bar.Zoom(_host.Scale);
        MarkDirty();
    }

    private void MarkDirty()
    {
        _dirty = true;
        Show();
        RequestFrame();
    }

    private void Show()
    {
        var words = Subject.Loaded?.Scene is not { } scene ? NoScene : CameraOf(scene) is null ? NoCamera : null;
        _note.IsVisible = words is not null;
        _note.Text = words;
        // A paused scene still shows its copy, which a camera added to the scene would not reach.
        _addCamera.IsVisible = words == NoCamera && _session.Playback is null;
    }

    private void RequestFrame()
    {
        if (_requested || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        _requested = true;
        var loop = _loop;
        top.RequestAnimationFrame(now =>
        {
            if (loop == _loop)
            {
                OnFrame(now);
            }
        });
    }

    private void OnFrame(TimeSpan now)
    {
        _requested = false;
        _origin ??= now;
        if (!_dirty || _renderer is null || Subject.Loaded?.Scene is not { } scene || CameraOf(scene) is not { } camera)
        {
            _dirty = false;
            return;
        }

        // The renderer picks how it will show frames after it is attached. Until it has, ask again.
        if (_renderer.Path == ViewportPath.Pending)
        {
            RequestFrame();
            return;
        }

        _dirty = false;
        var options = SessionRenderOptions.Of(_session, Subject, _geometry);
        _renderer.Frame(scene.ToRenderScene(camera, options), (now - _origin.Value).TotalSeconds);
    }
}
