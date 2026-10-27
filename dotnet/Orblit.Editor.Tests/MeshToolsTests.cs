namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using MeshEdge = (int, int);

public sealed class MeshToolsTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    private static MeshAction Named(string label) => MeshTools.All.First(action => action.Label == label);

    private static int TopOf(Mesh mesh) => mesh.Faces.FindIndex(f => mesh.NormalOf(f).Y > 0.9);

    private static List<string> LabelsIn(ElementMode? mode, ElementSelection selection) =>
        [.. MeshTools.AvailableIn(mode, selection).Select(action => action.Label)];

    [Fact]
    public void NothingThatNeedsASelectionIsOfferedWhenThereIsNone()
    {
        var offered = MeshTools.AvailableIn(ElementMode.Face, new ElementSelection());

        Assert.All(offered, action => Assert.Empty(action.Modes));
    }

    [Fact]
    public void TheFaceActionsAreOfferedWithAFaceSelected()
    {
        var offered = LabelsIn(ElementMode.Face, new ElementSelection(faces: [0]));

        Assert.Contains("Extrude", offered);
        Assert.Contains("Inset", offered);
        Assert.Contains("Subdivide", offered);
        Assert.Contains("Delete", offered);
    }

    [Fact]
    public void NothingThatNeedsTwoIsOfferedForOne()
    {
        Assert.DoesNotContain("Merge", LabelsIn(ElementMode.Face, new ElementSelection(faces: [0])));
        Assert.Contains("Merge", LabelsIn(ElementMode.Face, new ElementSelection(faces: [0, 1])));
    }

    [Fact]
    public void TheObjectActionsAreAlwaysOffered()
    {
        var offered = LabelsIn(null, new ElementSelection());

        Assert.Contains("Conform normals", offered);
        Assert.Contains("Flip all normals", offered);
        Assert.Contains("Weld all", offered);
    }

    [Fact]
    public void AnEdgeActionIsNotOfferedInFaceMode() =>
        Assert.DoesNotContain("Bridge", LabelsIn(ElementMode.Face, new ElementSelection(faces: [0, 1])));

    [Fact]
    public void ExtrudingMovesTheFaceAndSelectsItAgain()
    {
        var mesh = Cube();

        var after = Named("Extrude").Run(mesh, new ElementSelection(faces: [TopOf(mesh)]), 2);

        Assert.Equal(3, mesh.Bounds.Max.Y, 1e-9);
        // Selected again, because somebody who extrudes once usually extrudes twice.
        Assert.Single(after.Faces);
        Assert.Equal(3, mesh.CentreOf(after.FacesIn(mesh).Single()).Y, 1e-9);
    }

    [Fact]
    public void DeletingLeavesNothingSelected()
    {
        var mesh = Cube();

        var after = Named("Delete").Run(mesh, new ElementSelection(faces: [TopOf(mesh)]), 0);

        Assert.Equal(5, mesh.Faces.Count);
        Assert.True(after.IsEmpty);
    }

    [Fact]
    public void SubdividingSelectsThePiecesItMade()
    {
        var mesh = Cube();

        var after = Named("Subdivide").Run(mesh, new ElementSelection(faces: [TopOf(mesh)]), 0);

        Assert.Equal(4, after.Faces.Count);
        Assert.Equal(9, mesh.Faces.Count);
    }

    [Fact]
    public void GrowingWithoutAnAngleTakesTheNeighbours()
    {
        var mesh = Cube();

        var after = Named("Grow").Run(mesh, new ElementSelection(faces: [TopOf(mesh)]), 180);

        Assert.Equal(5, after.Faces.Count);
    }

    [Fact]
    public void GrowingWithinAnAngleStopsAtTheCorner()
    {
        var mesh = Cube();

        var after = Named("Grow").Run(mesh, new ElementSelection(faces: [TopOf(mesh)]), 5);

        Assert.Single(after.Faces);
    }

    [Fact]
    public void FlippingNormalsTurnsJustTheSelectedFace()
    {
        var mesh = Cube();
        var top = TopOf(mesh);

        Named("Flip normals").Run(mesh, new ElementSelection(faces: [top]), 0);

        Assert.Equal(-1, mesh.NormalOf(mesh.Faces[top]).Y, 1e-9);
    }

    [Fact]
    public void ConformingTurnsItBack()
    {
        var mesh = Cube();
        mesh.FlipFaces([mesh.Faces[TopOf(mesh)]]);

        Named("Conform normals").Run(mesh, new ElementSelection(), 0);

        Assert.Equal(1, mesh.NormalOf(mesh.Faces[TopOf(mesh)]).Y, 1e-9);
    }

    [Fact]
    public void AnEdgeRingSelectsTheEdgesRoundACylinder()
    {
        var mesh = (Shape.Of(ShapeKind.Cylinder) with { Sides = 8, Capped = false }).Build();
        var face = mesh.Faces[0];
        var selection = new ElementSelection(edges: [MeshActions.EdgeOf(face.Vertices[0], face.Vertices[1])]);

        var after = Named("Edge ring").Run(mesh, selection, 0);

        Assert.True(after.Edges.Count >= 2);
    }

    [Fact]
    public void FillingAHoleClosesWhatDeleteOpened()
    {
        var mesh = Cube();
        Named("Delete").Run(mesh, new ElementSelection(faces: [TopOf(mesh)]), 0);

        var after = Named("Fill hole").Run(mesh, new ElementSelection(edges: mesh.OpenEdges), 0);

        Assert.Single(after.Faces);
        Assert.Empty(mesh.OpenEdges);
    }

    [Fact]
    public void BridgingJoinsTheFirstTwoEdgesInTheOrderTheyWerePicked()
    {
        var mesh = Cube();

        Named("Bridge").Run(mesh, new ElementSelection(edges: [(0, 1), (2, 3), (4, 5)]), 0);
        var made = mesh.Faces[^1];

        // The order somebody clicked in is the order the face is wound in. A third edge is ignored.
        Assert.Equal([0, 1], made.Vertices.Take(2));
        Assert.Equal(4, made.Vertices.Count);
        Assert.DoesNotContain(4, made.Vertices);
    }

    [Fact]
    public void BridgingWithFewerThanTwoEdgesDoesNothing()
    {
        var mesh = Cube();
        var before = mesh.Faces.Count;
        var selection = new ElementSelection(edges: [(0, 1)]);

        var after = Named("Bridge").Run(mesh, selection, 0);

        Assert.Equal(before, mesh.Faces.Count);
        Assert.Same(selection, after);
    }

    [Fact]
    public void EveryActionLeavesAMeshWhoseFacesNameRealPoints()
    {
        // An action that corrupts the mesh is one that crashes the renderer two frames later, a
        // long way from here.
        foreach (var action in MeshTools.All)
        {
            var mesh = Cube();
            action.Run(mesh, Everything(mesh), action.Amount?.Value ?? 1);

            foreach (var face in mesh.Faces)
            {
                Assert.All(face.Vertices, index => Assert.InRange(index, 0, mesh.Positions.Count - 1));
                Assert.True(face.Vertices.Count >= 3, action.Label);
            }
        }
    }

    [Fact]
    public void AndOneThatCanBeDrawn()
    {
        foreach (var action in MeshTools.All)
        {
            var mesh = Cube();
            action.Run(
                mesh,
                new ElementSelection(vertices: [0, 1], edges: [(0, 1)], faces: [0]),
                action.Amount?.Value ?? 1);

            var drawn = mesh.Triangulate();

            Assert.All(drawn.Indices, index => Assert.True(index < drawn.Positions.Length / 3, action.Label));
        }
    }

    [Fact]
    public void EveryActionHasWordsAndAnIconAndAGroupOfItsOwn()
    {
        Assert.All(MeshTools.All, action =>
        {
            Assert.NotEmpty(action.Label);
            Assert.NotEmpty(action.Icon);
            Assert.NotEmpty(action.Hint);
            Assert.NotEmpty(action.Group.Label());
        });
        Assert.Equal(MeshTools.All.Count, MeshTools.All.Select(action => action.Label).Distinct().Count());
    }

    private static ElementSelection Everything(Mesh mesh)
    {
        var (a, b) = (mesh.Faces[0], mesh.Faces[1]);
        return new ElementSelection(
            vertices: [0, 1, 2],
            edges: [MeshActions.EdgeOf(a.Vertices[0], a.Vertices[1]), MeshActions.EdgeOf(b.Vertices[0], b.Vertices[1])],
            faces: [0, 1]);
    }
}
