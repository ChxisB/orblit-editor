namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;
using Orblit.Weather;

// A scene with its clock running, and the weather that clock drives.
public sealed class SceneWeatherTests
{
    private static RenderCamera Camera() => new OrbitCamera().ToRenderCamera();

    private static EditorScene WithSun(double hour = 12, bool cycle = true) =>
        new(
            [
                new SceneObject("sun", "Sun", ObjectKind.Light),
                new SceneObject("cube", "Cube", ObjectKind.Mesh),
            ],
            timeOfDay: hour,
            dayCycle: cycle);

    private static EditorScene WithWeather(WeatherState air, double bearing = 90, double transition = 8) =>
        new(
        [
            new SceneObject(
                "weather",
                "Weather",
                ObjectKind.Weather,
                weather: air,
                windDirection: bearing,
                transitionSeconds: transition),
            new SceneObject("sun", "Sun", ObjectKind.Light),
        ]);

    [Fact]
    public void TheHourPointsTheLightNotTheLightsOwnRotation()
    {
        var scene = WithSun(hour: 12);
        // Rotated to point sideways, which the cycle is expected to overrule.
        scene["sun"]!.Rotation.SetValues(0, 90, 0);
        scene.Invalidate();

        var light = scene.ToRenderScene(Camera()).Lights.Single();
        Assert.True(light.Direction.Y < -0.85);
    }

    [Fact]
    public void WithTheCycleOffTheLightKeepsWhatItWasAuthoredWith()
    {
        var scene = WithSun(hour: 12, cycle: false);
        var light = scene.ToRenderScene(Camera()).Lights.Single();

        // Unrotated, which is straight down the view axis rather than down from the sky. Turning
        // the cycle off puts the scene back the way it was.
        Assert.Equal(-1, light.Direction.Z, 1e-9);
    }

    [Fact]
    public void TheCameraOpensUpWhenTheSceneRunsIntoTheNight()
    {
        var day = WithSun(hour: 12).ToRenderScene(Camera());
        var night = WithSun(hour: 0).ToRenderScene(Camera());

        Assert.True(night.Camera.Sensitivity > day.Camera.Sensitivity);
        Assert.True(night.Camera.Aperture < day.Camera.Aperture);
    }

    [Fact]
    public void TheCameraIsSetForTheLightTheSceneHasNotForTheHour()
    {
        // How this went wrong the first time: the exposure came from what the clock said and the
        // light came from the scene, and the two disagreed. A bright light at midnight left the
        // viewport a white rectangle with the shapes barely showing through it.
        var scene = new EditorScene(
            [
                new SceneObject("sun", "Sun", ObjectKind.Light),
                // A second directional light, which the cycle does not drive: it keeps the
                // strength it was given, and it is what is actually lighting the scene.
                new SceneObject("floods", "Floods", ObjectKind.Light, power: 200, rotation: new Vector3(-90, 0, 0)),
            ],
            timeOfDay: 0,
            dayCycle: true);

        var camera = scene.ToRenderScene(Camera()).Camera;

        // Stopped down for daylight rather than opened up for a moon that is not what anybody is
        // looking at.
        Assert.Equal(100, camera.Sensitivity);
        Assert.True(camera.Aperture > 8);
    }

    [Fact]
    public void ASceneLitOnlyByAMoonOpensTheCameraUp()
    {
        var camera = WithSun(hour: 0).ToRenderScene(Camera()).Camera;

        Assert.True(camera.Aperture < 2);
        Assert.True(camera.Sensitivity > 1000);
    }

    [Fact]
    public void TheSkyIsTheScenesOwnUntilTheCycleTakesItOver()
    {
        var held = WithSun(cycle: false);
        held.SkyColour = new Colour(0xFF123456);
        var driven = WithSun(hour: 12);
        driven.SkyColour = new Colour(0xFF123456);

        var camera = Camera();
        var linear = EditorScene.LinearFromColour(new Colour(0xFF123456)).X;
        Assert.Equal(linear, held.ToRenderScene(camera).Sky.Colour.X);
        Assert.NotEqual(linear, driven.ToRenderScene(camera).Sky.Colour.X);
    }

