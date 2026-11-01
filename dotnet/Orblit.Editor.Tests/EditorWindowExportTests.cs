using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The ways into the export dialog: the File menu and the command palette. The project has no exports folder,
// so nothing cooks.
public sealed class EditorWindowExportTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_export");

    public void Dispose() => _project.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) Open()
    {
        var opened = ProjectWorkspace.Open(_project.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static IEnumerable<MenuItem> Items(ItemsControl root) =>
        root.Items.OfType<MenuItem>().SelectMany(item => new[] { item }.Concat(Items(item)));

    private static void Export(Window window)
    {
        Items(Headless.Find<Menu>(window, "menu")).First(one => one.Name == "menu_export").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.Render();
    }

    [Fact]
    public Task TheFileMenuOpensIt() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Assert.Empty(window.OwnedWindows);

        Export(window);

        var dialog = Assert.IsType<ExportDialog>(Assert.Single(window.OwnedWindows));
        Assert.True(Headless.Until(() => Headless.All<TextBlock>(dialog).Any(block => block.Text?.StartsWith("Nothing exported yet", StringComparison.Ordinal) == true)));
    });

    [Fact]
    public Task ThePaletteFindsIt() => Headless.Run(() =>
    {
        var (_, window) = Open();
        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Meta);
        Headless.Render();
        var palette = Assert.IsType<PaletteDialog>(Assert.Single(window.OwnedWindows));

        Headless.All<TextBox>(palette).First(box => box.Name == "field").Text = "export";
        Headless.Render();
        palette.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Headless.Render();
        Headless.Render();

        Assert.IsType<ExportDialog>(Assert.Single(window.OwnedWindows));
    });

    [Fact]
    public Task ClosesWithoutDisturbingTheScene() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var unsaved = session.AnyUnsaved;
        Export(window);
        var dialog = Assert.IsType<ExportDialog>(Assert.Single(window.OwnedWindows));

        Headless.Click(dialog, "close");
        Headless.Render();

        Assert.Empty(window.OwnedWindows);
        Assert.Equal(unsaved, session.AnyUnsaved);
    });
}
