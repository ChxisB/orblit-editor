namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Scene;
using Orblit.VectorMath;
using Doc = Orblit.Scene;
using SceneDocument = Orblit.Editor.Core.SceneDocument;

// Choosing a layout from the session: what it says, what it refuses and what it writes.
public sealed class EditorSessionLayoutsTests : IDisposable
{
    private static readonly Vector3 Raised = new(0, 1, 0);

    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_layouts_session");
    private readonly List<(string Text, SayLevel Level)> _said = [];

    public void Dispose() => _project.Delete(recursive: true);

    private string Scene => TestPaths.In(_project, "scenes/main.oscene");

    private EditorSession Open()
    {
        Directory.CreateDirectory(TestPaths.In(_project, "scenes"));
        File.WriteAllText(Scene, SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        session.Said += (text, level) => _said.Add((text, level));
        return session;
    }

    private static EditorScene SceneOf(EditorSession session) => session.Workspace.Loaded!.Scene!;

    private static void MoveCrate(EditorSession session, Vector3 to)
    {
        var crate = SceneOf(session)["crate"]!;
        session.History.Run(new SetTransform(session.Workspace.Loaded!.Id, "crate", TransformField.Position, crate.Name, crate.Position, to));
    }

    [Fact]
    public void TheBaseIsShownUntilALayoutIsChosen()
    {
        var session = Open();

        Assert.Null(session.ActiveLayout);
    }

    [Fact]
    public void ChoosingALayoutShowsIt()
    {
        var session = Open();

        session.ShowLayout(LayoutShape.Landscape);

        Assert.Equal(LayoutShape.Landscape, session.ActiveLayout);
    }

    [Fact]
    public void ChoosingTheBaseAgainLeavesTheLayout()
    {
        var session = Open();
        session.ShowLayout(LayoutShape.Portrait);

        session.ShowLayout(null);

        Assert.Null(session.ActiveLayout);
    }

    [Fact]
    public void UndoLeavesTheLayoutAndNamesTheStep()
    {
        var session = Open();
        session.ShowLayout(LayoutShape.Portrait);

        Assert.Equal("Switch to Portrait", session.History.UndoLabel);
        session.Undo();

        Assert.Null(session.ActiveLayout);
    }

    [Fact]
    public void ChoosingTheLayoutThatIsAlreadyShownAddsNoStep()
    {
        var session = Open();
        session.ShowLayout(LayoutShape.Portrait);

        session.ShowLayout(LayoutShape.Portrait);
        session.Undo();

        Assert.Null(session.ActiveLayout);
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void AddingAnObjectInALayoutIsRefusedAndSaysWhy()
    {
        var session = Open();
        session.SelectObject("crate");
        session.ShowLayout(LayoutShape.Portrait);
        var before = SceneOf(session).Objects.Count;

        session.Add(ObjectKind.Group);

        Assert.Equal(before, SceneOf(session).Objects.Count);
        Assert.Equal(["crate"], session.Selected);
        Assert.Equal(("Add Group cannot be made in a layout. Switch to Base to make it.", SayLevel.Info), _said[^1]);
    }

    [Fact]
    public void DeletingInALayoutKeepsTheObjectAndTheSelection()
    {
        var session = Open();
        session.SelectObject("crate");
        session.ShowLayout(LayoutShape.Portrait);

        session.DeleteSelection();

        Assert.NotNull(SceneOf(session)["crate"]);
        Assert.Equal(["crate"], session.Selected);
        Assert.Contains("cannot be made in a layout", _said[^1].Text);
    }

    [Fact]
    public void AddingTerrainInALayoutWritesNoFile()
    {
        var session = Open();
        session.ShowLayout(LayoutShape.Portrait);

        session.NewTerrain();

        Assert.False(Directory.Exists(TestPaths.In(_project, "terrain")));
        Assert.Contains("cannot be made in a layout", _said[^1].Text);
    }

    [Fact]
    public void AMoveInALayoutIsSavedWithTheBaseAndTheLayout()
    {
        var session = Open();
        var home = SceneOf(session)["crate"]!.Position.Y;
        session.ShowLayout(LayoutShape.Portrait);
        MoveCrate(session, Raised);

        session.Save();

        var written = Doc.SceneDocument.Decode(File.ReadAllText(Scene)).Document;
        var stored = (TransformComponent)written["crate"]![SceneComponents.Transform]!;
        Assert.Equal(home, stored.Position.Y);
        Assert.False(written.Layouts.IsEmpty);
    }

    [Fact]
    public void SavingFromALayoutLeavesTheLayoutShown()
    {
        var session = Open();
        session.ShowLayout(LayoutShape.Portrait);
        MoveCrate(session, Raised);

        session.Save();

        Assert.Equal(LayoutShape.Portrait, session.ActiveLayout);
        Assert.Equal(1, SceneOf(session)["crate"]!.Position.Y);
    }

    [Fact]
    public void LeavingALayoutWithAnEditItCannotKeepSaysSo()
    {
        var session = Open();
        session.ShowLayout(LayoutShape.Portrait);
        SceneOf(session)["crate"]!.Name = "Renamed";

        session.ShowLayout(null);

        Assert.Equal(
            ("1 edit made in Portrait could not be kept. A layout holds how things look.", SayLevel.Warning),
            _said[^1]);
    }

    [Fact]
    public void LeavingALayoutWithNothingLostSaysNothing()
    {
        var session = Open();
        session.ShowLayout(LayoutShape.Portrait);
        MoveCrate(session, Raised);

        session.ShowLayout(null);

        Assert.Empty(_said);
    }
}