    [Fact]
    public void TheLightIsNamedForWhateverIsAboveTheHorizon()
    {
        var scene = WithSun(hour: 12);
        Assert.Equal("Sun", scene.DisplayNameOf(scene["sun"]!));

        scene.TimeOfDay = 0;
        Assert.Equal("Moon", scene.DisplayNameOf(scene["sun"]!));
    }

    [Fact]
    public void ALightSomebodyHasNamedKeepsItsName()
    {
        var scene = WithSun(hour: 0);
        scene["sun"]!.Name = "Key light";
        Assert.Equal("Key light", scene.DisplayNameOf(scene["sun"]!));
    }

    [Fact]
    public void OnlyTheLightAboveTheSceneFollowsTheSky()
    {
        var scene = WithSun(hour: 0);
        scene.Add(new SceneObject("lamp", "Sun", ObjectKind.Light, lightType: LightType.Point));

        // Named the same and of the same mind about it, but it is not the one lighting the scene
        // from above, so it is left alone.
        Assert.Equal("Sun", scene.DisplayNameOf(scene["lamp"]!));
    }

    [Fact]
    public void TheClockIsWhereTheDayHasGotToNotWhereItWasSaved()
    {
        var scene = WithSun(hour: 6);
        scene.HoursPerSecond = 1;
        scene.Clock = 4;

        Assert.Equal(10, scene.CurrentTimeOfDay, 1e-9);
        // The authored hour is untouched, so saving does not record wherever a clock left running
        // happened to reach.
        Assert.Equal(6, scene.TimeOfDay);
    }

    [Fact]
    public void ASceneWithNothingMovingDoesNotAskToBeAnimated()
    {
        Assert.False(WithSun(cycle: false).IsAnimated);
        Assert.True(WithSun().IsAnimated);
    }

    [Fact]
    public void MistAsksForShapeAsWellAsHaze()
    {
        var scene = WithWeather(WeatherState.Of(WeatherCondition.Misty) with { MistSize = 40 });
        var fog = scene.ToRenderScene(Camera()).Fog;

        Assert.True(fog.Structure > 0);
        // Stated in metres and sent as turns per metre.
        Assert.Equal(1.0 / 40, fog.FeatureSize, 1e-9);
        // The even haze is still under it: banks with no haze behind them read as cut-outs hanging
        // in clear air.
        Assert.True(fog.Density > 0);
        Assert.True(fog.Thickness > 0);
    }

    [Fact]
    public void ClearWeatherAsksForNoShapeAtAll()
    {
        var fog = WithWeather(WeatherState.Of(WeatherCondition.Clear)).ToRenderScene(Camera()).Fog;
        Assert.Equal(0, fog.Structure);
    }

    [Fact]
    public void ASceneWithNoWeatherInItHasClearAir()
    {
        var scene = new EditorScene([new SceneObject("cube", "Cube", ObjectKind.Mesh)]);
        Assert.False(scene.ToRenderScene(Camera()).Fog.IsVisible);
    }

    [Fact]
    public void HidingTheWeatherClearsTheAir()
    {
        var scene = WithWeather(WeatherState.Of(WeatherCondition.Storm));
        scene["weather"]!.Visible = false;

        Assert.False(scene.ToRenderScene(Camera()).Fog.IsVisible);
    }

    [Fact]
    public void TheWindBlowsTheWayTheBearingSays()
    {
        var east = WithWeather(WeatherState.Of(WeatherCondition.Storm), bearing: 90).ToRenderScene(Camera()).Fog.Wind;
        var north = WithWeather(WeatherState.Of(WeatherCondition.Storm), bearing: 0).ToRenderScene(Camera()).Fog.Wind;

        Assert.True(east.X > Math.Abs(east.Y));
        Assert.True(north.Y > Math.Abs(north.X));
        // And it carries the speed of the weather it belongs to.
        Assert.Equal(WeatherState.Of(WeatherCondition.Storm).WindSpeed, east.Length, 1e-9);
    }

