namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

// The session takes the distance already worked out, since the grid step belongs to the window, so these
// check what it does with it. EditorWindowKeysTests presses the keys.
public sealed class NudgeTests
{
    private static SceneObject Shape(ModellingRig rig) => rig.Session.ChosenShape!;

    [Fact]
    public void ANudgeMovesTheSelectedObjectByTheDistance()
    {
        using var rig = new ModellingRig(cube: true);
        var start = Shape(rig).Position.Clone();

        rig.Session.Nudge(new Vector3(0.5, 0, 0));

        Assert.Equal(start.X + 0.5, Shape(rig).Position.X, 9);
        Assert.Equal(start.Z, Shape(rig).Position.Z);
    }

    [Fact]
    public void EachNudgeIsItsOwnStepToUndo()
    {
        using var rig = new ModellingRig(cube: true);
        var start = Shape(rig).Position.X;

        rig.Session.Nudge(new Vector3(0.5, 0, 0));
        rig.Session.Nudge(new Vector3(0.5, 0, 0));
        Assert.Equal(start + 1, Shape(rig).Position.X, 9);
        rig.Session.Undo();

        Assert.Equal(start + 0.5, Shape(rig).Position.X, 9);
    }

    [Fact]
    public void ANudgeIsNamedForWhatItMoved()
    {
        using var rig = new ModellingRig(cube: true);

        rig.Session.Nudge(new Vector3(0, 0.5, 0));

        Assert.Equal($"Move {Shape(rig).Name}", rig.Session.History.UndoLabel);
    }

    [Fact]
    public void NothingSelectedMeansNothingMoves()
    {
        using var rig = new ModellingRig(cube: true);
        rig.Session.Select([]);
        var before = rig.Session.History.UndoLabel;

        rig.Session.Nudge(new Vector3(0.5, 0, 0));

        Assert.Equal(before, rig.Session.History.UndoLabel);
    }

    [Fact]
    public void TheSceneSettingsHaveNoPlaceToMove()
    {
        using var rig = new ModellingRig(cube: true);
        rig.Session.Add(ObjectKind.Scene);
        var settings = rig.Session.Primary!;
        var before = rig.Session.History.UndoLabel;

        rig.Session.Nudge(new Vector3(1, 0, 0));

        Assert.Equal(0, rig.Session.Workspace.Loaded!.Scene![settings]!.Position.X);
        Assert.Equal(before, rig.Session.History.UndoLabel);
    }

    [Fact]
    public void SeveralObjectsMoveTogetherAsOneStep()
    {
        using var rig = new ModellingRig(cube: true);
        var scene = rig.Session.Workspace.Loaded!.Scene!;
        var other = scene.Objects.First(one => one.Id != Shape(rig).Id && one.Kind != ObjectKind.Scene);
        var both = new[] { Shape(rig).Id, other.Id };
        var starts = both.Select(id => scene[id]!.Position.X).ToArray();
        rig.Session.Select(both);

        rig.Session.Nudge(new Vector3(1, 0, 0));

        Assert.Equal(starts[0] + 1, scene[both[0]]!.Position.X, 9);
        Assert.Equal(starts[1] + 1, scene[both[1]]!.Position.X, 9);
        Assert.Equal("Move 2 objects", rig.Session.History.UndoLabel);
        rig.Session.Undo();
        Assert.Equal(starts[0], scene[both[0]]!.Position.X, 9);
        Assert.Equal(starts[1], scene[both[1]]!.Position.X, 9);
    }

    [Fact]
    public void AShiftInTheWorldIsTheSameShiftInsideATurnedParent()
    {
        using var rig = new ModellingRig(cube: true);
        var session = rig.Session;
        var scene = session.Workspace.Loaded!.Scene!;
        var child = Shape(rig).Id;
        session.Add(ObjectKind.Group);
        var group = session.Primary!;
        scene[group]!.Rotation.SetFrom(new Vector3(0, 90, 0));
        session.Move(child, new Drop(session.Workspace.Loaded!.Id, group, 0));
        session.Select([child]);
        var before = scene.WorldOf(child).GetTranslation();

        session.Nudge(new Vector3(1, 0, 0));

        var after = scene.WorldOf(child).GetTranslation();
        Assert.Equal(before.X + 1, after.X, 9);
        Assert.Equal(before.Y, after.Y, 9);
        Assert.Equal(before.Z, after.Z, 9);
    }
}
