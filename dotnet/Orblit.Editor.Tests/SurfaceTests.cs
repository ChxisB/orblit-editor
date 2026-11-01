namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

public sealed class SurfaceTests
{
    [Fact]
    public void AMaterialWithNothingSaidAboutItIsAPlainGreyOne()
    {
        var surface = new Surface();
        Assert.Equal(0, surface.Metallic);
        Assert.True(surface.Roughness > 0);
        Assert.False(surface.DoubleSided);
    }

    [Fact]
    public void TheColourIsConvertedOnTheWayToTheFile()
    {
        var glb = new Surface { Colour = new Colour(0xFF808080) }.ToGlb();
        // Mid grey in sRGB is a good deal darker than a half in linear, which is the whole reason
        // the conversion is not skipped.
        Assert.True(glb.Colour[0] < 0.3);
        Assert.True(glb.Colour[0] > 0.2);
        Assert.Equal(1.0, glb.Colour[3]);
    }

    [Fact]
    public void EmissiveIsTheColourScaled()
    {
        var dark = new Surface { Colour = new Colour(0xFFFFFFFF) };
        Assert.Equal([0.0, 0.0, 0.0], dark.ToGlb().Emissive);
        var lit = new Surface { Colour = new Colour(0xFFFFFFFF), Emissive = 2 };
        Assert.Equal(2, lit.ToGlb().Emissive[0], 1e-9);
    }

    [Fact]
    public void ItSurvivesBeingWrittenAndRead()
    {
        var surface = new Surface
        {
            Name = "Brass",
            Colour = new Colour(0xFFB5873A),
            Metallic = 1,
            Roughness = 0.25,
            Emissive = 0.5,
            DoubleSided = true,
        };
        var back = Surface.FromJson(surface.ToJson())!;
        Assert.Equal("Brass", back.Name);
        Assert.Equal(surface.Colour.Argb, back.Colour.Argb);
        Assert.Equal(1, back.Metallic);
        Assert.Equal(0.25, back.Roughness);
        Assert.Equal(0.5, back.Emissive);
        Assert.True(back.DoubleSided);
    }

    [Fact]
    public void ADefaultedMaterialWritesAlmostNothing() =>
        // A scene full of shapes would otherwise carry six numbers a slot that all say what the
        // default already says.
        Assert.Equal(["name", "colour"], new Surface().ToJson().Keys);

    [Fact]
    public void RubbishReadsBackAsNothingRatherThanAsADefault()
    {
        Assert.Null(Surface.FromJson(null));
        Assert.Null(Surface.FromJson("brass"));
        Assert.NotNull(Surface.FromJson(new Dictionary<string, object?>()));
    }

    [Fact]
    public void AnOutlineAndAMeshCanBothBeThereAndTheMeshWins()
    {
        var shape = new SceneObject(
            "a",
            "Room",
            ObjectKind.Shape,
            outline: new PolyShape([new Vector3(0, 0, 0), new Vector3(0, 0, 2), new Vector3(2, 0, 2)], height: 1),
            geometry: Shape.Of(ShapeKind.Cube).Build());

        // Because an outline cannot describe a face that has been extruded, and the corners are
        // kept only so undoing back to them is possible.
        Assert.Equal(6, shape.CurrentMesh!.FaceCount);
        Assert.NotNull(shape.Outline);
    }

    [Fact]
    public void ACopyOfAnObjectGetsItsOwnListOfSlots()
    {
        var shape = new SceneObject(
            "a",
            "Steps",
            ObjectKind.Shape,
            surfaces: [new Surface { Name = "Stone" }]);
        var copy = shape.Copy();
        copy.Surfaces.Add(new Surface { Name = "Added" });

        // A shared list would grow both.
        Assert.Single(shape.Surfaces);
        Assert.Equal(2, copy.Surfaces.Count);
    }

    [Fact]
    public void SlotsGoWithTheObjectThroughTheSceneFile()
    {
        var steps = new SceneObject(
            "a",
            "Steps",
            ObjectKind.Shape,
            shape: Shape.Of(ShapeKind.Stairs),
            surfaces: [new Surface { Name = "Stone" }, new Surface { Name = "Brass", Metallic = 1 }]);

        var read = SceneDocument.Decode(SceneDocument.Encode(new EditorScene([steps]), "A")).Scene.Objects.Single();

        Assert.Equal(2, read.Surfaces.Count);
        Assert.Equal("Brass", read.Surfaces[1].Name);
        Assert.Equal(1, read.Surfaces[1].Metallic);
    }

    [Fact]
    public void ADrawnOutlineGoesWithTheObjectThroughTheSceneFile()
    {
        var room = new SceneObject(
            "a",
            "Room",
            ObjectKind.Shape,
            outline: new PolyShape(
                [new Vector3(0, 0, 0), new Vector3(0, 0, 2), new Vector3(2, 0, 2)],
                height: 1.5,
                flipped: true));

        var read = SceneDocument.Decode(SceneDocument.Encode(new EditorScene([room]), "A")).Scene.Objects.Single();

        Assert.NotNull(read.Outline);
        Assert.Equal(3, read.Outline.Points.Count);
        Assert.Equal(1.5, read.Outline.Height);
        Assert.True(read.Outline.Flipped);
        // And it is still the shape, not just a note about how it was made.
        Assert.Equal(5, read.CurrentMesh!.FaceCount);
    }
}
