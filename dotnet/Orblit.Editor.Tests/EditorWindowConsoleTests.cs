using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Dart: orblit-editor/test/editor_shell_data_test.dart, the console group. The console shares the
// bottom group with the project browser here, so "folded under the view" is "behind the project tab".
// A png is let go over the scene view as a real drop of what a browser tile's drag carries. Gone from the
// status bar is a later message taking the status bar's place, since there is no snack bar timer to run out.
public sealed class EditorWindowConsoleTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_console");

    public void Dispose() => _project.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) Open()
    {
        Directory.CreateDirectory(Path.Combine(_project.FullName, "scenes"));
        File.WriteAllText(Path.Combine(_project.FullName, "scenes", "main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static void Press(Window window, string name)
    {
        Headless.Click(window, name);
        Headless.Render();
    }

    private static ConsolePanel? Console(Window window) => Headless.All<ConsolePanel>(window).SingleOrDefault();

    private static IEnumerable<string> Words(Control root) =>
        Headless.All<TextBlock>(root).Select(block => block.Text ?? "");

    [Fact]
    public Task ItStartsBehindTheProjectTab() => Headless.Run(() =>
    {
        var (_, window) = Open();

        // Its tab is there to be clicked, and nothing under the tab is built.
        Headless.Find<Button>(window, "tab_console");
        Assert.Null(Console(window));
        Assert.Single(Headless.All<AssetBrowserPanel>(window));
    });

    [Fact]
    public Task ItsTabFoldsItAwayAgain() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Press(window, "tab_console");
        Assert.NotNull(Console(window));

        Press(window, "tab_console");

        Assert.Null(Console(window));
        Headless.Find<Button>(window, "tab_console");
    });

    [Fact]
    public Task OpeningItShowsWhatTheEditorHasSaid() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Save();

        Press(window, "tab_console");

        Assert.Contains(Words(Console(window)!), text => text.StartsWith("Saved", StringComparison.Ordinal));
    });

    [Fact]
    public Task AMessageThatHasGoneFromTheStatusBarIsStillThere() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Save();
        session.Report(["crate.glb is not there"]);
        Assert.DoesNotContain("Saved", session.Last?.Text ?? "", StringComparison.Ordinal);

        Press(window, "tab_console");

        Assert.Contains(Words(Console(window)!), text => text.StartsWith("Saved", StringComparison.Ordinal));
    });

    [Fact]
    public Task ARefusalIsKeptThere() => Headless.Run(() =>
    {
        var (_, window) = Open();
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(AssetDrops.Format, Path.Combine(_project.FullName, "assets", "rock.png")));
        var at = Headless.Centre(window, Headless.All<SceneViewport>(window).Single());
        foreach (var step in new[] { RawDragEventType.DragEnter, RawDragEventType.DragOver, RawDragEventType.Drop })
        {
            window.DragDrop(at, step, data, DragDropEffects.Copy, RawInputModifiers.None);
        }

        // Kept, and counted where somebody who is not looking at the console will still see it.
        Press(window, "tab_console");

        Assert.Contains(Words(Console(window)!), text => text.StartsWith("Select an object first", StringComparison.Ordinal));
    });

    [Fact]
    public Task ItCanBeCleared() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Save();
        Press(window, "tab_console");

        Press(window, "console_clear");

        Assert.Contains("No messages yet. Problems and progress appear here.", Words(Console(window)!));
    });

    [Fact]
    public Task TheFiltersHideALevel() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Save();
        Press(window, "tab_console");

        Press(window, "console_filter_info");

        var words = Words(Console(window)!).ToList();
        Assert.DoesNotContain(words, text => text.StartsWith("Saved", StringComparison.Ordinal));
        Assert.Contains("No matching messages. Turn on a message type above.", words);
    });
}
