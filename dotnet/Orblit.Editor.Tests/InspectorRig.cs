using Avalonia.Controls;
using Avalonia.Input;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>An inspector in a window, over a workspace, with somewhere to move focus to.</summary>
internal static class InspectorRig
{
    internal static (EditorSession Session, Window Window) Open(EditorScene? scene = null, InspectorLinks? links = null)
    {
        var workspace = new Workspace("/project");
        if (scene is not null || !workspace.IsEmpty)
        {
            workspace.Add(new SceneEntry("main", "Main", scene: scene));
        }

        return Show(workspace, links);
    }

    internal static (EditorSession Session, Window Window) Show(Workspace workspace, InspectorLinks? links)
    {
        var session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var panel = new InspectorPanel(session, links);
        var frame = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        frame.Children.Add(panel);
        var elsewhere = new Button { Name = "elsewhere", Content = "elsewhere" };
        elsewhere[Grid.RowProperty] = 1;
        frame.Children.Add(elsewhere);
        var window = new Window { Width = 360, Height = 900, Content = frame };
        window.Show();
        Headless.Render();
        return (session, window);
    }

    internal static (EditorSession Session, Window Window) Selecting(string id, EditorScene? scene = null, InspectorLinks? links = null)
    {
        var (session, window) = Open(scene ?? EditorScene.Starter(), links);
        session.Select([id]);
        Headless.Render();
        return (session, window);
    }

    internal static bool Has(Control root, string name) => Headless.All<Control>(root).Any(one => one.Name == name);

    internal static string TextOf(Control root, string name) => Headless.Find<TextBlock>(root, name).Text ?? "";
}
