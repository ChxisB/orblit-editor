namespace Orblit.Editor.Tests;

using Orblit.Editor.Viewport;

// The solid round a set of points, drawn as its corners and the edges between them, with the
// diagonals that only split a flat face left out.
public sealed class ConvexOutlineTests
{
    private static readonly double[] FaceCentres =
    [
        1, 0, 0,
        -1, 0, 0,
        0, 1, 0,
        0, -1, 0,
        0, 0, 1,
        0, 0, -1,
    ];

    private static double[] Cube() =>
    [
        .. from x in new[] { -1.0, 1.0 }
           from y in new[] { -1.0, 1.0 }
           from z in new[] { -1.0, 1.0 }
           from value in new[] { x, y, z }
           select value,
    ];

    [Fact]
    public void ACubeHasEightCornersAndTwelveEdges()
    {
        var outline = ConvexOutline.Of(Cube());

        Assert.True(outline.HasVolume);
        Assert.Equal(8, outline.Corners.Count);
        Assert.Equal(12, outline.Edges.Count);
    }

    [Fact]
    public void ATetrahedronHasFourCornersAndSixEdges()
    {
        var outline = ConvexOutline.Of([0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1]);

        Assert.Equal(4, outline.Corners.Count);
        Assert.Equal(6, outline.Edges.Count);
    }

    [Fact]
    public void AnOctahedronHasSixCornersAndTwelveEdges()
    {
        var outline = ConvexOutline.Of(FaceCentres);

        Assert.Equal(6, outline.Corners.Count);
        Assert.Equal(12, outline.Edges.Count);
    }

    [Fact]
    public void ASquarePyramidLeavesOutTheDiagonalOfItsFlatBase()
    {
        var outline = ConvexOutline.Of(
        [
            -1, 0, -1,
            1, 0, -1,
            1, 0, 1,
            -1, 0, 1,
            0, 1, 0,
        ]);

        Assert.Equal(5, outline.Corners.Count);
        Assert.Equal(8, outline.Edges.Count);
    }

    [Fact]
    public void PointsInsideItOrInTheMiddleOfAFaceAddNothing()
    {
        var outline = ConvexOutline.Of([.. Cube(), .. FaceCentres, 0, 0, 0]);

        Assert.Equal(8, outline.Corners.Count);
        Assert.Equal(12, outline.Edges.Count);
    }

    [Fact]
    public void AreTheSameWhicheverOrderTheyComeIn()
    {
        // The centres first: each is a corner of the solid until the cube's own corners arrive and
        // cover it.
        var outline = ConvexOutline.Of([0, 0, 0, .. FaceCentres, .. Cube()]);

        Assert.Equal(8, outline.Corners.Count);
        Assert.Equal(12, outline.Edges.Count);
    }

    [Fact]
    public void EveryEdgeJoinsTwoOfItsCorners()
    {
        var outline = ConvexOutline.Of(Cube());

        foreach (var (a, b) in outline.Edges)
        {
            var apart = (outline.Corners[a] - outline.Corners[b]).Length;
            // Along a side of the cube, not across a face or through it.
            Assert.Equal(2, apart, 1e-9);
        }
    }

    [Fact]
    public void PointsThatEncloseNothingMakeNoOutline()
    {
        double[][] cases =
        [
            // Fewer than four.
            [0, 0, 0, 1, 0, 0, 0, 1, 0],
            // All in one plane.
            [0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 1],
            // All on one line.
            [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3],
            // All one point.
            [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
            // Not a whole number of points.
            [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0],
            // Not a number.
            [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, double.NaN],
            [],
        ];

        foreach (var flat in cases)
        {
            var outline = ConvexOutline.Of(flat);
            Assert.False(outline.HasVolume, string.Join(",", flat));
            Assert.Empty(outline.Edges);
        }
    }

    [Fact]
    public void AskingAgainForTheSameListIsNotWorkingItOutAgain()
    {
        var points = Cube();

        Assert.Same(ConvexOutline.Of(points), ConvexOutline.Of(points));
    }
}
