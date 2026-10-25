using Avalonia;

namespace Orblit.Editor;

using Orblit.Editor.Core;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // The launcher takes the place of this when there is no folder to open (task 14, row 7).
        if (args.Length == 0 || !Directory.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: Orblit.Editor <project folder>");
            return 2;
        }

        var directory = Path.GetFullPath(args[0]);
        return AppBuilder.Configure(() => new EditorApp(() => OpenWindow(directory)))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
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
