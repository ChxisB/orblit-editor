using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;
using Orblit.Mesh;
using Doc = Orblit.Scene;
using static Orblit.Editor.Tests.InspectorRig;

/// <summary>The parts of a compound body and the collision button, as a person meets them in the inspector.</summary>
public sealed class InspectorCompoundTests
{
    private static EditorScene World() =>
        new(
        [
            new SceneObject("crate", "Crate", ObjectKind.Mesh),
            new SceneObject("cube", "Cube", ObjectKind.Shape, shape: Shape.Of(ShapeKind.Cube)),
            new SceneObject("spot", "Spot", ObjectKind.Group),
        ]);

    private static InspectorLinks Reading(Func<SceneObject, Task<CollisionMesh>>? read = null) =>
        new() { CollisionOf = read ?? new ModelCollision("/project").ReadAsync };

    // What a file written now and opened again holds for the object.
    private static Doc.BodyComponent Saved(EditorSession session, string id) =>
        PhysicsComponents.PhysicsBodyOf(SceneDocument.Decode(SceneDocument.Encode(session.Workspace.Loaded!.Scene!)).Scene[id]!)!;

    private static (EditorSession Session, Window Window) CompoundCrate()
    {
        var (session, window) = Selecting("crate", World());
        Press(window, "button_add_body");
        Press(window, "choice__compound");
        return (session, window);
    }

    [Fact]
    public Task CompoundPartsSurviveSaveRemovalAndUndo() => Headless.Run(() =>
    {
        var (session, window) = CompoundCrate();
        Assert.Single(Saved(session, "crate").Parts);

        Press(window, "button_add_part");
        Assert.Equal(2, Saved(session, "crate").Parts.Count);

        Press(window, "button_remove_part_2");
        Assert.Single(Saved(session, "crate").Parts);

        session.History.Undo();
        Assert.Equal(2, Saved(session, "crate").Parts.Count);
    });

    [Fact]
    public Task EachPartHasItsOwnShapeAndRowsForIt() => Headless.Run(() =>
    {
        var (session, window) = CompoundCrate();

        Assert.True(Has(window, "choice_part_1_box"));
        Assert.True(Has(window, "drag_part_size_0"));
        Assert.False(Has(window, "drag_part_radius_0"));

        Press(window, "choice_part_1_ball");

        Assert.Equal(Doc.BodyShape.Sphere, Saved(session, "crate").Parts[0].Shape);
        Assert.True(Has(window, "drag_part_radius_0"));
        Assert.False(Has(window, "drag_part_size_0"));
    });

    [Fact]
    public Task APartThatBecomesAHullStartsWithTheCornersOfTheBodysBox() => Headless.Run(() =>
    {
        var (session, window) = CompoundCrate();

        // The body's own shape row has a hull too, above the part's.
        PressLast(window, "choice__hull");

        // Two metres across, so every corner is a metre from the middle.
        var hull = Saved(session, "crate").Parts[0].Hull;
        Assert.Equal(24, hull.Count);
        Assert.All(hull, value => Assert.Equal(1, Math.Abs(value)));
    });

    [Fact]
    public Task DraggingAPartsCentreIsOneUndoStep() => Headless.Run(() =>
    {
        var (session, window) = CompoundCrate();
        var before = Saved(session, "crate").Parts[0].Centre;

        Drag(window, "drag_part_centre_0", 40);
        Assert.NotEqual(before, Saved(session, "crate").Parts[0].Centre);

        session.History.Undo();
        Assert.Equal(before, Saved(session, "crate").Parts[0].Centre);
    });

    [Fact]
    public Task AddPartStopsAtTheMostPartsABodyKeeps() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate", World());
        var full = new Doc.BodyComponent(shape: Doc.BodyShape.Compound, parts: [.. Enumerable.Repeat(new Doc.BodyPart(), 64)]);
        session.History.Run(new SetObjectComponent(session.Workspace.Loaded!.Id, "crate", "Give crate a body", Doc.SceneComponents.Body, null, full));
        Headless.Render();

        Assert.False(Headless.Find<Button>(window, "button_add_part").IsEnabled);
    });

    [Fact]
    public Task ModelTrianglesSurviveSaveRemovalAndUndo() => Headless.Run(() =>
    {
        var (session, window) = Selecting("cube", World(), Reading());

        Press(window, "button_collide_with_this_model");

        var body = Saved(session, "cube");
        Assert.Equal(Doc.BodyShape.Mesh, body.Shape);
        Assert.Equal(Doc.BodyMotion.Fixed, body.Motion);
        Assert.Equal(36, body.MeshIndices.Count);

        Headless.Render();
        Press(window, "button_remove_body");
        session.History.Undo();
        Assert.Equal(body.MeshIndices, Saved(session, "cube").MeshIndices);
    });

    [Fact]
    public Task TheColliderIsAStepOfItsOwnNamedForTheObject() => Headless.Run(() =>
    {
        var (session, window) = Selecting("cube", World(), Reading());

        Press(window, "button_collide_with_this_model");

        Assert.Equal("Collide with Cube", session.History.UndoLabel);
    });

    [Fact]
    public Task AnObjectThatAlreadyHasABodyCanBeGivenItsModelToo() => Headless.Run(() =>
    {
        var (session, window) = Selecting("cube", World(), Reading());
        Press(window, "button_add_body");

        Press(window, "button_collide_with_this_model");

        Assert.Equal(Doc.BodyShape.Mesh, Saved(session, "cube").Shape);
        Assert.True(Has(window, "button_remove_body"));
    });

    [Fact]
    public Task TheButtonIsLeftOutWithNoWayToReadAModelOrNothingDrawn() => Headless.Run(() =>
    {
        Assert.False(Has(Selecting("cube", World()).Window, "button_collide_with_this_model"));
        Assert.False(Has(Selecting("spot", World(), Reading()).Window, "button_collide_with_this_model"));
        Assert.True(Has(Selecting("cube", World(), Reading()).Window, "button_collide_with_this_model"));
    });

    [Fact]
    public Task AModelThatCannotBeReadSaysWhyAndChangesNothing() => Headless.Run(() =>
    {
        var (session, window) = Selecting("cube", World(), Reading(_ => Task.FromException<CollisionMesh>(new FormatException("not a model"))));

        Press(window, "button_collide_with_this_model");

        Assert.Equal("Could not make this collider: not a model", TextOf(window, "collision_problem"));
        Assert.Null(PhysicsComponents.PhysicsBodyOf(session.Workspace.Loaded!.Scene!["cube"]!));
        Assert.True(Headless.Find<Button>(window, "button_collide_with_this_model").IsEnabled);
    });
}
