using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;

// Ported from nudge_test.dart and the G key cases of editor_shell_geometry_test.dart. The distance is the
// window's own grid step, so these press the keys. NudgeTests holds what the session does with it.
public sealed class EditorWindowKeysTests
{
    // The click gives the keyboard somewhere to be. With nothing focused the toolkit spends the first arrow
    // moving focus into the window, which is its keyboard navigation and not something to take over.
    private static ViewportRig Choosing(string id)
    {
        var rig = new ViewportRig();
        rig.Click(rig.Where(new Point(5, 5)));
        rig.Session.SelectObject(id);
        Headless.Render();
        return rig;
    }

    private static Vector3 Where(ViewportRig rig, string id = "ground") => rig.Session.Workspace.Loaded!.Scene![id]!.Position;

    private static void Press(ViewportRig rig, PhysicalKey key, RawInputModifiers held = RawInputModifiers.None)
    {
        rig.Window.KeyPressQwerty(key, held);
        rig.Window.KeyReleaseQwerty(key, held);
    }

    [Fact]
    public Task AnArrowMovesOneSquareAlongTheFloor() => Headless.Run(() =>
    {
        using var rig = Choosing("ground");
        var start = Where(rig).Clone();
        var step = rig.Window.Snapping.Step;

        Press(rig, PhysicalKey.ArrowRight);
        Assert.Equal(start.X + step, Where(rig).X, 9);
        Assert.Equal(start.Z, Where(rig).Z);

        Press(rig, PhysicalKey.ArrowDown);
        Assert.Equal(start.Z + step, Where(rig).Z, 9);
    });

    [Fact]
    public Task ShiftTakesItUpAndDownInstead() => Headless.Run(() =>
    {
        using var rig = Choosing("ground");
        var start = Where(rig).Clone();
        var step = rig.Window.Snapping.Step;

        Press(rig, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
        Assert.Equal(start.Y + step, Where(rig).Y, 9);
        Assert.Equal(start.Z, Where(rig).Z);

        Press(rig, PhysicalKey.ArrowDown, RawInputModifiers.Shift);
        Assert.Equal(start.Y, Where(rig).Y, 9);
    });

    [Fact]
    public Task OptionDoesTenAtATime() => Headless.Run(() =>
    {
        using var rig = Choosing("ground");
        var start = Where(rig).X;
        var step = rig.Window.Snapping.Step;

        Press(rig, PhysicalKey.ArrowRight, RawInputModifiers.Alt);

        Assert.Equal(start + (step * 10), Where(rig).X, 9);
    });

    [Fact]
    public Task EachPressIsItsOwnStepToUndo() => Headless.Run(() =>
    {
        using var rig = Choosing("ground");
        var start = Where(rig).X;
        var step = rig.Window.Snapping.Step;

        Press(rig, PhysicalKey.ArrowRight);
        Press(rig, PhysicalKey.ArrowRight);
        Assert.Equal(start + (step * 2), Where(rig).X, 9);
        rig.Session.Undo();

        Assert.Equal(start + step, Where(rig).X, 9);
    });

    [Fact]
    public Task NothingSelectedNothingMoves() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var before = rig.Session.History.UndoLabel;

        Press(rig, PhysicalKey.ArrowRight);

        Assert.Equal(before, rig.Session.History.UndoLabel);
    });

    [Fact]
    public Task ItMovesBySquaresEvenWithTheGridSwitchedOff() => Headless.Run(() =>
    {
        using var rig = Choosing("ground");
        var start = Where(rig).X;
        var step = rig.Window.Snapping.Step;
        rig.Window.Snapping.On = false;

        // An arrow key asks for a definite amount, and the one on offer is a square whether or not drags snap.
        Press(rig, PhysicalKey.ArrowRight);

        Assert.Equal(start + step, Where(rig).X, 9);
    });

    [Fact]
    public Task TheBracketKeysChangeTheGridAndSwitchItOn() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var snapping = rig.Window.Snapping;
        var step = snapping.Step;
        snapping.On = false;

        Press(rig, PhysicalKey.BracketRight);
        Assert.Equal(Coarser(step), snapping.Step);
        Assert.True(snapping.On);

        snapping.On = false;
        Press(rig, PhysicalKey.BracketLeft);
        Assert.Equal(step, snapping.Step);
        Assert.True(snapping.On);
    });

    private static double Coarser(double step) => new Snapping(step: step).Coarser;

    [Fact]
    public Task GGoesIntoTheGeometryAndThenRoundTheModes() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Session.AddShape(ShapeKind.Cube);
        Headless.Render();
        var mode = rig.Session.ElementMode;

        Press(rig, PhysicalKey.G);
        Assert.Equal(EditContext.Element, rig.Session.Context);
        Assert.Equal(mode, rig.Session.ElementMode);

        Press(rig, PhysicalKey.G);
        Assert.Equal(mode.Next(), rig.Session.ElementMode);
    });

    [Fact]
    public Task EscapeBacksOutToTheObject() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Session.AddShape(ShapeKind.Cube);
        Press(rig, PhysicalKey.G);
        Assert.Equal(EditContext.Element, rig.Session.Context);

        Press(rig, PhysicalKey.Escape);

        Assert.Equal(EditContext.Whole, rig.Session.Context);
    });

    [Fact]
    public Task TheseKeysAreLeftToATextBoxThatHasTheKeyboard() => Headless.Run(() =>
    {
        using var rig = Choosing("ground");
        var field = rig.Window.GetVisualDescendants().OfType<TextBox>().First();
        field.Focus();
        var before = Where(rig).X;
        var mode = rig.Session.Context;

        // Typing a g into a name must not go into the geometry, nor a bracket change the grid.
        Press(rig, PhysicalKey.G);
        Press(rig, PhysicalKey.BracketRight);
        Press(rig, PhysicalKey.ArrowRight, RawInputModifiers.Alt);

        Assert.Equal(mode, rig.Session.Context);
        Assert.Equal(before, Where(rig).X);
        Assert.Equal(new Snapping().Step, rig.Window.Snapping.Step);
    });
}
