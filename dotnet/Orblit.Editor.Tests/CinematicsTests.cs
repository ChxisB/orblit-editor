namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// Cutscenes in the editor: cutting between cameras, framing a camera from the scene view, and a
// cutscene's shots and keys edited through the undo stack.
public sealed class CinematicsTests
{
    private const double Tight = 1e-9;

    private sealed record Rig(EditorScene Scene, History History, ClipBench Bench);

    private static CutsceneShot Shot(string camera, double start, double duration) => new(camera, start, duration);

    // Each shot as camera, start and end, which reads more plainly in a failure than the shots do.
    private static List<(string, double, double)> Spans(IEnumerable<CutsceneShot> shots) =>
        [.. shots.Select(shot => (shot.Camera, shot.Start, shot.End))];

    private static T Some<T>(T? value)
        where T : class
    {
        Assert.NotNull(value);
        return value;
    }

    // A cube and two cameras, one looking down -Z and one turned to look down -X.
    private static Rig MakeRig(string? folder = null)
    {
        var root = folder ?? "/project";
        var scene = new EditorScene(
        [
            new SceneObject("cube", "Cube", ObjectKind.Group),
            new SceneObject("front", "Front", ObjectKind.Camera, position: new Vector3(0, 1, 5)),
            new SceneObject("side", "Side", ObjectKind.Camera, position: new Vector3(5, 1, 0), rotation: new Vector3(0, 90, 0)),
        ]);
        var workspace = new Workspace(root);
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);
        var bench = new ClipBench(history, () => scene, PlayedOn.Scene);
        bench.OpenDocument($"{root}/intro.ocutscene", new CutsceneDocument(new ClipDocument("intro", 4)));
        return new Rig(scene, history, bench);
    }

    [Fact]
    public void CuttingIntoAnEmptyCutsceneHoldsToTheEnd() =>
        Assert.Equal([("front", 1d, 4d)], Spans(Cinematics.CutTo([], "front", 1, 4)));

    [Fact]
    public void CuttingEndsTheShotRunningAtThePlayheadThere() =>
        Assert.Equal(
            [("front", 0d, 1.5), ("side", 1.5, 4d)],
            Spans(Cinematics.CutTo([Shot("front", 0, 4)], "side", 1.5, 4)));

    [Fact]
    public void ACutHoldsUntilTheNextShotStarts() =>
        Assert.Equal(
            [("front", 0d, 1d), ("front", 3d, 4d), ("side", 1d, 3d)],
            Spans(Cinematics.CutTo([Shot("front", 0, 1), Shot("front", 3, 1)], "side", 1, 4)));

    [Fact]
    public void ACutTakesThePlaceOfAShotThatStartsAtThePlayhead() =>
        Assert.Equal(
            [("front", 0d, 1d), ("front", 4d, 5d), ("side", 1d, 4d)],
            Spans(Cinematics.CutTo([Shot("front", 0, 1), Shot("front", 1, 1), Shot("front", 4, 1)], "side", 1, 6)));

    [Fact]
    public void ACutShortensTheShotRunningByWhatIsLeftOfItAfterItsOwnStart() =>
        Assert.Equal(
            [("front", 1d, 2.5), ("side", 2.5, 4d)],
            Spans(Cinematics.CutTo([Shot("front", 1, 4)], "side", 2.5, 4)));

    [Fact]
    public void ACutLeavesAShotThatEndedBeforeThePlayheadAsItWas() =>
        Assert.Equal(
            [("front", 0d, 1d), ("side", 3d, 4d)],
            Spans(Cinematics.CutTo([Shot("front", 0, 1)], "side", 3, 4)));

    [Fact]
    public void ACutAtTheVeryEndStillMakesAShotWithALength() =>
        Assert.Equal(
            [("front", 0d, 4d), ("side", 4d, 6d)],
            Spans(Cinematics.CutTo([Shot("front", 0, 4)], "side", 4, 4)));

    private static OrbitCamera View() => new(0.7, 0.3, 8, target: new Vector3(1, 2, 3));

    private static SceneObject Eye(Placement place, string? parent = null) =>
        new("eye", "Eye", ObjectKind.Camera, parentId: parent, position: place.Position, rotation: place.Rotation);

    // Where a camera is seen from and which way it looks.
    private static (Vector3 Position, Vector3 Forward) Seen(EditorScene scene, SceneObject camera)
    {
        var at = SceneCameras.CameraOf(scene, camera);
        return (at.Position, (at.Target - at.Position).Normalized());
    }

    private static void AssertSame(Vector3 actual, Vector3 expected) =>
        Assert.True(actual.DistanceTo(expected) < Tight, $"{actual} is not {expected}");

    [Fact]
    public void ACameraPutWhereTheViewIsSeesWhatTheViewSees()
    {
        var scene = new EditorScene([]);
        var view = View();
        var place = CameraViews.PlaceFor(view, scene);
        var camera = Eye(place);
        scene.Add(camera);

        var wanted = view.ToRenderCamera();
        var (position, forward) = Seen(scene, camera);
        AssertSame(position, wanted.Position);
        AssertSame(forward, (wanted.Target - wanted.Position).Normalized());
        // A view cannot roll, and the angles are the ones typed rather than another set that turns
        // the same way.
        Assert.Equal(0, place.Rotation.Z);
        Assert.Equal(Doubles.Degrees(0.7), place.Rotation.Y, Tight);
    }

    [Fact]
    public void LookingThroughACameraPutsTheViewBackWhereItWas()
    {
        var scene = new EditorScene([]);
        var view = View();
        var place = CameraViews.PlaceFor(view, scene);
        var camera = Eye(place);
        scene.Add(camera);

        var back = CameraViews.ViewThrough(scene, camera);
        Assert.Equal(view.Yaw, back.Yaw, Tight);
        Assert.Equal(view.Pitch, back.Pitch, Tight);
        AssertSame(back.ToRenderCamera().Position, place.Position);
    }

    [Fact]
    public void ACameraInsideSomethingTurnedIsPlacedInItsTerms()
    {
        var scene = new EditorScene(
        [
            new SceneObject("rig", "Rig", ObjectKind.Group, position: new Vector3(2, 0, -1), rotation: new Vector3(0, 40, 10)),
        ]);
        var view = View();
        var camera = Eye(CameraViews.PlaceFor(view, scene, "rig"), "rig");
        scene.Add(camera);

        var wanted = view.ToRenderCamera();
        var (position, forward) = Seen(scene, camera);
        AssertSame(position, wanted.Position);
        AssertSame(forward, (wanted.Target - wanted.Position).Normalized());
    }

    [Fact]
    public void ACameraUnderAParentTheSceneLacksIsPlacedInTheSceneItself()
    {
        var scene = new EditorScene([]);
        foreach (var view in new[] { View(), new OrbitCamera(2.3, -0.9, 8), new OrbitCamera(-1.1, 1.2, 3) })
        {
            // The angles as the view has them, which a trip through a matrix does not always give back to the last digit.
            var place = CameraViews.PlaceFor(view, scene, "gone");
            Assert.Equal(Doubles.Degrees(-view.Pitch), place.Rotation.X);
            Assert.Equal(Doubles.Degrees(view.Yaw), place.Rotation.Y);
            Assert.Equal(0, place.Rotation.Z);
        }
    }

    [Fact]
    public void LookingStraightDownStopsShortOfThePole()
    {
        var scene = new EditorScene([new SceneObject("eye", "Eye", ObjectKind.Camera, rotation: new Vector3(-90, 0, 0))]);

        var back = CameraViews.ViewThrough(scene, Some(scene["eye"]));
        Assert.True(back.Pitch < Math.PI / 2);
        Assert.True(back.Pitch > (Math.PI / 2) - 0.1);
    }

    private static CutsceneDocument Cutscene(params CutsceneShot[] shots) => new(new ClipDocument("intro", 4), shots);

    [Fact]
    public void TheFinishedShotLooksThroughTheCameraOfTheShotRunning()
    {
        var rig = MakeRig();
        var cutscene = Cutscene(Shot("front", 0, 2), Shot("side", 2, 2));

        var early = Some(SceneCameras.CutsceneCamera(rig.Scene, cutscene, 1));
        Assert.Equal(SceneCameras.CameraOf(rig.Scene, Some(rig.Scene["front"])).Position, early.Position);
        var late = Some(SceneCameras.CutsceneCamera(rig.Scene, cutscene, 3));
        Assert.Equal(SceneCameras.CameraOf(rig.Scene, Some(rig.Scene["side"])).Position, late.Position);
    }

    [Fact]
    public void TheFinishedShotIsHalfwayBetweenTwoCamerasHalfwayThroughAnOverlap()
    {
        var rig = MakeRig();
        var cutscene = Cutscene(Shot("front", 0, 3), Shot("side", 2, 2));
        var front = SceneCameras.CameraOf(rig.Scene, Some(rig.Scene["front"])).Position;
        var side = SceneCameras.CameraOf(rig.Scene, Some(rig.Scene["side"])).Position;

        var middle = Some(SceneCameras.CutsceneCamera(rig.Scene, cutscene, 2.5));
        AssertSame(middle.Position, (front + side) / 2);
    }

    [Fact]
    public void TheFinishedShotIsNothingBetweenShotsOrForACameraTheSceneLacks()
    {
        var rig = MakeRig();
        var cutscene = Cutscene(Shot("front", 0, 1), Shot("gone", 2, 2));

        Assert.Null(SceneCameras.CutsceneCamera(rig.Scene, cutscene, 1.5));
        Assert.Null(SceneCameras.CutsceneCamera(rig.Scene, cutscene, 3));
    }

    [Fact]
    public void OneNumberTypedIntoAShotIsOneStep()
    {
        var rig = MakeRig();
        rig.Bench.EditShots("Add shot", _ => [Shot("front", 0, 1)]);
        var typing = new object();
        foreach (var length in new[] { 2.0, 2.5 })
        {
            rig.Bench.EditShots("Change shot length", shots => [shots.Single().CopyWith(duration: length)], typing);
        }

        Assert.Equal(["Add shot", "Change shot length"], rig.History.Labels);
        Assert.Equal(2.5, Some(rig.Bench.Shown?.Shots).Single().Duration);

        rig.History.Undo();
        Assert.Equal(1, Some(rig.Bench.Shown?.Shots).Single().Duration);
    }

    [Fact]
    public void ACutsceneIsSavedWithItsShotsAndKeys()
    {
        var folder = Directory.CreateTempSubdirectory("cutscene_bench");
        try
        {
            var rig = MakeRig(folder.FullName);
            rig.Bench.Key(Some(rig.Scene["cube"]), "transform.position");
            rig.Bench.EditShots("Add shot", _ => [Shot("front", 0, 4)]);

            Assert.Empty(rig.Bench.SaveAll());
            Assert.False(rig.Bench.AnyUnsaved);
            var read = CutsceneDocument.Decode(File.ReadAllText(Path.Combine(folder.FullName, "intro.ocutscene"))).Cutscene;
            Assert.Equal([("front", 0d, 4d)], Spans(read.Shots));
            Assert.Equal("cube", Assert.Single(read.Motion.Channels).Target);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void PosingTheWholeSceneMovesThingsByTheirIdsAndNoClipPutsThemBack()
    {
        var rig = MakeRig();
        var rising = new ClipDocument(
            "rise",
            1,
            channels:
            [
                new ClipChannel<Vector3>(
                    "cube",
                    "transform.position",
                    ChannelKind.Vector,
                    [new Key<Vector3>(0, Vector3.Zero(), Hold.Linear), new Key<Vector3>(1, new Vector3(0, 2, 0))]),
            ]);
        var preview = new ClipPreview();

        preview.PoseAll(rig.Scene, rising, 0.5);
        Assert.Equal(1, Some(rig.Scene["cube"]).Position.Y, Tight);
        Assert.False(rig.History.CanUndo);

        preview.PoseAll(rig.Scene, null, 0.5);
        Assert.Equal(0, Some(rig.Scene["cube"]).Position.Y);
        Assert.False(preview.Posing);
    }

    private static PlaceObject Steer(EditorScene scene, Vector3 to)
    {
        var front = Some(scene["front"]);
        return new PlaceObject(
            "a",
            "front",
            "Front",
            new Placement(front.Position.Clone(), front.Rotation.Clone()),
            new Placement(to, new Vector3(10, 20, 0)));
    }

    [Fact]
    public void SteeringACameraMovesAndTurnsItAsOneStepHoweverLongItGoesOn()
    {
        var rig = MakeRig();
        rig.History.Run(Steer(rig.Scene, new Vector3(0, 1, 4)));
        rig.History.Run(Steer(rig.Scene, new Vector3(0, 1, 3)));

        var front = Some(rig.Scene["front"]);
        Assert.Equal(["Steer Front"], rig.History.Labels);
        Assert.Equal(new Vector3(0, 1, 3), front.Position);
        Assert.Equal(new Vector3(10, 20, 0), front.Rotation);

        rig.History.Undo();
        Assert.Equal(new Vector3(0, 1, 5), front.Position);
        Assert.Equal(Vector3.Zero(), front.Rotation);
    }
}
