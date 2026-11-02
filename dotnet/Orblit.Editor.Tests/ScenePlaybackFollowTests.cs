namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

public sealed class ScenePlaybackFollowTests
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

    [Fact]
    public void AMovedObjectMovesInTheCopyAndTheCopyStaysItsOwnScene()
    {
        var (editing, playback) = Playing(Crate("a"));

        Edited(editing)["a"]!.Position.SetFrom(new Vector3(3, 0, 0));

        playback.Follow();
        Assert.Equal(3, Shown(playback)["a"]!.Position.X);
        Assert.NotSame(Edited(editing)["a"], Shown(playback)["a"]);
    }

    [Fact]
    public void ARenamedAndRecolouredObjectShowsBoth()
    {
        var (editing, playback) = Playing(Crate("a"));

        Edited(editing)["a"]!.Name = "Door";
        Edited(editing)["a"]!.Colour = new Colour(0xFF112233);

        playback.Follow();
        Assert.Equal("Door", Shown(playback)["a"]!.Name);
        Assert.Equal(new Colour(0xFF112233), Shown(playback)["a"]!.Colour);
    }

    [Fact]
    public void AnAddedObjectAppearsAndADeletedObjectGoes()
    {
        var (editing, playback) = Playing(Crate("a"), Crate("b"));

        Edited(editing).Add(Crate("c"));
        Edited(editing).Remove("a");

        playback.Follow();
        Assert.Equal(["b", "c"], Shown(playback).Objects.Select(one => one.Id));
    }

    [Fact]
    public void WithNothingNewItChangesNothing()
    {
        var (editing, playback) = Playing(Crate("a"));
        Shown(playback)["a"]!.Position.SetFrom(new Vector3(9, 0, 0));

        playback.Follow();
        Assert.Equal(9, Shown(playback)["a"]!.Position.X);

        Edited(editing)["a"]!.Position.SetFrom(new Vector3(1, 0, 0));
        playback.Follow();
        Shown(playback)["a"]!.Position.SetFrom(new Vector3(9, 0, 0));
        playback.Follow();

        Assert.Equal(9, Shown(playback)["a"]!.Position.X);
    }

    [Fact]
    public void AnEditIsTakenOnceSoALaterEditToAnotherObjectDoesNotRepeatIt()
    {
        var (editing, playback) = Playing(Crate("a"), Crate("b"));
        Edited(editing)["a"]!.Position.SetFrom(new Vector3(1, 0, 0));
        playback.Follow();

        // The copy's own change is not an edit. If it were taken as one the next look would undo it.
        Shown(playback)["a"]!.Position.SetFrom(new Vector3(9, 0, 0));
        Edited(editing)["b"]!.Position.SetFrom(new Vector3(2, 0, 0));
        playback.Follow();

        Assert.Equal(9, Shown(playback)["a"]!.Position.X);
        Assert.Equal(2, Shown(playback)["b"]!.Position.X);
    }

    [Fact]
    public void AnEditToTheSharedSceneFollowsToo()
    {
        var editing = new Workspace("/project");
        editing.Add(new SceneEntry("main", "Main", scene: new EditorScene([Crate("a")])));
        editing.SharedEntry.Scene = new EditorScene([Crate("manager")]);
        var playback = new ScenePlayback(editing, _ => null);

        editing.Shared["manager"]!.Position.SetFrom(new Vector3(0, 5, 0));

        playback.Follow();
        Assert.Equal(5, playback.Workspace.Shared["manager"]!.Position.Y);
    }

    [Fact]
    public void AnObjectAClipMovesKeepsMovingAfterAnEditToAnotherObject()
    {
        var (editing, playback) = Playing(Crate("cube", Bouncing()), Crate("other"));
        var (_, untouched) = Playing(Crate("cube", Bouncing()), Crate("other"));
        playback.Advance(0.5);
        untouched.Advance(0.5);

        Edited(editing)["other"]!.Position.SetFrom(new Vector3(4, 0, 0));
        playback.Follow();
        Assert.Equal(Shown(untouched)["cube"]!.Position.Y, Shown(playback)["cube"]!.Position.Y, 1e-9);

        // The clip eases, so the later pose is compared with a playback nobody edited.
        playback.Advance(0.25);
        untouched.Advance(0.25);
        var later = Shown(playback)["cube"]!.Position.Y;
        Assert.Equal(Shown(untouched)["cube"]!.Position.Y, later, 1e-9);
        Assert.NotEqual(1, later, 1e-9);
        Assert.Equal(4, Shown(playback)["other"]!.Position.X);
    }

    [Fact]
    public void DeletingTheObjectAClipPlaysOnLeavesThePlaybackRunning()
    {
        var (editing, playback) = Playing(Crate("cube", Bouncing()), Crate("other"));
        playback.Advance(0.1);

        Edited(editing).Remove("cube");
        playback.Follow();
        playback.Advance(0.1);

        Assert.False(Shown(playback).Contains("cube"));
        Assert.True(Shown(playback).Contains("other"));
    }
}
