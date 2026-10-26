namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// What the joint section decides about a joint, apart from the rows that show it.
public sealed class JointModelTests
{
    private static Doc.JointComponent Of(Doc.JointKind kind = Doc.JointKind.Hinge) => new(kind: kind);

    private static EditorScene Reopened(EditorScene scene) => SceneDocument.Decode(SceneDocument.Encode(scene)).Scene;

    [Fact]
    public void AJointComesBackAsItWasSaved()
    {
        var hinge = new Doc.JointComponent(
            limits: new Dictionary<Doc.JointAxis, Doc.JointRange> { [Doc.JointAxis.AboutX] = new(-10, 95) },
            speed: 30,
            strength: 5,
            breakingTorque: 400);

        var back = Reopened(JointTests.Scene(hinge: hinge));

        Assert.Equal(hinge.ToJson(), PhysicsComponents.JointOf(back["hinge"]!)!.ToJson());
        Assert.Null(PhysicsComponents.JointOf(back["door"]!));
    }

    [Fact]
    public void AJointKeepsItsSpringsGoalRatioPointsAndPath()
    {
        var machine = new Doc.JointComponent(
            kind: Doc.JointKind.Path,
            springs: new Dictionary<Doc.JointAxis, Doc.JointGive> { [Doc.JointAxis.AlongX] = new(4, Rest: 0.25) },
            softHertz: 6,
            goal: 0.5,
            goalHertz: 2,
            ratio: -2,
            axisB: new Vector3(0, 1, 0),
            over: [new Vector3(0, 2, 0), new Vector3(1, 2, 0)],
            path: [Vector3.Zero(), new Vector3(1, 0, 0), new Vector3(1, 1, 0)],
            follow: true);

        var back = Reopened(JointTests.Scene(hinge: machine));

        Assert.Equal(machine.ToJson(), PhysicsComponents.JointOf(back["hinge"]!)!.ToJson());
    }

    [Fact]
    public void EveryKindHasAName()
    {
        Assert.Equal(11, JointModel.Kinds.Count);
        Assert.Equal("Six axes", JointModel.LabelOf(Doc.JointKind.SixAxis));
        Assert.All(Enum.GetValues<Doc.JointKind>(), kind => Assert.NotEmpty(JointModel.LabelOf(kind)));
    }

    [Fact]
    public void APulleyAndAPathAreGivenPointsToStartFromAndOtherKindsAreLeftAlone()
    {
        var pulley = JointModel.Seeded(Of(Doc.JointKind.Pulley));
        Assert.Equal([new Vector3(0, 1, 0), new Vector3(0, 1, 1)], pulley.Over);

        var path = JointModel.Seeded(Of(Doc.JointKind.Path));
        Assert.Equal([Vector3.Zero(), new Vector3(1, 0, 0)], path.Path);

        var own = new Doc.JointComponent(kind: Doc.JointKind.Pulley, over: [new Vector3(5, 5, 5), new Vector3(6, 6, 6)]);
        Assert.Same(own, JointModel.Seeded(own));
        var hinge = Of();
        Assert.Same(hinge, JointModel.Seeded(hinge));
    }

    [Fact]
    public void ABodyUnderNothingIsHeldToTheWorldAndOneUnderAnotherToThatOther()
    {
        var scene = JointTests.Scene();

        Assert.Equal(("Door to the world", false), JointModel.Holds(scene, "hinge"));
        Assert.Equal(("Forearm to Arm", false), JointModel.Holds(scene, "forearm"));
    }

    [Fact]
    public void WithNoBodyAtOrAboveItAJointHoldsNothingAndSaysSo() =>
        Assert.Equal(("Nothing: no body at or above it", true), JointModel.Holds(JointTests.Scene(), "lamp"));

    [Fact]
    public void ARopeIsAsPlacedUntilItIsSetAndEveryOtherJointIsFreeUntilItIsLocked()
    {
        Assert.Equal(["Free", "Locked", "Range"], JointModel.Modes(Doc.JointKind.Hinge));
        Assert.Equal(["As placed", "Set", "Range"], JointModel.Modes(Doc.JointKind.Distance));
        Assert.Equal(0, JointModel.ModeOf(null));
        Assert.Equal(1, JointModel.ModeOf(Doc.JointRange.At(2)));
        Assert.Equal(2, JointModel.ModeOf(new Doc.JointRange(-1, 1)));
    }

