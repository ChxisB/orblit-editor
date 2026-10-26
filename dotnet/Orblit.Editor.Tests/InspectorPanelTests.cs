using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Light;
using Orblit.Scene;
using Orblit.Weather;

/// <summary>The inspector as a window shows it: what it offers for each kind of object, and what its fields change.</summary>
public sealed class InspectorPanelTests
{
    private const string PrefabPath = "prefabs/Lamp.oprefab";

    private static (EditorSession Session, Window Window) Open(EditorScene? scene = null, InspectorLinks? links = null)
    {
        var workspace = new Workspace("/project");
        if (scene is not null || !workspace.IsEmpty)
        {
            workspace.Add(new SceneEntry("main", "Main", scene: scene));
        }

        return Show(workspace, links);
    }

    private static (EditorSession Session, Window Window) Show(Workspace workspace, InspectorLinks? links)
    {
        var session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var panel = new InspectorPanel(session, links);
        var frame = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        frame.Children.Add(panel);
        var elsewhere = new Button { Name = "elsewhere", Content = "elsewhere" };
        elsewhere[Grid.RowProperty] = 1;
        frame.Children.Add(elsewhere);
        var window = new Window { Width = 360, Height = 900, Content = frame };
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static (EditorSession Session, Window Window) Selecting(string id, EditorScene? scene = null, InspectorLinks? links = null)
    {
        var (session, window) = Open(scene ?? EditorScene.Starter(), links);
        session.Select([id]);
        Headless.Render();
        return (session, window);
    }

    private static bool Has(Control root, string name) => Headless.All<Control>(root).Any(one => one.Name == name);

    private static string TextOf(Control root, string name) => Headless.Find<TextBlock>(root, name).Text ?? "";

    private static EditorScene Prefabbed(PrefabState state) => new(
    [
        new SceneObject("lamp", "Lamp", ObjectKind.Group, prefab: new PrefabComponent(asset: PrefabPath, state: state)),
        new SceneObject("lamp/bulb", "Bulb", ObjectKind.Mesh, parentId: "lamp"),
        new SceneObject("crate", "Crate", ObjectKind.Mesh),
    ]);

    [Fact]
    public Task WithNoSceneItOffersToMakeOne() => Headless.Run(() =>
    {
        var (_, window) = Show(new Workspace("/project"), null);

        Assert.Equal("Open or create a scene to edit its objects.", TextOf(window, "inspector_empty"));
        Assert.True(Has(window, "button_new_scene"));
    });

    [Fact]
    public Task WithASceneAndNothingSelectedItAsksForAnObject() => Headless.Run(() =>
    {
        var (_, window) = Open(EditorScene.Starter());

        Assert.Equal("Select an object to edit it.", TextOf(window, "inspector_pick"));
        Assert.False(Has(window, "section_transform"));
    });

    [Fact]
    public Task AMeshShowsItsTransformAndItsRendererAndNoLight() => Headless.Run(() =>
    {
        var (_, window) = Selecting("crate");

        Assert.True(Has(window, "section_transform"));
        Assert.True(Has(window, "section_mesh_renderer"));
        Assert.False(Has(window, "section_light"));
        Assert.Contains(Headless.All<TextBlock>(window), one => one.Text == "cube (built in)");
    });

    [Fact]
    public Task WeatherHasNoPositionToShow() => Headless.Run(() =>
    {
        var (_, window) = Selecting("sky", new EditorScene([new SceneObject("sky", "Sky", ObjectKind.Weather)]));

        Assert.False(Has(window, "section_transform"));
    });

    [Fact]
    public Task APositionDragMovesTheObjectAndIsOneUndoStep() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");
        var crate = session.InspectedItem!;
        var start = crate.Position.X;
        var at = Headless.Centre(window, "drag_position_0");

        window.MouseDown(at, MouseButton.Left);
        window.MouseMove(at + new Vector(10, 0));
        window.MouseUp(at + new Vector(10, 0), MouseButton.Left);
        Headless.Render();

        Assert.Equal(start + 0.2, crate.Position.X, 6);
        session.History.Undo();
        Assert.Equal(start, crate.Position.X, 6);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task AScaleCannotBeDraggedThroughZero() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");
        var at = Headless.Centre(window, "drag_scale_0");

        window.MouseDown(at, MouseButton.Left);
        window.MouseMove(at + new Vector(-400, 0));
        window.MouseUp(at + new Vector(-400, 0), MouseButton.Left);
        Headless.Render();

        Assert.Equal(0.001, session.InspectedItem!.Scale.X, 9);
    });

    [Fact]
    public Task RenamingInTheHeaderIsOneUndoStepAndItsLabelNamesTheOldName() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");
        var box = Headless.Find<TextBox>(window, "inspector_name");

        box.Text = "B";
        box.Text = "Box";
        Headless.Find<Button>(window, "elsewhere").Focus();
        Headless.Render();

        Assert.Equal("Box", session.InspectedItem!.Name);
        Assert.Equal("Rename Crate", session.History.UndoLabel);
        session.History.Undo();
        Assert.Equal("Crate", session.InspectedItem!.Name);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task ANameOfOnlySpacesIsNotARename() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");

        Headless.Find<TextBox>(window, "inspector_name").Text = "   ";
        Headless.Render();

        Assert.False(session.History.CanUndo);
        Assert.Equal("Crate", session.InspectedItem!.Name);
    });

    [Fact]
    public Task TheVisibleSwitchHidesTheObjectAndUndoShowsIt() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");

        Headless.Click(window, "inspector_visible");

        Assert.False(session.InspectedItem!.Visible);
        session.History.Undo();
        Assert.True(session.InspectedItem!.Visible);
    });

    [Fact]
    public Task SomethingInsideAHiddenObjectSaysWhyItIsNotDrawn() => Headless.Run(() =>
    {
        var scene = EditorScene.Starter();
        scene["props"]!.Visible = false;
        var (_, window) = Selecting("crate", scene);

        Assert.Equal("Hidden anyway, because something it is inside is hidden.", TextOf(window, "inspector_hidden_above"));
        Assert.Equal("in Props", TextOf(window, "inspector_parent"));
    });

    [Fact]
    public Task SeveralSelectedObjectsSayWhichOneTheFieldsEdit() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());

        session.Select(["crate", "cube"]);
        Headless.Render();

        Assert.Contains(" selected · editing ", Headless.All<TextBlock>(Headless.Find<Control>(window, "inspector_multiple")).Single().Text);
        Assert.Contains("2 selected", Headless.All<TextBlock>(Headless.Find<Control>(window, "inspector_multiple")).Single().Text);
    });

    [Fact]
    public Task AnInstanceShowsItsPrefabBandAndItsButtonsCallBackWithTheSelectedId() => Headless.Run(() =>
    {
        var applied = new List<string>();
        var reverted = new List<string>();
        var links = new InspectorLinks { OnApplyPrefab = applied.Add, OnRevertPrefab = reverted.Add };
        var (_, window) = Selecting("lamp", Prefabbed(PrefabState.Open), links);

        Assert.Equal("Lamp.oprefab", TextOf(window, "prefab_name"));
        Assert.False(Has(window, "prefab_note"));
        Headless.Click(window, "prefab_apply");
        Headless.Click(window, "prefab_revert");

        Assert.Equal(["lamp"], applied);
        Assert.Equal(["lamp"], reverted);
        Assert.False(Headless.Find<Button>(window, "prefab_unpack").IsEnabled);
    });

    [Fact]
    public Task APartOfAnInstanceNamesTheInstanceAndActsOnItsOwnId() => Headless.Run(() =>
    {
        var applied = new List<string>();
        var (_, window) = Selecting("lamp/bulb", Prefabbed(PrefabState.Open), new InspectorLinks { OnApplyPrefab = applied.Add });

        Assert.Equal("Part of Lamp. Changes here are changes to that instance.", TextOf(window, "prefab_note"));
        Headless.Click(window, "prefab_apply");

        Assert.Equal(["lamp/bulb"], applied);
    });

    [Fact]
    public Task APrefabThatCannotBeReadOffersNothingToDoAboutIt() => Headless.Run(() =>
    {
        var links = new InspectorLinks { OnApplyPrefab = _ => { }, OnRevertPrefab = _ => { }, OnUnpackPrefab = _ => { } };
        var (_, window) = Selecting("lamp", Prefabbed(PrefabState.Folded), links);

        Assert.Equal("Lamp.oprefab could not be read. Until it can, this is only a link.", TextOf(window, "prefab_note"));
        Assert.All(["prefab_apply", "prefab_revert", "prefab_unpack"], name => Assert.False(Headless.Find<Button>(window, name).IsEnabled));
    });

    [Fact]
    public Task AnObjectThatIsNotInAnInstanceHasNoPrefabBand() => Headless.Run(() =>
    {
        var (_, window) = Selecting("crate", Prefabbed(PrefabState.Open));

        Assert.False(Has(window, "prefab_band"));
    });

    [Fact]
    public Task TurningShadowsOffOnAMeshIsOneUndoStep() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");
        var crate = session.InspectedItem!;
        Assert.True(crate.CastShadows);

        Headless.Click(window, "choice_cast_shadows_off");

        Assert.False(crate.CastShadows);
        session.History.Undo();
        Assert.True(crate.CastShadows);
    });

    [Fact]
    public Task ATextureCanBeClearedFromTheMeshAndPutBack() => Headless.Run(() =>
    {
        var scene = EditorScene.Starter();
        scene["crate"]!.MaterialAsset = "textures/brick.png";
        var (session, window) = Selecting("crate", scene);

        Headless.Click(window, "texture_clear");

        Assert.Null(session.InspectedItem!.MaterialAsset);
        Assert.False(Has(window, "texture_clear"));
        session.History.Undo();
        Assert.Equal("textures/brick.png", session.InspectedItem!.MaterialAsset);
    });

    [Fact]
    public Task ASunBecomingAPointLightRestatesItsPowerAndUndoPutsItBack() => Headless.Run(() =>
    {
        var (session, window) = Selecting("sun");
        var sun = session.InspectedItem!;
        var was = sun.Power;
        Assert.Equal(LightType.Sun, sun.LightType);
        Assert.False(Has(window, "row_cone"));

        Headless.Click(window, "choice_type_point");

        Assert.Equal(LightType.Point, sun.LightType);
        Assert.Equal(was * 4 * Math.PI, sun.Power, 6);
        session.History.Undo();
        Assert.Equal(LightType.Sun, sun.LightType);
        Assert.Equal(was, sun.Power, 6);
    });

    [Fact]
    public Task OnlyASpotHasAConeAndOnlyASunHasASunSize() => Headless.Run(() =>
    {
        var (_, window) = Selecting("sun");
        Assert.True(Has(window, "row_sun_size"));
        Assert.False(Has(window, "row_cone"));

        Headless.Click(window, "choice_type_spot");

        Assert.True(Has(window, "row_cone"));
        Assert.True(Has(window, "row_blend"));
        Assert.True(Has(window, "row_radius"));
        Assert.False(Has(window, "row_sun_size"));
    });

    [Fact]
    public Task TheCelestialLightOffersABodyAndTheNoteChangesWithTheType() => Headless.Run(() =>
    {
        var (session, window) = Selecting("sun");

        Headless.Click(window, "choice_body_moon");

        Assert.Equal(CelestialBody.Moon, session.InspectedItem!.Body);
        Headless.Click(window, "choice_type_area");
        Assert.False(Has(window, "choice_body_moon"));
        Assert.Contains(Headless.All<TextBlock>(window), one => (one.Text ?? "").StartsWith("An area light", StringComparison.Ordinal));
    });

    [Fact]
    public Task WhileTheDayRunsTheBodyIsNotAChoice() => Headless.Run(() =>
    {
        var scene = EditorScene.Starter();
        scene.DayCycle = true;
        var (session, window) = Selecting("sun", scene);
        var was = session.InspectedItem!.Body;

        Headless.Click(window, "choice_body_moon");

        Assert.Equal(was, session.InspectedItem!.Body);
        Assert.Contains(Headless.All<TextBlock>(window), one => (one.Text ?? "").StartsWith("The day cycle is deciding", StringComparison.Ordinal));
    });

    [Fact]
    public Task ALightOtherThanTheCelestialOneHasNoBody() => Headless.Run(() =>
    {
        var scene = EditorScene.Starter();
        scene.Add(new SceneObject("lamp", "Lamp", ObjectKind.Light, lightType: LightType.Point));
        var (_, window) = Selecting("lamp", scene);

        Assert.False(Has(window, "choice_body_moon"));
        Assert.True(Has(window, "choice_type_point"));
    });

    [Fact]
    public Task DataLinksOpenWhereTheyLiveAndComeOffTheObjectOnRequest() => Headless.Run(() =>
    {
        var scene = EditorScene.Starter();
        scene["crate"]!.Data.AddRange(["data/Weight.odata", "data/Wood.odata"]);
        var opened = new List<string>();
        var detached = new List<(string, string)>();
        var links = new InspectorLinks { OnOpenData = opened.Add, OnDetachData = (id, path) => detached.Add((id, path)) };
        var (_, window) = Selecting("crate", scene, links);

        Assert.Equal("Weight.odata", TextOf(window, "data_open_0"));
        Headless.Click(window, "data_open_1");
        Headless.Click(window, "data_remove_0");

        Assert.Equal(["data/Wood.odata"], opened);
        Assert.Equal([("crate", "data/Weight.odata")], detached);
    });

    [Fact]
    public Task WithNothingToDetachWithNoRemoveButtonIsOffered() => Headless.Run(() =>
    {
        var scene = EditorScene.Starter();
        scene["crate"]!.Data.Add("data/Weight.odata");
        var (_, window) = Selecting("crate", scene);

        Assert.True(Has(window, "data_open_0"));
        Assert.False(Has(window, "data_remove_0"));
    });

    [Fact]
    public Task AnObjectWithNoDataHasNoDataSection() => Headless.Run(() =>
    {
        var (_, window) = Selecting("crate");

        Assert.False(Has(window, "section_data"));
    });

    [Fact]
    public Task ACanvasShowsItsInterfaceAndOpensIt() => Headless.Run(() =>
    {
        var scene = new EditorScene([new SceneObject("hud", "Hud", ObjectKind.Canvas) { InterfaceAsset = "ui/Hud.oui" }]);
        var opened = new List<string>();
        var (_, window) = Selecting("hud", scene, new InspectorLinks { OnOpenInterface = opened.Add });

        Assert.Equal("Hud.oui", TextOf(window, "interface_link"));
        Headless.Click(window, "interface_link");

        Assert.Equal(["ui/Hud.oui"], opened);
    });

    [Fact]
    public Task ACanvasWithNoInterfaceSaysHowToGiveItOne() => Headless.Run(() =>
    {
        var (_, window) = Selecting("hud", new EditorScene([new SceneObject("hud", "Hud", ObjectKind.Canvas)]));

        Assert.Contains(Headless.All<TextBlock>(window), one => (one.Text ?? "").StartsWith("Nothing yet. Drag a .oui", StringComparison.Ordinal));
        Assert.False(Has(window, "interface_link"));
    });

    [Fact]
    public Task AFoldedSectionStaysFoldedWhenAnotherObjectIsSelected() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");

        Headless.Click(window, "section_transform");
        Assert.False(Headless.Find<Control>(window, "drag_position_0").IsEffectivelyVisible);
        session.Select(["cube"]);
        Headless.Render();

        Assert.False(Headless.Find<Control>(window, "drag_position_0").IsEffectivelyVisible);
    });
}
