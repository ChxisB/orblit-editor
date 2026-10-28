using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;

// Dart: orblit-editor/test/editor_shell_blends_test.dart. A blend opened from the Project panel in the
// window, chosen from, changed through the undo stack and written back as the file a game reads.
public sealed class EditorWindowBlendsTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_blends");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    // Goes from idle to move once speed is above a tenth, as the Dart tests' moves.oblend does.
    private string WriteMoves()
    {
        Directory.CreateDirectory(PathOf("blends"));
        File.WriteAllText(
            PathOf("blends/moves.oblend"),
            new BlendDocument(
                "Moves",
                [new BlendState("idle", new BlendClip("idle")), new BlendState("move", new BlendClip("walk"))],
                new Dictionary<string, double> { ["speed"] = 0 },
                [new BlendChange("move", from: "idle", when: BlendCondition.Above("speed", 0.1))]).Encode());
        return PathOf("blends/moves.oblend");
    }

    private BlendDocument Saved() => BlendDocument.Decode(File.ReadAllText(PathOf("blends/moves.oblend"))).Blend;

    private (EditorSession Session, EditorWindow Window) Open()
    {
        var opened = ProjectWorkspace.Open(_project.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    // The blend is opened through the session. The Project panel's route to it has its own test.
    private (EditorSession Session, EditorWindow Window) OpenMoves()
    {
        var path = WriteMoves();
        var (session, window) = Open();
        session.OpenBlend(path);
        Headless.Render();
        return (session, window);
    }

    private static bool Has(Control root, string name) => Headless.All<Control>(root).Any(one => one.Name == name);

    private static void Press(Control root, string name) =>
        Headless.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    // Two double clicks, into the folder and onto the file, as a person opens a blend from Project.
    private static void OpenFromProject(Window window, string file)
    {
        Headless.DoubleClick(window, Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == "blends"));
        Headless.DoubleClick(window, Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == file));
    }

    [Fact]
    public Task ABlendOpenedFromTheProjectPanelDrawsItsGraph() => Headless.Run(() =>
    {
        WriteMoves();
        var (session, window) = Open();

        OpenFromProject(window, "moves.oblend");

        Assert.True(Headless.Until(() => session.Blends.Shown is not null));
        Assert.Equal("animation", session.Mode.Name);
        Assert.Single(Headless.All<BlendGraphPanel>(window));
        Assert.True(Has(window, "state_idle"));
        Assert.True(Has(window, "state_move"));
        Assert.True(Has(window, "change_0"));
        window.Close();
    });

    [Fact]
    public Task AFileThatIsNotABlendDoesNotOpenTheGraph() => Headless.Run(() =>
    {
        Directory.CreateDirectory(PathOf("blends"));
        File.WriteAllText(PathOf("blends/bad.oblend"), "{\"kind\":\"orblit.clip\"}");
        var (session, window) = Open();

        OpenFromProject(window, "bad.oblend");
        Headless.Wait(100);

        Assert.Null(session.Blends.Shown);
        Assert.Empty(Headless.All<BlendGraphPanel>(window));
        Assert.False(session.Layout.Holds("blendGraph"));
        window.Close();
    });

    [Fact]
    public Task AChoiceOfStartStateIsAStepAndIsSaved() => Headless.Run(() =>
    {
        var (session, window) = OpenMoves();
        Headless.Click(window, "state_move");
        Press(window, "button_start_here");

        session.Save();
        Assert.Equal("move", Saved().WrittenStart);

        session.Undo();
        session.Save();
        Assert.Equal("idle", Saved().WrittenStart);
        window.Close();
    });

    [Fact]
    public Task DraggingFromAStateOntoAnotherMakesAChange() => Headless.Run(() =>
    {
        var (session, window) = OpenMoves();
        var from = Headless.Centre(window, "link_move");
        var onto = Headless.Centre(window, "state_idle");

        window.MouseDown(from, MouseButton.Left);
        foreach (var step in new[] { 0.25, 0.5, 0.75, 1 })
        {
            window.MouseMove(new Point(from.X + ((onto.X - from.X) * step), from.Y + ((onto.Y - from.Y) * step)));
        }

        window.MouseUp(onto, MouseButton.Left);
        Headless.Render();

        session.Save();
        var changes = Saved().WrittenChanges;
        Assert.Equal(2, changes.Count);
        Assert.Equal(("move", "idle"), (changes[^1].From, changes[^1].To));
        window.Close();
    });

    [Fact]
    public Task AChangeIsChosenFromItsArrowAndDeletedFromTheFields() => Headless.Run(() =>
    {
        var (session, window) = OpenMoves();

        Headless.Click(window, "change_0");
        Press(window, "button_delete");
        session.Save();
        Assert.Empty(Saved().WrittenChanges);

        session.Undo();
        session.Save();
        Assert.Single(Saved().WrittenChanges);
        window.Close();
    });

    [Fact]
    public Task ThePanelOpenedEmptyMakesABlend() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.OpenPanel(PanelKind.BlendGraph);
        Headless.Render();
        Assert.True(Has(window, "blend_none"));

        Press(window, "button_new_blend");
        Headless.Render();

        Assert.True(File.Exists(PathOf("blends/blend.oblend")));
        Assert.True(Has(window, "state_idle"));
        window.Close();
    });

    [Fact]
    public Task ABlendWithChangesMarksItsTabUntilItIsSaved() => Headless.Run(() =>
    {
        var (session, window) = OpenMoves();
        Assert.Equal("moves", Headless.Find<TextBlock>(window, "blend_tab_text_moves").Text);

        Headless.Click(window, "state_move");
        Press(window, "button_start_here");
        Assert.Equal("moves •", Headless.Find<TextBlock>(window, "blend_tab_text_moves").Text);

        session.Save();
        Headless.Render();
        Assert.Equal("moves", Headless.Find<TextBlock>(window, "blend_tab_text_moves").Text);
        window.Close();
    });
}
