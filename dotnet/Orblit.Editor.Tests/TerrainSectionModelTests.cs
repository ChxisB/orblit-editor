namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Terrain;
using Ground = Orblit.Terrain.Terrain;

// The settings the terrain section builds and the words it says are held here.
public sealed class TerrainSectionModelTests
{
    private static TerrainSettings Three(AutoCover? cover = null) => new(
        [new TerrainSet("Rock"), new TerrainSet("Grass"), new TerrainSet("Sand")],
        cover ?? new AutoCover(),
        0.5);

    [Fact]
    public void TheMapRunsOneSquareBeyondTheGroundOnEverySide()
    {
        var span = TerrainSectionModel.SpanOf([new RegionKey(0, 0), new RegionKey(2, 1), new RegionKey(1, -1)]);

        Assert.Equal(new RegionSpan(-1, 3, -2, 2), span);
        Assert.Equal((5, 5), (span.Columns, span.Rows));
    }

    [Fact]
    public void WithNoGroundTheMapIsTheSquaresRoundTheOrigin() =>
        Assert.Equal(new RegionSpan(-1, 0, -1, 0), TerrainSectionModel.SpanOf([]));

    [Fact]
    public void TheCaptionSaysHowMuchGroundOneSquareIs()
    {
        var terrain = new Ground(regionSize: 32, spacing: 0.5);

        Assert.StartsWith("Each square is 16 m of ground.", TerrainSectionModel.RegionsCaption(terrain));
    }

    [Fact]
    public void ANewSetIsNamedByItsPlaceAndTheRestAreLeftBe()
    {
        var now = TerrainSectionModel.AddSet(Three());

        Assert.Equal(["Rock", "Grass", "Sand", "Set 4"], now.Sets.Select(set => set.Name));
        Assert.Equal(0.5, now.BlendSharpness);
    }

    [Fact]
    public void TakingTheLastSetAwayPullsCoverThatNamedItBackOntoOneThatIsLeft()
    {
        var was = Three(new AutoCover(steep: 2, flat: 2, slope: 1.5, heightFalloff: 0.3));

        var now = TerrainSectionModel.RemoveLast(was);

        Assert.Equal(["Rock", "Grass"], now.Sets.Select(set => set.Name));
        Assert.Equal((1, 1, 1.5, 0.3), (now.AutoCover.Steep, now.AutoCover.Flat, now.AutoCover.Slope, now.AutoCover.HeightFalloff));
    }

    [Fact]
    public void TakingTheLastSetAwayLeavesCoverThatStillHasItsSetAlone()
    {
        var now = TerrainSectionModel.RemoveLast(Three(new AutoCover(steep: 0, flat: 1)));

        Assert.Equal((0, 1), (now.AutoCover.Steep, now.AutoCover.Flat));
    }

    [Fact]
    public void ChangingOneSetLeavesTheOthersAndTheRestOfTheSettings()
    {
        var was = Three();
        var changed = TerrainSectionModel.WithTileSize(was.Sets[1], 8);

        var now = TerrainSectionModel.WithSet(was, 1, changed);

        Assert.Same(was.Sets[0], now.Sets[0]);
        Assert.Equal(8, now.Sets[1].TileSize);
        Assert.Same(was.Sets[2], now.Sets[2]);
        Assert.Same(was.AutoCover, now.AutoCover);
    }

    [Fact]
    public void ASetCopyChangesOnlyTheFieldItIsAskedTo()
    {
        var set = new TerrainSet("Rock", albedo: "a.png", normal: "n.png", tileSize: 2, triplanar: true);

        var bare = TerrainSectionModel.WithAlbedo(set, null);
        var wrapped = TerrainSectionModel.WithTriplanar(TerrainSectionModel.WithNormal(set, null), false);

        Assert.Equal((null, "n.png", 2, true), (bare.Albedo, bare.Normal, bare.TileSize, bare.Triplanar));
        Assert.Equal(("a.png", null, 2, false), (wrapped.Albedo, wrapped.Normal, wrapped.TileSize, wrapped.Triplanar));
        Assert.Equal("Rock", wrapped.Name);
    }

    [Fact]
    public void ACoverCopyChangesOnlyTheValueItIsAskedTo()
    {
        var cover = new AutoCover(steep: 0, flat: 1, slope: 1, heightFalloff: 0.1);

        Assert.Equal((2, 1, 1, 0.1), Values(TerrainSectionModel.WithSteep(cover, 2)));
        Assert.Equal((0, 2, 1, 0.1), Values(TerrainSectionModel.WithFlat(cover, 2)));
        Assert.Equal((0, 1, 3, 0.1), Values(TerrainSectionModel.WithSlope(cover, 3)));
        Assert.Equal((0, 1, 1, 0.5), Values(TerrainSectionModel.WithHeightFalloff(cover, 0.5)));
    }

    private static (long, long, double, double) Values(AutoCover cover) => (cover.Steep, cover.Flat, cover.Slope, cover.HeightFalloff);

    [Fact]
    public void ACoverNamingASetThatIsGoneSaysSo()
    {
        var sets = Three().Sets;

        Assert.Equal("Grass", TerrainSectionModel.CoverShown(sets, 1));
        Assert.Equal("Set 6 (missing)", TerrainSectionModel.CoverShown(sets, 5));
    }
}
