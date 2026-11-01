namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Weather;

// The scene fields' model, tested apart from the panel.
public sealed class SceneFieldsModelTests
{
    [Fact]
    public void TheTimeIsWhatTheSceneIsSetTo()
    {
        var scene = EditorScene.Starter();
        scene.TimeOfDay = 7.5;
        scene.DayCycle = true;
        scene.HoursPerSecond = 2;

        Assert.Equal(new SceneTime(7.5, true, 2), SceneFieldsModel.TimeOf(scene));
    }

    [Fact]
    public void ASceneWithNoFileSaysSoAndOneWithAFileNamesIt()
    {
        var unsaved = new SceneEntry("a", "A", scene: EditorScene.Starter());
        var saved = new SceneEntry("b", "B", path: "/project/scenes/main.oscene", scene: EditorScene.Starter());

        Assert.Equal("Not saved to a file yet", SceneFieldsModel.FileLine(unsaved));
        Assert.Equal("main.oscene", SceneFieldsModel.FileLine(saved));
        Assert.Equal("Never saved", SceneFieldsModel.WhereLine(unsaved));
        Assert.Equal("/project/scenes/main.oscene", SceneFieldsModel.WhereLine(saved));
    }

    [Fact]
    public void NowReadsTheClockAndTheBodyThatIsUp()
    {
        var scene = EditorScene.Starter();
        scene.TimeOfDay = 13.5;

        Assert.Equal("13:30  Sun", SceneFieldsModel.Now(scene));
    }

    [Fact]
    public void TheTimeNoteSaysWhetherTheTimeRuns()
    {
        Assert.StartsWith("The time runs", SceneFieldsModel.TimeNote(cycle: true), StringComparison.Ordinal);
        Assert.StartsWith("The scene sits", SceneFieldsModel.TimeNote(cycle: false), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCountsAreEveryObjectThoseDrawnAndTheLights()
    {
        var scene = new EditorScene(
        [
            new SceneObject(id: "a", name: "A", kind: ObjectKind.Mesh),
            new SceneObject(id: "b", name: "B", kind: ObjectKind.Shape),
            new SceneObject(id: "c", name: "C", kind: ObjectKind.Light),
            new SceneObject(id: "d", name: "D", kind: ObjectKind.Group),
        ]);

        Assert.Equal((4, 2, 1), SceneFieldsModel.Counts(scene));
    }
}
