using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Terrain;
using static Orblit.Editor.Tests.InspectorRig;
using Doc = Orblit.Scene;
using Ground = Orblit.Terrain.Terrain;

// What the terrain section shows and what each control changes are held here.
public sealed class TerrainSectionTests : IDisposable
{
    private const string File = "terrain/hills/hills.oterrain";

    private readonly string _root = Directory.CreateTempSubdirectory("terrain_section").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static SceneObject GroundObject(Doc.TerrainComponent component) => new(
        "ground",
        "Ground",
        ObjectKind.Group,
        components: new OrderedDictionary<string, Doc.SceneComponent> { [Doc.SceneComponents.Terrain] = component });

    private static Ground Hills()
    {
        var terrain = new Ground(
            regionSize: 32,
            sets: [new TerrainSet("Rock", albedo: "textures/rock.png"), new TerrainSet("Grass", tileSize: 2), new TerrainSet("Sand")]);
        terrain.AddRegion(new RegionKey(0, 0));
        return terrain;
    }

    private (EditorSession Session, TerrainBench Bench, OpenTerrain? Open, Window Window) Show(Doc.TerrainComponent? component = null, bool withBench = true)
    {
        var workspace = new Workspace(_root);
        workspace.Add(new SceneEntry("main", "Main", scene: new EditorScene([GroundObject(component ?? new Doc.TerrainComponent(File))])));
        var history = new History(workspace);
        var bench = new TerrainBench(history, _root);
        Assert.Null(bench.Create(File, Hills()));
        var session = new EditorSession(workspace, history, EditorModes.Standard(), PanelTypes.BuiltIn());
        var panel = new InspectorPanel(session, new InspectorLinks { Terrain = withBench ? bench : null });
        var window = new Window { Width = 360, Height = 3600, Content = panel };
        window.Show();
        session.Select(["ground"]);
        Headless.Render();
        return (session, bench, bench[File], window);
    }

    private static Control Card(Window window, int index) => Headless.Find<Control>(window, $"set_card_{index}");

