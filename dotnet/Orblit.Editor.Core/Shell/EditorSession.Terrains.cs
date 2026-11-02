namespace Orblit.Editor.Core;

using Orblit.Terrain;
using Doc = Orblit.Scene;
using Ground = Orblit.Terrain.Terrain;

// Terrains: made from the Add menu, found for the brush and saved with everything else.
public sealed partial class EditorSession
{
    private bool _terrainsUnsaved;

    /// <summary>The terrains the project has opened, and the brush they are shaped with.</summary>
    public TerrainBench Terrains { get; }

    /// <summary>
    /// The terrain a brush lands on: the selected object's, or else the first one the loaded scene or
    /// the shared set puts in the world.
    /// <para>
    /// Not only the selected one, so the brush works on the ground straight after switching to the
    /// terrain mode, without finding it in the outliner first.
    /// </para>
    /// </summary>
    public OpenTerrain? TerrainTarget()
    {
        var selected = Primary is { } primary ? Working?.Scene?[primary] : null;
        if (selected is not null && TerrainOf(selected) is { } chosen)
        {
            return Terrains.TerrainFor(chosen.File);
        }

        return FirstTerrainIn(Workspace.Loaded) ?? FirstTerrainIn(Workspace.SharedEntry);
    }

    /// <summary>
    /// Puts new ground in the scene: a terrain file of its own, written now, and a group object that
    /// shows it.
    /// <para>
    /// Written straight away rather than when the scene is saved, so the object never names a file
    /// that is not there. Undoing the add takes the object away and leaves the file, as it leaves any
    /// other asset.
    /// </para>
    /// </summary>
    public void NewTerrain()
    {
        if (Working is not { Scene: { } scene } open)
        {
            Say("There is no scene loaded to add to.", SayLevel.Warning);
            return;
        }

        if (scene.Layouts.Active is not null)
        {
            Say(LayoutRefusal("Add terrain"), SayLevel.Info);
            return;
        }

        var file = FreeTerrainFile();
        if (Terrains.Create(file, StartingGround()) is { } problem)
        {
            Say(problem, SayLevel.Error);
            return;
        }

        var added = new SceneObject(
            id: NextObjectId(),
            name: UniqueName(scene, "Terrain"),
            kind: ObjectKind.Group,
            components: new OrderedDictionary<string, Doc.SceneComponent> { [Doc.SceneComponents.Terrain] = new Doc.TerrainComponent(file) });
        if (Run(new AddObject(added, open.Id)))
        {
            SelectObject(added.Id);
            Say($"Made {file}", SayLevel.Info);
        }
    }

    private static Doc.TerrainComponent? TerrainOf(SceneObject item) =>
        PhysicsComponents.Of<Doc.TerrainComponent>(item, Doc.SceneComponents.Terrain);

    private OpenTerrain? FirstTerrainIn(SceneEntry? entry) => (entry?.Scene?.Objects ?? [])
        .Select(TerrainOf)
        .OfType<Doc.TerrainComponent>()
        .Select(component => Terrains.TerrainFor(component.File))
        .FirstOrDefault(open => open is not null);

    // A folder of its own under terrain/, since a terrain's regions are written beside it and two in
    // one folder would write over each other's.
    private string FreeTerrainFile()
    {
        for (var at = 1; ; at++)
        {
            var name = at == 1 ? "terrain" : $"terrain_{at}";
            var file = $"terrain/{name}/{name}{Ground.Extension}";
            if (Terrains[file] is null && !Directory.Exists(Path.Combine(Workspace.ProjectDirectory, "terrain", name)))
            {
                return file;
            }
        }
    }

    // Flat ground half a kilometre across round the origin, with rock for cliffs and grass for the
    // rest: the two sets the automatic cover starts out choosing between.
    private static Ground StartingGround()
    {
        var terrain = new Ground(sets: [new TerrainSet("Rock", tileSize: 8, triplanar: true), new TerrainSet("Grass")]);
        foreach (var z in new long[] { -1, 0 })
        {
            foreach (var x in new long[] { -1, 0 })
            {
                terrain.AddRegion(new RegionKey(x, z));
            }
        }

        return terrain;
    }

    // Writes every terrain with changes, and says which could not be written.
    private void SaveTerrains()
    {
        foreach (var problem in Terrains.SaveAll())
        {
            Say(problem, SayLevel.Error);
        }
    }

    // A change on the bench that is not a step on the undo stack, such as a terrain written. Only the
    // window's mark of unsaved changes follows it, and that moves when the answer does.
    private void OnTerrainsChanged()
    {
        if (Terrains.AnyUnsaved != _terrainsUnsaved)
        {
            _terrainsUnsaved = Terrains.AnyUnsaved;
            Changed?.Invoke();
        }
    }
}
