namespace Orblit.Editor.Tests;

using System.Globalization;
using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// What the timeline decides without a window: playing is sampling, so each frame asks how far the
// playhead moved and where that puts it, and every control is an edit that goes through the undo stack.
// These run on the model alone, so none of them needs a window, a tap or a key.
public sealed class TimelineModelTests
{
    private const string Wave = "/project/wave.oclip";

    private static readonly ChannelAddress LampPower = new(string.Empty, null, "light.power");

    private sealed record Rig(EditorScene Scene, History History, ClipBench Bench, TimelineModel Timeline);

    private static Rig MakeRig(WhenDone whenDone = WhenDone.Hold)
    {
        var scene = new EditorScene(
        [
            new SceneObject("lamp", "Lamp", ObjectKind.Light, power: 100),
            new SceneObject("box", "Box", ObjectKind.Group),
        ]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);
        var bench = new ClipBench(history, () => scene);
        bench.OpenDocument(Wave, new ClipDocument("wave", 2, whenDone));
        return new Rig(scene, history, bench, new TimelineModel(bench));
    }

    private static ClipDocument ClipOf(ClipBench bench)
    {
        Assert.NotNull(bench.Clip);
        return bench.Clip;
    }

    // Plays from the start, giving the clock its first reading and then the time later.
    private static void PlayFor(Rig rig, double seconds)
    {
        rig.Timeline.Play();
        rig.Timeline.Tick(TimeSpan.Zero);
        rig.Timeline.Tick(TimeSpan.FromSeconds(seconds));
    }

    private static ClipDocument Rising() => new(
        "rise",
        1,
        channels:
        [
            new ClipChannel<Vector3>(
                "box",
                "transform.position",
                ChannelKind.Vector,
                [new Key<Vector3>(0, Vector3.Zero(), Hold.Linear), new Key<Vector3>(1, new Vector3(0, 2, 0))]),
            new ClipChannel<double>(
                string.Empty,
                "light.power",
                ChannelKind.Number,
                [new Key<double>(0, 10, Hold.Linear), new Key<double>(1, 30)]),
            new ClipChannel<double>(
                "box",
                "light.power",
                ChannelKind.Number,
                [new Key<double>(0.5, 1), new Key<double>(3, 4)]),
        ]);

    [Fact]
    public void ThePlayheadMovesByTheTimeBetweenFramesAndTheFirstFrameOnlyStartsTheClock()
    {
        var rig = MakeRig();
        rig.Timeline.Play();

        rig.Timeline.Tick(TimeSpan.FromSeconds(10));
        Assert.Equal(0, rig.Bench.At);

        rig.Timeline.Tick(TimeSpan.FromSeconds(10.5));
        Assert.Equal(0.5, rig.Bench.At, 1e-9);
        Assert.True(rig.Bench.Playing);
    }

    [Fact]
    public void ThePlayheadStaysPutWhenNothingIsPlaying()
    {
        var rig = MakeRig();
        rig.Timeline.Tick(TimeSpan.FromSeconds(1));
        rig.Timeline.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal(0, rig.Bench.At);
    }

    [Fact]
    public void PlayingAgainStartsTheClockFromItsNextReading()
    {
        var rig = MakeRig();
        PlayFor(rig, 0.5);
        rig.Timeline.Play();
        Assert.False(rig.Bench.Playing);

        // A clock that kept its old reading would jump the playhead by all the time spent paused.
        rig.Timeline.Tick(TimeSpan.FromSeconds(20));
        rig.Timeline.Play();
        rig.Timeline.Tick(TimeSpan.FromSeconds(30));
        rig.Timeline.Tick(TimeSpan.FromSeconds(30.25));

        Assert.Equal(0.75, rig.Bench.At, 1e-9);
    }

    [Fact]
    public void PlayingAgainAfterTheEndDoesNotCountTheTimeSince()
    {
        var rig = MakeRig();
        PlayFor(rig, 5);
        Assert.False(rig.Bench.Playing);

        // Nothing ticked while it was stopped, as when the panel stops asking for frames.
        rig.Timeline.Play();
        rig.Timeline.Tick(TimeSpan.FromSeconds(100));
        rig.Timeline.Tick(TimeSpan.FromSeconds(100.5));

        Assert.Equal(0.5, rig.Bench.At, 1e-9);
        Assert.True(rig.Bench.Playing);
    }

