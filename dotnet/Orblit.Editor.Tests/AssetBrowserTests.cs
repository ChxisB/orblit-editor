using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

/// <summary>The project's files as the browser draws them, over a real folder.</summary>
public sealed class AssetBrowserTests : IDisposable
{
    // A real 2 by 2 picture, small enough to keep in a test and whole enough to decode.
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEklEQVR4nGNg0DgBQhUaPUAEABo6BDmBaztmAAAAAElFTkSuQmCC";

    private readonly string _root = Directory.CreateTempSubdirectory("orblit_browser").FullName;
    private readonly List<AssetTree> _trees = [];

    public void Dispose()
    {
        foreach (var tree in _trees)
        {
            tree.Dispose();
        }

        Directory.Delete(_root, recursive: true);
    }

    private string At(string name) => Path.Join(_root, name);

    private void Write(string name, string text = "x") => File.WriteAllText(At(name), text);

    private CookStatusIndex Cook(params (string Name, CookState State)[] states)
    {
        var index = new CookStatusIndex(_root, At("cache"));
        index.Hold(states.ToDictionary(one => At(one.Name), one => one.State));
        return index;
    }

    private (AssetBrowserPanel Panel, Window Window) Show(CookStatusIndex? cook = null, AssetBrowserLinks? links = null, double width = 1000)
    {
        var tree = new AssetTree(_root);
        _trees.Add(tree);
        var panel = new AssetBrowserPanel(tree, cook, links);
        var window = new Window { Width = width, Height = 700, Content = panel };
        window.Show();
        Headless.Render();
        return (panel, window);
    }

    private static List<string> Names(Window window) => [.. Headless.All<AssetTile>(window).Select(tile => tile.Asset.Name)];

