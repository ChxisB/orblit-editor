namespace Orblit.Editor.Tests;

using Doc = Orblit.Scene;
using Orblit.Editor.Core;
using Orblit.Mesh;

public sealed class PaletteEntriesTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");

    public void Dispose() => _temp.Delete(recursive: true);

    private EditorSession Session()
    {
        var opened = ProjectWorkspace.Open(_temp.FullName);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
    }

    private static PaletteEntry Entry(EditorSession session, string label) =>
        Assert.Single(PaletteEntries.For(session, HostPlatform.MacOS), one => one.Label == label);

    [Fact]
    public void EveryModeCanBeReachedByName()
    {
        var session = Session();

        foreach (var mode in session.Modes.All)
        {
            var entry = Entry(session, $"Go to {mode.Label}");
            Assert.Equal("Workspace", entry.Meta);
            entry.OnRun();
            Assert.Equal(mode.Name, session.Mode.Name);
        }
    }

    [Fact]
    public void UndoAndRedoSayWhatTheyWouldDoAndDoIt()
    {
        var session = Session();
        session.History.Run(new AddObject(new SceneObject(id: "extra", name: "Extra", kind: ObjectKind.Group), "scene0"));

        Assert.Equal("Add Extra", Entry(session, "Undo").Meta);
        Assert.Equal("", Entry(session, "Redo").Meta);

        Entry(session, "Undo").OnRun();

        Assert.False(session.History.CanUndo);
        Assert.Equal("Add Extra", Entry(session, "Redo").Meta);
    }

    [Fact]
    public void TheKeysShownAreTheOnesTheHostUses()
    {
        var session = Session();

        Assert.Equal("⌘S", PaletteEntries.For(session, HostPlatform.MacOS).First(one => one.Label == "Save").Keys);
        Assert.Equal("Ctrl+S", PaletteEntries.For(session, HostPlatform.Windows).First(one => one.Label == "Save").Keys);
        Assert.Equal("⇧⌘S", PaletteEntries.For(session, HostPlatform.MacOS).First(one => one.Label == "Save as").Keys);
    }

    [Fact]
    public void SaveAsAsksForAName()
    {
        var session = Session();
        var asked = 0;
        session.NameWanted += () => asked++;

        Entry(session, "Save as").OnRun();

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task NewSceneStartsAnUntitledSceneAndReadsTheKeyForTheHost()
    {
        var session = Session();
        // The starter scene has never been written, so leaving it is asked about.
        session.ConfirmLeaving = _ => Task.FromResult(Leaving.Discard);
        var entry = Entry(session, "New scene");

        entry.OnRun();
        await Task.Yield();

        Assert.Equal("Untitled", session.Workspace.Loaded!.Title);
        Assert.Equal("⌘N", entry.Keys);
    }

    [Fact]
    public void ChoosingAnObjectSelectsIt()
    {
        var session = Session();
        session.History.Run(new AddObject(new SceneObject(id: "extra", name: "Extra", kind: ObjectKind.Group), "scene0"));

        var entry = Entry(session, "Extra");
        entry.OnRun();

        Assert.Equal(PaletteGroup.Objects, entry.Group);
        Assert.Equal(["extra"], session.Selected);
    }

    [Fact]
    public void AnObjectSaysWhereItSitsByTheNameOfItsParent()
    {
        var session = Session();
        session.History.Run(new AddObject(new SceneObject(id: "parent", name: "Parent", kind: ObjectKind.Group), "scene0"));
        session.History.Run(new AddObject(new SceneObject(id: "child", name: "Child", kind: ObjectKind.Group), "scene0", "parent"));

        Assert.Equal("Parent", Entry(session, "Child").Meta);
        Assert.Equal("", Entry(session, "Parent").Meta);
    }

    [Fact]
    public void TheFocusEntryNamesWhatItWouldDo()
    {
        var session = Session();

        Entry(session, "Focus view").OnRun();

        Assert.True(session.IsFocused);
        Assert.Equal("Show panels", Entry(session, "Show panels").Label);
    }

    [Fact]
    public void EveryShapeAndKindCanBeAddedByName()
    {
        var session = Session();

        var adds = PaletteEntries.For(session, HostPlatform.MacOS).Where(one => one.Label.StartsWith("Add ")).ToList();

        // The extra one is Add Terrain, which makes a file as well as an object.
        Assert.Equal(Enum.GetValues<ShapeKind>().Length + AddMenu.Items.Count + 1, adds.Count);
        Assert.All(adds.Where(one => one.Meta == "Shape"), one => Assert.Contains(one.Label[4..], Enum.GetNames<ShapeKind>()));
    }

    [Fact]
    public void AnAddCommandPutsThatThingInTheScene()
    {
        var session = Session();

        Entry(session, "Add Sphere").OnRun();
        Entry(session, "Add Light").OnRun();

        var names = session.Workspace.Loaded!.Scene!.Objects.Select(one => one.Name).ToList();
        Assert.Contains("Sphere", names);
        Assert.Contains("Light", names);
    }

    [Fact]
    public void DuplicateAndDeleteWorkOnTheSelection()
    {
        var session = Session();
        Entry(session, "Add Group").OnRun();
        var added = session.Primary!;

        Entry(session, "Duplicate").OnRun();
        Assert.Equal(2, session.Workspace.Loaded!.Scene!.Objects.Count(one => one.Name == "Group"));

        Entry(session, "Delete").OnRun();
        Assert.Equal(1, session.Workspace.Loaded!.Scene!.Objects.Count(one => one.Name == "Group"));
        Assert.True(session.Workspace.Loaded.Scene!.Contains(added));
    }

    [Fact]
    public void CopyAndPasteGoThroughTheClipboard()
    {
        var session = Session();
        Entry(session, "Add Group").OnRun();

        Entry(session, "Copy").OnRun();
        Entry(session, "Paste").OnRun();

        Assert.Equal(2, session.Workspace.Loaded!.Scene!.Objects.Count(one => one.Name == "Group"));
    }
}
