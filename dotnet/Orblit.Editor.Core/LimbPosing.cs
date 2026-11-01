namespace Orblit.Editor.Core;

using System.Runtime.CompilerServices;
using Orblit.Motion;
using Orblit.VectorMath;

/// <summary>The three bones of an arm or a leg, parent to child, by name.</summary>
public sealed record Limb(string Name, string Root, string Mid, string Tip);

/// <summary>Where to put the tip of one limb of a model, and when.</summary>
/// <param name="Target">What the clip calls the model.</param>
/// <param name="Limb">The limb to turn.</param>
/// <param name="Point">Where the tip goes, in the model's own space.</param>
/// <param name="At">The time in the clip the turns are keyed at.</param>
public readonly record struct LimbGoal(string Target, Limb Limb, Vector3 Point, double At);

/// <summary>A limb reached for with two keyed turns.</summary>
public static class LimbPosing
{
    private static readonly (string Name, HumanoidBone Root, HumanoidBone Mid, HumanoidBone Tip)[] Parts =
    [
        ("left arm", HumanoidBone.LeftUpperArm, HumanoidBone.LeftLowerArm, HumanoidBone.LeftHand),
        ("right arm", HumanoidBone.RightUpperArm, HumanoidBone.RightLowerArm, HumanoidBone.RightHand),
        ("left leg", HumanoidBone.LeftUpperLeg, HumanoidBone.LeftLowerLeg, HumanoidBone.LeftFoot),
        ("right leg", HumanoidBone.RightUpperLeg, HumanoidBone.RightLowerLeg, HumanoidBone.RightFoot),
    ];

    // Held by the skeleton it was worked out from, so a skeleton read once is searched once however
    // often the view is drawn.
    private static readonly ConditionalWeakTable<RestSkeleton, IReadOnlyList<Limb>> Found = new();

    /// <summary>The arms and legs of <paramref name="skeleton"/> that two turns can reach with.</summary>
    /// <remarks>
    /// Found as the parts of a person are, so a model that is not one has none, and an arm or leg with
    /// more bones than the finder expects is not placed. The finder places each limb as a line of bones,
    /// so each of the three hangs from the one before, which the two turns rely on.
    /// </remarks>
    public static IReadOnlyList<Limb> LimbsOf(RestSkeleton skeleton) => Found.GetValue(skeleton, Search);

    /// <summary>
    /// <paramref name="clip"/> with the two turns that put the tip of the goal's limb on its point, keyed
    /// at its time.
    /// </summary>
    /// <remarks>
    /// The turns are worked out from the pose the clip gives at that time, so a bone the clip does not
    /// move stands where it rests. Only rotations are keyed: the limb is turned and never stretched. Baked
    /// as plain turns on purpose. The file stays an ordinary clip that plays anywhere a clip does, and
    /// nothing has to solve a limb at run time.
    /// </remarks>
    public static ClipDocument WithLimbReach(ClipDocument clip, RestSkeleton skeleton, LimbGoal goal)
    {
        var limb = goal.Limb;
        var pose = clip.SampleAt(goal.At).Bones.GetValueOrDefault(goal.Target) ?? new();
        var reach = LimbReach.Of(skeleton, pose, limb.Root, limb.Mid, limb.Tip, goal.Point);
        var upper = ClipEdits.Keyed(clip, new(goal.Target, limb.Root, "rotation"), ChannelKind.Rotation, new(goal.At, reach.Root));
        return ClipEdits.Keyed(upper, new(goal.Target, limb.Mid, "rotation"), ChannelKind.Rotation, new(goal.At, reach.Mid));
    }

    private static IReadOnlyList<Limb> Search(RestSkeleton skeleton)
    {
        var person = HumanoidMap.Of(skeleton);
        var limbs = new List<Limb>();
        foreach (var (name, root, mid, tip) in Parts)
        {
            if (person[root] is { } upper && person[mid] is { } lower && person[tip] is { } end)
            {
                limbs.Add(new Limb(name, upper, lower, end));
            }
        }

        return limbs;
    }
}
