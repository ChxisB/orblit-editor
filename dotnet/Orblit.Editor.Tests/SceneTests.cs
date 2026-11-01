namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Filament;
using Orblit.Light;
using Orblit.Weather;

public sealed class SceneTests
{
    private static RenderCamera Camera() => new OrbitCamera().ToRenderCamera();

    private static EditorScene Hedge(string texture = "leaves.png", double sway = 1, params SceneObject[] more) =>
        new(
        [
            .. more,
            new SceneObject("hedge", "Hedge", ObjectKind.Mesh, materialAsset: texture, sway: sway),
        ]);

    private static SceneObject Storm(double bearing = 135) =>
        new(
            "air",
            "Weather",
            ObjectKind.Weather,
            condition: WeatherCondition.Storm,
            windDirection: bearing);

    [Fact]
    public void SendsOnlyMeshesToTheRenderer()
    {
        var scene = EditorScene.Starter();
        var rendered = scene.ToRenderScene(Camera());

        Assert.Equal(scene.Objects.Count(o => o.Kind == ObjectKind.Mesh), rendered.Objects.Count);
        // The sun, the camera and the scene root have no geometry; sending them would draw a cube
        // where the light is.
        Assert.True(rendered.Objects.Count < scene.Objects.Count);
    }

    [Fact]
    public void ColourIsConvertedOutOfSrgbBecauseShadingIsLinear()
    {
        // Mid grey in sRGB is a good deal darker than a half in linear.
        var linear = EditorScene.LinearFromColour(new Colour(0xFF808080));
        Assert.Equal(0.2158, linear.X, 0.001);
    }

    [Fact]
    public void PointsALightWhereItsRotationPoints()
    {
        var scene = new EditorScene([new SceneObject("sun", "Sun", ObjectKind.Light)]);
        // Unrotated, forward is -Z: light falling straight down the view axis.
        var direction = scene.ToRenderScene(Camera()).Lights.Single().Direction;
        Assert.Equal(-1, direction.Z, 1e-9);
    }

    [Fact]
    public void StatesASunInLuxAndABulbInLumens()
    {
        var scene = new EditorScene(
        [
            new SceneObject("sun", "Sun", ObjectKind.Light, power: 100),
            new SceneObject("bulb", "Bulb", ObjectKind.Light, lightType: LightType.Point, power: 100),
        ]);

        var lights = scene.ToRenderScene(Camera()).Lights;
        var sun = lights.First(l => l.Kind == RenderLightKind.Directional);
        var bulb = lights.First(l => l.Kind == RenderLightKind.Point);

        // The same number of watts means two different things, and the units are the whole reason
        // the conversion lives in one place.
        Assert.Equal(100 * 683, sun.Intensity, 1);
        Assert.Equal(100 * 683, bulb.Intensity, 1);
        // A sun does not fall off, so it is sent no radius to fall off within.
        Assert.Equal(0, sun.FalloffRadius);
        Assert.True(bulb.FalloffRadius > 0);
    }

    [Fact]
    public void ASurfaceThatSwaysTakesTheWindFromTheWeather()
    {
        var scene = Hedge(more: Storm(bearing: 90));

        var material = scene.ToRenderScene(Camera()).Materials.Single();

        // The bearing comes from the weather object and the speed from the condition, so the trees
        // lean the way the rain already falls.
        Assert.True(material.Wind.Moves);
        Assert.Equal(90, material.Wind.Bearing);
        Assert.Equal(1, material.Wind.Strength);
        Assert.True(material.Wind.Speed > 0);
    }

    [Fact]
    public void ASceneWithNoWeatherLeavesEverythingRigid()
    {
        var material = Hedge().ToRenderScene(Camera()).Materials.Single();

        // Nothing is doing any weather, so there is no wind to answer, and a surface that claims to
        // sway in still air would sway for ever.
        Assert.Equal(Wind.None, material.Wind);
    }

