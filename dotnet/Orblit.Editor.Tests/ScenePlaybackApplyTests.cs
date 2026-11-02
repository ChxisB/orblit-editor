namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

public sealed class ScenePlaybackApplyTests
{
    private static SceneObject Crate(string id, OrderedDictionary<string, Doc.SceneComponent>? components = null) =>
        new(id, id, ObjectKind.Mesh, components: components);

    private static ClipDocument Bounce() => new(
        "Bounce",
        1,
        WhenDone.Loop,
        channels:
        [
            new ClipChannel<Vector3>(
                "",
                "transform.position",
                ChannelKind.Vector,
                [new Key<Vector3>(0, Vector3.Zero()), new Key<Vector3>(1, new Vector3(0, 2, 0))]),
        ]);

    private static OrderedDictionary<string, Doc.SceneComponent> Bouncing() =>
        new() { [Doc.SceneComponents.Motion] = new Doc.MotionComponent(["bounce.oclip"], "bounce.oclip") };

    private static (Workspace Editing, ScenePlayback Playback) Playing(params SceneObject[] objects)
    {
        var editing = new Workspace("/project");
        editing.Add(new SceneEntry("main", "Main", scene: new EditorScene([.. objects])));
        return (editing, new ScenePlayback(editing, _ => Bounce()));
    }

    private static EditorScene Edited(Workspace editing) => editing.Loaded!.Scene!;

    private static EditorScene Shown(ScenePlayback playback) => playback.Workspace.Loaded!.Scene!;

    // What a game does to move one object: a document with it moved, and the difference.
    private static Doc.SceneDiff MovedTo(EditorScene scene, string id, double x)
    {
        var before = SceneDocument.DocumentOf(scene);
        var entity = before[id]!;
        var transform = (Doc.TransformComponent)entity[Doc.SceneComponents.Transform]!;
        var moved = new Doc.TransformComponent(new Vector3(x, 0, 0), transform.Rotation, transform.Scale);
        var after = before.WithEntity(id, entity.WithComponent(Doc.SceneComponents.Transform, moved));
        return Doc.SceneDiff.Between(before, after);
    }

    [Fact]
    public void AMoveShowsInTheCopyAndNotInTheScene()
    {
        var (editing, playback) = Playing(Crate("a"));

        playback.Apply(MovedTo(Shown(playback), "a", 3));

        Assert.Equal(3, Shown(playback)["a"]!.Position.X);
        Assert.Equal(0, Edited(editing)["a"]!.Position.X);
    }

    [Fact]
    public void ObjectsTheDiffDoesNotTouchAreTheSameObjects()
    {
        var (_, playback) = Playing(Crate("a"), Crate("b"), Crate("c"));
        var untouched = new[] { Shown(playback)["b"], Shown(playback)["c"] };

        playback.Apply(MovedTo(Shown(playback), "a", 3));

        Assert.Same(untouched[0], Shown(playback)["b"]);
        Assert.Same(untouched[1], Shown(playback)["c"]);
    }

    [Fact]
    public void AnObjectTheDiffChangedKeepsWhatTheRendererKnowsItBy()
    {
        var (_, playback) = Playing(Crate("a"));
        var key = Shown(playback)["a"]!.RenderKey;

        playback.Apply(MovedTo(Shown(playback), "a", 3));

        Assert.Equal(key, Shown(playback)["a"]!.RenderKey);
    }

    [Fact]
    public void ADiffToAnObjectAClipMovesDoesNotUndoTheClip()
    {
        var (_, playback) = Playing(Crate("cube", Bouncing()));
        playback.Advance(0.5);
        var posed = Shown(playback)["cube"]!.Position.Y;
        Assert.NotEqual(0, posed, 1e-9);

        playback.Apply(new Doc.SceneDiff([new Doc.SetEntityName("cube", "cube", "Crate")]));

        Assert.Equal("Crate", Shown(playback)["cube"]!.Name);
        Assert.Equal(posed, Shown(playback)["cube"]!.Position.Y, 1e-9);
    }