    private static AssetTile Tile(Window window, string name) => Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == name);

    private static Border Thumb(AssetTile tile) => Headless.All<Border>(tile).First(one => Equals(one.Tag, "thumb"));

    private static List<Border> Dots(Window window) => [.. Headless.All<Border>(window).Where(one => Equals(one.Tag, AssetTile.CookDot))];

    private static void Type(Window window, string text)
    {
        Headless.Find<TextBox>(window, "browser_filter").Text = text;
        Headless.Render();
    }

    private static string CountText(Window window) => Headless.Find<TextBlock>(window, "browser_count").Text!;

    private static bool Says(Window window, string text) => Headless.All<TextBlock>(window).Any(one => one.Text == text);

    [Fact]
    public Task ATextureTilePointsAtThePictureInTheFile() => Headless.Run(() =>
    {
        File.WriteAllBytes(At("rock.png"), Convert.FromBase64String(Png));
        var (_, window) = Show();

        Assert.True(Headless.Until(() => Headless.All<Image>(window).Any()));
        Assert.IsAssignableFrom<Bitmap>(Assert.Single(Headless.All<Image>(window)).Source);
    });

    [Fact]
    public Task ATextureItCannotDecodeKeepsTheIcon() => Headless.Run(() =>
    {
        File.WriteAllBytes(At("rock.ktx2"), [0xAB, 0x4B, 0x54]);
        var (_, window) = Show();
        Headless.Wait(100);

        Assert.Empty(Headless.All<Image>(window));
        Assert.IsType<Glyph>(Thumb(Tile(window, "rock.ktx2")).Child);
    });

    [Fact]
    public Task APictureThatIsNotOneKeepsTheIconAfterTryingIt() => Headless.Run(async () =>
    {
        Write("bad.png", "this is not a picture");
        var (_, window) = Show();
        var load = AssetThumbnails.LoadAsync(At("bad.png"));
        Assert.True(Headless.Until(() => load.IsCompleted));
        Assert.Null(await load);
        Headless.Wait(100);

        Assert.Empty(Headless.All<Image>(window));
        Assert.IsType<Glyph>(Thumb(Tile(window, "bad.png")).Child);
    });

    [Fact]
    public Task AnAssetThatNeedsCookingIsMarkedAsSo() => Headless.Run(() =>
    {
        Write("ship.gltf", "{}");
        var (_, window) = Show(Cook(("ship.gltf", CookState.Stale)));

        Assert.Same(OrblitColors.Warn, Assert.Single(Dots(window)).Background);
    });

    [Fact]
    public Task ACookedAssetIsMarkedDifferently() => Headless.Run(() =>
    {
        Write("ship.gltf", "{}");
        var (_, window) = Show(Cook(("ship.gltf", CookState.Cooked)));

        Assert.Same(OrblitColors.Good, Assert.Single(Dots(window)).Background);
    });

    [Fact]
    public Task AnAssetThatWouldNotCookIsMarkedAsSo() => Headless.Run(() =>
    {
        Write("ship.gltf", "{}");
        var (_, window) = Show(Cook(("ship.gltf", CookState.Failed)));

        Assert.Same(OrblitColors.Bad, Assert.Single(Dots(window)).Background);
    });

    [Fact]
    public Task AFileWithNothingToCookIsLeftUnmarked() => Headless.Run(() =>
    {
        Write("notes.md");
        var (_, window) = Show(Cook(("notes.md", CookState.Ignored)));

        Assert.Empty(Dots(window));
    });

    [Fact]
    public Task NothingIsMarkedWithoutACookToAsk() => Headless.Run(() =>
    {
        Write("ship.gltf", "{}");
        var (_, window) = Show();

        Assert.Empty(Dots(window));
    });

    [Fact]
    public Task TheHeaderSaysWhatIsLeftToDo() => Headless.Run(() =>
    {
        Write("a.gltf", "{}");
        Write("b.gltf", "{}");
        Write("c.gltf", "{}");
        var (_, window) = Show(Cook(("a.gltf", CookState.Stale), ("b.gltf", CookState.Failed), ("c.gltf", CookState.Cooked)));

        Assert.Equal("1 would not cook · 1 to cook", Headless.Find<TextBlock>(window, "browser_cook").Text);
    });

    [Fact]
    public Task TheHeaderSaysNothingWhenThereIsNothingToDo() => Headless.Run(() =>
    {
        Write("a.gltf", "{}");
        var (_, window) = Show(Cook(("a.gltf", CookState.Cooked)));

        Assert.DoesNotContain(Headless.All<TextBlock>(window), text => text.IsEffectivelyVisible && text.Text?.Contains("to cook", StringComparison.Ordinal) == true);
    });

    [Fact]
    public Task TheStateIsInTheTilesTooltip() => Headless.Run(() =>
    {
        Write("ship.gltf", "{}");
        var (_, window) = Show(Cook(("ship.gltf", CookState.Failed)));

        var tip = Assert.IsType<string>(ToolTip.GetTip(Tile(window, "ship.gltf")));
        Assert.StartsWith("ship.gltf", tip, StringComparison.Ordinal);
        Assert.Contains("Would not cook", tip, StringComparison.Ordinal);
        Assert.Contains("importer could not do it", tip, StringComparison.Ordinal);
    });

    [Fact]
    public Task ThingsThatAreNotPicturesKeepTheIcon() => Headless.Run(() =>
    {
        Write("player.ts");
        var (_, window) = Show();

        Assert.Empty(Headless.All<Image>(window));
        Assert.IsType<Glyph>(Thumb(Tile(window, "player.ts")).Child);
    });

    [Fact]
    public Task TheFilterKeepsTheFilesWhoseNameHasWhatWasTyped() => Headless.Run(() =>
    {
        Write("rock.ts");
        Write("rocket.ts");
        Write("tree.ts");
        var (_, window) = Show();

        Type(window, "rock");

        Assert.Equal(["rock.ts", "rocket.ts"], Names(window));
    });

    [Fact]
    public Task TheFilterIgnoresCaseAndTheSpacesAroundIt() => Headless.Run(() =>
    {
        Write("Rock.ts");
        var (_, window) = Show();

        Type(window, "  rOCK ");

        Assert.Equal(["Rock.ts"], Names(window));
    });

    [Fact]
    public Task TheFilterCountsWhatIsLeft() => Headless.Run(() =>
    {
        Write("rock.ts");
        Write("rocket.ts");
        Write("tree.ts");
        var (_, window) = Show();
        Assert.Equal("3 items", CountText(window));

        Type(window, "tree");

        Assert.Equal("1 item", CountText(window));
    });

    [Fact]
    public Task TheFilterSaysNothingMatchedAndNotThatTheFolderIsEmpty() => Headless.Run(() =>
    {
        Write("rock.ts");
        var (_, window) = Show();

        Type(window, "zzz");

        Assert.True(Says(window, "Nothing here has that name."));
        Assert.False(Says(window, "This folder is empty."));
    });

    [Fact]
    public Task ClearingTheFilterBringsEveryFileBack() => Headless.Run(() =>
    {
        Write("rock.ts");
        Write("tree.ts");
        var (_, window) = Show();
        Type(window, "rock");

        Type(window, "");

        Assert.Equal(["rock.ts", "tree.ts"], Names(window));
    });

    [Fact]
    public Task AnEmptyFolderSaysSoAndOffersTheMenu() => Headless.Run(() =>
    {
        var (_, window) = Show();

        Assert.True(Says(window, "This folder is empty."));
        Assert.Equal("0 items", CountText(window));
        Assert.NotNull(Headless.Find<Button>(window, "browser_new"));
    });

    [Fact]
    public Task AClickPicksATileAndTellsTheEditor()
    {
        Write("a.ts");
        Write("b.ts");
        var told = new List<string?>();
        var (_, window) = (default(AssetBrowserPanel)!, default(Window)!);
        return Headless.Run(() =>
        {
            (_, window) = Show(links: new AssetBrowserLinks { OnSelectAsset = asset => told.Add(asset?.Name) });

            Headless.Click(window, Tile(window, "b.ts"));

            Assert.Equal(["b.ts"], told);
            Assert.True(Tile(window, "b.ts").Selected);
            Assert.False(Tile(window, "a.ts").Selected);
        });
    }

    [Fact]
    public Task OpeningAFolderShowsItsFilesAndUpGoesBack() => Headless.Run(() =>
    {
        Directory.CreateDirectory(At("models"));
        File.WriteAllText(At(Path.Join("models", "rock.ts")), "x");
        var told = new List<string?>();
        var (_, window) = Show(links: new AssetBrowserLinks { OnSelectAsset = asset => told.Add(asset?.Name) });
        Assert.False(Headless.Find<Button>(window, "browser_up").IsEnabled);

        Headless.DoubleClick(window, Tile(window, "models"));

        Assert.Equal(["rock.ts"], Names(window));
        Assert.Equal("models", Headless.Find<TextBlock>(window, "browser_crumb").Text);

        Headless.Click(window, "browser_up");

        Assert.Equal(["models"], Names(window));
        Assert.Equal("/", Headless.Find<TextBlock>(window, "browser_crumb").Text);
        Assert.Null(told[^1]);
    });

    [Fact]
    public Task TheRefreshButtonReadsTheFolderAgain() => Headless.Run(() =>
    {
        var (_, window) = Show();
        Write("late.ts");

        Headless.Click(window, "browser_refresh");

        Assert.Equal(["late.ts"], Names(window));
    });

    [Fact]
    public Task AMarkFollowsTheCookWhenItChangesAfterTheFirstDraw() => Headless.Run(() =>
    {
        Write("ship.gltf", "{}");
        var cook = Cook(("ship.gltf", CookState.Stale));
        var (_, window) = Show(cook);
        Assert.Same(OrblitColors.Warn, Assert.Single(Dots(window)).Background);

        cook.Hold(new Dictionary<string, CookState> { [At("ship.gltf")] = CookState.Cooked });
        Headless.Render();

        Assert.Same(OrblitColors.Good, Assert.Single(Dots(window)).Background);
    });

    [Fact]
    public Task MakingAFileSelectsItAndOpensIt() => Headless.Run(() =>
    {
        var opened = new List<string>();
        var (panel, window) = Show(links: new AssetBrowserLinks { OnOpenAsset = asset => opened.Add(asset.Name) });

        panel.MakeNew(NewAsset.Script, "player");

        Assert.Equal(["player.ts"], Names(window));
        Assert.True(File.Exists(At("player.ts")));
        Assert.Equal(["player.ts"], opened);
        Assert.True(Tile(window, "player.ts").Selected);
    });

    [Fact]
    public Task MakingAFolderDoesNotOpenIt() => Headless.Run(() =>
    {
        var opened = new List<string>();
        var (panel, window) = Show(links: new AssetBrowserLinks { OnOpenAsset = asset => opened.Add(asset.Name) });

        panel.MakeNew(NewAsset.Folder, "models");

        Assert.Equal(["models"], Names(window));
        Assert.Empty(opened);
    });

    [Fact]
    public Task AnameThatIsAPathIsRefusedWithAReason() => Headless.Run(() =>
    {
        var problems = new List<string>();
        var (panel, window) = Show(links: new AssetBrowserLinks { OnProblem = problems.Add });

        panel.MakeNew(NewAsset.Script, "a/b");

        Assert.Equal(["Could not create it: A name cannot be a path."], problems);
        Assert.Empty(Names(window));
    });

    [Fact]
    public Task DeletingAFileTakesItOffTheDiskAndOutOfTheList() => Headless.Run(() =>
    {
        Write("a.ts");
        Write("b.ts");
        var (panel, window) = Show();
        panel.Remove(Tile(window, "a.ts").Asset);

        Assert.Equal(["b.ts"], Names(window));
        Assert.False(File.Exists(At("a.ts")));
    });

    [Fact]
    public Task DeletingAFileThatIsGoneSaysSo() => Headless.Run(() =>
    {
        Write("a.ts");
        var problems = new List<string>();
        var (panel, window) = Show(links: new AssetBrowserLinks { OnProblem = problems.Add });
        var asset = Tile(window, "a.ts").Asset;
        File.Delete(At("a.ts"));

        panel.Remove(asset);

        Assert.Equal(["Could not delete a.ts: It is not there any more."], problems);
    });

    [Fact]
    public Task RenamingAFileKeepsItInTheList() => Headless.Run(() =>
    {
        Write("a.ts");
        var (panel, window) = Show();

        panel.Rename(Tile(window, "a.ts").Asset, "b.ts");

        Assert.Equal(["b.ts"], Names(window));
    });

    [Fact]
    public Task RenamingOverAnotherFileIsRefused() => Headless.Run(() =>
    {
        Write("a.ts");
        Write("b.ts");
        var problems = new List<string>();
        var (panel, window) = Show(links: new AssetBrowserLinks { OnProblem = problems.Add });

        panel.Rename(Tile(window, "a.ts").Asset, "b.ts");

        Assert.Single(problems, problem => problem.StartsWith("Could not rename a.ts: There is already", StringComparison.Ordinal));
        Assert.Equal(["a.ts", "b.ts"], Names(window));
    });

    [Fact]
    public Task ANarrowPanelPutsTheFoldersOverTheFilesAndDropsTheFilter() => Headless.Run(() =>
    {
        Write("a.ts");
        var (_, window) = Show(width: 300);

        Assert.False(Headless.Find<TextBox>(window, "browser_filter").IsVisible);
        var folders = Headless.All<AssetFolderTree>(window).Single();
        var grid = Headless.All<AssetGrid>(window).Single();
        Assert.True(Grid.GetRow(folders) < Grid.GetRow(grid));
        Assert.Equal(Grid.GetColumn(folders), Grid.GetColumn(grid));
    });

    [Fact]
    public Task AWidePanelPutsTheFoldersBesideTheFiles() => Headless.Run(() =>
    {
        var (_, window) = Show(width: 1000);

        Assert.True(Headless.Find<TextBox>(window, "browser_filter").IsVisible);
        var folders = Headless.All<AssetFolderTree>(window).Single();
        var grid = Headless.All<AssetGrid>(window).Single();
        Assert.True(Grid.GetColumn(folders) < Grid.GetColumn(grid));
    });

    [Fact]
    public void TheGridCountsColumnsByTheNarrowestTile()
    {
        Assert.Equal(1, AssetGrid.ColumnsFor(50));
        Assert.Equal(1, AssetGrid.ColumnsFor(138));
        Assert.Equal(2, AssetGrid.ColumnsFor(258));
        Assert.Equal(8, AssetGrid.ColumnsFor(1000));
        Assert.Equal(64, AssetGrid.ColumnsFor(100_000));
    }

    [Fact]
    public Task TheFolderTreeListsTheProjectThenEachFolderUnderIt() => Headless.Run(() =>
    {
        Directory.CreateDirectory(At(Path.Join("models", "rocks")));
        var (_, window) = Show();

        var rows = Headless.All<FolderRow>(window).Select(row => (string?)row.Tag).ToList();

        Assert.Equal(["Project", "models", "rocks"], rows);
    });

    [Fact]
    public Task ARightClickMenuOffersWhatCanBeDoneToAFileAndWhatCanBeMade()
    {
        var done = new List<string>();
        var acting = new AssetActions(() => done.Add("open"), () => done.Add("rename"), () => done.Add("delete"), () => done.Add("build"));
        return Headless.Run(() =>
        {
            var menu = AssetMenu.Build(_ => { }, acting);
            var tags = menu.Items.OfType<MenuItem>().Select(item => item.Tag).ToList();

            Assert.Equal(["Build", "Open", "Rename", "Delete"], tags.Take(4));
            Assert.Contains(NewAsset.Folder, tags);
            Assert.Equal(NewAssetGroup.Values.Count, tags.OfType<NewAssetGroup>().Count());
            Assert.Contains(NewAsset.Scene, tags);

            menu.Items.OfType<MenuItem>().First(item => Equals(item.Tag, "Rename")).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(["rename"], done);
        });
    }

    [Fact]
    public Task AMenuOnTheEmptySpaceHasNoActionsOnAFile() => Headless.Run(() =>
    {
        var menu = AssetMenu.Build(_ => { });

        Assert.Equal(NewAsset.Folder, menu.Items.OfType<MenuItem>().First().Tag);
    });

    [Fact]
    public Task AFileThatCanBeBuiltOffersBuildAndOthersDoNot() => Headless.Run(() =>
    {
        Write("hull.cpp");
        Write("plain.ts");
        var (_, window) = Show(links: new AssetBrowserLinks { OnBuild = _ => { } });

        Assert.True(Tile(window, "hull.cpp").Asset.CanBuild);
        Assert.False(Tile(window, "plain.ts").Asset.CanBuild);
    });
}
