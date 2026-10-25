namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>
/// What a scene view keeps from one pointer event to the next, without a window: which handle is
/// lit, what a drag grabbed and where it stood, whether the camera is flying, and who gets the
/// pointer.
/// </summary>
/// <remarks>
/// Held apart from whatever draws it so a drag, a pick or a flight can be tested without a window, a
/// frame or a renderer. The camera, the selection and the snapping belong to the shell, so the view
/// reads them and reports back with <see cref="CameraChanged"/> and <see cref="Picked"/>.
/// </remarks>
public sealed class SceneViewportState
{
    private static readonly HashSet<ViewportKey> ForwardKeys = [ViewportKey.W, ViewportKey.Up];
    private static readonly HashSet<ViewportKey> BackKeys = [ViewportKey.S, ViewportKey.Down];
    private static readonly HashSet<ViewportKey> LeftKeys = [ViewportKey.A, ViewportKey.Left];
    private static readonly HashSet<ViewportKey> RightKeys = [ViewportKey.D, ViewportKey.Right];
    private static readonly HashSet<ViewportKey> UpKeys = [ViewportKey.E, ViewportKey.Space];
    private static readonly HashSet<ViewportKey> DownKeys = [ViewportKey.Q];

    // Every key flying answers to, so one held down is not also passed on to whatever else is
    // listening.
    private static readonly HashSet<ViewportKey> FlyKeys =
    [
        .. ForwardKeys,
        .. BackKeys,
        .. LeftKeys,
        .. RightKeys,
        .. UpKeys,
        .. DownKeys,
        ViewportKey.ShiftLeft,
        ViewportKey.ShiftRight,
    ];

    // How often the panels that are not the view hear the clock. A few times a second, not sixty:
    // the tree and the inspector show the hour and the name of whatever is in the sky, and those
    // change slowly enough to read.
    private static readonly TimeSpan TellInterval = TimeSpan.FromMilliseconds(250);

    private readonly Workspace _workspace;
    private readonly History? _history;
    private readonly Registry<GizmoType> _gizmos = new();
    private readonly ViewportInputOrder _order;
    private readonly HashSet<ViewportKey> _held = [];

    private GizmoMode _mode = GizmoMode.Move;
    private Grip? _grip;
    private Offset? _orbitFrom;
    private Offset? _looking;
    private bool _flyLocked;
    private bool _onTrackpad;
    private double _panZoomFrom = 1;
    private TimeSpan _elapsed;
    private TimeSpan _lastFlew;
    private TimeSpan _lastTold;

    public SceneViewportState(
        Workspace workspace,
        Snapping snapping,
        History? history = null,
        IReadOnlyList<GizmoType>? gizmos = null)
    {
        _workspace = workspace;
        _history = history;
        Snapping = snapping;
        _order = new ViewportInputOrder(Stages);

        // Registered here rather than by the shell, because what the handles draw and what a drag on
        // them does is state this view keeps: which handle is lit, what was grabbed, where it was
        // when the drag began.
        _gizmos.Register(new GizmoType(
            "transform",
            target => target.Item.Kind != ObjectKind.Scene,
            Input: (_, gesture) => TransformInput(gesture)));
        _gizmos.Register(new GizmoType("camera view", CameraViewApplies));
        foreach (var type in gizmos ?? [])
        {
            _gizmos.Register(type);
        }
    }

    /// <summary>Something here changed, so whatever draws the view should draw it again.</summary>
    public event Action? Changed;

    /// <summary>The camera a drag, a wheel turn or a flight would move to. The shell owns it.</summary>
    public event Action<OrbitCamera>? CameraChanged;

    /// <summary>
    /// Something in the scene was clicked, or nothing was. The flag is set when a modifier was held,
    /// which the shell reads as adding to or taking away from the selection rather than replacing it.
    /// </summary>
    public event Action<string?, bool>? Picked;

    /// <summary>A few times a second while a scene is animating, so the other panels can keep up.</summary>
    public event Action? Clocked;

