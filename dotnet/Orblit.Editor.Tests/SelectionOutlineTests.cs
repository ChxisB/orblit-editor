namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;

// The selection, as the renderer is told to outline it.
public sealed class SelectionOutlineTests
{
    private static readonly SceneObject Crate = new("crate", "Crate", ObjectKind.Mesh);
    private static readonly SceneObject Barrel = new("barrel", "Barrel", ObjectKind.Mesh);
    private static readonly SceneObject Sun = new("sun", "Sun", ObjectKind.Light);
    private static readonly SceneObject Eye = new("eye", "Eye", ObjectKind.Camera);
    private static readonly SceneObject Group = new("group", "Group", ObjectKind.Group);
    private static readonly EditorScene Scene = new([Crate, Barrel, Sun, Eye, Group]);

    [Fact]
    public void NothingSelectedOutlinesNothingAndSoCostsTheRendererNothing()
    {
        var outline = SelectionOutline.Of(scene: Scene, selected: []);
        Assert.True(outline.IsEmpty);
    }

    [Fact]
    public void OutlinesEachSelectedMeshByTheKeyTheRendererDrawsItBy()
    {
        var outline = SelectionOutline.Of(scene: Scene, selected: ["crate", "barrel"], primary: "barrel");

        Assert.Equal([Crate.RenderKey, Barrel.RenderKey], outline.Keys);
        Assert.Equal(Barrel.RenderKey, outline.Primary);
    }

    [Fact]
    public void LeavesOutWhatHasNoSilhouetteToFollow()
    {
        // A light, a camera and a group are drawn by nothing, so there is no shape for an outline
        // to go round. The handles show where they are.
        var outline = SelectionOutline.Of(scene: Scene, selected: ["sun", "eye", "group"], primary: "sun");
        Assert.True(outline.IsEmpty);
    }

    [Fact]
    public void KeepsTheMeshesOfAMixedSelection()
    {
        var outline = SelectionOutline.Of(scene: Scene, selected: ["sun", "crate"], primary: "sun");

        Assert.Equal([Crate.RenderKey], outline.Keys);
        // The active object is the light, which has nothing to outline; the crate is still
        // selected, and is drawn in the ordinary selection colour.
        Assert.Null(outline.Primary);
    }

    [Fact]
    public void FindsObjectsInTheSharedSetAsWellAsTheOpenScene()
    {
        var lamp = new SceneObject("lamp", "Lamp", ObjectKind.Mesh);
        var outline = SelectionOutline.Of(
            scene: Scene,
            shared: new EditorScene([lamp]),
            selected: ["lamp"],
            primary: "lamp");

        Assert.Equal(lamp.RenderKey, outline.Primary);
    }

    [Fact]
    public void IgnoresAnIdThatIsNoLongerInEitherScene()
    {
        var outline = SelectionOutline.Of(scene: Scene, selected: ["gone"], primary: "gone");
        Assert.True(outline.IsEmpty);
    }

    [Fact]
    public void NamesKeysTheRenderedSceneActuallyContains()
    {
        // The outline is only any use if its keys are the objects' keys in the scene the viewport
        // sends, otherwise it outlines nothing, silently.
        var rendered = Scene.ToRenderScene(new OrbitCamera().ToRenderCamera());
        var outline = SelectionOutline.Of(scene: Scene, selected: ["crate", "barrel"]);
        var sent = rendered.Objects.Select(o => o.Key).ToHashSet();
        Assert.True(sent.IsSupersetOf(outline.Keys));
    }
}
