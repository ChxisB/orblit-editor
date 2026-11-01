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

        Assert.True(camera.Distance > 1.0);

        for (var i = 0; i < 400; i++)
        {
            camera = camera.Zoom(500);
        }

        Assert.True(camera.Distance <= 200.0);
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
}
