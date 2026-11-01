namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// What the view draws of a skeleton and of the poses round the playhead, worked out without a view.
public sealed class SkeletonViewTests
{
    private static readonly SkeletonSettings On = new(Onion: true, Before: 2, After: 2, Stride: 1);

    private static readonly RestSkeleton Spine = new(["hips", "spine", "head"], [-1, 0, 1], [At(0, 1, 0), At(0, 0.5, 0), At(0, 0.4, 0)]);

    private static Matrix4 At(double x, double y, double z) => Matrix4.TranslationValues(x, y, z);

    private static ClipDocument ClipOf(double duration = 1, double rate = 10, WhenDone whenDone = WhenDone.Hold) =>
        new("walk", duration, whenDone, rate);

    private static void Times(IReadOnlyList<OnionMoment> moments, params double[] want)
    {
        var got = moments.Select(moment => moment.At).ToList();
        Assert.True(got.Count == want.Length, $"{string.Join(", ", got)} is not {string.Join(", ", want)}");
        for (var i = 0; i < want.Length; i++)
        {
            Assert.Equal(want[i], got[i], 1e-9);
        }
    }

    private static void Near(Vector3 got, Vector3 want) =>
        Assert.True((got - want).Length < 1e-9, $"{got} is not {want}");

    [Fact]
    public void GhostsAreOneFrameApartEarlierFirstAndNearestFirst()
    {
        var moments = On.OnionMoments(ClipOf(), 0.5);

        Times(moments, 0.4, 0.3, 0.6, 0.7);
        Assert.Equal([true, true, false, false], moments.Select(moment => moment.Earlier));
    }

    [Fact]
    public void GhostsStepByTheStrideInFrames() =>
        Times((On with { Stride = 2, Before = 1, After = 1 }).OnionMoments(ClipOf(), 0.5), 0.3, 0.7);

    [Fact]
    public void ThereAreNoGhostsWithOnionSkinOff() =>
        Assert.Empty(new SkeletonSettings(Bones: true).OnionMoments(ClipOf(), 0.5));

    [Fact]
    public void AGhostThatFallsOffEitherEndOfAClipIsLeftOut()
    {
        Times(On.OnionMoments(ClipOf(), 0.1), 0, 0.2, 0.3);
        Times(On.OnionMoments(ClipOf(), 0.9), 0.8, 0.7, 1);
    }

    [Fact]
    public void GhostsWrapRoundForAClipThatLoops() =>
        // One frame before the start of a loop is the frame before its end.
        Times(On.OnionMoments(ClipOf(whenDone: WhenDone.Loop), 0.1), 0, 0.9, 0.2, 0.3);

    [Fact]
    public void AShortLoopNeverGhostsThePlayheadOrOneMomentTwice() =>
        Times(On.OnionMoments(ClipOf(duration: 0.2, whenDone: WhenDone.Loop), 0), 0.1);

    [Fact]
    public void ThereAreNoGhostsForAClipWithNoLengthOrNoRate()
    {
        Assert.Empty(On.OnionMoments(ClipOf(rate: 0), 0));
        Assert.Empty(On.OnionMoments(ClipOf(duration: 0), 0));
        // Added in C#. A loop has no ends to drop a ghost off, so only the check keeps these out.
        Assert.Empty(On.OnionMoments(ClipOf(rate: 0, whenDone: WhenDone.Loop), 0));
        Assert.Empty(On.OnionMoments(ClipOf(duration: 0, whenDone: WhenDone.Loop), 0));
    }

    [Fact]
    public void EachBoneJoinsTheJointItHangsFromAndARootJoinsNothing()
    {
        var lines = SkeletonGizmo.BoneLines(Spine, new Dictionary<string, BoneLocal>());

        Assert.Equal(2, lines.Count);
        Near(lines[0].From, new Vector3(0, 1, 0));
        Near(lines[0].To, new Vector3(0, 1.5, 0));
        Near(lines[1].From, new Vector3(0, 1.5, 0));
        Near(lines[1].To, new Vector3(0, 1.9, 0));
    }

    [Fact]
    public void BoneLinesFollowAPose()
    {
        // A quarter turn about Z at the spine lays the head along -X.
        var lines = SkeletonGizmo.BoneLines(Spine, new Dictionary<string, BoneLocal>
        {
            ["spine"] = new BoneLocal(rotation: Quaternion.AxisAngle(new Vector3(0, 0, 1), Math.PI / 2)),
        });

        Near(lines[0].To, new Vector3(0, 1.5, 0));
        Near(lines[1].To, new Vector3(-0.4, 1.5, 0));
    }
}
