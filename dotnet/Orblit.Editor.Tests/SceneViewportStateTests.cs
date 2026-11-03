namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;

// What a scene view keeps between events: flying, the wheel, a trackpad, a turn and the numbers a drag
// reports.
public sealed class SceneViewportStateTests
{
    private static readonly Size Surface = new(800, 600);
    private readonly List<OrbitCamera> _cameras = [];
    private readonly List<OrbitCamera> _eased = [];
    private readonly SceneObject _cube = new(
        "a", "A", ObjectKind.Shape, position: new Vector3(0, 0.5, 0), shape: Shape.Of(ShapeKind.Cube));

    private History? _history;
    private Workspace? _workspace;

    private SceneViewportState View(GizmoMode mode = GizmoMode.Move)
    {
        _workspace = new Workspace("/project");
        _workspace.Add(new SceneEntry("s", "S", scene: new EditorScene([_cube])));
        _history = new History(_workspace);
        var view = new SceneViewportState(_workspace, new Snapping(on: false, step: 0.5), _history)
        {
            Camera = new OrbitCamera(yaw: 0.6, pitch: 0.4, distance: 8),
            Surface = Surface,
            Primary = "a",
            Selected = new HashSet<string> { "a" },
        };
        view.SelectMode(mode);
        view.CameraChanged += _cameras.Add;
        view.CameraEased += _eased.Add;
        return view;
    }

    private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

    private double Moved(SceneViewportState view) => (_cameras[^1].Target - view.Camera.Target).Length;

    // A view wired as the shell wires it: a cut is written at once and an ease only moves the goal.
    private SceneViewportState ShellView()
    {
        var view = View();
        view.CameraChanged += camera => view.Camera = camera;
        view.CameraEased += view.EaseTo;
        return view;
    }

