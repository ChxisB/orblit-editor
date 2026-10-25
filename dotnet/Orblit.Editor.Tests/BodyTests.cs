namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// A physics body on an object, drawn as the simulation will see it.
public sealed class BodyTests
{
    private static SceneObject Crate(Doc.BodyComponent? body = null, Vector3? scale = null, Vector3? rotation = null)
    {
        var parts = new OrderedDictionary<string, Doc.SceneComponent>();
        if (body is not null)
        {
            parts[Doc.SceneComponents.Body] = body;
        }

        return new SceneObject("crate", "Crate", ObjectKind.Mesh, scale: scale, rotation: rotation, components: parts);
    }

    private static double[] Cube(double half) =>
    [
        .. from x in new[] { -half, half }
           from y in new[] { -half, half }
           from z in new[] { -half, half }
           from value in new[] { x, y, z }
           select value,
    ];

    // Where the wireframe lands on screen, or null when it draws nothing.
    private static Box? DrawnOrNothing(params SceneObject[] objects)
    {
        var scene = new EditorScene([.. objects]);
        return BodyGizmo.Type().Overlay!(GizmoView.TargetOf(scene, "crate"))[0].Lines.Bounds;
    }

    private static Box Drawn(SceneObject crate) => DrawnOrNothing(crate)!.Value;

    private static void ExpectSame(Box a, Box b)
    {
        Assert.Equal(b.Left, a.Left, 1e-6);
        Assert.Equal(b.Top, a.Top, 1e-6);
        Assert.Equal(b.Right, a.Right, 1e-6);
        Assert.Equal(b.Bottom, a.Bottom, 1e-6);
    }

    [Fact]
    public void IsThereForASelectionWithABodyAndOnlyThen()
    {
        var scene = new EditorScene([Crate(new Doc.BodyComponent()), new SceneObject("rock", "Rock", ObjectKind.Mesh)]);
        var gizmo = BodyGizmo.Type();

        Assert.False(gizmo.AppliesTo(GizmoView.TargetOf(scene, "rock")));
        Assert.True(gizmo.AppliesTo(GizmoView.TargetOf(scene, "crate")));
        // Any of the selection, not only the one the handles are on.
        Assert.True(gizmo.AppliesTo(GizmoView.TargetOf(scene, "rock", "crate")));
    }

    [Fact]
    public void ABoxScalesWithItsObject()
    {
        var scaled = Drawn(Crate(new Doc.BodyComponent(), scale: Vector3.All(2)));
        var big = Drawn(Crate(new Doc.BodyComponent(size: Vector3.All(2))));
        var small = Drawn(Crate(new Doc.BodyComponent()));

        ExpectSame(scaled, big);
        Assert.True(small.Width < big.Width);
    }

    [Fact]
    public void ABoxTurnsTheWayItsObjectTurns()
    {
        // Partway round and tipped: a quarter turn draws a centred box the same whichever way it
        // went, so it would not catch a turn the wrong way.
        var crate = Crate(new Doc.BodyComponent(size: new Vector3(3, 1, 1)), rotation: new Vector3(20, 40, 0));
        var world = new EditorScene([crate]).WorldOf("crate");
        var corners = (
            from x in new[] { -1.5, 1.5 }
            from y in new[] { -0.5, 0.5 }
            from z in new[] { -0.5, 0.5 }
            select GizmoView.Projection.Project(world.Transformed3(new Vector3(x, y, z)))!.Value).ToList();

        var box = Drawn(crate);

        // Where the object's own matrix puts the corners, as a model is drawn.
        Assert.Equal(corners.Min(c => c.Dx), box.Left, 1e-3);
        Assert.Equal(corners.Max(c => c.Dx), box.Right, 1e-3);
        Assert.Equal(corners.Min(c => c.Dy), box.Top, 1e-3);
        Assert.Equal(corners.Max(c => c.Dy), box.Bottom, 1e-3);
    }

