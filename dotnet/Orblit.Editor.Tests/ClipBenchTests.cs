namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// The timeline's side of the editor: clips opened, keyed from the inspector, changed through the undo
// stack, and shown on the scene without the scene reading as changed.
public sealed class ClipBenchTests
{
    private const string Wave = "/project/wave.oclip";

    private static readonly ChannelAddress LampPower = new(string.Empty, null, "light.power");

    private sealed record Rig(EditorScene Scene, History History, ClipBench Bench);

    private static Rig MakeRig()
    {
        var scene = new EditorScene(
        [
            new SceneObject("lamp", "Lamp", ObjectKind.Light, power: 100),
            new SceneObject("box", "Box", ObjectKind.Group),
        ]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);
        var bench = new ClipBench(history, () => scene);
        bench.OpenDocument(Wave, new ClipDocument("wave", 2));
        return new Rig(scene, history, bench);
    }

    // Assert.NotNull does not hand the value back for a reference type.
    private static T Some<T>(T? value)
        where T : class
    {
        Assert.NotNull(value);
        return value;
    }

    private static SceneObject Obj(EditorScene scene, string id) => Some(scene[id]);

    private static ClipDocument ClipOf(ClipBench bench) => Some(bench.Clip);

    private static ClipChannel LampChannel(ClipBench bench) =>
        Assert.IsAssignableFrom<ClipChannel>(ClipEdits.ChannelAt(ClipOf(bench), LampPower));

    [Fact]
    public void NothingCanBeKeyedWithNoClipOpen()
    {
        var scene = new EditorScene([new SceneObject("lamp", "Lamp", ObjectKind.Light)]);
        var bench = new ClipBench(new History(new Workspace("/project")), () => scene);

        Assert.Equal(KeyMark.None, bench.MarkFor(Obj(scene, "lamp"), "light.power"));
    }

    [Fact]
    public void KeysWhatTheRowShowsAndPlaysOnWhatWasKeyedFirst()
    {
        var (scene, history, bench) = MakeRig();
        var lamp = Obj(scene, "lamp");
        Assert.Equal(KeyMark.Unkeyed, bench.MarkFor(lamp, "light.power"));

        // A colour is four numbers, which is not something a key is guessed to be.
        Assert.Equal(KeyMark.None, bench.MarkFor(lamp, "light.colour"));

        bench.Key(lamp, "light.power");
        Assert.Equal("lamp", bench.Owner);
        Assert.Equal(KeyMark.Keyed, bench.MarkFor(lamp, "light.power"));
        Assert.Equal(100.0, Assert.IsType<double>(Assert.Single(LampChannel(bench).BoxedKeys).Value));
        Assert.Equal(["Key Lamp power"], history.Labels);

        bench.At = 0.5;
        Assert.Equal(KeyMark.Animated, bench.MarkFor(lamp, "light.power"));
    }

    [Fact]
    public void NamesAnythingElseByItsIdFromWhateverPlaysTheClip()
    {
        var (scene, _, bench) = MakeRig();
        bench.Owner = "lamp";
        bench.Key(Obj(scene, "box"), "transform.position");

        var channel = Assert.Single(ClipOf(bench).Channels);
        Assert.Equal("box", channel.Target);
        Assert.Same(ChannelKind.Vector, channel.Kind);
    }

    [Fact]
    public void AKeyGoesOnTheFrameNearestThePlayhead()
    {
        var (scene, _, bench) = MakeRig();
        bench.At = 0.51;
        bench.Key(Obj(scene, "lamp"), "light.power");

        Assert.Equal(15.0 / 30, Assert.Single(LampChannel(bench).BoxedKeys).At, 1e-9);
    }

    [Fact]
    public void UndoTakesAKeyBackOffAndTheClipIsSavedWhenItIsBack()
    {
        var (scene, history, bench) = MakeRig();
        var clip = Some(bench.Shown);
        Assert.False(bench.IsUnsaved(clip));

        bench.Key(Obj(scene, "lamp"), "light.power");
        Assert.True(bench.IsUnsaved(clip));
        history.Undo();
        Assert.Empty(ClipOf(bench).Channels);
        Assert.False(bench.IsUnsaved(clip));
        history.Redo();
        Assert.Single(ClipOf(bench).Channels);
    }

