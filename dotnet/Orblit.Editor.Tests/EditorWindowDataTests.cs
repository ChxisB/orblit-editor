using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The data objects group. One is made through the browser's MakeNew rather than its menu, and attached
// through the session rather than a drop, since the drag is not tested here. Saving after an attach is asked
// of the session in EditorSessionDataTests. An object is selected through the session rather than its
// outliner row.
public sealed class EditorWindowDataTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_data");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private DataObject Read(string name) => DataObject.Read(File.ReadAllText(PathOf(name)))!;

    private (EditorSession Session, EditorWindow Window) Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static void MakeOne(Window window, string name)
    {
        Headless.All<AssetBrowserPanel>(window).Single().MakeNew(NewAsset.DataObject, name);
        Headless.Render();
    }

    private static void Select(Window window, string name)
    {
        Headless.Click(window, Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == name));
        Headless.Render();
    }

    private static void SelectCrate(EditorSession session)
    {
        session.Select([session.Workspace.Loaded!.Scene!.Objects.Single(one => one.Name == "Crate").Id]);
        Headless.Render();
    }

    private static DataPanel? Panel(Window window) => Headless.All<DataPanel>(window).SingleOrDefault();

    private static List<string> InspectorWords(Window window) =>
        Headless.All<TextBlock>(Headless.All<InspectorPanel>(window).Single()).Select(block => block.Text ?? "").ToList();

    // A section's title carries its fold mark in front.
    private static bool HasDataSection(Window window) => InspectorWords(window).Any(word => word.EndsWith(" Data", StringComparison.Ordinal));

    [Fact]
    public Task OneIsMadeWithSomethingAlreadyInIt() => Headless.Run(() =>
    {
        var (_, window) = Open();

        MakeOne(window, "ball");

        Assert.NotEmpty(Read("ball.odata").Fields);
    });

    [Fact]
    public Task SelectingOneEditsItInTheInspector() => Headless.Run(() =>
    {
        var (_, window) = Open();
        MakeOne(window, "ball");

        Select(window, "ball.odata");

        Assert.NotNull(Panel(window));
        Headless.Find<Button>(window, "button_add_a_value");
    });

    [Fact]
    public Task AValueTypedInReachesTheFile() => Headless.Run(() =>
    {
        var (_, window) = Open();
        MakeOne(window, "ball");
        Select(window, "ball.odata");

        // The number the blank object starts with.
        Headless.Find<TextBox>(window, "field_value_0").Text = "12.5";

        Assert.True(Headless.Until(() => Read("ball.odata").Fields[0].AsNumber == 12.5));
    });

    // Added in C#. A panel built again would drop a value half typed and the caret in it.
    [Fact]
    public Task TheSameFileKeepsItsPanelThroughAChangeElsewhere() => Headless.Run(() =>
    {
        var (session, window) = Open();
        MakeOne(window, "ball");
        Select(window, "ball.odata");
        var shown = Panel(window);

        session.ShowData("ball.odata");
        Headless.Render();

        Assert.Same(shown, Panel(window));
    });

    [Fact]
    public Task SelectingAnObjectPutsTheInspectorBack() => Headless.Run(() =>
    {
        var (session, window) = Open();
        MakeOne(window, "ball");
        Select(window, "ball.odata");
        Assert.NotNull(Panel(window));

        SelectCrate(session);

        Assert.Null(Panel(window));
    });

    [Fact]
    public Task SelectingAMeshDoesNotTakeTheInspector() => Headless.Run(() =>
    {
        File.WriteAllBytes(PathOf("rock.glb"), [1]);
        var (session, window) = Open();
        SelectCrate(session);

        Select(window, "rock.glb");

        // Still the object: only a data object claims the panel.
        Assert.Null(Panel(window));
        Assert.Equal("Crate", session.InspectedItem?.Name);
    });

    [Fact]
    public Task AttachingOneListsItOnTheObject() => Headless.Run(() =>
    {
        var (session, window) = Open();
        MakeOne(window, "weight");
        SelectCrate(session);

        session.AttachData(PathOf("weight.odata"));
        Headless.Render();

        // Listed on the object, and named as what it is rather than a path.
        Assert.True(HasDataSection(window));
        Assert.Contains("weight.odata", InspectorWords(window));
    });

    [Fact]
    public Task AttachingIsUndoable() => Headless.Run(() =>
    {
        var (session, window) = Open();
        MakeOne(window, "weight");
        SelectCrate(session);
        session.AttachData(PathOf("weight.odata"));
        Headless.Render();
        Assert.True(HasDataSection(window));

        session.Undo();
        Headless.Render();

        Assert.False(HasDataSection(window));
    });

    [Fact]
    public Task TheBindingsItWritesLandBesideIt() => Headless.Run(() =>
    {
        var (_, window) = Open();
        MakeOne(window, "ball");
        Select(window, "ball.odata");

        Headless.Click(window, "button_write_script_bindings");

        Assert.Contains("namespace Ball", File.ReadAllText(PathOf("ball.h")), StringComparison.Ordinal);
    });
}
