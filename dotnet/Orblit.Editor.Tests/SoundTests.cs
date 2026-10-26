namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// Sound on objects, drawn as the range it carries.
public sealed class SoundTests
{
    private static EditorScene Scene(Doc.SoundSourceComponent? source = null)
    {
        var parts = new OrderedDictionary<string, Doc.SceneComponent>();
        if (source is not null)
        {
            parts[Doc.SceneComponents.SoundSource] = source;
        }

        return new EditorScene(
        [
            new SceneObject("fire", "Fire", ObjectKind.Group, position: new Vector3(0, 1, 0), components: parts),
            new SceneObject("rock", "Rock", ObjectKind.Group),
            new SceneObject("sky", "Sky", ObjectKind.Weather),
        ]);
    }

    [Fact]
    public void IsThereForASelectionWithASourceAndOnlyThen()
    {
        var world = Scene(new Doc.SoundSourceComponent());
        var gizmo = SoundGizmo.Type();

        Assert.False(gizmo.AppliesTo(GizmoView.TargetOf(world, "rock")));
        Assert.True(gizmo.AppliesTo(GizmoView.TargetOf(world, "fire")));
        Assert.True(gizmo.AppliesTo(GizmoView.TargetOf(world, "rock", "fire")));
    }

    [Fact]
    public void DrawsARingForEachEndOfTheRange()
    {
        var world = Scene(new Doc.SoundSourceComponent(near: 1, far: 4));

        // The far ring is drawn first so the near one reads over it.
        var strokes = SoundGizmo.Type().Overlay!(GizmoView.TargetOf(world, "fire"));
        var far = strokes[0].Lines.Bounds!.Value;
        var near = strokes[1].Lines.Bounds!.Value;
        var centre = GizmoView.Projection.Project(new Vector3(0, 1, 0))!.Value;

        Assert.True(far.Contains(centre));
        Assert.True(near.Contains(centre));
        Assert.True(far.Width > near.Width * 2);
        Assert.True(far.Contains(new Offset(near.Left, near.Top)) && far.Contains(new Offset(near.Right, near.Bottom)));
    }

    [Fact]
    public void SoundComesBackFromTheSceneFileAsItWasSaved()
    {
        var source = new Doc.SoundSourceComponent(clip: "sounds/fire.ogg", bus: "sfx", loop: false, volume: 0.5, near: 2, far: 30, curve: Doc.SoundCurve.Linear);
        var world = Scene(source);
        world["fire"]!.Components[Doc.SceneComponents.SoundListener] = new Doc.SoundListenerComponent();
        world["fire"]!.Components[Doc.SceneComponents.SoundSurface] = new Doc.SoundSurfaceComponent(name: "wood");

        var back = SceneDocument.Decode(SceneDocument.Encode(world)).Scene["fire"]!;

        Assert.Equal(source.ToJson(), PhysicsComponents.SoundSourceOf(back)!.ToJson());
        Assert.True(back.Components.ContainsKey(Doc.SceneComponents.SoundListener));
        Assert.Equal("wood", ((Doc.SoundSurfaceComponent)back.Components[Doc.SceneComponents.SoundSurface]).Name);
    }
}
