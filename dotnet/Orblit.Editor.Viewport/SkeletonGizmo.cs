namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.VectorMath;

/// <summary>
/// Draws the bones of every selected model, in the pose the open clip gives them at the playhead, and
/// the poses either side of it as onion skin.
/// </summary>
/// <remarks>
/// The renderer poses a skinned model on its own and tells the editor nothing, so this is a second
/// reading of the same two things it works from: the skeleton in the model's file and the clip at the
/// playhead. A bone you cannot see is a rotation you key by guessing. A clip only reaches the model it
/// is played on and the parts of it the clip names, so any other selected model is drawn at rest. It
/// applies with the bones off as well and then draws nothing, so turning them on never asks the view to
/// rebuild. It only draws and never takes the pointer.
/// </remarks>
public static class SkeletonGizmo
{
    private static readonly IReadOnlyDictionary<string, BoneLocal> AtRest = new Dictionary<string, BoneLocal>();

    /// <summary>The gizmo the shell registers, posed by the clip on <paramref name="bench"/> and shown as <paramref name="look"/> says.</summary>
    public static GizmoType Type(ClipBench bench, ModelSkeletons models, SkeletonLook look) => new(
        "skeleton",
        target => target.Selected.Any(id => target.Scene[id] is { } item && models.OfAsset(item.MeshAsset) is not null),
        target => Strokes(target, bench, models, look.Value));

    /// <summary>For each selected model with a skeleton, its ghosts and then its current pose on top.</summary>
    public static IReadOnlyList<GizmoStroke> Strokes(GizmoTarget target, ClipBench bench, ModelSkeletons models, SkeletonSettings settings)
    {
        if (!settings.Bones)
        {
            return [];
        }

        var scope = bench.ScopeIn(target.Scene);
        var poses = Poses(bench, settings);
        var strokes = new List<GizmoStroke>();
        foreach (var id in target.Selected)
        {
            if (target.Scene[id] is { } item && models.OfAsset(item.MeshAsset) is { } skeleton)
            {
                var model = new Model(target.Scene.WorldOf(id), skeleton, scope?.TargetOf(id));
                strokes.AddRange(StrokesOf(model, poses, target.Projection));
            }
        }

        return strokes;
    }

    /// <summary>
    /// Every bone of <paramref name="skeleton"/> that has a parent, as the two points it is drawn between,
    /// in the model's own space.
    /// </summary>
    /// <remarks>
    /// <paramref name="pose"/> is the clip's say over some of the bones, by name. A root has no line, since
    /// a bone is drawn from the joint it hangs from to its own.
    /// </remarks>
    public static IReadOnlyList<(Vector3 From, Vector3 To)> BoneLines(RestSkeleton skeleton, IReadOnlyDictionary<string, BoneLocal> pose)
    {
        var origins = skeleton.PosedOrigins(pose);
        return [.. Enumerable.Range(0, skeleton.Length).Where(bone => skeleton.Parents[bone] >= 0).Select(bone => (origins[skeleton.Parents[bone]], origins[bone]))];
    }

    private static IEnumerable<GizmoStroke> StrokesOf(Model model, IReadOnlyList<Posed> poses, ViewportProjection projection)
    {
        foreach (var pose in poses)
        {
            var bones = model.Named is { } named && pose.Frame?.Bones.GetValueOrDefault(named) is { } posed ? posed : AtRest;
            // A ghost of a pose the clip does not move would only sit under the current one.
            if (pose.Current || bones.Count > 0)
            {
                var lines = new WorldLines(projection);
                foreach (var (from, to) in BoneLines(model.Skeleton, bones))
                {
                    lines.Line(model.World.Transformed3(from), model.World.Transformed3(to));
                }

                yield return new GizmoStroke(lines, pose.Tone, pose.Alpha);
            }
        }
    }

    // The ghosts first, so the current pose is drawn over them. Earlier ghosts are red and later ones
    // green, each fainter the further it is from the playhead. A model the clip does not reach has no
    // name in it, so it reads every pose as rest and its ghosts are dropped.
    private static List<Posed> Poses(ClipBench bench, SkeletonSettings settings)
    {
        if (bench.Clip is not { } clip)
        {
            return [new Posed(null, GizmoTone.AxisZ, byte.MaxValue, Current: true)];
        }

        var earlier = 0;
        var later = 0;
        var poses = new List<Posed>();
        foreach (var moment in settings.OnionMoments(clip, bench.At))
        {
            var rank = moment.Earlier ? ++earlier : ++later;
            var alpha = (byte)Math.Clamp(190 - (45 * (rank - 1)), 50, 190);
            poses.Add(new Posed(clip.SampleAt(moment.At), moment.Earlier ? GizmoTone.AxisX : GizmoTone.AxisY, alpha, Current: false));
        }

        poses.Add(new Posed(clip.SampleAt(bench.At), GizmoTone.AxisZ, byte.MaxValue, Current: true));
        return poses;
    }

    // A selected model: where it is, its bones, and what the clip calls it, or null when the clip does not reach it.
    private sealed record Model(Matrix4 World, RestSkeleton Skeleton, string? Named);

    // One pose to draw, and how. A null frame is the rest pose.
    private sealed record Posed(ClipFrame? Frame, GizmoTone Tone, byte Alpha, bool Current);
}
