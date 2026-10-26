namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Panels;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// The parts of a compound body and a model collider, apart from the fields that show them.
public sealed class BodyPartsTests
{
    private static Doc.BodyComponent Compound(int parts) =>
        new(shape: Doc.BodyShape.Compound, parts: [.. Enumerable.Range(0, parts).Select(index => new Doc.BodyPart(radius: index + 1))]);

    [Fact]
    public void ThePartShapesAreLaidOutThreeToARowAndOtherShapesReadAsABox()
    {
        Assert.Equal([["Box", "Ball", "Capsule"], ["Cylinder", "Hull"]], BodyPartsModel.ShapeRows());
        Assert.Equal("Hull", BodyPartsModel.LabelOf(new Doc.BodyPart(shape: Doc.BodyShape.Hull)));
        Assert.Equal("Box", BodyPartsModel.LabelOf(new Doc.BodyPart(shape: Doc.BodyShape.Plane)));
    }

    [Fact]
    public void AHullWithNoPointsStartsAsTheBodysAndOneWithPointsKeepsThem()
    {
        double[] fitted = [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1];
        double[] own = [0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2];

        Assert.Equal(fitted, BodyPartsModel.Reshaped(new Doc.BodyPart(), Doc.BodyShape.Hull, fitted).Hull);
        Assert.Equal(own, BodyPartsModel.Reshaped(new Doc.BodyPart(hull: own), Doc.BodyShape.Hull, fitted).Hull);
        Assert.Empty(BodyPartsModel.Reshaped(new Doc.BodyPart(), Doc.BodyShape.Sphere, fitted).Hull);
    }

    [Fact]
    public void APartIsAddedRemovedAndEditedWithoutTouchingTheOthers()
    {
        var two = BodyPartsModel.Added(Compound(1));
        Assert.Equal(2, two.Parts.Count);
        Assert.Equal(0.5, two.Parts[1].Radius);

        var edited = BodyPartsModel.Edited(Compound(3), 1, part => part.CopyWith(radius: 9));
        Assert.Equal([1.0, 9.0, 3.0], edited.Parts.Select(part => part.Radius));

        var removed = BodyPartsModel.Removed(Compound(3), 0);
        Assert.Equal([2.0, 3.0], removed.Parts.Select(part => part.Radius));
    }

    [Fact]
    public void ATurnReadsBackAsTheDegreesItWasMadeFrom()
    {
        double[] degrees = [30, -40, 75];

        var back = BodyPartsModel.Angles(BodyPartsModel.Rotation(degrees));

        Assert.All(degrees.Zip(back), pair => Assert.Equal(pair.First, pair.Second, 6));
        Assert.Equal([0.0, 0.0, 0.0], BodyPartsModel.Angles(Quaternion.Identity()));
    }

    [Fact]
    public void ATurnOfNinetyDegreesAboutYStaysInRange() =>
        Assert.Equal(90, BodyPartsModel.Angles(BodyPartsModel.Rotation([0, 90, 0]))[1], 6);

    [Fact]
    public void AColliderIsAFixedSurfaceOfTheModelsTrianglesInItsOwnFrame()
    {
        var geometry = new CollisionMesh([0, 0, 0, 1, 0, 0, 0, 1, 0], [0, 1, 2]);
        var before = new Doc.BodyComponent(
            shape: Doc.BodyShape.Sphere,
            motion: Doc.BodyMotion.Free,
            mass: 4,
            centre: new Vector3(1, 2, 3),
            shapeScale: Vector3.All(3));

        var collider = BodyPartsModel.Collider(before, geometry);

        Assert.Equal(Doc.BodyShape.Mesh, collider.Shape);
        Assert.Equal(Doc.BodyMotion.Fixed, collider.Motion);
        Assert.Equal(geometry.Vertices, collider.MeshVertices);
        Assert.Equal([0L, 1L, 2L], collider.MeshIndices);
        Assert.Equal(Vector3.Zero(), collider.Centre);
        Assert.Equal(Vector3.All(1), collider.ShapeScale);
        // What the model does not change is kept.
        Assert.Equal(4, collider.Mass);
    }

    [Fact]
    public void AColliderOnNothingStartsFromANewBody() =>
        Assert.Equal(Doc.BodyShape.Mesh, BodyPartsModel.Collider(null, new CollisionMesh([0, 0, 0, 1, 0, 0, 0, 1, 0], [0, 1, 2])).Shape);
}
