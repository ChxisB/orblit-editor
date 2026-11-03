using System.Diagnostics;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

public sealed class OutlinerModelTests
{
    private static (Workspace Workspace, OutlinerModel Model, EditorScene Scene) Rig()
    {
        var scene = EditorScene.Starter();
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        return (workspace, new OutlinerModel(workspace), scene);
    }

    // What a row is drawn as: the object's name, or the scene's own.
    private static string[] Names(OutlinerModel model) =>
        [.. model.Rows().Select(row => row.Item is { } one ? row.Entry.Scene!.DisplayNameOf(one) : row.Entry.Title)];

    private static OutlinerRow RowOf(OutlinerModel model, string id) =>
        model.Rows().Single(row => row.Key == id);

    [Fact]
    public void ListsTheSharedSetFirstThenEachSceneWithItsObjectsUnderIt()
    {
        var (_, model, _) = Rig();

        Assert.Equal(["Shared", "A", "Sun", "Ground", "Props", "Cube", "Crate", "Camera", "Weather"], Names(model));
    }

    [Fact]
    public void RowsRecordHowDeepTheyAreAndWhetherTheyHoldAnything()
    {
        var (_, model, _) = Rig();

        Assert.Equal(0, RowOf(model, "a").Depth);
        Assert.Equal(1, RowOf(model, "props").Depth);
        Assert.Equal(2, RowOf(model, "crate").Depth);
        Assert.True(RowOf(model, "props").HasChildren);
        Assert.False(RowOf(model, "crate").HasChildren);
    }

    [Fact]
    public void KeepsTheNamesThatHoldTheText()
    {
        var (_, model, _) = Rig();

        model.Filter = "cub";

        Assert.Contains("Cube", Names(model));
        Assert.DoesNotContain("Ground", Names(model));
    }

    [Fact]
    public void IgnoresCaseAndTheSpacesAroundTheText()
    {
        var (_, model, _) = Rig();

        model.Filter = "  GROUND ";

        Assert.Contains("Ground", Names(model));
        Assert.DoesNotContain("Cube", Names(model));
    }

    [Fact]
    public void KeepsTheParentOfAMatchSoItShowsWhereItSits()
    {
        var (_, model, _) = Rig();

        model.Filter = "crate";

        Assert.Contains("Crate", Names(model));
        Assert.Contains("Props", Names(model));
        Assert.DoesNotContain("Ground", Names(model));
    }

    [Fact]
    public void ShowsAMatchInsideAParentThatWasClosed()
    {
        var (_, model, _) = Rig();
        model.Toggle("props");
        Assert.DoesNotContain("Crate", Names(model));

        model.Filter = "crate";

        Assert.Contains("Crate", Names(model));
    }

    [Fact]
    public void ListsNothingWhenNothingHasThatName()
    {
        var (_, model, _) = Rig();

        model.Filter = "zzz";

        Assert.Empty(model.Rows());
    }

    [Fact]
    public void ClearingTheFilterBringsTheWholeTreeBack()
    {
        var (_, model, _) = Rig();
        model.Filter = "cub";

        model.Filter = "";

        Assert.Contains("Ground", Names(model));
        Assert.Contains("Cube", Names(model));
    }

    [Fact]
    public void ASceneWhoseNameMatchesIsListedWithOnlyTheObjectsThatMatchToo()
    {
        var (_, model, _) = Rig();

        model.Filter = "a";

        Assert.Contains("A", Names(model));
        Assert.DoesNotContain("Sun", Names(model));
        Assert.Contains("Camera", Names(model));
    }

    [Fact]
    public void ClosingAParentHidesWhatIsInsideIt()
    {
        var (_, model, _) = Rig();

        model.Toggle("props");

        Assert.True(model.IsCollapsed("props"));
        Assert.Equal(["Shared", "A", "Sun", "Ground", "Props", "Camera", "Weather"], Names(model));

        model.Toggle("props");
        Assert.False(model.IsCollapsed("props"));
    }

    [Fact]
    public void ClosingASceneHidesAllItsObjects()
    {
        var (_, model, _) = Rig();

        model.Toggle("a");

        Assert.Equal(["Shared", "A"], Names(model));
    }

    [Fact]
    public void AnUnloadedSceneIsOneRowWithNothingUnderIt()
    {
        var (workspace, model, _) = Rig();
        workspace.Add(new SceneEntry("b", "B", "/project/scenes/level2.oscene"));

        var row = RowOf(model, "b");

        Assert.Equal("level2", row.Entry.Title);
        Assert.False(row.HasChildren);
        Assert.Equal(1, model.Rows().Count(one => one.Entry.Id == "b"));
    }

