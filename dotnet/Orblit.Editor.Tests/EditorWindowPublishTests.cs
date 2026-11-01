using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The ways into the publish dialog: the File menu and the command palette. The store's way in from the File
// menu is covered too.
public sealed class EditorWindowPublishTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_publish");

    public void Dispose() => _project.Delete(recursive: true);

    private EditorWindow Open()
    {
        var opened = ProjectWorkspace.Open(_project.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return window;
    }

    private static IEnumerable<MenuItem> Items(ItemsControl root) =>
        root.Items.OfType<MenuItem>().SelectMany(item => new[] { item }.Concat(Items(item)));

    private static void Choose(Window window, string name)
    {
        Items(Headless.Find<Menu>(window, "menu")).First(one => one.Name == name).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.Render();
    }

    [Fact]
    public Task TheFileMenuOpensIt() => Headless.Run(() =>
    {
        var window = Open();

        Choose(window, "menu_publish_kit");

        var dialog = Assert.IsType<PublishDialog>(Assert.Single(window.OwnedWindows));
        Assert.Contains(Headless.All<TextBlock>(dialog), block => block.Text == "FOLDER IN THE PROJECT");
    });

    [Fact]
    public Task ThePaletteFindsIt() => Headless.Run(() =>
    {
        var window = Open();
        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Meta);
        Headless.Render();
        var palette = Assert.IsType<PaletteDialog>(Assert.Single(window.OwnedWindows));

        Headless.All<TextBox>(palette).First(box => box.Name == "field").Text = "publish kit";
        Headless.Render();
        palette.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Headless.Render();
        Headless.Render();

        Assert.IsType<PublishDialog>(Assert.Single(window.OwnedWindows));
    });

    // The store opens offline, so nothing is fetched. Its settings are read from the editor's support folder.
    [Fact]
    public Task TheFileMenuOpensTheKitStore() => Headless.Run(() =>
    {
        var window = Open();

        Choose(window, "menu_kit_store");

        Assert.IsType<StoreDialog>(Assert.Single(window.OwnedWindows));
    });
}
