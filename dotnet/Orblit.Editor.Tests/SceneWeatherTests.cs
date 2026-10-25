namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Light;
using Orblit.Weather;

// A scene with its clock running, and the weather that clock drives. The tests that read the
// render scene belong to the viewport task.
public sealed class SceneWeatherTests
{
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
