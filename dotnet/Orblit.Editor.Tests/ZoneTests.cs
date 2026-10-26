namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// A trigger and a zone on an object: carried through the editor, told apart from a solid body, and
// edited as one thing each.
public sealed class ZoneTests
{
    // A fixed box that is a trigger or has a zone, the way a place is laid out.
    private static SceneObject Pool(Doc.BodyComponent? body = null, Doc.ZoneComponent? zone = null)
    {
        var parts = new OrderedDictionary<string, Doc.SceneComponent>
        {
            [Doc.SceneComponents.Body] = body ?? new Doc.BodyComponent(motion: Doc.BodyMotion.Fixed),
        };
        if (zone is not null)
        {
            parts[Doc.SceneComponents.Zone] = zone;
        }

        return new SceneObject("pool", "Pool", ObjectKind.Group, components: parts);
    }

    private static EditorScene Reopened(EditorScene scene) => SceneDocument.Decode(SceneDocument.Encode(scene)).Scene;

    private static SetObjectComponent PutZone(EditorScene scene, Doc.ZoneComponent? to, string label = "Set Pool zone") =>
        new("a", "pool", label, Doc.SceneComponents.Zone, PhysicsComponents.ZoneOf(scene["pool"]!), to);

    private static (EditorScene Scene, History History) Editing()
    {
        var scene = new EditorScene([Pool()]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        return (scene, new History(workspace));
    }

    [Fact]
    public void ATriggerItsStayAndItsBeltComeBackAsTheyWereSaved()
    {
        var body = new Doc.BodyComponent(motion: Doc.BodyMotion.Fixed, trigger: true, stay: true, surface: new Vector3(2, 0, -1));

        var back = Reopened(new EditorScene([Pool(body)]));

        Assert.Equal(body.ToJson(), PhysicsComponents.PhysicsBodyOf(back["pool"]!)!.ToJson());
    }

    [Fact]
    public void AZoneComesBackAsItWasSaved()
    {
        var zone = new Doc.ZoneComponent(new Vector3(0, 20, 0), linearDamping: 4, angularDamping: 2, priority: 3);

        var back = Reopened(new EditorScene([Pool(zone: zone)]));

        Assert.Equal(zone.ToJson(), PhysicsComponents.ZoneOf(back["pool"]!)!.ToJson());
    }

    [Fact]
    public void AFieldAZoneLeavesOutStaysOutOfTheFile()
    {
        var back = Reopened(new EditorScene([Pool(zone: new Doc.ZoneComponent(linearDamping: 3))]));

        var saved = PhysicsComponents.ZoneOf(back["pool"]!)!.ToJson();
        Assert.Equal(3.0, saved["linearDamping"]);
        Assert.False(saved.ContainsKey("gravity"));
        Assert.False(saved.ContainsKey("angularDamping"));
    }

    [Fact]
    public void AnObjectWithoutAZoneDoesNotGrowOne()
    {
        var back = Reopened(new EditorScene([Pool()]));

        Assert.Null(PhysicsComponents.ZoneOf(back["pool"]!));
        Assert.DoesNotContain("\"zone\"", SceneDocument.Encode(back));
    }

    [Fact]
    public void ACopyCarriesTheZoneAndDoesNotShareTheMap()
    {
        var item = Pool(zone: new Doc.ZoneComponent());
        var copy = item.CopyAs("other");
        copy.Components.Remove(Doc.SceneComponents.Zone);

        Assert.NotNull(PhysicsComponents.ZoneOf(item));
        Assert.Null(PhysicsComponents.ZoneOf(copy));
    }

    [Fact]
    public void AFixedTriggerIsAPlaceAndAFixedSolidBodyIsNot()
    {
        var trigger = new Doc.BodyComponent(motion: Doc.BodyMotion.Fixed, trigger: true);

        Assert.True(PhysicsComponents.IsPlace(Pool(trigger)));
        Assert.False(PhysicsComponents.IsPlace(Pool()));
    }

    [Fact]
    public void ADrivenTriggerIsOneForAMovingLiftOrAPatrol() =>
        Assert.True(PhysicsComponents.IsPlace(Pool(new Doc.BodyComponent(motion: Doc.BodyMotion.Driven, trigger: true))));

    [Fact]
    public void AZoneMakesItsBodyAPlaceWithoutBeingAsked() =>
        Assert.True(PhysicsComponents.IsPlace(Pool(zone: new Doc.ZoneComponent())));

    [Fact]
    public void AFreeBodyIsNeverAPlaceWhateverItSays()
    {
        var free = new Doc.BodyComponent(trigger: true);

        Assert.False(PhysicsComponents.IsPlace(Pool(free)));
        Assert.False(PhysicsComponents.IsPlace(Pool(free, new Doc.ZoneComponent())));
    }

    [Fact]
    public void AZoneWithNoBodyIsNoPlace()
    {
        var lone = new SceneObject(
            "pool",
            "Pool",
            ObjectKind.Group,
            components: new() { [Doc.SceneComponents.Zone] = new Doc.ZoneComponent() });

        Assert.False(PhysicsComponents.IsPlace(lone));
    }

    [Fact]
    public void GroundIsFixedEvenWhenItsFileSaysFree()
    {
        Assert.False(PhysicsComponents.MovesFreely(new Doc.BodyComponent(shape: Doc.BodyShape.Plane)));
        Assert.True(PhysicsComponents.MovesFreely(new Doc.BodyComponent()));
    }

    [Fact]
    public void AddingAZoneIsUndoneByTakingItOffAgain()
    {
        var (scene, history) = Editing();
        history.Run(PutZone(scene, new Doc.ZoneComponent(), "Add zone"));
        Assert.NotNull(PhysicsComponents.ZoneOf(scene["pool"]!));

        history.Undo();
        Assert.Null(PhysicsComponents.ZoneOf(scene["pool"]!));

        history.Redo();
        Assert.NotNull(PhysicsComponents.ZoneOf(scene["pool"]!));
    }

    [Fact]
    public void ADragThroughAFieldIsOneStep()
    {
        var (scene, history) = Editing();
        history.Run(PutZone(scene, new Doc.ZoneComponent(), "Add zone"));
        history.Seal();
        for (var i = 1; i <= 30; i++)
        {
            history.Run(PutZone(scene, PhysicsComponents.ZoneOf(scene["pool"]!)!.CopyWith(linearDamping: i * 0.1)));
        }

        Assert.Equal(3, PhysicsComponents.ZoneOf(scene["pool"]!)!.LinearDamping!.Value, 9);
        Assert.Equal(["Add zone", "Set Pool zone"], history.Labels);

        // Back to before the drag, and the zone stays on.
        history.Undo();
        Assert.Null(PhysicsComponents.ZoneOf(scene["pool"]!)!.LinearDamping);
        Assert.NotNull(PhysicsComponents.ZoneOf(scene["pool"]!));
    }

    [Fact]
    public void LeavingAFieldToTheBodyPutsItOutOfTheZone()
    {
        var (scene, history) = Editing();
        history.Run(PutZone(scene, new Doc.ZoneComponent(Vector3.Zero(), linearDamping: 2), "Add zone"));
        history.Seal();

        var zone = PhysicsComponents.ZoneOf(scene["pool"]!)!;
        history.Run(PutZone(scene, new Doc.ZoneComponent(linearDamping: zone.LinearDamping)));

        Assert.Null(PhysicsComponents.ZoneOf(scene["pool"]!)!.Gravity);
        Assert.Equal(2, PhysicsComponents.ZoneOf(scene["pool"]!)!.LinearDamping);
    }

    [Fact]
    public void ATriggerFlagIsOneUndoableStepOnTheBody()
    {
        var (scene, history) = Editing();
        var body = PhysicsComponents.PhysicsBodyOf(scene["pool"]!)!;
        history.Run(new SetObjectComponent("a", "pool", "Set Pool body", Doc.SceneComponents.Body, body, body.CopyWith(trigger: true)));
        history.Seal();
        Assert.True(PhysicsComponents.IsPlace(scene["pool"]!));

        history.Undo();
        Assert.False(PhysicsComponents.IsPlace(scene["pool"]!));
    }
}
