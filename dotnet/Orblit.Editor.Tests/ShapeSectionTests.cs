using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

public sealed class ShapeSectionTests
{
    private static bool Has(Control root, string name) => InspectorRig.Has(root, name);

    private static SceneObject Made(ShapeKind kind) =>
        new(id: "made", name: "Made", kind: ObjectKind.Shape, shape: Shape.Of(kind));

    private static SceneObject Drawn(bool edited = false) => new(
        id: "drawn",
        name: "Drawn",
        kind: ObjectKind.Shape,
        outline: new PolyShape([new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(0, 0, 2)], height: 2),
        geometry: edited ? Shape.Of(ShapeKind.Cube).Build() : null);

    private static (EditorSession Session, Window Window, SceneObject Item) Open(SceneObject item)
    {
        var (session, window) = InspectorRig.Open(EditorScene.Starter());
        session.History.Run(new AddObject(item, "main"));
        session.Select([item.Id]);
        Headless.Render();
        return (session, window, item);
    }

    private static void Set(Window window, string slider, double value) =>
        Headless.Find<Slider>(window, slider).Value = value;

    [Fact]
    public void EveryKindsRowsReadBackWhatTheyWrite()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var shape = Shape.Of(kind);
            foreach (var row in ShapeParameters.For(shape))
            {
                var same = row switch
                {
                    ShapeNumber number => number.Write(shape, number.Read(shape)),
                    ShapeFlag flag => flag.Write(shape, flag.Read(shape)),
                    _ => throw new InvalidOperationException(),
                };

                Assert.Equal(shape, same);
            }
        }
    }

    [Fact]
    public void EveryNumberStartsInsideItsRange()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var shape = Shape.Of(kind);
            foreach (var number in ShapeParameters.For(shape).OfType<ShapeNumber>())
            {
                Assert.InRange(number.Read(shape), number.Options.Min, number.Options.Max);
            }
        }
    }

    [Fact]
    public Task ACubeHasASizeAndNothingElseOfItsOwn() => Headless.Run(() =>
    {
        var (_, window, _) = Open(Made(ShapeKind.Cube));

        Assert.True(Has(window, "section_cube"));
        Assert.True(Has(window, "slider_width"));
        Assert.True(Has(window, "slider_depth"));
        Assert.False(Has(window, "slider_sides"));
        window.Close();
    });

    [Fact]
    public Task ACylinderAddsItsSidesAndFlags() => Headless.Run(() =>
    {
        var (session, window, item) = Open(Made(ShapeKind.Cylinder));

        Set(window, "slider_sides", 12.4);
        Assert.Equal(12, item.Shape!.Sides);

        InspectorRig.Press(window, "choice_ends_off");
        Assert.False(item.Shape.Capped);
        Assert.StartsWith("Change Made", session.History.UndoLabel);
        window.Close();
    });

    [Fact]
    public Task ADraggedSizeIsOneStepToUndo() => Headless.Run(() =>
    {
        var (session, window, item) = Open(Made(ShapeKind.Cube));

        InspectorRig.Drag(window, "slider_width", 40);

        Assert.NotEqual(1, item.Shape!.Width);
        session.History.Undo();
        Assert.Equal(1, item.Shape.Width);
        window.Close();
    });

    [Fact]
    public Task AStaircaseShowsTheFieldItsChoiceMakesTrue() => Headless.Run(() =>
    {
        var (_, window, item) = Open(Made(ShapeKind.Stairs));

        Assert.True(Has(window, "slider_steps"));
        Assert.False(Has(window, "slider_step_height"));

        InspectorRig.Press(window, "choice_steps_by_height");

        Assert.False(item.Shape!.ByCount);
        Assert.True(Has(window, "slider_step_height"));
        Assert.False(Has(window, "slider_steps"));
        window.Close();
    });

    [Fact]
    public Task ARowOnAShapeThatWasEditedIsShownAndDoesNothing() => Headless.Run(() =>
    {
        var edited = new SceneObject(
            id: "made",
            name: "Made",
            kind: ObjectKind.Shape,
            shape: Shape.Of(ShapeKind.Cube),
            geometry: Shape.Of(ShapeKind.Cube).Build());
        var (_, window, _) = Open(edited);

        Assert.True(Has(window, "shape_edited"));
        Assert.False(Headless.Find<Slider>(window, "slider_width").IsEffectivelyEnabled);
        window.Close();
    });

    [Fact]
    public Task ADrawnShapeSaysHowManyCornersAndChangesItsHeightAsOneStep() => Headless.Run(() =>
    {
        var (session, window, item) = Open(Drawn());

        Assert.Equal("3 corners", InspectorRig.TextOf(window, "shape_corners"));
        Assert.False(Has(window, "slider_width"));

        InspectorRig.Drag(window, "slider_height", 40);

        Assert.NotEqual(2, item.Outline!.Height);
        Assert.StartsWith("Reshape Drawn", session.History.UndoLabel);
        session.History.Undo();
        Assert.Equal(2, item.Outline.Height);
        window.Close();
    });

    [Fact]
    public Task ADrawnShapeTurnsOver() => Headless.Run(() =>
    {
        var (session, window, item) = Open(Drawn());

        InspectorRig.Press(window, "button_turn_it_over");
        Assert.True(item.Outline!.Flipped);

        session.History.Undo();
        Assert.False(item.Outline.Flipped);
        window.Close();
    });

    [Fact]
    public Task ADrawnShapeThatWasEditedHasNoOutlineFields() => Headless.Run(() =>
    {
        var (_, window, _) = Open(Drawn(edited: true));

        Assert.True(Has(window, "shape_edited"));
        Assert.False(Has(window, "slider_height"));
        Assert.False(Has(window, "button_turn_it_over"));
        window.Close();
    });

    [Fact]
    public Task TheBoundaryIsChosenAndSizedFromTheShape() => Headless.Run(() =>
    {
        var (session, window, item) = Open(Made(ShapeKind.Cube));
        Assert.Equal("1.00 × 1.00 × 1.00 m", InspectorRig.TextOf(window, "boundary_size"));

        InspectorRig.Press(window, "choice_shape_box");
        Assert.Equal(BoundaryKind.Box, item.Boundary.Kind);
        Assert.StartsWith("Boundary of Made", session.History.UndoLabel);

        Set(window, "slider_padding", 0.5);
        Headless.Render();
        Assert.Equal("2.00 × 2.00 × 2.00 m", InspectorRig.TextOf(window, "boundary_size"));
        window.Close();
    });

    [Fact]
    public Task AnOffsetKeepsTheOtherTwoAxes() => Headless.Run(() =>
    {
        var (_, window, item) = Open(Made(ShapeKind.Cube));

        Set(window, "slider_offset_x", 2);
        Set(window, "slider_offset_z", -1);

        Assert.Equal(2, item.Boundary.Offset.X);
        Assert.Equal(0, item.Boundary.Offset.Y);
        Assert.Equal(-1, item.Boundary.Offset.Z);
        window.Close();
    });

    [Fact]
    public Task ADraggedPaddingIsOneStepToUndo() => Headless.Run(() =>
    {
        var (session, window, item) = Open(Made(ShapeKind.Cube));

        InspectorRig.Drag(window, "slider_padding", 40);

        Assert.NotEqual(0, item.Boundary.Padding);
        session.History.Undo();
        Assert.Equal(0, item.Boundary.Padding);
        window.Close();
    });

    [Fact]
    public Task NoBoundaryHasNothingToSize() => Headless.Run(() =>
    {
        var (_, window, _) = Open(Made(ShapeKind.Cube));

        InspectorRig.Press(window, "choice_shape_none");

        Assert.False(Has(window, "slider_padding"));
        Assert.False(Has(window, "boundary_size"));
        window.Close();
    });

    [Fact]
    public Task ABoundaryOfMeshWithNoGeometrySaysItUsesTheBox() => Headless.Run(() =>
    {
        var (_, window, _) = Open(new SceneObject(id: "bare", name: "Bare", kind: ObjectKind.Shape));

        Assert.StartsWith("No geometry here", InspectorRig.TextOf(window, "boundary_hint"));
        window.Close();
    });

    [Fact]
    public Task GoToModellingEntersTheModellingMode() => Headless.Run(() =>
    {
        var (session, window, _) = Open(Made(ShapeKind.Cube));

        InspectorRig.Press(window, "button_go_to_modelling");

        Assert.Equal("modelling", session.Mode.Name);
        window.Close();
    });

    [Fact]
    public Task AnObjectThatIsNotAShapeHasNoShapeSection() => Headless.Run(() =>
    {
        var (_, window, _) = Open(new SceneObject(id: "plain", name: "Plain", kind: ObjectKind.Mesh));

        Assert.False(Has(window, "section_boundary"));
        Assert.False(Has(window, "button_go_to_modelling"));
        window.Close();
    });
}
