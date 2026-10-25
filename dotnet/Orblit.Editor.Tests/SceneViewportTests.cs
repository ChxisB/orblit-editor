using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;

// What the control does with a pointer and a window, which the state alone cannot say. Added tests,
// the Dart view has no counterpart.
public sealed class SceneViewportTests
{
    private static SceneViewport[] Views(ViewportRig rig) => [.. rig.Window.GetLogicalDescendants().OfType<SceneViewport>()];

    [Fact]
    public Task DraggingTheViewOrbitsTheCamera() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var from = rig.Centre;
        var yaw = rig.View.State.Camera.Yaw;

        rig.Window.MouseDown(from, MouseButton.Left);
        rig.Window.MouseMove(from + new Avalonia.Vector(60, 0), RawInputModifiers.LeftMouseButton);
        rig.Window.MouseUp(from + new Avalonia.Vector(60, 0), MouseButton.Left);

        Assert.NotEqual(yaw, rig.View.State.Camera.Yaw);
    });

    [Fact]
    public Task AShortMoveIsStillATapAndLeavesTheCameraAlone() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var from = rig.Centre;
        var yaw = rig.View.State.Camera.Yaw;
        var picked = 0;
        rig.View.State.Picked += (_, _) => picked++;

        rig.Window.MouseDown(from, MouseButton.Left);
        rig.Window.MouseMove(from + new Avalonia.Vector(2, 0), RawInputModifiers.LeftMouseButton);
        rig.Window.MouseUp(from + new Avalonia.Vector(2, 0), MouseButton.Left);

        Assert.Equal(1, picked);
        Assert.Equal(yaw, rig.View.State.Camera.Yaw);
    });

    [Fact]
    public Task TheSurfaceFollowsAResizeOfTheWindow() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var before = rig.View.State.Surface!.Value;

        rig.Window.Width = 1000;
        rig.Window.Height = 700;
        rig.Window.UpdateLayout();
        Headless.Render();
        var after = rig.View.State.Surface!.Value;

        Assert.True(before.Width > 0);
        Assert.True(after.Width < before.Width);
        Assert.True(after.Height < before.Height);
    });

    [Fact]
    public Task TheSnapChipSaysTheStepAndATapSwitchesItOff() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        Assert.True(rig.Says("Snap: "));

        rig.Click(rig.Where(rig.Chip(rig.Words().First(one => one.StartsWith("Snap: ", StringComparison.Ordinal)))));

        Assert.False(rig.View.State.Snapping.On);
        Assert.True(rig.Says("Snap off"));
    });

    [Fact]
    public Task AChangeThatLeavesTheLayoutAloneKeepsTheSameView() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var view = rig.View;

        rig.Session.ToggleStats();
        Headless.Render();

        Assert.Same(view, Assert.Single(Views(rig)));
    });

    [Fact]
    public Task AChangeOfLayoutHandsTheSameViewBack() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var view = rig.View;
        var id = rig.Session.Layout.Panels.First(panel => panel.Kind != PanelKind.Viewport).Id;

        rig.Window.GetLogicalDescendants().OfType<Button>().First(one => one.Name == $"close_{id}")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Headless.Render();

        Assert.DoesNotContain(rig.Session.Layout.Panels, panel => panel.Id == id);
        Assert.Same(view, Assert.Single(Views(rig)));
        Assert.True(view.IsEffectivelyVisible);
    });
}
