using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;
using Doc = Orblit.Scene;
using static Orblit.Editor.Tests.InspectorRig;

/// <summary>The body section, as a person meets it in the inspector: adding a body, fitting it, and what a free one is held to.</summary>
public sealed class InspectorBodyTests
{
    // A crate that draws nothing of its own yet, a cube with a mesh, a place in the tree, and a sky.
    private static EditorScene World(Doc.BodyComponent? crateBody = null)
    {
        var parts = new OrderedDictionary<string, Doc.SceneComponent>();
        if (crateBody is not null)
        {
            parts[Doc.SceneComponents.Body] = crateBody;
        }

        return new EditorScene(
        [
            new SceneObject("crate", "Crate", ObjectKind.Mesh, components: parts),
            new SceneObject("cube", "Cube", ObjectKind.Shape, shape: Shape.Of(ShapeKind.Cube)),
            new SceneObject("spot", "Spot", ObjectKind.Group),
            new SceneObject("sky", "Sky", ObjectKind.Weather),
        ]);
    }

    private static Doc.BodyComponent BodyOf(EditorSession session, string id) =>
        PhysicsComponents.PhysicsBodyOf(session.Workspace.Loaded!.Scene![id]!)!;

    private static (EditorSession Session, Window Window) FreeCrate()
    {
        var (session, window) = Selecting("crate", World());
        Press(window, "button_add_body");
        return (session, window);
    }

    private static (EditorSession Session, Window Window) FixedCrate()
    {
        var (session, window) = FreeCrate();
        Press(window, "choice_motion_fixed");
        return (session, window);
    }

    private static bool Says(Window window, string text) => Headless.All<TextBlock>(window).Any(one => one.Text == text);

    [Fact]
    public Task AnythingDrawnOrGroupedIsOfferedABodyAndTheWeatherIsNot() => Headless.Run(() =>
    {
        Assert.True(Has(Selecting("crate", World()).Window, "section_physics_body"));
        Assert.True(Has(Selecting("spot", World()).Window, "section_physics_body"));
        Assert.False(Has(Selecting("sky", World()).Window, "section_physics_body"));
    });

    [Fact]
    public Task AddingABodyFitsItToWhatIsDrawnAndUndoTakesItOff() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate", World());

        Press(window, "button_add_body");

        // The crate has no mesh of its own yet, so it draws the placeholder, two metres across.
        var body = BodyOf(session, "crate");
        Assert.Equal(Doc.BodyShape.Box, body.Shape);
        Assert.Equal(Vector3.All(2), body.Size);
        Assert.Equal(Vector3.Zero(), body.Centre);
        Assert.True(Has(window, "button_remove_body"));

