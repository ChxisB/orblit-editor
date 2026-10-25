using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Viewport;

// The strip of tools over the corner of the view, and the chips that make room for it. The window here
// draws no frame from the renderer, so the strip is shown by hand, as it is where the renderer runs.
public sealed class ViewportToolsTests
{
    private static ViewportRig Drawing(double width = 1440, double height = 900)
    {
        var rig = new ViewportRig(width, height);
        rig.View.Palette.IsVisible = true;
        rig.Window.UpdateLayout();
        Headless.Render();
        return rig;
    }

    private static Border Tool(ViewportRig rig, GizmoMode mode) =>
        rig.View.Palette.GetLogicalDescendants().OfType<Border>().First(one => one.Name == $"tool_{mode.ToString().ToLowerInvariant()}");

    private static bool IsOn(ViewportRig rig, GizmoMode mode) => rig.View.State.Mode == mode
        && Equals(Tool(rig, mode).Background, OrblitColors.EmberWash);

    [Fact]
    public Task EveryToolIsInTheStripStackedDownTheLeft() => Headless.Run(() =>
    {
        using var rig = Drawing();
        var move = rig.View.Palette.TranslatePoint(new Point(), rig.View)!.Value;
        var movePoint = Tool(rig, GizmoMode.Move).TranslatePoint(new Point(), rig.View)!.Value;
        var rotatePoint = Tool(rig, GizmoMode.Rotate).TranslatePoint(new Point(), rig.View)!.Value;

        Assert.Equal(movePoint.X, rotatePoint.X);
        Assert.True(rotatePoint.Y > movePoint.Y + Tool(rig, GizmoMode.Move).Bounds.Height - 1);
        Assert.True(movePoint.X < 32);
        Assert.True(movePoint.Y < 32);
        Assert.True(move.X < 32);
    });

    [Fact]
    public Task MoveIsOnToBeginWithAndATapSwitchesToRotate() => Headless.Run(() =>
    {
        using var rig = Drawing();
        Assert.True(IsOn(rig, GizmoMode.Move));
        Assert.False(IsOn(rig, GizmoMode.Rotate));

        rig.Click(rig.Where(Tool(rig, GizmoMode.Rotate)));
        Headless.Render();

        Assert.False(IsOn(rig, GizmoMode.Move));
        Assert.True(IsOn(rig, GizmoMode.Rotate));
    });

    [Fact]
    public Task EachToolSaysWhatItIs() => Headless.Run(() =>
    {
        using var rig = Drawing();

        Assert.Equal("Move", ToolTip.GetTip(Tool(rig, GizmoMode.Move)));
        Assert.Equal("Rotate", ToolTip.GetTip(Tool(rig, GizmoMode.Rotate)));
    });

    [Fact]
    public Task TheChipsStartToTheRightOfTheStrip() => Headless.Run(() =>
    {
        using var rig = Drawing();
        var strip = rig.View.Palette.TranslatePoint(new Point(rig.View.Palette.Bounds.Width, 0), rig.Window)!.Value;
        var chip = rig.Chip("Perspective").TranslatePoint(new Point(), rig.Window)!.Value;

        Assert.True(chip.X > strip.X);
    });

    [Fact]
    public Task ThereIsNoStripWhereThereIsNoRenderer() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();

        Assert.False(rig.View.Palette.IsVisible);
        Assert.False(rig.View.Palette.IsEffectivelyVisible);
    });

    [Fact]
    public Task TheSmallestWindowFitsTheStripAndTheChips() => Headless.Run(() =>
    {
        using var rig = Drawing(960, 620);
        var chip = rig.Chip("Perspective");
        var right = chip.TranslatePoint(new Point(chip.Bounds.Width, 0), rig.View)!.Value.X;

        Assert.True(rig.View.Palette.IsEffectivelyVisible);
        Assert.True(right < rig.View.Bounds.Width);
        Assert.True(rig.View.Palette.Bounds.Bottom < rig.View.Bounds.Height);
    });
}
