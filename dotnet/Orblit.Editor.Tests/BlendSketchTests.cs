namespace Orblit.Editor.Tests;

using Orblit.Editor.Panels;
using Orblit.Motion;

// Where the blend graph is drawn, without a control.
public sealed class BlendSketchTests
{
    private static BlendDocument Pair() => new(
        "Pair",
        [new BlendState("idle", new BlendClip("idle")), new BlendState("move", new BlendClip("walk"))],
        new Dictionary<string, double> { ["speed"] = 0 },
        [new BlendChange("move", from: "idle", when: BlendCondition.Above("speed", 0.1))]);

    [Fact]
    public void EachStateIsABoxOnAGridThatStartsAtTheMargin()
    {
        var sketch = BlendSketch.Of(Pair());

        Assert.Equal(new SketchBox(28, 28, 156, 52), sketch.States["idle"]);
        Assert.Equal(new SketchBox(28 + 156 + 92, 28, 156, 52), sketch.States["move"]);
    }

    [Fact]
    public void TheSizeCoversEveryBoxAndLeavesRoomUnderThemForAnArrowThatGoesBack()
    {
        var sketch = BlendSketch.Of(Pair());

        Assert.Equal(28 + 156 + 92 + 156 + 28, sketch.Width);
        Assert.Equal(28 + 52 + 28 + 40, sketch.Height);
    }

    [Fact]
    public void ABlendWithNoChangeFromAnywhereHasNoBoxForIt()
    {
        Assert.Null(BlendSketch.Of(Pair()).Anywhere);
    }

    [Fact]
    public void AChangeFromAnywhereGetsABoxInAColumnOfItsOwnAndEverythingMovesAlong()
    {
        var sketch = BlendSketch.Of(Pair().WithChangeAdded(null, "move"));

        Assert.Equal(new SketchBox(28, 28, 156, 52), sketch.Anywhere);
        Assert.Equal(28 + 248, sketch.States["idle"].Left);
        var arrow = sketch.Arrows[^1];
        Assert.Equal(sketch.Anywhere, arrow.From);
        Assert.Equal(sketch.States["move"], arrow.To);
        Assert.Equal([1], arrow.Changes);
    }

    [Fact]
    public void AnArrowThatGoesForwardLeavesTheRightEdgeAndEntersTheLeftEdgeOfTheNextBox()
    {
        var sketch = BlendSketch.Of(Pair());
        var curve = Assert.Single(sketch.Arrows).Curve;

        Assert.Equal(sketch.States["idle"].CentreRight, curve.Start);
        Assert.Equal(sketch.States["move"].CentreLeft, curve.End);
        Assert.Equal(0, curve.Heading);
    }

    [Fact]
    public void TheMiddleOfAForwardCurveIsHalfWayBetweenItsEnds()
    {
        var curve = Assert.Single(BlendSketch.Of(Pair()).Arrows).Curve;

        Assert.Equal(curve.Start, curve.At(0));
        Assert.Equal(curve.End, curve.At(1));
        Assert.Equal(new Spot((curve.Start.X + curve.End.X) / 2, curve.Start.Y), curve.At(0.5));
    }

    [Fact]
    public void AnArrowThatGoesBackPassesUnderBothBoxesSoItNeverCrossesOne()
    {
        var sketch = BlendSketch.Of(Pair().WithChangeAdded("move", "idle"));
        var curve = sketch.Arrows[1].Curve;

        Assert.Equal(sketch.States["move"].BottomCentre, curve.Start);
        Assert.Equal(sketch.States["idle"].BottomCentre, curve.End);
        Assert.Equal(sketch.States["move"].Bottom + 36, curve.PullStart.Y);
        Assert.Equal(curve.PullStart.Y, curve.PullEnd.Y);
    }

    [Fact]
    public void AnArrowBackBetweenTwoRowsRunsUnderTheLowerBox()
    {
        var sketch = BlendSketch.Of(Pair()
            .WithStateAdded("jump")
            .WithChangeAdded("idle", "jump")
            .WithChangeAdded("jump", "move"));
        var (jump, move) = (sketch.States["jump"], sketch.States["move"]);
        Assert.Equal(move.Left, jump.Left);
        Assert.NotEqual(move.Top, jump.Top);

        var curve = sketch.Arrows.Single(arrow => arrow.From == jump && arrow.To == move).Curve;

        Assert.Equal(Math.Max(jump.Bottom, move.Bottom) + 36, curve.PullStart.Y);
    }

    [Fact]
    public void AChangeToAStateThatIsNotThereIsLeftOffTheGraphWhereverItStarts()
    {
        var sketch = BlendSketch.Of(Pair()
            .WithChangeAdded("idle", "ghost")
            .WithChangeAdded(null, "ghost"));

        Assert.Single(sketch.Arrows);
    }

    [Fact]
    public void AChangeThatStaysInAStateIsALoopLabelledBelowItsCorner()
    {
        var sketch = BlendSketch.Of(Pair().WithChangeAdded("move", "move"));
        var loop = sketch.Arrows[1];

        Assert.True(loop.IsLoop);
        Assert.False(sketch.Arrows[0].IsLoop);
        Assert.Equal(new Spot(loop.From.Left, loop.From.Bottom + 14), loop.Anchor);
    }

    [Fact]
    public void AStateIsFoundUnderASpotAndNothingIsFoundInTheGap()
    {
        var sketch = BlendSketch.Of(Pair());

        Assert.Equal("idle", sketch.StateAt(new Spot(30, 30)));
        Assert.Equal("move", sketch.StateAt(sketch.States["move"].CentreRight));
        Assert.Null(sketch.StateAt(new Spot(230, 50)));
    }

    [Fact]
    public void TappingAnArrowAgainStepsAlongTheChangesItStandsForAndThenAround()
    {
        Assert.Equal(0, BlendSketch.ChangeAfter([0, 4], -1));
        Assert.Equal(4, BlendSketch.ChangeAfter([0, 4], 0));
        Assert.Equal(0, BlendSketch.ChangeAfter([0, 4], 4));
        Assert.Equal(2, BlendSketch.ChangeAfter([2], 2));
    }
}