    [Fact]
    public void ALimitIsLockedWhereItWasPlacedAndGivenARangeToStartFrom()
    {
        var hinge = Of();
        Assert.Equal(Doc.JointRange.At(0), JointModel.Moded(hinge, Doc.JointAxis.AboutX, 1, 2).Limits[Doc.JointAxis.AboutX]);
        Assert.Equal(new Doc.JointRange(-45, 45), JointModel.Moded(hinge, Doc.JointAxis.AboutX, 2, 2).Limits[Doc.JointAxis.AboutX]);

        var slider = Of(Doc.JointKind.Slider);
        Assert.Equal(new Doc.JointRange(-0.5, 0.5), JointModel.Moded(slider, Doc.JointAxis.AlongX, 2, 2).Limits[Doc.JointAxis.AlongX]);

        var rope = Of(Doc.JointKind.Distance);
        Assert.Equal(Doc.JointRange.At(2), JointModel.Moded(rope, Doc.JointAxis.AlongX, 1, 2).Limits[Doc.JointAxis.AlongX]);
        Assert.Equal(new Doc.JointRange(0, 2), JointModel.Moded(rope, Doc.JointAxis.AlongX, 2, 2).Limits[Doc.JointAxis.AlongX]);
    }

    [Fact]
    public void FreeingALimitTakesItOffAndKeepsTheOthers()
    {
        var both = Of(Doc.JointKind.SixAxis)
            .Limit(Doc.JointAxis.AlongX, Doc.JointRange.At(1))
            .Limit(Doc.JointAxis.AboutY, Doc.JointRange.At(2));

        var freed = JointModel.Moded(both, Doc.JointAxis.AlongX, 0, 1);

        Assert.Equal([Doc.JointAxis.AboutY], freed.Limits.Keys);
    }

    [Fact]
    public void ARangeDraggedPastItsOtherEndStopsThere()
    {
        var joint = Of().Limit(Doc.JointAxis.AboutX, new Doc.JointRange(-10, 20));

        Assert.Equal(new Doc.JointRange(-5, 20), JointModel.Ordered(joint, Doc.JointAxis.AboutX, [-5, 20]));
        // The low end moved past the high one.
        Assert.Equal(new Doc.JointRange(20, 20), JointModel.Ordered(joint, Doc.JointAxis.AboutX, [30, 20]));
        // The high end moved past the low one.
        Assert.Equal(new Doc.JointRange(-10, -10), JointModel.Ordered(joint, Doc.JointAxis.AboutX, [-10, -30]));
    }

    [Fact]
    public void ARopeStartsAsLongAsItWasPlacedToTheCentimetre()
    {
        // The hinge sits half a metre from the middle of the door it holds.
        Assert.Equal(0.5, JointModel.PlacedLength(JointTests.Scene(), "hinge"));
        // Nothing to measure to, and no distance to speak of: a metre.
        Assert.Equal(1, JointModel.PlacedLength(JointTests.Scene(), "lamp"));
        Assert.Equal(1, JointModel.PlacedLength(JointTests.Scene(), "forearm"));

        var near = new EditorScene(
        [
            new SceneObject("box", "Box", ObjectKind.Mesh, components: new() { [Doc.SceneComponents.Body] = new Doc.BodyComponent() }),
            new SceneObject("pin", "Pin", ObjectKind.Group, parentId: "box", position: new Vector3(0.123456, 0, 0)),
        ]);
        Assert.Equal(0.12, JointModel.PlacedLength(near, "pin"));
    }

    [Fact]
    public void ALimitIsNamedForWhatItDoesToThatKindOfJoint()
    {
        Assert.Equal("Turn", JointModel.LimitLabel(Doc.JointKind.Hinge, Doc.JointAxis.AboutX));
        Assert.Equal("Travel", JointModel.LimitLabel(Doc.JointKind.Slider, Doc.JointAxis.AlongX));
        Assert.Equal("Length", JointModel.LimitLabel(Doc.JointKind.Distance, Doc.JointAxis.AlongX));
        Assert.Equal("Twist", JointModel.LimitLabel(Doc.JointKind.Cone, Doc.JointAxis.AboutX));
        Assert.Equal("Along z", JointModel.LimitLabel(Doc.JointKind.SixAxis, Doc.JointAxis.AlongZ));
        Assert.Equal("About y", JointModel.LimitLabel(Doc.JointKind.SixAxis, Doc.JointAxis.AboutY));
    }

