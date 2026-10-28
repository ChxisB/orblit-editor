using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Dart: orblit-editor/test/editor_shell_workspaces_test.dart, the Interface workspace group's two
// cases about the window: nothing open, and the top bar. The rest of the group is the session's, in
// EditorSessionInterfacesTests. The top bar here has no object count or frame rate to hide.
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
}
