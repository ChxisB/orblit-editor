namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Editor.Viewport;
using Orblit.Weather;

// The weather model, tested apart from the panel.
public sealed class WeatherModelTests
{
    [Fact]
    public void TheConditionsSplitAtTheOnesYouCanSeeThrough()
    {
        Assert.Equal(["Clear", "Fair", "Hazy", "Misty"], WeatherModel.ConditionRow(0));
        Assert.Equal(["Overcast", "Rain", "Storm", "Snow"], WeatherModel.ConditionRow(1));
    }

    [Fact]
    public void EveryConditionIsOnOneRowAndAnswersToItsLabel() =>
        Assert.All(
            Enum.GetValues<WeatherCondition>(),
            condition => Assert.Equal(condition, WeatherModel.ConditionFor(condition.Label())));

    [Theory]
    [InlineData(null, 0, "Auto")]
    [InlineData(null, 1, "")]
    [InlineData(CloudKind.Cumulus, 0, "Cumulus")]
    [InlineData(CloudKind.Cumulus, 1, "")]
    [InlineData(CloudKind.Cirrus, 0, "")]
    [InlineData(CloudKind.Cirrus, 1, "Cirrus")]
    [InlineData(CloudKind.None, 0, "")]
    [InlineData(CloudKind.None, 1, "")]
    public void OnlyOneOfTheTwoCloudRowsLightsAChoice(CloudKind? kind, int row, string lit) =>
        Assert.Equal(lit, WeatherModel.CloudLabel(kind, row));

    [Fact]
    public void AutoIsNoShapeAndEveryOtherLabelIsItsShape()
    {
        Assert.Null(WeatherModel.CloudKindFor("Auto"));
        Assert.Equal(CloudKind.Stratocumulus, WeatherModel.CloudKindFor("Stratocumulus"));
        Assert.All(
            WeatherModel.CloudRow(0).Concat(WeatherModel.CloudRow(1)).Where(label => label != "Auto"),
            label => Assert.Equal(label, WeatherModel.CloudKindFor(label)!.Value.Label()));
    }

    [Fact]
    public void ASecondWeatherObjectIsSpareAndTheFirstIsNot()
    {
        var first = new SceneObject(id: "a", name: "A", kind: ObjectKind.Weather);
        var second = new SceneObject(id: "b", name: "B", kind: ObjectKind.Weather);
        var scene = new EditorScene([first, second]);

        Assert.False(WeatherModel.IsSpare(scene, first));
        Assert.True(WeatherModel.IsSpare(scene, second));
        Assert.False(WeatherModel.IsSpare(new EditorScene([first]), first));
    }

    [Fact]
    public void TheCaptionNamesTheOneThingWorthSaying()
    {
        var dry = WeatherState.Of(WeatherCondition.Clear);
        var both = dry with { Rain = 0.4, Snow = 0.4 };

        Assert.StartsWith("Cloud takes the strength", WeatherModel.Caption(dry, spare: false), StringComparison.Ordinal);
        Assert.StartsWith("Both at once", WeatherModel.Caption(both, spare: false), StringComparison.Ordinal);
        Assert.StartsWith("Another Weather object", WeatherModel.Caption(both, spare: true), StringComparison.Ordinal);
    }

    [Fact]
    public void TheAirNoteTalksAboutMistOnlyOnceThereIsSome()
    {
        Assert.StartsWith("Density is", WeatherModel.AirNote(0), StringComparison.Ordinal);
        Assert.StartsWith("Mist is", WeatherModel.AirNote(0.2), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, 900)]
    [InlineData(CloudKind.None, 900)]
    [InlineData(CloudKind.Cumulus, 900)]
    [InlineData(CloudKind.Stratocumulus, 700)]
    [InlineData(CloudKind.Stratus, 480)]
    [InlineData(CloudKind.Cirrus, 7000)]
    [InlineData(CloudKind.Cumulonimbus, 600)]
    public void ACloudShapeStartsAtTheHeightItsShapeSays(CloudKind? kind, double height) =>
        Assert.Equal(height, CloudShapes.StartHeight(kind));
}
