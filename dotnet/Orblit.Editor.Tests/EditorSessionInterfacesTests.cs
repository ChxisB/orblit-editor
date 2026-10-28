namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Ui;

// The Interface workspace in the editor: opened from a file, opened on what the scene shows, made new,
// saved with everything else. The first five follow the Interface workspace group of
// editor_shell_workspaces_test, asked of the session rather than of the panels. The rest are added in
// C#, since the Dart shell has no test for putting onto a chosen canvas or for focusing the canvas.
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
}
