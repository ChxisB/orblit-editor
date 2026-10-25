namespace Orblit.Editor.Tests;

using Doc = Orblit.Scene;
using Orblit.Editor.Core;

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
}
