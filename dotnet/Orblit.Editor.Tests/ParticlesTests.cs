namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// An emitter on an object, carried through the scene file.
public sealed class ParticlesTests
{
    [Fact]
    public void AnEmitterComesBackFromTheSceneFileAsItWasSaved()
    {
        var emitter = new Doc.ParticleEmitterComponent(
            shape: Doc.EmitterShape.Cone,
            rate: 40,
            lifetimeMin: 0.5,
            lifetimeMax: 3,
            gravity: new Vector3(0, -9.8, 0),
            space: Doc.EmitterSpace.Local);
        var parts = new OrderedDictionary<string, Doc.SceneComponent> { [Doc.SceneComponents.Particles] = emitter };
        var world = new EditorScene(
        [
            new SceneObject("fire", "Fire", ObjectKind.Group, position: new Vector3(0, 1, 0), components: parts),
            new SceneObject("sky", "Sky", ObjectKind.Weather),
        ]);

        var back = SceneDocument.Decode(SceneDocument.Encode(world)).Scene["fire"]!;

        Assert.Equal(emitter.ToJson(), PhysicsComponents.Of<Doc.ParticleEmitterComponent>(back, Doc.SceneComponents.Particles)!.ToJson());
    }
}
