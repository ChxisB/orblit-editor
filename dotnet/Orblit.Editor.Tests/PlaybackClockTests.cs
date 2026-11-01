namespace Orblit.Editor.Tests;

/// <summary>How long a frame counts for in the playing scene.</summary>
public sealed class PlaybackClockTests
{
    [Fact]
    public void TheFirstFrameAfterAStartTakesNoTime()
    {
        Assert.Equal(0, PlaybackClock.StepSince(null, TimeSpan.FromSeconds(40)));
    }

    [Fact]
    public void ALaterFrameTakesTheTimeSinceTheLast()
    {
        var step = PlaybackClock.StepSince(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.016));

        Assert.Equal(0.016, step, 1e-9);
    }

    [Fact]
    public void AStallIsCutShortSoACutsceneIsNotThrownPastItsShots()
    {
        var step = PlaybackClock.StepSince(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(9));

        Assert.Equal(0.1, step);
    }
}