    [Fact]
    public void CloudTakesTheStrengthOutOfWhatIsAboveTheScene()
    {
        var clear = WithWeather(WeatherState.Of(WeatherCondition.Clear));
        var covered = WithWeather(WeatherState.Of(WeatherCondition.Overcast));

        var bright = clear.ToRenderScene(Camera()).Lights.Single();
        var dull = covered.ToRenderScene(Camera()).Lights.Single();

        Assert.True(dull.Intensity < bright.Intensity * 0.3);
        // And widens it, which is what softens the shadows.
        Assert.True(dull.SunAngularRadius > bright.SunAngularRadius * 10);
    }

    [Fact]
    public void CloudIsDrawnInTheSkyNotInTheFog()
    {
        // The two are different weather and were being confused: what was called cloud only dimmed
        // the light, and what was drawn was the mist at ground level. Cover now puts something
        // overhead.
        var clear = WithWeather(WeatherState.Of(WeatherCondition.Clear));
        var covered = WithWeather(WeatherState.Of(WeatherCondition.Overcast));

        Assert.False(clear.ToRenderScene(Camera()).Sky.Clouds.IsVisible);

        var sky = covered.ToRenderScene(Camera()).Sky.Clouds;
        Assert.True(sky.IsVisible);
        Assert.True(sky.Cover > 0.5);
        // Overhead, in metres, rather than anywhere near the ground.
        Assert.True(sky.Altitude > 40);
        // And carried faster than anything at ground level is.
        Assert.True(sky.Wind.Length > covered.ToRenderScene(Camera()).Fog.Wind.Length);
    }

    [Fact]
    public void TheCloudIsLitFromWhereTheLightActuallyComesFrom()
    {
        // The whole point of the sky being one object: a sun drawn in one place and a cloud lit
        // from another is the single thing that gives a sky away, and it can only be got wrong if
        // they are two objects.
        var scene = WithWeather(WeatherState.Of(WeatherCondition.Fair));
        var rendered = scene.ToRenderScene(Camera());

        var beam = rendered.Lights.First(light => light.Kind == RenderLightKind.Directional);

        // Towards the body is away from where its light travels.
        var towards = -beam.Direction;
        towards.Normalize();
        Assert.True((rendered.Sky.BodyDirection - towards).Length < 0.001);
    }

    [Fact]
    public void EachShapeOfCloudIsADifferentShape()
    {
        // Not presets of one shape with the numbers moved: they differ in how high the base sits
        // and how deep the layer is, and no slider reaches either.
        var cumulus = Clouds.Cumulus with { Cover = 0.5 };
        var cirrus = Clouds.Cirrus with { Cover = 0.5 };
        var storm = Clouds.Cumulonimbus with { Cover = 0.5 };

        // Ice needs the cold seven kilometres up; cumulus condense far lower.
        Assert.True(cirrus.Altitude > cumulus.Altitude * 4);
        // A thunderhead is deep. That depth is why its base is dark.
        Assert.True(storm.Thickness > cumulus.Thickness * 3);
        // And it is nearly a smooth sheet at one end and a cauliflower at the other.
        Assert.True((Clouds.Stratus with { Cover = 0.5 }).Billow < 0.2);
        Assert.True(cumulus.Billow > 0.7);
    }

    [Fact]
    public void AConditionBringsItsOwnShapeAndAChoiceOverrulesIt()
    {
        var scene = WithWeather(WeatherState.Of(WeatherCondition.Storm));
        var weather = scene.Objects.First(one => one.Kind == ObjectKind.Weather);
        weather.Condition = WeatherCondition.Storm;

        // A storm is a thunderhead, and a thunderhead is deep.
        var automatic = scene.ToRenderScene(Camera()).Sky.Clouds;
        Assert.True(automatic.Thickness > 2000);

        // Until somebody says otherwise, at which point it is a different shape: streaks of ice
        // rather than a folded cauliflower.
        weather.CloudKind = CloudKind.Cirrus;
        var chosen = scene.ToRenderScene(Camera()).Sky.Clouds;
        Assert.True(chosen.Thickness < automatic.Thickness / 2);
        Assert.True(chosen.Density < automatic.Density / 3);

        // Including saying there should be none.
        weather.CloudKind = CloudKind.None;
        Assert.False(scene.ToRenderScene(Camera()).Sky.Clouds.IsVisible);
    }

