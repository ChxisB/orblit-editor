namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// A joint on an object: holding the bodies its place in the tree says it holds, and drawn with its
// frame and limits.
public sealed class JointTests
{
    private static readonly ViewportProjection Projection = GizmoView.Projection;

    // A door on a hinge at its edge, an arm with a forearm under it, and a lamp with nothing to hold.
    internal static EditorScene Scene(Doc.JointComponent? hinge = null, Doc.JointComponent? elbow = null)
    {
        var hingeParts = new OrderedDictionary<string, Doc.SceneComponent>();
        if (hinge is not null)
        {
            hingeParts[Doc.SceneComponents.Joint] = hinge;
        }

        var forearmParts = new OrderedDictionary<string, Doc.SceneComponent>
        {
            [Doc.SceneComponents.Body] = new Doc.BodyComponent(),
        };
        if (elbow is not null)
        {
            forearmParts[Doc.SceneComponents.Joint] = elbow;
        }

        return new EditorScene(
        [
            new SceneObject(
                "door",
                "Door",
                ObjectKind.Mesh,
                position: new Vector3(0, 1, 0),
                components: new() { [Doc.SceneComponents.Body] = new Doc.BodyComponent() }),
            new SceneObject(
                "hinge",
                "Hinge",
                ObjectKind.Group,
                parentId: "door",
                position: new Vector3(-0.5, 0, 0),
                components: hingeParts),
            new SceneObject(
                "arm",
                "Arm",
                ObjectKind.Mesh,
                position: new Vector3(3, 1, 0),
                components: new() { [Doc.SceneComponents.Body] = new Doc.BodyComponent() }),
            new SceneObject(
                "forearm",
                "Forearm",
                ObjectKind.Mesh,
                parentId: "arm",
                position: new Vector3(0, -1, 0),
                components: forearmParts),
            new SceneObject("lamp", "Lamp", ObjectKind.Group),
        ]);
    }

    // Where the joint's lines land on screen.
    private static Box Drawn(EditorScene scene, string id = "hinge") =>
        JointGizmo.Type().Overlay!(GizmoView.TargetOf(scene, id)).Single().Lines.Bounds!.Value;

    private static Offset Screen(double x, double y, double z) => Projection.Project(new Vector3(x, y, z))!.Value;

    [Fact]
    public void UnderABodyThatBodyToTheWorld() =>
        Assert.Equal<(string?, string?)>(("door", null), PhysicsComponents.JointEndsIn(Scene(), "hinge"));

    [Fact]
    public void OnABodyUnderAnotherTheOneToTheOther() =>
        Assert.Equal<(string?, string?)>(("forearm", "arm"), PhysicsComponents.JointEndsIn(Scene(), "forearm"));

    [Fact]
    public void NothingWithNoBodyAtOrAboveIt() =>
        Assert.Equal<(string?, string?)>((null, null), PhysicsComponents.JointEndsIn(Scene(), "lamp"));

    [Fact]
    public void ABodyMiddleIsWhereItsBodyIsDrawn()
    {
        Assert.Equal(new Vector3(3, 0, 0), PhysicsComponents.BodyMiddleIn(Scene(), "forearm"));
        Assert.Null(PhysicsComponents.BodyMiddleIn(Scene(), "lamp"));
    }

    [Fact]
    public void IsThereForASelectionWithAJointAndOnlyThen()
    {
        var door = Scene(hinge: new Doc.JointComponent());
        var gizmo = JointGizmo.Type();

        Assert.False(gizmo.AppliesTo(GizmoView.TargetOf(door, "door")));
        Assert.True(gizmo.AppliesTo(GizmoView.TargetOf(door, "hinge")));
        Assert.True(gizmo.AppliesTo(GizmoView.TargetOf(door, "door", "hinge")));
    }

    [Fact]
    public void JoinsTheJointToTheBodyItHolds()
    {
        var bounds = Drawn(Scene(hinge: new Doc.JointComponent()));

        Assert.True(bounds.Inflate(1).Contains(Screen(0, 1, 0)));
    }

    [Fact]
    public void JoinsAForearmToTheArmItHangsFrom()
    {
        var bounds = Drawn(Scene(elbow: new Doc.JointComponent(kind: Doc.JointKind.Point)), "forearm");

        Assert.True(bounds.Inflate(1).Contains(Screen(3, 1, 0)));
    }

    [Fact]
    public void ALimitedHingeDrawsTheArcItMayTurnThrough()
    {
        // A hinge at the door's edge, turning about its x axis: free, a whole ring round it; limited
        // to a few degrees up from y, a sliver.
        var free = Drawn(Scene(hinge: new Doc.JointComponent()));
        var limited = Drawn(Scene(hinge: new Doc.JointComponent(
            limits: new Dictionary<Doc.JointAxis, Doc.JointRange> { [Doc.JointAxis.AboutX] = new(0, 10) })));

        Assert.True(free.Bottom > limited.Bottom + 1);
    }

    [Fact]
    public void AConeOpensWithItsSwing()
    {
        Box ConeOf(double swing) =>
            Drawn(Scene(hinge: new Doc.JointComponent(kind: Doc.JointKind.Cone, swing: swing)));

        Assert.True(ConeOf(60).Height > ConeOf(15).Height);
    }

    [Fact]
    public void ASlidersTravelIsMetresInTheWorld()
    {
        Box SliderOf(double reach) => Drawn(Scene(hinge: new Doc.JointComponent(
            kind: Doc.JointKind.Slider,
            limits: new Dictionary<Doc.JointAxis, Doc.JointRange> { [Doc.JointAxis.AlongX] = new(-reach, reach) })));

        var shortTravel = SliderOf(0.5);
        var longTravel = SliderOf(3);

        Assert.True(longTravel.Width > shortTravel.Width);
        Assert.True(longTravel.Inflate(1).Contains(Screen(-3.5, 1, 0)));
    }

    [Fact]
    public void APathIsDrawnWhereItsPointsAre()
    {
        var bounds = Drawn(Scene(hinge: new Doc.JointComponent(
            kind: Doc.JointKind.Path,
            path: [Vector3.Zero(), new Vector3(0, 3, 0), new Vector3(0, 3, 4)])));

        Assert.True(bounds.Inflate(1).Contains(Screen(-0.5, 4, 4)));
    }

    [Fact]
    public void APulleyIsDrawnOverItsTwoPoints()
    {
        var bounds = Drawn(Scene(hinge: new Doc.JointComponent(
            kind: Doc.JointKind.Pulley,
            over: [new Vector3(0, 5, 0), new Vector3(0, 5, 3)])));

        Assert.True(bounds.Inflate(1).Contains(Screen(-0.5, 6, 3)));
    }

    [Fact]
    public void APulleyWithTheWrongNumberOfPointsDrawsNoRope()
    {
        Box PulleyOf(params Vector3[] over) =>
            Drawn(Scene(hinge: new Doc.JointComponent(kind: Doc.JointKind.Pulley, over: over)));

        var two = PulleyOf(new Vector3(0, 5, 0), new Vector3(0, 5, 3));
        var three = PulleyOf(new Vector3(0, 5, 0), new Vector3(0, 5, 3), new Vector3(0, 5, 6));

        Assert.True(three.Height < two.Height);
    }
}
