namespace Orblit.Editor.Core;

using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>What the editor asks of an object's physics, sound, vehicle and particle components.</summary>
public static class PhysicsComponents
{
    /// <summary>The joint on <paramref name="item"/>, if it has one.</summary>
    public static JointComponent? JointOf(SceneObject item) => Of<JointComponent>(item, SceneComponents.Joint);

    /// <summary>The physics body on <paramref name="item"/>, if it has one.</summary>
    public static BodyComponent? PhysicsBodyOf(SceneObject item) => Of<BodyComponent>(item, SceneComponents.Body);

    /// <summary>
    /// The zone on <paramref name="item"/>, if it has one: a region that changes how the bodies
    /// inside it move. It uses the object's body as the region.
    /// </summary>
    public static ZoneComponent? ZoneOf(SceneObject item) => Of<ZoneComponent>(item, SceneComponents.Zone);

    /// <summary>The sound source on <paramref name="item"/>, if it has one.</summary>
    public static SoundSourceComponent? SoundSourceOf(SceneObject item) =>
        Of<SoundSourceComponent>(item, SceneComponents.SoundSource);

    /// <summary>The vehicle on <paramref name="item"/>, if it has one.</summary>
    public static VehicleComponent? VehicleOf(SceneObject item) => Of<VehicleComponent>(item, SceneComponents.Vehicle);

    /// <summary>The wheel on <paramref name="item"/>, if it has one.</summary>
    public static WheelComponent? WheelOf(SceneObject item) => Of<WheelComponent>(item, SceneComponents.Wheel);

    /// <summary>Whether the solver moves <paramref name="body"/>. Ground never moves, whatever its file says.</summary>
    public static bool MovesFreely(BodyComponent body) =>
        body.Motion == BodyMotion.Free && body.Shape != BodyShape.Plane && body.Shape != BodyShape.Mesh;

    /// <summary>
    /// Whether <paramref name="item"/> is a place rather than a thing: a body that asks to be a
    /// trigger, or that is a zone's region. The solver moves a free body and cannot make one a
    /// place, so a free body is never one.
    /// </summary>
    public static bool IsPlace(SceneObject item)
    {
        var body = PhysicsBodyOf(item);
        if (body is null || MovesFreely(body))
        {
            return false;
        }

        return body.Trigger || ZoneOf(item) is not null;
    }

    /// <summary>
    /// The two bodies a joint on <paramref name="id"/> holds, found the way a running scene finds
    /// them: the nearest body at or above it, and the nearest body above that, or null for the
    /// world.
    /// </summary>
    /// <remarks>
    /// Asked of the editor's own tree rather than a saved document, so what the inspector names and
    /// the gizmo joins up is what will be simulated.
    /// </remarks>
    public static (string? Body, string? Holder) JointEndsIn(EditorScene scene, string id) =>
        JointComponent.EndsOf(
            id,
            parentOf: found => scene[found]?.ParentId,
            hasBody: found => scene[found] is { } item && PhysicsBodyOf(item) is not null);

    /// <summary>
    /// Where the middle of the body on <paramref name="id"/> is in the world: the point a joint
    /// draws its line to and a distance joint measures to.
    /// </summary>
    public static Vector3? BodyMiddleIn(EditorScene scene, string id)
    {
        var body = scene[id] is { } item ? PhysicsBodyOf(item) : null;
        return body is null ? null : scene.WorldOf(id).Transformed3(body.Centre);
    }

    /// <summary>The component of type <typeparamref name="T"/> that <paramref name="item"/> keeps under <paramref name="type"/>, if it has one.</summary>
    public static T? Of<T>(SceneObject item, string type)
        where T : SceneComponent =>
        item.Components.TryGetValue(type, out var found) ? found as T : null;
}