    [Fact]
    public void ADragOfKeysIsOneStepAndSaysItOnlyMovedThem()
    {
        var (scene, history, bench) = MakeRig();
        bench.Key(Obj(scene, "lamp"), "light.power");
        var reference = new KeyRef(LampPower, 0);
        bench.Selection = new HashSet<KeyRef> { reference };
        var start = ClipOf(bench);
        var gesture = new object();

        foreach (var by in new[] { 0.1, 0.2, 0.3 })
        {
            var moved = ClipEdits.MovedKeys(start, new HashSet<KeyRef> { reference }, by);
            bench.Edit("Move keys", _ => moved.Clip, new EditOptions(moved.Keys, gesture, OnlyMoves: true));
            Assert.True(history.LastOnlyMoved);
        }

        history.Seal();

        Assert.Equal(["Key Lamp power", "Move keys"], history.Labels);
        Assert.Equal(0.3, Some(ClipEdits.KeyOf(ClipOf(bench), reference)).At, 1e-9);
        history.Undo();
        Assert.Equal(0, Some(ClipEdits.KeyOf(ClipOf(bench), reference)).At);
        Assert.True(bench.Selection.SetEquals([reference]));
    }

    [Fact]
    public void UndoingAClipThatIsNotShownShowsIt()
    {
        var (scene, history, bench) = MakeRig();
        bench.Key(Obj(scene, "lamp"), "light.power");
        bench.OpenDocument("/project/other.oclip", new ClipDocument("o", 1));
        Assert.Equal("o", Some(bench.Shown).Clip.Name);

        history.Undo();
        Assert.Equal("wave", Some(bench.Shown).Clip.Name);
    }

    [Fact]
    public void ClosingAClipForgetsItsSteps()
    {
        var (scene, history, bench) = MakeRig();
        bench.Key(Obj(scene, "lamp"), "light.power");
        bench.Close(Wave);

        Assert.False(history.CanUndo);
        Assert.Null(bench.Shown);
    }