    [Theory]
    [InlineData(WhenDone.Hold)]
    [InlineData(WhenDone.Release)]
    public void AClipThatPlaysOnceStopsOnItsLastFrame(WhenDone whenDone)
    {
        var rig = MakeRig(whenDone);
        PlayFor(rig, 5);

        Assert.Equal(2, rig.Bench.At);
        Assert.False(rig.Bench.Playing);
    }

    [Fact]
    public void PlayingAClipThatHasRunOutStartsItOverAndOnePartWayContinuesIt()
    {
        var rig = MakeRig();
        rig.Bench.At = 2;
        rig.Timeline.Play();
        Assert.Equal(0, rig.Bench.At);
        Assert.True(rig.Bench.Playing);

        rig.Timeline.Play();
        rig.Bench.At = 1;
        rig.Timeline.Play();
        Assert.Equal(1, rig.Bench.At);
    }

    [Fact]
    public void ALoopingClipComesRoundToTheStart()
    {
        var rig = MakeRig(WhenDone.Loop);
        PlayFor(rig, 2.5);

        Assert.Equal(0.5, rig.Bench.At, 1e-9);
        Assert.True(rig.Bench.Playing);
    }

    [Fact]
    public void ABouncingClipTurnsAtEachEnd()
    {
        var rig = MakeRig(WhenDone.Bounce);
        PlayFor(rig, 2.5);
        Assert.Equal(1.5, rig.Bench.At, 1e-9);

        // Still going backwards, so a second and a half more brings it to the start, where it turns.
        rig.Timeline.Tick(TimeSpan.FromSeconds(4));
        Assert.Equal(0, rig.Bench.At, 1e-9);
        rig.Timeline.Tick(TimeSpan.FromSeconds(4.25));
        Assert.Equal(0.25, rig.Bench.At, 1e-9);
    }

    [Fact]
    public void StepMovesOneFrameOntoAFrameAndStopsPlaying()
    {
        var rig = MakeRig();
        rig.Bench.At = 0.51;
        rig.Bench.Playing = true;

        rig.Timeline.Step(1);

        Assert.False(rig.Bench.Playing);
        Assert.Equal(16.0 / 30, rig.Bench.At, 1e-9);
        rig.Timeline.Step(-2);
        Assert.Equal(14.0 / 30, rig.Bench.At, 1e-9);
    }

    [Fact]
    public void StepDoesNotLeaveTheClip()
    {
        var rig = MakeRig();
        rig.Timeline.Step(-1);
        Assert.Equal(0, rig.Bench.At);

        rig.Bench.At = 2;
        rig.Timeline.Step(1);
        Assert.Equal(2, rig.Bench.At);
    }

    [Fact]
    public void HomeAndEndStopPlayingAtEitherEnd()
    {
        var rig = MakeRig();
        rig.Bench.At = 1;
        rig.Bench.Playing = true;

        rig.Timeline.End();
        Assert.Equal(2, rig.Bench.At);
        Assert.False(rig.Bench.Playing);

        rig.Bench.Playing = true;
        rig.Timeline.Home();
        Assert.Equal(0, rig.Bench.At);
        Assert.False(rig.Bench.Playing);
    }

    [Fact]
    public void DeleteTakesThePickedKeysAndNamesTheStepBySize()
    {
        var rig = MakeRig();
        rig.Bench.OpenDocument("/project/rise.oclip", Rising());
        rig.Bench.Show("/project/rise.oclip");
        var power = new KeyRef(LampPower, 0);

        rig.Timeline.DeleteKeys();
        Assert.Empty(rig.History.Labels);

        rig.Bench.Selection = new HashSet<KeyRef> { power };
        rig.Timeline.DeleteKeys();
        Assert.Equal(["Delete key"], rig.History.Labels);
        Assert.Single(Assert.IsAssignableFrom<ClipChannel>(ClipEdits.ChannelAt(ClipOf(rig.Bench), LampPower)).BoxedKeys);
        Assert.Empty(rig.Bench.Selection);

        rig.Bench.Selection = new HashSet<KeyRef> { new(LampPower, 0), new(new ChannelAddress("box", null, "transform.position"), 0) };
        rig.Timeline.DeleteKeys();
        Assert.Equal("Delete keys", rig.History.Labels[^1]);
    }

