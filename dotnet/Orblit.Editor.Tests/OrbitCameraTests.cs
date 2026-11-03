namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.VectorMath;

// The viewport camera on its own: where it ends up when asked to frame something, and what panning
// does to it.
public sealed class OrbitCameraTests
{
    [Fact]
    public void StopsShortOfThePolesWhereTheViewMatrixCollapses()
    {
        // Far more drag than anyone would apply in one gesture.
        var up = new OrbitCamera().Orbit(new Offset(0, -100000));
        var down = new OrbitCamera().Orbit(new Offset(0, 100000));

        foreach (var camera in new[] { up, down })
        {
            var render = camera.ToRenderCamera();
            var view = render.Position - render.Target;
            // A view direction parallel to up is what flips the image.
            Assert.True(view.Cross(new Vector3(0, 1, 0)).Length > 0.01);
        }
    }

    [Fact]
    public void ZoomingStaysInFrontOfTheNearPlaneAndInsideTheWorld()
    {
        var camera = new OrbitCamera();
        for (var i = 0; i < 200; i++)
        {
            camera = camera.Zoom(-500);
        }

        Assert.True(camera.Distance >= OrbitCamera.MinDistance);

        for (var i = 0; i < 400; i++)
        {
            camera = camera.Zoom(500);
        }

        Assert.True(camera.Distance <= OrbitCamera.MaxDistance);
    }

    [Fact]
    public void ZoomNeverLeavesTheRange()
    {
        Assert.Equal(OrbitCamera.MinDistance, new OrbitCamera().Zoom(-1e6).Distance);
        Assert.Equal(OrbitCamera.MaxDistance, new OrbitCamera().Zoom(1e6).Distance);
    }

    [Fact]
    public void TheRangeKeepsTheTargetInFrontOfTheNearPlaneAndInsideTheFarOne()
    {
        Assert.True(OrbitCamera.MinDistance > OrbitCamera.NearPlane);
        Assert.True(OrbitCamera.MaxDistance < OrbitCamera.FarPlane);
    }

    [Fact]
    public void FramingAWorldOfAHundredMetresFitsIt()
    {
        var framed = new OrbitCamera().Framing(new Vector3(0, 0, 0), 100);

        // The ceiling used to be 200 metres, which cut this one short.
        var fitted = 100 / Math.Tan(Doubles.Radians(50) / 2) * 1.6;
        Assert.Equal(fitted, framed.Distance, 1e-6);
    }

    [Fact]
    public void FramingAGrainOrAContinentStaysInTheRange()
    {
        Assert.Equal(OrbitCamera.MinDistance, new OrbitCamera().Framing(new Vector3(0, 0, 0), 0.001).Distance);
        Assert.Equal(OrbitCamera.MaxDistance, new OrbitCamera().Framing(new Vector3(0, 0, 0), 1e6).Distance);
    }

    [Fact]
    public void FramingFitsAThingThatIsOffToOneSide()
    {
        var scene = EditorScene.Starter();
        scene["crate"]!.Position.SetValues(20, 0, 0);
        scene.Invalidate();

        var bounds = scene.BoundsOf("crate");
        Assert.Equal(20, bounds.Centre.X, 1e-6);

        var framed = new OrbitCamera().Framing(bounds.Centre, bounds.Radius);
        // The camera now looks at the crate rather than the origin.
        Assert.Equal(20, framed.Target.X, 1e-6);
    }

    [Fact]
    public void FramingKeepsTheAngleSoFramingIsNotAlsoANewShot()
    {
        var camera = new OrbitCamera(yaw: 1.2, pitch: -0.4);
        var framed = camera.Framing(new Vector3(5, 0, 0), 2);

        Assert.Equal(camera.Yaw, framed.Yaw);
        Assert.Equal(camera.Pitch, framed.Pitch);
    }

    [Fact]
    public void AGroupIsFramedByEverythingInIt()
    {
        var scene = EditorScene.Starter();
        var group = scene.BoundsOf("props");
        var one = scene.BoundsOf("cube");

        // Props holds the cube and the crate, so it is wider than either.
        Assert.True(group.Radius > one.Radius);
    }

    [Fact]
    public void SomethingFlatDoesNotPutTheCameraInsideIt()
    {
        var scene = EditorScene.Starter();
        // The ground is 8 x 0.05 x 8.
        var bounds = scene.BoundsOf("ground");
        var framed = new OrbitCamera().Framing(bounds.Centre, bounds.Radius);

        Assert.True(framed.Distance > 1.5);
    }

    [Fact]
    public void ALightHasNoGeometryAndIsStillFramedRatherThanRefused()
    {
        var scene = EditorScene.Starter();
        var bounds = scene.BoundsOf("sun");

        Assert.True(bounds.Radius > 0);
        Assert.False(double.IsNaN(bounds.Centre.X) || double.IsNaN(bounds.Centre.Y) || double.IsNaN(bounds.Centre.Z));
    }

    [Fact]
    public void PanningMovesWhatTheCameraLooksAtNotHowItLooks()
    {
        var camera = new OrbitCamera(yaw: 0.6, pitch: 0.35);
        var panned = camera.Pan(new Offset(40, 0));

        Assert.Equal(camera.Yaw, panned.Yaw);
        Assert.Equal(camera.Distance, panned.Distance);
        Assert.True((panned.Target - camera.Target).Length > 0);
    }

    private static readonly OrbitCamera EaseStart = new(yaw: 0.6, pitch: 0.3, distance: 10, target: new Vector3(0, 0.5, 0));
    private static readonly OrbitCamera EaseGoal = new(yaw: 1.0, pitch: -0.2, distance: 40, target: new Vector3(20, 0.5, -10));

    [Fact]
    public void EasingIsTheSameAtAnyFrameRate()
    {
        var once = EaseStart.Toward(EaseGoal, 0.032, 0.05);
        var twice = EaseStart.Toward(EaseGoal, 0.016, 0.05).Toward(EaseGoal, 0.016, 0.05);

        Assert.Equal(once.Yaw, twice.Yaw, 1e-9);
        Assert.Equal(once.Pitch, twice.Pitch, 1e-9);
        Assert.Equal(once.Distance, twice.Distance, 1e-9);
        Assert.Equal(0, (once.Target - twice.Target).Length, 1e-9);
    }

    [Fact]
    public void EasingTakesTheShortWayRoundInYaw()
    {
        var eased = new OrbitCamera(yaw: 3.0).Toward(new OrbitCamera(yaw: -3.0), 0.05, 0.05);

        // Three to minus three is 0.28 radians on through the back, not nearly six the other way.
        Assert.True(eased.Yaw > 3.0);
    }

    [Fact]
    public void EasingSettlesInsideTheToleranceInHalfASecond()
    {
        var camera = EaseStart;
        Assert.False(camera.IsNear(EaseGoal));

        camera = camera.Toward(EaseGoal, 1.0 / 60, 0.05);
        Assert.False(camera.IsNear(EaseGoal));

        for (var frame = 0; frame < 30; frame++)
        {
            camera = camera.Toward(EaseGoal, 1.0 / 60, 0.05);
        }

        Assert.True(camera.IsNear(EaseGoal));
    }
}