    [Fact]
    public void TheGroundMistAndTheSkyAreSetApart()
    {
        var scene = WithWeather(WeatherState.Of(WeatherCondition.Misty) with { CloudCover = 0 });
        var rendered = scene.ToRenderScene(Camera());

        // Mist near the ground with a clear sky over it is a real morning, and it has to be
        // possible to ask for it.
        Assert.True(rendered.Fog.Structure > 0);
        Assert.False(rendered.Sky.Clouds.IsVisible);
    }

    [Fact]
    public void CloudPutsThatLightBackAsSky()
    {
        var clear = WithWeather(WeatherState.Of(WeatherCondition.Clear));
        var covered = WithWeather(WeatherState.Of(WeatherCondition.Overcast));

        Assert.True(covered.ToRenderScene(Camera()).Sky.Ambient > clear.ToRenderScene(Camera()).Sky.Ambient * 2);
    }

    [Fact]
    public void AChangeOfConditionArrivesOverTimeRatherThanAtOnce()
    {
        var scene = WithWeather(WeatherState.Of(WeatherCondition.Clear), transition: 10);
        var weather = scene["weather"]!;

        weather.BlendFrom = WeatherState.Of(WeatherCondition.Clear);
        weather.BlendSince = 0;
        weather.Weather = WeatherState.Of(WeatherCondition.Storm);

        scene.Clock = 0;
        Assert.Equal(WeatherState.Of(WeatherCondition.Clear).CloudCover, scene.WeatherNow!.CloudCover, 1e-9);

        scene.Clock = 5;
        var half = scene.WeatherNow!.CloudCover;
        Assert.True(half > 0.4);
        Assert.True(half < 0.6);

        scene.Clock = 11;
        Assert.Equal(1, scene.WeatherNow!.CloudCover);
    }

    [Fact]
    public void ASceneInTheMiddleOfChangingItsWeatherIsAnimated()
    {
        var scene = WithWeather(WeatherState.Of(WeatherCondition.Clear), transition: 10);
        var weather = scene["weather"]!;
        weather.BlendFrom = WeatherState.Of(WeatherCondition.Storm);
        weather.BlendSince = 0;

        scene.Clock = 2;
        Assert.True(scene.IsAnimated);

        // Settled: the renderer moves the cloud on its own clock from here, so the editor has
        // nothing left to tick for.
        scene.Clock = 20;
        Assert.False(scene.IsAnimated);
    }

    [Fact]
    public void RainFallsAsStreaksAndSnowAsFlakes()
    {
        var wet = WithWeather(WeatherState.Of(WeatherCondition.Rain)).ToRenderScene(Camera()).Precipitation;
        var white = WithWeather(WeatherState.Of(WeatherCondition.Snow)).ToRenderScene(Camera()).Precipitation;

        Assert.True(wet.IsVisible);
        Assert.True(white.IsVisible);

        // How far a drop travels while the shutter is open, which is the whole difference between
        // the two. A flake is not a point, it has to be large enough to see, so this is a ratio
        // rather than an order of magnitude.
        Assert.True(wet.Stretch > white.Stretch * 4);
        Assert.True(wet.Fall > white.Fall * 5);
        // And there are more drops in rain than flakes in snow.
        Assert.True(wet.DropsPerMetre > white.DropsPerMetre);
    }

    [Fact]
    public void DryWeatherDrawsNoCurtainAtAll()
    {
        var dry = WithWeather(WeatherState.Of(WeatherCondition.Overcast)).ToRenderScene(Camera()).Precipitation;
        Assert.False(dry.IsVisible);
    }

