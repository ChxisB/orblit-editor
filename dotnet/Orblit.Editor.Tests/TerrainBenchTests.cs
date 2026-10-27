namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Terrain;
using Orblit.VectorMath;
using Doc = Orblit.Scene;
using Ground = Orblit.Terrain.Terrain;

// The terrain mode's side of the editor: terrains written and read back, strokes and settings changed
// through the undo stack, and the ground and what grows on it handed to the renderer.
public sealed class TerrainBenchTests : IDisposable
{
    private const string File = "terrain/hills/hills.oterrain";

    private readonly string _root = Directory.CreateTempSubdirectory("terrain_bench").FullName;

    private sealed record Rig(TerrainBench Bench, History History, OpenTerrain Open, List<string> Said);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Assert.NotNull does not hand the value back for a reference type.
    private static T Some<T>(T? value)
        where T : class
    {
        Assert.NotNull(value);
        return value;
    }

    private Rig MakeRig()
    {
        var history = new History(new Workspace(_root));
        var said = new List<string>();
        var bench = new TerrainBench(history, _root, said.Add);
        var terrain = new Ground(regionSize: 32, sets: [new TerrainSet("Rock"), new TerrainSet("Grass")]);
        foreach (var z in new long[] { -1, 0 })
        {
            foreach (var x in new long[] { -1, 0 })
            {
                terrain.AddRegion(new RegionKey(x, z));
            }
        }

        Assert.Null(bench.Create(File, terrain));
        return new Rig(bench, history, Some(bench[File]), said);
    }

    // An object that puts the terrain in a file in the scene.
    private static SceneObject GroundObject(string id, string file = File, bool castShadows = true, bool visible = true) =>
        new(
            id,
            id,
            ObjectKind.Group,
            visible: visible,
            components: new OrderedDictionary<string, Doc.SceneComponent>
            {
                [Doc.SceneComponents.Terrain] = new Doc.TerrainComponent(file, castShadows),
            });

    // Every height in the terrain, so two can be compared whole.
    private static List<float> Heights(Ground terrain) => [.. terrain.Regions.SelectMany(region => region.Heights)];

    // A drag of the brush from x = 0 to x = 8 along z = 0, as one gesture.
    private static void Stroke(Rig rig)
    {
        var stroke = rig.Bench.StrokeOn(rig.Open, seed: 1);
        var gesture = new object();
        foreach (var x in new[] { 0.0, 4.0, 8.0 })
        {
            rig.History.Run(new TerrainStrokeStep(rig.Bench, rig.Open.File, stroke, new BrushMove(x, 0, gesture, "Raise hills")));
        }

        rig.History.Seal();
    }

    private static SceneRenderOptions Options(TerrainBench bench) =>
        new() { TerrainOf = bench.RenderFor, ScatterOf = bench.ScatterFor };

    [Fact]
    public void ATerrainMadeHereIsReadBackAsItWasWritten()
    {
        var rig = MakeRig();
        rig.Bench.Brush = new Brush(size: 8, strength: 1);
        Stroke(rig);
        Assert.Null(rig.Bench.Save(rig.Open));

        var again = Some(new TerrainBench(new History(new Workspace(_root)), _root).TerrainFor(File));

        Assert.Equal(4, again.Terrain.Regions.Count());
        Assert.Equal(["Rock", "Grass"], again.Terrain.Sets.Select(set => set.Name));
        Assert.Equal(Heights(rig.Open.Terrain), Heights(again.Terrain));
        Assert.True(again.Terrain.HeightAt(4, 0) > 0);
    }

    [Fact]
    public void AFileThatCannotBeReadIsSaidOnceAndNotTriedAgain()
    {
        var rig = MakeRig();

        Assert.Null(rig.Bench.TerrainFor("terrain/none/none.oterrain"));
        Assert.Null(rig.Bench.TerrainFor("terrain/none/none.oterrain"));

        Assert.Contains("none.oterrain", Assert.Single(rig.Said));
    }

    [Fact]
    public void SavingWritesWhatChangedAndAStrokeUndoneAfterASaveIsAChangeAgain()
    {
        var rig = MakeRig();
        Assert.False(rig.Bench.IsUnsaved(rig.Open));

        Stroke(rig);
        Assert.True(rig.Bench.IsUnsaved(rig.Open));
        Assert.True(rig.Bench.AnyUnsaved);

        Assert.Empty(rig.Bench.SaveAll());
        Assert.False(rig.Bench.IsUnsaved(rig.Open));

        rig.History.Undo();
        Assert.True(rig.Bench.IsUnsaved(rig.Open));
    }