    public OrbitCamera Camera { get; set; } = new();

    /// <summary>
    /// The size of the surface, as laid out. Needed to turn a pointer position into a ray, and only
    /// known once the view has been given a box.
    /// </summary>
    public Size? Surface { get; set; }

    /// <summary>What a drag lands on.</summary>
    public Snapping Snapping { get; set; }

    /// <summary>
    /// Whether the grid is switched off for this drag. Held down suspends it rather than switching it
    /// on, because somebody who wants a shelf half a millimetre off wants it for one drag and not for
    /// the afternoon.
    /// </summary>
    public bool SuspendSnapping { get; set; }

    /// <summary>The one of the selection the handles sit on, and whose transform a drag writes first.</summary>
    public string? Primary { get; set; }

    /// <summary>The objects to outline: all of them, so a multiple selection is visible.</summary>
    public IReadOnlySet<string> Selected { get; set; } = new HashSet<string>();

    /// <summary>
    /// What the mode being worked in does with the pointer. Asked before anything else in the view: a
    /// brush that could not take a drag before the handles do would move objects when somebody meant
    /// to sculpt.
    /// </summary>
    public Func<ViewportGesture, bool>? ModeInput { get; set; }

    /// <summary>How big an imported model says it is, for picking and for what a drag lands on.</summary>
    public Func<SceneObject, (Vector3 Min, Vector3 Max)?>? BoundsOf { get; set; }

    /// <summary>Whether a selected camera shows what it sees in a corner of the view.</summary>
    public bool PreviewsCameras { get; set; }

    public GizmoMode Mode => _mode;

    public GizmoAxis? Hovered { get; private set; }

    public GizmoAxis? Dragging => _grip?.Axis;

    /// <summary>Whether the camera is flying: a button held to look around, or switched on.</summary>
    public bool Flying => _looking is not null || _flyLocked;

    /// <summary>Metres a second, which the wheel changes while flying.</summary>
    public double FlySpeed { get; private set; } = 8;

    /// <summary>
    /// Whether the view needs a frame every tick: something in the scene moves on its own, or the
    /// camera moves a little every frame while a key is held. A clock that never stops is a view that
    /// redraws sixty times a second to show a scene that has not changed.
    /// </summary>
    public bool NeedsClock => Flying || (_workspace.Loaded?.Scene?.IsAnimated ?? false);

    /// <summary>The handles as they stand, or null when there is nothing to put them on.</summary>
    public Gizmo? Handles
    {
        get
        {
            if (EditingScene is not { } scene || Primary is not { } id || Projection() is not { } projection)
            {
                return null;
            }

            if (scene[id] is not { } item || item.Kind == ObjectKind.Scene)
            {
                return null;
            }

            return new Gizmo(_mode, scene.WorldOf(id).GetTranslation(), projection);
        }
    }

    /// <summary>What the gizmos are asked about, or null when there is nothing to put one on.</summary>
    public GizmoTarget? Target
    {
        get
        {
            if (EditingScene is not { } scene || Primary is not { } id || Projection() is not { } projection)
            {
                return null;
            }

            return scene[id] is { } item ? new GizmoTarget(item, scene, Selected, projection) : null;
        }
    }

    /// <summary>The camera whose shot is shown in the corner, if one is selected and has one.</summary>
    public SceneObject? PreviewCamera =>
        PreviewsCameras && Target is { } target && CameraViewApplies(target) ? target.Item : null;

    /// <summary>
    /// How far the drag has taken things, for saying so on screen: in squares as well as metres,
    /// because "two squares" is what somebody means when they are placing something on a grid.
    /// </summary>
    public (double Metres, double Squares)? Dragged
    {
        get
        {
            if (_grip is not { } grip || Primary is not { } id)
            {
                return null;
            }

            var metres = (grip.Scene.WorldOf(id).GetTranslation() - grip.Pivot).Dot(grip.Axis.Direction());
            return (metres, Snapping.Step <= 0 ? 0 : metres / Snapping.Step);
        }
    }

