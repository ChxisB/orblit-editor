namespace Orblit.Editor.Tests;

// The words on the chips over the view.
public sealed class ViewportChipsTests
{
    [Theory]
    [InlineData(0.001, "1mm")]
    [InlineData(0.0025, "3mm")]
    [InlineData(0.005, "5mm")]
    [InlineData(0.01, "1cm")]
    [InlineData(0.05, "5cm")]
    [InlineData(0.5, "50cm")]
    [InlineData(1, "1m")]
    [InlineData(2.5, "2.5m")]
    [InlineData(10, "10m")]
    public void AGridStepIsSaidAsSomebodyWouldSayIt(double step, string words) =>
        Assert.Equal(words, ViewportChips.GridLabel(step));

    [Theory]
    [InlineData(1.234, 2, "+1.23")]
    [InlineData(-0.5, 1, "-0.5")]
    [InlineData(0, 1, "+0.0")]
    [InlineData(12, 0, "+12")]
    public void AChangeShowsItsSign(double value, int decimals, string words) =>
        Assert.Equal(words, ViewportChips.Signed(value, decimals));

    [Fact]
    public Task ALitChipIsEmberAndAnIdleOneIsNot() => Headless.Run(() =>
    {
        var lit = ViewportChips.Build(new ChipSpec("Snap", On: true));
        var idle = ViewportChips.Build(new ChipSpec("Snap"));

        Assert.Equal(OrblitColors.EmberWash, lit.Background);
        Assert.NotEqual(OrblitColors.EmberWash, idle.Background);
    });
}
