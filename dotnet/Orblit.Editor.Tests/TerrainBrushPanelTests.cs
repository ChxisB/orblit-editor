using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Terrain;
using Ground = Orblit.Terrain.Terrain;

// What the panel shows and what a click does are held here.
public sealed class TerrainBrushPanelTests : IDisposable
{
    private const string File = "terrain/hills/hills.oterrain";

    private readonly string _root = Directory.CreateTempSubdirectory("brush_panel").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private (TerrainBench Bench, OpenTerrain? Open, Window Window, TerrainBrushPanel Panel, Counter Asked) Show(bool withTerrain = true)
    {
        var history = new History(new Workspace(_root));
        var bench = new TerrainBench(history, _root);
        OpenTerrain? open = null;
        if (withTerrain)
        {
            var terrain = new Ground(regionSize: 32, sets: [new TerrainSet("Rock"), new TerrainSet("Grass", tileSize: 2, triplanar: true)]);
            terrain.AddRegion(new RegionKey(0, 0));
            Assert.Null(bench.Create(File, terrain));
            open = bench[File];
        }

        var asked = new Counter();
        var panel = new TerrainBrushPanel(bench, history, () => open, () => asked.Count++);
        var window = new Window { Width = 360, Height = 900, Content = panel };
        window.Show();
        Headless.Render();
        return (bench, open, window, panel, asked);
    }

    private sealed class Counter
    {
        public int Count { get; set; }
    }

    private static string TextOf(Control root, string name) => Headless.Find<TextBlock>(root, name).Text ?? "";

    private static bool Has(Control root, string name) => Headless.All<Control>(root).Any(one => one.Name == name);

    [Fact]
    public Task WithATerrainItIsNamedAndSaysHowMuchGroundItHas() => Headless.Run(() =>
    {
        var (_, _, window, _, _) = Show();

        Assert.Equal("hills", TextOf(window, "brush_terrain"));
        Assert.Equal("One region, 32 m across", TextOf(window, "brush_extent"));
    });

    [Fact]
    public Task WithNoTerrainItSaysSoAndOffersToMakeOne() => Headless.Run(() =>
    {
        var (_, _, window, _, asked) = Show(withTerrain: false);

        Assert.Contains("Nothing to shape", TextOf(window, "brush_nothing"));
        Headless.Click(window, "button_add_terrain");
        Assert.Equal(1, asked.Count);
    });

    [Fact]
    public Task ATerrainThatGoesAwayIsNotLeftOnShowUntilThePanelIsAskedAgain() => Headless.Run(() =>
    {
        var history = new History(new Workspace(_root));
        var bench = new TerrainBench(history, _root);
        var terrain = new Ground(regionSize: 32);
        Assert.Null(bench.Create(File, terrain));
        var open = bench[File];
        var panel = new TerrainBrushPanel(bench, history, () => open);
        var window = new Window { Width = 360, Height = 900, Content = panel };
        window.Show();
        Headless.Render();
        Assert.Equal("hills", TextOf(window, "brush_terrain"));

        open = null;
        panel.Refresh();
        Headless.Render();

        Assert.Contains("Nothing to shape", TextOf(window, "brush_nothing"));
    });

    [Fact]
    public Task TheToolIsNamedWithWhatItDoesAndEachToolHasItsOwnControls() => Headless.Run(() =>
    {
        var (bench, _, window, _, _) = Show();

        Assert.True(Has(window, "section_raise"));
        Assert.Equal(BrushWords.Of(BrushTool.Raise).Hint, TextOf(window, "brush_hint"));
        Assert.False(Has(window, "slider_rougher_by"));

        bench.Tool = BrushTool.Roughness;
        Headless.Render();
        Assert.True(Has(window, "section_paint_roughness"));
        Assert.True(Has(window, "slider_rougher_by"));

        bench.Tool = BrushTool.Colour;
        Headless.Render();
        Assert.True(Has(window, "swatch_tint_0"));

        bench.Tool = BrushTool.Cover;
        Headless.Render();
        Assert.True(Has(window, "set_row_0"));
    });

    [Fact]
    public Task EachSetIsARowAndClickingOneChoosesIt() => Headless.Run(() =>
    {
        var (bench, _, window, _, _) = Show();
        bench.Tool = BrushTool.Cover;
        Headless.Render();

        Assert.Equal("Rock", TextOf(window, "set_name_0"));
        Assert.Equal("4 m", TextOf(window, "set_detail_0"));
        Assert.Equal("2 m, on cliffs", TextOf(window, "set_detail_1"));
        Assert.Equal(0, bench.Set);

        Headless.Click(window, "set_row_1");

        Assert.Equal(1, bench.Set);
    });

    [Fact]
    public Task ATerrainWithNoSetsSaysThereIsNothingToLay() => Headless.Run(() =>
    {
        var history = new History(new Workspace(_root));
        var bench = new TerrainBench(history, _root) { Tool = BrushTool.Cover };
        Assert.Null(bench.Create(File, new Ground(regionSize: 32)));
        var open = bench[File];
        var window = new Window { Width = 360, Height = 900, Content = new TerrainBrushPanel(bench, history, () => open) };
        window.Show();
        Headless.Render();

        Assert.Contains("no sets to lay", TextOf(window, "brush_no_sets"));
    });

    [Fact]
    public Task ASliderMovesTheBrushSettingItNamesAndLeavesTheRestAlone() => Headless.Run(() =>
    {
        var (bench, _, window, _, _) = Show();
        var was = bench.Brush;

        Headless.Find<Slider>(window, "slider_size").Value = 40;
        Headless.Render();

        Assert.Equal(40, bench.Brush.Size);
        Assert.Equal((was.Strength, was.Falloff, was.Jitter, was.Spacing), (bench.Brush.Strength, bench.Brush.Falloff, bench.Brush.Jitter, bench.Brush.Spacing));
        Assert.Equal(Brush.MinSpacing, Headless.Find<Slider>(window, "slider_spacing").Minimum);
    });

    [Fact]
    public Task ASwatchTintsWhatTheColourToolPaintsAndKeepsItsRoughness() => Headless.Run(() =>
    {
        var (bench, _, window, _, _) = Show();
        bench.Tool = BrushTool.Colour;
        Headless.Render();
        var roughness = bench.Colour.RoughnessByte;

        Headless.Click(window, "swatch_tint_1");

        Assert.Equal(new Colour(0xFFC9B48A), BrushPanelModel.TintOf(bench.Colour));
        Assert.Equal(roughness, bench.Colour.RoughnessByte);
    });

    [Fact]
    public Task TheRoughnessSliderSetsWhatTheRoughnessToolPaints() => Headless.Run(() =>
    {
        var (bench, _, window, _, _) = Show();
        bench.Tool = BrushTool.Roughness;
        Headless.Render();

        Headless.Find<Slider>(window, "slider_rougher_by").Value = -0.5;

        Assert.Equal(-0.5, bench.Roughness);
    });
}
