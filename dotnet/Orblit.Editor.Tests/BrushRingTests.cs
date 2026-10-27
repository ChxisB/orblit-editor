namespace Orblit.Editor.Tests;

using Orblit.Editor.Viewport;
using Orblit.Terrain;
using Orblit.VectorMath;
using Ground = Orblit.Terrain.Terrain;

// Added in C#: the Dart ring is drawn in the view and has no test of its own.
public sealed class BrushRingTests
{
    private static Ground Flat()
    {
        var ground = new Ground(regionSize: 32, sets: [new TerrainSet("Rock")]);
        ground.AddRegion(new RegionKey(0, 0));
        return ground;
    }

    private static ViewportProjection Above() =>
        new(new OrbitCamera(pitch: 1.2, distance: 60, target: new Vector3(16, 0, 16)), new Size(800, 600));

    [Fact]
    public void AFullRingIsSixtyFourPiecesJoinedEndToEnd()
    {
        var segments = BrushRing.Segments(Flat(), new Vector3(16, 0, 16), 5, Above());

        Assert.Equal(64, segments.Count);
        Assert.All(segments.Zip(segments.Skip(1).Append(segments[0])), pair => Assert.Equal(pair.First.To, pair.Second.From));
    }

    [Fact]
    public void ARingFollowsTheGroundAndAHillRaisesItOnTheScreen()
    {
        var ground = Flat();
        var projection = Above();
        var flat = BrushRing.Segments(ground, new Vector3(16, 0, 16), 5, projection);

        ground.FillHeights(new RegionKey(0, 0), (_, _) => 8);
        var hilly = BrushRing.Segments(ground, new Vector3(16, 0, 16), 5, projection);

        Assert.NotEqual(flat, hilly);
    }

    [Fact]
    public void APieceWithAnEndBehindTheCameraIsLeftOutAndTheRestIsKept()
    {
        var camera = new OrbitCamera(pitch: 0.1, distance: 0.5, target: new Vector3(16, 0, 16));

        var segments = BrushRing.Segments(Flat(), new Vector3(16, 0, 16), 5, new ViewportProjection(camera, new Size(800, 600)));

        Assert.True(segments.Count < 64);
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(0.0, false)]
    [InlineData(0.99, false)]
    [InlineData(1.0, false)]
    public void TheInnerRingShowsOnlyWhenItWouldNotSitOnTheEdgeOrTheCentre(double falloff, bool shown)
    {
        var brush = new Brush().CopyWith(size: 10, falloff: falloff);

        Assert.Equal(shown, BrushRing.CoreRadius(brush) is not null);
    }

    [Fact]
    public void TheInnerRingIsAsFarOutAsTheStrengthHoldsFull()
    {
        var brush = new Brush().CopyWith(size: 10, falloff: 0.4);

        Assert.Equal(3, BrushRing.CoreRadius(brush)!.Value, 6);
    }
}