    [Fact]
    public void ACompoundGizmoShowsItsChildCentresAndExactScaling()
    {
        var compound = Drawn(Crate(
            new Doc.BodyComponent(
                shape: Doc.BodyShape.Compound,
                parts:
                [
                    new Doc.BodyPart(size: Vector3.All(1), centre: new Vector3(2, 0, 0), scale: new Vector3(2, 1, 0.5)),
                ]),
            scale: new Vector3(1, 2, 1)));
        var box = Drawn(Crate(
            new Doc.BodyComponent(size: new Vector3(2, 1, 0.5), centre: new Vector3(2, 0, 0)),
            scale: new Vector3(1, 2, 1)));

        ExpectSame(compound, box);
    }

    [Fact]
    public void AShapeScaleDrawsTheSameEllipsoidAsASingleScaledPart()
    {
        var scaled = Drawn(Crate(new Doc.BodyComponent(shape: Doc.BodyShape.Sphere, shapeScale: new Vector3(2, 1, 0.5))));
        var part = Drawn(Crate(new Doc.BodyComponent(
            shape: Doc.BodyShape.Compound,
            parts: [new Doc.BodyPart(shape: Doc.BodyShape.Sphere, scale: new Vector3(2, 1, 0.5))])));

        ExpectSame(scaled, part);
    }

    [Fact]
    public void ABallTakesTheLargestOfItsScales()
    {
        // The simulation cannot stretch a ball, so neither does the drawing: it is the ball the
        // physics will use, not an egg nobody simulates.
        var stretched = Drawn(Crate(new Doc.BodyComponent(shape: Doc.BodyShape.Sphere), scale: new Vector3(1, 3, 1)));
        var round = Drawn(Crate(new Doc.BodyComponent(shape: Doc.BodyShape.Sphere, radius: 1.5)));

        ExpectSame(stretched, round);
    }

    [Fact]
    public void AShortCapsuleIsDrawnAsTheBallItIsSimulatedAs()
    {
        var capsule = Drawn(Crate(new Doc.BodyComponent(shape: Doc.BodyShape.Capsule, height: 0.8)));
        var ball = Drawn(Crate(new Doc.BodyComponent(shape: Doc.BodyShape.Sphere)));

        ExpectSame(capsule, ball);
    }

    [Fact]
    public void ACylinderTakesItsSidewaysAndUprightScalesApart()
    {
        var same = Drawn(Crate(new Doc.BodyComponent(shape: Doc.BodyShape.Cylinder, radius: 1, height: 3)));

        // The larger of x and z for the radius and y for the height, which is what the simulation
        // reads. Either sideways scale can be the larger.
        foreach (var scale in new[] { new Vector3(2, 3, 1), new Vector3(1, 3, 2) })
        {
            var stretched = Drawn(Crate(
                new Doc.BodyComponent(shape: Doc.BodyShape.Cylinder, radius: 0.5, height: 1),
                scale: scale));

            ExpectSame(stretched, same);
        }
    }

    [Fact]
    public void AHullOfACubeIsDrawnAsTheBoxItIsTheShapeOf()
    {
        var hull = Drawn(Crate(new Doc.BodyComponent(shape: Doc.BodyShape.Hull, hull: Cube(0.5))));
        var box = Drawn(Crate(new Doc.BodyComponent()));

        ExpectSame(hull, box);
    }

    [Fact]
    public void AHullIsStretchedByEachOfItsScales()
    {
        var stretched = Drawn(Crate(
            new Doc.BodyComponent(shape: Doc.BodyShape.Hull, hull: Cube(1)),
            scale: new Vector3(1, 2, 3)));
        var box = Drawn(Crate(new Doc.BodyComponent(size: new Vector3(2, 4, 6))));

        ExpectSame(stretched, box);
    }

    [Fact]
    public void AHullWithNoVolumeIsDrawnAsNothing()
    {
        // Four points in one plane: the world has no body for it either.
        var flat = DrawnOrNothing(Crate(new Doc.BodyComponent(
            shape: Doc.BodyShape.Hull,
            hull: [0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 1])));

        Assert.Null(flat);
    }
}
