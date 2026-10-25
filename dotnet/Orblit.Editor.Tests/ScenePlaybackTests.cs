namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

public sealed class ScenePlaybackTests
{
    private static ClipDocument Bounce(WhenDone whenDone = WhenDone.Loop) => new(
        "Bounce",
        1,
        whenDone,
        channels:
        [
            new ClipChannel<Vector3>(
                "",
                "transform.position",
                ChannelKind.Vector,
                [new Key<Vector3>(0, Vector3.Zero()), new Key<Vector3>(1, new Vector3(0, 2, 0))]),
        ]);

    private static OrderedDictionary<string, Doc.SceneComponent> Autoplaying(string clip) =>
        new() { [Doc.SceneComponents.Motion] = new Doc.MotionComponent([clip], clip) };

    private static double HeightOf(ScenePlayback playback, string id) =>
        playback.Workspace.Loaded!.Scene![id]!.Position.Y;

    [Fact]
    public void AutoplayAnimatesACopyAndIncludesObjectsInEverySceneAutoplay()
    {
        SceneObject Cube(string id) => new(id, id, ObjectKind.Mesh, components: Autoplaying("bounce.oclip"));
        var editing = new Workspace("/project");
        editing.Add(new SceneEntry("main", "Main", scene: new EditorScene([Cube("cube")])));
        editing.SharedEntry.Scene = new EditorScene([Cube("shared")]);
        var playback = new ScenePlayback(editing, _ => Bounce());

        playback.Advance(0.5);
        Assert.Equal(1, HeightOf(playback, "cube"), 1e-9);
        Assert.Equal(1, playback.Workspace.Shared["shared"]!.Position.Y, 1e-9);
        Assert.Equal(0, editing.Loaded!.Scene!["cube"]!.Position.Y);
        Assert.Equal(0, editing.Shared["shared"]!.Position.Y);

        playback.Advance(1);
        Assert.Equal(1, HeightOf(playback, "cube"), 1e-9);
    }

    [Fact]
    public void AnUnassignedAutoplayIsIgnoredAndReleaseRestoresThePose()
    {
        var editing = new Workspace("/project");
        editing.Add(new SceneEntry(
            "main",
            "Main",
            scene: new EditorScene(
            [
                new SceneObject(
                    "ignored",
                    "Ignored",
                    ObjectKind.Mesh,
                    components: new OrderedDictionary<string, Doc.SceneComponent>
                    {
                        [Doc.SceneComponents.Motion] = new Doc.MotionComponent([], "missing.oclip"),
                    }),
                new SceneObject("release", "Release", ObjectKind.Mesh, components: Autoplaying("bounce.oclip")),
            ])));
        var read = new List<string>();
        var playback = new ScenePlayback(
            editing,
            path =>
            {
                read.Add(path);
                return Bounce(WhenDone.Release);
            });
        Assert.Equal(["bounce.oclip"], read);

        playback.Advance(0.5);
        Assert.Equal(1, HeightOf(playback, "release"), 1e-9);

        playback.Advance(0.5);
        Assert.Equal(0, HeightOf(playback, "release"));
    }

    // Marks "intro" a quarter of a second in.
    private static ClipDocument Cue() => new("Cue", 1, marks: [new Mark(0.25, "intro")]);

    // Lifts the cube four metres over two seconds, seen through the eye.
    private static CutsceneDocument Intro(WhenDone whenDone = WhenDone.Hold) => new(
        new ClipDocument(
            "intro",
            2,
            whenDone,
            channels:
            [
                new ClipChannel<Vector3>(
                    "cube",
                    "transform.position",
                    ChannelKind.Vector,
                    [
                        new Key<Vector3>(0, Vector3.Zero(), Hold.Linear),
                        new Key<Vector3>(2, new Vector3(0, 4, 0)),
                    ]),
            ]),
        [new CutsceneShot("eye", 0, 2)]);

    private static (Workspace Editing, ScenePlayback Playback) Play(CutsceneDocument cutscene)
    {
        var editing = new Workspace("/project");
        editing.Add(new SceneEntry(
            "main",
            "Main",
            scene: new EditorScene(
            [
                new SceneObject("trigger", "Trigger", ObjectKind.Group, components: Autoplaying("cue.oclip")),
                new SceneObject("cube", "Cube", ObjectKind.Mesh),
                new SceneObject("eye", "Eye", ObjectKind.Camera, position: new Vector3(0, 1, 5)),
            ])));
        var playback = new ScenePlayback(
            editing,
            _ => Cue(),
            new Dictionary<string, CutsceneDocument> { [cutscene.Name] = cutscene });
        return (editing, playback);
    }

    [Fact]
    public void ACutsceneStartsFromAMarkNamedAfterIt()
    {
        var (editing, playback) = Play(Intro());
        playback.Advance(0.2);
        Assert.Null(playback.Camera);

        playback.Advance(0.1);
        Assert.Equal(new Vector3(0, 1, 5), playback.Camera!.Position);
        Assert.Equal(0, HeightOf(playback, "cube"));

        playback.Advance(1);
        Assert.Equal(2, HeightOf(playback, "cube"), 1e-9);
        Assert.Equal(0, editing.Loaded!.Scene!["cube"]!.Position.Y);
    }

    [Fact]
    public void ACutsceneThatHoldsStaysWhereItEndsAndTheGameCameraIsBack()
    {
        var (_, playback) = Play(Intro());
        playback.Advance(0.3);
        playback.Advance(2.5);

        Assert.Equal(4, HeightOf(playback, "cube"), 1e-9);
        Assert.Null(playback.Camera);
    }

    [Fact]
    public void ACutsceneThatReleasesPutsBackEverythingItMoved()
    {
        var (_, playback) = Play(Intro(WhenDone.Release));
        playback.Advance(0.3);
        playback.Advance(1);
        Assert.Equal(2, HeightOf(playback, "cube"), 1e-9);

        playback.Advance(1.5);
        Assert.Equal(0, HeightOf(playback, "cube"));
        Assert.Null(playback.Camera);
    }
}
