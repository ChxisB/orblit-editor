using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Doc = Orblit.Scene;
using static Orblit.Editor.Tests.InspectorRig;

/// <summary>The inspector with a scene selected and not an object in it: the scene's own settings.</summary>
public sealed class InspectorSceneTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_inspector_scene");

    public void Dispose() => _project.Delete(recursive: true);

    // A project with a saved main scene and a props scene beside it that is listed and not loaded.
    private (EditorSession Session, Window Window) Project()
    {
        Directory.CreateDirectory(Path.Combine(_project.FullName, "scenes"));
        Write("main", EditorScene.Starter());
        Write("props", new EditorScene([], name: "Props"));
        var opened = ProjectWorkspace.Open(_project.FullName);
        return Show(opened.Workspace, null);
    }

    private void Write(string name, EditorScene scene) => File.WriteAllText(
        Path.Combine(_project.FullName, "scenes", name + Doc.SceneDocument.SceneExtension),
        SceneDocument.Encode(scene));

    private static void Drag(Window window, string name, double pixels)
    {
        var at = Headless.Centre(window, name);
        window.MouseDown(at, MouseButton.Left);
        window.MouseMove(at + new Vector(pixels, 0));
        window.MouseUp(at + new Vector(pixels, 0), MouseButton.Left);
        Headless.Render();
    }

    [Fact]
    public Task WithNothingSelectedItShowsTheSceneItself() => Headless.Run(() =>
    {
        var (_, window) = Open(EditorScene.Starter());

        Assert.True(Has(window, "section_sky"));
        Assert.True(Has(window, "section_environment"));
        Assert.True(Has(window, "section_layers"));
        Assert.True(Has(window, "section_contents"));
        Assert.False(Has(window, "section_transform"));
    });

    [Fact]
    public Task SelectingTheSceneShowsItsOwnSettingsAndNoTransform() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");
        Assert.True(Has(window, "section_transform"));

        session.SelectScene("main");
        Headless.Render();

        Assert.True(Has(window, "section_environment"));
        Assert.True(Has(window, "section_sky"));
        Assert.True(Has(window, "slider_ambient"));
        Assert.False(Has(window, "section_transform"));
    });

    [Fact]
    public Task TheSceneHeaderRenamesTheSceneAsOneUndoStep() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());
        var was = session.Workspace.Loaded!.Scene!.Name;
        var box = Headless.Find<TextBox>(window, "inspector_name");

        box.Text = "L";
        box.Text = "Level";
        Headless.Find<Button>(window, "elsewhere").Focus();
        Headless.Render();

        Assert.Equal("Level", session.Workspace.Loaded!.Scene!.Name);
        session.History.Undo();
        Assert.Equal(was, session.Workspace.Loaded!.Scene!.Name);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task TheSceneSettingsAreSavedWithTheScene() => Headless.Run(() =>
    {
        var (session, window) = Project();
        var scene = session.Workspace.Loaded!.Scene!;
        var was = scene.Ambient;

        Drag(window, "slider_ambient", 30);
        session.Save();

        Assert.NotEqual(was, scene.Ambient);
        var saved = File.ReadAllText(session.Workspace.Loaded!.Path!);
        Assert.Contains("\"sky\"", saved, StringComparison.Ordinal);
        Assert.Contains("\"ambient\"", saved, StringComparison.Ordinal);
    });

    [Fact]
    public Task ASkyColourIsOneUndoStep() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());
        var scene = session.Workspace.Loaded!.Scene!;
        var was = scene.SkyColour;

        Headless.Click(window, "swatch_sky_1");

        Assert.NotEqual(was, scene.SkyColour);
        session.History.Undo();
        Assert.Equal(was, scene.SkyColour);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task TurningTheDayCycleOnShowsItsSpeedAndHandsTheSkyToTheClock() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());
        Assert.False(Has(window, "slider_speed"));
        Assert.True(Has(window, "slider_ambient"));

        Headless.Click(window, "choice_day_cycle_on");

        Assert.True(session.Workspace.Loaded!.Scene!.DayCycle);
        Assert.True(Has(window, "slider_speed"));
        Assert.False(Has(window, "slider_ambient"));
        Assert.True(Has(window, "row_now"));
        Assert.False(Has(window, "row_set_to"));
        session.History.Undo();
        Assert.False(session.Workspace.Loaded!.Scene!.DayCycle);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task ChoosingTheDayCycleItAlreadyHasIsNotAnEdit() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());

        Headless.Click(window, "choice_day_cycle_off");

        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task AFewLayerNamesAreShownUntilAllAreAskedFor() => Headless.Run(() =>
    {
        var (_, window) = Open(EditorScene.Starter());
        Assert.Equal(4, Headless.All<TextBox>(window).Count(one => one.Name?.StartsWith("entry_layer_", StringComparison.Ordinal) == true));

        Headless.Click(window, "button_show_all_32");

        Assert.Equal(32, Headless.All<TextBox>(window).Count(one => one.Name?.StartsWith("entry_layer_", StringComparison.Ordinal) == true));
        Assert.False(Has(window, "button_show_all_32"));
    });

    [Fact]
    public Task NamingALayerIsOneUndoStepAndTheNextRowAppears() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());
        var scene = session.Workspace.Loaded!.Scene!;
        var box = Headless.Find<TextBox>(window, "entry_layer_4");

        Assert.True(box.Focus());
        box.Text = "F";
        box.Text = "Fast";
        Headless.Render();
        Assert.True(session.History.CanUndo);
        Assert.True(Headless.Find<Button>(window, "elsewhere").Focus());
        Assert.False(box.IsFocused);
        Headless.Render();

        Assert.Equal(["", "", "", "Fast"], scene.LayerNames);
        Assert.True(Has(window, "entry_layer_5"));
        session.History.Undo();
        Assert.Empty(scene.LayerNames);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task TheContentsCountObjectsDrawnThingsAndLights() => Headless.Run(() =>
    {
        var scene = EditorScene.Starter();
        var (_, window) = Open(scene);

        Assert.Equal($"{scene.Length}", ValueOf(window, "row_objects"));
        Assert.Equal($"{scene.Objects.Count(one => one.IsDrawable)}", ValueOf(window, "row_drawn"));
        Assert.Equal($"{scene.Objects.Count(one => one.Kind == ObjectKind.Light)}", ValueOf(window, "row_lights"));
    });

    [Fact]
    public Task ASceneThatIsNotOpenOffersToLoadAndLoadsOnRequest() => Headless.Run(async () =>
    {
        var (session, window) = Project();
        var props = session.Workspace.Entries.Single(one => one.Title == "props");
        session.SelectScene(props.Id);
        Headless.Render();

        Assert.Equal("props", TextOf(window, "inspector_title"));
        Assert.False(Has(window, "inspector_name"));
        Assert.False(Has(window, "section_sky"));
        Headless.Click(window, "button_load_scene");
        for (var wait = 0; wait < 100 && !props.IsLoaded; wait++)
        {
            await Task.Delay(20);
        }

        Assert.True(props.IsLoaded);
    });

    [Fact]
    public Task TheSharedSceneIsNamedAndCountedAndHasNoSky() => Headless.Run(() =>
    {
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("main", "Main", scene: EditorScene.Starter()));
        workspace.Shared.Add(new SceneObject("sun", "Sun", ObjectKind.Light));
        var (session, window) = Show(workspace, null);

        session.SelectScene(Workspace.SharedSceneId);
        Headless.Render();

        Assert.Equal("Shared", TextOf(window, "inspector_title"));
        Assert.Equal("1", ValueOf(window, "row_lights"));
        Assert.False(Has(window, "section_sky"));
        Assert.False(Has(window, "section_environment"));
    });

    private static string ValueOf(Window window, string row) =>
        Headless.All<TextBlock>(Headless.Find<Control>(window, row)).Last().Text ?? "";
}
