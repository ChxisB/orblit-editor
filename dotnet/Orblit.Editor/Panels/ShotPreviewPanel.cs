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
/// What the open cutscene looks through at the playhead: the scene as its shots' cameras see it, with
/// nothing of the editor drawn over it.
/// </summary>
/// <remarks>
/// shortcut: this draws the scene and not the game's interface or sound. The game view has a Play
/// button and draws the scene the same way, so it has the same two gaps.
/// </remarks>
internal sealed class ShotPreviewPanel : Border
{
    private const string NoCutscene = "No cutscene is open.";
    private const string NoShot = "No shot at this time.";

    private readonly EditorSession _session;
    private readonly GeometryStore _geometry;
    private readonly RendererViewport? _renderer;
    private readonly TextBlock _note = new()
    {
        Name = "shot_preview_note",
        FontSize = OrblitText.Label,
        Foreground = OrblitColors.InkDim,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private bool _dirty = true;
    private bool _requested;
    private int _loop;
    private TimeSpan? _origin;

    /// <param name="session">What is open.</param>
    /// <param name="renderer">Whether to draw with the renderer. Off, a plain grid takes its place, which is what a test wants.</param>
    public ShotPreviewPanel(EditorSession session, bool renderer = true)
    {
        _session = session;
        _geometry = new GeometryStore(session.Workspace.ProjectDirectory);
        Name = "shot_preview";
        Margin = new Thickness(Space.Sm);
        Background = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF14181F));
        CornerRadius = Radii.Card;
        ClipToBounds = true;
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

        stack.Children.Add(_note);
        Child = stack;
        Show();
    }

    /// <summary>The words over the picture, or null when the picture is showing. Read by tests.</summary>
    internal string? Saying => _note.IsVisible ? _note.Text : null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.Cutscenes.Changed += MarkDirty;
        _session.History.Changed += MarkDirty;
        _session.Workspace.Changed += MarkDirty;
        _session.Changed += MarkDirty;
        _session.Surfaces.Changed += MarkDirty;
        MarkDirty();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.Cutscenes.Changed -= MarkDirty;
        _session.History.Changed -= MarkDirty;
        _session.Workspace.Changed -= MarkDirty;
        _session.Changed -= MarkDirty;
        _session.Surfaces.Changed -= MarkDirty;
        // A frame already asked for belongs to the loop that ended here.
        _loop++;
        _requested = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void MarkDirty()
    {
        _dirty = true;
        Show();
        RequestFrame();
    }

    // The camera the cutscene looks through now, or null between its shots.
    private RenderCamera? ShotCamera() =>
        _session.Workspace.Loaded?.Scene is { } scene && _session.Cutscenes.Shown?.Cutscene is { } cutscene
            ? SceneCameras.CutsceneCamera(scene, cutscene, _session.Cutscenes.At)
            : null;

    private void Show()
    {
        var showing = ShotCamera() is not null;
        _note.IsVisible = !showing;
        _note.Text = _session.Cutscenes.Shown?.Shots is null ? NoCutscene : NoShot;
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
        if (!_dirty || _renderer is null || ShotCamera() is not { } camera || _session.Workspace.Loaded?.Scene is not { } scene)
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
        var options = SessionRenderOptions.Of(_session, _session.Workspace, _geometry);
        _renderer.Frame(scene.ToRenderScene(camera, options), (now - _origin.Value).TotalSeconds);
    }
}