    [Fact]
    public void DroppingBesideARowPlacesItBeforeOrAfterItAmongItsSiblings()
    {
        var (_, model, scene) = Rig();
        var cube = RowOf(model, "cube");

        Assert.Equal(new Drop("a", "props", scene.IndexOf("cube")), OutlinerModel.PlaceFor(cube, DropKind.Before));
        Assert.Equal(new Drop("a", "props", scene.IndexOf("cube") + 1), OutlinerModel.PlaceFor(cube, DropKind.After));
    }

    [Fact]
    public void DroppingInsideARowPutsItLastAmongWhatThatRowHolds()
    {
        var (_, model, _) = Rig();

        Assert.Equal(new Drop("a", "props", 2), OutlinerModel.PlaceFor(RowOf(model, "props"), DropKind.Inside));
    }

    [Fact]
    public void DroppingOnASceneRowGoesToItsTopLevel()
    {
        var (_, model, scene) = Rig();
        var row = RowOf(model, "a");

        Assert.Equal(new Drop("a", null, 0), OutlinerModel.PlaceFor(row, DropKind.Before));
        Assert.Equal(new Drop("a", null, scene.Roots.Count), OutlinerModel.PlaceFor(row, DropKind.Inside));
    }

    [Fact]
    public void AnObjectCannotBeDroppedIntoItselfOrWhatItHolds()
    {
        var (_, model, _) = Rig();

        Assert.False(model.Accepts(RowOf(model, "props"), "props"));
        Assert.False(model.Accepts(RowOf(model, "cube"), "props"));
        Assert.True(model.Accepts(RowOf(model, "sun"), "props"));
    }

    [Fact]
    public void AnObjectNobodyHoldsIsRefused()
    {
        var (_, model, _) = Rig();

        Assert.False(model.Accepts(RowOf(model, "sun"), "missing"));
    }

    [Fact]
    public void NothingIsDroppedOnASceneThatIsNotLoaded()
    {
        var (workspace, model, _) = Rig();
        workspace.Add(new SceneEntry("b", "B", "/project/scenes/level2.oscene"));

        Assert.False(model.Accepts(RowOf(model, "b"), "sun"));
    }

    [Fact]
    public void ASceneRowTakesAnyObjectIncludingOneFromAnotherScene()
    {
        var (workspace, model, _) = Rig();
        workspace.Shared.Add(new SceneObject(id: "manager", name: "Manager", kind: ObjectKind.Group));

        Assert.True(model.Accepts(RowOf(model, "a"), "manager"));
        Assert.True(model.Accepts(RowOf(model, "a"), "sun"));
    }

    [Fact]
    public void TheEdgesOfAnObjectRowAreAboveAndBelowItAndTheMiddleIsInside()
    {
        var (_, model, _) = Rig();
        var row = RowOf(model, "sun");

        Assert.Equal(DropKind.Before, OutlinerModel.BandAt(row, 0.1));
        Assert.Equal(DropKind.Inside, OutlinerModel.BandAt(row, 0.5));
        Assert.Equal(DropKind.After, OutlinerModel.BandAt(row, 0.9));
    }

    [Fact]
    public void ASceneRowOnlyTakesThingsIn()
    {
        var (_, model, _) = Rig();
        var row = RowOf(model, "a");

        Assert.Equal(DropKind.Inside, OutlinerModel.BandAt(row, 0.1));
        Assert.Equal(DropKind.Inside, OutlinerModel.BandAt(row, 0.9));
    }

    // Nothing in a scene can count how often it is read, so this is a budget and not a count. The scan
    // per object that Rows() used to make costs seconds at this size, and one pass costs a few
    // milliseconds, so the budget has room on both sides.
    [Fact]
    public void RowsAreBuiltInOnePass()
    {
        var objects = new List<SceneObject>();
        for (var group = 0; group < 200; group++)
        {
            objects.Add(new SceneObject(id: $"g{group}", name: $"G{group}", kind: ObjectKind.Group));
            for (var child = 0; child < 99; child++)
            {
                objects.Add(new SceneObject(id: $"g{group}c{child}", name: $"C{child}", kind: ObjectKind.Mesh, parentId: $"g{group}"));
            }
        }

        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: new EditorScene(objects)));
        var model = new OutlinerModel(workspace);

        var clock = Stopwatch.StartNew();
        var rows = model.Rows().Where(row => row.Entry.Id == "a").ToList();
        clock.Stop();

        Assert.Equal(20_001, rows.Count);
        Assert.Equal(["a", "g0", "g0c0", "g0c1"], rows.Take(4).Select(row => row.Key));
        Assert.Equal("g199c98", rows[^1].Key);
        Assert.True(clock.ElapsedMilliseconds < 400, $"Rows() took {clock.ElapsedMilliseconds} ms for 20,000 objects");
    }
}
