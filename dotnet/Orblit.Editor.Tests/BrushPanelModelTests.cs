namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Terrain;
using Ground = Orblit.Terrain.Terrain;

// Added in C#. No Dart test reaches the brush panel's words, so they are held here.
public sealed class BrushPanelModelTests
{
    private static Ground Regions(int count, int regionSize = 32, double spacing = 1)
    {
        var terrain = new Ground(regionSize: regionSize, spacing: spacing);
        for (var at = 0; at < count; at++)
        {
            terrain.AddRegion(new RegionKey(at, 0));
        }

        return terrain;
    }

    [Theory]
    [InlineData(16, "16")]
    [InlineData(2.5, "2.5")]
    [InlineData(2.04, "2.0")]
    public void MetresDropTheDecimalPointOnAWholeNumber(double value, string said) =>
        Assert.Equal(said, BrushPanelModel.Metres(value));

    [Fact]
    public void AnExtentSaysHowMuchGroundThereIsAndHowWideEachRegionIs()
    {
        Assert.Equal("No ground yet. Add a region in the inspector.", BrushPanelModel.Extent(Regions(0)));
        Assert.Equal("One region, 32 m across", BrushPanelModel.Extent(Regions(1)));
        Assert.Equal("4 regions, each 32 m across", BrushPanelModel.Extent(Regions(4)));
        // A region is a count of texels, so the metres it spans follow the spacing between them.
        Assert.Equal("One region, 64 m across", BrushPanelModel.Extent(Regions(1, spacing: 2)));
    }

    [Fact]
    public void ASetsDetailSaysHowBigItsTilesAreAndWhereItIsLaidOnCliffs()
    {
        Assert.Equal("4 m", BrushPanelModel.SetDetail(new TerrainSet("Rock")));
        Assert.Equal("1.5 m, on cliffs", BrushPanelModel.SetDetail(new TerrainSet("Scree", tileSize: 1.5, triplanar: true)));
    }

    [Fact]
    public void APickedTintKeepsTheRoughnessItWasPaintedWith()
    {
        var was = GroundColour.Of(red: 10, green: 20, blue: 30, roughness: 0.5);

        var now = BrushPanelModel.Painted(was, new Colour(0xFFC9B48A));

        Assert.Equal((0xC9, 0xB4, 0x8A), (now.Red, now.Green, now.Blue));
        Assert.Equal(was.RoughnessByte, now.RoughnessByte);
        Assert.Equal(new Colour(0xFFC9B48A), BrushPanelModel.TintOf(now));
    }

    [Fact]
    public void TheTintsOfferedAreAllDifferent() =>
        Assert.Equal(BrushPanelModel.Tints.Count, BrushPanelModel.Tints.Distinct().Count());
}
