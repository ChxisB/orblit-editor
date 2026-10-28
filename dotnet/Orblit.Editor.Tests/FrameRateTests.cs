namespace Orblit.Editor.Tests;

using Orblit.Editor.Viewport;

// Dart: orblit-editor/test/frame_rate_test.dart. The scene view hands each drawn frame in, so there is no
// engine callback to start or stop, and Dart's case for starting and stopping twice has no counterpart.
public sealed class FrameRateTests
{
    // A frame every reading apart, so each one is read and the reading is the whole window.
    private static void Feed(FrameRate rate, int frames, double buildMs, double gpuMs, TimeSpan apart, TimeSpan from)
    {
        for (var i = 0; i < frames; i++)
        {
            rate.Took(from + (apart * i), TimeSpan.FromMilliseconds(buildMs), TimeSpan.FromMilliseconds(gpuMs));
        }
    }

    [Fact]
    public void SaysNothingUntilThereIsSomethingToSay() =>
        Assert.Null(new FrameRate().Reading);

    [Fact]
    public void MeasuresWhatTheFramesTook()
    {
        var rate = new FrameRate();

        // Ten milliseconds of work a frame, so a hundred a second of headroom, though they came slower.
        Feed(rate, 1, 4, 6, FrameRate.Every, TimeSpan.Zero);
        // One frame is not read as a whole window of them.
        Assert.InRange(Assert.NotNull(rate.Reading).Fps, 99, 101);
        Feed(rate, FrameRate.Window, 4, 6, FrameRate.Every, FrameRate.Every);

        var reading = Assert.NotNull(rate.Reading);
        Assert.InRange(reading.Fps, 99, 101);
        Assert.True(reading.BuildMs > 0);
        Assert.True(reading.GpuMs > reading.BuildMs);
        Assert.True(reading.GpuBound);
    }

    [Fact]
    public void ASlowBuildSaysTheProcessorNotTheCard()
    {
        var rate = new FrameRate();

        Feed(rate, FrameRate.Window, 20, 2, FrameRate.Every, TimeSpan.Zero);

        var reading = Assert.NotNull(rate.Reading);
        Assert.False(reading.GpuBound);
        // Twenty-two milliseconds of work is under fifty a second.
        Assert.True(reading.Fps < 50);
    }

    // The chips are rebuilt when the reading changes, so it changes a few times a second and no more.
    [Fact]
    public void OnlySpeaksAFewTimesASecond()
    {
        var rate = new FrameRate();
        var said = 0;
        FrameReading? last = null;

        // Two seconds at sixty a second.
        for (var i = 0; i < 120; i++)
        {
            rate.Took(TimeSpan.FromTicks(i * 166_667), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));
            if (rate.Reading != last)
            {
                said++;
                last = rate.Reading;
            }
        }

        Assert.InRange(said, 2, 6);
    }

    [Fact]
    public void ForgetsFramesOlderThanItsWindow()
    {
        var rate = new FrameRate();

        Feed(rate, FrameRate.Window * 3, 1, 1, FrameRate.Every, TimeSpan.Zero);
        // Then a run of slow ones, enough to fill the window.
        Feed(rate, FrameRate.Window, 40, 10, FrameRate.Every, FrameRate.Every * FrameRate.Window * 3);

        // Fifty milliseconds of work a frame: twenty a second, what it does now and not a minute ago.
        Assert.InRange(Assert.NotNull(rate.Reading).Fps, 19, 21);
    }

    [Theory]
    [InlineData(99.6, 4.04, 6.26, "100 fps · 6.3 ms gpu")]
    [InlineData(45.4, 12.0, 1.5, "45 fps · 12.0 ms cpu")]
    public void TheChipSaysTheRateAndTheSlowerHalf(double fps, double buildMs, double gpuMs, string words) =>
        Assert.Equal(words, ViewportChips.Frames(new FrameReading(fps, buildMs, gpuMs)));

    [Fact]
    public void TheChipSaysNoRateBeforeTheFirstFrame() =>
        Assert.Equal("— fps", ViewportChips.Frames(null));
}
