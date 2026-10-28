namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Ui;

// The Interface workspace in the editor: opened from a file, opened on what the scene shows, made new,
// saved with everything else. The first five follow the Interface workspace group of
// editor_shell_workspaces_test, asked of the session rather than of the panels. Putting onto a chosen
// canvas, saving with the scene and the scene's interface follow the "an interface on a scene" group of
// editor_shell_data_test, with PutInterfaceOnScene for the drop. The rest are added in C#.
public sealed class EditorSessionInterfacesTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_interfaces_session");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private EditorSession Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
    }

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

    private static IReadOnlyList<SceneObject> Canvases(EditorSession session) =>
        [.. session.Workspace.Loaded!.Scene!.Objects.Where(one => one.Kind == ObjectKind.Canvas)];

    private static UiNode Root(EditorSession session)
    {
        Assert.NotNull(session.Interfaces.Document);
        return session.Interfaces.Document.Root;
    }

    [Fact]
    public void OpeningAnInterfaceGoesThere()
    {
        var session = Open();

        session.OpenInterface(WriteHud());

        Assert.Equal("interface", session.Mode.Name);
        Assert.Equal("Health 100", Root(session).Children[0].Text);
        Assert.True(session.Layout.Holds("canvas") && session.Layout.Holds("elements") && session.Layout.Holds("design"));
    }

    [Fact]
    public void TheEditorKeysUndoAndSaveWhatIsLaidOut()
    {
        var session = Open();
        var path = WriteHud();
        session.OpenInterface(path);

        session.Interfaces.Add(UiElement.Box);
        Assert.True(session.AnyUnsaved);

        session.Undo();
        Assert.Single(Root(session).Children);
        Assert.False(session.AnyUnsaved);

        session.Interfaces.Add(UiElement.Box);
        session.Save();

        Assert.False(session.AnyUnsaved);
        Assert.Equal("box", UiDocument.Read(File.ReadAllText(path))!.Root.Children[^1].Type);
    }

    [Fact]
    public void ItOpensOnTheInterfaceTheSceneShows()
    {
        var session = Open();
        session.PutInterfaceOnScene(WriteHud());

        session.EnterMode("interface");

        Assert.Equal("hud.oui", session.Interfaces.Shown!.Name);
        var canvas = Assert.Single(Canvases(session));
        Assert.Equal(("hud", "hud.oui"), (canvas.Name, canvas.InterfaceAsset));
        Assert.Equal([canvas.Id], session.Selected);
    }

    [Fact]
    public void DeleteTakesOutTheElementNotTheObject()
    {
        var session = Open();
        session.PutInterfaceOnScene(WriteHud());
        session.EnterMode("interface");
        session.Interfaces.Selected = [0];

        session.Backspace();

        Assert.Empty(Root(session).Children);
        Assert.Single(Canvases(session));
    }

    [Fact]
    public void WithNothingOpenANewOneIsMadeAndTheSceneShowsIt()
    {
        var session = Open();
        session.EnterMode("interface");
        Assert.Null(session.Interfaces.Shown);

        session.NewInterface();

        Assert.True(File.Exists(PathOf("interfaces/screen.oui")));
        Assert.Equal("screen.oui", session.Interfaces.Shown!.Name);
        var canvas = Assert.Single(Canvases(session));
        Assert.Equal(("screen", "interfaces/screen.oui"), (canvas.Name, canvas.InterfaceAsset));
    }

    [Fact]
    public void ANewOneLeavesAnInterfaceTheSceneAlreadyShows()
    {
        var session = Open();
        session.PutInterfaceOnScene(WriteHud());

        session.NewInterface();

        Assert.Equal("hud.oui", Assert.Single(Canvases(session)).InterfaceAsset);
        Assert.Equal("screen.oui", session.Interfaces.Shown!.Name);
    }

    [Fact]
    public void PuttingOnAChosenCanvasChangesWhatItShows()
    {
        var session = Open();
        var hud = WriteHud();
        session.PutInterfaceOnScene(hud);
        File.Copy(hud, PathOf("pause.oui"));

        session.PutInterfaceOnScene(PathOf("pause.oui"));

        Assert.Equal(("hud", "pause.oui"), (Assert.Single(Canvases(session)).Name, Canvases(session)[0].InterfaceAsset));
        Assert.Equal("hud now shows pause.oui.", session.Last!.Value.Text);
    }

    [Fact]
    public void ItIsSavedWithTheSceneAndComesBack()
    {
        var session = Open();
        session.PutInterfaceOnScene(WriteHud());

        session.Save();

        var load = SceneDocument.Decode(File.ReadAllText(PathOf("scenes/main.oscene")));
        Assert.False(load.HasProblems, string.Join("; ", load.Problems));
        Assert.Equal("hud.oui", Assert.Single(load.Scene.Objects, one => one.Kind == ObjectKind.Canvas).InterfaceAsset);
    }

    [Fact]
    public void AFileThatIsNotAnInterfaceIsSaidAndTheModeStays()
    {
        var session = Open();
        File.WriteAllText(PathOf("bad.oui"), "not an interface");

        session.OpenInterface(PathOf("bad.oui"));

        Assert.Equal("scene", session.Mode.Name);
        Assert.Equal(("bad.oui is not a readable interface.", SayLevel.Error), session.Last);
    }

    [Fact]
    public void FocusingTheWorkspaceShowsTheCanvasAlone()
    {
        var session = Open();
        session.OpenInterface(WriteHud());

        session.ToggleFocus();

        Assert.Equal(PanelKind.InterfaceCanvas, Assert.Single(session.VisibleLayout.Panels).Kind);
    }

    [Fact]
    public void TheSceneShowsTheInterfaceItsCanvasNames()
    {
        var session = Open();
        Assert.Null(session.SceneInterface());

        session.PutInterfaceOnScene(WriteHud());

        Assert.Equal("Health 100", session.SceneInterface()!.Root.Children[0].Text);
    }

    [Fact]
    public void AChangeShowsOverTheSceneBeforeItIsSaved()
    {
        var session = Open();
        var hud = WriteHud();
        session.PutInterfaceOnScene(hud);
        Assert.NotNull(session.SceneInterface());

        session.OpenInterface(hud);
        session.Interfaces.Add(UiElement.All.First(one => one.Type == "box"));

        Assert.Equal("box", session.SceneInterface()!.Root.Children[^1].Type);
    }

    [Fact]
    public void HidingTheCanvasTakesTheInterfaceOffTheScene()
    {
        var session = Open();
        session.PutInterfaceOnScene(WriteHud());
        var canvas = Canvases(session)[0];

        session.History.Run(new SetVisible(session.Workspace.Loaded!.Id, canvas.Id, canvas.Name, false));

        Assert.Null(session.SceneInterface());
    }

    [Fact]
    public void AFileThatCannotBeReadShowsNothing()
    {
        var session = Open();
        session.PutInterfaceOnScene(PathOf("gone.oui"));

        Assert.Null(session.SceneInterface());
        Assert.NotEqual(SayLevel.Error, session.Last?.Level);
    }
}
