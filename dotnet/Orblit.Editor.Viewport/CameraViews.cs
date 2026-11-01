namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>Moving between the scene view and the cameras of a scene.</summary>
public static class CameraViews
{
    // The view turns around a point this far ahead of the camera, which is near enough to orbit
    // something in front of it.
    private const double Ahead = 5;

    /// <summary>
    /// The scene view put where <paramref name="camera"/> is and looking where it looks, turning
    /// around a point a few metres ahead.
    /// </summary>
    public static OrbitCamera ViewThrough(EditorScene scene, SceneObject camera)
    {
        var seen = SceneCameras.CameraOf(scene, camera);
        var forward = (seen.Target - seen.Position).Normalized();
        return new OrbitCamera(
            Math.Atan2(-forward.X, -forward.Z),
            Doubles.Clamp(Math.Asin(Doubles.Clamp(-forward.Y, -1.0, 1.0)), -OrbitCamera.PitchLimit, OrbitCamera.PitchLimit),
            Ahead,
            target: seen.Position + (forward * Ahead));
    }

    /// <summary>
    /// Where a camera under <paramref name="parent"/> has to stand, and how it turns in degrees, to
    /// see what <paramref name="view"/> sees. A null or unknown parent is the scene itself. A view
    /// cannot roll, so neither does this.
    /// </summary>
    public static Placement PlaceFor(OrbitCamera view, EditorScene scene, string? parent = null)
    {
        var position = view.ToRenderCamera().Position;
        var rotation = new Vector3(Doubles.Degrees(-view.Pitch), Doubles.Degrees(view.Yaw), 0);

        // As they are. Read back from a matrix they can come out as another set of angles that turns
        // the same way, which reads oddly in the inspector.
        if (parent is null || !scene.Contains(parent))
        {
            return new Placement(position, rotation);
        }

        var world = Matrix4.TranslationValues(position.X, position.Y, position.Z);
        world.Multiply(SceneMath.RotationFromDegrees(rotation));
        var local = Matrix4.Inverted(scene.WorldOf(parent)).Multiplied(world);
        return new Placement(local.GetTranslation(), SceneMath.EulerDegreesOf(local));
    }
}
