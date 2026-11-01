namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;

// Dragging the handles of a mesh's corners, and drawing a box round them. The pointer events go straight to
// the state.
public sealed class ElementDragTests
{
    private static readonly Size Surface = new(800, 600);
    private static readonly OrbitCamera Camera = new(yaw: 0.6, pitch: 0.4, distance: 8);

    private readonly Mesh _mesh = Shape.Of(ShapeKind.Cube).Build();
    private readonly SceneObject _cube;
    private readonly EditorScene _scene;
    private readonly History _history;
    private readonly SceneViewportState _view;
    private readonly List<ElementDrag> _dragged = [];
    private readonly List<(ElementSelection Found, bool Add)> _boxed = [];
    private readonly List<(ElementSelection Found, bool Add)> _picked = [];
    private readonly List<string?> _objectsPicked = [];
    private object? _gesture;

    public ElementDragTests()
    {
        _cube = new SceneObject("shape", "Cube", ObjectKind.Shape, geometry: _mesh);
        _scene = new EditorScene([_cube]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: _scene));
        _history = new History(workspace);
        _view = new SceneViewportState(workspace, new Snapping(on: false), _history)
        {
            Camera = Camera,
            Surface = Surface,
            Primary = "shape",
            Selected = new HashSet<string> { "shape" },
            Context = EditContext.Element,
            ElementMode = ElementMode.Face,
        };

