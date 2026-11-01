namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using MeshEdge = (int, int);

public sealed class ElementSelectionTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    [Fact]
    public void KnowsHowMuchIsInItPerMode()
    {
        var selection = new ElementSelection(vertices: [1, 2], edges: [(0, 1)], faces: [3, 4, 5]);

        Assert.Equal(2, selection.CountIn(ElementMode.Vertex));
        Assert.Equal(1, selection.CountIn(ElementMode.Edge));
        Assert.Equal(3, selection.CountIn(ElementMode.Face));
        Assert.False(selection.IsEmpty);
    }

    [Fact]
    public void AFaceThatHasGoneIsSkippedRatherThanCrashedOn()
    {
        // Every operation makes new faces and drops old ones, so a selection holds positions
        // and has to cope with the list being shorter.
        var selection = new ElementSelection(faces: [2, 99]);

        Assert.Single(selection.FacesIn(Cube()));
    }

    [Fact]
    public void ThePointsItTouchesComeFromWhicheverModeMadeIt()
    {
        var mesh = Cube();

        Assert.Equal(new HashSet<int> { 0, 1 }, new ElementSelection(vertices: [0, 1]).PointsIn(mesh));
        Assert.Equal(new HashSet<int> { 0, 3 }, new ElementSelection(edges: [(0, 3)]).PointsIn(mesh));
        Assert.Equal(mesh.Faces[0].Vertices.ToHashSet(), new ElementSelection(faces: [0]).PointsIn(mesh));
    }

    [Fact]
    public void APointThatHasGoneIsSkippedToo()
    {
        var points = new ElementSelection(vertices: [-1, 0, 500]).PointsIn(Cube());

        Assert.Equal(new HashSet<int> { 0 }, points);
    }

    [Fact]
    public void ThePivotIsTheMiddleOfWhatIsSelected()
    {
        var mesh = Cube();
        var top = mesh.Faces.FindIndex(face => mesh.NormalOf(face).Y > 0.9);

        var pivot = new ElementSelection(faces: [top]).PivotIn(mesh)!;

        Assert.Equal(1, pivot.Y, 9);
        Assert.Equal(0, pivot.X, 9);
    }

    [Fact]
    public void AnEmptySelectionHasNoPivotRatherThanOneAtTheOrigin() =>
        Assert.Null(new ElementSelection().PivotIn(Cube()));

    [Fact]
    public void ACopyDoesNotShareItsSets()
    {
        var was = new ElementSelection(faces: [1]);
        var copy = was.Copy();
        copy.Faces.Add(2);

        Assert.Equal([1], was.Faces);
        Assert.Equal([1, 2], copy.Faces);
    }

    [Fact]
    public void ClearingEmptiesEveryKind()
    {
        var selection = new ElementSelection([1], [(0, 1)], [2]);

        selection.Clear();

        Assert.True(selection.IsEmpty);
    }

    [Fact]
    public void TheOrderThingsWerePickedInIsKept()
    {
        // A bridge is wound from the first edge to the second, so which was picked first matters.
        var selection = new ElementSelection(edges: [(5, 6), (1, 2), (3, 4)]);
        selection.Edges.Remove((1, 2));
        selection.Edges.Add((1, 2));

        Assert.Equal([(5, 6), (3, 4), (1, 2)], selection.Edges);
        Assert.Equal([(5, 6), (3, 4), (1, 2)], selection.Copy().Edges);
    }

    [Fact]
    public void TheModesCycleRound()
    {
        Assert.Equal(ElementMode.Edge, ElementMode.Vertex.Next());
        Assert.Equal(ElementMode.Face, ElementMode.Edge.Next());
        Assert.Equal(ElementMode.Vertex, ElementMode.Face.Next());
    }
}
