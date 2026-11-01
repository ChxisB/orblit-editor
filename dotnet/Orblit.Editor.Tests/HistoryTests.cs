namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

public sealed class HistoryTests
{
    private static readonly Colour Red = new(0xFFF44336);
    private static readonly Colour Blue = new(0xFF2196F3);
    private static readonly Colour Teal = new(0xFF009688);

    // One scene in a workspace, which is what a history edits through.
    private static (Workspace Workspace, EditorScene Scene, History History) Open()
    {
        var scene = EditorScene.Starter();
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        return (workspace, scene, new History(workspace));
    }

    private static (Workspace Workspace, History History) Listed()
    {
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: EditorScene.Starter()));
        workspace.Add(new SceneEntry("b", "B", path: "/project/scenes/b.oscene"));
        return (workspace, new History(workspace));
    }

    private static SetTransform Move(EditorScene scene, string id, Vector3 to) =>
        new("a", id, TransformField.Position, scene[id]!.Name, scene[id]!.Position, to);

    private static MoveObject Reparent(EditorScene scene, string id, string name, string? from, string? to) =>
        new("a", id, name, from, to, scene.IndexOf(id), scene.Roots.Count);

    private static List<string> NamesUnder(EditorScene scene, string? parent) =>
        [.. scene.SiblingsOf(parent).Select(one => one.Name)];

    [Fact]
    public void UndoPutsAValueBackWhereItStarted()
    {
        var (_, scene, history) = Open();
        history.Run(Move(scene, "cube", new Vector3(5, 0, 0)));

        Assert.Equal(5, scene["cube"]!.Position.X);
        history.Undo();
        Assert.Equal(0, scene["cube"]!.Position.X);
        history.Redo();
        Assert.Equal(5, scene["cube"]!.Position.X);
    }

    [Fact]
    public void UndoADragIsOneStepNotOnePerFrame()
    {
        var (_, scene, history) = Open();

        // What a slider sends: a command per pointer move.
        for (var i = 1; i <= 40; i++)
        {
            history.Run(Move(scene, "cube", new Vector3(i, 0, 0)));
        }

        Assert.Equal(["Move Cube"], history.Labels);
        history.Undo();

        // Back to before the drag began, not to the frame before it ended.
        Assert.Equal(0, scene["cube"]!.Position.X);
    }

    [Fact]
    public void UndoAWholeDragOfTheSwaySliderIsOneUndoStep()
    {
        var (_, scene, history) = Open();

        // What a drag actually sends: a command per frame the slider moved.
        for (var i = 1; i <= 20; i++)
        {
            history.Run(new SetSway("a", "cube", "Cube", scene["cube"]!.Sway, i / 20.0));
        }

        Assert.Equal(["Set Cube sway"], history.Labels);
        Assert.Equal(1, scene["cube"]!.Sway, 1e-9);

        history.Undo();

        // Back to rigid, which is where the drag began, not to the frame before it ended.
        Assert.Equal(0, scene["cube"]!.Sway);
    }

    [Fact]
    public void UndoSealingEndsTheRunSoTwoDragsAreTwoSteps()
    {
        var (_, scene, history) = Open();
        history.Run(Move(scene, "cube", new Vector3(1, 0, 0)));
        history.Seal();
        history.Run(Move(scene, "cube", new Vector3(2, 0, 0)));

        Assert.Equal(2, history.Labels.Count);
        history.Undo();
        Assert.Equal(1, scene["cube"]!.Position.X);
    }

    [Fact]
    public void UndoADifferentFieldStartsItsOwnStep()
    {
        var (_, scene, history) = Open();
        history.Run(Move(scene, "cube", new Vector3(1, 0, 0)));
        history.Run(new SetTransform("a", "cube", TransformField.Scale, "Cube", scene["cube"]!.Scale, new Vector3(2, 2, 2)));

        Assert.Equal(["Move Cube", "Scale Cube"], history.Labels);
    }

    [Fact]
    public void UndoDoingSomethingNewDiscardsTheRedoStack()
    {
        var (_, scene, history) = Open();
        history.Run(Move(scene, "cube", new Vector3(5, 0, 0)));

        history.Undo();
        Assert.True(history.CanRedo);

        history.Run(new SetColour("a", "cube", "Cube", Red, Blue));
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UndoNamesTheStepSoTheMenuCanSayWhatItUndoes()
    {
        var (_, scene, history) = Open();
        history.Run(Move(scene, "cube", new Vector3(5, 0, 0)));

        Assert.Equal("Move Cube", history.UndoLabel);
        history.Undo();
        Assert.Null(history.UndoLabel);
        Assert.Equal("Move Cube", history.RedoLabel);
    }

    [Fact]
    public void UndoARefusedEditLeavesTheStackAlone()
    {
        var (_, scene, history) = Open();
        history.Run(Move(scene, "cube", new Vector3(5, 0, 0)));
        history.Undo();

        // Refused: a thing cannot contain its own parent.
        Assert.Throws<SceneException>(() => history.Run(new MoveObject("a", "props", "Props", null, "cube", 0, 0)));

        // The redo that was there before the failed edit is still there.
        Assert.True(history.CanRedo);
        Assert.Empty(history.Labels);
    }

    [Fact]
    public void UndoForgetsTheOldestStepsRatherThanGrowingWithoutLimit()
    {
        var (_, scene, history) = Open();

        for (var i = 0; i < History.Limit + 20; i++)
        {
            history.Run(Move(scene, "cube", new Vector3(i, 0, 0)));
            history.Seal();
        }

        Assert.Equal(History.Limit, history.Labels.Count);
    }

    [Fact]
    public void DeletingTakesTheChildrenWithItAndBringsThemBack()
    {
        var (_, scene, history) = Open();
        history.Run(new DeleteObjects("a", ["props"], "Props"));

        Assert.False(scene.Contains("props"));
        Assert.False(scene.Contains("cube"), "a child was left behind");
        Assert.False(scene.Contains("crate"));

        history.Undo();
        Assert.True(scene.Contains("cube"));
        Assert.Equal("props", scene["cube"]!.ParentId);
    }

    [Fact]
    public void DeletingPutsThingsBackInTheOrderTheyWereIn()
    {
        var (_, scene, history) = Open();
        var before = scene.Objects.Select(one => one.Id).ToList();

        history.Run(new DeleteObjects("a", ["props"], "Props"));
        history.Undo();

        Assert.Equal(before, scene.Objects.Select(one => one.Id));
    }

    [Fact]
    public void ReparentingDoesNotMoveTheObjectOnScreen()
    {
        var (_, scene, history) = Open();

        // Props is at the origin, so give it a transform worth inheriting.
        scene["props"]!.Position.SetValues(3, 1, -2);
        scene["props"]!.Rotation.SetValues(0, 40, 0);
        scene.Invalidate();

        var before = scene.WorldOf("crate").Clone();

        history.Run(Reparent(scene, "crate", "Crate", "props", null));

        var after = scene.WorldOf("crate");
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(before[i], after[i], 1e-9);
        }
    }

    [Fact]
    public void ReparentingUndoReturnsBothTheParentAndTheLocalTransform()
    {
        var (_, scene, history) = Open();
        scene["props"]!.Position.SetValues(3, 1, -2);
        scene.Invalidate();

        var localBefore = scene["crate"]!.Position.Clone();

        history.Run(Reparent(scene, "crate", "Crate", "props", null));
        history.Undo();

        Assert.Equal("props", scene["crate"]!.ParentId);
        Assert.True((scene["crate"]!.Position - localBefore).Length < 1e-9);
    }

    [Fact]
    public void ReparentingRefusesToPutAThingInsideItsOwnChild()
    {
        var (_, scene, history) = Open();

        Assert.Throws<SceneException>(() => history.Run(new MoveObject("a", "props", "Props", null, "cube", 0, 0)));

        // And the tree is untouched, rather than half-moved.
        Assert.Null(scene["props"]!.ParentId);
        Assert.Equal("props", scene["cube"]!.ParentId);
    }

    [Fact]
    public void TheHierarchyAChildInheritsItsParentTransform()
    {
        var scene = EditorScene.Starter();
        scene["props"]!.Position.SetValues(10, 0, 0);
        scene.Invalidate();

        Assert.Equal(10, scene.WorldOf("cube").GetTranslation().X);
    }

    [Fact]
    public void TheHierarchyWorldMatricesAreRecomputedAfterAnEditNotCachedStale()
    {
        var scene = EditorScene.Starter();
        Assert.Equal(0, scene.WorldOf("cube").GetTranslation().X);

        scene["props"]!.Position.SetValues(4, 0, 0);
        scene.Invalidate();

        Assert.Equal(4, scene.WorldOf("cube").GetTranslation().X);
    }

    [Fact]
    public void TheHierarchyDepthIsWhatTheOutlinerIndentsBy()
    {
        var scene = EditorScene.Starter();
        Assert.Equal(0, scene.DepthOf("props"));
        Assert.Equal(1, scene.DepthOf("cube"));
    }

    [Fact]
    public void ReorderingMovesAThingAmongItsSiblingsWithoutChangingItsParent()
    {
        var (_, scene, history) = Open();
        Assert.Equal(["Cube", "Crate"], NamesUnder(scene, "props"));

        history.Run(new MoveObject("a", "crate", "Crate", "props", "props", 1, 0));

        Assert.Equal(["Crate", "Cube"], NamesUnder(scene, "props"));
        Assert.Equal("props", scene["crate"]!.ParentId);
    }

    [Fact]
    public void ReorderingAReorderDoesNotMoveTheObjectInTheWorld()
    {
        var (_, scene, history) = Open();
        var before = scene.WorldOf("crate").Clone();

        history.Run(new MoveObject("a", "crate", "Crate", "props", "props", 1, 0));

        // Only reparenting rewrites the transform; a reorder must not, or every drag in the tree
        // would nudge things by a rounding error.
        var after = scene.WorldOf("crate");
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(before[i], after[i]);
        }
    }

    [Fact]
    public void ReorderingUndoPutsItBackInTheOrderItWasIn()
    {
        var (_, scene, history) = Open();

        history.Run(new MoveObject("a", "crate", "Crate", "props", "props", 1, 0));
        history.Undo();

        Assert.Equal(["Cube", "Crate"], NamesUnder(scene, "props"));
    }

    [Fact]
    public void ReorderingASubtreeKeepsItsChildrenWhenItMoves()
    {
        var (_, scene, history) = Open();

        history.Run(new MoveObject("a", "props", "Props", null, null, scene.IndexOf("props"), 0));

        Assert.Equal("Props", scene.SiblingsOf(null)[0].Name);
        Assert.Equal(2, scene.ChildrenOf("props").Count);
    }

    [Fact]
    public void ReorderingMovingToTheEndLandsAfterASiblingAndItsChildren()
    {
        var (_, scene, history) = Open();

        // Sun to the very end, past Props and everything nested in it.
        history.Run(new MoveObject("a", "sun", "Sun", null, null, 0, scene.Roots.Count));

        Assert.Equal("Sun", NamesUnder(scene, null)[^1]);

        // And Props still owns its children rather than having Sun inserted among them.
        Assert.Equal(2, scene.ChildrenOf("props").Count);
    }

    [Fact]
    public void MoreThanOneSceneOnlyOneHoldsADocumentAtATime()
    {
        var (workspace, _) = Listed();

        Assert.True(workspace["a"]!.IsLoaded);
        Assert.False(workspace["b"]!.IsLoaded);
        Assert.Equal("a", workspace.Loaded!.Id);
    }

    [Fact]
    public void MoreThanOneSceneLoadingOneEmptiesTheOther()
    {
        var (workspace, _) = Listed();
        workspace.Load(workspace["b"]!, EditorScene.Starter());

        // Nothing is left of the first but its name and its path, so there is never a question
        // about which scene an edit or a draw belongs to.
        Assert.False(workspace["a"]!.IsLoaded);
        Assert.True(workspace["b"]!.IsLoaded);
        Assert.Equal("b", workspace.Loaded!.Id);
    }

    [Fact]
    public void MoreThanOneSceneACommandForAnUnloadedSceneDoesNothingRatherThanThrow()
    {
        var (_, history) = Listed();

        // What happens if a step outlives the scene it belongs to.
        history.Run(new SetColour("b", "cube", "Cube", Red, Blue));

        Assert.True(history.CanUndo);
        history.Undo();
    }

    [Fact]
    public void MoreThanOneSceneUnloadingForgetsItsStepsSoUndoCannotReachIntoIt()
    {
        var (workspace, history) = Listed();
        var a = workspace["a"]!;

        history.Run(new SetColour("a", "cube", "Cube", a.Scene!["cube"]!.Colour, Red));
        Assert.True(history.CanUndo);

        // Undoing into a scene that is not loaded would be a step that appears to do nothing.
        history.Forget("a");
        workspace.Unload(a);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void MoreThanOneSceneAnUnloadedSceneIsNeverReportedAsHavingChanges()
    {
        var (workspace, history) = Listed();
        var b = workspace["b"]!;

        // It holds nothing to change, so there is nothing to warn about.
        Assert.False(b.IsLoaded);
        Assert.Equal(b.SavedStamp, history.StampFor("b"));
    }

    [Fact]
    public void MoreThanOneSceneObjectsAreOnlyFoundInTheLoadedScene()
    {
        var (workspace, _) = Listed();
        Assert.Equal("a", workspace.SceneHolding("cube")?.Id);

        workspace.Unload(workspace["a"]!);
        Assert.Null(workspace.SceneHolding("cube"));
    }

    [Fact]
    public void MoreThanOneSceneANameNothingElseIsUsing()
    {
        var (workspace, _) = Listed();

        Assert.Equal("A 2", workspace.AvailableName("A"));
        Assert.Equal("C", workspace.AvailableName("C"));
    }

    [Fact]
    public void SceneSettingsTheSkyIsPartOfTheSceneAndUndoesWithEverythingElse()
    {
        var (_, scene, history) = Open();
        var was = scene.SkyColour;

        history.Run(new SetSceneSky("a", was, Teal, scene.Ambient, 50000));

        Assert.Equal(Teal, scene.SkyColour);
        Assert.Equal(50000, scene.Ambient);

        history.Undo();
        Assert.Equal(was, scene.SkyColour);
    }

    [Fact]
    public void SceneSettingsDraggingTheAmbientSliderIsOneStep()
    {
        var (_, scene, history) = Open();

        for (var i = 1; i <= 20; i++)
        {
            history.Run(new SetSceneSky("a", scene.SkyColour, scene.SkyColour, 28000, 28000 + (i * 100)));
        }

        Assert.Equal(["Change the sky"], history.Labels);
        history.Undo();
        Assert.Equal(28000, scene.Ambient);
    }

    [Fact]
    public void SceneSettingsNamingTheLayersIsUndoableAndTypingIsOneStep()
    {
        var (_, scene, history) = Open();
        foreach (var name in new[] { "P", "Pl", "Player" })
        {
            history.Run(new SetLayerNames("a", scene.LayerNames, [name]));
        }

        Assert.Equal(["Name the layers"], history.Labels);
        Assert.Equal(["Player"], scene.LayerNames);
        history.Undo();
        Assert.Empty(scene.LayerNames);
        history.Redo();
        Assert.Equal(["Player"], scene.LayerNames);
    }

    [Fact]
    public void SceneSettingsRenamingTheSceneIsUndoableLikeAnythingElse()
    {
        var (_, scene, history) = Open();

        history.Run(new RenameScene("a", "Scene", "Level 1"));
        Assert.Equal("Level 1", scene.Name);
        history.Undo();
        Assert.Equal("Scene", scene.Name);
    }
}