        // What the shell does with a drag, so the undo behaviour under test is the real one.
        _view.ElementsDragged += drag =>
        {
            _dragged.Add(drag);
            if (!drag.Merge)
            {
                _gesture = new object();
            }

            _history.Run(new SetGeometry("a", "shape", "Cube", drag.Mesh, drag.What, _gesture));
            _view.ElementSelection = drag.Selection;
        };
        _view.ElementsBoxed += (found, add) => _boxed.Add((found, add));
        _view.ElementPicked += (found, add) => _picked.Add((found, add));
        _view.Picked += (id, _) => _objectsPicked.Add(id);
    }

    private int Top => _mesh.Faces.FindIndex(f => _mesh.NormalOf(f).Y > 0.99);

    private Mesh Now => _cube.CurrentMesh!;

    private static ViewportProjection Projection => new(Camera, Surface);

    // Where the object stands, which the scene caches until told.
    private void Place(Vector3 position, Vector3 rotation)
    {
        _cube.Position.SetFrom(position);
        _cube.Rotation.SetFrom(rotation);
        _scene.Invalidate();
    }

    private ElementSelection Select(ElementMode mode, ElementSelection selection)
    {
        _view.ElementMode = mode;
        _view.ElementSelection = selection;
        return selection;
    }

    // Not the very end of the arm: a little short of it is well inside the handle's reach whichever
    // way it points.
    private Offset HandleAt(GizmoAxis axis, ElementSelection selection)
    {
        var pivot = selection.PivotIn(_mesh) ?? Vector3.Zero();
        var gizmo = new Gizmo(GizmoMode.Move, pivot, Projection);
        return Projection.Project(gizmo.Pivot + (axis.Direction() * (gizmo.Length * 0.8))) ?? throw new InvalidOperationException("Off screen.");
    }

    private void Drag(Offset from, Offset by, int steps = 8, bool add = false)
    {
        _view.Touch(ViewportPhase.DragStart, from, add);
        for (var i = 1; i <= steps; i++)
        {
            _view.Touch(ViewportPhase.DragUpdate, from + (by * ((double)i / steps)), add);
        }

        _view.Touch(ViewportPhase.DragEnd, from + by, add);
    }

    private void DragBox(bool add = false) =>
        Drag(new Offset(10, 10), new Offset(Surface.Width - 20, Surface.Height - 20), add: add);

    private static Vector3 ShiftOf(Mesh moved, Mesh from, int index) => moved.Positions[index] - from.Positions[index];

    [Fact]
    public void DraggingAHandleMovesTheSelectedFaceAndNothingElse()
    {
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));

        Drag(HandleAt(GizmoAxis.Y, selection), new Offset(0, -80));

        Assert.NotEmpty(_dragged);
        var last = _dragged[^1].Mesh;
        Assert.Equal(_mesh.Faces.Count, last.Faces.Count);
        Assert.Equal(_mesh.Positions.Count, last.Positions.Count);
        var moved = selection.PointsIn(_mesh);
        for (var i = 0; i < last.Positions.Count; i++)
        {
            var shift = ShiftOf(last, _mesh, i);
            if (moved.Contains(i))
            {
                Assert.True(shift.Y > 0.1, $"corner {i} went up");
            }
            else
            {
                Assert.True(shift.Length < 1e-9, $"corner {i} stayed");
            }
        }
    }

    [Fact]
    public void EveryFrameAfterTheFirstFoldsIntoOneStepAndUndoesAtOnce()
    {
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));

        Drag(HandleAt(GizmoAxis.Y, selection), new Offset(0, -80));

        Assert.True(_dragged.Count > 1, "a frame each");
        Assert.False(_dragged[0].Merge);
        Assert.All(_dragged.Skip(1), drag => Assert.True(drag.Merge));
        _history.Undo();
        Assert.False(_history.CanUndo);
        for (var i = 0; i < Now.Positions.Count; i++)
        {
            Assert.True(ShiftOf(Now, _mesh, i).Length < 1e-9);
        }
    }

    [Fact]
    public void TheDragWorksFromACopyAndNeverChangesTheMeshItWasGiven()
    {
        var before = _mesh.Copy();
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));

        Drag(HandleAt(GizmoAxis.Y, selection), new Offset(0, -40));
        _history.Undo();

        for (var i = 0; i < before.Positions.Count; i++)
        {
            Assert.True(ShiftOf(_mesh, before, i).Length < 1e-12);
        }
    }

    [Fact]
    public void HoldingShiftExtrudesBeforeItMoves()
    {
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));
        _view.ShiftHeld = true;

        Drag(HandleAt(GizmoAxis.Y, selection), new Offset(0, -60));

        Assert.Equal("Extrude", _dragged[0].What);
        var made = _dragged[^1].Mesh;
        // Four walls where the face was, and the face carried to the end of them.
        Assert.Equal(_mesh.Faces.Count + 4, made.Faces.Count);
        Assert.Equal(_mesh.Positions.Count + 4, made.Positions.Count);
        // The new face is what is selected afterwards, because somebody who has just extruded is
        // almost always about to extrude again.
        Assert.Single(_dragged[^1].Selection.Faces);
        var top = made.CentreOf(made.Faces[_dragged[^1].Selection.Faces.First()]);
        Assert.True(top.Y > _mesh.CentreOf(_mesh.Faces[Top]).Y);
    }

    [Fact]
    public void TheExtrudeIsWrittenBeforeAnythingHasMoved()
    {
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));
        _view.ShiftHeld = true;

        // Barely a movement: enough to start a drag and no more. Somebody who pulls a face and
        // changes their mind has still made one, and it should be there and be undoable.
        Drag(HandleAt(GizmoAxis.Y, selection), new Offset(0, -6), steps: 3);

        Assert.Equal("Extrude", _dragged[0].What);
        Assert.False(_dragged[0].Merge);
        Assert.Equal(_mesh.Faces.Count + 4, _dragged[0].Mesh.Faces.Count);
    }

    [Fact]
    public void ShiftOnAVertexSelectionMovesAndDoesNotExtrude()
    {
        var selection = Select(ElementMode.Vertex, new ElementSelection(vertices: [0, 1]));
        _view.ShiftHeld = true;

        Drag(HandleAt(GizmoAxis.Y, selection), new Offset(0, -50));

        Assert.NotEmpty(_dragged);
        Assert.All(_dragged, drag => Assert.Equal("Move", drag.What));
        Assert.Equal(_mesh.Faces.Count, _dragged[^1].Mesh.Faces.Count);
    }

    [Fact]
    public void ShiftWhileTurningDoesNotExtrudeEither()
    {
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));
        _view.SelectMode(GizmoMode.Rotate);
        _view.ShiftHeld = true;
        var ring = _view.Handles!.RingOnScreen(GizmoAxis.Y);

        _view.Touch(ViewportPhase.DragStart, ring[ring.Count / 8]);
        _view.Touch(ViewportPhase.DragUpdate, ring[(ring.Count / 8) + 4]);
        _view.Touch(ViewportPhase.DragEnd, ring[(ring.Count / 8) + 4]);

        Assert.NotEmpty(_dragged);
        Assert.All(_dragged, drag => Assert.Equal("Turn", drag.What));
        Assert.Equal(_mesh.Faces.Count, _dragged[^1].Mesh.Faces.Count);
        Assert.NotEmpty(selection.Faces);
    }

    [Fact]
    public void TurningSpinsTheSelectedCornersAboutTheirOwnCentre()
    {
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));
        _view.SelectMode(GizmoMode.Rotate);
        var ring = _view.Handles!.RingOnScreen(GizmoAxis.Y);
        var start = ring.Count / 8;

        _view.Touch(ViewportPhase.DragStart, ring[start]);
        for (var i = 1; i <= 6; i++)
        {
            _view.Touch(ViewportPhase.DragUpdate, ring[start + i]);
        }

        _view.Touch(ViewportPhase.DragEnd, ring[start + 6]);

        var last = _dragged[^1].Mesh;
        var moved = selection.PointsIn(_mesh);
        Assert.Equal(_mesh.CentreOfPoints(moved)!.Y, last.CentreOfPoints(moved)!.Y, 1e-9);
        Assert.Equal(_mesh.CentreOfPoints(moved)!.X, last.CentreOfPoints(moved)!.X, 1e-9);
        Assert.All(moved, index => Assert.True(ShiftOf(last, _mesh, index).Length > 1e-3, $"corner {index} turned"));
        Assert.All(
            Enumerable.Range(0, last.Positions.Count).Where(index => !moved.Contains(index)),
            index => Assert.True(ShiftOf(last, _mesh, index).Length < 1e-9, $"corner {index} stayed"));
    }

    [Fact]
    public void WithNothingSelectedTheHandlesMoveTheObjectAgain()
    {
        var selection = Select(ElementMode.Face, new ElementSelection());

        Drag(HandleAt(GizmoAxis.Y, selection), new Offset(0, -40));

        // The handles are back on the object, so a drag on them is an object move and nothing is
        // reported as a change to its geometry.
        Assert.Empty(_dragged);
        Assert.True(_cube.Position.Y > 0.1);
    }

    [Fact]
    public void TheHandlesSitOnTheSelectionRatherThanTheObject()
    {
        Place(new Vector3(2, 0, 0), Vector3.Zero());
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));

        var pivot = _view.Handles!.Pivot;

        var expected = _mesh.CentreOfPoints(selection.PointsIn(_mesh))! + new Vector3(2, 0, 0);
        Assert.Equal(expected.X, pivot.X, 1e-9);
        Assert.Equal(expected.Y, pivot.Y, 1e-9);
        Assert.Equal(expected.Z, pivot.Z, 1e-9);
    }

    [Fact]
    public void ADragInsideATurnedObjectMovesTheCornersAlongTheWorldAxis()
    {
        Place(Vector3.Zero(), new Vector3(0, 90, 0));
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));

        Drag(HandleAt(GizmoAxis.Y, selection), new Offset(0, -60));

        // Turning about Y leaves up where it was, so the corners still go up in the object's frame.
        var shift = ShiftOf(_dragged[^1].Mesh, _mesh, selection.PointsIn(_mesh).First());
        Assert.True(shift.Y > 0.1);
        Assert.Equal(0, shift.X, 1e-6);
    }

    [Fact]
    public void ASnappedDragLandsTheSelectionOnAGridLine()
    {
        _view.Snapping = new Snapping(step: 0.5);
        var selection = Select(ElementMode.Face, new ElementSelection(faces: [Top]));
        var was = _mesh.CentreOfPoints(selection.PointsIn(_mesh))!.Y;

        Drag(HandleAt(GizmoAxis.Y, selection), new Offset(0, -70));

        var last = _dragged[^1];
        var now = last.Mesh.CentreOfPoints(last.Selection.PointsIn(last.Mesh))!.Y;
        Assert.True(now > was, "it moved");
        Assert.Equal(0, now % 0.5, 1e-6);
    }

    [Fact]
    public void AMarqueeOverTheWholeShapeTakesEveryFaceInOneReport()
    {
        _view.SeeThroughElements = true;

        DragBox();

        var (found, add) = Assert.Single(_boxed);
        Assert.Equal(_mesh.Faces.Count, found.Faces.Count);
        Assert.False(add);
    }

    [Fact]
    public void WithoutSeeingThroughTheFarSideIsLeftBehind()
    {
        DragBox();

        // Three of a cube's six faces point at the camera from any angle that is not straight on,
        // and the other three are behind them.
        var found = Assert.Single(_boxed).Found;
        Assert.InRange(found.Faces.Count, 1, _mesh.Faces.Count - 1);
    }

    [Fact]
    public void AMarqueeInVertexModeTakesTheVerticesAndInEdgeModeTheEdges()
    {
        _view.SeeThroughElements = true;
        _view.ElementMode = ElementMode.Vertex;
        DragBox();
        _view.ElementMode = ElementMode.Edge;
        DragBox();

        Assert.Equal(_mesh.Positions.Count, _boxed[0].Found.Vertices.Count);
        Assert.Empty(_boxed[0].Found.Faces);
        Assert.Equal(_mesh.AllEdges.Count, _boxed[1].Found.Edges.Count);
    }

    [Fact]
    public void ABoxThatMissesEverythingSaysSoRatherThanDoingNothing()
    {
        Select(ElementMode.Face, new ElementSelection(faces: [0]));

        Drag(new Offset(4, 4), new Offset(40, 30));

        Assert.True(Assert.Single(_boxed).Found.IsEmpty);
    }

    [Fact]
    public void AClickThatWobbledIsNotAMarquee()
    {
        Select(ElementMode.Face, new ElementSelection(faces: [0]));

        Drag(new Offset(400, 300), new Offset(2, 1), steps: 2);

        Assert.Empty(_boxed);
        Assert.Null(_view.Marquee);
    }

    [Fact]
    public void HoldingShiftAddsToWhatWasAlreadyThere()
    {
        Select(ElementMode.Face, new ElementSelection(faces: [0]));

        DragBox(add: true);

        Assert.True(Assert.Single(_boxed).Add);
    }

    [Fact]
    public void TheBoxIsThereWhileDraggingAndGoneAfter()
    {
        _view.Touch(ViewportPhase.DragStart, new Offset(100, 100));
        _view.Touch(ViewportPhase.DragUpdate, new Offset(300, 250));

        Assert.Equal(new Rect(100, 100, 300, 250), _view.Marquee);

        _view.Touch(ViewportPhase.DragUpdate, new Offset(50, 40));

        // Dragged up and to the left is the same box the other way round.
        Assert.Equal(new Rect(50, 40, 100, 100), _view.Marquee);

        _view.Touch(ViewportPhase.DragCancel, new Offset(50, 40));

        Assert.Null(_view.Marquee);
        Assert.Empty(_boxed);
    }

    [Fact]
    public void TappingAFaceReportsItAndNeverPicksAnotherObject()
    {
        var at = Projection.Project(_mesh.CentreOf(_mesh.Faces[Top]))!.Value;

        _view.Touch(ViewportPhase.Tap, at, add: true);

        var (found, add) = Assert.Single(_picked);
        Assert.Equal([Top], found.Faces);
        Assert.True(add);
        Assert.Empty(_objectsPicked);
    }

    [Fact]
    public void TappingAVertexInVertexModeReportsTheNearestOne()
    {
        _view.ElementMode = ElementMode.Vertex;
        var corner = _mesh.Faces[Top].Vertices[0];
        var at = Projection.Project(_mesh.Positions[corner])!.Value + new Offset(3, 2);

        _view.Touch(ViewportPhase.Tap, at);

        Assert.Equal([corner], Assert.Single(_picked).Found.Vertices);
    }

    [Fact]
    public void TappingNothingReportsNothingSoTheSelectionCanBeCleared()
    {
        _view.Touch(ViewportPhase.Tap, new Offset(5, 5));

        Assert.True(Assert.Single(_picked).Found.IsEmpty);
        Assert.Empty(_objectsPicked);
    }

    [Fact]
    public void HoveringNamesTheElementUnderThePointerAndLeavingClearsIt()
    {
        _view.ElementMode = ElementMode.Vertex;
        var corner = _mesh.Faces[Top].Vertices[0];
        var told = 0;
        _view.Changed += () => told++;

        _view.Touch(ViewportPhase.Hover, Projection.Project(_mesh.Positions[corner])!.Value);

        Assert.Equal([corner], _view.HoveredElement!.Vertices);
        Assert.True(told > 0);

        _view.Touch(ViewportPhase.Hover, new Offset(5, 5));

        Assert.Null(_view.HoveredElement);
    }

    [Fact]
    public void OutsideEditingAnEmptyDragTurnsTheViewAndATapPicksObjects()
    {
        _view.Context = EditContext.Whole;
        var turned = new List<OrbitCamera>();
        _view.CameraChanged += turned.Add;

        Drag(new Offset(10, 10), new Offset(60, 20));
        _view.Touch(ViewportPhase.Tap, new Offset(5, 5));

        Assert.NotEmpty(turned);
        Assert.Null(_view.Marquee);
        Assert.Empty(_boxed);
        Assert.Empty(_picked);
        Assert.Single(_objectsPicked);
    }

    [Fact]
    public void WithoutAShapeSelectedThereIsNothingToEdit()
    {
        _view.Primary = null;

        Assert.Null(_view.Editing);
    }
}
