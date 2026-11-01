namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Filament;

public sealed class MaterialAssignmentTests
{
    private static RenderScene Drawn(EditorScene scene) =>
        scene.ToRenderScene(new OrbitCamera().ToRenderCamera(), new SceneRenderOptions { ProjectRoot = "/proj" });

    [Fact]
    public void ATextureBecomesAMaterialTheObjectIsDrawnWith()
    {
        var scene = EditorScene.Starter();
        var cube = scene["cube"]!;
        cube.MaterialAsset = "assets/colormap.png";
        scene.Invalidate();

        var drawn = Drawn(scene);

        var material = Assert.Single(drawn.Materials, one => one.BaseColourMap?.Path == "/proj/assets/colormap.png");
        Assert.Equal(material.Key, Assert.Single(drawn.Objects, one => one.Key == cube.RenderKey).Material);
    }

    [Fact]
    public void TwoObjectsSharingATextureShareOneMaterial()
    {
        var scene = EditorScene.Starter();
        scene["cube"]!.MaterialAsset = "assets/colormap.png";
        scene["crate"]!.MaterialAsset = "assets/colormap.png";
        scene.Invalidate();

        Assert.Single(Drawn(scene).Materials, one => one.BaseColourMap is not null);
    }

    [Fact]
    public void AnObjectWithNoTextureKeepsItsOwnMaterials()
    {
        var drawn = Drawn(EditorScene.Starter());

        Assert.All(drawn.Objects, one => Assert.Null(one.Material));
        Assert.DoesNotContain(drawn.Materials, one => one.BaseColourMap is not null);
    }
}
