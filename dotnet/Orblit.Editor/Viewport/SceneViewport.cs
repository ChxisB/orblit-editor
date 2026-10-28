using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
using Orblit.Profile;
using Orblit.VectorMath;
using ViewOffset = Orblit.Editor.Viewport.Offset;
using ViewSize = Orblit.Editor.Viewport.Size;

/// <summary>
/// The scene as the editor shows it: the frame from the renderer, what is drawn over it, and the
/// pointer and keys that move the camera and the selection.
/// </summary>
/// <remarks>
/// What a gesture means is decided by <see cref="SceneViewportState"/>, which knows nothing of
/// Avalonia. This control turns the pointer into phases, draws what the state says to draw, and asks
/// the renderer for a frame when something changed. A control built without a renderer keeps a plain
/// grid in its place, which is what a test sees.
///
/// shortcut: a two finger scroll on a trackpad arrives as a wheel and only zooms. Orbit and pan by
/// trackpad need the scroll phases, which Avalonia has no event for yet.
///
/// shortcut: every frame builds the whole render scene, even when only the camera moved. Cache what
/// comes from the scene when a large one drops frames during an orbit.
/// </remarks>
public sealed class SceneViewport : Border
{
    // The pointer moves this far before a press becomes a drag, so a click that wobbles is still a click.
    private const double DragDistance = 4;

    private const string FlyTip = "Hold the right button, or press the backtick, then WASD to move, Q and E for down and up.";

    private const string SnapTip =
        "Where a drag lands, and which part of the thing is put on the line. Hold control or option to "
        + "suspend it for one drag; the brackets make it coarser and finer, and the arrow keys move by "
        + "whole squares.";

    private const string InterfaceTip =
        "Draws the scene's interface over the viewport. Turning it off here is for getting at what is "
        + "behind it. Hiding the canvas object is what hides it in the game.";

    private static readonly IReadOnlyList<GizmoType> Gizmos = [BodyGizmo.Type(), JointGizmo.Type(), SoundGizmo.Type()];

    private readonly EditorSession _session;
    private readonly SceneViewportState _state;
    private readonly TerrainBrushInput _brush;
    private readonly GridStore _grids;
    private readonly GeometryStore _geometry;
    private readonly Grid _stack = new() { Background = Brushes.Transparent, Focusable = true, ClipToBounds = true };
    private readonly ViewportOverlay _overlay;
    private readonly ViewportInterface _interface = new();
    private readonly RendererViewport? _renderer;
    private readonly ToolPalette _palette;
    private readonly WrapPanel _chips = new() { Orientation = Orientation.Horizontal };
    private readonly FrameRate _frames = new();
    private Point? _downAt;
    private bool _dragging;
    private bool _dirty = true;
    private bool _requested;
    private int _loop;
    private bool _outlineSelection = true;
    private bool _showInterface = true;
    private bool _pinching;
    private string _chipKey = "";
    private string? _looked;
    private TimeSpan? _origin;

