namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

// These are the session's own methods. UvPanelTests presses the buttons.
public sealed class UvSessionTests : IDisposable
{
    private readonly UvRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private EditorSession Session => _rig.Session;

    [Fact]
    public void FreezingTurnsTheRuleIntoTheCoordinatesItGave()
    {
        _rig.Select(0);
        var before = _rig.Corners(0);

        Session.RunUvAction(UvAction.Freeze);

        Assert.True(_rig.Mesh.Faces[0].Uv.IsManual);
        // Exactly what the rule said, or freezing would move the texture the moment somebody reached for it.
        var now = _rig.Corners(0);
        Assert.All(before.Zip(now), pair =>
        {
            Assert.Equal(pair.First.X, pair.Second.X, 1e-9);
            Assert.Equal(pair.First.Y, pair.Second.Y, 1e-9);
        });
    }

    [Fact]
    public void ReleasingGoesBackToTheRule()
    {
        _rig.Select(0);
        Session.RunUvAction(UvAction.Freeze);

        Session.RunUvAction(UvAction.Release);

        Assert.False(_rig.Mesh.Faces[0].Uv.IsManual);
    }

    [Fact]
    public void AMovingDragIsOneStepToUndoHoweverManyFramesItRuns()
    {
        _rig.Select(0);
        var before = _rig.Corners(0)[0].X;

        for (var frame = 0; frame < 4; frame++)
        {
            Session.NudgeUvs(new Vector2(0.1, 0));
        }

        Session.EndUvRun();
        Assert.Equal(before + 0.4, _rig.Corners(0)[0].X, 1e-9);

        Session.Undo();

        // The whole drag, not its last frame.
        Assert.Equal(before, _rig.Corners(0)[0].X, 1e-9);
    }

    [Fact]
    public void ScalingAndTurningAreDragsToo()
    {
        _rig.Select(0);
        Session.RunUvAction(UvAction.Fit);
        var fitted = _rig.Corners(0);

        Session.ScaleUvs(new Vector2(2, 2));
        Session.ScaleUvs(new Vector2(1.5, 1.5));
        Session.TurnUvs(10);
        Session.TurnUvs(20);
        Session.EndUvRun();
        Session.Undo();

        // Both kinds of frame are one step, so one undo is back at the fit and no further.
        Assert.StartsWith("Fit", Session.History.UndoLabel);
        Assert.Equal(fitted[0].X, _rig.Corners(0)[0].X, 1e-9);
    }

    [Fact]
    public void TwoPressesOfAButtonAreTwoThingsToUndo()
    {
        _rig.Select(0, 1);

        Session.RunUvAction(UvAction.Box);
        Session.RunUvAction(UvAction.Fit);

        Assert.Equal(0, _rig.Mesh.UvBoundsOf(_rig.Mesh.Faces.Take(2))!.Value.Min.X, 1e-9);

        Session.Undo();

        // Back to the projection, not back to the beginning.
        Assert.True(_rig.Mesh.Faces[0].Uv.IsManual);
        Assert.NotEqual(0, _rig.Mesh.UvBoundsOf(_rig.Mesh.Faces.Take(2))!.Value.Min.X, 1e-9);
    }

    [Fact]
    public void ASliderIsOneStepAndTwoClicksAreTwo()
    {
        _rig.Select(0);
        var rule = _rig.Mesh.Faces[0].Uv;

        Session.SetUvRule(rule.CopyWith(rotation: 10), live: true);
        Session.SetUvRule(rule.CopyWith(rotation: 20), live: true);
        Session.Undo();
        Assert.Equal(0, _rig.Mesh.Faces[0].Uv.Rotation);

        Session.SetUvRule(rule.CopyWith(flipU: true), live: false);
        Session.SetUvRule(rule.CopyWith(swap: true), live: false);
        Session.Undo();
        Assert.True(_rig.Mesh.Faces[0].Uv.FlipU);
        Assert.False(_rig.Mesh.Faces[0].Uv.Swap);
    }

    [Fact]
    public void NothingHappensWithNoFacesSelected()
    {
        var label = Session.History.UndoLabel;

        Session.RunUvAction(UvAction.Freeze);
        Session.NudgeUvs(new Vector2(1, 0));

        Assert.Equal(label, Session.History.UndoLabel);
        Assert.False(_rig.Mesh.Faces[0].Uv.IsManual);
    }

    [Fact]
    public void FacesNeedFaceModeOrThereAreNoCoordinatesToEdit()
    {
        _rig.Select(0);
        Session.SetElementMode(ElementMode.Edge);
        var label = Session.History.UndoLabel;

        Session.RunUvAction(UvAction.Freeze);

        Assert.True(Session.UvWrongMode);
        Assert.True(Session.UvSelection.IsEmpty);
        Assert.Equal(label, Session.History.UndoLabel);
    }

    [Fact]
    public void TheModeIsOnlyWrongWhileEditingParts()
    {
        Session.SetElementMode(ElementMode.Vertex);
        Assert.True(Session.UvWrongMode);

        Session.SetContext(EditContext.Whole);

        Assert.False(Session.UvWrongMode);
    }

    [Fact]
    public void PartsStartInFaceMode()
    {
        Assert.Equal(ElementMode.Face, Session.ElementMode);
        Assert.False(Session.UvWrongMode);
    }

    [Fact]
    public void ThereIsNoMeshToShowWithoutAShape()
    {
        var empty = new EditorSession(new Workspace("/project"), new History(new Workspace("/project")), EditorModes.Standard(), PanelTypes.BuiltIn());

        Assert.Null(empty.UvMesh);
    }
}
