namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

public sealed class BoundaryTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    [Fact]
    public void AMeshBoundaryIsTheShapeGrownByThePadding()
    {
        var shell = new Boundary(padding: 0.1).MeshFrom(Cube())!;
        Assert.Equal(6, shell.FaceCount);
        Assert.Equal(Cube().Bounds.Max.X + 0.1, shell.Bounds.Max.X, 1e-9);
    }

    [Fact]
    public void ABoxBoundaryHasNoMeshToTestAgainst() =>
        Assert.Null(new Boundary(BoundaryKind.Box).MeshFrom(Cube()));

    [Fact]
    public void AMeshBoundaryWithNothingToFollowHasNoneEither()
    {
        Assert.Null(new Boundary().MeshFrom(null));
        Assert.Null(new Boundary().MeshFrom(new Mesh()));
    }

    [Fact]
    public void PaddingGrowsTheBoxInEveryDirection()
    {
        var natural = (Min: Vector3.All(-1), Max: Vector3.All(1));
        var box = new Boundary(padding: 0.25).BoxFrom(natural);
        Assert.Equal(-1.25, box.Min.X, 1e-9);
        Assert.Equal(1.25, box.Max.Z, 1e-9);
    }

    [Fact]
    public void AnOffsetMovesItWithoutResizingIt()
    {
        var natural = (Min: Vector3.All(-1), Max: Vector3.All(1));
        var box = new Boundary(offset: new Vector3(0, 2, 0)).BoxFrom(natural);
        Assert.Equal(1, box.Min.Y, 1e-9);
        Assert.Equal(3, box.Max.Y, 1e-9);
        Assert.Equal(2, box.Max.X - box.Min.X, 1e-9);
    }

    [Fact]
    public void AnOffsetMovesTheMeshToo()
    {
        var shell = new Boundary(offset: new Vector3(5, 0, 0)).MeshFrom(Cube())!;
        Assert.Equal(Cube().Bounds.Min.X + 5, shell.Bounds.Min.X, 1e-9);
    }

    [Fact]
    public void ABoundaryNobodyChangedWritesNothing() =>
        Assert.Empty(new Boundary().ToJson());

    [Fact]
    public void RubbishReadsBackAsNothing()
    {
        Assert.Null(Boundary.FromJson(null));
        Assert.Null(Boundary.FromJson("mesh"));
        // An empty map is a boundary nobody described, which is the default.
        Assert.Equal(BoundaryKind.Mesh, Boundary.FromJson(new Dictionary<string, object?>())!.Kind);
    }

    [Fact]
    public void AKindAndAnOffsetReadBackWhatWasWritten()
    {
        var written = new Boundary(BoundaryKind.Box, 0.15, new Vector3(0, 0.5, 0)).ToJson();
        var back = Boundary.FromJson(written)!;
        Assert.Equal(BoundaryKind.Box, back.Kind);
        Assert.Equal(0.15, back.Padding);
        Assert.Equal(0.5, back.Offset.Y);
    }

    [Fact]
    public void ItGoesWithTheObject()
    {
        var shape = ShapeWith(new Boundary(BoundaryKind.Box, padding: 0.15, offset: new Vector3(0, 0.5, 0)));
        var back = SceneDocument.Decode(SceneDocument.Encode(new EditorScene([shape]), "A")).Scene.Objects.Single();

        Assert.Equal(BoundaryKind.Box, back.Boundary.Kind);
        Assert.Equal(0.15, back.Boundary.Padding);
        Assert.Equal(0.5, back.Boundary.Offset.Y);
    }

    private static SceneObject ShapeWith(Boundary boundary, Mesh? mesh = null) =>
        new(
            "a",
            "A",
            ObjectKind.Shape,
            shape: mesh is null ? Shape.Of(ShapeKind.Cube) : null,
            geometry: mesh,
            boundary: boundary);

    private static (Vector3 Origin, Vector3 Direction) FromAbove(double x, double z) =>
        (new Vector3(x, 50, z), new Vector3(0, -1, 0));

    private static Mesh Ell() =>
        new PolyShape(
            [
                new Vector3(-1, 0, -1),
                new Vector3(-1, 0, 1),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, -1),
            ],
            height: 1).Build();

    [Fact]
    public void AShapeGetsTheMeshByDefaultBecauseABoxIsRightByLuck() =>
        Assert.Equal(BoundaryKind.Mesh, new SceneObject("a", "A", ObjectKind.Shape).Boundary.Kind);

    [Fact]
    public void PaddingMakesAShapeEasierToHit()
    {
        var tight = new EditorScene([ShapeWith(new Boundary())]);
        var loose = new EditorScene([ShapeWith(new Boundary(padding: 0.3))]);
        // Just outside the cube, and inside the padded one.
        var ray = FromAbove(0.65, 0);

        Assert.Null(tight.ObjectAlong(ray.Origin, ray.Direction));
        Assert.Equal("a", loose.ObjectAlong(ray.Origin, ray.Direction));
    }

    [Fact]
    public void ABoxBoundaryFillsTheNotchAMeshOneLeavesOpen()
    {
        var ray = FromAbove(0.5, 0.5);

        // The notch is a hole, and a mesh boundary knows it.
        Assert.Null(new EditorScene([ShapeWith(new Boundary(), Ell())]).ObjectAlong(ray.Origin, ray.Direction));

        // A box does not, and sometimes that is what somebody wants.
        var boxed = new EditorScene([ShapeWith(new Boundary(BoundaryKind.Box), Ell())]);
        Assert.Equal("a", boxed.ObjectAlong(ray.Origin, ray.Direction));
    }

    [Fact]
    public void NoneMeansNone()
    {
        // Decoration to walk through is decoration not to click.
        var scene = new EditorScene([ShapeWith(new Boundary(BoundaryKind.None))]);
        var ray = FromAbove(0, 0);
        Assert.Null(scene.ObjectAlong(ray.Origin, ray.Direction));
    }

    [Fact]
    public void AnOffsetBoundaryIsHitWhereItIsNotWhereTheShapeIs()
    {
        var shape = ShapeWith(new Boundary(BoundaryKind.Box));
        shape.Boundary = new Boundary(BoundaryKind.Box, offset: new Vector3(3, 0, 0));
        var scene = new EditorScene([shape]);

        var there = FromAbove(3, 0);
        Assert.Equal("a", scene.ObjectAlong(there.Origin, there.Direction));

        var here = FromAbove(0, 0);
        Assert.Null(scene.ObjectAlong(here.Origin, here.Direction));
    }

    [Fact]
    public void ACopyOfAnObjectGetsItsOwnOffset()
    {
        var shape = ShapeWith(new Boundary(offset: new Vector3(1, 0, 0)));
        var copy = shape.Copy();
        copy.Boundary.Offset.X = 9;
        Assert.Equal(1, shape.Boundary.Offset.X);
    }
}