        session.History.Undo();
        Assert.Null(PhysicsComponents.PhysicsBodyOf(session.Workspace.Loaded!.Scene!["crate"]!));
        Assert.True(Has(window, "button_add_body"));
    });

    [Fact]
    public Task APlaceInTheTreeGetsAMetreBox() => Headless.Run(() =>
    {
        var (session, window) = Selecting("spot", World());

        Press(window, "button_add_body");

        Assert.Equal(Vector3.All(1), BodyOf(session, "spot").Size);
    });

    [Fact]
    public Task ABodyIsFittedToTheSizeTheEditorReportsForAMeshItHoldsNoGeometryFor() => Headless.Run(() =>
    {
        var links = new InspectorLinks { BoundsOf = _ => (new Vector3(-1, 0, -1), new Vector3(1, 4, 1)) };
        var (session, window) = Selecting("crate", World(), links);

        Press(window, "button_add_body");

        Assert.Equal(new Vector3(2, 4, 2), BodyOf(session, "crate").Size);
        Assert.Equal(new Vector3(0, 2, 0), BodyOf(session, "crate").Centre);
    });

    [Fact]
    public Task RemovingTheBodyIsItsOwnStepAndUndoBringsItBack() => Headless.Run(() =>
    {
        var (session, window) = FreeCrate();

        Press(window, "button_remove_body");
        Assert.False(Has(window, "button_remove_body"));
        Assert.Equal("Remove body from Crate", session.History.UndoLabel);

        session.History.Undo();
        Assert.Equal(Doc.BodyShape.Box, BodyOf(session, "crate").Shape);
    });

    [Fact]
    public Task ChangingShapeKeepsTheSizeItHadForAnother() => Headless.Run(() =>
    {
        var (session, window) = FreeCrate();

        Press(window, "choice_shape_ball");

        var body = BodyOf(session, "crate");
        Assert.Equal(Doc.BodyShape.Sphere, body.Shape);
        // Written whole, so switching back is the box it was.
        Assert.Equal(Vector3.All(2), body.Size);
    });

    [Fact]
    public Task ACylinderIsFittedToWhatIsDrawn() => Headless.Run(() =>
    {
        var (session, window) = FreeCrate();

        Press(window, "choice__cylinder");
        Press(window, "button_fit_to_mesh");

        var body = BodyOf(session, "crate");
        Assert.Equal(Doc.BodyShape.Cylinder, body.Shape);
        // The placeholder is two metres across and high.
        Assert.Equal((1, 2), (body.Radius, body.Height));
        Assert.Equal("Fit Crate body to its mesh", session.History.UndoLabel);
    });

    [Fact]
    public Task AHullIsCutFromWhatIsDrawn() => Headless.Run(() =>
    {
        var (session, window) = FreeCrate();

        Press(window, "choice__hull");

        // The placeholder has no mesh to cut from, so the corners of its box.
        Assert.True(Says(window, "8 corners."));
        var hull = BodyOf(session, "crate").Hull;
        Assert.Equal(24, hull.Count);
        Assert.All(hull, value => Assert.Equal(1, Math.Abs(value)));
    });

    [Fact]
    public Task AHullWithoutAVolumeSaysWhatIsMissing() => Headless.Run(() =>
    {
        var flat = new Doc.BodyComponent(shape: Doc.BodyShape.Hull, hull: [0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 1]);
        var (_, window) = Selecting("crate", World(flat));

        Assert.True(Says(window, "Needs four points that enclose a volume. Until then the body does nothing."));
    });

    [Fact]
    public Task AMeshBodyIsFixedAndSaysHowManyTrianglesItHas() => Headless.Run(() =>
    {
        var mesh = new Doc.BodyComponent(shape: Doc.BodyShape.Mesh, motion: Doc.BodyMotion.Fixed, meshIndices: [.. Enumerable.Range(0, 36).Select(index => (long)index)]);
        var (_, window) = Selecting("crate", World(mesh));

        Assert.True(Says(window, "12 triangles. Fixed, two-sided surface."));
        Assert.False(Has(window, "button_fit_to_mesh"));
        // There is nothing to choose for one that never moves, so the row reads as fact.
        Assert.False(Has(window, "choice_motion_free"));
    });

    [Fact]
    public Task HoldingAnAxisStillAndGivingItBackAreSeparateSteps() => Headless.Run(() =>
    {
        var (session, window) = FreeCrate();

        Press(window, "toggle_lock_move_x");
        Press(window, "toggle_lock_turn_y");
        Assert.Equal([Doc.BodyLock.MoveX, Doc.BodyLock.TurnY], BodyOf(session, "crate").Locks.Order());

        session.History.Undo();
        // The rows an undo rebuilt are not laid out until a frame, and a press scrolls by where they are.
        Headless.Render();
        Assert.Equal([Doc.BodyLock.MoveX], BodyOf(session, "crate").Locks);

        Press(window, "toggle_lock_move_x");
        Assert.Empty(BodyOf(session, "crate").Locks);
    });

    [Fact]
    public Task ContinuousIsSwitchedOnAndUndone() => Headless.Run(() =>
    {
        var (session, window) = FreeCrate();
        Assert.False(BodyOf(session, "crate").Continuous);

        Press(window, "choice_continuous_on");
        Assert.True(BodyOf(session, "crate").Continuous);

        session.History.Undo();
        Assert.False(BodyOf(session, "crate").Continuous);
    });

    [Fact]
    public Task AFixedBodyIsOfferedNoMovementControlsButStillTheLayers() => Headless.Run(() =>
    {
        var (_, window) = FreeCrate();
        Assert.True(Has(window, "row_lock_move"));

        Press(window, "choice_motion_fixed");

        Assert.False(Has(window, "row_lock_move"));
        Assert.False(Has(window, "row_gravity"));
        Assert.False(Has(window, "row_continuous"));
        Assert.False(Has(window, "row_mass"));
        // What it collides with does not depend on whether it moves.
        Assert.True(Has(window, "row_is_in"));
    });

    [Fact]
    public Task AMaterialIsAPresetAndReadsAsCustomAfterAChange() => Headless.Run(() =>
    {
        var (session, window) = FreeCrate();
        Assert.Equal("Custom", ((TextBlock)Headless.Find<Button>(window, "pick_material").Content!).Text);

        var menu = Assert.IsType<MenuFlyout>(Headless.Find<Button>(window, "pick_material").Flyout);
        menu.Items.OfType<MenuItem>().First(one => one.Name == "pick_material_ice").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.Render();

        var ice = BodyOf(session, "crate");
        Assert.Equal((0.05, 0.05), (ice.Friction, ice.Restitution));
        Assert.Equal("Ice", ((TextBlock)Headless.Find<Button>(window, "pick_material").Content!).Text);

        session.History.Undo();
        Headless.Render();
        Assert.Equal("Custom", ((TextBlock)Headless.Find<Button>(window, "pick_material").Content!).Text);
    });

    [Fact]
    public Task LayersAreToggledOneAtATimeFromAGrid() => Headless.Run(() =>
    {
        var (session, window) = FreeCrate();

        // Layer one is where a body starts, and it sees every layer.
        Press(window, "layer_is_in_2");
        Press(window, "layer_sees_0");

        var body = BodyOf(session, "crate");
        Assert.Equal(1 | 4, body.Layers);
        Assert.Equal(Doc.BodyComponent.EveryLayer ^ 1, body.Cares);

        session.History.Undo();
        Assert.Equal(Doc.BodyComponent.EveryLayer, BodyOf(session, "crate").Cares);
    });

    [Fact]
    public Task AMotionOfCharacterTakesItsWalkingNumbersAndNoPlaceOrBelt() => Headless.Run(() =>
    {
        var (session, window) = FreeCrate();

        Press(window, "choice_motion_character");

        Assert.All(["row_step_height", "row_steepest", "row_skin", "row_strength", "row_crouch_height"], name => Assert.True(Has(window, name), name));
        // It is swept, not solved, and no place or belt.
        Assert.All(["row_lock_move", "row_acts_as", "row_belt_speed"], name => Assert.False(Has(window, name), name));
        Assert.Equal(Doc.BodyMotion.Character, BodyOf(session, "crate").Motion);

        session.History.Undo();
        Assert.Equal(Doc.BodyMotion.Free, BodyOf(session, "crate").Motion);
    });

    [Fact]
    public Task ATriggerIsAChoiceForABodyThatStaysPutAndNotForOneThatFalls() => Headless.Run(() =>
    {
        var (_, window) = FreeCrate();
        Assert.True(Has(window, "row_stay_events"));
        Assert.False(Has(window, "choice_acts_as_trigger"));

        Press(window, "choice_motion_fixed");

        Assert.True(Has(window, "choice_acts_as_trigger"));
        Assert.True(Has(window, "choice_acts_as_solid"));
    });

    [Fact]
    public Task ATriggerIsAPlaceAndCannotBeABelt() => Headless.Run(() =>
    {
        var (session, window) = FixedCrate();
        Assert.True(Has(window, "row_belt_speed"));

        Press(window, "choice_acts_as_trigger");

        Assert.False(Has(window, "row_belt_speed"));
        Assert.True(BodyOf(session, "crate").Trigger);

        session.History.Undo();
        Assert.False(BodyOf(session, "crate").Trigger);
    });

    [Fact]
    public Task ATriggerCanAskToHearEveryStepOfWhatIsInsideIt() => Headless.Run(() =>
    {
        var (session, window) = FixedCrate();

        Press(window, "choice_acts_as_trigger");
        Press(window, "choice_stay_events_on");

        Assert.True(BodyOf(session, "crate").Stay);
    });

    [Fact]
    public Task AZoneMakesItsBodyATriggerAndSaysSo() => Headless.Run(() =>
    {
        var (_, window) = FixedCrate();

        Press(window, "button_add_zone");

        Assert.True(Has(window, "choice_acts_as_trigger"));
        Assert.False(Has(window, "choice_acts_as_solid"));
        Assert.False(Has(window, "row_belt_speed"));
    });
}
