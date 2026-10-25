namespace Orblit.Editor.Tests;

using Orblit.Editor.Viewport;
using Orblit.VectorMath;

public sealed class FlyCameraTests
{
    // How far apart two points are, for assertions about movement.
    private static double Gap(Vector3 a, Vector3 b) => (a - b).Length;

    [Fact]
    public void LookingAroundKeepsTheEyeWhereItIs()
    {
        var was = new OrbitCamera(yaw: 0.6, pitch: 0.2, distance: 9);
        var looked = was.Looking(new Offset(120, 40));

        // Orbiting swings the eye around the target; looking keeps the eye still and moves the
        // target. From inside a room that is the whole difference.
        Assert.True(Gap(looked.ToRenderCamera().Position, was.ToRenderCamera().Position) < 1e-9);
    }

    [Fact]
    public void LookingAroundChangesWhereItPoints()
    {
        var was = new OrbitCamera(yaw: 0.6, pitch: 0.2, distance: 9);
        var looked = was.Looking(new Offset(120, 0));

        Assert.True(Math.Abs(looked.Yaw - was.Yaw) > 1e-6);
        Assert.True(Gap(looked.Target, was.Target) > 0.5);
    }

    [Fact]
    public void LookingAroundCannotBeTurnedPastStraightUpOrStraightDown()
    {
        var camera = new OrbitCamera();
        for (var i = 0; i < 200; i++)
        {
            camera = camera.Looking(new Offset(0, 40));
        }

        // At exactly vertical the up vector and the view direction are parallel and the image
        // flips.
        Assert.True(Math.Abs(camera.Pitch) < 1.5708);
        Assert.True(double.IsFinite(camera.ToRenderCamera().Position.X));
    }

    [Fact]
    public void OrbitingStillSwingsAroundTheTarget()
    {
        var was = new OrbitCamera(distance: 9);
        var orbited = was.Orbit(new Offset(120, 0));

        Assert.True(Gap(orbited.Target, was.Target) < 1e-9);
        Assert.True(Gap(orbited.ToRenderCamera().Position, was.ToRenderCamera().Position) > 0.5);
    }

    [Fact]
    public void FlyingForwardGoesTowardsWhatTheCameraIsLookingAt()
    {
        var was = new OrbitCamera(yaw: 0.6, pitch: 0.25, distance: 20);
        var flown = was.Flying(new Vector3(0, 0, 5));

        // Closer to the thing it was looking at, by the distance asked for.
        var before = Gap(was.ToRenderCamera().Position, was.Target);
        var after = Gap(flown.ToRenderCamera().Position, was.Target);
        Assert.Equal(5, before - after, 1e-6);
    }

    [Fact]
    public void FlyingMovesTheEyeAndWhatItLooksAtTogether()
    {
        var was = new OrbitCamera(yaw: 0.6, pitch: 0.25, distance: 20);
        var flown = was.Flying(new Vector3(3, 2, 5));

        var eyeMoved = flown.ToRenderCamera().Position - was.ToRenderCamera().Position;
        var targetMoved = flown.Target - was.Target;
        Assert.True(Gap(eyeMoved, targetMoved) < 1e-9);
        Assert.Equal(was.Distance, flown.Distance);
    }

    [Fact]
    public void FlyingRightIsRightOfWhereItIsLookingNotRightOfTheWorld()
    {
        var facing = new OrbitCamera(yaw: 0, pitch: 0);
        var turned = new OrbitCamera(yaw: 1.5707963, pitch: 0);

        var one = facing.Flying(new Vector3(4, 0, 0)).Target;
        var other = turned.Flying(new Vector3(4, 0, 0)).Target;

        // Both moved four metres, in different world directions.
        Assert.Equal(4, Gap(one, facing.Target), 1e-6);
        Assert.Equal(4, Gap(other, turned.Target), 1e-6);
        Assert.True(Gap(one, other) > 1);
    }

    [Fact]
    public void TheAxesAreUnitLengthSoAMetreIsAMetre()
    {
        foreach (var camera in new[]
        {
            new OrbitCamera(),
            new OrbitCamera(yaw: 2.1, pitch: -0.9),
            new OrbitCamera(yaw: -0.4, pitch: 1.2),
        })
        {
            var axes = camera.Basis;
            Assert.Equal(1, axes.Right.Length, 1e-9);
            Assert.Equal(1, axes.Up.Length, 1e-9);
            Assert.Equal(1, axes.Forward.Length, 1e-9);
        }
    }

    [Fact]
    public void TheAxesAreAtRightAnglesToEachOther()
    {
        var axes = new OrbitCamera(yaw: 0.7, pitch: 0.3).Basis;
        Assert.Equal(0, axes.Right.Dot(axes.Up), 1e-9);
        Assert.Equal(0, axes.Right.Dot(axes.Forward), 1e-9);
        Assert.Equal(0, axes.Up.Dot(axes.Forward), 1e-9);
    }

    [Fact]
    public void GoingNowhereChangesNothing()
    {
        var was = new OrbitCamera(yaw: 0.6, pitch: 0.25);
        var flown = was.Flying(Vector3.Zero());
        Assert.True(Gap(flown.Target, was.Target) < 1e-12);
    }
}