    [Fact]
    public void ADragIsOneStepHoweverManyMovesItTakesAndUndoingItPutsTheGroundBack()
    {
        var rig = MakeRig();
        rig.Bench.Brush = new Brush(size: 8, strength: 1);
        var flat = Heights(rig.Open.Terrain);

        Stroke(rig);
        var raised = Heights(rig.Open.Terrain);
        Assert.True(rig.Open.Terrain.HeightAt(0, 0) > 0);
        Assert.True(rig.Open.Terrain.HeightAt(8, 0) > 0);
        Assert.Equal("Raise hills", rig.History.UndoLabel);

        rig.History.Undo();
        Assert.Equal(flat, Heights(rig.Open.Terrain));
        Assert.False(rig.History.CanUndo);

        rig.History.Redo();
        Assert.Equal(raised, Heights(rig.Open.Terrain));
    }

    [Fact]
    public void AStepKeepsOnlyTheTilesTheStrokeTouched()
    {
        var rig = MakeRig();

        // One region of 128 is sixteen tiles of 32. A brush 4 m across in the middle of one of them
        // touches that one and no other.
        const string big = "terrain/big/big.oterrain";
        var terrain = new Ground(regionSize: 128);
        terrain.AddRegion(new RegionKey(0, 0));
        Assert.Null(rig.Bench.Create(big, terrain));
        rig.Bench.Brush = new Brush(size: 4, strength: 1);
        var step = new TerrainStrokeStep(
            rig.Bench,
            big,
            rig.Bench.StrokeOn(Some(rig.Bench[big]), seed: 1),
            new BrushMove(48, 48, new object(), "Raise big"));

        rig.History.Run(step);

        var patch = Some(step.Patch);
        Assert.False(patch.IsEmpty);

        // A tile's heights before and after, and nothing else.
        Assert.Equal(32L * 32 * 4 * 2, patch.ByteCount);
    }

    [Fact]
    public void TwoDragsAreTwoSteps()
    {
        var rig = MakeRig();
        Stroke(rig);
        Stroke(rig);

        rig.History.Undo();
        Assert.True(rig.History.CanUndo);

        rig.History.Undo();
        Assert.False(rig.History.CanUndo);
    }

    [Fact]
    public void ASliderDraggedIsOneSettingsStep()
    {
        var rig = MakeRig();
        var was = rig.Open.Terrain.BlendSharpness;
        foreach (var value in new[] { 0.6, 0.4, 0.3 })
        {
            rig.Bench.Edit(rig.Open, "Set hills blend", settings => settings with { BlendSharpness = value }, "blend");
        }

        rig.History.Seal();
        Assert.Equal(0.3, rig.Open.Terrain.BlendSharpness);

        rig.History.Undo();
        Assert.Equal(was, rig.Open.Terrain.BlendSharpness);
        Assert.False(rig.History.CanUndo);
    }

    [Fact]
    public void ChangingTheSetsAsksTheRendererForPicturesAgain()
    {
        var rig = MakeRig();
        var before = rig.Bench.PicturesRevision;

        rig.Bench.Edit(rig.Open, "Add a set to hills", settings => settings with { Sets = [.. settings.Sets, new TerrainSet("Sand")] });

        Assert.Equal("Sand", rig.Open.Terrain.Sets[^1].Name);
        Assert.True(rig.Bench.PicturesRevision > before);
    }

    [Fact]
    public void TakingARegionAwayAndUndoingItPutsBackTheSameGroundAndItsFileFollows()
    {
        var rig = MakeRig();
        rig.Bench.Brush = new Brush(size: 8, strength: 1);
        Stroke(rig);
        var key = new RegionKey(0, 0);
        var region = Some(rig.Open.Terrain.RegionAt(key));
        var shape = region.Heights.ToArray();
        var path = Path.Combine(rig.Open.Folder, key.FileName);
        Assert.Null(rig.Bench.Save(rig.Open));
        Assert.True(System.IO.File.Exists(path));

        rig.History.Run(new TerrainRegionEdit(rig.Bench, rig.Open.File, region, adding: false));
        rig.History.Seal();
        Assert.Equal("Take ground from hills", rig.History.UndoLabel);
        Assert.Null(rig.Open.Terrain.RegionAt(key));
        Assert.Null(rig.Bench.Save(rig.Open));
        Assert.False(System.IO.File.Exists(path));

        rig.History.Undo();
        Assert.Same(region, rig.Open.Terrain.RegionAt(key));
        Assert.Equal(shape, region.Heights);
        Assert.Null(rig.Bench.Save(rig.Open));
        Assert.True(System.IO.File.Exists(path));
    }