    [Fact]
    public void SavingWritesTheClipsThatChangedAndTheyReadAsSaved()
    {
        var folder = Directory.CreateTempSubdirectory("clip_bench");
        try
        {
            var path = Path.Combine(folder.FullName, "wave.oclip");
            File.WriteAllText(path, new ClipDocument("wave", 2).Encode());

            var scene = new EditorScene([new SceneObject("lamp", "Lamp", ObjectKind.Light)]);
            var bench = new ClipBench(new History(new Workspace(folder.FullName)), () => scene);
            Assert.Empty(bench.OpenFile(path));
            bench.Key(Obj(scene, "lamp"), "light.power");
            Assert.True(bench.AnyUnsaved);

            Assert.Empty(bench.SaveAll());
            Assert.False(bench.AnyUnsaved);
            var read = ClipDocument.Decode(File.ReadAllText(path)).Clip;
            Assert.Equal("light.power", Assert.Single(read.Channels).Property);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private static ClipDocument Rising() => new(
        "rise",
        1,
        channels:
        [
            new ClipChannel<Vector3>(
                "box",
                "transform.position",
                ChannelKind.Vector,
                [new Key<Vector3>(0, Vector3.Zero(), Hold.Linear), new Key<Vector3>(1, new Vector3(0, 2, 0))]),
            new ClipChannel<double>(
                string.Empty,
                "light.power",
                ChannelKind.Number,
                [new Key<double>(0, 10, Hold.Linear), new Key<double>(1, 30)]),
        ]);

    [Fact]
    public void PreviewPosesTheSceneWhereThePlayheadIsAndPutsItBack()
    {
        var (scene, history, _) = MakeRig();
        var preview = new ClipPreview();
        var clip = Rising();

        Assert.Equal(PoseChange.Rebuilt, preview.Pose(scene, clip, "lamp", 0.5));
        Assert.Equal(1, Obj(scene, "box").Position.Y, 1e-9);
        Assert.Equal(20, Obj(scene, "lamp").Power, 1e-9);

        // Looking is not editing.
        Assert.False(history.CanUndo);

        Assert.Equal(PoseChange.Rebuilt, preview.Pose(scene, clip, "lamp", 0.75));
        Assert.Equal(1.5, Obj(scene, "box").Position.Y, 1e-9);

        Assert.Equal(PoseChange.Rebuilt, preview.Lift(scene));
        Assert.Equal(0, Obj(scene, "box").Position.Y);
        Assert.Equal(100, Obj(scene, "lamp").Power);
        Assert.False(preview.Posing);
    }

    [Fact]
    public void PreviewOfOnlyMovingThingsIsOnlyAMove()
    {
        var (scene, _, _) = MakeRig();
        var preview = new ClipPreview();
        var clip = Rising();
        var moving = clip.CopyWith(channels: [clip.Channels[0]]);

        Assert.Equal(PoseChange.Moved, preview.Pose(scene, moving, "lamp", 0.5));
        Assert.Equal(PoseChange.Moved, preview.Pose(scene, moving, "lamp", 0.75));

        // Where it already is: nothing to build again.
        Assert.Equal(PoseChange.None, preview.Pose(scene, moving, "lamp", 0.75));
    }

    [Fact]
    public void PreviewPutsBackWhatTheClipStopsMoving()
    {
        var (scene, _, _) = MakeRig();
        var preview = new ClipPreview();
        var clip = Rising();
        preview.Pose(scene, clip, "lamp", 1);
        Assert.Equal(30, Obj(scene, "lamp").Power);

        // The power channel deleted: the lamp is its own again.
        preview.Pose(scene, clip.CopyWith(channels: [clip.Channels[0]]), "lamp", 1);
        Assert.Equal(100, Obj(scene, "lamp").Power);
        Assert.Equal(2, Obj(scene, "box").Position.Y);

        // Nothing to play on: everything is.
        preview.Pose(scene, clip, null, 1);
        Assert.Equal(0, Obj(scene, "box").Position.Y);
        Assert.False(preview.Posing);
    }

    [Fact]
    public void PreviewKeepsWhatTheRendererKnowsEachObjectBy()
    {
        var (scene, _, _) = MakeRig();
        var key = Obj(scene, "lamp").RenderKey;
        new ClipPreview().Pose(scene, Rising(), "lamp", 0.5);

        Assert.Equal(key, Obj(scene, "lamp").RenderKey);
        Assert.Equal("lamp", scene.Objects[0].Id);
    }

    [Fact]
    public void ACutsceneKeysByTheScenesOwnIdsAndKeepsItsShotsInOrderThroughUndo()
    {
        var (scene, history, _) = MakeRig();
        var bench = new ClipBench(history, () => scene, PlayedOn.Scene);
        const string path = "/project/intro.ocutscene";
        bench.OpenDocument(path, new CutsceneDocument(new ClipDocument("intro", 4), [new CutsceneShot("cam", 2, 1)]));

        bench.Key(Obj(scene, "box"), "transform.position");
        Assert.Equal("box", Assert.Single(ClipOf(bench).Channels).Target);
        Assert.Null(bench.Owner);

        bench.EditShots("Add shot", shots => [.. shots, new CutsceneShot("other", 0, 1)]);
        Assert.Equal(["other", "cam"], CamerasOf(bench));
        Assert.Equal("Add shot", history.Labels[^1]);

        history.Undo();
        Assert.Equal(["cam"], CamerasOf(bench));
    }

    private static List<string> CamerasOf(ClipBench bench) =>
        [.. Some(Some(bench.Shown).Shots).Select(shot => shot.Camera)];

    [Fact]
    public void AClipIsPlayedOnTheLoadedSceneThroughItsOwner()
    {
        var (scene, _, bench) = MakeRig();
        Assert.Null(bench.ObjectOf("box"));
        Assert.Null(bench.ScopeIn(scene));

        bench.Owner = "lamp";
        Assert.Equal("box", bench.ObjectOf("box")?.Id);
        Assert.Equal("lamp", bench.ObjectOf(string.Empty)?.Id);
        Some(bench.ScopeIn(scene));

        // Names mean nothing in a scene the clip is not played on.
        Assert.Null(bench.ScopeIn(new EditorScene([])));
    }

    [Fact]
    public void ThePlayheadStaysInsideTheClipAndANewClipStartsWithNothingSelected()
    {
        var (scene, _, bench) = MakeRig();
        bench.At = 5;
        Assert.Equal(2, bench.At);

        bench.Key(Obj(scene, "lamp"), "light.power");
        bench.Selection = new HashSet<KeyRef> { new(LampPower, 0) };
        bench.OpenDocument("/project/short.oclip", new ClipDocument("short", 1));

        Assert.Equal(1, bench.At);
        Assert.Empty(bench.Selection);
    }
}