    // The scene the handles are working in: whichever holds what is selected, the open scene or the
    // shared set. A manager put in the shared set is dragged the same way everything else is.
    private EditorScene? EditingScene =>
        (Primary is { } id ? _workspace.SceneHolding(id) : _workspace.Loaded)?.Scene;

    private string? EditingId =>
        (Primary is { } id ? _workspace.SceneHolding(id) : _workspace.Loaded)?.Id;

    // The grid as it applies right now.
    private Snapping Snap =>
        SuspendSnapping ? new Snapping(false, Snapping.Step, Snapping.Angle, Snapping.To) : Snapping;

    public void SelectMode(GizmoMode mode)
    {
        _mode = mode;
        Changed?.Invoke();
    }

    /// <summary>
    /// What every gizmo that applies to the selection draws, in the order they were registered.
    /// </summary>
    public IReadOnlyList<(string Name, IReadOnlyList<GizmoStroke> Strokes)> Overlays()
    {
        var drawn = new List<(string, IReadOnlyList<GizmoStroke>)>();
        if (Target is not { } target)
        {
            return drawn;
        }

        foreach (var type in _gizmos.All)
        {
            if (type.Overlay is { } overlay && type.AppliesTo(target))
            {
                drawn.Add((type.Name, overlay(target)));
            }
        }

        return drawn;
    }

    /// <summary>Hands a pointer event to whoever takes it, first to last. Leaving and cancelling have their own methods.</summary>
    public void Touch(ViewportPhase phase, Offset at, bool add = false)
    {
        // A trackpad gesture also arrives as an ordinary drag, so without this both run and the
        // second undoes the first. The view would move once and then jump back every frame.
        if (_onTrackpad && phase is ViewportPhase.DragStart or ViewportPhase.DragUpdate)
        {
            return;
        }

        _order.Handle(new ViewportGesture(phase, at, add, Projection()));
    }

    public void Leave(Offset at) => _order.Leave(at);

    public void Cancel() => _order.Cancel();

    /// <summary>A key went down or up. Whether the view took it.</summary>
    public bool Key(ViewportKey key, bool down)
    {
        // The one key that works whether or not the view is already flying.
        if (down && key == ViewportKey.Backquote)
        {
            ToggleFlying();
            return true;
        }

        if (Flying && down && key == ViewportKey.Escape)
        {
            StopFlying();
            return true;
        }

        // Everything else only while flying. Otherwise W would fly the view every time somebody typed
        // a name into the inspector.
        if (!Flying)
        {
            return false;
        }

        _ = down ? _held.Add(key) : _held.Remove(key);
        return FlyKeys.Contains(key);
    }

    /// <summary>
    /// Switches flying on or off, for the trackpad. Holding a button is fine with a mouse and awkward
    /// on a trackpad: a two-finger click held down while the other hand types WASD is a hand position
    /// nobody keeps for long.
    /// </summary>
    public void ToggleFlying()
    {
        if (Flying)
        {
            StopFlying();
            return;
        }

        _flyLocked = true;
        _lastFlew = _elapsed;
        Changed?.Invoke();
    }

    /// <summary>The right button went down at <paramref name="at"/>.</summary>
    public void BeginLooking(Offset at)
    {
        _looking = at;
        _lastFlew = _elapsed;
        Changed?.Invoke();
    }

    /// <summary>The pointer moved while looking. <paramref name="held"/> is whether the right button still is.</summary>
    public void Look(Offset at, bool held)
    {
        if (_looking is not { } was)
        {
            return;
        }

        if (!held)
        {
            StopFlying();
            return;
        }

        CameraChanged?.Invoke(Camera.Looking(at - was));
        _looking = at;
    }

    /// <summary>A pointer went up or was cancelled, which ends flying however it began.</summary>
    public void EndLooking()
    {
        if (Flying)
        {
            StopFlying();
        }
    }