    public SceneViewport(
        EditorSession session,
        bool renderer = true,
        Func<SceneObject, (Vector3 Min, Vector3 Max)?>? boundsOf = null,
        Snapping? snapping = null)
    {
        _session = session;
        var workspace = session.Workspace;
        _grids = new GridStore(workspace.ProjectDirectory);
        _geometry = new GeometryStore(workspace.ProjectDirectory);
        _state = new SceneViewportState(workspace, snapping ?? new Snapping(), session.History, Gizmos) { BoundsOf = boundsOf, Sketch = session.Sketch };
        _brush = new TerrainBrushInput(session.Terrains, session.History, session.TerrainTarget);
        _overlay = new ViewportOverlay(_state, session);

        Margin = new Thickness(Space.Sm);
        Background = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF14181F));
        BorderBrush = OrblitColors.LineSoft;
        BorderThickness = new Thickness(1);
        CornerRadius = Radii.Card;
        ClipToBounds = true;
        Child = _stack;

        // The tools turn the handles, which only a drawn scene has.
        _palette = new ToolPalette(_state) { IsVisible = renderer };
        if (renderer)
        {
            _renderer = new RendererViewport();
            _stack.Children.Add(_renderer);
        }
        else
        {
            _stack.Children.Add(new ViewportPlaceholder());
        }

        // Over the scene and under the handles, as a game draws an interface over its world.
        _stack.Children.Add(_interface);
        _stack.Children.Add(_overlay);
        _stack.Children.Add(TopRow());
        _stack.Children.Add(HintChip());
        AttachInput();
    }

    /// <summary>The state the control drives. A test reads what the camera did from here.</summary>
    public SceneViewportState State => _state;

    /// <summary>Told of each frame the view draws.</summary>
    public Profiler Profiler { get; init; } = Profiler.Off;

    /// <summary>The renderer, or null when the control was built without one.</summary>
    public RendererViewport? Renderer => _renderer;

    internal ToolPalette Palette => _palette;

    protected override Avalonia.Size ArrangeOverride(Avalonia.Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        var inside = _stack.Bounds.Size;
        if (inside.Width > 0 && inside.Height > 0)
        {
            var size = new ViewSize(inside.Width, inside.Height);
            if (_state.Surface != size)
            {
                _state.Surface = size;
                MarkDirty();
            }
        }

        return arranged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _state.CameraChanged += OnCameraChanged;
        _state.Picked += OnPicked;
        _state.ElementPicked += _session.PickElements;
        _state.ElementsBoxed += _session.BoxElements;
        _state.ElementsDragged += OnElementsDragged;
        _state.DrawPointed += _session.DrawPoint;
        _state.DrawFinished += _session.FinishDrawing;
        _state.Changed += MarkDirty;
        _state.Clocked += MarkDirty;
        _state.Snapping.Changed += MarkDirty;
        _session.Changed += OnSessionChanged;
        _session.SceneReplaced += OnSceneReplaced;
        _session.History.Changed += MarkDirty;
        _session.Workspace.Changed += MarkDirty;
        _session.Terrains.HoverChanged += MarkDirty;
        _session.Terrains.Changed += MarkDirty;
        _session.Interfaces.Changed += ShowInterface;
        OnSessionChanged();
        if (_renderer is not null)
        {
            _ = PrepareGrid();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _state.CameraChanged -= OnCameraChanged;
        _state.Picked -= OnPicked;
        _state.ElementPicked -= _session.PickElements;
        _state.ElementsBoxed -= _session.BoxElements;
        _state.ElementsDragged -= OnElementsDragged;
        _state.DrawPointed -= _session.DrawPoint;
        _state.DrawFinished -= _session.FinishDrawing;
        _state.Changed -= MarkDirty;
        _state.Clocked -= MarkDirty;
        _state.Snapping.Changed -= MarkDirty;
        _session.Changed -= OnSessionChanged;
        _session.SceneReplaced -= OnSceneReplaced;
        _session.History.Changed -= MarkDirty;
        _session.Workspace.Changed -= MarkDirty;
        _session.Terrains.HoverChanged -= MarkDirty;
        _session.Terrains.Changed -= MarkDirty;
        _session.Interfaces.Changed -= ShowInterface;
        // A frame already asked for belongs to the loop that ended here. Moving to a new group attaches
        // again at once, and the old callback would run beside the new one.
        _loop++;
        _requested = false;
        Abandon();
        if (_pinching)
        {
            _pinching = false;
            _state.PanZoomEnd();
        }

        base.OnDetachedFromVisualTree(e);
    }

    private static bool Held(KeyModifiers keys, KeyModifiers wanted) => (keys & wanted) != 0;

    private static ViewportKey? KeyOf(Key key) => key switch
    {
        Key.OemTilde => ViewportKey.Backquote,
        Key.Escape => ViewportKey.Escape,
        Key.W => ViewportKey.W,
        Key.A => ViewportKey.A,
        Key.S => ViewportKey.S,
        Key.D => ViewportKey.D,
        Key.Q => ViewportKey.Q,
        Key.E => ViewportKey.E,
        Key.Space => ViewportKey.Space,
        Key.Up => ViewportKey.Up,
        Key.Down => ViewportKey.Down,
        Key.Left => ViewportKey.Left,
        Key.Right => ViewportKey.Right,
        Key.LeftShift => ViewportKey.ShiftLeft,
        Key.RightShift => ViewportKey.ShiftRight,
        _ => null,
    };

    private static ViewOffset OffsetOf(Point point) => new(point.X, point.Y);

    private static Border HintChip()
    {
        var hint = ViewportChips.Build(new ChipSpec(
            "Drag to orbit · scroll or pinch to zoom · ` to fly, then WASD"));
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        hint.VerticalAlignment = VerticalAlignment.Bottom;
        hint.Margin = new Thickness(Space.Md);
        hint.IsHitTestVisible = false;
        ((TextBlock)hint.Child!).TextWrapping = TextWrapping.Wrap;
        hint.MaxWidth = 360;
        return hint;
    }

    private void AttachInput()
    {
        _stack.PointerPressed += OnPressed;
        _stack.PointerMoved += OnMoved;
        _stack.PointerReleased += OnReleased;
        _stack.PointerExited += (_, e) => _state.Leave(OffsetOf(e.GetPosition(_stack)));
        _stack.PointerCaptureLost += OnCaptureLost;
        _stack.PointerWheelChanged += OnWheel;
        _stack.AddHandler(InputElement.PinchEvent, OnPinch);
        _stack.AddHandler(InputElement.PinchEndedEvent, OnPinchEnded);
        _stack.KeyDown += (_, e) => OnKey(e, true);
        _stack.KeyUp += (_, e) => OnKey(e, false);
        _stack.LostFocus += (_, _) => _state.EndLooking();
    }

    // The tools down the left and the chips beside them, kept clear of the orientation gizmo. The row
    // has no background, so a press on the space between its parts reaches the view.
    private Grid TopRow()
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(Space.Md, Space.Md, Space.Md + ViewportOverlay.OrientationSize + Space.Sm, 0),
        };
        _chips[Grid.ColumnProperty] = 1;
        row.Children.Add(_palette);
        row.Children.Add(_chips);
        return row;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        _stack.Focus();
        var point = e.GetCurrentPoint(_stack);
        var at = OffsetOf(point.Position);
        if (point.Properties.IsRightButtonPressed)
        {
            _state.BeginLooking(at);
        }
        else if (point.Properties.IsLeftButtonPressed)
        {
            _downAt = point.Position;
        }
        else
        {
            return;
        }

        e.Pointer.Capture(_stack);
        e.Handled = true;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(_stack);
        var at = OffsetOf(point.Position);
        var add = Held(e.KeyModifiers, KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Meta);
        if (point.Properties.IsRightButtonPressed)
        {
            _state.Look(at, true);
        }
        else if (_downAt is { } from)
        {
            // Read on every move, so a key pressed part way through a drag suspends the grid as well.
            _state.SuspendSnapping = Held(e.KeyModifiers, KeyModifiers.Control | KeyModifiers.Alt);
            _state.ShiftHeld = Held(e.KeyModifiers, KeyModifiers.Shift);
            Drag(from, at, add);
        }
        else
        {
            _state.Touch(ViewportPhase.Hover, at, add);
        }
    }

    private void Drag(Point from, ViewOffset at, bool add)
    {
        if (!_dragging)
        {
            if ((at - OffsetOf(from)).Distance < DragDistance)
            {
                return;
            }

            _dragging = true;
            _state.Touch(ViewportPhase.DragStart, OffsetOf(from), add);
        }

        _state.Touch(ViewportPhase.DragUpdate, at, add);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        var at = OffsetOf(e.GetPosition(_stack));
        var add = Held(e.KeyModifiers, KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Meta);
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            _state.EndLooking();
        }
        else if (e.InitialPressMouseButton == MouseButton.Left)
        {
            _state.Touch(_dragging ? ViewportPhase.DragEnd : ViewportPhase.Tap, at, add);
            _downAt = null;
            _dragging = false;
            _state.SuspendSnapping = false;
        }

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Abandon();

    // A gesture that ends without finishing: the pointer was taken away or the view left the window.
    // A release of any button ends a flight, whichever way it began, as the Dart view does.
    private void Abandon()
    {
        if (_dragging)
        {
            _state.Cancel();
        }

        _downAt = null;
        _dragging = false;
        _state.SuspendSnapping = false;
        _state.EndLooking();
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        _state.Wheel(-e.Delta.Y * 100);
        e.Handled = true;
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (!_pinching)
        {
            _pinching = true;
            _state.PanZoomStart();
        }

        _state.PanZoomUpdate(e.Scale, ViewOffset.Zero, false);
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
    {
        _pinching = false;
        _state.PanZoomEnd();
        e.Handled = true;
    }

    private void OnKey(KeyEventArgs e, bool down)
    {
        // Only a key going down is left to a shortcut. A key coming up always reaches the state, or a
        // letter let go of under a command key would stay held and keep the camera flying.
        if (down && Held(e.KeyModifiers, KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt))
        {
            return;
        }

        if (KeyOf(e.Key) is { } key && _state.Key(key, down))
        {
            e.Handled = true;
        }
    }

    private void OnCameraChanged(OrbitCamera camera)
    {
        _state.Camera = camera;
        if (_session.Piloting is { View: EditorSession.SceneView } look && _session.Workspace.Loaded?.Scene is { } scene)
        {
            _session.Steer(EditorSession.SceneView, CameraViews.PlaceFor(camera, scene, scene[look.Camera]?.ParentId));
        }

        MarkDirty();
    }

    // Looking through a camera puts the view where the camera is. After that, moving the view steers
    // the camera, which OnCameraChanged does. Going there is not a move, so it steers nothing.
    private void FlyToPiloted()
    {
        var camera = _session.Piloting is { View: EditorSession.SceneView } look ? look.Camera : null;
        if (camera == _looked)
        {
            return;
        }

        _looked = camera;
        if (camera is not null && _session.Workspace.Loaded?.Scene is { } scene && scene[camera] is { } found)
        {
            _state.Camera = CameraViews.ViewThrough(scene, found);
        }
    }

    // A camera left where another scene's work was would show this one from the wrong place.
    private void OnSceneReplaced() => OnCameraChanged(new OrbitCamera());

    // A tap on empty space clears the selection, a plain tap on a thing selects it, and a tap with a
    // modifier held adds the thing or takes it away.
    private void OnPicked(string? id, bool add)
    {
        if (id is null)
        {
            if (!add)
            {
                _session.Select([]);
            }

            return;
        }

        var chosen = _session.Selected;
        if (!add)
        {
            _session.Select([id]);
        }
        else if (chosen.Contains(id))
        {
            _session.Select([.. chosen.Where(held => held != id)]);
        }
        else
        {
            _session.Select([.. chosen, id]);
        }
    }

    private void OnElementsDragged(ElementDrag drag) =>
        _session.DragElements(drag.Mesh, drag.Selection, drag.What, drag.Merge);

    private void OnSessionChanged()
    {
        FlyToPiloted();
        _state.Selected = new HashSet<string>(_session.Selected);
        _state.Primary = _session.Primary;
        _state.Context = _session.Context;
        _state.ElementMode = _session.ElementMode;
        _state.ElementSelection = _session.Elements;
        _state.SeeThroughElements = _session.SeeThrough;
        _state.ModeInput = _session.Mode.UsesBrush ? _brush.Handle : null;
        if (!_session.Mode.UsesBrush)
        {
            // The brush gets no leave gesture once it is switched off, so the ring would wait at the last point.
            _session.Terrains.Hover = null;
        }

        MarkDirty();
    }

    private async Task PrepareGrid()
    {
        try
        {
            await _grids.PrepareAsync();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The grid is decoration, so a folder that cannot be written costs the view its lines and nothing else.
            return;
        }

        MarkDirty();
    }

    private void MarkDirty()
    {
        _dirty = true;
        RequestFrame();
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
        var elapsed = now - _origin.Value;
        if (_state.NeedsClock)
        {
            _state.Tick(elapsed);
        }

        if (_dirty)
        {
            _dirty = false;
            // Only a drawn frame counts. A still view adds none, so the gap before its next one is rest.
            Profiler.Frame();
            var started = Stopwatch.GetTimestamp();
            Redraw(elapsed);
            _frames.Took(now, Stopwatch.GetElapsedTime(started), GpuTime());
        }

        if (_state.NeedsClock)
        {
            RequestFrame();
        }
    }

    private void Redraw(TimeSpan elapsed)
    {
        RefreshChips();
        ShowInterface();
        _palette.Sync();
        _overlay.InvalidateVisual();
        if (_renderer is null)
        {
            return;
        }

        // The renderer picks how it will show frames after it is attached. Until it has, ask again.
        // shortcut: this asks every frame, so a renderer that never chooses keeps the loop running.
        // Wait on an event from the renderer once it raises one.
        if (_renderer.Path == ViewportPath.Pending)
        {
            MarkDirty();
            return;
        }

        _renderer.Frame(Build(), elapsed.TotalSeconds);
    }

    // The renderer's median over its recent frames. Nought until the backend has reported, and with no renderer.
    private TimeSpan GpuTime() =>
        _renderer?.Renderer is { } renderer ? TimeSpan.FromMilliseconds(renderer.GetStats().GpuMilliseconds) : TimeSpan.Zero;

    private RenderScene Build()
    {
        var workspace = _session.Workspace;
        var scene = workspace.Loaded?.Scene ?? new EditorScene([]);
        var built = scene.ToRenderScene(
            _state.Camera.ToRenderCamera(),
            new SceneRenderOptions
            {
                ProjectRoot = workspace.ProjectDirectory,
                Shared = workspace.Shared,
                GeometryOf = _geometry.PathFor,
                Grid = _grids.PlanFor(_state.Snapping, _state.Camera.Target),
            });
        return built with
        {
            Outline = _outlineSelection
                ? SelectionOutline.Of(scene, _state.Selected, workspace.Shared, _state.Primary)
                : Outline.None,
        };
    }

    private void RefreshChips()
    {
        var specs = Chips();
        var key = string.Join('|', specs.Select(spec => $"{spec.Text}:{spec.On}"));
        if (key == _chipKey)
        {
            return;
        }

        _chipKey = key;
        _chips.Children.Clear();
        foreach (var spec in specs)
        {
            _chips.Children.Add(ViewportChips.Build(spec));
        }
    }

    private List<ChipSpec> Chips() =>
    [
        new ChipSpec("Perspective"),
        new ChipSpec("Shaded"),
        .. new[] { StatsChip(), DraggedChip(), SnapChip(), FlyChip(), OutlineChip(), InterfaceChip() }.OfType<ChipSpec>(),
    ];

    // A view setting, so the scene is not changed and nothing is left to save.
    private ChipSpec? InterfaceChip() => _session.SceneInterface() is null
        ? null
        : new ChipSpec("Interface", _showInterface, InterfaceTip, () =>
        {
            _showInterface = !_showInterface;
            MarkDirty();
        });

    // An interface being laid out changes on the bench and not in the scene, so the bench is followed
    // as well. The surface skips a value it already has, so a hover there costs nothing here.
    private void ShowInterface() => _interface.Show(_showInterface ? _session.SceneInterface() : null, _session.Interfaces.Strings);

    private ChipSpec? StatsChip()
    {
        if (!_session.ShowStats)
        {
            return null;
        }

        var scene = _session.Workspace.Loaded?.Scene;
        var summary = scene is null ? "No scene" : $"{scene.Objects.Count(item => item.IsDrawable)} drawn";
        return new ChipSpec($"{ViewportChips.Frames(_frames.Reading)} · {summary}");
    }

    private ChipSpec? DraggedChip()
    {
        if (_state.Dragged is not { } dragged)
        {
            return null;
        }

        var metres = $"{ViewportChips.Signed(dragged.Metres, 2)} m";
        var text = _state.Snapping.On
            ? $"{ViewportChips.Signed(dragged.Squares, 0)} square{(Math.Abs(dragged.Squares) == 1 ? "" : "s")} · {metres}"
            : metres;
        return new ChipSpec(text, On: true);
    }

    private ChipSpec SnapChip() => new(
        _state.Snapping.On ? $"Snap: {ViewportChips.GridLabel(_state.Snapping.Step)}" : "Snap off",
        _state.Snapping.On,
        SnapTip,
        _state.ToggleSnapping);

    private ChipSpec? FlyChip() => _state.Flying
        ? new ChipSpec($"Flying · {_state.FlySpeed:0.0} m/s", true, FlyTip, _state.ToggleFlying)
        : null;

    private ChipSpec? OutlineChip() => _state.Selected.Count == 0
        ? null
        : new ChipSpec(
            _outlineSelection ? "Outline" : "Boundary",
            _outlineSelection,
            "Outline the selection in the frame, or draw its boundary over it.",
            () =>
            {
                _outlineSelection = !_outlineSelection;
                _overlay.OutlineSelection = _outlineSelection;
                MarkDirty();
            });
}
