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
}
