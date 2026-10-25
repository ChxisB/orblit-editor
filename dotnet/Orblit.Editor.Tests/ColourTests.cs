namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Light;

public sealed class ColourTests
{
    [Fact]
    public void HexWithSixDigitsIsOpaque() =>
        Assert.Equal(0xFF336699u, Colour.FromHex("#336699").Argb);

    [Fact]
    public void HexWithEightDigitsKeepsItsAlpha() =>
        Assert.Equal(0x80336699u, Colour.FromHex("80336699").Argb);

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("-12345")]
    public void RubbishFallsBack(string hex) =>
        Assert.Equal(0xFFD9634Fu, Colour.FromHex(hex).Argb);

    [Fact]
    public void TheFallbackIsTheOneThatWasGiven() =>
        Assert.Equal(7u, Colour.FromHex("nope", new Colour(7)).Argb);

    [Fact]
    public void HexWritesCapitalsAndDropsTheAlpha() =>
        Assert.Equal("#0A0B0C", new Colour(0x400A0B0C).ToHex());

    [Fact]
    public void ATintAboveOneClampsToTheTopOfTheChannel() =>
        Assert.Equal(0xFFFF8000u, Colour.FromTint(new Tint(2, 0.5019607843137255, -1)).Argb);

    [Fact]
    public void ATintSurvivesTheRoundTrip()
    {
        var tint = new Colour(0xFF123456).ToTint();
        Assert.Equal(0xFF123456u, Colour.FromTint(tint).Argb);
    }
}