    private static void TickFor(SceneViewportState view, int frames, int fromMs = 0)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            view.Tick(Ms(fromMs + (frame * 16)));
        }
    }

    [Fact]
    public void AWheelNotchDoesNotMoveTheShownCameraAtOnce()
    {
        var view = ShellView();
        var shown = view.Camera;

        view.Wheel(-100);

        Assert.Same(shown, view.Camera);
        Assert.True(view.Goal.Distance < shown.Distance);
        Assert.True(view.NeedsClock);
        Assert.Empty(_cameras);
    }

    [Fact]
    public void AWheelNotchChangesTheDistanceByAboutEightPercent()
    {
        var view = ShellView();
        var start = view.Camera.Distance;

        view.Wheel(-100);

        Assert.Equal(start / 1.08, view.Goal.Distance, 1e-9);

        view.Wheel(200);

        Assert.Equal(start / 1.08 * 1.08 * 1.08, view.Goal.Distance, 1e-9);
    }

    [Fact]
    public void TwoNotchesBeforeATickAddUpInTheGoal()
    {
        var view = ShellView();
        var start = view.Camera.Distance;

        view.Wheel(-100);
        var first = view.Goal.Distance;
        view.Wheel(-100);

        Assert.Equal(first / start, view.Goal.Distance / first, 1e-9);
    }

    [Fact]
    public void ACameraLeftAtRestDoesNotLeapOnItsFirstTick()
    {
        var view = ShellView();
        view.Wheel(-100);
        var shown = view.Camera.Distance;

        // The clock has been stopped for a while, so the time since the last frame says nothing.
        view.Tick(Ms(5000));
        Assert.Equal(shown, view.Camera.Distance);

        view.Tick(Ms(5016));
        var step = shown - view.Camera.Distance;
        Assert.InRange(step, 1e-6, 0.4 * (shown - view.Goal.Distance));
    }

    [Fact]
    public void TicksBringTheShownCameraToTheGoalAndTheClockStops()
    {
        var view = ShellView();
        view.Wheel(-100);
        var last = view.Camera.Distance;

        for (var frame = 0; frame < 40; frame++)
        {
            view.Tick(Ms(frame * 16));
            Assert.True(view.Camera.Distance <= last);
            last = view.Camera.Distance;
        }

        Assert.Equal(view.Goal.Distance, view.Camera.Distance);
        Assert.False(view.NeedsClock);
    }

    [Fact]
    public void OrbitMovesTheShownCameraOnTheSameEvent()
    {
        var view = ShellView();
        var from = new Offset(700, 80);
        var yaw = view.Camera.Yaw;

        view.Touch(ViewportPhase.DragStart, from);
        view.Touch(ViewportPhase.DragUpdate, from + new Offset(20, 0));

        Assert.NotEqual(yaw, view.Camera.Yaw);
        Assert.Equal(view.Camera.Yaw, view.Goal.Yaw);
        Assert.False(view.NeedsClock);
    }

    [Fact]
    public void AnOrbitDuringAnEaseKeepsWhereTheEaseHadGot()
    {
        var view = ShellView();
        view.Wheel(-100);
        TickFor(view, 4);
        var midway = view.Camera.Distance;
        var from = new Offset(700, 80);

        view.Touch(ViewportPhase.DragStart, from);
        view.Touch(ViewportPhase.DragUpdate, from + new Offset(20, 0));

        Assert.Equal(midway, view.Camera.Distance);
        Assert.Equal(midway, view.Goal.Distance);
    }

    [Fact]
    public void BackquoteSwitchesFlyingOnAndOffAndEscapeEndsIt()
    {
        var view = View();

        Assert.True(view.Key(ViewportKey.Backquote, true));
        Assert.True(view.Flying);
        Assert.True(view.Key(ViewportKey.Escape, true));
        Assert.False(view.Flying);
        Assert.False(view.Key(ViewportKey.Escape, true));
    }

    [Fact]
    public void SwitchingTheGridOnAndOffTellsTheView()
    {
        var view = View();
        var told = 0;
        view.Changed += () => told++;

        view.ToggleSnapping();
        view.ToggleSnapping();

        Assert.False(view.Snapping.On);
        Assert.Equal(2, told);
    }

    [Fact]
    public void AMovementKeyIsLeftAloneUnlessFlying()
    {
        var view = View();

        Assert.False(view.Key(ViewportKey.W, true));
        view.Key(ViewportKey.Backquote, true);
        Assert.True(view.Key(ViewportKey.W, true));
        Assert.False(view.Key(ViewportKey.Escape, false));
    }

    [Fact]
    public void FlyingMovesTheCameraByTheSpeedAndTheTimeTaken()
    {
        var view = View();
        view.Key(ViewportKey.Backquote, true);
        view.Key(ViewportKey.W, true);

        view.Tick(Ms(20));

        Assert.Single(_cameras);
        Assert.Equal(8 * 0.02, Moved(view), 1e-9);
    }

    [Fact]
    public void ShiftFliesFourTimesAsFast()
    {
        var view = View();
        view.Key(ViewportKey.Backquote, true);
        view.Key(ViewportKey.W, true);
        view.Key(ViewportKey.ShiftLeft, true);

        view.Tick(Ms(20));

        Assert.Equal(4 * 8 * 0.02, Moved(view), 1e-9);
    }

    [Fact]
    public void TwoKeysAtOnceGoNoFasterThanOne()
    {
        var view = View();
        view.Key(ViewportKey.Backquote, true);
        view.Key(ViewportKey.W, true);
        view.Key(ViewportKey.D, true);

        view.Tick(Ms(20));

        Assert.Equal(8 * 0.02, Moved(view), 1e-9);
    }

    [Fact]
    public void AFrameThatTookASecondMovesNoFurtherThanFiftyMilliseconds()
    {
        var view = View();
        view.Key(ViewportKey.Backquote, true);
        view.Key(ViewportKey.W, true);

        view.Tick(Ms(1000));

        Assert.Equal(8 * 0.05, Moved(view), 1e-9);
    }

    [Fact]
    public void TheWheelSetsTheSpeedWhileFlyingAndZoomsOtherwise()
    {
        var view = View();

        view.Wheel(-100);
        Assert.Single(_eased);
        Assert.Equal(8, view.FlySpeed);

        view.Key(ViewportKey.Backquote, true);
        view.Wheel(-100);
        Assert.Single(_eased);
        Assert.Equal(8 * Math.Exp(0.25), view.FlySpeed, 1e-9);

        view.Wheel(100000);
        Assert.Equal(0.25, view.FlySpeed);
    }

    [Fact]
    public void LookingTurnsTheCameraOnlyWhileTheButtonIsHeld()
    {
        var view = View();
        view.BeginLooking(new Offset(100, 100));

        view.Look(new Offset(110, 100), held: true);
        Assert.Single(_cameras);
        Assert.True(view.Flying);

        view.Look(new Offset(120, 100), held: false);
        Assert.False(view.Flying);
    }

    [Fact]
    public void APinchZoomsAndTwoFingersOrbitAndShiftPans()
    {
        var view = View();
        view.PanZoomStart();

        view.PanZoomUpdate(1.5, Offset.Zero, shifted: false);
        Assert.Single(_cameras);
        Assert.True(_cameras[0].Distance < view.Camera.Distance);

        view.PanZoomUpdate(1.5, new Offset(10, 0), shifted: false);
        Assert.Equal(2, _cameras.Count);
        Assert.NotEqual(view.Camera.Yaw, _cameras[1].Yaw);

        view.PanZoomUpdate(1.5, new Offset(10, 0), shifted: true);
        Assert.NotEqual(view.Camera.Target, _cameras[2].Target);
    }

    [Fact]
    public void ADragIsLeftToTheTrackpadWhileOneIsInProgress()
    {
        var view = View();
        var from = new Offset(700, 80);
        view.PanZoomStart();

        view.Touch(ViewportPhase.DragStart, from);
        view.Touch(ViewportPhase.DragUpdate, from + new Offset(20, 0));
        Assert.Empty(_cameras);

        view.PanZoomEnd();
        view.Touch(ViewportPhase.DragStart, from);
        view.Touch(ViewportPhase.DragUpdate, from + new Offset(20, 0));
        Assert.Single(_cameras);
    }

    [Fact]
    public void TheNumbersADragReportsAreMetresAndSquares()
    {
        var view = View();
        var handles = view.Handles ?? throw new InvalidOperationException("No handles.");
        var from = handles.Projection.Project(handles.Pivot + (GizmoAxis.Y.Direction() * (handles.Length * 0.8)))
            ?? throw new InvalidOperationException("Behind the camera.");

        Assert.Null(view.Dragged);
        view.Touch(ViewportPhase.DragStart, from);
        view.Touch(ViewportPhase.DragUpdate, from + new Offset(0, -20));

        var (metres, squares) = view.Dragged ?? throw new InvalidOperationException("Not dragging.");
        Assert.True(metres > 0);
        Assert.Equal(metres / 0.5, squares, 1e-9);
        Assert.Equal(GizmoAxis.Y, view.Dragging);

        view.Touch(ViewportPhase.DragEnd, from + new Offset(0, -20));
        Assert.Null(view.Dragged);
        Assert.Null(view.Dragging);
    }

    [Fact]
    public void ADragOnARingTurnsTheObjectAndOneUndoPutsItBack()
    {
        var view = View(GizmoMode.Rotate);
        var handles = view.Handles ?? throw new InvalidOperationException("No handles.");
        var ring = handles.RingOnScreen(GizmoAxis.Y);
        // An eighth of the way round, off the points where two rings cross.
        var start = ring.Count / 8;
        var from = ring[start];

        view.Touch(ViewportPhase.DragStart, from);
        Assert.Equal(GizmoAxis.Y, view.Dragging);
        for (var i = 1; i <= 6; i++)
        {
            view.Touch(ViewportPhase.DragUpdate, ring[start + i]);
        }

        view.Touch(ViewportPhase.DragEnd, ring[start + 6]);

        Assert.NotEqual(0, _cube.Rotation.Y);
        Assert.Equal(0, _cube.Rotation.X, 1e-6);
        Assert.Equal(0, _cube.Rotation.Z, 1e-6);
        Assert.True(_history!.CanUndo);
        _history.Undo();
        Assert.Equal(0, _cube.Rotation.Y, 1e-9);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void NothingNeedsTheClockUntilAnObjectMovesOnItsOwnOrTheViewFlies()
    {
        var view = View();
        Assert.False(view.NeedsClock);

        view.Key(ViewportKey.Backquote, true);
        Assert.True(view.NeedsClock);
    }

    [Fact]
    public void TheClockIsToldFourTimesASecond()
    {
        var view = View();
        var told = 0;
        view.Clocked += () => told++;

        for (var ms = 0; ms <= 1000; ms += 50)
        {
            view.Tick(Ms(ms));
        }

        Assert.InRange(told, 4, 5);
    }
}
