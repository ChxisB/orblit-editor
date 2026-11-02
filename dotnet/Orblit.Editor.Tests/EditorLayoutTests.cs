namespace Orblit.Editor.Tests;

using System.Text.Json;
using Orblit.Editor.Core;
using Orblit.Scene;
using Orblit.VectorMath;
using Doc = Orblit.Scene;
using SceneDocument = Orblit.Editor.Core.SceneDocument;
using SetVisible = Orblit.Editor.Core.SetVisible;

public sealed class EditorLayoutTests
{
    private static readonly Vector3 Raised = new(0, 1, 0);

    // One scene in a workspace, which is what a history edits through.
    private static (EditorScene Scene, History History) Open(EditorScene? scene = null)
    {
        var held = scene ?? EditorScene.Starter();
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: held));
        return (held, new History(workspace));
    }

    private static void Show(History history, EditorScene scene, LayoutShape? shape) =>
        history.Run(new SwitchLayout("a", scene.Layouts.Active, shape));

    private static void Move(History history, EditorScene scene, string id, Vector3 to) =>
        history.Run(new SetTransform("a", id, TransformField.Position, scene[id]!.Name, scene[id]!.Position, to));

    private static Doc.SceneDocument Stored(EditorScene scene) =>
        Doc.SceneDocument.Decode(SceneDocument.Encode(scene)).Document;

    private static Vector3 CrateIn(Doc.SceneDocument document) =>
        ((TransformComponent)document["crate"]![SceneComponents.Transform]!).Position;

    private static double LensIn(Doc.SceneDocument document) =>
        ((CameraComponent)document["camera"]![SceneComponents.Camera]!).FieldOfView;

    private static EditorScene WithPortraitLens(double fieldOfView)
    {
        var plain = Stored(EditorScene.Starter());
        var lens = new SetField("camera", SceneComponents.Camera, "fieldOfView", 50.0, fieldOfView);
        var text = plain.CopyWith(layouts: SceneLayouts.None.With(LayoutShape.Portrait, new SceneDiff([lens]))).Encode();
        return SceneDocument.Decode(text).Scene;
    }

    [Fact]
    public void ASceneIsShownAsTheBaseUntilALayoutIsChosen()
    {
        var (scene, _) = Open();

        Assert.Null(scene.Layouts.Active);
    }

    [Fact]
    public void AMoveInALayoutLeavesTheBaseWhereItWas()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        Show(history, scene, null);

        Assert.Equal(2.2, scene["crate"]!.Position.X);
    }

    [Fact]
    public void ALayoutShowsItsMovesAgainOnReturning()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        Show(history, scene, null);

        Show(history, scene, LayoutShape.Portrait);

        Assert.Equal(1, scene["crate"]!.Position.Y);
    }

    [Fact]
    public void EachLayoutKeepsItsOwnMoves()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        Show(history, scene, LayoutShape.Landscape);

        Assert.Equal(2.2, scene["crate"]!.Position.X);
        Assert.Equal(LayoutShape.Landscape, scene.Layouts.Active);
    }

    [Fact]
    public void AChangeToTheBaseReachesALayoutThatDidNotTouchIt()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        Show(history, scene, null);
        history.Run(new SetSway("a", "cube", "Cube", 0, 0.5));

        Show(history, scene, LayoutShape.Portrait);

        Assert.Equal(0.5, scene["cube"]!.Sway);
    }

    [Fact]
    public void AMoveBackToTheBaseValueLeavesNothingInTheLayout()
    {
        var (scene, history) = Open();
        var start = scene["crate"]!.Position;
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        history.Seal();
        Move(history, scene, "crate", start);

        Show(history, scene, null);

        Assert.True(scene.Layouts.Held.IsEmpty);
    }

    [Fact]
    public void HidingAnObjectInALayoutHidesItThereOnly()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Landscape);
        history.Run(new SetVisible("a", "crate", "Crate", false));
        Assert.False(scene["crate"]!.Visible);

        Show(history, scene, LayoutShape.Portrait);

        Assert.True(scene["crate"]!.Visible);
    }

    [Fact]
    public void UndoStepsBackThroughTheEditAndThenTheSwitch()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        Show(history, scene, null);

        history.Undo();
        Assert.Equal(LayoutShape.Portrait, scene.Layouts.Active);
        Assert.Equal(1, scene["crate"]!.Position.Y);

        history.Undo();
        Assert.Equal(-0.65, scene["crate"]!.Position.Y);

        history.Undo();
        Assert.Null(scene.Layouts.Active);

        history.Redo();
        history.Redo();
        history.Redo();
        Assert.Null(scene.Layouts.Active);
        Show(history, scene, LayoutShape.Portrait);
        Assert.Equal(1, scene["crate"]!.Position.Y);
    }

    [Fact]
    public void SwitchingAloneDoesNotMarkTheSceneChanged()
    {
        var (scene, history) = Open();
        var saved = history.StampFor("a");

        Show(history, scene, LayoutShape.Portrait);
        Show(history, scene, null);

        Assert.Equal(saved, history.StampFor("a"));
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void AnEditInALayoutMarksTheSceneChanged()
    {
        var (scene, history) = Open();
        var saved = history.StampFor("a");
        Show(history, scene, LayoutShape.Portrait);

        Move(history, scene, "crate", Raised);

        Assert.NotEqual(saved, history.StampFor("a"));
    }

    [Fact]
    public void AnObjectCannotBeAddedInALayout()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        var refused = new List<string>();
        history.Refused += command => refused.Add(command.Label);

        history.Run(new AddObject(new SceneObject("extra", "Extra", ObjectKind.Group), "a"));

        Assert.Null(scene["extra"]);
        Assert.Single(refused);
        Assert.Equal("Switch to Portrait", history.UndoLabel);
    }

    [Fact]
    public void AnObjectCannotBeDeletedInALayout()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Landscape);

        history.Run(new DeleteObjects("a", ["crate"], "Crate"));

        Assert.NotNull(scene["crate"]);
    }

    [Fact]
    public void AnObjectCannotBeRenamedInALayout()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Landscape);

        history.Run(new Rename("a", "crate", "Crate", "Box"));

        Assert.Equal("Crate", scene["crate"]!.Name);
    }

    [Fact]
    public void TheSceneSettingsCannotBeChangedInALayout()
    {
        var (scene, history) = Open();
        var hour = scene.TimeOfDay;
        Show(history, scene, LayoutShape.Landscape);

        history.Run(new SetSceneTime("a", new SceneTime(hour, false, 0.5), new SceneTime(3, false, 0.5)));

        Assert.Equal(hour, scene.TimeOfDay);
    }

    [Fact]
    public void TheSameEditsRunAsUsualInTheBase()
    {
        var (scene, history) = Open();

        history.Run(new Rename("a", "crate", "Crate", "Box"));

        Assert.Equal("Box", scene["crate"]!.Name);
    }

    [Fact]
    public void AnEditNoLayoutCanHoldIsCountedAndNotWritten()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        history.Run(new RenameWithoutAsking("a", "crate", "Box"));

        Assert.Equal(1, scene.Layouts.CountLost(scene));
        Show(history, scene, null);

        Assert.True(scene.Layouts.Held.IsEmpty);
    }

    [Fact]
    public void ASceneWithNoLayoutsIsWrittenAsTheOlderVersion()
    {
        var text = SceneDocument.Encode(EditorScene.Starter());

        using var parsed = JsonDocument.Parse(text);

        Assert.Equal(Doc.SceneDocument.PlainVersion, parsed.RootElement.GetProperty("formatVersion").GetInt32());
    }

    [Fact]
    public void ASceneWithLayoutsIsWrittenAsTheNewVersion()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        using var parsed = JsonDocument.Parse(SceneDocument.Encode(scene));

        Assert.Equal(Doc.SceneDocument.FormatVersion, parsed.RootElement.GetProperty("formatVersion").GetInt32());
    }

    [Fact]
    public void SavingInALayoutWritesTheBaseAndTheLayout()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        var stored = Stored(scene);

        Assert.Equal(2.2, CrateIn(stored).X);
        Assert.Equal(1, CrateIn(stored.InLayout(LayoutShape.Portrait)).Y);
        Assert.Equal(-0.65, CrateIn(stored.InLayout(LayoutShape.Landscape)).Y);
    }

    [Fact]
    public void SavingInTheBaseKeepsTheLayoutsMadeEarlier()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        Show(history, scene, null);

        var stored = Stored(scene);

        Assert.Equal(1, CrateIn(stored.InLayout(LayoutShape.Portrait)).Y);
    }

    [Fact]
    public void ASceneOpensWithTheLayoutsItWasSavedWith()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        var reopened = SceneDocument.Decode(SceneDocument.Encode(scene)).Scene;
        var (_, again) = Open(reopened);

        Assert.Null(reopened.Layouts.Active);
        Assert.Equal(2.2, reopened["crate"]!.Position.X);
        Show(again, reopened, LayoutShape.Portrait);
        Assert.Equal(1, reopened["crate"]!.Position.Y);
    }

    [Fact]
    public void AnEditTheEditorHasNoRowForSurvivesWithoutBeingTouched()
    {
        var scene = WithPortraitLens(70);
        var (_, history) = Open(scene);

        Show(history, scene, LayoutShape.Portrait);
        Show(history, scene, null);

        Assert.Equal(70, LensIn(Stored(scene).InLayout(LayoutShape.Portrait)));
    }

    [Fact]
    public void AnEditTheEditorHasNoRowForSurvivesAnotherEditInTheSameLayout()
    {
        var scene = WithPortraitLens(70);
        var (_, history) = Open(scene);
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        var stored = Stored(scene).InLayout(LayoutShape.Portrait);

        Assert.Equal(70, LensIn(stored));
        Assert.Equal(1, CrateIn(stored).Y);
    }

    [Fact]
    public void ACopyForPlayingCarriesTheLayouts()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        var copy = SceneDocument.Decode(SceneDocument.Encode(scene)).Scene;

        Assert.False(copy.Layouts.Held.IsEmpty);
    }

    [Fact]
    public void ALayoutEditForAnObjectTheSceneLacksIsSaidWhenTheSceneOpens()
    {
        var plain = Stored(EditorScene.Starter());
        var gone = new SetField("gone", SceneComponents.Camera, "fieldOfView", 50.0, 40.0);
        var text = plain.CopyWith(layouts: SceneLayouts.None.With(LayoutShape.Portrait, new SceneDiff([gone]))).Encode();

        var load = SceneDocument.Decode(text);

        Assert.Equal(["The portrait layout has 1 edit for something the scene does not have. It changes nothing."], load.Problems);
    }

    [Fact]
    public void ALayoutWhoseEditsAllLandSaysNothingWhenTheSceneOpens()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        var load = SceneDocument.Decode(SceneDocument.Encode(scene));

        Assert.Empty(load.Problems);
    }

    [Fact]
    public void AMovedObjectNamesWhatTheLayoutOverrides()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        Assert.Equal(["Position"], scene.Layouts.Overrides(scene, "crate"));
    }

    [Fact]
    public void AHiddenObjectNamesVisibleAsWhatTheLayoutOverrides()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        history.Run(new SetVisible("a", "crate", "Crate", false));

        Assert.Equal(["Visible"], scene.Layouts.Overrides(scene, "crate"));
    }

    [Fact]
    public void AnObjectTheLayoutLeavesAloneOverridesNothing()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        Assert.Empty(scene.Layouts.Overrides(scene, "camera"));
    }

    [Fact]
    public void TheBaseOverridesNothing()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        Show(history, scene, null);

        Assert.Empty(scene.Layouts.Overrides(scene, "crate"));
    }

    [Fact]
    public void AValueMovedBackToTheBaseIsNoOverride()
    {
        var (scene, history) = Open();
        var home = scene["crate"]!.Position;
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        Move(history, scene, "crate", home);

        Assert.Empty(scene.Layouts.Overrides(scene, "crate"));
    }

    [Fact]
    public void AnEditWrittenByHandIsNamedInWords()
    {
        var scene = WithPortraitLens(70);
        var (_, history) = Open(scene);
        Show(history, scene, LayoutShape.Portrait);

        Assert.Equal(["Field of view"], scene.Layouts.Overrides(scene, "camera"));
    }

    [Fact]
    public void ResettingAnObjectPutsItBackToTheBaseInTheLayout()
    {
        var (scene, history) = Open();
        var home = scene["crate"]!.Position;
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);

        history.Run(new ResetToBase("a", "crate", "Crate"));

        Assert.Equal(home, scene["crate"]!.Position);
        Assert.Empty(scene.Layouts.Overrides(scene, "crate"));
    }

    [Fact]
    public void ResettingOneObjectKeepsWhatTheLayoutDoesToAnother()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        Move(history, scene, "camera", new Vector3(0, 9, 9));

        history.Run(new ResetToBase("a", "crate", "Crate"));

        Assert.Equal(["Position"], scene.Layouts.Overrides(scene, "camera"));
        Assert.Equal(9, scene["camera"]!.Position.Y);
    }

    [Fact]
    public void ResettingAnObjectDropsAnEditWrittenByHandToo()
    {
        var scene = WithPortraitLens(70);
        var (_, history) = Open(scene);
        Show(history, scene, LayoutShape.Portrait);

        history.Run(new ResetToBase("a", "camera", "Camera"));

        Assert.Empty(scene.Layouts.Overrides(scene, "camera"));
        Assert.True(Stored(scene).Layouts.IsEmpty);
    }

    [Fact]
    public void UndoPutsTheOverridesBackAndRedoDropsThemAgain()
    {
        var (scene, history) = Open();
        var home = scene["crate"]!.Position;
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        history.Run(new ResetToBase("a", "crate", "Crate"));

        history.Undo();
        Assert.Equal(1, scene["crate"]!.Position.Y);
        Assert.Equal(["Position"], scene.Layouts.Overrides(scene, "crate"));

        history.Redo();
        Assert.Equal(home, scene["crate"]!.Position);
        Assert.Empty(scene.Layouts.Overrides(scene, "crate"));
    }

    [Fact]
    public void ResettingChangesWhatIsSaved()
    {
        var (scene, history) = Open();
        Show(history, scene, LayoutShape.Portrait);
        Move(history, scene, "crate", Raised);
        history.Run(new ResetToBase("a", "crate", "Crate"));

        Assert.True(Stored(scene).Layouts.IsEmpty);
        Assert.Equal("Reset Crate to base", history.UndoLabel);
    }

    // Says it fits a layout and then renames the object, which a layout has no way to write down.
    private sealed class RenameWithoutAsking(string sceneId, string id, string to) : EditorCommand
    {
        public override string SceneId { get; } = sceneId;

        public override string Label => "Rename quietly";

        public override bool FitsLayout => true;

        public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[id]?.Name = to;

        public override void Revert(ISceneHost host)
        {
        }
    }
}