    [Fact]
    public void OneTextureOnTwoSwayingDifferentlyIsTwoMaterials()
    {
        // The trap this guards: materials used to be keyed by texture alone, so the trunk and the
        // canopy cut from the same bark would have shared one instance, and whichever was built
        // second would have decided how both of them moved.
        var scene = new EditorScene(
        [
            new SceneObject("air", "Weather", ObjectKind.Weather, condition: WeatherCondition.Storm),
            new SceneObject("trunk", "Trunk", ObjectKind.Mesh, materialAsset: "bark.png", sway: 0.1),
            new SceneObject("canopy", "Canopy", ObjectKind.Mesh, materialAsset: "bark.png", sway: 1),
        ]);

        var rendered = scene.ToRenderScene(Camera());
        Assert.Equal(2, rendered.Materials.Count);

        var strengths = rendered.Materials.Select(m => m.Wind.Strength).Order().ToList();
        Assert.Equal([0.1, 1.0], strengths);

        // And each object names the one that describes it.
        var trunk = rendered.Objects.First(o => o.Key == scene["trunk"]!.RenderKey);
        var canopy = rendered.Objects.First(o => o.Key == scene["canopy"]!.RenderKey);
        Assert.NotEqual(trunk.Material, canopy.Material);
    }

    [Fact]
    public void TwoRigidObjectsOnOneTextureStillShareAMaterial()
    {
        // The other half of the same rule: keying by more than the texture must not stop the
        // ordinary case from sharing, or a scene of a hundred identical crates becomes a hundred
        // material instances.
        var scene = new EditorScene(
        [
            new SceneObject("a", "A", ObjectKind.Mesh, materialAsset: "crate.png"),
            new SceneObject("b", "B", ObjectKind.Mesh, materialAsset: "crate.png"),
        ]);

        Assert.Single(scene.ToRenderScene(Camera()).Materials);
    }

    [Fact]
    public void AnAreaLightArrivesAsARectangleNotAsAPoint()
    {
        var scene = new EditorScene(
        [
            new SceneObject("panel", "Panel", ObjectKind.Light, lightType: LightType.Area, power: 100, sourceRadius: 0.5),
        ]);

        var light = scene.ToRenderScene(Camera()).Lights.Single();

        // It used to arrive as a point with a wide source, because the renderer had nowhere to put
        // a rectangle. It has one now, and the difference is not cosmetic: a point of the same
        // power is integrated against a direction, a rectangle against its own area, so the shape
        // of the highlight and the gradient of the shadow edge both come out of the panel's
        // proportions rather than out of one radius.
        Assert.Equal(RenderLightKind.Area, light.Kind);
        Assert.Equal(100 * 683, light.Intensity, 1);
    }

    [Fact]
    public void ALightThatHasNoSizeIsStillGivenOne()
    {
        // The light package leaves width and height at zero for every kind that has no size, and
        // zero would reach the renderer as a panel with no area to integrate, which is a light that
        // emits nothing. Anything that is not an area light carries the renderer's own default
        // instead.
        var scene = new EditorScene(
        [
            new SceneObject("bulb", "Bulb", ObjectKind.Light, lightType: LightType.Point, power: 100),
        ]);

        var light = scene.ToRenderScene(Camera()).Lights.Single();
        Assert.True(light.Width > 0);
        Assert.True(light.Height > 0);
    }

    [Fact]
    public void HidingAGroupHidesWhatIsInsideIt()
    {
        var scene = EditorScene.Starter();
        scene["props"]!.Visible = false;

        var rendered = scene.ToRenderScene(Camera());
        var cube = rendered.Objects.First(o => o.Key == scene["cube"]!.RenderKey);

        // The cube's own flag was never touched. It is hidden because the thing it sits in is.
        Assert.True(scene["cube"]!.Visible);
        Assert.False(cube.Visible);
    }

    [Fact]
    public void AHiddenLightIsLeftOutRatherThanSentDark()
    {
        var scene = new EditorScene([new SceneObject("sun", "Sun", ObjectKind.Light, visible: false)]);

        Assert.Empty(scene.ToRenderScene(Camera()).Lights);
    }

    [Fact]
    public void AnObjectKeepsTheKeyTheRendererKnowsItBy()
    {
        var scene = EditorScene.Starter();
        var before = scene.ToRenderScene(Camera()).Objects.Select(o => o.Key).ToList();

        scene["cube"]!.Position.SetValues(3, 0, 0);
        scene.Invalidate();

        var after = scene.ToRenderScene(Camera()).Objects.Select(o => o.Key).ToList();

        // The whole point: an edit is the same objects in new places, so the renderer moves them
        // rather than building the scene again.
        Assert.Equal(before, after);
    }

