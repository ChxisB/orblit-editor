using System.Diagnostics;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Nothing in a scene can count how often it scans its list, so these are budgets, as in
// OutlinerModelTests. Each budget sits between the cost of one pass and the cost of a scan per
// object, which is seconds at these sizes.
public sealed class SceneScaleTests
{
    private static (EditorScene Scene, History History) Branches(int groups, int leaves)
    {
        var objects = new List<SceneObject>();
        for (var group = 0; group < groups; group++)
        {
            objects.Add(new SceneObject($"g{group}", $"G{group}", ObjectKind.Group));
            for (var leaf = 0; leaf < leaves; leaf++)
            {
                objects.Add(new SceneObject($"g{group}l{leaf}", $"L{leaf}", ObjectKind.Mesh, parentId: $"g{group}"));
            }
        }

        var scene = new EditorScene(objects);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        return (scene, new History(workspace));
    }

    private static string[] Ids(EditorScene scene) => [.. scene.Objects.Select(one => one.Id)];

    private static void Within(int milliseconds, string what, Action act)
    {
        var clock = Stopwatch.StartNew();
        act();
        Assert.True(clock.ElapsedMilliseconds < milliseconds, $"{what} took {clock.ElapsedMilliseconds} ms");
    }

    // What the index must say: the objects whose parent is that id, in the order the list holds them.
    private static void AssertSiblingsMatchTheList(EditorScene scene)
    {
        foreach (var parent in scene.Objects.Select(one => one.Id).Prepend(null))
        {
            var expected = scene.Objects.Where(one => one.ParentId == parent).Select(one => one.Id);
            Assert.Equal(expected, scene.SiblingsOf(parent).Select(one => one.Id));
        }
    }

    [Fact]
    public void EveryBranchIsWalkedInTimeForItsOwnSize()
    {
        var (scene, _) = Branches(10, 3000);
        var counts = new List<int>();

        Within(400, "walking ten branches of 3,000", () =>
        {
            for (var group = 0; group < 10; group++)
            {
                counts.Add(scene.DescendantsOf($"g{group}").Count);
            }
        });

        Assert.All(counts, count => Assert.Equal(3000, count));
    }

    [Fact]
    public void AThousandObjectsAreDeletedInOnePassOverTheList()
    {
        var (scene, history) = Branches(10, 5000);
        var before = Ids(scene);
        var doomed = Enumerable.Range(0, 3000).Select(leaf => $"g2l{leaf}").ToList();

        Within(500, "deleting 3,000 of 50,010", () => history.Run(new DeleteObjects("a", doomed, "3000")));
        Assert.Equal(before.Length - 3000, scene.Length);
        Assert.Null(scene["g2l0"]);

        Within(500, "undoing the delete", history.Undo);
        Assert.Equal(before, Ids(scene));
    }

    [Fact]
    public void UndoingADeletePutsEveryObjectBackWhereItWas()
    {
        var (scene, history) = Branches(5, 5);
        var before = Ids(scene);

        history.Run(new DeleteObjects("a", ["g1", "g3l2", "g3l4", "g1l1"], "four"));
        Assert.Equal(before.Length - 6 - 2, scene.Length);

        history.Undo();
        Assert.Equal(before, Ids(scene));
        history.Redo();
        Assert.Equal(before.Length - 8, scene.Length);
    }

    [Fact]
    public void ARestoredObjectPastTheEndOfTheListGoesLast()
    {
        var (scene, _) = Branches(1, 2);
        var extra = new SceneObject("extra", "Extra", ObjectKind.Mesh);

        scene.Restore([(extra, 99)]);

        Assert.Equal(["g0", "g0l0", "g0l1", "extra"], Ids(scene));
        Assert.Same(extra, scene["extra"]);
    }

    [Fact]
    public void CopyingEverythingDoesNotCompareTheSelectionWithItself()
    {
        var (scene, _) = Branches(100, 99);
        var everything = Ids(scene);
        var clipboard = new SceneClipboard();

        Within(400, "copying all 10,000", () => clipboard.Take(scene, everything));

        var pasted = clipboard.Contents(() => Guid.NewGuid().ToString());
        Assert.Equal(everything.Length, pasted.Objects.Count);
        Assert.Equal(100, pasted.Roots.Count);
    }

    [Fact]
    public void ACopyOfAParentAndItsChildKeepsOnlyTheParentAsATop()
    {
        var (scene, _) = Branches(2, 3);

        Assert.True(scene.HasAncestorAmong("g0l1", new HashSet<string> { "g0" }));
        Assert.False(scene.HasAncestorAmong("g0l1", new HashSet<string> { "g1" }));
        Assert.False(scene.HasAncestorAmong("g0l1", new HashSet<string> { "g0l1" }));
        Assert.False(scene.HasAncestorAmong("g0", new HashSet<string> { "g0" }));
    }

    [Fact]
    public void SiblingsFollowEveryEditToTheList()
    {
        var (scene, _) = Branches(3, 4);
        AssertSiblingsMatchTheList(scene);

        scene.Add(new SceneObject("added", "Added", ObjectKind.Mesh, parentId: "g0"));
        AssertSiblingsMatchTheList(scene);

        scene.MoveTo("g0l2", 0, "g1");
        AssertSiblingsMatchTheList(scene);

        scene.MoveTo("g1l3", 1);
        AssertSiblingsMatchTheList(scene);

        scene.Reparent("g2l1", "g0");
        AssertSiblingsMatchTheList(scene);

        scene.Replace(new SceneObject("g2l2", "Moved", ObjectKind.Mesh, parentId: "g1"));
        AssertSiblingsMatchTheList(scene);

        var removed = scene.Remove("g1");
        AssertSiblingsMatchTheList(scene);

        scene.Restore(removed);
        AssertSiblingsMatchTheList(scene);

        scene.ReplaceAll([new SceneObject("only", "Only", ObjectKind.Group)]);
        AssertSiblingsMatchTheList(scene);
    }

    [Fact]
    public void AMoveWithinOneParentLandsBeforeTheSiblingAtThatPlace()
    {
        var (scene, _) = Branches(1, 4);
        Assert.Equal(4, scene.SiblingsOf("g0").Count);

        scene.MoveTo("g0l0", 2, "g0");

        // Placed among the three that stay, so it goes before the last of them.
        Assert.Equal(["g0l1", "g0l2", "g0l0", "g0l3"], scene.SiblingsOf("g0").Select(one => one.Id));
        Assert.Equal(["g0", "g0l1", "g0l2", "g0l0", "g0l3"], Ids(scene));
    }

    [Fact]
    public void ANewListOfSiblingsDoesNotChangeTheOneAlreadyHanded()
    {
        var (scene, _) = Branches(1, 3);
        var handed = scene.SiblingsOf("g0");

        scene.Remove("g0l1");

        Assert.Equal(3, handed.Count);
        Assert.Equal(2, scene.SiblingsOf("g0").Count);
    }
}
