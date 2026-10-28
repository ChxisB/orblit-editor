using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Ui;

// Dart: orblit-editor/test/editor_shell_workspaces_test.dart, the Interface workspace group's cases
// about the window: opening from Project, nothing open, and the top bar. The rest of the group is the
// session's, in EditorSessionInterfacesTests. The top bar here has no object count or frame rate to
// hide. Then editor_shell_data_test.dart, the "an interface on a scene" group. A drop onto the scene
// view is not ported, so PutInterfaceOnScene, which is what the drop does, stands in for it. Saving
// with the scene and replacing on a chosen canvas are the session's.
public sealed class EditorWindowInterfacesTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_interfaces");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private (EditorSession Session, EditorWindow Window) Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static void Enter(EditorSession session, string mode)
    {
        session.EnterMode(mode);
        Headless.Render();
    }

    private static void Press(Control root, string name)
    {
        Headless.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Headless.Render();
    }

    private static string Title(Window window) => Headless.Find<TextBlock>(window, "title").Text ?? "";

    private string WriteHud()
    {
        var path = PathOf("hud.oui");
        var hud = new UiDocument
        {
            Root = new UiNode
            {
                Type = "stack",
                Classes = "w-full h-full",
                Children = [new UiNode { Type = "text", Css = "left: 40px; top: 40px", Text = "Health 100" }],
            },
        };
        File.WriteAllText(path, hud.ToText());
        return path;
    }

    private static bool Shows(Control root, string text) => Headless.All<TextBlock>(root).Any(one => one.Text == text);

    private static bool OverTheScene(Window window, string text)
    {
        var drawn = Headless.Find<Control>(window, "viewport_interface");
        return drawn.IsEffectivelyVisible && Shows(drawn, text);
    }

    private static TextBlock Chip(Window window, string text) =>
        Headless.All<TextBlock>(Headless.All<SceneViewport>(window).Single()).First(one => one.Text == text);

    [Fact]
    public Task WithNothingOpenItOffersToMakeOne() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Enter(session, "interface");

        Assert.True(Headless.Find<Control>(window, "interface_nothing").IsEffectivelyVisible);
        // Once, on the canvas. A second on the shelf read as a second thing.
        Assert.False(Headless.Find<Control>(window, "shelf_new_interface").IsEffectivelyVisible);

        Press(window, "button_new_interface");

        Assert.True(File.Exists(PathOf("interfaces/screen.oui")));
        Assert.False(Headless.Find<Control>(window, "interface_nothing").IsEffectivelyVisible);
        // The shelf's, for the next one.
        Assert.True(Headless.Find<Control>(window, "shelf_new_interface").IsEffectivelyVisible);
    });

    [Fact]
    public Task TheTopBarNamesTheInterface() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var scene = session.Workspace.Loaded!.Title;
        Enter(session, "interface");
        Press(window, "button_new_interface");

        // Where it is in the project, since the shelf gives only its name.
        Assert.Equal("interfaces/screen.oui", Title(window));

        Press(window, "add_box");
        Assert.Equal("interfaces/screen.oui •", Title(window));

        Enter(session, "scene");
        Assert.StartsWith(scene, Title(window), StringComparison.Ordinal);
    });

    [Fact]
    public Task OpeningAnInterfaceFromProjectGoesThere() => Headless.Run(() =>
    {
        WriteHud();
        var (session, window) = Open();
        session.OpenPanel(PanelKind.Project);
        Headless.Render();

        Headless.DoubleClick(window, Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == "hud.oui"));
        Headless.Render();

        Assert.Equal("interface", session.Mode.Name);
        Assert.True(Shows(Headless.Find<Control>(window, "interface_surface"), "Health 100"));
        Assert.True(Shows(Headless.Find<Control>(window, "interface_elements"), "Health 100"));
    });

    [Fact]
    public Task TheSceneViewShowsTheInterfaceItsCanvasNames() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Assert.False(OverTheScene(window, "Health 100"));

        session.PutInterfaceOnScene(WriteHud());
        Headless.Render();

        Assert.True(OverTheScene(window, "Health 100"));
    });

    [Fact]
    public Task TheCanvasSaysWhichInterfaceItShowsAndOpensIt() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.PutInterfaceOnScene(WriteHud());
        Headless.Render();

        var link = Headless.Find<TextBlock>(window, "interface_link");
        Assert.Equal("hud.oui", link.Text);
        Headless.Click(window, link);
        Headless.Render();

        Assert.Equal("interface", session.Mode.Name);
        Assert.Equal("hud.oui", session.Interfaces.Shown?.Name);
    });

    [Fact]
    public Task HidingTheCanvasTakesTheInterfaceOffTheSceneView() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.PutInterfaceOnScene(WriteHud());
        Headless.Render();

        Headless.Click(window, "inspector_visible");
        Headless.Render();

        // What hides it here is what hides it in the game: the object's own visibility, not a view setting.
        Assert.False(session.Workspace.Loaded!.Scene!.Objects.Single(one => one.Kind == ObjectKind.Canvas).Visible);
        Assert.False(OverTheScene(window, "Health 100"));
    });

    [Fact]
    public Task TheViewCanPutItAsideWithoutChangingTheScene() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.PutInterfaceOnScene(WriteHud());
        session.Save();
        Headless.Render();

        Headless.Click(window, Chip(window, "Interface"));
        Headless.Render();

        Assert.False(OverTheScene(window, "Health 100"));
        // A view setting, so the scene is still saved.
        Assert.False(Title(window).EndsWith(" •", StringComparison.Ordinal));

        Headless.Click(window, Chip(window, "Interface"));
        Headless.Render();
        Assert.True(OverTheScene(window, "Health 100"));
    });
}
