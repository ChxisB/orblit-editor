namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Light;

// The light model, tested apart from the inspector.
public sealed class LightModelTests
{
    [Fact]
    public void ASunIsStatedPerSquareMetreSoBecomingOneSpreadsTheWattsOverASphere() =>
        Assert.Equal(100, LightModel.PowerFor(LightType.Sun, LightType.Point, 100 * 4 * Math.PI), 9);

    [Fact]
    public void LeavingASunTakesTheWattsBackOutOfTheSphere() =>
        Assert.Equal(400 * Math.PI, LightModel.PowerFor(LightType.Spot, LightType.Sun, 100), 9);

    [Theory]
    [InlineData(LightType.Point, LightType.Spot)]
    [InlineData(LightType.Spot, LightType.Area)]
    [InlineData(LightType.Sun, LightType.Sun)]
    public void ChangingBetweenTypesMeasuredInWattsKeepsThePower(LightType from, LightType wanted) =>
        Assert.Equal(250, LightModel.PowerFor(wanted, from, 250));

    [Fact]
    public void TheShapeIsReadFromTheLightInTheOrderTheCommandTakesIt()
    {
        var light = new SceneObject("lamp", "Lamp", ObjectKind.Light) { SpotSize = 40, SpotBlend = 0.3, SourceRadius = 0.5, SunAngle = 0.5 };

        Assert.Equal(new LightShape(40, 0.3, 0.5, 0.5), LightModel.ShapeOf(light));
    }

    [Fact]
    public void EveryLightTypeIsOfferedOnceAndTheSunAndAreaHaveTheirOwnNotes()
    {
        Assert.Equal(Enum.GetValues<LightType>().Order(), LightModel.Names.Select(one => one.Key).Order());
        Assert.StartsWith("Sun size", LightModel.Note(LightType.Sun));
        Assert.StartsWith("An area light", LightModel.Note(LightType.Area));
        Assert.StartsWith("Radius is", LightModel.Note(LightType.Point));
        Assert.Equal(LightModel.Note(LightType.Point), LightModel.Note(LightType.Spot));
    }
}
