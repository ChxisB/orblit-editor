namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;

public sealed class ViewportInputTests
{
    private readonly List<string> _seen = [];

    // A stage that notes everything it is shown and takes what it is told.
    private ViewportStage Stage(string name, params ViewportPhase[] takes) =>
        new(name, gesture =>
        {
            _seen.Add($"{name} {PhaseName(gesture.Phase)}");
            return takes.Contains(gesture.Phase);
        });

    private static string PhaseName(ViewportPhase phase) => phase switch
    {
        ViewportPhase.Hover => "hover",
        ViewportPhase.Leave => "leave",
        ViewportPhase.Tap => "tap",
        ViewportPhase.DragStart => "dragStart",
        ViewportPhase.DragUpdate => "dragUpdate",
        ViewportPhase.DragEnd => "dragEnd",
        ViewportPhase.DragCancel => "dragCancel",
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private static ViewportGesture At(ViewportPhase phase) => new(phase, Offset.Zero);

    [Fact]
    public void TheFirstStageToTakeAGestureIsTheOnlyOneToSeeIt()
    {
        var order = new ViewportInputOrder(() =>
        [
            Stage("tool"),
            Stage("gizmo", ViewportPhase.Tap),
            Stage("selection", ViewportPhase.Tap),
        ]);

        Assert.Equal("gizmo", order.Handle(At(ViewportPhase.Tap)));
        Assert.Equal(["tool tap", "gizmo tap"], _seen);
    }

    [Fact]
    public void NothingTakingAGestureSaysSo()
    {
        var order = new ViewportInputOrder(() => [Stage("tool")]);
        Assert.Null(order.Handle(At(ViewportPhase.Tap)));
    }

    [Fact]
    public void ADragStaysWithTheStageThatTookItsStart()
    {
        List<ViewportStage> stages =
        [
            Stage("tool", ViewportPhase.DragStart),
            Stage("gizmo", ViewportPhase.DragStart),
        ];
        var order = new ViewportInputOrder(() => stages);

        Assert.Equal("tool", order.Handle(At(ViewportPhase.DragStart)));
        Assert.Equal("tool", order.Holding);

        // Whatever the order has become since, the rest of the drag is the tool's: half a brush
        // stroke is not something to hand to the handles.
        stages = [stages[^1]];
        order.Handle(At(ViewportPhase.DragUpdate));
        order.Handle(At(ViewportPhase.DragEnd));

        Assert.Equal(["tool dragStart", "tool dragUpdate", "tool dragEnd"], _seen);
        Assert.Null(order.Holding);
    }

    [Fact]
    public void ADragNobodyTookGoesNowhere()
    {
        var order = new ViewportInputOrder(() => [Stage("selection")]);
        order.Handle(At(ViewportPhase.DragStart));
        _seen.Clear();

        Assert.Null(order.Handle(At(ViewportPhase.DragUpdate)));
        Assert.Null(order.Handle(At(ViewportPhase.DragEnd)));
        Assert.Empty(_seen);
    }

    [Fact]
    public void AHoverTakenAheadIsALeaveBehind()
    {
        var order = new ViewportInputOrder(() =>
        [
            Stage("tool"),
            Stage("gizmo", ViewportPhase.Hover),
            Stage("selection", ViewportPhase.Hover),
        ]);

        Assert.Equal("gizmo", order.Handle(At(ViewportPhase.Hover)));
        // The stage ahead looked and passed; the one behind is told the pointer is not over
        // anything of its own, so its highlight goes out.
        Assert.Equal(["tool hover", "gizmo hover", "selection leave"], _seen);
    }

    [Fact]
    public void LeavingTheViewIsALeaveForEverybody()
    {
        var order = new ViewportInputOrder(() => [Stage("tool"), Stage("gizmo"), Stage("selection")]);
        order.Leave();
        Assert.Equal(["tool leave", "gizmo leave", "selection leave"], _seen);
    }

    [Fact]
    public void ACancelledDragIsCancelledWhereItWas()
    {
        Offset? where = null;
        var order = new ViewportInputOrder(() =>
        [
            new ViewportStage("tool", gesture =>
            {
                where = gesture.At;
                return true;
            }),
        ]);
        order.Handle(new ViewportGesture(ViewportPhase.DragStart, new Offset(1, 1)));
        order.Handle(new ViewportGesture(ViewportPhase.DragUpdate, new Offset(4, 5)));
        order.Cancel();

        Assert.Equal(new Offset(4, 5), where);
        Assert.Null(order.Holding);
    }

    [Fact]
    public void ADragWhoseEndWentMissingIsCancelledByTheNext()
    {
        var order = new ViewportInputOrder(() => [Stage("tool", ViewportPhase.DragStart)]);
        order.Handle(At(ViewportPhase.DragStart));
        order.Handle(At(ViewportPhase.DragStart));

        Assert.Equal(["tool dragStart", "tool dragCancel", "tool dragStart"], _seen);
    }

    // ---- a scene view ----

    private static readonly OrbitCamera ViewCamera = new(yaw: 0.6, pitch: 0.4, distance: 8);

    private static readonly Size ViewSize = new(800, 600);

    // A corner of the view with nothing in it.
    private static readonly Offset Nowhere = new(700, 80);

    private readonly SceneObject _cube = new(
        "a",
        "A",
        ObjectKind.Shape,
        position: new Vector3(0, 0.5, 0),
        shape: Shape.Of(ShapeKind.Cube));

    private readonly List<string?> _picked = [];
    private History? _history;
    private int _orbits;

    private History ViewHistory => _history ?? throw new InvalidOperationException("No view was made.");

    private SceneViewportState View(Func<ViewportGesture, bool>? tool = null, params GizmoType[] gizmos)
    {
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("s", "S", scene: new EditorScene([_cube])));
        _history = new History(workspace);

        var view = new SceneViewportState(workspace, new Snapping(on: false), _history, gizmos)
        {
            Camera = ViewCamera,
            Surface = ViewSize,
            Primary = "a",
            Selected = new HashSet<string> { "a" },
            ModeInput = tool,
        };
        view.CameraChanged += _ => _orbits++;
        view.Picked += (id, _) => _picked.Add(id);
        return view;
    }