    /// <summary>
    /// The wheel turned. While flying it sets how fast and not how close: somebody going somewhere
    /// would be moved sideways by zooming the orbit distance under them.
    /// </summary>
    public void Wheel(double deltaY)
    {
        if (!Flying)
        {
            CameraChanged?.Invoke(Camera.Zoom(deltaY));
            return;
        }

        FlySpeed = Math.Clamp(FlySpeed * Math.Exp(-deltaY * 0.0025), 0.25, 400);
        Changed?.Invoke();
    }

    public void PanZoomStart()
    {
        _panZoomFrom = 1;
        _onTrackpad = true;
    }

    /// <summary>
    /// Fingers moved on a trackpad: <paramref name="scale"/> is the pinch since it began and
    /// <paramref name="pan"/> the movement since the last event.
    /// </summary>
    public void PanZoomUpdate(double scale, Offset pan, bool shifted)
    {
        // Pinching is zoom, whichever mode the view is in. It is the one gesture on a trackpad that
        // has never meant anything else.
        if (Math.Abs(scale - _panZoomFrom) > 0.001)
        {
            var step = scale / (_panZoomFrom == 0 ? 1 : _panZoomFrom);
            _panZoomFrom = scale;
            CameraChanged?.Invoke(Camera.Zoom(-Math.Log(step) * 620));
            return;
        }

        if (pan != Offset.Zero)
        {
            CameraChanged?.Invoke(Flying ? Camera.Looking(pan) : shifted ? Camera.Pan(pan) : Camera.Orbit(pan));
        }
    }

    public void PanZoomEnd()
    {
        _panZoomFrom = 1;
        _onTrackpad = false;
    }

    /// <summary>
    /// One frame of the clock: flies the camera for the keys held and moves whatever in the scene
    /// moves on its own.
    /// </summary>
    public void Tick(TimeSpan elapsed)
    {
        Fly(elapsed);
        _lastFlew = elapsed;

        if (_workspace.Loaded?.Scene is not { } scene)
        {
            return;
        }

        _elapsed = elapsed;
        scene.Clock = Microseconds(elapsed) / 1e6;
        Changed?.Invoke();

        if (elapsed - _lastTold >= TellInterval)
        {
            _lastTold = elapsed;
            Clocked?.Invoke();
        }
    }

    private static long Microseconds(TimeSpan span) => span.Ticks / TimeSpan.TicksPerMicrosecond;

    private ViewportProjection? Projection() =>
        Surface is { Width: > 0, Height: > 0 } size ? new ViewportProjection(Camera, size) : null;

    // A camera that is not in the scene has no shot to preview, and a preview each for a row of them
    // would cover the view they are meant to help with.
    private bool CameraViewApplies(GizmoTarget target) =>
        target.Item.Kind == ObjectKind.Camera
        && target.Selected.Count == 1
        && ReferenceEquals(target.Scene, _workspace.Loaded?.Scene)
        && target.Item.Visible
        && target.Scene.IsShown(target.Item.Id);

    // Who gets a gesture, first to last: the mode's tool, then the gizmos, then the selection, then
    // the camera. Each takes what it wants and says so, and the first to take something is the only
    // one that sees it.
    private IEnumerable<ViewportStage> Stages()
    {
        if (ModeInput is { } tool)
        {
            yield return new ViewportStage("tool", tool);
        }

        if (Target is { } target)
        {
            foreach (var type in _gizmos.All)
            {
                if (type.Input is { } input && type.AppliesTo(target))
                {
                    yield return new ViewportStage($"gizmo:{type.Name}", gesture => input(target, gesture));
                }
            }
        }

        yield return new ViewportStage("selection", SelectionInput);
        yield return new ViewportStage("orbit", OrbitInput);
    }

