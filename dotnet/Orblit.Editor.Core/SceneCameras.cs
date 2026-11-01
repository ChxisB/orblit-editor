namespace Orblit.Editor.Core;

using Orblit.Filament;
using Orblit.Motion;
using Orblit.Stage;
using Orblit.VectorMath;

/// <summary>
/// Cameras as the scene has them, for the game view and for a cutscene that cuts between them.
/// </summary>
public static class SceneCameras
{
    /// <summary>
    /// Where <paramref name="target"/> stands in <paramref name="scene"/> and what it looks at, as a
    /// camera.
    /// <para>
    /// Shown or not: a cutscene cuts to cameras the game never looks through.
    /// </para>
    /// </summary>
    public static RenderCamera CameraOf(EditorScene scene, SceneObject target)
    {
        var world = scene.WorldOf(target.Id);
        var position = world.GetTranslation();

        // An unrotated camera looks down -Z, which is the convention the light direction and glTF
        // both use.
        var forward = world.GetRotation().Transformed(new Vector3(0, 0, -1));
        return new RenderCamera
        {
            Position = position,
            Target = position + (forward.Normalized() * 10),
            FieldOfView = 50,
        };
    }

    /// <summary>
    /// The camera the game looks through: the scene's first shown one, or null when it has none.
    /// <para>
    /// The first rather than a chosen one. A scene with two cameras has not said which is the
    /// player's, and the one nearest the top of the tree is at least a rule somebody can see the
    /// effect of.
    /// </para>
    /// </summary>
    public static RenderCamera? GameCamera(EditorScene scene) =>
        scene.Objects.FirstOrDefault(one => one.Kind == ObjectKind.Camera && scene.IsShown(one.Id)) is { } first
            ? CameraOf(scene, first)
            : null;

    /// <summary>
    /// What <paramref name="cutscene"/> looks through <paramref name="at"/> seconds in, on
    /// <paramref name="scene"/>: its shots' cameras blended by how much each has to say, as the game
    /// blends them. Null between shots, or when no shot's camera is in the scene.
    /// </summary>
    public static RenderCamera? CutsceneCamera(EditorScene scene, CutsceneDocument cutscene, double at) =>
        Cutscenes.BlendCameras(
            cutscene.ShotsAt(at)
                .Where(shot => scene[shot.Camera] is not null)
                .Select(shot => (CameraOf(scene, scene[shot.Camera]!), shot.Weight)));
}