    // Where the Y handle is on screen.
    private Offset Handle()
    {
        var projection = new ViewportProjection(ViewCamera, ViewSize);
        var gizmo = new Gizmo(GizmoMode.Move, _cube.Position, projection);
        return projection.Project(gizmo.Pivot + (GizmoAxis.Y.Direction() * (gizmo.Length * 0.8)))
            ?? throw new InvalidOperationException("The handle is behind the camera.");
    }

    private static void Drag(SceneViewportState view, Offset from)
    {
        view.Touch(ViewportPhase.DragStart, from);
        var at = from;
        for (var i = 0; i < 6; i++)
        {
            at += new Offset(0, -6);
            view.Touch(ViewportPhase.DragUpdate, at);
        }

        view.Touch(ViewportPhase.DragEnd, at);
    }

    [Fact]
    public void ADragOnAHandleMovesWhatItIsOn()
    {
        var view = View();
        Drag(view, Handle());

        Assert.True(_cube.Position.Y > 0.5);
        Assert.True(ViewHistory.CanUndo);
    }

    [Fact]
    public void AToolTakesADragBeforeTheHandlesDo()
    {
        var phases = new List<ViewportPhase>();
        var view = View(gesture =>
        {
            phases.Add(gesture.Phase);
            return gesture.Phase == ViewportPhase.DragStart;
        });
        Drag(view, Handle());

        // The whole drag went to the tool, and none of it reached the handles it started on.
        Assert.Contains(ViewportPhase.DragStart, phases);
        Assert.Contains(ViewportPhase.DragUpdate, phases);
        Assert.Equal(ViewportPhase.DragEnd, phases[^1]);
        Assert.Equal(0.5, _cube.Position.Y);
        Assert.False(ViewHistory.CanUndo);
    }

    [Fact]
    public void AToolThatPassesLetsTheHandlesHaveIt()
    {
        var view = View(_ => false);
        Drag(view, Handle());

        Assert.True(_cube.Position.Y > 0.5);
    }

    [Fact]
    public void ADragNothingElseWantsTurnsTheView()
    {
        var view = View();
        Drag(view, Nowhere);

        Assert.True(_orbits > 0);
        Assert.Equal(0.5, _cube.Position.Y);
    }

    [Fact]
    public void AToolIsGivenAWayIntoTheWorld()
    {
        ViewportGesture? given = null;
        var view = View(gesture =>
        {
            given = gesture;
            return true;
        });
        view.Touch(ViewportPhase.Tap, Nowhere);

        Assert.Equal(ViewportPhase.Tap, given?.Phase);
        Assert.Equal(ViewSize, given?.Projection?.Size);
        // The tool took the click.
        Assert.Empty(_picked);
    }

    [Fact]
    public void ARegisteredGizmoIsDrawnAndAskedBeforeTheSelection()
    {
        GizmoTarget? asked = null;
        var view = View(
            null,
            new GizmoType(
                "probe",
                target => target.Item.Kind == ObjectKind.Shape,
                target => [new GizmoStroke(new WorldLines(target.Projection), GizmoTone.Good)],
                (target, gesture) =>
                {
                    asked = target;
                    return gesture.Phase == ViewportPhase.Tap;
                }));

        Assert.Equal(["probe"], view.Overlays().Select(overlay => overlay.Name));

        view.Touch(ViewportPhase.Tap, Nowhere);

        Assert.Equal("a", asked?.Item.Id);
        Assert.Equal(new[] { "a" }, asked?.Selected);
        // The gizmo took the click.
        Assert.Empty(_picked);
    }

    [Fact]
    public void AGizmoThatDoesNotApplyIsNeitherDrawnNorAsked()
    {
        var asked = false;
        var view = View(
            null,
            new GizmoType(
                "probe",
                target => target.Item.Kind == ObjectKind.Light,
                target => [new GizmoStroke(new WorldLines(target.Projection), GizmoTone.Good)],
                (_, _) => asked = true));

        Assert.Empty(view.Overlays());

        view.Touch(ViewportPhase.Tap, Nowhere);

        Assert.False(asked);
        // The click reached the selection.
        Assert.Equal([null], _picked);
    }

    [Fact]
    public void TwoGizmosUnderOneNameIsAMistake() =>
        Assert.Throws<InvalidOperationException>(() => View(null, new GizmoType("transform", _ => true)));
}