    [Fact]
    public void AnAddedObjectAppearsWhereTheDiffPutsItAndARemovedOneGoes()
    {
        var (_, playback) = Playing(Crate("a"), Crate("b"));
        var added = new Doc.SceneEntity("c", "c").ToJson();
        var gone = new Doc.SceneEntity("b", "b").ToJson();

        playback.Apply(new Doc.SceneDiff([new Doc.AddEntity(added, "a"), new Doc.RemoveEntity(gone)]));

        Assert.Equal(["a", "c"], Shown(playback).Objects.Select(one => one.Id));
    }

    [Fact]
    public void ASettingOfTheSceneChanges()
    {
        var (_, playback) = Playing(Crate("a"));

        playback.Apply(new Doc.SceneDiff([new Doc.SetSetting(Doc.SetSetting.Ambient, Shown(playback).Ambient, 12000.0)]));

        Assert.Equal(12000, Shown(playback).Ambient);
    }

    [Fact]
    public void ADiffForAnObjectThatIsNotThereChangesNothing()
    {
        var (_, playback) = Playing(Crate("a"));
        var before = Shown(playback).Objects;

        playback.Apply(new Doc.SceneDiff([new Doc.SetEntityName("gone", "gone", "Door")]));

        Assert.Equal(before, Shown(playback).Objects);
    }

    [Fact]
    public void AnEmptyDiffChangesNothing()
    {
        var (_, playback) = Playing(Crate("a"));
        var before = Shown(playback)["a"];

        playback.Apply(Doc.SceneDiff.None);

        Assert.Same(before, Shown(playback)["a"]);
    }

    [Fact]
    public void AnEditIsHandedOnForTheGameToTake()
    {
        var (editing, playback) = Playing(Crate("a"));
        var heard = new List<Doc.SceneDiff>();
        playback.Edited += heard.Add;

        Edited(editing)["a"]!.Name = "Door";
        playback.Follow();

        var diff = Assert.Single(heard);
        Assert.Contains(diff.Operations, op => op is Doc.SetEntityName { Id: "a", To: "Door" });
    }

    [Fact]
    public void NothingNewIsNotHandedOn()
    {
        var (_, playback) = Playing(Crate("a"));
        var heard = new List<Doc.SceneDiff>();
        playback.Edited += heard.Add;

        playback.Follow();

        Assert.Empty(heard);
    }

    [Fact]
    public void TheGamesOwnChangeIsNotHandedBackAsAnEdit()
    {
        var (_, playback) = Playing(Crate("a"));
        var heard = new List<Doc.SceneDiff>();
        playback.Edited += heard.Add;

        playback.Apply(MovedTo(Shown(playback), "a", 3));
        playback.Follow();

        Assert.Empty(heard);
        Assert.Equal(3, Shown(playback)["a"]!.Position.X);
    }

    [Fact]
    public void AnEditToTheSharedSceneIsNotHandedOnBecauseTheGameDoesNotPlayInIt()
    {
        var editing = new Workspace("/project");
        editing.Add(new SceneEntry("main", "Main", scene: new EditorScene([Crate("a")])));
        editing.SharedEntry.Scene = new EditorScene([Crate("manager")]);
        var playback = new ScenePlayback(editing, _ => null);
        var heard = new List<Doc.SceneDiff>();
        playback.Edited += heard.Add;

        editing.Shared["manager"]!.Name = "Boss";
        playback.Follow();

        Assert.Empty(heard);
        Assert.Equal("Boss", playback.Workspace.Shared["manager"]!.Name);
    }

    [Fact]
    public void AnEditKeepsTheObjectsItDidNotTouch()
    {
        var (editing, playback) = Playing(Crate("a"), Crate("b"));
        var untouched = Shown(playback)["b"];

        Edited(editing)["a"]!.Name = "Door";
        playback.Follow();

        Assert.Same(untouched, Shown(playback)["b"]);
    }
}
