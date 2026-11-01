namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Terrain;
using Doc = Orblit.Scene;

// Terrain in the editor: made from the Add menu, found for the brush, saved with everything else. Cases also
// cover finding the target and the mode's arrangement.
public sealed class EditorSessionTerrainTests : IDisposable
{
    private const string Made = "terrain/terrain/terrain.oterrain";

    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_terrain_session");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => TestPaths.In(_project, name);

    private EditorSession Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
    }

    private static SceneObject? Terrain(EditorSession session) =>
        session.Workspace.Loaded!.Scene!.Objects.FirstOrDefault(one => PhysicsComponents.Of<Doc.TerrainComponent>(one, Doc.SceneComponents.Terrain) is not null);

    private OpenTerrain OnDisk() => new TerrainBench(new History(new Workspace(_project.FullName)), _project.FullName).TerrainFor(Made)!;

    [Fact]
    public void AddingTerrainWritesTheGroundAtOnceAndPutsAnObjectInTheSceneThatNamesIt()
    {
        var session = Open();

        session.NewTerrain();

        var written = OnDisk();
        Assert.Equal(4, written.Terrain.Regions.Count());
        Assert.Equal(["Rock", "Grass"], written.Terrain.Sets.Select(set => set.Name));
        Assert.True(written.Terrain.Sets[0].Triplanar);
        var added = Terrain(session)!;
        Assert.Equal("Terrain", added.Name);
        Assert.Equal(Made, PhysicsComponents.Of<Doc.TerrainComponent>(added, Doc.SceneComponents.Terrain)!.File);
        Assert.Equal("Add Terrain", session.History.UndoLabel);
        Assert.Equal([added.Id], session.Selected);
        Assert.Equal($"Made {Made}", session.Last!.Value.Text);
    }

    [Fact]
    public void ASecondTerrainGetsAFolderAndANameOfItsOwn()
    {
        var session = Open();

        session.NewTerrain();
        session.NewTerrain();

        Assert.True(File.Exists(PathOf("terrain/terrain_2/terrain_2.oterrain")));
        Assert.Equal(["Terrain", "Terrain 2"], session.Workspace.Loaded!.Scene!.Objects.Where(one => one.Name.StartsWith("Terrain")).Select(one => one.Name));
    }

    [Fact]
    public void UndoingTheAddTakesTheObjectAwayAndLeavesTheFile()
    {
        var session = Open();
        session.NewTerrain();

        session.Undo();

        Assert.Null(Terrain(session));
        Assert.True(File.Exists(PathOf(Made)));
    }

    [Fact]
    public void WithNoSceneLoadedThereIsNowhereToAddItAndNothingIsWritten()
    {
        var workspace = new Workspace(_project.FullName);
        var session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn());

        session.NewTerrain();

        Assert.Equal(("There is no scene loaded to add to.", SayLevel.Warning), session.Last!.Value);
        Assert.False(Directory.Exists(PathOf("terrain")));
    }

    [Fact]
    public void SavingWritesTheGroundAndTheSceneThatNamesIt()
    {
        var session = Open();
        session.NewTerrain();
        session.Save();
        session.Terrains.Edit(session.Terrains[Made]!, "Blend", settings => settings with { BlendSharpness = 0.25 });
        session.History.Seal();
        Assert.True(session.AnyUnsaved);

        session.Save();

        Assert.False(session.AnyUnsaved);
        Assert.Equal(0.25, OnDisk().Terrain.BlendSharpness);
        Assert.Contains(Made, File.ReadAllText(PathOf("scenes/main.oscene")));
    }

    [Fact]
    public void GroundWithChangesMarksTheEditorUnsavedAndSaysSoOnceEachWay()
    {
        var session = Open();
        session.NewTerrain();
        session.Save();
        var changes = 0;
        session.Changed += () => changes++;

        session.Terrains.Edit(session.Terrains[Made]!, "Blend", settings => settings with { BlendSharpness = 0.25 });
        session.Terrains.Edit(session.Terrains[Made]!, "Blend", settings => settings with { BlendSharpness = 0.3 });
        Assert.Equal(1, changes);

        session.Save();
        Assert.Equal(2, changes);
    }

    [Fact]
    public void TheBrushLandsOnTheSelectedTerrainElseTheFirstOneInTheScene()
    {
        var session = Open();
        Assert.Null(session.TerrainTarget());
        session.NewTerrain();
        session.NewTerrain();
        session.Select(["cube"]);

        Assert.Equal(Made, session.TerrainTarget()!.File);

        session.Select([session.Workspace.Loaded!.Scene!.Objects.First(one => one.Name == "Terrain 2").Id]);
        Assert.Equal("terrain/terrain_2/terrain_2.oterrain", session.TerrainTarget()!.File);
    }

    [Fact]
    public void TheTerrainModeComesAfterModellingAndArrangesTheBrushBesideTheInspector()
    {
        var modes = EditorModes.Standard().All.Select(mode => mode.Name).ToList();
        var layout = EditorModes.Standard()["terrain"]!.Layout();

        Assert.Equal(["scene", "modelling", "terrain", "animation", "cinematics"], modes);
        Assert.Equal(["outliner", "scene", "game", "console", "brush", "inspector"], layout.Panels.Select(panel => panel.Id));
        Assert.Equal(PanelKind.TerrainBrush, layout.Panels.First(panel => panel.Id == "brush").Kind);
        Assert.Equal(("Brush", "brush_outlined"), (PanelKind.TerrainBrush.Label, PanelKind.TerrainBrush.Icon));
    }

    [Fact]
    public void OnlyTheTerrainModeWorksTheBrush()
    {
        var working = EditorModes.Standard().All.Where(mode => mode.UsesBrush).Select(mode => mode.Name);

        Assert.Equal(["terrain"], working);
    }
}
