namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;
using MeshEdge = (int, int);

public sealed class MeshPickerTests
{
    private static readonly Size Screen = new(800, 600);

    // The editor's starting view: in front, and a little above.
    private static OrbitCamera Start() => new(0, 0.3, 6);

    private static ViewportProjection ProjectionOf(OrbitCamera? camera = null) => new(camera ?? Start(), Screen);

    private static MeshPicker PickerFor(Mesh mesh, Matrix4? at = null, OrbitCamera? camera = null) =>
        new(mesh, at ?? Matrix4.Identity(), ProjectionOf(camera));

    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    [Fact]
    public void AClickInTheMiddleHitsTheNearFace()
    {
        var mesh = Cube();

        var hit = PickerFor(mesh).FaceAt(new Offset(400, 300));

        // Nearest wins: a ray through a box crosses two faces and the first reached was clicked.
        Assert.NotNull(hit);
        Assert.True(mesh.NormalOf(mesh.Faces[hit.Value]).Z > 0.5);
    }

    [Fact]
    public void AClickOnNothingHitsNothing() =>
        Assert.Null(PickerFor(Cube()).FaceAt(new Offset(5, 5)));

    [Fact]
    public void WhereTheObjectIsMovedSoIsWhatIsClickable()
    {
        var moved = PickerFor(Cube(), Matrix4.TranslationValues(40, 0, 0));

        Assert.Null(moved.FaceAt(new Offset(400, 300)));
    }

    [Fact]
    public void AFaceIsPickableFromInsideAsWellAsOutside()
    {
        var room = (Shape.Of(ShapeKind.Cube) with { Width = 20, Depth = 20, Height = 20 }).Build();
        var picker = PickerFor(room, Matrix4.TranslationValues(0, -10, 0), new OrbitCamera(0, 0, 1));

        // Culling back faces would leave nothing selectable from inside a room.
        Assert.NotNull(picker.FaceAt(new Offset(400, 300)));
    }

    [Fact]
    public void TheNearestVertexWithinReachIsFound()
    {
        var mesh = Cube();
        var corner = ProjectionOf().Project(mesh.Positions[2])!.Value;

        Assert.Equal(2, PickerFor(mesh).VertexAt(corner));
    }

    [Fact]
    public void NoVertexWhenTheClickIsNowhereNear() =>
        Assert.Null(PickerFor(Cube()).VertexAt(new Offset(10, 10)));

    [Fact]
    public void ACornerWithNoAreaIsStillClickable()
    {
        // A ray through a corner misses every triangle. Clicking near one means the nearest.
        var mesh = Cube();
        var corner = ProjectionOf().Project(mesh.Positions[6])!.Value;

        Assert.Equal(6, PickerFor(mesh).VertexAt(corner + new Offset(4, 4)));
    }

    [Fact]
    public void TheNearestEdgeWithinReachIsFound()
    {
        var mesh = Cube();
        var projection = ProjectionOf();
        var a = projection.Project(mesh.Positions[2])!.Value;
        var b = projection.Project(mesh.Positions[3])!.Value;

        var found = PickerFor(mesh).EdgeAt((a + b) / 2);

        Assert.NotNull(found);
        Assert.Equal(new HashSet<int> { 2, 3 }, new HashSet<int> { found.Value.Item1, found.Value.Item2 });
    }

    [Fact]
    public void NoEdgeFarFromAnyOfThem() =>
        Assert.Null(PickerFor(Cube()).EdgeAt(new Offset(20, 20)));

    [Fact]
    public void ABoxRoundTheWholeShapeTakesEveryFaceVertexAndEdgeItCanSee()
    {
        var mesh = Cube();
        var picker = PickerFor(mesh);
        var all = new Rect(0, 0, 800, 600);

        // The far side is behind the near side from here, so it is left behind.
        Assert.True(picker.FacesIn(all).Count < mesh.Faces.Count);
        Assert.True(picker.VerticesIn(all).Count < mesh.Positions.Count);
        Assert.NotEmpty(picker.EdgesIn(all));
    }

    [Fact]
    public void SeeingThroughTakesTheFarSideToo()
    {
        var mesh = Cube();
        var picker = new MeshPicker(mesh, Matrix4.Identity(), ProjectionOf(), seeThrough: true);
        var all = new Rect(0, 0, 800, 600);

        Assert.Equal(mesh.Faces.Count, picker.FacesIn(all).Count);
        Assert.Equal(mesh.Positions.Count, picker.VerticesIn(all).Count);
    }

    [Fact]
    public void AnEdgeIsTakenWholeOrNotAtAll()
    {
        var mesh = Cube();
        var projection = ProjectionOf();
        var a = projection.Project(mesh.Positions[2])!.Value;
        var b = projection.Project(mesh.Positions[3])!.Value;
        var picker = new MeshPicker(mesh, Matrix4.Identity(), projection, seeThrough: true);

        var around = new Rect(
            Math.Min(a.Dx, b.Dx) - 1, Math.Min(a.Dy, b.Dy) - 1, Math.Max(a.Dx, b.Dx) + 1, Math.Max(a.Dy, b.Dy) + 1);
        var half = new Rect(around.Left, around.Top, (a.Dx + b.Dx) / 2, around.Bottom);

        Assert.Contains(MeshActions.EdgeOf(2, 3), picker.EdgesIn(around));
        Assert.DoesNotContain(MeshActions.EdgeOf(2, 3), picker.EdgesIn(half));
    }

    [Fact]
    public void AFaceIsTakenByItsMiddleNotByHowMuchOfItIsInside()
    {
        var mesh = Cube();
        var projection = ProjectionOf();
        var picker = new MeshPicker(mesh, Matrix4.Identity(), projection, seeThrough: true);
        var face = mesh.Faces.FindIndex(f => mesh.NormalOf(f).Z > 0.9);
        var middle = projection.Project(mesh.CentreOf(mesh.Faces[face]))!.Value;

        var over = new Rect(middle.Dx - 2, middle.Dy - 2, middle.Dx + 2, middle.Dy + 2);

        Assert.Contains(face, picker.FacesIn(over));
    }

    [Fact]
    public void ARectContainsItsTopLeftAndNotItsFarEdges()
    {
        var box = new Rect(10, 20, 30, 40);

        Assert.True(box.Contains(new Offset(10, 20)));
        Assert.False(box.Contains(new Offset(30, 30)));
        Assert.False(box.Contains(new Offset(20, 40)));
    }
}
