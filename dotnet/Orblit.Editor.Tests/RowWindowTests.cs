namespace Orblit.Editor.Tests;

using Orblit.Editor.Panels;

public sealed class RowWindowTests
{
    [Fact]
    public void AnEmptyListBuildsNothing()
    {
        Assert.Equal(new RowWindow(0, 0), RowWindow.Of(0, 0, 240, 24));
    }

    [Fact]
    public void ATopOfListBuildsTheViewAndTheSpareBelow()
    {
        Assert.Equal(new RowWindow(0, 10 + RowWindow.Spare), RowWindow.Of(1000, 0, 240, 24));
    }

    [Fact]
    public void AScrolledListBuildsTheViewWithSpareOnBothSides()
    {
        // Row 100 is the first one in view, and 110 is the first one past it.
        Assert.Equal(new RowWindow(100 - RowWindow.Spare, 10 + (2 * RowWindow.Spare)), RowWindow.Of(1000, 2400, 240, 24));
    }

    [Fact]
    public void ARowHalfInViewIsBuilt()
    {
        var window = RowWindow.Of(1000, 2412, 240, 24);

        Assert.Equal(100 - RowWindow.Spare, window.First);
        Assert.Equal(111 + RowWindow.Spare, window.First + window.Count);
    }

    [Fact]
    public void TheEndOfTheListStopsAtTheLastRow()
    {
        var window = RowWindow.Of(1000, (1000 * 24) - 240, 240, 24);

        Assert.Equal(1000, window.First + window.Count);
        Assert.Equal(990 - RowWindow.Spare, window.First);
    }

    [Fact]
    public void AListShorterThanTheViewBuildsEveryRow()
    {
        Assert.Equal(new RowWindow(0, 5), RowWindow.Of(5, 0, 900, 24));
    }

    [Fact]
    public void AnOffsetPastTheEndOfAShrunkListBuildsNothingSoItCannotReadOutOfRange()
    {
        Assert.Equal(new RowWindow(10, 0), RowWindow.Of(10, 50_000, 240, 24));
    }

    [Fact]
    public void ANegativeOffsetCountsAsTheTop()
    {
        Assert.Equal(RowWindow.Of(1000, 0, 240, 24), RowWindow.Of(1000, -30, 240, 24));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-24)]
    public void ARowHeightOfNothingIsRefused(double height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RowWindow.Of(10, 0, 240, height));
    }

    [Fact]
    public void AHundredThousandRowsBuildTheSameFewAsTenThousand()
    {
        Assert.Equal(RowWindow.Of(10_000, 0, 900, 24).Count, RowWindow.Of(100_000, 0, 900, 24).Count);
    }
}
