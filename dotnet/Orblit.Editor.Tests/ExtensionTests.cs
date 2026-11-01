using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// What something outside the editor can add to it, arriving the way the built-in ones do. A panel
// and a mode are tried here through the window. A gizmo is tried in ViewportInputTests, where the
// view takes its list.
public sealed class ExtensionTests : IDisposable
{
    private static readonly PanelKind NotesKind = new("notes", "Notes", "sticky_note_2_outlined");

    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_extend");

    public void Dispose() => _temp.Delete(recursive: true);

    private static void Notes(Registry<PanelType> panels) => panels.Register(new PanelType(NotesKind));

    private static DockLayout OnlyTheScene() => new(new DockGroup("only", [new DockPanel("scene", PanelKind.Viewport)]));

    private (EditorSession Session, EditorWindow Window) Open(Action<Registry<PanelType>>? extend = null, Registry<EditorMode>? modes = null)
    {
        var panels = PanelTypes.BuiltIn();
        extend?.Invoke(panels);
        var opened = ProjectWorkspace.Open(_temp.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), modes ?? EditorModes.Standard(), panels);
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        return (session, window);
    }

    private static string[] PanelIds(DockLayout layout) => [.. layout.Panels.Select(panel => panel.Id)];

    private static bool Has(Control root, string name) => Headless.All<Control>(root).Any(one => one.Name == name);

    private static void Choose(EditorWindow window, string name) =>
        Headless.Find<MenuItem>(window, name).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    [Fact]
    public Task APanelOpensFromThePanelsMenuAndComesBackWithTheLayout() => Headless.Run(() =>
    {
        var (_, first) = Open(Notes);
        Assert.False(Has(first, "tab_notes"));

        Choose(first, "panel_notes");
        Assert.True(Has(first, "tab_notes"));
        first.Close();

        var (_, again) = Open(Notes);
        Assert.True(Has(again, "tab_notes"));
        again.Close();
    });

    [Fact]
    public Task WithoutWhatRegisteredItTheRestOfTheLayoutOpens() => Headless.Run(() =>
    {
        var (_, first) = Open(Notes);
        Choose(first, "panel_notes");
        first.Close();

        var (session, again) = Open();

        Assert.False(Has(again, "tab_notes"));
        Assert.DoesNotContain(session.Layout.Panels, panel => panel.Kind == NotesKind);
        Assert.Contains(session.Layout.Panels, panel => panel.Kind == PanelKind.Outliner);
        Assert.Contains(session.Layout.Panels, panel => panel.Kind == PanelKind.Viewport);
        Assert.All(session.Layout.Panels, panel => Assert.True(Has(again, $"tab_{panel.Id}")));
        again.Close();
    });

    [Fact]
    public Task AModeBringsItsOwnPanelsAndKeepsTheArrangementForItself() => Headless.Run(() =>
    {
        var modes = EditorModes.Standard();
        modes.Register(new EditorMode { Name = "ground", Label = "Ground", Icon = "landscape_outlined", Layout = OnlyTheScene });
        var (session, window) = Open(modes: modes);

        // It opens in the scene, as it always has.
        Assert.Equal("scene", session.Mode.Name);
        Assert.True(Has(window, "tab_outliner"));

        Headless.Click(window, "mode_ground");
        Assert.False(Has(window, "tab_outliner"));
        Assert.False(Has(window, "tab_console"));

        // Arranged in this mode, kept for this mode.
        Choose(window, "panel_console");
        Assert.True(Has(window, "tab_console"));
        Assert.Contains("\"console\"", File.ReadAllText(Path.Combine(_temp.FullName, ".orblit", "layout.ground.json")));

        Headless.Click(window, "mode_scene");
        Assert.True(Has(window, "tab_outliner"));
        Assert.Equal(PanelIds(DockLayout.Standard()), PanelIds(session.Layout));
        Assert.False(File.Exists(Path.Combine(_temp.FullName, ".orblit", "layout.json")));

        Headless.Click(window, "mode_ground");
        Assert.True(Has(window, "tab_console"));
        window.Close();
    });
}