    [Fact]
    public void ALengthTypedInOneRunIsOneStepThatUndoesToWhatItWas()
    {
        var rig = MakeRig();

        rig.Timeline.SetLength("3");
        rig.Timeline.SetLength("3.5");
        rig.Timeline.FinishLength();
        Assert.Equal(3.5, ClipOf(rig.Bench).Duration);

        rig.Timeline.SetLength("4");
        rig.Timeline.FinishLength();
        Assert.Equal(["Clip length", "Clip length"], rig.History.Labels);

        rig.History.Undo();
        Assert.Equal(3.5, ClipOf(rig.Bench).Duration);
        rig.History.Undo();
        Assert.Equal(2, ClipOf(rig.Bench).Duration);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("601")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void ALengthThatIsNotASensibleNumberOfSecondsIsIgnored(string text)
    {
        var rig = MakeRig();
        rig.Timeline.SetLength(text);

        Assert.Equal(2, ClipOf(rig.Bench).Duration);
        Assert.Empty(rig.History.Labels);
    }

    [Fact]
    public void ALengthIsReadWithSpacesAroundItAndTheSameWhateverTheLanguage()
    {
        var rig = MakeRig();
        // The test host runs without culture data, so a culture that writes a comma is made rather than named.
        var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        comma.NumberFormat.NumberDecimalSeparator = ",";
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = comma;
            rig.Timeline.SetLength(" 1.5 ");
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }

        Assert.Equal(1.5, ClipOf(rig.Bench).Duration);
    }

    [Fact]
    public void TheClipsRateAndEndingAreEditsThatCanBeUndone()
    {
        var rig = MakeRig();
        rig.Timeline.SetRate(24);
        rig.Timeline.SetWhenDone(WhenDone.Loop);

        Assert.Equal(24, ClipOf(rig.Bench).Rate);
        Assert.Equal(WhenDone.Loop, ClipOf(rig.Bench).WhenDone);
        Assert.Equal(["Frame rate", "When done"], rig.History.Labels);

        rig.History.Undo();
        rig.History.Undo();
        Assert.Equal(30, ClipOf(rig.Bench).Rate);
        Assert.Equal(WhenDone.Hold, ClipOf(rig.Bench).WhenDone);
    }

    [Fact]
    public void HoldsAndAutoSlopesWorkOnThePickedKeysOnly()
    {
        var rig = MakeRig();
        rig.Bench.OpenDocument("/project/rise.oclip", Rising());
        rig.Bench.Show("/project/rise.oclip");

        rig.Timeline.SetHold(Hold.Step);
        rig.Timeline.AutoSlopes();
        Assert.Empty(rig.History.Labels);

        rig.Bench.Selection = new HashSet<KeyRef> { new(LampPower, 0) };
        rig.Timeline.SetHold(Hold.Shaped, Easing.Bounce);
        Assert.Equal("Hold shaped", rig.History.Labels[^1]);
        Assert.Equal(Hold.Shaped, ClipEdits.KeyOf(ClipOf(rig.Bench), new KeyRef(LampPower, 0))?.Hold);

        rig.Timeline.AutoSlopes();
        Assert.Equal("Auto slopes", rig.History.Labels[^1]);
    }

    [Fact]
    public void RemovingAChannelTakesAllItsKeysAndLeavesNothingPicked()
    {
        var rig = MakeRig();
        rig.Bench.OpenDocument("/project/rise.oclip", Rising());
        rig.Bench.Show("/project/rise.oclip");
        rig.Bench.Selection = new HashSet<KeyRef> { new(LampPower, 1) };

        rig.Timeline.RemoveChannel(LampPower);

        Assert.Equal(2, ClipOf(rig.Bench).Channels.Count);
        Assert.Empty(rig.Bench.Selection);
        Assert.Equal("Remove power", rig.History.Labels[^1]);
    }

    [Fact]
    public void RowsGroupWhatTheClipMovesByTargetInTheOrderItFirstMovesIt()
    {
        var rows = TimelineRow.Of(Rising());

        Assert.Equal(
            [("box", true), ("box", false), ("box", false), ("", true), ("", false)],
            rows.Select(row => (row.Target, row.IsHeading)));
    }

