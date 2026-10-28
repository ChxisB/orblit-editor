namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Mesh;
using Orblit.VectorMath;

// Added in C#. Dart does this sum in the middle of a canvas widget's pan callback.
public sealed class UvDragTests : IDisposable
{
    private static readonly Vector2 Middle = new(0.5, 0.5);
    private readonly UvRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private (double Wide, double High) SizeOfFace()
    {
        var box = _rig.Mesh.UvBoundsOf([_rig.Mesh.Faces[0]])!.Value;
        return (box.Max.X - box.Min.X, box.Max.Y - box.Min.Y);
    }

    private void Freeze()
    {
        _rig.Select(0);
        _rig.Session.RunUvAction(UvAction.Freeze);
    }

    [Fact]
    public void MovingSaysHowFarThePointerWentSinceTheLastFrame()
    {
        _rig.Select(0);
        var start = _rig.Corners(0)[0];
        var drag = new UvDrag(UvGesture.Move, Middle, new Vector2(0.5, 0.5));

        drag.Move(new Vector2(0.6, 0.5), _rig.Session);
        drag.Move(new Vector2(0.65, 0.4), _rig.Session);

        // Each frame is from the last one, and the two add up to where the pointer is.
        var now = _rig.Corners(0)[0];
        Assert.Equal(start.X + 0.15, now.X, 1e-9);
        Assert.Equal(start.Y - 0.1, now.Y, 1e-9);
    }

    [Fact]
    public void ScalingGrowsWithThePointersDistanceFromTheMiddle()
    {
        Freeze();
        var (wide, high) = SizeOfFace();
        var drag = new UvDrag(UvGesture.Scale, Middle, new Vector2(1, 1));

        drag.Move(new Vector2(1.5, 1), _rig.Session);

        var (nowWide, nowHigh) = SizeOfFace();
        // Out from 0.5 to 1 across, so twice. The other axis did not move, so it is left alone.
        Assert.Equal(wide * 2, nowWide, 1e-9);
        Assert.Equal(high, nowHigh, 1e-9);
    }

    [Fact]
    public void ScalingFromTheMiddleItselfDoesNotDivideByNothing()
    {
        Freeze();
        var (wide, high) = SizeOfFace();
        var drag = new UvDrag(UvGesture.Scale, Middle, new Vector2(0.5, 0.5));

        drag.Move(new Vector2(0.9, 0.9), _rig.Session);

        var (nowWide, nowHigh) = SizeOfFace();
        Assert.Equal(wide, nowWide, 1e-9);
        Assert.Equal(high, nowHigh, 1e-9);
    }

    [Fact]
    public void TurningIsByTheAngleTheDragSweptAboutTheMiddle()
    {
        Freeze();
        var expected = _rig.Mesh.Copy();
        expected.TurnUvs([expected.Faces[0]], 90);
        var drag = new UvDrag(UvGesture.Turn, Middle, new Vector2(1.5, 0.5));

        drag.Move(new Vector2(0.5, 1.5), _rig.Session);

        var made = _rig.Corners(0);
        var wanted = expected.UvsOf(expected.Faces[0]);
        Assert.All(made.Zip(wanted), pair =>
        {
            Assert.Equal(pair.Second.X, pair.First.X, 1e-9);
            Assert.Equal(pair.Second.Y, pair.First.Y, 1e-9);
        });
    }
}