    [Fact]
    public void HalfOfEachIsOneCurtainHalfwayBetween()
    {
        var both = WithWeather(WeatherState.Of(WeatherCondition.Rain) with { Rain = 0.4, Snow = 0.4 })
            .ToRenderScene(Camera())
            .Precipitation;

        var rain = WithWeather(WeatherState.Of(WeatherCondition.Rain) with { Rain = 0.8, Snow = 0 })
            .ToRenderScene(Camera())
            .Precipitation;

        Assert.Equal(rain.Amount, both.Amount, 1e-9);
        // Falling at something between the two speeds rather than at either.
        Assert.True(both.Fall < rain.Fall);
        Assert.True(both.Fall > 1);
    }

    [Fact]
    public void TheWindCarriesSnowFurtherThanItCarriesRain()
    {
        var air = WeatherState.Of(WeatherCondition.Rain) with { WindSpeed = 6 };
        var wet = WithWeather(air).ToRenderScene(Camera()).Precipitation.Wind.Length;
        var white = WithWeather(air with { Rain = 0, Snow = 0.65 }).ToRenderScene(Camera()).Precipitation.Wind.Length;

        Assert.True(white > wet);
    }

    [Fact]
    public void LightningStrikesAndMostOfTheTimeItDoesNot()
    {
        var struck = 0;
        var brightest = 0.0;

        // Ten minutes of a storm, ten times a second.
        for (var step = 0; step < 6000; step++)
        {
            var flash = WeatherState.FlashAt(step / 10.0, 0.6);
            if (flash > 0.01)
            {
                struck++;
            }

            if (flash > brightest)
            {
                brightest = flash;
            }
        }

        Assert.True(brightest > 0.5);
        // Lit for a fraction of the time. A storm that was bright half the night would be a lamp.
        Assert.True(struck / 6000.0 < 0.15);
        Assert.True(struck > 0);
    }

    [Fact]
    public void TheSameSecondOfTheSameStormLooksTheSameTwice()
    {
        Assert.Equal(WeatherState.FlashAt(42.5, 0.6), WeatherState.FlashAt(42.5, 0.6));
        Assert.Equal(0, WeatherState.FlashAt(42.5, 0));
    }

    [Fact]
    public void AStrikeBrightensWhatIsAboveTheScene()
    {
        var scene = WithWeather(WeatherState.Of(WeatherCondition.Storm));

        // Walk until one lands, then compare that instant with a dark one.
        var lit = 0.0;
        for (var step = 0; step < 4000; step++)
        {
            var at = step / 20.0;
            if (WeatherState.FlashAt(at, WeatherState.Of(WeatherCondition.Storm).Lightning) > 0.5)
            {
                lit = at;
                break;
            }
        }

        Assert.True(lit > 0, "no strike inside three minutes");

        scene.Clock = 0.05;
        var dark = scene.ToRenderScene(Camera());
        scene.Clock = lit;
        var flash = scene.ToRenderScene(Camera());

        Assert.True(flash.Lights.Single().Intensity > dark.Lights.Single().Intensity * 5);
        Assert.True(flash.Sky.Ambient > dark.Sky.Ambient * 5);
    }

    [Fact]
    public void AStormKeepsTheEditorTickingSoTheStrikesAreSeen()
    {
        var quiet = WithWeather(WeatherState.Of(WeatherCondition.Overcast));
        var storm = WithWeather(WeatherState.Of(WeatherCondition.Storm));

        Assert.False(quiet.IsAnimated);
        Assert.True(storm.IsAnimated);
    }

    [Fact]
    public void OneSceneOneAnswerAboutTheWeather()
    {
        var scene = WithWeather(WeatherState.Of(WeatherCondition.Storm));
        scene.Add(new SceneObject(
            "second",
            "Weather",
            ObjectKind.Weather,
            weather: WeatherState.Of(WeatherCondition.Clear)));

        Assert.True(scene.HasSpareWeather);
        // The first one is it, and the second is ignored rather than blended with or fought over.
        Assert.Equal(WeatherState.Of(WeatherCondition.Storm).Mist, scene.WeatherNow!.Mist);
    }
}
