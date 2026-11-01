using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

// A drag runs a move every frame, and the outliner shows nothing a move changes. The row control stays the
// same one.
public sealed class EditorWindowRebuildTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_rebuild");

    public void Dispose() => _temp.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) OpenWithTheCrateSelected()
    {
        var opened = ProjectWorkspace.Open(_temp.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        Headless.Click(window, "row_crate");
        Headless.Render();
        return (session, window);
    }

    // The way a drag moves it: one command, merged.
    private static void MoveTo(EditorSession session, double y)
    {
        var changes = new Dictionary<string, TransformChange> { ["crate"] = new(Vector3.Zero(), new Vector3(0, y, 0)) };
        session.History.Run(new TransformMany(session.Workspace.Loaded!.Id, TransformField.Position, "Crate", changes));
        Headless.Render();
    }

    private static Control Row(Window window) => Headless.Find<Control>(window, "row_crate");

    private static bool Shows(Control root, string text) => Headless.All<TextBlock>(root).Any(one => one.Text == text);

    private static bool Has(Window window, string name) => Headless.All<Control>(window).Any(one => one.Name == name);

    [Fact]
    public Task AMoveShowsInTheInspectorWithoutTheOutlinerRebuilding() => Headless.Run(() =>
    {
        var (session, window) = OpenWithTheCrateSelected();
        var was = Row(window);

        MoveTo(session, 3.25);

        // The number is live, which is why the inspector is not simply left alone too.
        Assert.True(Shows(window, "3.25"));
        Assert.Same(was, Row(window));
        window.Close();
    });

    [Fact]
    public Task AnythingThatIsNotAMoveRebuilds() => Headless.Run(() =>
    {
        var (session, window) = OpenWithTheCrateSelected();
        var was = Row(window);

        session.History.Run(new Rename(session.Workspace.Loaded!.Id, "crate", "Crate", "Barrel"));
        Headless.Render();

        Assert.NotSame(was, Row(window));
        Assert.True(Shows(Row(window), "Barrel"));
        window.Close();
    });

    [Fact]
    public Task UndoingAMoveIsNotItselfAMove() => Headless.Run(() =>
    {
        var (session, window) = OpenWithTheCrateSelected();
        MoveTo(session, 5);
        var was = Row(window);

        session.History.Undo();
        Headless.Render();

        Assert.NotSame(was, Row(window));
        window.Close();
    });

    [Fact]
    public Task APanelOpenedDuringADragIsBuilt() => Headless.Run(() =>
    {
        var (session, window) = OpenWithTheCrateSelected();
        MoveTo(session, 1);
        Assert.False(Has(window, "uv_fields"));

        session.OpenPanel(PanelKind.Uvs);
        Headless.Render();

        Assert.True(Has(window, "uv_fields"));
        window.Close();
    });

    [Fact]
    public Task TheInspectorStillFollowsAChangeOfSelection() => Headless.Run(() =>
    {
        var (session, window) = OpenWithTheCrateSelected();
        Assert.True(Has(window, "drag_position_0"));
        MoveTo(session, 2);

        session.ClearSelection();
        Headless.Render();

        Assert.False(Has(window, "drag_position_0"));
        window.Close();
    });
}
