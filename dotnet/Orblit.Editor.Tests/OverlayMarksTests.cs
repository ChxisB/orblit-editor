namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;

// Added in C#: the Dart painters draw straight to a canvas and have no test of their own. These check
// the marks they would have drawn.
public sealed class OverlayMarksTests
{
    private static readonly Size Surface = new(800, 600);
    private static readonly OrbitCamera Camera = new(yaw: 0.6, pitch: 0.4, distance: 8);

    private readonly Mesh _mesh = Shape.Of(ShapeKind.Cube).Build();
    private readonly Sketch _sketch = new();
    private readonly SceneViewportState _view;

    public OverlayMarksTests()
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
            Context = EditContext.Element,
            ElementMode = ElementMode.Face,
            Sketch = _sketch,
        };
    }

    private static ViewportProjection Projection => new(Camera, Surface);

    private int Top => _mesh.Faces.FindIndex(f => _mesh.NormalOf(f).Y > 0.99);

    private List<OverlayMark> Elements() => ElementMarks.Of(_view, Projection);

    private static List<T> Of<T>(List<OverlayMark> marks, OverlayStyle style) where T : OverlayMark =>
        [.. marks.OfType<T>().Where(mark => mark.Style == style)];

    // Inside the top face, towards the corner furthest from where the handles meet. The handles of the
    // mesh take the pointer before the mesh does, and their arms cross the middle of the face.
    private Offset OnTop()
    {
        var corners = _mesh.Faces[Top].Vertices.Select(index => Projection.Project(_mesh.Positions[index])!.Value).ToList();
        var middle = new Offset(corners.Average(c => c.Dx), corners.Average(c => c.Dy));
        var pivot = On(0, 0, 0);
        var far = corners.MaxBy(corner => (corner - pivot).Distance);
        return middle + ((far - middle) * 0.8);
    }

    private static Offset On(double x, double y, double z) =>
        Projection.Project(new Vector3(x, y, z)) ?? throw new InvalidOperationException("Off screen.");

    private void Draw(ViewportTool tool, params Vector3[] points)
    {
        _sketch.Start(tool);
        foreach (var point in points)
        {
            _sketch.Add(point);
        }
    }

    [Fact]
    public void EveryEdgeIsWiredFaintlyWhateverTheModeIs()
    {
        foreach (var mode in Enum.GetValues<ElementMode>())
        {
            _view.ElementMode = mode;

            var wire = Assert.Single(Of<LineMark>(Elements(), OverlayStyle.Wire));

            Assert.Equal(_mesh.AllEdges.Count, wire.Lines.Count);
        }
    }

    [Fact]
    public void OutsideEditingThereAreNoElementMarks()
    {
        _view.Context = EditContext.Whole;

        Assert.Empty(Elements());
    }

    [Fact]
    public void AChosenFaceIsFilledAndOutlinedAndTheRestAreLeftAlone()
    {
        _view.ElementSelection = new ElementSelection(faces: [Top]);

        var chosen = Assert.Single(Of<PolygonMark>(Elements(), OverlayStyle.FaceChosen));

        Assert.Equal(_mesh.Faces[Top].Vertices.Count, chosen.Corners.Count);
        Assert.Empty(Of<PolygonMark>(Elements(), OverlayStyle.FaceHovered));
    }

    [Fact]
    public void AFaceUnderThePointerIsLitLighterThanAChosenOne()
    {
        _view.Touch(ViewportPhase.Hover, OnTop());

        var hovered = Assert.Single(Of<PolygonMark>(Elements(), OverlayStyle.FaceHovered));

        Assert.Equal(4, hovered.Corners.Count);
    }

    [Fact]
    public void AChosenFaceThatIsAlsoHoveredIsDrawnOnlyAsChosen()
    {
        var top = Top;
        _view.ElementSelection = new ElementSelection(faces: [top]);
        _view.Touch(ViewportPhase.Hover, OnTop());

        Assert.Empty(Of<PolygonMark>(Elements(), OverlayStyle.FaceHovered));
        Assert.Single(Of<PolygonMark>(Elements(), OverlayStyle.FaceChosen));
    }

    [Fact]
    public void ChosenEdgesAreBatchedAndAnEdgeNobodyChoseIsNotThere()
    {
        _view.ElementMode = ElementMode.Edge;
        var edges = _mesh.AllEdges.Take(2).ToList();
        _view.ElementSelection = new ElementSelection(edges: edges);

        var chosen = Assert.Single(Of<LineMark>(Elements(), OverlayStyle.EdgeChosen));

        Assert.Equal(2, chosen.Lines.Count);
        Assert.Empty(Of<LineMark>(Elements(), OverlayStyle.EdgeHovered));
    }

    [Fact]
    public void EveryCornerIsADotAndAChosenOneIsBiggerAndOrange()
    {
        _view.ElementMode = ElementMode.Vertex;
        _view.ElementSelection = new ElementSelection(vertices: [0]);

        var marks = Elements();

        Assert.Single(Of<DotMark>(marks, OverlayStyle.VertexChosen));
        Assert.Equal(_mesh.Positions.Count - 1, Of<DotMark>(marks, OverlayStyle.Vertex).Count);
    }

    [Fact]
    public void ACornerUnderThePointerIsBiggerButKeepsThePlainColour()
    {
        _view.ElementMode = ElementMode.Vertex;
        _view.Touch(ViewportPhase.Hover, Projection.Project(_mesh.Positions[_mesh.Faces[Top].Vertices[0]])!.Value);

        Assert.Single(Of<DotMark>(Elements(), OverlayStyle.VertexHovered));
    }

    [Fact]
    public void APointBehindTheCameraIsSkippedAndTheRestAreKept()
    {
        _view.ElementMode = ElementMode.Vertex;
        // Inside the cube, so about half its corners are behind the eye.
        var close = new ViewportProjection(new OrbitCamera(yaw: 0.6, pitch: 0.1, distance: 0.3), Surface);

        var dots = ElementMarks.Of(_view, close).OfType<DotMark>().Count();

        Assert.InRange(dots, 1, _mesh.Positions.Count - 1);
    }

    [Fact]
    public void NothingIsDrawnWhileNoToolIsDrawing()
    {
        Assert.Empty(SketchMarks.Of(_sketch, Projection));
    }

    [Fact]
    public void PlacedPointsAreJoinedAndEachHasADot()
    {
        Draw(ViewportTool.PolyShape, new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 0, 2));

        var marks = SketchMarks.Of(_sketch, Projection);

        Assert.Equal(2, Assert.Single(Of<LineMark>(marks, OverlayStyle.Placed)).Lines.Count);
        Assert.Equal(3, Of<DotMark>(marks, OverlayStyle.Point).Count);
    }

    [Fact]
    public void OnlyTheFirstPointOfAShapeIsRingedBecauseClickingItClosesIt()
    {
        Draw(ViewportTool.PolyShape, new Vector3(0, 0, 0), new Vector3(2, 0, 0));

        var ring = Assert.Single(Of<DotMark>(SketchMarks.Of(_sketch, Projection), OverlayStyle.Ring));

        Assert.Equal(On(0, 0, 0), ring.At);
    }

    [Fact]
    public void ACutHasNoRingBecauseItIsNotClosed()
    {
        Draw(ViewportTool.Cut, new Vector3(0, 0, 0), new Vector3(2, 0, 0));

        Assert.Empty(Of<DotMark>(SketchMarks.Of(_sketch, Projection), OverlayStyle.Ring));
    }

    [Fact]
    public void TheLineFollowingThePointerGoesBackToTheStartOnceThereAreTwoPoints()
    {
        Draw(ViewportTool.PolyShape, new Vector3(0, 0, 0), new Vector3(2, 0, 0));
        _sketch.Hovering = new Vector3(2, 0, 2);

        var marks = SketchMarks.Of(_sketch, Projection);

        var ghost = Assert.Single(Of<LineMark>(marks, OverlayStyle.Ghost));
        Assert.Equal(
            [(On(2, 0, 0), On(2, 0, 2)), (On(2, 0, 2), On(0, 0, 0))],
            ghost.Lines);
        Assert.Equal(On(2, 0, 2), Assert.Single(Of<DotMark>(marks, OverlayStyle.Reach)).At);
    }

    [Fact]
    public void WithOnePointTheLineFollowingThePointerDoesNotLoopBack()
    {
        Draw(ViewportTool.PolyShape, new Vector3(0, 0, 0));
        _sketch.Hovering = new Vector3(2, 0, 0);

        var ghost = Assert.Single(Of<LineMark>(SketchMarks.Of(_sketch, Projection), OverlayStyle.Ghost));

        Assert.Single(ghost.Lines);
    }

    [Fact]
    public void ACutDoesNotLoopBackToItsStart()
    {
        Draw(ViewportTool.Cut, new Vector3(0, 0, 0), new Vector3(2, 0, 0));
        _sketch.Hovering = new Vector3(2, 0, 2);

        var ghost = Assert.Single(Of<LineMark>(SketchMarks.Of(_sketch, Projection), OverlayStyle.Ghost));

        Assert.Single(ghost.Lines);
    }

    [Fact]
    public void WithNoPointsYetOnlyThePointerDotShows()
    {
        Draw(ViewportTool.PolyShape);
        _sketch.Hovering = new Vector3(1, 0, 1);

        var marks = SketchMarks.Of(_sketch, Projection);

        Assert.Equal(On(1, 0, 1), Assert.Single(marks.OfType<DotMark>()).At);
        Assert.Empty(marks.OfType<LineMark>());
    }
}
