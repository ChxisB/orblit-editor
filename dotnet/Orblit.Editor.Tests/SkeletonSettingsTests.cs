namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

// What the timeline's Bones and Onion chips decide, which the chips only draw.
public sealed class SkeletonSettingsTests
{
    [Fact]
    public void BonesSwitchOnAndOff()
    {
        var on = new SkeletonSettings().WithBonesToggled();
        Assert.True(on.Bones);

        Assert.False(on.WithBonesToggled().Bones);
    }

    [Fact]
    public void OnionBringsTheBonesWithItAndTheyTakeItBack()
    {
        var onion = new SkeletonSettings().WithOnionToggled();
        Assert.True(onion.Onion);
        Assert.True(onion.Bones);

        var without = onion.WithBonesToggled();
        Assert.False(without.Bones);
        Assert.False(without.Onion);
    }

    [Fact]
    public void OnionOffLeavesTheBonesAsTheyWere()
    {
        var onion = new SkeletonSettings(Bones: true, Onion: true).WithOnionToggled();

        Assert.True(onion.Bones);
        Assert.False(onion.Onion);
    }

    [Fact]
    public void ABoneChipDoesNotTouchTheGhostCounts()
    {
        var settings = new SkeletonSettings(Before: 3, After: 1, Stride: 4).WithOnionToggled().WithBonesToggled();

        Assert.Equal((3, 1, 4), (settings.Before, settings.After, settings.Stride));
    }

    [Fact]
    public void TheLookSaysSoOnlyWhenSomethingChanged()
    {
        var look = new SkeletonLook();
        var told = 0;
        look.Changed += () => told++;

        look.Value = new SkeletonSettings();
        Assert.Equal(0, told);

        look.Value = look.Value.WithBonesToggled();
        Assert.Equal(1, told);
        Assert.True(look.Value.Bones);
    }

    [Theory]
    [InlineData(0, 30, "0.00 s   frame 0")]
    [InlineData(1, 30, "1.00 s   frame 30")]
    [InlineData(0.52, 24, "0.52 s   frame 12")]
    [InlineData(1, 0, "1.00 s   frame 0")]
    public void ThePlayheadSaysWhereItIs(double at, double rate, string words) =>
        Assert.Equal(words, TimelineWords.Where(at, rate));

    [Theory]
    [InlineData(2, 2, "2 each side, every 2 frames")]
    [InlineData(1, 1, "1 each side, every frame")]
    [InlineData(3, 4, "3 each side, every 4 frames")]
    public void TheGhostButtonSaysHowManyAndHowFarApart(int count, int stride, string words) =>
        Assert.Equal(words, TimelineWords.Ghosts(new SkeletonSettings(Before: count, After: count, Stride: stride)));
}