    [Fact]
    public void ARowOfAChannelCarriesTheChannelAndAHeadingCarriesNone()
    {
        var rows = TimelineRow.Of(Rising());

        Assert.Null(rows[0].Channel);
        Assert.Equal("transform.position", Assert.IsAssignableFrom<ClipChannel>(rows[1].Channel).Property);
    }

    [Fact]
    public void TheAxisFitsTheWholeClipAndStretchesForKeysLeftPastTheEnd()
    {
        var plain = new TimeAxis(new ClipDocument("a", 2), 220);
        Assert.Equal(2, plain.Span);
        Assert.Equal(100, plain.PerSecond, 1e-9);
        Assert.Equal(TimeAxis.Margin, plain.XOf(0));
        Assert.Equal(TimeAxis.Margin + 200, plain.XOf(2), 1e-9);

        // The last key is at three seconds, past the end of a one second clip.
        var stretched = new TimeAxis(Rising(), 220);
        Assert.Equal(1, stretched.Duration);
        Assert.Equal(3, stretched.Span);
    }

    [Fact]
    public void TheAxisTurnsAnXBackIntoATimeAndKeepsItInsideTheClip()
    {
        var axis = new TimeAxis(new ClipDocument("a", 2), 220);

        Assert.Equal(1, axis.AtOf(axis.XOf(1)), 1e-9);
        Assert.Equal(0, axis.AtOf(-50));
        Assert.Equal(2, axis.AtOf(5000));
    }

    [Fact]
    public void AnAxisWithNoLengthOrNoWidthDoesNotDivideByZero()
    {
        var empty = new TimeAxis(new ClipDocument("a", 0), 220);
        Assert.Equal(0, empty.PerSecond);
        Assert.Equal(0, empty.AtOf(100));

        // Narrower than its own margins, so it still draws a pixel a second rather than none.
        var narrow = new TimeAxis(new ClipDocument("a", 2), 10);
        Assert.Equal(0.5, narrow.PerSecond);
    }

    [Theory]
    [InlineData(300, 30, 1)]
    [InlineData(100, 30, 5)]
    [InlineData(30, 30, 10)]
    [InlineData(1, 30, 300)]
    [InlineData(0, 30, 1)]
    [InlineData(100, 0, 1)]
    public void LinesAreDrawnAFewFramesApartSoNoneCrowdTheNext(double perSecond, double rate, int frames)
    {
        Assert.Equal(frames, TimeAxis.FrameStep(perSecond, rate));
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(0.5, "0.5")]
    [InlineData(0.25, "0.25")]
    [InlineData(2.004, "2")]
    [InlineData(30, "30")]
    [InlineData(10, "10")]
    [InlineData(0, "0")]
    public void SecondsAreWrittenAsFewFiguresAsSayThem(double seconds, string text)
    {
        Assert.Equal(text, TimelineWords.Seconds(seconds));
    }

    [Fact]
    public void AClipAndACutsceneAreNamedForWhatTheyAre()
    {
        var clip = TimelineWords.For(PlayedOn.Owner);
        var cutscene = TimelineWords.For(PlayedOn.Scene);

        Assert.Equal("No clip open", clip.None);
        Assert.Equal("Make a clip", clip.Make);
        Assert.Equal("No cutscene open", cutscene.None);
        Assert.Equal("New cutscene", cutscene.Make);
        Assert.Equal("Cutscene length", cutscene.Length);
    }

    [Fact]
    public void AChannelIsNamedByItsFieldAndAnyBoneItBelongsTo()
    {
        Assert.Equal("power", TimelineWords.Channel(LampPower));
        Assert.Equal("rotation", TimelineWords.Channel(new ChannelAddress(string.Empty, null, "rotation")));
        Assert.Equal("arm · rotation", TimelineWords.Channel(new ChannelAddress(string.Empty, "arm", "rotation")));
    }

    [Fact]
    public void EveryEndingHoldAndEasingHasAName()
    {
        Assert.All(Enum.GetValues<WhenDone>(), done => Assert.NotEmpty(TimelineWords.Ending(done)));
        Assert.All(Enum.GetValues<Hold>(), hold => Assert.NotEmpty(TimelineWords.Holding(hold)));
        Assert.All(Enum.GetValues<Easing>(), shape => Assert.NotEmpty(TimelineWords.Shape(shape)));
        Assert.Equal("Ease in and out", TimelineWords.Shape(Easing.InOut));
    }
}