    [Fact]
    public void OneGroundPerFileHoweverManyObjectsNameItWithTheShadowsTheObjectAsksFor()
    {
        var rig = MakeRig();

        var terrains = rig.Bench.RenderFor(
        [
            GroundObject("a", castShadows: false),
            GroundObject("b"),
            new SceneObject("box", "Box", ObjectKind.Mesh),
        ]);

        var drawn = Assert.Single(terrains);
        Assert.Equal(4, drawn.Regions.Count);
        Assert.False(drawn.Options.CastShadows);
        Assert.True(drawn.Options.ReceiveShadows);
        Assert.Empty(rig.Said);
    }

    [Fact]
    public void AFileThatCannotBeReadDrawsNothingAndIsSaid()
    {
        var rig = MakeRig();

        Assert.Empty(rig.Bench.RenderFor([GroundObject("lost", "terrain/none/none.oterrain")]));

        Assert.Single(rig.Said);
    }

    [Fact]
    public void AStrokeChangesTheRevisionTheRendererUploadsBy()
    {
        var rig = MakeRig();
        long Revisions() => rig.Bench.RenderFor([GroundObject("a")]).Single().Regions.Sum(region => (long)region.Revision);

        var before = Revisions();
        Stroke(rig);

        Assert.True(Revisions() > before);
    }

    [Fact]
    public void ASceneHandsOverTheGroundItShowsAndTheSharedSets()
    {
        var rig = MakeRig();
        const string other = "terrain/other/other.oterrain";
        var terrain = new Ground(regionSize: 32);
        terrain.AddRegion(new RegionKey(0, 0));
        Assert.Null(rig.Bench.Create(other, terrain));
        var camera = new OrbitCamera().ToRenderCamera();

        var scene = new EditorScene([GroundObject("here"), GroundObject("hidden", other, visible: false)]);
        // A hidden object puts no ground in the view.
        Assert.Single(scene.ToRenderScene(camera, Options(rig.Bench)).Terrain);

        var shared = new EditorScene([GroundObject("there", other)]);
        Assert.Equal(2, scene.ToRenderScene(camera, Options(rig.Bench) with { Shared = shared }).Terrain.Count);

        // Without a bench there is nothing to draw.
        Assert.Empty(scene.ToRenderScene(camera).Terrain);
    }

    [Fact]
    public void WhatIsScatteredOverItIsDrawnAsBlocksOncePerFile()
    {
        var rig = MakeRig();
        rig.Open.Terrain.Scatter.AddRange(
        [
            new ScatterLayer("grass", density: 1),
            new ScatterLayer("trees", seed: 2, density: 0.1, mesh: "tree.glb"),
        ]);

        // A population a region for the grass. The trees are models, which the editor does not draw yet.
        var drawn = rig.Bench.ScatterFor([GroundObject("a"), GroundObject("b")]);
        Assert.Equal(4, drawn.Count);
        Assert.Equal(4, drawn.Select(population => population.Key).Distinct().Count());
        Assert.All(drawn, population => Assert.NotEmpty(population.Transforms));

        var again = rig.Bench.ScatterFor([GroundObject("a")]);
        for (var i = 0; i < drawn.Count; i++)
        {
            // Nothing moved.
            Assert.Same(drawn[i], again[i]);
        }

        Stroke(rig);
        var after = rig.Bench.ScatterFor([GroundObject("a")]);
        // The grass is placed again where the brush raised it.
        Assert.NotEqual(drawn.Select(population => population.Revision), after.Select(population => population.Revision));

        var camera = new OrbitCamera().ToRenderCamera();
        var scene = new EditorScene([GroundObject("here")]);
        Assert.Equal(4, scene.ToRenderScene(camera, Options(rig.Bench)).Populations.Count);
        Assert.Empty(scene.ToRenderScene(camera).Populations);
    }

    [Fact]
    public void GroundWithNothingScatteredDrawsNothingOverIt()
    {
        var rig = MakeRig();

        Assert.Empty(rig.Bench.ScatterFor([GroundObject("a")]));
    }

    // Looking down at the origin from above, the way a new view does.
    private static readonly ViewportProjection Looking = new(new OrbitCamera(), new Size(800, 600));
    private static readonly Offset Centre = new(400, 300);

