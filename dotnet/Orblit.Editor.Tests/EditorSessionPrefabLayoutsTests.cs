namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// The prefab steps while a layout shows. Each adds, removes or renames objects, which a layout cannot
// hold, so each is refused before a file is written or a selection is moved.
public sealed class EditorSessionPrefabLayoutsTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_prefab_layouts");
    private readonly List<string> _said = [];

    public void Dispose() => _project.Delete(recursive: true);

    private string PrefabDirectory => TestPaths.In(_project, "prefabs");

    private List<string> PrefabsOnDisk() =>
        Directory.Exists(PrefabDirectory) ? [.. Directory.EnumerateFiles(PrefabDirectory, "*" + PrefabFiles.Extension)] : [];

    private static EditorScene LampScene() =>
        new(
        [
            new SceneObject("lamp", "Lamp post", ObjectKind.Group, position: new Vector3(40, 0, 12)),
            new SceneObject("post", "Post", ObjectKind.Mesh, parentId: "lamp", scale: new Vector3(0.2, 4, 0.2)),
            new SceneObject("bulb", "Bulb", ObjectKind.Light, parentId: "lamp", position: new Vector3(0, 4, 0)),
        ]);

    private EditorSession Open()
    {
        Directory.CreateDirectory(TestPaths.In(_project, "scenes"));
        File.WriteAllText(TestPaths.In(_project, "scenes/main.oscene"), SceneDocument.Encode(LampScene()));
        var prefabs = new PrefabFiles(_project.FullName);
        var opened = ProjectWorkspace.Open(_project.FullName, prefabs.Find);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn())
        {
            Prefabs = prefabs,
        };
        session.Said += (text, _) => _said.Add(text);
        return session;
    }

    private static EditorScene SceneOf(EditorSession session) => session.Workspace.Loaded!.Scene!;

    // The lamp made into a prefab in the base, with a part of the new instance chosen.
    private EditorSession WithAnInstance()
    {
        var session = Open();
        session.MakePrefab("lamp", PrefabDirectory);
        var part = SceneOf(session).Objects.Single(one => one.Name == "Post");
        session.SelectObject(part.Id);
        _said.Clear();
        return session;
    }

    [Fact]
    public void MakingAPrefabInALayoutWritesNoFile()
    {
        var session = Open();
        session.ShowLayout(Doc.LayoutShape.Portrait);

        session.MakePrefab("lamp", PrefabDirectory);

        Assert.Empty(PrefabsOnDisk());
        Assert.Equal("Make prefab cannot be made in a layout. Switch to Base to make it.", Assert.Single(_said));
    }

    [Fact]
    public void PlacingAPrefabInALayoutAddsNothingAndMovesNoSelection()
    {
        var session = WithAnInstance();
        var prefab = PrefabsOnDisk().Single();
        session.ShowLayout(Doc.LayoutShape.Portrait);
        var objects = SceneOf(session).Objects.Select(one => one.Id).ToList();
        var chosen = session.Selected.ToList();

        session.PlacePrefab(prefab);

        Assert.Equal(objects, SceneOf(session).Objects.Select(one => one.Id));
        Assert.Equal(chosen, session.Selected);
        Assert.Equal("Place prefab cannot be made in a layout. Switch to Base to make it.", Assert.Single(_said));
    }

    [Fact]
    public void ApplyingAPrefabInALayoutWritesNoFile()
    {
        var session = WithAnInstance();
        File.Delete(PrefabsOnDisk().Single());
        session.ShowLayout(Doc.LayoutShape.Portrait);

        session.ApplyPrefab(session.Primary!);

        Assert.Empty(PrefabsOnDisk());
        Assert.Equal("Apply prefab cannot be made in a layout. Switch to Base to make it.", Assert.Single(_said));
    }

    [Fact]
    public void RevertingAPrefabInALayoutSaysWhyAndChangesNothing()
    {
        var session = WithAnInstance();
        session.ShowLayout(Doc.LayoutShape.Landscape);
        var chosen = session.Selected.ToList();

        session.RevertPrefab(session.Primary!);

        Assert.Equal(chosen, session.Selected);
        Assert.Equal("Revert prefab cannot be made in a layout. Switch to Base to make it.", Assert.Single(_said));
    }

    [Fact]
    public void UnpackingAPrefabInALayoutMovesNoSelectionAndLeavesTheInstance()
    {
        var session = WithAnInstance();
        session.ShowLayout(Doc.LayoutShape.Landscape);
        var chosen = session.Selected.ToList();
        var ids = SceneOf(session).Objects.Select(one => one.Id).ToList();

        session.UnpackPrefab(session.Primary!);

        Assert.Equal(chosen, session.Selected);
        Assert.Equal(ids, SceneOf(session).Objects.Select(one => one.Id));
        Assert.Equal("Unpack prefab cannot be made in a layout. Switch to Base to make it.", Assert.Single(_said));
    }
}
