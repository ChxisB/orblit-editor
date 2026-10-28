namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.VectorMath;

/// <summary>The hand or foot of a selected model that the open clip can be keyed through.</summary>
/// <param name="Id">The scene object the model is.</param>
/// <param name="Target">What the clip calls the model.</param>
/// <param name="World">From the model's own space to the world.</param>
/// <param name="Tip">Where the end of the limb stands in the world.</param>
public sealed record LimbHandle(string Id, string Target, RestSkeleton Skeleton, Limb Limb, Matrix4 World, Vector3 Tip);

/// <summary>
/// Handles on the hands and feet of a selected model, which put the limb where they are dragged and key
/// it there.
/// </summary>
/// <remarks>
/// Shown with the view's bones, over the pose the clip gives at the playhead. A drag keys the rotations
/// of the two bones above the handle at the playhead, as one step of undo, and leaves everything else of
/// the pose alone. A limb that cannot reach straightens towards the pointer. It is registered after the
/// transform handles, so a drag that starts on one of those is theirs.
/// </remarks>
public static class LimbGizmo
{
    // How near a handle the pointer has to be to take it, in pixels.
    private const double Grab = 12;

    private const double RingRadius = 7;

    private static readonly IReadOnlyDictionary<string, BoneLocal> AtRest = new Dictionary<string, BoneLocal>();

    /// <summary>The gizmo the shell registers, keying the clip on <paramref name="bench"/> and shown as <paramref name="look"/> says.</summary>
    public static GizmoType Type(ClipBench bench, ModelSkeletons models, SkeletonLook look) => new(
        "limb",
        target => target.Selected.Any(id => target.Scene[id] is { } item && models.OfAsset(item.MeshAsset) is not null),
        target => look.Value.Bones ? Rings(target.Projection, Handles(target, bench, models)) : [],
        new LimbDrag(bench, models, look).Input);

    /// <summary>Every handle the open clip gives the selection at the playhead.</summary>
    /// <remarks>Empty while the clip plays, since a pose that is moving by itself is not one to hold a hand against.</remarks>
    public static IReadOnlyList<LimbHandle> Handles(GizmoTarget target, ClipBench bench, ModelSkeletons models)
    {
        if (bench.ScopeIn(target.Scene) is not { } scope || bench.Clip is not { } clip || bench.Playing)
        {
            return [];
        }

        var frame = clip.SampleAt(bench.Frame);
        var handles = new List<LimbHandle>();
        foreach (var id in target.Selected)
        {
            if (target.Scene[id] is { } item && models.OfAsset(item.MeshAsset) is { } skeleton && scope.TargetOf(id) is { } named)
            {
                var world = target.Scene.WorldOf(id);
                var origins = skeleton.PosedOrigins(frame.Bones.GetValueOrDefault(named) ?? AtRest);
                // LimbsOf names only bones of this skeleton.
                handles.AddRange(LimbPosing.LimbsOf(skeleton).Select(limb =>
                    new LimbHandle(id, named, skeleton, limb, world, world.Transformed3(origins[skeleton.IndexOf(limb.Tip)!.Value]))));
            }
        }

        return handles;
    }

    private static List<GizmoStroke> Rings(ViewportProjection projection, IReadOnlyList<LimbHandle> handles) =>
        [.. handles.Select(handle =>
        {
            var lines = new WorldLines(projection);
            lines.Ring(handle.Tip, RingRadius);
            return new GizmoStroke(lines, GizmoTone.Ember);
        })];

    // What a drag on a handle remembers between one move and the next. Plane is a point of the plane the
    // pointer is held to, through the tip and facing the camera. Grabbed is how far the tip was from the
    // point first grabbed, so it does not jump to the pointer.
    private sealed record Hold(LimbHandle Handle, object Gesture, Matrix4 ToModel, Vector3 Plane, Vector3 Grabbed);

    private sealed class LimbDrag(ClipBench bench, ModelSkeletons models, SkeletonLook look)
    {
        private Hold? _hold;

        public bool Input(GizmoTarget target, ViewportGesture gesture)
        {
            if (_hold is { } hold)
            {
                return Carry(target.Projection, hold, gesture);
            }

            if (!look.Value.Bones || Under(target, gesture.At) is not { } handle)
            {
                return false;
            }

            if (gesture.Phase == ViewportPhase.DragStart)
            {
                _hold = Pick(target.Projection, handle, gesture.At);
            }

            // A tap is taken too, so a click that misses by a pixel does not clear the selection the
            // handle belongs to.
            return gesture.Phase is ViewportPhase.DragStart or ViewportPhase.Tap;
        }

        private static Hold Pick(ViewportProjection projection, LimbHandle handle, Offset at)
        {
            var first = projection.RayThrough(at).Meets(handle.Tip, projection.Forward);
            return new Hold(
                handle,
                new object(),
                Matrix4.Inverted(handle.World),
                handle.Tip,
                first is { } point ? handle.Tip - point : Vector3.Zero());
        }

        private LimbHandle? Under(GizmoTarget target, Offset at) =>
            Handles(target, bench, models)
                .Select(handle => (Handle: handle, Away: target.Projection.Project(handle.Tip) is { } screen ? (screen - at).Distance : double.PositiveInfinity))
                .Where(one => one.Away <= Grab)
                .OrderBy(one => one.Away)
                .Select(one => one.Handle)
                .FirstOrDefault();

        // The rest of a drag, which this gizmo keeps whatever is under the pointer.
        private bool Carry(ViewportProjection projection, Hold hold, ViewportGesture gesture)
        {
            if (gesture.Phase == ViewportPhase.DragUpdate)
            {
                Reach(projection, hold, gesture.At);
            }
            else if (gesture.Phase is ViewportPhase.DragEnd or ViewportPhase.DragCancel)
            {
                // A cancelled drag keeps what it keyed. It is one step, and undo is the way back.
                _hold = null;
            }

            return true;
        }

        private void Reach(ViewportProjection projection, Hold hold, Offset at)
        {
            if (projection.RayThrough(at).Meets(hold.Plane, projection.Forward) is not { } hit)
            {
                return;
            }

            var handle = hold.Handle;
            var goal = new LimbGoal(handle.Target, handle.Limb, hold.ToModel.Transformed3(hit + hold.Grabbed), bench.Frame);
            bench.Edit(
                $"Pose {handle.Limb.Name}",
                clip => LimbPosing.WithLimbReach(clip, handle.Skeleton, goal),
                new EditOptions(Gesture: hold.Gesture));
        }
    }
}