    // What a browser tile's drag carries, let go over a picture well.
    private static void Drop(Window window, string path, string well)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(AssetDrops.Format, path));
        var at = Headless.Centre(window, well);
        foreach (var step in new[] { RawDragEventType.DragEnter, RawDragEventType.DragOver, RawDragEventType.Drop })
        {
            window.DragDrop(at, step, data, DragDropEffects.Copy, RawInputModifiers.None);
        }

        Headless.Render();
    }

    [Fact]
    public Task ATerrainShowsItsFileAndWhatItIsMadeOf() => Headless.Run(() =>
    {
        var (_, _, _, window) = Show();

        Assert.Contains(Headless.All<TextBlock>(window), one => one.Text == File);
        Assert.True(Has(window, "section_terrain"));
        Assert.True(Has(window, "section_regions"));
        Assert.True(Has(window, "section_sets"));
        Assert.True(Has(window, "section_automatic_cover"));
        Assert.Contains("Each square is 32 m", Headless.Find<TextBlock>(window, "regions_caption").Text);
        Assert.Equal("1. Rock", TextOf(window, "set_heading_0"));
    });

    [Fact]
    public Task AWindowWithNoBenchHasNoTerrainSection() => Headless.Run(() =>
    {
        var (_, _, _, window) = Show(withBench: false);

        Assert.False(Has(window, "section_terrain"));
    });

    [Fact]
    public Task ATerrainThatNamesNoFileSaysThereIsNoGround() => Headless.Run(() =>
    {
        var (_, _, _, window) = Show(new Doc.TerrainComponent());

        Assert.Contains("Names no terrain file", TextOf(window, "terrain_problem"));
        Assert.False(Has(window, "section_regions"));
    });

    [Fact]
    public Task AFileThatCannotBeReadSaysSoAndOffersNoEditing() => Headless.Run(() =>
    {
        var (_, _, _, window) = Show(new Doc.TerrainComponent("terrain/gone/gone.oterrain"));

        Assert.Contains("Could not be read", TextOf(window, "terrain_problem"));
        Assert.False(Has(window, "section_sets"));
    });

    [Fact]
    public Task TurningShadowsOffIsOneStepOfItsOwn() => Headless.Run(() =>
    {
        var (session, _, _, window) = Show();

        Headless.Click(window, "choice_cast_shadows_off");

        var component = PhysicsComponents.Of<Doc.TerrainComponent>(session.Workspace.Loaded!.Scene!["ground"]!, Doc.SceneComponents.Terrain)!;
        Assert.Equal((false, true, File), (component.CastShadows, component.ReceiveShadows, component.File));
        Assert.Equal("Set Ground shadows", session.History.UndoLabel);
    });

    [Fact]
    public Task AnEmptySquareAddsGroundAndAFullOneTakesItAway() => Headless.Run(() =>
    {
        var (session, _, open, window) = Show();
        var region = open!.Terrain.RegionAt(new RegionKey(0, 0));

        Headless.Click(window, "region_1_0");
        Assert.NotNull(open.Terrain.RegionAt(new RegionKey(1, 0)));
        Assert.Equal("Add ground to hills", session.History.UndoLabel);

        Headless.Click(window, "region_0_0");
        Assert.Null(open.Terrain.RegionAt(new RegionKey(0, 0)));
        Assert.Equal("Take ground from hills", session.History.UndoLabel);

        session.History.Undo();
        Assert.Same(region, open.Terrain.RegionAt(new RegionKey(0, 0)));
    });

    [Fact]
    public Task TheMapGrowsAMarginRoundTheGroundSoThereIsAlwaysAnEmptySquareToClick() => Headless.Run(() =>
    {
        var (_, _, _, window) = Show();

        Assert.True(Has(window, "region_n1_n1"));
        Assert.True(Has(window, "region_1_1"));
        Assert.False(Has(window, "region_2_0"));
    });

    [Fact]
    public Task AddingASetNamesItByItsPlaceAndUndoTakesItBack() => Headless.Run(() =>
    {
        var (session, _, open, window) = Show();

        Headless.Click(window, "button_add_set");

        Assert.Equal(["Rock", "Grass", "Sand", "Set 4"], open!.Terrain.Sets.Select(set => set.Name));
        Assert.Equal("Add a set to hills", session.History.UndoLabel);
        session.History.Undo();
        Assert.Equal(3, open.Terrain.Sets.Count);
    });

    [Fact]
    public Task TakingTheLastSetAwayPullsCoverThatNamedItBackAndCanBeUndone() => Headless.Run(() =>
    {
        var (session, bench, open, window) = Show();
        bench.Edit(open!, "Cover the sand", settings => settings with { AutoCover = new AutoCover(steep: 2, flat: 2) });
        session.History.Seal();
        Headless.Render();

        Headless.Click(window, "button_remove_last");

        Assert.Equal(["Rock", "Grass"], open!.Terrain.Sets.Select(set => set.Name));
        Assert.Equal((1, 1), (open.Terrain.AutoCover.Steep, open.Terrain.AutoCover.Flat));
        Assert.Equal("Remove Sand from hills", session.History.UndoLabel);
        session.History.Undo();
        Assert.Equal((2, 2), (open.Terrain.AutoCover.Steep, open.Terrain.AutoCover.Flat));
        Assert.Equal(3, open.Terrain.Sets.Count);
    });

    [Fact]
    public Task TheLastSetCannotBeTakenAwayAndTheThirtyThirdCannotBeAdded() => Headless.Run(() =>
    {
        var (session, bench, open, window) = Show();
        bench.Edit(open!, "Only one", settings => settings with { Sets = [settings.Sets[0]] });
        session.History.Seal();
        Headless.Render();
        Assert.False(Headless.Find<Button>(window, "button_remove_last").IsEnabled);

        bench.Edit(open!, "Fill them", settings => settings with { Sets = [.. Enumerable.Range(0, Cover.SetCount).Select(at => new TerrainSet($"S{at}"))] });
        session.History.Seal();
        Headless.Render();

        Assert.False(Headless.Find<Button>(window, "button_add_set").IsEnabled);
    });

    [Fact]
    public Task DraggingARepeatSliderIsOneStepNamedForTheSetAndLeavesTheRestOfIt() => Headless.Run(() =>
    {
        var (session, _, open, window) = Show();

        Headless.Find<Slider>(Card(window, 0), "slider_repeats").Value = 12;
        Headless.Render();

        var rock = open!.Terrain.Sets[0];
        Assert.Equal((12, "textures/rock.png", "Rock"), (rock.TileSize, rock.Albedo, rock.Name));
        Assert.Equal("Set Rock repeat", session.History.UndoLabel);
        session.History.Undo();
        Assert.Equal(4, open.Terrain.Sets[0].TileSize);
    });

    [Fact]
    public Task WrappingAtCliffsChangesOnlyThatSetAndOnlyWhenItIsDifferent() => Headless.Run(() =>
    {
        var (session, _, open, window) = Show();

        Headless.Click(window, Headless.Find<Control>(Card(window, 1), "choice_on_cliffs_wrap"));
        Assert.True(open!.Terrain.Sets[1].Triplanar);
        Assert.False(open.Terrain.Sets[0].Triplanar);

        var steps = 0;
        session.History.Changed += () => steps++;
        Headless.Click(window, Headless.Find<Control>(Card(window, 1), "choice_on_cliffs_wrap"));
        Assert.Equal(0, steps);
    });

    [Fact]
    public Task APictureShowsItsFileNameAndCanBeTakenOff() => Headless.Run(() =>
    {
        var (session, _, open, window) = Show();
        Assert.Equal("rock.png", TextOf(window, "picture_colour_0"));
        Assert.Equal("Drop a PNG or JPEG here", TextOf(window, "picture_colour_1"));
        Assert.False(Has(window, "picture_clear_colour_1"));

        Headless.Click(window, "picture_clear_colour_0");

        Assert.Null(open!.Terrain.Sets[0].Albedo);
        Assert.Equal("Set Rock colour picture", session.History.UndoLabel);
    });

    [Fact]
    public Task APictureDroppedOnAWellGoesOnTheSet() => Headless.Run(() =>
    {
        var (session, _, open, window) = Show();

        Drop(window, Path.Combine(_root, "textures", "sand.png"), "picture_well_colour_1");

        Assert.Equal("textures/sand.png", open!.Terrain.Sets[1].Albedo);
        Assert.Equal("Set Grass colour picture", session.History.UndoLabel);
        Assert.Equal("sand.png", TextOf(window, "picture_colour_1"));
    });

    [Fact]
    public Task AWellRefusesWhatIsNotAPictureInTheProject() => Headless.Run(() =>
    {
        var (session, _, open, window) = Show();
        var steps = 0;
        session.History.Changed += () => steps++;

        Drop(window, Path.Combine(_root, "textures", "sand.tga"), "picture_well_colour_1");
        Drop(window, Path.Combine(Path.GetTempPath(), "elsewhere.png"), "picture_well_colour_1");
        Drop(window, Path.Combine(_root, "textures", "rock.png"), "picture_well_colour_0");

        Assert.Null(open!.Terrain.Sets[1].Albedo);
        Assert.Equal(0, steps);
    });

    [Fact]
    public Task ChoosingTheFlatSetFromTheMenuSetsWhatLevelBareGroundShows() => Headless.Run(() =>
    {
        var (session, _, open, window) = Show();
        Assert.Equal("Grass", ((TextBlock)Headless.Find<Button>(window, "pick_flat").Content!).Text);

        var menu = Assert.IsType<MenuFlyout>(Headless.Find<Button>(window, "pick_flat").Flyout);
        menu.Items.OfType<MenuItem>().First(one => one.Name == "pick_flat_sand").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal((0, 2), (open!.Terrain.AutoCover.Steep, open.Terrain.AutoCover.Flat));
        Assert.Equal("Set hills flat set", session.History.UndoLabel);
    });

    [Fact]
    public Task ACoverThatNamesASetThatIsGoneSaysSoInTheMenu() => Headless.Run(() =>
    {
        var (session, bench, open, window) = Show();
        bench.Edit(open!, "Point at nothing", settings => settings with { AutoCover = new AutoCover(steep: 0, flat: 7) });
        session.History.Seal();
        Headless.Render();

        Assert.Equal("Set 8 (missing)", ((TextBlock)Headless.Find<Button>(window, "pick_flat").Content!).Text);
    });

    [Fact]
    public Task TheCoverAndBlendSlidersEachSetOneValueAndAreOneStepPerDrag() => Headless.Run(() =>
    {
        var (session, _, open, window) = Show();

        Headless.Find<Slider>(window, "slider_slope").Value = 2.5;
        Assert.Equal((2.5, "Set hills slope"), (open!.Terrain.AutoCover.Slope, session.History.UndoLabel));

        Headless.Find<Slider>(window, "slider_by_height").Value = 0.6;
        Assert.Equal((0.6, "Set hills height"), (open.Terrain.AutoCover.HeightFalloff, session.History.UndoLabel));

        Headless.Find<Slider>(window, "slider_sharpness").Value = 0.9;
        Assert.Equal((0.9, "Set hills blend"), (open.Terrain.BlendSharpness, session.History.UndoLabel));
        Assert.Equal(2.5, open.Terrain.AutoCover.Slope);
    });
}
