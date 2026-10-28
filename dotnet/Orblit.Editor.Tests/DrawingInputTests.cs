namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;

// Where a click puts a point while a tool is drawing, and what else the view stops doing. The Dart
// does this through a widget, with the pointer's events. Here they go straight to the state.
public sealed class DrawingInputTests
{
    private static readonly Size Surface = new(800, 600);
    private static readonly OrbitCamera Camera = new(yaw: 0.6, pitch: 0.7, distance: 9);

    private readonly Mesh _mesh = Shape.Of(ShapeKind.Cube).Build();
    private readonly Sketch _sketch = new();
    private readonly SceneViewportState _view;
    private readonly List<DrawnPoint> _pointed = [];
    private readonly List<string?> _picked = [];
    private int _finished;
    private int _cameraMoves;
    private int _changes;

    public DrawingInputTests()
    {
        var cube = new SceneObject("shape", "Cube", ObjectKind.Shape, geometry: _mesh);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: new EditorScene([cube])));
        _view = new SceneViewportState(workspace, new Snapping(on: false), new History(workspace))
        {
            Camera = Camera,
            Surface = Surface,
            Primary = "shape",
            Selected = new HashSet<string> { "shape" },
            Sketch = _sketch,
        };

        // What the session does with a point, so the plane is fixed the way it is for real.
        _view.DrawPointed += point =>
        {
            _pointed.Add(point);
            _sketch.PlaneAt(point.Origin, point.Normal, point.Face);
            _sketch.Add(point.At);
        };
        _view.DrawFinished += () => _finished++;
        _view.Picked += (id, _) => _picked.Add(id);
        _view.CameraChanged += _ => _cameraMoves++;
        _view.Changed += () => _changes++;
    }

    private static ViewportProjection Projection => new(Camera, Surface);

    private int Top => _mesh.Faces.FindIndex(f => _mesh.NormalOf(f).Y > 0.99);

    private void Editing() => _view.Context = EditContext.Element;

    // Where a world point lands on the surface.
    private static Offset On(double x, double y, double z) =>
        Projection.Project(new Vector3(x, y, z)) ?? throw new InvalidOperationException("Off screen.");

    private void Tap(Offset at) => _view.Touch(ViewportPhase.Tap, at);

    [Fact]
    public void AClickOnNothingPutsAPointOnTheGround()
    {
        _sketch.Start(ViewportTool.PolyShape);

        Tap(On(4, 0, 3));

        var point = Assert.Single(_pointed);
        Assert.Equal(4, point.At.X, 1e-6);
        Assert.Equal(0, point.At.Y, 1e-6);
        Assert.Equal(3, point.At.Z, 1e-6);
        Assert.Equal(1, point.Normal.Y, 1e-9);
        Assert.Null(point.Face);
    }

    [Fact]
    public void OnceThePlaneIsDownEveryPointIsOnIt()
    {
        _sketch.Start(ViewportTool.PolyShape);
        Tap(On(4, 0, 3));
        Editing();

        // Over the cube, which has a face under the pointer, but the plane was chosen by the first.
        Tap(On(0, 1, 0));

        var second = _pointed[1];
        Assert.Equal(0, second.At.Y, 1e-6);
        Assert.Null(second.Face);
    }

    [Fact]
    public void WithoutAPlaneAFaceUnderThePointerBecomesIt()
    {
        _sketch.Start(ViewportTool.PolyShape);
        Editing();

        Tap(On(0, 1, 0));

        var point = Assert.Single(_pointed);
        Assert.Equal(Top, point.Face);
        Assert.Equal(1, point.Normal.Y, 1e-9);
        Assert.Equal(1, point.Origin.Y, 1e-9);
        Assert.Equal(1, point.At.Y, 1e-6);
    }

    [Fact]
    public void ACutOnlyStartsOnAFace()
    {
        _sketch.Start(ViewportTool.Cut);
        Editing();

        Tap(On(6, 0, 6));
        Assert.Empty(_pointed);

        Tap(On(0, 1, 0));
        Assert.Single(_pointed);
    }

    [Fact]
    public void AClickBackOnTheFirstPointFinishesAShape()
    {
        _sketch.Start(ViewportTool.PolyShape);
        Tap(On(4, 0, 3));
        Tap(On(4, 0, 6));
        Tap(On(7, 0, 6));

        Tap(On(4, 0, 3));

        Assert.Equal(3, _pointed.Count);
        Assert.Equal(1, _finished);
    }

    [Fact]
    public void AClickBackOnTheFirstPointOfACutIsJustAnotherPoint()
    {
        _sketch.Start(ViewportTool.Cut);
        Editing();
        Tap(On(0, 1, 0));
        Tap(On(0.5, 1, 0.5));
        Tap(On(-0.5, 1, 0.5));

        Tap(On(0, 1, 0));

        Assert.Equal(0, _finished);
    }

    [Fact]
    public void TheLineFollowsThePointer()
    {
        _sketch.Start(ViewportTool.PolyShape);

        _view.Touch(ViewportPhase.Hover, On(2, 0, 5));

        Assert.Equal(2, _sketch.Hovering!.X, 1e-6);
        Assert.Equal(5, _sketch.Hovering.Z, 1e-6);
    }

    [Fact]
    public void AHoverThatHasNotMovedDoesNotAskForADraw()
    {
        _sketch.Start(ViewportTool.PolyShape);
        var at = On(2, 0, 5);
        _view.Touch(ViewportPhase.Hover, at);
        var before = _changes;

        _view.Touch(ViewportPhase.Hover, at);

        Assert.Equal(before, _changes);
    }

    [Fact]
    public void TheLineReachesTheGroundEvenForACut()
    {
        _sketch.Start(ViewportTool.Cut);

        _view.Touch(ViewportPhase.Hover, On(2, 0, 5));

        Assert.NotNull(_sketch.Hovering);
    }

    [Fact]
    public void ADragWhileDrawingDoesNotTurnTheView()
    {
        _sketch.Start(ViewportTool.PolyShape);
        var from = On(4, 0, 3);

        _view.Touch(ViewportPhase.DragStart, from);
        _view.Touch(ViewportPhase.DragUpdate, from + new Offset(60, 20));
        _view.Touch(ViewportPhase.DragEnd, from + new Offset(60, 20));

        Assert.Equal(0, _cameraMoves);
        Assert.Empty(_pointed);
    }

    [Fact]
    public void WithNoToolTheClickSelectsAsUsual()
    {
        Tap(On(0, 0, 0));

        Assert.Empty(_pointed);
        Assert.Single(_picked);
    }

    [Fact]
    public void AViewWithoutASurfaceTakesNothing()
    {
        _sketch.Start(ViewportTool.PolyShape);
        _view.Surface = null;

        Tap(new Offset(10, 10));

        Assert.Empty(_pointed);
    }

    [Fact]
    public void ARayThatNeverMeetsThePlaneAddsNothingRatherThanLandingOnTheGround()
    {
        _sketch.Start(ViewportTool.PolyShape);
        var pixel = On(4, 0, 3);
        var ray = Projection.RayThrough(pixel);
        // A plane the ray runs along, which it can never meet.
        _sketch.PlaneAt(ray.Origin, ray.Direction.Cross(new Vector3(0, 1, 0)));

        Tap(pixel);

        Assert.Empty(_pointed);
    }
}