    private static ViewportGesture At(ViewportPhase phase, Offset where, bool add = false) =>
        new(phase, where, add, Looking);

    private static TerrainBrushInput BrushOn(Rig rig) => new(rig.Bench, rig.History, () => rig.Open);

    [Fact]
    public void ADragOverTheGroundIsOneStrokeNamedForTheTool()
    {
        var rig = MakeRig();
        var input = BrushOn(rig);
        var along = new Offset(Centre.Dx + 40, Centre.Dy);

        Assert.True(input.Handle(At(ViewportPhase.DragStart, Centre)));
        Assert.True(input.Handle(At(ViewportPhase.DragUpdate, along)));
        Assert.True(input.Handle(At(ViewportPhase.DragEnd, along)));

        Assert.Equal("Raise hills", rig.History.UndoLabel);
        Assert.Contains(Heights(rig.Open.Terrain), height => height > 0);
        rig.History.Undo();
        Assert.False(rig.History.CanUndo);
    }

    [Fact]
    public void WithTheModifierHeldTheToolTurnsRound()
    {
        var rig = MakeRig();
        var input = BrushOn(rig);

        input.Handle(At(ViewportPhase.DragStart, Centre, add: true));
        input.Handle(At(ViewportPhase.DragEnd, Centre, add: true));

        Assert.Contains(Heights(rig.Open.Terrain), height => height < 0);
        Assert.DoesNotContain(Heights(rig.Open.Terrain), height => height > 0);
    }

    [Fact]
    public void TheSkyOrNoTerrainAtAllIsLeftToTheView()
    {
        var rig = MakeRig();

        Assert.False(BrushOn(rig).Handle(At(ViewportPhase.Tap, new Offset(400, 0))));
        Assert.False(rig.History.CanUndo);

        var none = new TerrainBrushInput(rig.Bench, rig.History, () => null);
        Assert.False(none.Handle(At(ViewportPhase.Tap, Centre)));
        Assert.False(rig.History.CanUndo);
    }

    [Fact]
    public void HoveringOverTheGroundShowsWhereTheBrushIs()
    {
        var rig = MakeRig();
        var input = BrushOn(rig);
        var moved = 0;
        rig.Bench.HoverChanged += () => moved++;

        Assert.True(input.Handle(At(ViewportPhase.Hover, Centre)));
        Assert.NotNull(rig.Bench.Hover);
        input.Handle(At(ViewportPhase.Leave, Centre));
        Assert.Null(rig.Bench.Hover);
        Assert.Equal(2, moved);
    }

    [Fact]
    public void ADragThatLeavesTheGroundCarriesOnAtTheHeightItWasLastAt()
    {
        var rig = MakeRig();
        var input = BrushOn(rig);

        // Far enough back that a point beyond the last region is still in front of the eye.
        var far = new ViewportProjection(new OrbitCamera(distance: 300), new Size(800, 600));
        var past = far.Project(new Vector3(100, 0, 0)) ?? throw new InvalidOperationException("behind the eye");

        input.Handle(new ViewportGesture(ViewportPhase.DragStart, Centre, Projection: far));
        Assert.True(input.Handle(new ViewportGesture(ViewportPhase.DragUpdate, past, Projection: far)));
        Assert.True(input.Handle(new ViewportGesture(ViewportPhase.DragEnd, past, Projection: far)));

        // 100 is past the last region, so only the level the stroke was at can have put it there.
        Assert.Equal(100, Some(rig.Bench.Hover).X, 3);
        Assert.Equal("Raise hills", rig.History.UndoLabel);
        rig.History.Undo();
        Assert.False(rig.History.CanUndo);
    }

    [Fact]
    public void EachToolHasItsWordsAndAStrokeIsNamedForIt()
    {
        foreach (var tool in Enum.GetValues<BrushTool>())
        {
            var words = BrushWords.Of(tool);
            Assert.NotEmpty(words.Label);
            Assert.NotEmpty(words.Hint);
            Assert.NotEmpty(words.Icon);
        }

        Assert.Equal("Raise hills", BrushWords.StrokeLabel(BrushTool.Raise, "hills"));
        Assert.Equal("Paint colour on hills", BrushWords.StrokeLabel(BrushTool.Colour, "hills"));
        Assert.Equal("Punch holes in hills", BrushWords.StrokeLabel(BrushTool.Hole, "hills"));
    }
}
