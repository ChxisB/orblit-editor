using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

/// <summary>What the preview beside the browser shows for the file picked, over a real folder.</summary>
public sealed class AssetPreviewTests : IDisposable
{
    // A real 2 by 2 picture, small enough to keep in a test and whole enough to decode.
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEklEQVR4nGNg0DgBQhUaPUAEABo6BDmBaztmAAAAAElFTkSuQmCC";

    private const string Gltf = """{"asset":{"version":"2.0"},"meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}],"accessors":[{"type":"VEC3","min":[-1,-1,-1],"max":[1,1,1]}]}""";

    private readonly string _root = Directory.CreateTempSubdirectory("orblit_preview").FullName;
    private readonly List<AssetTree> _trees = [];

    public void Dispose()
    {
        foreach (var tree in _trees)
        {
            tree.Dispose();
        }

        Directory.Delete(_root, recursive: true);
    }

    private void Write(string name, string text) => File.WriteAllText(Path.Join(_root, name), text);

    private Window Show(bool renderer = false, double width = 1000, double height = 700)
    {
        var tree = new AssetTree(_root);
        _trees.Add(tree);
        var window = new Window { Width = width, Height = height, Content = new AssetBrowserPanel(tree, renderer: renderer) };
        window.Show();
        Headless.Render();
        return window;
    }

    private static AssetPreviewPanel Preview(Window window) => Assert.Single(Headless.All<AssetPreviewPanel>(window));

    private static void Pick(Window window, string name) =>
        Headless.Click(window, Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == name));

    private static bool Says(Control root, string text) =>
        Headless.All<TextBlock>(root).Any(one => one.Text?.Contains(text, StringComparison.Ordinal) == true);

    [Fact]
    public Task WhereTheRendererDoesNotRunAModelSaysSoAndHasNoRenderer() => Headless.Run(() =>
    {
        Write("barrel.gltf", Gltf);
        var window = Show(renderer: false);

        Pick(window, "barrel.gltf");

        Assert.True(Says(Preview(window), "not available on this platform"));
        Assert.Null(Preview(window).Renderer);
    });

    [Fact]
    public Task AFileItCannotDrawIsNamedByItsKind() => Headless.Run(() =>
    {
        Write("player.cpp", "int x;");
        var window = Show(renderer: true);

        Pick(window, "player.cpp");

        Assert.True(Says(Preview(window), AssetKind.Native.Label));
        Assert.Null(Preview(window).Renderer);
        Assert.True(Assert.Single(Headless.All<Glyph>(Assert.Single(Headless.All<PreviewNote>(Preview(window))))).IsVisible);
    });

    [Fact]
    public Task APictureIsShownAsItself() => Headless.Run(() =>
    {
        File.WriteAllBytes(Path.Join(_root, "colormap.png"), Convert.FromBase64String(Png));
        var window = Show();

        Pick(window, "colormap.png");
        var held = Headless.Find<Border>(Preview(window), "preview_picture");

        Assert.True(Headless.Until(() => Headless.All<Image>(held).Any()));
        Assert.IsAssignableFrom<Bitmap>(Assert.Single(Headless.All<Image>(held)).Source);
    });

    [Fact]
    public Task APictureThatWillNotDecodeSaysSoRatherThanBreaking() => Headless.Run(() =>
    {
        Write("bad.png", "this is not a picture");
        var window = Show();

        Pick(window, "bad.png");

        Assert.True(Headless.Until(() => Says(Preview(window), "could not be read as an image")));
    });

    [Fact]
    public Task TheTurnButtonStopsAndStartsTheModel() => Headless.Run(() =>
    {
        Write("barrel.gltf", Gltf);
        var window = Show();
        Pick(window, "barrel.gltf");
        Assert.True(Preview(window).Turning);

        Headless.Click(window, "preview_turn");
        Assert.False(Preview(window).Turning);

        Headless.Click(window, "preview_turn");
        Assert.True(Preview(window).Turning);
    });

    [Fact]
    public Task AFileWithNothingToTurnHasNoTurnButton() => Headless.Run(() =>
    {
        Write("player.cpp", "int x;");
        var window = Show();

        Pick(window, "player.cpp");

        Assert.False(Headless.Find<Button>(window, "preview_turn").IsVisible);
    });

    [Fact]
    public Task ThePreviewCanBeShutForANarrowWindowAndOpenedAgain() => Headless.Run(() =>
    {
        Write("barrel.gltf", Gltf);
        var window = Show();
        Pick(window, "barrel.gltf");
        Assert.NotNull(Preview(window));

        Headless.Click(window, "browser_preview");

        // Gone from the window and not hidden in it, so what drew the model is freed too.
        Assert.Empty(Headless.All<AssetPreviewPanel>(window));

        Headless.Click(window, "browser_preview");
        Assert.Equal("barrel.gltf", Preview(window).Asset?.Name);
    });

    [Fact]
    public Task ANarrowPanelHasNeitherThePreviewNorTheControlForIt() => Headless.Run(() =>
    {
        Write("barrel.gltf", Gltf);
        var window = Show(width: 500);

        Assert.Empty(Headless.All<AssetPreviewPanel>(window));
        Assert.False(Headless.Find<Button>(window, "browser_preview").IsVisible);
    });

    [Fact]
    public Task APanelDraggedShortStillSaysWhatTheFileIsAndDropsTheIcon() => Headless.Run(() =>
    {
        // The dock it lives in can be dragged down to a couple of rows. An icon over two lines of words
        // does not fit there, and the words are the part that matters.
        Write("player.cpp", "int x;");
        var window = Show(width: 900, height: 132);

        Pick(window, "player.cpp");
        var note = Assert.Single(Headless.All<PreviewNote>(Preview(window)));

        Assert.True(Says(note, AssetKind.Native.Label));
        Assert.All(Headless.All<Glyph>(note), icon => Assert.False(icon.IsVisible));
    });

    [Fact]
    public Task AModelIsHandedToTheRendererWhereThereIsOne() => Headless.Run(() =>
    {
        Write("barrel.gltf", Gltf);
        var window = Show(renderer: true);

        Pick(window, "barrel.gltf");
        Assert.NotNull(Preview(window).Renderer);

        Headless.Click(window, "browser_preview");
        Headless.Wait(100);

        Assert.Empty(Headless.All<AssetPreviewPanel>(window));
    });
}
