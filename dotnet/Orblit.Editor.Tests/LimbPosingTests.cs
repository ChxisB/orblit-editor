namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// A limb reached for with two keyed turns: which limbs a model has, and the clip that puts the tip of one
// where it was asked.
public sealed class LimbPosingTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("orblit_limb");
    private readonly RestSkeleton _skeleton;
    private readonly Limb _arm;

    public LimbPosingTests()
    {
        File.WriteAllBytes(Path.Combine(_root.FullName, "man.glb"), SkinnedGlb.PersonGlb());
        _skeleton = new ModelSkeletons(_root.FullName).OfAsset("man.glb")!;
        _arm = LimbPosing.LimbsOf(_skeleton).First(limb => limb.Name == "left arm");
    }

    public void Dispose() => _root.Delete(recursive: true);

    private static ClipDocument EmptyClip() => new("pose", 1, rate: 10);

    private static ClipChannel<Quaternion> SpineTurn(Quaternion turn) =>
        new(string.Empty, "rotation", ChannelKind.Rotation, [new Key<Quaternion>(0, turn)], bone: "spine");

    private RestSkeleton SkeletonIn(string file, byte[] bytes)
    {
        File.WriteAllBytes(Path.Combine(_root.FullName, file), bytes);
        return new ModelSkeletons(_root.FullName).OfAsset(file)!;
    }

    private IReadOnlyList<Vector3> OriginsIn(ClipDocument clip, double at) =>
        _skeleton.PosedOrigins(clip.SampleAt(at).Bones.GetValueOrDefault(string.Empty) ?? new());

    private Vector3 TipOf(ClipDocument clip, double at) => OriginsIn(clip, at)[_skeleton.IndexOf(_arm.Tip)!.Value];

    private ClipDocument Reach(ClipDocument clip, Vector3 point, double at) =>
        LimbPosing.WithLimbReach(clip, _skeleton, new LimbGoal(string.Empty, _arm, point, at));

    [Fact]
    public void FindsTheArmsAndTheLegsOfAPerson() =>
        Assert.Equal(["left arm", "right arm", "left leg", "right leg"], LimbPosing.LimbsOf(_skeleton).Select(limb => limb.Name));

    [Fact]
    public void NamesThreeBonesThatHangFromOneAnother()
    {
        foreach (var limb in LimbPosing.LimbsOf(_skeleton))
        {
            Assert.Equal(_skeleton.IndexOf(limb.Root), _skeleton.Parents[_skeleton.IndexOf(limb.Mid)!.Value]);
            Assert.Equal(_skeleton.IndexOf(limb.Mid), _skeleton.Parents[_skeleton.IndexOf(limb.Tip)!.Value]);
        }
    }

    [Fact]
    public void PutsTheHandAtTheEndOfTheArmAndTheFootOfTheLeg()
    {
        var limbs = LimbPosing.LimbsOf(_skeleton).ToDictionary(limb => limb.Name);

        Assert.Contains("hand", limbs["left arm"].Tip, StringComparison.Ordinal);
        Assert.Contains("foot", limbs["left leg"].Tip, StringComparison.Ordinal);
        Assert.True(_skeleton.OriginOf(_skeleton.IndexOf(limbs["left arm"].Tip)!.Value).X > 0);
    }

    [Fact]
    public void IsTheSameAnswerForTheSameSkeleton() =>
        Assert.Same(LimbPosing.LimbsOf(_skeleton), LimbPosing.LimbsOf(_skeleton));

    [Fact]
    public void LeavesOutAnArmTheFinderCannotReadAsOne()
    {
        // Five bones in a line from the chest are more than an arm has, so the finder places neither arm.
        var names = LimbPosing.LimbsOf(SkeletonIn("twist.glb", SkinnedGlb.PersonGlb(forearmTwist: true))).Select(limb => limb.Name).ToList();

        Assert.DoesNotContain("left arm", names);
        Assert.Contains("left leg", names);
        Assert.Contains("right leg", names);
    }

    [Fact]
    public void HasNoneForASkeletonThatIsNotAPerson() =>
        Assert.Empty(LimbPosing.LimbsOf(SkeletonIn("spine.glb", SkinnedGlb.ChainGlb("hips", "spine", "head"))));

    [Fact]
    public void PutsTheTipOfTheLimbOnAPointItCanReach()
    {
        var point = TipOf(EmptyClip(), 0) + new Vector3(-0.1, 0.15, 0.2);

        var clip = Reach(EmptyClip(), point, 0.5);

        Assert.True((TipOf(clip, 0.5) - point).Length < 1e-4);
    }

    [Fact]
    public void KeysTheTurnOfTheTwoBonesAboveTheTipAndNoMore()
    {
        var clip = Reach(EmptyClip(), TipOf(EmptyClip(), 0) + new Vector3(-0.1, 0.1, 0.1), 0.5);

        Assert.Equal(
            new HashSet<string> { $"{_arm.Root}.rotation", $"{_arm.Mid}.rotation" },
            clip.Channels.Select(channel => $"{channel.Bone}.{channel.Property}").ToHashSet());
        Assert.All(clip.Channels, channel => Assert.Equal(0.5, channel.BoxedKeys.Single().At));
    }

    [Fact]
    public void ChangesNothingElseOfTheClip()
    {
        var other = SpineTurn(Quaternion.Identity());
        var before = new ClipDocument("pose", 1, rate: 10, channels: [other]);

        var clip = Reach(before, TipOf(before, 0) + new Vector3(-0.1, 0.1, 0.1), 0.5);

        Assert.Same(other, clip.Channels[0]);
        Assert.Equal(3, clip.Channels.Count);
    }

    [Fact]
    public void WorksFromThePoseTheClipAlreadyGives()
    {
        var before = new ClipDocument("pose", 1, rate: 10, channels: [SpineTurn(Quaternion.AxisAngle(new Vector3(1, 0, 0), 0.5))]);
        var point = TipOf(before, 0) + new Vector3(-0.15, -0.1, 0.1);

        var clip = Reach(before, point, 0);

        Assert.True((TipOf(clip, 0) - point).Length < 1e-4);
    }

    [Fact]
    public void ReachesAgainFromWhatAnEarlierReachKeyed()
    {
        var first = Reach(EmptyClip(), TipOf(EmptyClip(), 0) + new Vector3(-0.15, 0.2, 0.1), 0);
        var point = TipOf(first, 0) + new Vector3(0.05, -0.1, 0.05);

        var second = Reach(first, point, 0);

        Assert.Equal(2, second.Channels.Count);
        Assert.True((TipOf(second, 0) - point).Length < 1e-4);
    }

    [Fact]
    public void StraightensTheLimbTowardsAPointOutOfReach()
    {
        var far = new Vector3(5, 1.4, 0);

        var clip = Reach(EmptyClip(), far, 0);

        var shoulder = OriginsIn(clip, 0)[_skeleton.IndexOf(_arm.Root)!.Value];
        Assert.Equal(1, (TipOf(clip, 0) - shoulder).Normalized().Dot((far - shoulder).Normalized()), 1e-3);
    }

    // Added in C#. The two turns are keyed on the model the goal names, not on the clip's owner.
    [Fact]
    public void KeysTheModelTheGoalNames()
    {
        var clip = LimbPosing.WithLimbReach(EmptyClip(), _skeleton, new LimbGoal("man", _arm, TipOf(EmptyClip(), 0), 0));

        Assert.All(clip.Channels, channel => Assert.Equal("man", channel.Target));
    }
}
