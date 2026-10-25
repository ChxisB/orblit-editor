namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

public sealed class PickingTests
{
    private static SceneObject ShapeAt(string id, Vector3 at, Shape? shape = null, Mesh? mesh = null) =>
        new(
            id,
            id,
            ObjectKind.Shape,
            position: at,
            shape: shape ?? (mesh is null ? Shape.Of(ShapeKind.Cube) : null),
            geometry: mesh);

    private static SceneObject Marker() =>
        new("m", "Marker", ObjectKind.Mesh, meshAsset: "models/thing.glb");

    // A ray straight down from high above a point.
    private static (Vector3 Origin, Vector3 Direction) FromAbove(double x, double z) =>
        (new Vector3(x, 50, z), new Vector3(0, -1, 0));

    [Fact]
    public void AShapeIsItsOwnSizeNotTwoMetres()
    {
        var scene = new EditorScene([ShapeAt("a", Vector3.Zero())]);
        var cube = Shape.Of(ShapeKind.Cube).Build().Bounds;
        // The built-in cube is a metre across, so its own bounds stop well short of the two-metre
        // box everything used to be picked as.
        Assert.True(cube.Max.X < 1);

        var inside = FromAbove(cube.Max.X - 0.05, 0);
        Assert.Equal("a", scene.ObjectAlong(inside.Origin, inside.Direction));

        // A click beside a small shape used to select it.
        var past = FromAbove(cube.Max.X + 0.3, 0);
        Assert.Null(scene.ObjectAlong(past.Origin, past.Direction));
    }

    [Fact]
    public void TheObjectBehindIsFoundThroughTheGapInAnL()
    {
        // An L-shaped room, and something standing in the notch of it. A box round the room covers
        // the notch. The room itself does not.
        var ell = new PolyShape(
            [
                new Vector3(-1, 0, -1),
                new Vector3(-1, 0, 1),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, -1),
            ],
            height: 1).Build();

        var scene = new EditorScene(
        [
            ShapeAt("room", Vector3.Zero(), mesh: ell),
            ShapeAt("behind", new Vector3(0.5, -3, 0.5)),
        ]);

        // Down through the notch, which is the quarter the room does not fill. This is the whole
        // difference between a box round a thing and the thing.
        var ray = FromAbove(0.5, 0.5);
        Assert.Equal("behind", scene.ObjectAlong(ray.Origin, ray.Direction));

        // And through a part the room does fill.
        var onto = FromAbove(-0.5, 0.5);
        Assert.Equal("room", scene.ObjectAlong(onto.Origin, onto.Direction));
    }

    [Fact]
    public void TheNearestIsStillTheOnePicked()
    {
        var scene = new EditorScene(
        [
            ShapeAt("low", new Vector3(0, 0, 0)),
            ShapeAt("high", new Vector3(0, 4, 0)),
        ]);
        var ray = FromAbove(0, 0);
        Assert.Equal("high", scene.ObjectAlong(ray.Origin, ray.Direction));
    }

    [Fact]
    public void AnObjectWithNoGeometryKeepsThePlaceholderBox()
    {
        var box = Marker().LocalBounds();
        Assert.Equal(-1, box.Min.X);
        Assert.Equal(1, box.Max.Y);
    }

    [Fact]
    public void AFileThatSaysHowBigItIsIsBelieved()
    {
        var marker = Marker();
        var told = marker.LocalBounds((Vector3.All(-0.05), Vector3.All(0.05)));
        Assert.Equal(0.05, told.Max.X);

        var scene = new EditorScene([marker]);
        // Ten centimetres away: inside the placeholder box, outside the thing.
        var ray = FromAbove(0.1, 0);
        Assert.Null(scene.ObjectAlong(ray.Origin, ray.Direction, _ => (Vector3.All(-0.05), Vector3.All(0.05))));

        // Told nothing, so back to the placeholder.
        Assert.Equal("m", scene.ObjectAlong(ray.Origin, ray.Direction, _ => null));
    }

    [Fact]
    public void AnObjectWithGeometryIgnoresWhatAFileSays()
    {
        // The geometry is the truth when there is any. A reported box is only for the objects whose
        // geometry the editor does not hold.
        var told = ShapeAt("a", Vector3.Zero()).LocalBounds((Vector3.All(-9), Vector3.All(9)));
        Assert.True(told.Max.X < 1);
    }

    [Fact]
    public void AHiddenObjectCannotBeClicked()
    {
        var scene = new EditorScene([ShapeAt("a", Vector3.Zero())]);
        scene["a"]!.Visible = false;
        var ray = FromAbove(0, 0);
        Assert.Null(scene.ObjectAlong(ray.Origin, ray.Direction));
    }

    [Fact]
    public void AScaledObjectIsPickedAtTheSizeItIsDrawn()
    {
        var squashed = ShapeAt("a", Vector3.Zero());
        squashed.Scale.SetValues(4, 1, 4);
        var scene = new EditorScene([squashed]);

        // Well outside a metre-wide cube, well inside one scaled by four.
        var ray = FromAbove(1.5, 0);
        Assert.Equal("a", scene.ObjectAlong(ray.Origin, ray.Direction));
    }

    [Fact]
    public void AnEmptyMeshFallsBackToTheBoxRatherThanNeverHitting()
    {
        var scene = new EditorScene([ShapeAt("a", Vector3.Zero(), mesh: new Mesh())]);
        var ray = FromAbove(0, 0);
        Assert.Equal("a", scene.ObjectAlong(ray.Origin, ray.Direction));
    }
}
