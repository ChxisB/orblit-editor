using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Viewport;

// The Dart file drives the viewport from the shell. The three camera preview tests need the outliner,
// the inspector and the preview window, which are task 16, so they are not here. The same rule is
// tested on the state instead, in SceneViewportStateTests.
public sealed class EditorShellViewportTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(100);

    [Fact]
    public Task HoldingTheRightButtonSaysItIsFlying() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        Assert.False(rig.Says("Flying"));

        rig.Look();

        Assert.True(rig.Says("Flying"));
        Assert.True(rig.Says("m/s"));
    });

    [Fact]
    public Task LettingGoStops() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Look();

        rig.LetGo();

        Assert.False(rig.Says("Flying"));
    });

    [Fact]
    public Task WMovesTheViewForwardWhileTheButtonIsHeld() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Look();
        var was = rig.Eye.Clone();

        rig.Press(PhysicalKey.W);
        rig.View.State.Tick(Frame);
        rig.View.State.Tick(Frame * 2);
        rig.Release(PhysicalKey.W);
        var moved = (rig.Eye - was).Length;
        rig.LetGo();

        Assert.True(moved > 0.1);
        Assert.False(rig.Says("Flying"));
    });

    [Fact]
    public Task WDoesNothingWhenTheButtonIsNotHeld() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var was = rig.Eye.Clone();

        // Otherwise typing a name into the inspector would fly the view across the level a letter at a time.
        rig.Press(PhysicalKey.W);
        rig.View.State.Tick(Frame);
        rig.Release(PhysicalKey.W);

        Assert.Equal(0, (rig.Eye - was).Length);
        Assert.False(rig.Says("Flying"));
    });

    [Fact]
    public Task ItCanBeSwitchedOnRatherThanHeld() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Click(rig.Centre);

        rig.Tap(PhysicalKey.Backquote);

        // No button held: a two-finger click held down while the other hand types WASD is a hand
        // position nobody keeps for long.
        Assert.True(rig.Says("Flying"));
    });

    [Fact]
    public Task EscapeStopsIt() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Click(rig.Centre);
        rig.Tap(PhysicalKey.Backquote);

        rig.Tap(PhysicalKey.Escape);

        Assert.False(rig.Says("Flying"));
    });

    [Fact]
    public Task AndSoDoesPressingTheKeyAgain() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Click(rig.Centre);

        rig.Tap(PhysicalKey.Backquote);
        rig.Tap(PhysicalKey.Backquote);

        Assert.False(rig.Says("Flying"));
    });

    [Fact]
    public Task WMovesWhileItIsSwitchedOnWithNothingHeld() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Click(rig.Centre);
        rig.Tap(PhysicalKey.Backquote);
        var was = rig.Eye.Clone();

        rig.Press(PhysicalKey.W);
        rig.View.State.Tick(Frame);
        rig.View.State.Tick(Frame * 2);
        rig.Release(PhysicalKey.W);
        var moved = (rig.Eye - was).Length;

        Assert.True(moved > 0.1);
        Assert.True(rig.Says("Flying"));
    });

    [Fact]
    public Task TheBackquoteDoesNothingUntilAViewIsClickedIn() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();

        // Otherwise typing a backquote into a name would launch the view.
        rig.Tap(PhysicalKey.Backquote);

        Assert.False(rig.Says("Flying"));
    });

    [Fact]
    public Task TheWheelSetsTheSpeedRatherThanTheDistance() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Look();
        var before = Speed(rig);
        var distance = rig.View.State.Camera.Distance;

        rig.Window.MouseWheel(rig.Centre, new Vector(0, 1));

        Assert.NotEqual(before, Speed(rig));
        Assert.Equal(distance, rig.View.State.Camera.Distance);
    });

    // Two fingers on a trackpad reach the control as a pan and a zoom. Avalonia gives no phases for a
    // two finger scroll, so the state is sent them as the control would on a pinch.
    [Fact]
    public Task TwoFingersOrbit() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var was = rig.Eye.Clone();

        TwoFingers(rig, new Offset(80, 0), 1, shifted: false);

        // Moved around what it was looking at, rather than towards it.
        Assert.True((rig.Eye - was).Length > 0.5);
        Assert.InRange(rig.Eye.Length, was.Length - 0.5, was.Length + 0.5);
    });

    [Fact]
    public Task ShiftAndTwoFingersPan() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var wasAt = rig.View.State.Camera.Target.Clone();

        TwoFingers(rig, new Offset(80, 0), 1, shifted: true);

        Assert.True((rig.View.State.Camera.Target - wasAt).Length > 0.05);
    });

    [Fact]
    public Task PinchingZooms() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var was = rig.View.State.Camera.Distance;

        TwoFingers(rig, Offset.Zero, 1.4, shifted: false);

        Assert.True(rig.View.State.Camera.Distance < was);
    });

    [Fact]
    public Task TwoFingersSteerWhileFlyingWithNothingHeld() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Click(rig.Centre);
        rig.Tap(PhysicalKey.Backquote);
        var was = rig.Eye.Clone();

        TwoFingers(rig, new Offset(80, 0), 1, shifted: false);

        Assert.InRange((rig.Eye - was).Length, 0, 1e-6);
    });

    private static string Speed(ViewportRig rig) =>
        rig.Words().First(one => one.Contains("m/s", StringComparison.Ordinal));

    private static void TwoFingers(ViewportRig rig, Offset by, double scale, bool shifted)
    {
        var state = rig.View.State;
        state.PanZoomStart();
        state.PanZoomUpdate(scale, by, shifted);
        state.PanZoomEnd();
    }
}
