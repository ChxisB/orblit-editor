namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;

public sealed class ModellingSessionTests : IDisposable
{
    private readonly ModellingRig _rig = new(cube: true);

    public void Dispose() => _rig.Dispose();

    private EditorSession Session => _rig.Session;

    private static MeshAction Action(string label) => MeshTools.All.Single(one => one.Label == label);

    [Fact]
    public void AnActionChangesTheMeshAndSelectsWhatItMadeAndUndoes()
    {
        var before = _rig.Mesh.Faces.Count;
        _rig.Edit(0);

        Session.RunMeshAction(Action("Extrude"));

        Assert.True(_rig.Mesh.Faces.Count > before);
        Assert.NotEmpty(Session.Elements.Faces);
        Assert.Equal(ElementMode.Face, Session.ElementMode);
        Assert.StartsWith("Extrude", Session.History.UndoLabel);

        Session.History.Undo();

        Assert.Equal(before, _rig.Mesh.Faces.Count);
    }

    [Fact]
    public void AnActionOnTheWholeShapeNeedsNoSelection()
    {
        var before = _rig.Mesh.Faces.Count;

        Session.RunMeshAction(Action("Subdivide all"));

        Assert.True(_rig.Mesh.Faces.Count > before);
    }

    [Fact]
    public void AnActionDoesNothingWithoutAShape()
    {
        using var empty = new ModellingRig();

        empty.Session.RunMeshAction(Action("Subdivide all"));

        Assert.False(empty.Session.History.CanUndo);
    }

    [Fact]
    public void AnActionTakesItsAmountFromWhatWasSetOrWhatItStartsWith()
    {
        var extrude = Action("Extrude");
        Assert.Equal(0.5, Session.AmountOf(extrude));

        Session.SetAmount(extrude, 2);

        Assert.Equal(2, Session.AmountOf(extrude));
        Assert.Equal(0.15, Session.AmountOf(Action("Inset")));
    }

    [Fact]
    public void AnActionWithoutAnAmountReadsOne()
    {
        Assert.Equal(1, Session.AmountOf(Action("Delete")));
    }

    [Fact]
    public void SeeThroughIsSaidToTheViewsAndKept()
    {
        var said = 0;
        Session.Changed += () => said++;

        Session.SetSeeThrough(true);

        Assert.True(Session.SeeThrough);
        Assert.Equal(1, said);
    }

    [Fact]
    public void PaintingGivesTheSelectedFacesTheSlotAndNoOthers()
    {
        Session.ReplaceSurfaces([new Surface(), new Surface { Name = "Red" }], live: false);
        _rig.Edit(0, 2);

        Session.PaintFaces(1);

        Assert.Equal([1, 0, 1], _rig.Mesh.Faces.Take(3).Select(face => face.Material));
        Assert.Equal("Paint", Session.History.UndoLabel![..5]);
    }

    [Fact]
    public void PaintingIsOneStepToUndoAndTheSlotsAreAStepOfTheirOwn()
    {
        Session.ReplaceSurfaces([new Surface { Name = "Stone" }, new Surface { Name = "Brass" }], live: false);
        _rig.Edit(2);
        Session.PaintFaces(1);
        Assert.Equal(1, _rig.Mesh.Faces[2].Material);

        Session.Undo();

        Assert.Equal(0, _rig.Mesh.Faces[2].Material);
        Assert.Equal(2, Session.ChosenShape!.Surfaces.Count);
    }

    [Fact]
    public void FlippingAllTheNormalsIsOneStepAndTheShapeStopsBeingParametric()
    {
        Assert.True(Session.ChosenShape!.IsParametric);

        Session.RunMeshAction(Action("Flip all normals"));

        Assert.NotNull(Session.ChosenShape.Geometry);
        Assert.False(Session.ChosenShape.IsParametric);
        Session.Undo();
        Assert.Null(Session.ChosenShape.Geometry);
        Assert.True(Session.ChosenShape.IsParametric);
    }

    [Fact]
    public void PaintingWithNothingSelectedChangesNothing()
    {
        Session.ReplaceSurfaces([new Surface()], live: false);
        var steps = Session.History.UndoLabel;

        Session.PaintFaces(0);

        Assert.Equal(steps, Session.History.UndoLabel);
    }

    [Fact]
    public void ASliderRunOnAMaterialIsOneStepAndAButtonIsAnother()
    {
        Session.ReplaceSurfaces([new Surface()], live: false);
        var shape = Session.ChosenShape!;

        Session.ReplaceSurfaces([new Surface { Metallic = 0.2 }], live: true);
        Session.ReplaceSurfaces([new Surface { Metallic = 0.7 }], live: true);
        Assert.Equal(0.7, shape.Surfaces[0].Metallic);

        Session.History.Undo();
        Assert.Equal(0, shape.Surfaces[0].Metallic);
        Session.History.Undo();
        Assert.Empty(shape.Surfaces);
    }

    [Fact]
    public void SayingWhatIsSelectedWhenItIsNotAShape()
    {
        Assert.Null(Session.NotShapeName);

        Session.History.Run(new AddObject(new SceneObject(id: "extra", name: "Extra", kind: ObjectKind.Group), "a"));
        Session.SelectObject("extra");

        Assert.Null(Session.ChosenShape);
        Assert.Equal("Extra", Session.NotShapeName);
    }
}