    // The move and turn handles. A drag that starts on one is a transform, and anywhere else is not.
    // Nothing to hold down and no mode to be in: the handles are the mode.
    private bool TransformInput(ViewportGesture gesture)
    {
        switch (gesture.Phase)
        {
            case ViewportPhase.Hover:
                return Hover(gesture.At);
            case ViewportPhase.Leave:
                return ClearHover();
            case ViewportPhase.DragStart:
                return Grab(gesture.At);
            case ViewportPhase.DragUpdate:
                DragTo(gesture.At);
                return true;
            case ViewportPhase.DragEnd or ViewportPhase.DragCancel:
                Release();
                return true;
            default:
                return false;
        }
    }

    private bool Hover(Offset at)
    {
        var axis = Handles?.AxisAt(at);
        if (axis != Hovered)
        {
            Hovered = axis;
            Changed?.Invoke();
        }

        return Hovered is not null;
    }

    private bool ClearHover()
    {
        if (Hovered is not null)
        {
            Hovered = null;
            Changed?.Invoke();
        }

        return false;
    }

    // Choosing what to work on. A drag that reaches here is not the selection's, so it goes on to
    // turn the view.
    private bool SelectionInput(ViewportGesture gesture)
    {
        if (gesture.Phase != ViewportPhase.Tap)
        {
            return false;
        }

        Pick(gesture.At, gesture.Add);
        return true;
    }

    // A drag that nothing else wanted turns the view.
    private bool OrbitInput(ViewportGesture gesture)
    {
        switch (gesture.Phase)
        {
            case ViewportPhase.DragStart:
                _orbitFrom = gesture.At;
                return true;
            case ViewportPhase.DragUpdate:
                Orbit(gesture.At);
                return true;
            case ViewportPhase.DragEnd or ViewportPhase.DragCancel:
                _orbitFrom = null;
                return true;
            default:
                return false;
        }
    }

    private void Orbit(Offset at)
    {
        if (_orbitFrom is { } from)
        {
            CameraChanged?.Invoke(Camera.Orbit(at - from));
            _orbitFrom = at;
        }
    }

    // Picks whatever is under the pointer, or nothing. The open scene first, then what every scene
    // has: a shared prop standing in front of a scene's own is the uncommon way round, and picking
    // the thing somebody is working on when both are under the pointer is the better answer.
    private void Pick(Offset at, bool add)
    {
        if (_workspace.Loaded?.Scene is not { } scene || Projection() is not { } projection)
        {
            return;
        }

        var ray = projection.RayThrough(at);
        var hit = scene.ObjectAlong(ray.Origin, ray.Direction, BoundsOf)
            ?? _workspace.Shared.ObjectAlong(ray.Origin, ray.Direction, BoundsOf);
        Picked?.Invoke(hit, add);
    }

    // Takes hold of a handle, remembering where everything was.
    private bool Grab(Offset at)
    {
        if (_history is null || HandlesInScene() is not { } work || work.Gizmo.AxisAt(at) is not { } axis)
        {
            return false;
        }

        var grabbed = _mode == GizmoMode.Move ? work.Gizmo.PointOnAxis(at, axis) : work.Gizmo.PointOnRing(at, axis);
        // The pointer is on the handle but the plane behind it is edge-on to the camera, so there is
        // no sensible place to have grabbed. Better to orbit than to teleport whatever is selected.
        if (grabbed is null)
        {
            return false;
        }

        var items = Targets(work.Scene);
        _grip = new Grip(
            work.Scene,
            work.SceneId,
            _mode,
            axis,
            grabbed,
            work.Gizmo.Pivot.Clone(),
            AnchorOn(work.Scene, axis, work.Gizmo.Pivot),
            items.ToDictionary(item => item.Id, item => _mode == GizmoMode.Move ? item.Position.Clone() : item.Rotation.Clone()),
            items.ToDictionary(item => item.Id, item => work.Scene.WorldOf(item.Id).GetRotation()));
        Changed?.Invoke();
        return true;
    }

