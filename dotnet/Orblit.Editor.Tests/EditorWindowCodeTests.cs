using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The menu entries, and the open in code, reveal and build actions, over fake programs, so nothing is
// started. A build is asked of the window directly, because the tile's menu that asks for it opens in a popup
// no helper reaches. That the menu offers Build is in AssetBrowserTests.
public sealed class EditorWindowCodeTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_code");
    private readonly FakePrograms _programs = new();

    public EditorWindowCodeTests() => _programs.Answers.Add("which code");

    public void Dispose() => _project.Delete(recursive: true);

    private string Folder => _project.FullName;

    private (EditorSession Session, EditorWindow Window) Open()
    {
        var opened = ProjectWorkspace.Open(Folder);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS, renderer: false, new CodeEditor(_programs, HostPlatform.MacOS));
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static IEnumerable<MenuItem> Items(ItemsControl root) =>
        root.Items.OfType<MenuItem>().SelectMany(item => new[] { item }.Concat(Items(item)));

    private static MenuItem Menu(Window window, string name) =>
        Items(Headless.Find<Menu>(window, "menu")).First(one => one.Name == name);

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static string? Status(Window window) => Headless.Find<TextBlock>(window, "status").Text;

    [Fact]
    public Task TheFileMenuNamesTheEditorFoundAndTheMacsFileBrowser() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Assert.Equal("Open project in Visual Studio Code", Menu(window, "menu_open_in_code").Header);
        Assert.Equal("Show in Finder", Menu(window, "menu_reveal").Header);
    });

    [Fact]
    public Task OpeningTheProjectFromTheMenuSaysWhereItWent() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Click(Menu(window, "menu_open_in_code"));
        Headless.Render();

        Assert.Equal([$"code {Folder}"], _programs.Started);
        Assert.Equal("Opened the project in Visual Studio Code.", Status(window));
    });

    [Fact]
    public Task ShowInFinderShowsTheProjectFolder() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Click(Menu(window, "menu_reveal"));

        Assert.Equal([$"open {Folder}"], _programs.Started);
    });

    [Fact]
    public Task WithNoEditorTheMenuOffersVsCodeAndSaysWhatToInstall() => Headless.Run(() =>
    {
        _programs.Answers.Clear();
        var (_, window) = Open();

        Click(Menu(window, "menu_open_in_code"));
        Headless.Render();

        Assert.Equal("Open project in VS Code", Menu(window, "menu_open_in_code").Header);
        Assert.StartsWith("No code editor found.", Status(window), StringComparison.Ordinal);
    });

    [Fact]
    public Task OpeningAScriptFromProjectOpensItInTheEditor() => Headless.Run(() =>
    {
        File.WriteAllText(Path.Combine(Folder, "hero.cpp"), "");
        var (session, window) = Open();
        session.OpenPanel(PanelKind.Project);
        Headless.Render();

        Headless.DoubleClick(window, Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == "hero.cpp"));
        Headless.Render();

        Assert.Equal([$"code {Folder} --goto {Path.Combine(Folder, "hero.cpp")}"], _programs.Started);
        Assert.Equal("Opened hero.cpp in Visual Studio Code.", Status(window));
    });

    [Fact]
    public Task ThePaletteOpensTheProjectInTheEditor() => Headless.Run(() =>
    {
        var (_, window) = Open();
        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Meta);
        Headless.Render();
        var palette = Assert.IsType<PaletteDialog>(Assert.Single(window.OwnedWindows));

        Headless.All<TextBox>(palette).First(box => box.Name == "field").Text = "open in code";
        Headless.Render();
        palette.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Headless.Render();
        Headless.Render();

        Assert.Equal([$"code {Folder}"], _programs.Started);
    });

    [Fact]
    public Task AScriptThatDoesNotBuildSaysSoAndShowsWhatTheCompilerSaid() => Headless.Run(() =>
    {
        var path = Path.Combine(Folder, "broken.cpp");
        File.WriteAllText(path, "#include \"orblit_script.h\"\nnonsense here");
        var (session, window) = Open();

        window.BuildScript(new Asset("broken.cpp", path, AssetKind.Native));

        Assert.True(Headless.Until(() => window.OwnedWindows.Count == 1));
        var dialog = Assert.IsType<BuildDialog>(window.OwnedWindows[0]);
        Assert.Equal("broken.cpp did not build", dialog.Title);
        Assert.NotEqual("The compiler said nothing.", Headless.All<SelectableTextBlock>(dialog).Single(block => block.Name == "build_output").Text);
        Assert.Equal(("broken.cpp did not build.", SayLevel.Error), session.Last);
    });

    [Fact]
    public Task AWindowClosedDuringABuildIsLeftClosed() => Headless.Run(() =>
    {
        var path = Path.Combine(Folder, "broken.cpp");
        File.WriteAllText(path, "nonsense here");
        var (session, window) = Open();

        window.BuildScript(new Asset("broken.cpp", path, AssetKind.Native));
        window.Close();
        Headless.Render();

        Assert.False(window.IsVisible);
        Headless.Wait(3000);
        Assert.Equal("Building broken.cpp…", session.Last?.Text);
        Assert.Empty(window.OwnedWindows);
    });

    // There is no case for a clean build. The linker since ld-27037 warns about the -undefined error
    // that Orblit.Native passes, so every build on such a Mac comes with warnings.
    [Fact]
    public Task AScriptThatBuildsWithWarningsSaysSoAndShowsThem() => Headless.Run(() =>
    {
        var path = Path.Combine(Folder, "tuned.cpp");
        File.WriteAllText(path, """
            #include "orblit_script.h"
            #warning "tune this"
            ORBLIT_SCRIPT {}
            extern "C" void orblit_step(double d) { (void)d; }
            extern "C" void orblit_stop(void) {}
            """);
        var (session, window) = Open();

        window.BuildScript(new Asset("tuned.cpp", path, AssetKind.Native));

        Assert.True(Headless.Until(() => window.OwnedWindows.Count == 1));
        var dialog = Assert.IsType<BuildDialog>(window.OwnedWindows[0]);
        Assert.Equal("tuned.cpp built, with warnings", dialog.Title);
        Assert.Contains("tune this", Headless.All<SelectableTextBlock>(dialog).Single(block => block.Name == "build_output").Text, StringComparison.Ordinal);
        Assert.Equal(("Built tuned.cpp. With warnings.", SayLevel.Warning), session.Last);
    });
}
