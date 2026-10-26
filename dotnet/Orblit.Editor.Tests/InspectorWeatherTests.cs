using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Weather;
using static Orblit.Editor.Tests.InspectorRig;

/// <summary>The inspector on a Weather object: the sky it makes, what falls out of it, and the haze.</summary>
public sealed class InspectorWeatherTests
{
    private static (EditorSession Session, Window Window) Weather(WeatherCondition condition = WeatherCondition.Clear) =>
        Selecting("sky", new EditorScene([new SceneObject("sky", "Sky", ObjectKind.Weather, condition: condition)]));

    private static void Set(Window window, string slider, double value)
    {
        Headless.Find<Slider>(window, slider).Value = value;
        Headless.Render();
    }

    [Fact]
    public Task WeatherShowsItsWeatherAndAirAndNoPosition() => Headless.Run(() =>
    {
        var (_, window) = Weather();

        Assert.True(Has(window, "section_weather"));
        Assert.True(Has(window, "section_air"));
        Assert.False(Has(window, "section_transform"));
    });

    [Fact]
    public Task AConditionSetsTheValuesItIsMadeOfAsOneUndoStep() => Headless.Run(() =>
    {
        var (session, window) = Weather();
        var sky = session.InspectedItem!;
        var was = sky.Weather;

        Headless.Click(window, "choice__rain");

        Assert.Equal(WeatherCondition.Rain, sky.Condition);
        Assert.Equal(WeatherState.Of(WeatherCondition.Rain), sky.Weather);
        session.History.Undo();
        Assert.Equal(WeatherCondition.Clear, sky.Condition);
        Assert.Equal(was, sky.Weather);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task TheSecondRowOfConditionsChoosesToo() => Headless.Run(() =>
    {
        var (session, window) = Weather();

        Headless.Click(window, "choice__storm");

        Assert.Equal(WeatherCondition.Storm, session.InspectedItem!.Condition);
    });

    [Fact]
    public Task ChoosingTheConditionItAlreadyHasIsNotAnEdit() => Headless.Run(() =>
    {
        var (session, window) = Weather();

        Headless.Click(window, "choice_condition_clear");

        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task CloudKindsAreOfferedOnlyOnceThereIsCloud() => Headless.Run(() =>
    {
        var (_, window) = Weather();
        Assert.False(Has(window, "choice_cloud_kind_auto"));

        Set(window, "slider_cloud", 0.5);

        Assert.True(Has(window, "choice_cloud_kind_auto"));
        Assert.True(Has(window, "slider_cloud_height"));
    });

    [Fact]
    public Task ACloudKindMovesTheCloudToWhereItsShapeStartsAndAutoGivesTheShapeBack() => Headless.Run(() =>
    {
        var (session, window) = Weather(WeatherCondition.Fair);
        var sky = session.InspectedItem!;

        Headless.Click(window, "choice__cirrus");

        Assert.Equal(CloudKind.Cirrus, sky.CloudKind);
        Assert.Equal(7000, sky.Weather.CloudHeight);
        Headless.Click(window, "choice_cloud_kind_auto");
        Assert.Null(sky.CloudKind);
        Assert.Equal(900, sky.Weather.CloudHeight);
    });

    [Fact]
    public Task ADragOfRainIsOneUndoStep() => Headless.Run(() =>
    {
        var (session, window) = Weather();
        var sky = session.InspectedItem!;

        Set(window, "slider_rain", 0.6);

        Assert.Equal(0.6, sky.Weather.Rain, 6);
        session.History.Undo();
        Assert.Equal(0, sky.Weather.Rain);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task TheBearingIsTheWindDirectionAndUndoReturnsIt() => Headless.Run(() =>
    {
        var (session, window) = Weather();
        var sky = session.InspectedItem!;
        var was = sky.WindDirection;

        Set(window, "slider_bearing", 90);

        Assert.Equal(90, sky.WindDirection, 6);
        session.History.Undo();
        Assert.Equal(was, sky.WindDirection);
    });

    [Fact]
    public Task HazeIsOfferedOnlyWhereThereIsDensityAndMistSizeOnlyWhereThereIsMist() => Headless.Run(() =>
    {
        var (_, window) = Weather();
        Assert.True(Has(window, "slider_height"));
        Assert.False(Has(window, "slider_mist_size"));

        Set(window, "slider_ground_mist", 0.5);
        Assert.True(Has(window, "slider_mist_size"));

        Set(window, "slider_density", 0);
        Assert.False(Has(window, "slider_height"));
        Assert.False(Has(window, "slider_mist_size"));
    });

    [Fact]
    public Task TheFogColourIsOneUndoStep() => Headless.Run(() =>
    {
        var (session, window) = Weather();
        var sky = session.InspectedItem!;
        var was = sky.Weather.FogColour;

        Headless.Click(window, "swatch_colour_1");

        Assert.NotEqual(was, sky.Weather.FogColour);
        session.History.Undo();
        Assert.Equal(was, sky.Weather.FogColour);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task ASecondWeatherObjectSaysItIsIgnored() => Headless.Run(() =>
    {
        var scene = new EditorScene(
        [
            new SceneObject("first", "First", ObjectKind.Weather),
            new SceneObject("second", "Second", ObjectKind.Weather),
        ]);

        var (_, first) = Selecting("first", scene);
        var (_, second) = Selecting("second", scene);

        Assert.DoesNotContain(Headless.All<TextBlock>(first), line => line.Text?.StartsWith("Another Weather object", StringComparison.Ordinal) == true);
        Assert.Contains(Headless.All<TextBlock>(second), line => line.Text?.StartsWith("Another Weather object", StringComparison.Ordinal) == true);
    });
}