    private Working? HandlesInScene() =>
        Handles is { } gizmo && EditingScene is { } scene && EditingId is { } id ? new Working(scene, id, gizmo) : null;

    // Everything a drag moves: the whole selection, or just the one the handles are on when the
    // selection is empty for some reason. Only what lives in the same scene as the one the handles
    // are on, because a drag is one command and a command changes one scene.
    private List<SceneObject> Targets(EditorScene scene)
    {
        IEnumerable<string> ids = Selected.Count > 0 ? Selected : Primary is { } primary ? [primary] : [];
        return [.. ids.Select(id => scene[id]).OfType<SceneObject>().Where(item => item.Kind != ObjectKind.Scene)];
    }

    // How far the snapping anchor is from the handle, for the object the handles are on. The primary
    // one only: a selection of several keeps its shape, so the thing that lands on a line is the one
    // being held and the others come along.
    private double AnchorOn(EditorScene scene, GizmoAxis axis, Vector3 pivot)
    {
        if (Primary is not { } id || scene[id] is not { } item)
        {
            return 0;
        }

        var (min, max) = item.LocalBounds(BoundsOf?.Invoke(item));
        var world = scene.WorldOf(id);
        var direction = axis.Direction();
        // Every corner, because a turned or scaled object's box in the world is not its box
        // multiplied through.
        var along = (
            from x in new[] { min.X, max.X }
            from y in new[] { min.Y, max.Y }
            from z in new[] { min.Z, max.Z }
            select world.Transformed3(new Vector3(x, y, z)).Dot(direction)).ToList();
        return Snap.AnchorFor(direction, pivot, (direction * along.Min(), direction * along.Max()));
    }

    // Applies the drag as it stands: one command, however many objects.
    private void DragTo(Offset at)
    {
        if (_grip is not { } grip || Handles is not { } gizmo || _history is null)
        {
            return;
        }

        var changes = grip.Mode == GizmoMode.Move ? Moves(grip, gizmo, at) : Turns(grip, gizmo, at);
        if (changes.Count == 0)
        {
            return;
        }

        var what = changes.Count == 1 ? grip.Scene[changes.Keys.Single()]?.Name ?? string.Empty : $"{changes.Count} objects";
        var field = grip.Mode == GizmoMode.Move ? TransformField.Position : TransformField.Rotation;
        _history.Run(new TransformMany(grip.SceneId, field, what, changes));
    }

    private Dictionary<string, TransformChange> Moves(Grip grip, Gizmo gizmo, Offset at)
    {
        var changes = new Dictionary<string, TransformChange>();
        if (gizmo.PointOnAxis(at, grip.Axis) is not { } now)
        {
            return changes;
        }

        // From where the handle was, so a selection of several keeps its shape and the one the
        // handles are on is the one that lands on a line.
        var shift = Snap.Along(grip.Pivot, now - grip.Grabbed + grip.Pivot, grip.Axis.Direction(), grip.Anchor);
        foreach (var (id, before) in grip.Before)
        {
            if (grip.Scene[id] is { } item)
            {
                changes[id] = new TransformChange(before, before + ShiftInParent(grip.Scene, item, shift));
            }
        }

        return changes;
    }

    private Dictionary<string, TransformChange> Turns(Grip grip, Gizmo gizmo, Offset at)
    {
        var changes = new Dictionary<string, TransformChange>();
        if (gizmo.PointOnRing(at, grip.Axis) is not { } now)
        {
            return changes;
        }

        var angle = Snap.Turn(gizmo.AngleBetween(grip.Grabbed, now, grip.Axis));
        var turn = Quaternion.AxisAngle(grip.Axis.Direction(), angle).AsRotationMatrix();
        foreach (var (id, before) in grip.Before)
        {
            if (grip.Scene[id] is { } item && grip.BeforeWorld.TryGetValue(id, out var was))
            {
                // Turned about the world axis, then read back into the parent's frame. Each object
                // turns where it stands rather than orbiting the handle, so a selection of several
                // keeps its shape.
                var turned = Copy(turn);
                turned.Multiply(was);
                var local = RotationInParent(grip.Scene, item, turned);
                changes[id] = new TransformChange(before, SceneMath.EulerDegreesOf(Carrying(local)));
            }
        }

        return changes;
    }

