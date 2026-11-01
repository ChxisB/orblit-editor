namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>The camera the game looks through when a scene says nothing else.</summary>
public sealed class GameCameraTests
{
    private static SceneObject Camera(string id, double x, bool visible = true) =>
        new(id, id, ObjectKind.Camera, position: new Vector3(x, 1, 5), visible: visible);

    [Fact]
    public void ASceneWithNoCameraHasNoGameCamera()
    {
        var scene = new EditorScene([new SceneObject("cube", "Cube", ObjectKind.Mesh)]);

        Assert.Null(SceneCameras.GameCamera(scene));
    }

    [Fact]
    public void TheFirstCameraIsTheOneTheGameLooksThrough()
    {
        var scene = new EditorScene([Camera("front", 1), Camera("side", 9)]);

        var seen = SceneCameras.GameCamera(scene);

        Assert.Equal(SceneCameras.CameraOf(scene, scene["front"]!).Position, seen!.Position);
    }

    [Fact]
    public void ACameraThatIsHiddenIsSkipped()
    {
        var scene = new EditorScene([Camera("front", 1, visible: false), Camera("side", 9)]);

        var seen = SceneCameras.GameCamera(scene);

        Assert.Equal(SceneCameras.CameraOf(scene, scene["side"]!).Position, seen!.Position);
    }

    [Fact]
    public void ACameraInAHiddenGroupIsSkippedToo()
    {
        var scene = new EditorScene(
        [
            new SceneObject("rig", "Rig", ObjectKind.Group, visible: false),
            new SceneObject("inside", "Inside", ObjectKind.Camera, parentId: "rig", position: new Vector3(1, 1, 5)),
            Camera("outside", 9),
        ]);

        var seen = SceneCameras.GameCamera(scene);

        Assert.Equal(SceneCameras.CameraOf(scene, scene["outside"]!).Position, seen!.Position);
    }
}