    [Fact]
    public void TheEndsAreSoftOnlyWhereALimitIsARange()
    {
        var locked = Of().Limit(Doc.JointAxis.AboutX, Doc.JointRange.At(0));
        Assert.False(JointModel.HasRange(Of()));
        Assert.False(JointModel.HasRange(locked));
        Assert.True(JointModel.HasRange(locked.Limit(Doc.JointAxis.AboutX, new Doc.JointRange(-1, 1))));
    }

    [Fact]
    public void ASpringIsOfferedOnTheWaysEachKindGivesIn()
    {
        Assert.Equal(6, JointModel.GiveAxes(Of(Doc.JointKind.Fixed)).Count);
        Assert.Equal([Doc.JointAxis.AboutX, Doc.JointAxis.AboutY, Doc.JointAxis.AboutZ], JointModel.GiveAxes(Of(Doc.JointKind.Point)));
        Assert.Equal([Doc.JointAxis.AlongX], JointModel.GiveAxes(Of(Doc.JointKind.Gear)));
        Assert.Equal(Of(Doc.JointKind.Hinge).Axes, JointModel.GiveAxes(Of(Doc.JointKind.Hinge)));
    }

    [Fact]
    public void ASpringIsOneWordWhereThereIsOneWayToGiveAndNamesTheWayWhereThereAreSeveral()
    {
        Assert.Equal("Spring", JointModel.SpringLabel(Of(), Doc.JointAxis.AboutX));
        Assert.Equal("Spring about y", JointModel.SpringLabel(Of(Doc.JointKind.Point), Doc.JointAxis.AboutY));
    }

    [Fact]
    public void OnlyAHingeASliderAndAPathCanBeDriven()
    {
        Assert.All([Doc.JointKind.Hinge, Doc.JointKind.Slider, Doc.JointKind.Path], kind => Assert.True(JointModel.IsDriven(kind)));
        Assert.All([Doc.JointKind.Fixed, Doc.JointKind.Cone, Doc.JointKind.Gear], kind => Assert.False(JointModel.IsDriven(kind)));
    }

    [Fact]
    public void AMotorToAPlaceIsGivenAStrengthToHoldItAndKeepsOneItHas()
    {
        var place = JointModel.Driven(Of(), toPlace: true);
        Assert.Equal(JointModel.FirstHertz, place.GoalHertz);
        Assert.Equal(JointModel.FirstStrength, place.Strength);

        var strong = JointModel.Driven(new Doc.JointComponent(strength: 9), toPlace: true);
        Assert.Equal(9, strong.Strength);

        Assert.Equal(0, JointModel.Driven(place, toPlace: false).GoalHertz);
    }

    [Fact]
    public void APathCarriesOnPastItsLastPoint()
    {
        Assert.Equal(Vector3.Zero(), JointModel.Beyond([]));
        Assert.Equal(new Vector3(3, 1, 1), JointModel.Beyond([new Vector3(2, 1, 1)]));
        Assert.Equal(new Vector3(2, 0, 0), JointModel.Beyond([Vector3.Zero(), new Vector3(1, 0, 0)]));
    }

    [Fact]
    public void APointIsReadAndSetWhereThereIsNoneYet()
    {
        IReadOnlyList<Vector3> points = [new Vector3(1, 2, 3)];

        Assert.Equal([1.0, 2.0, 3.0], JointModel.PointOf(points, 0));
        Assert.Equal([0.0, 0.0, 0.0], JointModel.PointOf(points, 1));

        // Padded with the origin up to the count asked for, which is how a pulley gets its second point.
        Assert.Equal([new Vector3(1, 2, 3), Vector3.Zero(), new Vector3(7, 8, 9)], JointModel.WithPoint(points, 2, [7, 8, 9], take: 3));
        Assert.Equal([new Vector3(1, 2, 3), new Vector3(7, 8, 9)], JointModel.WithPoint(points, 1, [7, 8, 9], take: 2));
        Assert.Equal([new Vector3(4, 5, 6)], JointModel.WithPoint(points, 0, [4, 5, 6]));
    }
}