    // A world-space shift means something different inside a parent that is itself turned or scaled,
    // so it is taken into that parent's frame before it is added to a local position.
    private static Vector3 ShiftInParent(EditorScene scene, SceneObject item, Vector3 shift) =>
        item.ParentId is { } parent && scene.Contains(parent)
            ? Matrix4.Inverted(scene.WorldOf(parent)).Rotated3(shift)
            : shift;

    private static Matrix3 RotationInParent(EditorScene scene, SceneObject item, Matrix3 turned)
    {
        if (item.ParentId is not { } parent || !scene.Contains(parent))
        {
            return turned;
        }

        var inverse = new Matrix3();
        inverse.CopyInverse(scene.WorldOf(parent).GetRotation());
        inverse.Multiply(turned);
        return inverse;
    }

    private static Matrix3 Copy(Matrix3 matrix)
    {
        var copy = new Matrix3();
        for (var i = 0; i < 9; i++)
        {
            copy[i] = matrix[i];
        }

        return copy;
    }

    // A transform with this rotation and nothing else, to read the angles back out of.
    private static Matrix4 Carrying(Matrix3 rotation)
    {
        var carrier = Matrix4.Identity();
        for (var column = 0; column < 3; column++)
        {
            for (var row = 0; row < 3; row++)
            {
                carrier[(column * 4) + row] = rotation.Entry(row, column);
            }
        }

        return carrier;
    }

    private void Release()
    {
        if (_grip is null)
        {
            return;
        }

        // Sealed here rather than on a timer, so the whole gesture is one step however long somebody
        // took over it.
        _history?.Seal();
        _grip = null;
        Changed?.Invoke();
    }

    private void StopFlying()
    {
        _looking = null;
        _flyLocked = false;
        _held.Clear();
        Changed?.Invoke();
    }

    // Moves the camera for one frame of held keys.
    private void Fly(TimeSpan elapsed)
    {
        if (!Flying)
        {
            return;
        }

        var along = new Vector3(Along(RightKeys, LeftKeys), Along(UpKeys, DownKeys), Along(ForwardKeys, BackKeys));
        if (along.Length2 == 0)
        {
            return;
        }

        var seconds = Microseconds(elapsed - _lastFlew) / 1e6;
        _lastFlew = elapsed;
        // Clamped: a frame that took a second, a rebuild or a breakpoint, should not throw the camera
        // across the level.
        var step = Math.Clamp(seconds, 0, 0.05);
        var fast = _held.Contains(ViewportKey.ShiftLeft) || _held.Contains(ViewportKey.ShiftRight);
        // Normalised, or holding two keys would go a metre and a half diagonally for every metre going
        // straight.
        CameraChanged?.Invoke(Camera.Flying(along.Normalized() * (FlySpeed * (fast ? 4 : 1) * step)));
    }

    // Which way a pair of opposite keys add up to.
    private double Along(HashSet<ViewportKey> plus, HashSet<ViewportKey> minus) =>
        (_held.Overlaps(plus) ? 1 : 0) - (_held.Overlaps(minus) ? 1 : 0);

    private sealed record Working(EditorScene Scene, string SceneId, Gizmo Gizmo);

    // F2: a grip is the scene, the handle, where it was taken and what everything looked like before.
    private sealed record Grip(
        EditorScene Scene,
        string SceneId,
        GizmoMode Mode,
        GizmoAxis Axis,
        Vector3 Grabbed,
        Vector3 Pivot,
        double Anchor,
        IReadOnlyDictionary<string, Vector3> Before,
        IReadOnlyDictionary<string, Matrix3> BeforeWorld);
}
