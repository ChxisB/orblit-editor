namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>How big each device frame is, which way it is turned and where its unsafe edges are.</summary>
public sealed class DeviceFrameTests
{
    [Theory]
    [InlineData(DeviceKind.Desktop, FrameOrientation.Landscape, 1920, 1080)]
    [InlineData(DeviceKind.Tablet, FrameOrientation.Portrait, 834, 1112)]
    [InlineData(DeviceKind.Tablet, FrameOrientation.Landscape, 1112, 834)]
    [InlineData(DeviceKind.Mobile, FrameOrientation.Portrait, 390, 844)]
    [InlineData(DeviceKind.Mobile, FrameOrientation.Landscape, 844, 390)]
    public void EachDeviceHasItsSize(DeviceKind kind, FrameOrientation orientation, int width, int height)
    {
        var frame = new DeviceFrame(kind, orientation);

        Assert.Equal((width, height), (frame.Width, frame.Height));
    }

    [Fact]
    public void ADesktopIsLandscapeWhateverWasAsked()
    {
        var frame = new DeviceFrame(DeviceKind.Desktop, FrameOrientation.Portrait);

        Assert.Equal(FrameOrientation.Landscape, frame.Orientation);
        Assert.Equal((1920, 1080), (frame.Width, frame.Height));
    }

    [Fact]
    public void ATurnSwapsTheSidesAndTurningBackReturnsTheFrame()
    {
        var portrait = new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Portrait);

        var landscape = portrait.Turned();

        Assert.Equal(FrameOrientation.Landscape, landscape.Orientation);
        Assert.Equal((844, 390), (landscape.Width, landscape.Height));
        Assert.Equal(portrait, landscape.Turned());
    }

    [Fact]
    public void ADesktopCannotTurnAndTurnedAnswersTheSameFrame()
    {
        var desktop = DeviceFrame.Desktop;

        Assert.False(desktop.CanTurn);
        Assert.Equal(desktop, desktop.Turned());
    }

    [Fact]
    public void ATabletAndAPhoneCanTurn()
    {
        Assert.All(
            DeviceFrame.All.Where(frame => frame.Kind != DeviceKind.Desktop),
            frame => Assert.True(frame.CanTurn));
    }

    [Fact]
    public void ThePhoneKeepsItsNotchAndHomeBarOffTheLongSides()
    {
        var portrait = new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Portrait).Edges;
        var landscape = new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Landscape).Edges;

        Assert.Equal(new UnsafeEdges(0, 47, 0, 34), portrait);
        Assert.Equal(new UnsafeEdges(47, 0, 47, 21), landscape);
    }

    [Fact]
    public void ADesktopAndATabletHaveNoUnsafeEdges()
    {
        Assert.All(
            DeviceFrame.All.Where(frame => frame.Kind != DeviceKind.Mobile),
            frame => Assert.Equal(UnsafeEdges.None, frame.Edges));
    }

    [Fact]
    public void EveryFramesUnsafeEdgesFitInsideIt()
    {
        Assert.All(
            DeviceFrame.All,
            frame =>
            {
                Assert.True(frame.Edges.Left + frame.Edges.Right < frame.Width);
                Assert.True(frame.Edges.Top + frame.Edges.Bottom < frame.Height);
            });
    }

    [Fact]
    public void ThereAreFiveFramesAndNoTwoAreAlike()
    {
        Assert.Equal(5, DeviceFrame.All.Count);
        Assert.Equal(5, DeviceFrame.All.Distinct().Count());
    }

    [Fact]
    public void TheSizeReadsAsWidthTimesHeight()
    {
        Assert.Equal("390 × 844", new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Portrait).Size);
    }

    [Fact]
    public void ItFitsTheRoomItIsGiven()
    {
        Assert.Equal(0.5, DeviceFrame.Desktop.FitInto(960, 540), precision: 6);
    }

    [Fact]
    public void ItNeverGrowsPastItsOwnSize()
    {
        Assert.Equal(1.0, DeviceFrame.Desktop.FitInto(5000, 5000), precision: 6);
    }

    [Fact]
    public void ARoomWiderThanTheFrameIsLimitedByItsHeight()
    {
        var scale = new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Portrait).FitInto(2000, 422);

        Assert.Equal(0.5, scale, precision: 6);
    }

    [Fact]
    public void ARoomWithNoSizeFitsAtZero()
    {
        Assert.Equal(0, DeviceFrame.Desktop.FitInto(0, 300));
    }
}
