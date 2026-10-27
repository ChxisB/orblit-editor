using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Terrain;
using static Orblit.Editor.Tests.InspectorRig;

// The shell's terrain tests in editor_shell_terrain_test, driven through the window. The Dart test
// for a set added in the inspector is here too, on the window's own inspector.
public sealed class EditorWindowTerrainTests
{
    private const string Made = "terrain/terrain/terrain.oterrain";

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static OpenTerrain OnDisk(EditorSession session)
    {
        var root = session.Workspace.ProjectDirectory;
        return new TerrainBench(new History(new Workspace(root)), root).TerrainFor(Made)!;
    }

    private static void AddTerrain(ViewportRig rig)
    {
        Click(Headless.Find<MenuItem>(rig.Window, "add_terrain"));
        Headless.Render();
    }

    [Fact]
    public Task AddingTerrainFromTheMenuWritesTheGroundAndTheInspectorShowsWhatItIsMadeOf() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();

        AddTerrain(rig);

        Assert.Equal("Add Terrain", rig.Session.History.UndoLabel);
        Assert.True(Has(rig.Window, "section_regions"));
        Assert.True(Has(rig.Window, "section_sets"));
        Assert.True(Has(rig.Window, "section_automatic_cover"));
        rig.Session.Save();
        Assert.Equal(4, OnDisk(rig.Session).Terrain.Regions.Count());
        Assert.Contains(Made, File.ReadAllText(Path.Combine(rig.Session.Workspace.ProjectDirectory, rig.Session.Workspace.Loaded!.Path!)));
    });

    [Fact]
    public Task ASetAddedInTheInspectorIsOneStepToUndo() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        AddTerrain(rig);
        var sets = rig.Session.Terrains[Made]!.Terrain.Sets;

        Press(rig.Window, "button_add_set");

        Assert.Equal(["Rock", "Grass", "Set 3"], sets.Select(set => set.Name));
        rig.Session.Undo();
        Assert.Equal(2, sets.Count);
    });

    [Fact]
    public Task TheTerrainModeShowsTheShelfAndTheBrushPanelAndNothingBeforeIt() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        AddTerrain(rig);
        var shelf = Headless.Find<Control>(rig.Window, "terrain_shelf");
        Assert.False(shelf.IsVisible);
        Assert.False(Has(rig.Window, "brush_fields"));

        rig.Session.EnterMode("terrain");
        Headless.Render();

        Assert.True(shelf.IsVisible);
        Assert.Equal("terrain", TextOf(rig.Window, "brush_terrain"));
        Headless.Click(rig.Window, "tool_Lower");
        Assert.Equal(BrushTool.Lower, rig.Session.Terrains.Tool);
        Assert.Equal(BrushWords.Of(BrushTool.Lower).Hint, TextOf(rig.Window, "brush_hint"));
    });

    [Fact]
    public Task TheShelfAndThePanelShowTheSameBrushAndFollowEachOther() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        AddTerrain(rig);
        rig.Session.EnterMode("terrain");
        Headless.Render();

        Headless.Find<Slider>(rig.Window, "shelf_size").Value = 40;
        Headless.Render();

        Assert.Equal(40, rig.Session.Terrains.Brush.Size);
        Assert.Equal(40, Headless.Find<Slider>(rig.Window, "slider_size").Value);
        rig.Session.Terrains.Brush = rig.Session.Terrains.Brush.CopyWith(strength: 0.25);
        Headless.Render();
        Assert.Equal(0.25, Headless.Find<Slider>(rig.Window, "shelf_strength").Value);
        Assert.Equal("0.25", Headless.All<TextBlock>(rig.Window).First(one => one.Text == "0.25").Text);
    });

    [Fact]
    public Task ADragInTheViewRaisesTheGroundAsOneStepAndMarksTheSceneUnsaved() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        AddTerrain(rig);
        rig.Session.Save();
        rig.Session.EnterMode("terrain");
        Headless.Render();
        Assert.DoesNotContain("•", Headless.Find<TextBlock>(rig.Window, "title").Text);

        var from = rig.Centre;
        rig.Window.MouseDown(from, MouseButton.Left);
        foreach (var step in new[] { 0.25, 0.5, 0.75, 1 })
        {
            rig.Window.MouseMove(new Point(from.X + (60 * step), from.Y));
        }

        rig.Window.MouseUp(new Point(from.X + 60, from.Y), MouseButton.Left);
        Headless.Render();

        Assert.Equal("Raise terrain", rig.Session.History.UndoLabel);
        Assert.EndsWith("•", Headless.Find<TextBlock>(rig.Window, "title").Text);
        rig.Session.Save();
        Assert.True(OnDisk(rig.Session).Terrain.Regions.SelectMany(region => region.Heights).Max() > 0);
        rig.Session.Undo();
        Assert.Equal("Add Terrain", rig.Session.History.UndoLabel);
    });

    [Fact]
    public Task LeavingTheTerrainModeTakesTheRingOffTheGround() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        AddTerrain(rig);
        rig.Session.EnterMode("terrain");
        Headless.Render();
        rig.Window.MouseMove(rig.Centre);
        Assert.NotNull(rig.Session.Terrains.Hover);

        rig.Session.EnterMode("scene");

        Assert.Null(rig.Session.Terrains.Hover);
    });

    [Fact]
    public Task ADragOutsideTheTerrainModeNeverShapesTheGround() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        AddTerrain(rig);

        var from = rig.Centre;
        rig.Window.MouseDown(from, MouseButton.Left);
        rig.Window.MouseMove(new Point(from.X + 30, from.Y));
        rig.Window.MouseMove(new Point(from.X + 60, from.Y));
        rig.Window.MouseUp(new Point(from.X + 60, from.Y), MouseButton.Left);

        // The terrain is what is selected, so the drag moves it, as it would any other object.
        Assert.Equal("Move Terrain", rig.Session.History.UndoLabel);
        Assert.False(rig.Session.Terrains.AnyUnsaved);
        Assert.Null(rig.Session.Terrains.Hover);
    });
}
