using Avalonia;

namespace Orblit.Editor;

using Orblit.Editor.Core;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // A folder with a project in it opens straight into the editor. Anything else shows the launcher.
        var project = LaunchArguments.ProjectPath(args) is { } path ? ProjectStore.Open(Path.GetFullPath(path)) : null;
        return AppBuilder.Configure(() => new EditorApp(() => project is null ? Launch() : OpenWindow(project.Directory)))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }

    // shortcut: the recents list is kept under the user's config folder, not where the Dart editor keeps
    // its own, so the two editors do not share it. Share it when a project is opened in both.
    private static string RecentsFile() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Orblit",
        "recent.json");

    private static LauncherWindow Launch()
    {
        var session = new LauncherSession(new ProjectStore(RecentsFile()), HostPaths.HomeDirectory());
        var launcher = new LauncherWindow(session, splash: TimeSpan.FromMilliseconds(1100));
        session.Opened += project =>
        {
            // The editor opens before the launcher closes, so the last window is never gone in between.
            OpenWindow(project.Directory).Show();
            launcher.Close();
        };
        return launcher;
    }

    private static EditorWindow OpenWindow(string directory)
    {
        var prefabs = new PrefabFiles(directory);
        var opened = ProjectWorkspace.Open(directory, prefabs.Find);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn())
        {
            Prefabs = prefabs.Find,
        };
        session.Report(opened.Problems);
        return new EditorWindow(session);
    }
}
