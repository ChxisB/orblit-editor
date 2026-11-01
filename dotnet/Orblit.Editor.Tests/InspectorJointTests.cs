using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Doc = Orblit.Scene;
using static Orblit.Editor.Tests.InspectorRig;

/// <summary>A joint put on from the inspector, as a person meets it.</summary>
public sealed class InspectorJointTests
{
    private static EditorScene World() => new([new SceneObject("crate", "Crate", ObjectKind.Mesh)]);

    // What a file written now and opened again holds for the object.
    private static Doc.JointComponent? Saved(EditorSession session, string id) =>
        PhysicsComponents.JointOf(SceneDocument.Decode(SceneDocument.Encode(session.Workspace.Loaded!.Scene!)).Scene[id]!);

    private static (EditorSession Session, Window Window) JointedCrate()
    {
        var (session, window) = Selecting("crate", World());
        Press(window, "button_add_body");
        Press(window, "button_add_joint");
        return (session, window);
    }

    private static void Pick(Window window, string menu, string item)
    {
        var flyout = Assert.IsType<MenuFlyout>(Headless.Find<Button>(window, menu).Flyout);
        flyout.Items.OfType<MenuItem>().First(one => one.Name == $"{menu}_{item}").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.Render();
    }

    private static bool Says(Window window, string text) => Headless.All<TextBlock>(window).Any(one => one.Text == text);

    [Fact]
    public Task AJointIsOfferedOnlyWhereThereIsABodyToHold() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate", World());
        Assert.False(Has(window, "button_add_joint"));

        Press(window, "button_add_body");
        Press(window, "button_add_joint");

        Assert.True(Has(window, "button_remove_joint"));
        Assert.True(Says(window, "Crate to the world"));
        Assert.Equal(Doc.JointKind.Hinge, Saved(session, "crate")!.Kind);
    });

    [Fact]
    public Task AJointStillShowsWhereAFileGaveOneToSomethingWithNoBody() => Headless.Run(() =>
    {
        var lamp = new SceneObject(
            "lamp",
            "Lamp",
            ObjectKind.Group,
            components: new() { [Doc.SceneComponents.Joint] = new Doc.JointComponent() });

        var (_, window) = Selecting("lamp", new EditorScene([lamp]));

        Assert.True(Has(window, "button_remove_joint"));
        Assert.True(Says(window, "Nothing: no body at or above it"));
    });

    [Fact]
    public void TheSectionIsOfferedWhereABodyIsHeldAndNotWhereNothingIs()
    {
        var scene = JointTests.Scene();

        Assert.True(JointSection.AppliesTo(InspectorModelTests.TargetFor(scene, "door")));
        Assert.True(JointSection.AppliesTo(InspectorModelTests.TargetFor(scene, "hinge")));
        Assert.False(JointSection.AppliesTo(InspectorModelTests.TargetFor(scene, "lamp")));
    }

    [Fact]
    public void TheSectionIsStillOfferedForAJointAFileGaveToSomethingThatHoldsNothing()
    {
        var scene = JointTests.Scene();
        scene["lamp"]!.Components[Doc.SceneComponents.Joint] = new Doc.JointComponent();

        Assert.True(JointSection.AppliesTo(InspectorModelTests.TargetFor(scene, "lamp")));
    }

    [Fact]
    public void AddingAJointIsUndoneByTakingItOffAgain()
    {
        var scene = JointTests.Scene();
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);

        history.Run(new SetObjectComponent("a", "hinge", "Add joint", Doc.SceneComponents.Joint, null, new Doc.JointComponent()));
        Assert.NotNull(PhysicsComponents.JointOf(scene["hinge"]!));

        history.Undo();
        Assert.Null(PhysicsComponents.JointOf(scene["hinge"]!));
    }

    [Fact]
    public Task TheKindIsChosenFromAMenuAndNamesWhatItsLimitsRead() => Headless.Run(() =>
    {
        var (session, window) = JointedCrate();
        Assert.True(Has(window, "choice_turn_free"));

        Pick(window, "pick_kind", "slider");
        Assert.True(Has(window, "choice_travel_free"));
        Assert.False(Has(window, "choice_turn_free"));

        Press(window, "choice_travel_range");

        var joint = Saved(session, "crate")!;
        Assert.Equal(Doc.JointKind.Slider, joint.Kind);
        Assert.Equal(new Doc.JointRange(-0.5, 0.5), joint.Limits[Doc.JointAxis.AlongX]);
    });

    [Fact]
    public Task APulleyAndAPathAreGivenPointsToStartFromAndAPathGrows() => Headless.Run(() =>
    {
        var (session, window) = JointedCrate();

        Pick(window, "pick_kind", "pulley");
        Assert.True(Has(window, "drag_over_1_0"));
        Assert.True(Has(window, "drag_over_2_0"));

        Pick(window, "pick_kind", "path");
        Assert.True(Has(window, "drag_point_1_0"));
        Assert.True(Has(window, "drag_point_2_0"));
        Assert.False(Has(window, "drag_point_3_0"));

        Press(window, "button_add_point");
        Assert.True(Has(window, "drag_point_3_0"));

        var joint = Saved(session, "crate")!;
        Assert.Equal(Doc.JointKind.Path, joint.Kind);
        Assert.Equal(2, joint.Over.Count);
        Assert.Equal(3, joint.Path.Count);
        Assert.Equal(2, joint.Path[^1].X);
    });

    [Fact]
    public Task APathKeepsTwoPoints() => Headless.Run(() =>
    {
        var (session, window) = JointedCrate();
        Pick(window, "pick_kind", "path");
        Press(window, "button_add_point");
        Press(window, "button_remove_point");
        Assert.Equal(2, Saved(session, "crate")!.Path.Count);

        Assert.False(Headless.Find<Button>(window, "button_remove_point").IsEnabled);
    });

    [Fact]
    public Task ASpringAndAPlaceToHoldAreSwitchedOnFromRows() => Headless.Run(() =>
    {
        var (session, window) = JointedCrate();
        Assert.False(Has(window, "drag_stiffness_0"));

        Press(window, "choice_spring_on");
        Assert.True(Has(window, "drag_stiffness_0"));

        Press(window, "choice_drive_place");
        Assert.True(Has(window, "row_hold_at"));
        Assert.False(Has(window, "row_motor"));

        var joint = Saved(session, "crate")!;
        Assert.True(joint.Springs[Doc.JointAxis.AboutX].Hertz > 0);
        Assert.True(joint.GoalHertz > 0);
        Assert.True(joint.Strength > 0);
    });

    [Fact]
    public Task AJointIsTakenOffAgainByUndoAndByItsOwnButton() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate", World());
        Press(window, "button_add_body");

        Press(window, "button_add_joint");
        session.History.Undo();
        Headless.Render();
        Assert.False(Has(window, "button_remove_joint"));
        Assert.Null(Saved(session, "crate"));

        Press(window, "button_add_joint");
        Press(window, "button_remove_joint");
        Assert.Null(Saved(session, "crate"));
        Assert.True(Has(window, "button_add_joint"));
    });
}
