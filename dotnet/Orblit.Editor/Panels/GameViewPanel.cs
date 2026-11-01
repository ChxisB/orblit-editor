using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using Orblit.Filament.Avalonia;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Filament;

/// <summary>
/// The scene as the game shows it: through the scene's first camera, with nothing of the editor drawn
/// over it. While the scene plays it shows the playing copy, and at rest it shows the scene as edited.
/// </summary>
/// <remarks>
/// shortcut: this draws the scene and not the game's interface or sound. Terrain is not drawn in a C#
/// viewport yet, so it is missing here as it is in the scene view.
/// </remarks>
internal sealed class GameViewPanel : Border
{
    private const string NoScene = "No scene is open.";
    private const string NoCamera = "This scene has no camera, so the game has nothing to look through.";

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

    private bool _dirty = true;
    private bool _requested;
    private int _loop;
    private TimeSpan? _origin;

    /// <param name="session">What is open, and what is playing.</param>
    /// <param name="renderer">Whether to draw with the renderer. Off, a plain grid takes its place, which is what a test wants.</param>
    public GameViewPanel(EditorSession session, bool renderer = true)
    {
        _session = session;
        _geometry = new GeometryStore(session.Workspace.ProjectDirectory);
        Name = "game_view";
        Margin = new Thickness(Space.Sm);
        Background = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF14181F));
        CornerRadius = Radii.Card;
        ClipToBounds = true;
        _addCamera.Click += (_, _) => _session.Add(ObjectKind.Camera);
        var stack = new Grid();
        if (renderer)
        {
            _renderer = new RendererViewport();
            stack.Children.Add(_renderer);
        }
        else
        {
            stack.Children.Add(new ViewportPlaceholder());
        }

        stack.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = Space.Sm,
            Children = { _note, _addCamera },
        });
        Child = stack;
        Show();
    }

    /// <summary>The words over the picture, or null when the picture is showing. Read by tests.</summary>
    internal string? Saying => _note.IsVisible ? _note.Text : null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.PlaybackChanged += MarkDirty;
        _session.History.Changed += MarkDirty;
        _session.Workspace.Changed += MarkDirty;
        _session.Changed += MarkDirty;
        _session.Surfaces.Changed += MarkDirty;
        MarkDirty();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.PlaybackChanged -= MarkDirty;
        _session.History.Changed -= MarkDirty;
        _session.Workspace.Changed -= MarkDirty;
        _session.Changed -= MarkDirty;
        _session.Surfaces.Changed -= MarkDirty;
        // A frame already asked for belongs to the loop that ended here.
        _loop++;
        _requested = false;
        base.OnDetachedFromVisualTree(e);
    }

    // A playing scene is a copy with a workspace of its own, which is where its shared set lives.
    private Workspace Subject => _session.Playback?.Workspace ?? _session.Workspace;

    private RenderCamera? CameraOf(EditorScene scene) => _session.Playback?.Camera ?? SceneCameras.GameCamera(scene);

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