    [Fact]
    public void ACopyIsANewObjectNotTheSameOneSomewhereElse()
    {
        var original = new SceneObject("a", "A", ObjectKind.Mesh);
        var copy = original.CopyAs("b");

        Assert.NotEqual(original.RenderKey, copy.RenderKey);
    }

    [Fact]
    public void AnEditToAnObjectReachesTheNextRenderedScene()
    {
        var scene = EditorScene.Starter();
        scene["cube"]!.Position.SetValues(3, 0, 0);
        scene.Invalidate();

        var rendered = scene.ToRenderScene(Camera());
        var translations = rendered.Objects.Select(o => o.Transform.GetTranslation().X);
        Assert.Contains(3.0, translations);
    }

    // What every scene has: the shared set drawn beside the open scene.
    private static EditorScene Shared(params SceneObject[] objects) => new([.. objects]);

    [Fact]
    public void SharedObjectsAreDrawnAlongsideTheOpenScene()
    {
        var open = new EditorScene([new SceneObject("cube", "Cube", ObjectKind.Mesh)]);
        var shared = Shared(new SceneObject("prop", "Prop", ObjectKind.Mesh));

        var drawn = open.ToRenderScene(Camera(), new SceneRenderOptions { Shared = shared }).Objects;
        Assert.Equal(2, drawn.Count);
        // By key, because that is the only thing the renderer knows either of them by, and two
        // scenes' objects must never collide in it.
        Assert.Equal(
            new HashSet<long> { open["cube"]!.RenderKey, shared["prop"]!.RenderKey },
            drawn.Select(o => o.Key).ToHashSet());
    }

    [Fact]
    public void ASharedLightLightsTheOpenScene()
    {
        var open = new EditorScene([new SceneObject("cube", "Cube", ObjectKind.Mesh)]);
        var shared = Shared(new SceneObject("sun", "Sun", ObjectKind.Light));
        var options = new SceneRenderOptions { Shared = shared };

        Assert.Single(open.ToRenderScene(Camera(), options).Lights);
        // And is what the sky draws its disk for.
        Assert.True(open.ToRenderScene(Camera(), options).Sky.ShowBody);
    }

    [Fact]
    public void SharedWeatherIsWhatTheSceneIsInUntilItHasItsOwn()
    {
        var open = new EditorScene([]);
        var shared = Shared(
            new SceneObject("weather", "Weather", ObjectKind.Weather, weather: WeatherState.Of(WeatherCondition.Storm)));
        var options = new SceneRenderOptions { Shared = shared };

        Assert.True(open.ToRenderScene(Camera(), options).Precipitation.IsVisible);

        // A scene that has its own overrules it, so a level can be dry inside a project that
        // rains.
        var dry = new EditorScene(
        [
            new SceneObject("weather", "Weather", ObjectKind.Weather, weather: WeatherState.Of(WeatherCondition.Clear)),
        ]);
        Assert.False(dry.ToRenderScene(Camera(), options).Precipitation.IsVisible);
    }

    [Fact]
    public void ASceneWithItsOwnSunKeepsIt()
    {
        var open = new EditorScene([new SceneObject("stage", "Stage", ObjectKind.Light, power: 400)]);
        var shared = Shared(new SceneObject("sun", "Sun", ObjectKind.Light, power: 50));

        var lights = open.ToRenderScene(Camera(), new SceneRenderOptions { Shared = shared }).Lights;
        // Both are sent, because two lights are two lights, but the scene's own is the one the sky
        // and the day cycle are about.
        Assert.Equal(2, lights.Count);
        Assert.True(lights[0].Intensity > lights[^1].Intensity);
    }

    [Fact]
    public void HidingSomethingSharedHidesItEverywhere()
    {
        var open = new EditorScene([]);
        var shared = Shared(new SceneObject("prop", "Prop", ObjectKind.Mesh, visible: false));

        var drawn = open.ToRenderScene(Camera(), new SceneRenderOptions { Shared = shared }).Objects;
        Assert.False(drawn.Single().Visible);
    }
}
