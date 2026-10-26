namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

// What a press, a drag and a tap on a curve do. The clip is two seconds long and drawn 220 pixels wide, so a
// second is 100 pixels and a key at t stands at x = 10 + 100 t. The power is 0 at the start, 10 at one second
// and 4 at the end, and the view is 114 pixels tall. Where a value stands is asked of the frame, so no test
// leans on how the range is rounded.
public sealed class CurveModelTests
{
    private const string Wave = "/project/wave.oclip";
    private const double Width = 220;
    private const double Height = 114;

    private static readonly ChannelAddress Power = new("box", null, "light.power");
    private static readonly ChannelAddress Flag = new("box", null, "visible");

    private sealed record Rig(History History, ClipBench Bench, CurveModel Model);

    private static ClipChannel<double> Rising() => new(
        "box",
        "light.power",
        ChannelKind.Number,
        [new Key<double>(0, 0, Hold.Linear), new Key<double>(1, 10, Hold.Linear), new Key<double>(2, 4, Hold.Linear)]);

    private static Rig MakeRig(params ClipChannel[] channels)
    {
        var scene = new EditorScene([new SceneObject("box", "Box", ObjectKind.Group)]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);
        var bench = new ClipBench(history, () => scene);
        bench.OpenDocument(Wave, new ClipDocument("wave", 2, channels: channels.Length > 0 ? channels : [Rising()]));
        bench.Curve = Power;
        return new Rig(history, bench, new CurveModel(bench));
    }

    private static CurveFrame FrameOf(Rig rig, double height = Height)
    {
        var frame = rig.Model.FrameOf(Width, height);
        Assert.NotNull(frame);
        return frame;
    }

    private static ClipChannel Channel(Rig rig)
    {
        Assert.NotNull(rig.Bench.Clip);
        var found = ClipEdits.ChannelAt(rig.Bench.Clip, Power);
        Assert.NotNull(found);
        return found;
    }

    private static Key<object> KeyOf(Rig rig, int index) => Channel(rig).BoxedKeys[index];

    private static double SlopeOf(Slope<object>? slope)
    {
        Assert.NotNull(slope);
        return (double)slope.Value.Value;
    }

    private static double ValueOf(Rig rig, int index) => (double)KeyOf(rig, index).Value;

    private static Spot OnKey(CurveFrame frame, int index) => frame.At(frame.Channel.BoxedKeys[index].At, (double)frame.Channel.BoxedKeys[index].Value);

    private static KeyRef Of(int index) => new(Power, index);

    [Fact]
    public void WithNoChannelChosenItSaysToChooseOneAndDrawsNothing()
    {
        var rig = MakeRig();
        rig.Bench.Curve = null;

        Assert.Equal("Choose a number or a position on the left to draw its curve.", rig.Model.Note);
        Assert.Null(rig.Model.FrameOf(Width, Height));
    }

    [Fact]
    public void AFlagHasNoCurveAndSaysItsKeysAreOnTheSheet()
    {
        var rig = MakeRig(new ClipChannel<bool>("box", "visible", ChannelKind.Flag, [new Key<bool>(0, true), new Key<bool>(1, false)]));
        rig.Bench.Curve = Flag;

        Assert.Equal("A turn or a flag has no curve to draw. Its keys are edited on the sheet.", rig.Model.Note);
        Assert.Null(rig.Model.FrameOf(Width, Height));
    }

    [Fact]
    public void ACurveHasNothingToSayAndAFrameToDrawIn()
    {
        var rig = MakeRig();

        Assert.Null(rig.Model.Note);
        Assert.Equal(Power, ClipEdits.AddressOf(FrameOf(rig).Channel));
    }

    [Fact]
    public void TheViewSpansTheValuesWithAMarginAndKeepsThePaddingAtBothEnds()
    {
        var frame = FrameOf(MakeRig());

        Assert.Equal(-1, frame.Low, 1e-9);
        Assert.Equal(11, frame.High, 1e-9);
        Assert.Equal(CurveFrame.Pad, frame.YOf(frame.High), 1e-9);
        Assert.Equal(Height - CurveFrame.Pad, frame.YOf(frame.Low), 1e-9);
        Assert.Equal(7.5, frame.ValueOf(frame.YOf(7.5)), 1e-9);
    }

    [Fact]
    public void AChannelThatNeverChangesStillGetsARangeToDrawIn()
    {
        var flat = new ClipChannel<double>("box", "light.power", ChannelKind.Number, [new Key<double>(0, 5), new Key<double>(1, 5)]);
        var frame = FrameOf(MakeRig(flat));

        Assert.True(frame.Low < 5);
        Assert.True(frame.High > 5);
        Assert.True(frame.PerUnit > 0);
    }

    [Fact]
    public void AKeyIsFoundWithinSixPixelsAndNotBeyond()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        var key = OnKey(frame, 1);

        Assert.Equal(new Grip(1, 0, null), rig.Model.Hit(frame, key));
        Assert.Equal(new Grip(1, 0, null), rig.Model.Hit(frame, key with { X = key.X + 5.9 }));
        Assert.Null(rig.Model.Hit(frame, key with { X = key.X + 6.5 }));
    }

    [Fact]
    public void TappingAKeyPicksItAndShiftAddsAnotherAndTakesOneOff()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);

        rig.Model.Tap(frame, OnKey(frame, 1), shift: false);
        Assert.Equal([Of(1)], rig.Bench.Selection);

        rig.Model.Tap(frame, OnKey(frame, 2), shift: true);
        Assert.Equal(2, rig.Bench.Selection.Count);

        rig.Model.Tap(frame, OnKey(frame, 1), shift: true);
        Assert.Equal([Of(2)], rig.Bench.Selection);
    }

    [Fact]
    public void TappingOnNothingClearsWhatWasPickedAndShiftKeepsIt()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        rig.Model.Tap(frame, OnKey(frame, 1), shift: false);
        var empty = new Spot(frame.Axis.XOf(0.5), frame.YOf(frame.High));

        rig.Model.Tap(frame, empty, shift: true);
        Assert.Equal([Of(1)], rig.Bench.Selection);

        rig.Model.Tap(frame, empty, shift: false);
        Assert.Empty(rig.Bench.Selection);
    }

    [Fact]
    public void OnlyPickedKeysGrowHandlesAndTheEndsHaveOneEach()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        Assert.Empty(rig.Model.PickedHandles(frame));

        rig.Bench.Selection = new HashSet<KeyRef> { Of(0), Of(1), Of(2) };

        // The first key only leaves, the last only arrives, and the one between has both.
        var grips = rig.Model.PickedHandles(frame).Select(handle => handle.Grip).ToList();
        Assert.Equal(4, grips.Count);
        Assert.Contains(new Grip(0, 0, KeySide.Leaving), grips);
        Assert.Contains(new Grip(1, 0, KeySide.Arriving), grips);
        Assert.Contains(new Grip(1, 0, KeySide.Leaving), grips);
        Assert.Contains(new Grip(2, 0, KeySide.Arriving), grips);
    }

    [Fact]
    public void AHandleStandsAFixedLengthFromItsKeyOnTheSideItIsOn()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        var key = OnKey(frame, 1);

        var leaving = CurveModel.Handle(frame, 1, 0, KeySide.Leaving);
        var arriving = CurveModel.Handle(frame, 1, 0, KeySide.Arriving);

        Assert.Equal(CurveModel.HandleLength, key.Distance(leaving), 1e-6);
        Assert.Equal(CurveModel.HandleLength, key.Distance(arriving), 1e-6);
        Assert.True(leaving.X > key.X);
        Assert.True(arriving.X < key.X);
    }

    [Fact]
    public void TappingAHandleChangesNothing()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        rig.Bench.Selection = new HashSet<KeyRef> { Of(1) };
        var handle = CurveModel.Handle(frame, 1, 0, KeySide.Leaving);

        Assert.Equal(new Grip(1, 0, KeySide.Leaving), rig.Model.Hit(frame, handle));
        rig.Model.Tap(frame, handle, shift: false);

        Assert.Equal([Of(1)], rig.Bench.Selection);
    }

    [Fact]
    public void DraggingAKeyMovesItInTimeAndInValueAndOneUndoPutsItBack()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        var key = OnKey(frame, 1);

        rig.Model.Start(frame, key, shift: false);
        rig.Model.Update(frame, new Spot(key.X + 50, key.Y - 30), shift: false, alt: false);
        rig.Model.End();

        Assert.Equal(1.5, KeyOf(rig, 1).At, 1e-9);
        Assert.Equal(10 + 30 / frame.PerUnit, ValueOf(rig, 1), 1e-9);
        Assert.Equal(["Move keys"], rig.History.Labels);

        rig.History.Undo();
        Assert.Equal(1, KeyOf(rig, 1).At, 1e-9);
        Assert.Equal(10, ValueOf(rig, 1), 1e-9);
    }

    [Fact]
    public void ShiftKeepsADragToWhicheverWayItWentFurthest()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        var key = OnKey(frame, 1);

        rig.Model.Start(frame, key, shift: false);
        rig.Model.Update(frame, new Spot(key.X + 50, key.Y - 20), shift: true, alt: false);
        rig.Model.End();

        Assert.Equal(1.5, KeyOf(rig, 1).At, 1e-9);
        Assert.Equal(10, ValueOf(rig, 1), 1e-9);
    }

    [Fact]
    public void EveryPickedKeyOfTheChannelRisesWithTheOneDragged()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        rig.Bench.Selection = new HashSet<KeyRef> { Of(1), Of(2) };
        var key = OnKey(frame, 2);

        rig.Model.Start(frame, key, shift: false);
        rig.Model.Update(frame, key with { Y = key.Y - 20 }, shift: false, alt: false);
        rig.Model.End();

        Assert.Equal(10 + 20 / frame.PerUnit, ValueOf(rig, 1), 1e-9);
        Assert.Equal(4 + 20 / frame.PerUnit, ValueOf(rig, 2), 1e-9);
        Assert.Equal(0, ValueOf(rig, 0), 1e-9);
    }

    [Fact]
    public void TheRangeStaysPutWhileAKeyIsDraggedAndIsWorkedOutAgainAfter()
    {
        var rig = MakeRig();
        var before = FrameOf(rig);
        var key = OnKey(before, 1);

        rig.Model.Start(before, key, shift: false);
        rig.Model.Update(before, key with { Y = key.Y - 40 }, shift: false, alt: false);
        var during = FrameOf(rig);
        rig.Model.End();
        var after = FrameOf(rig);

        Assert.Equal(before.High, during.High, 1e-9);
        Assert.Equal(before.Low, during.Low, 1e-9);
        Assert.True(after.High > before.High);
    }

    [Fact]
    public void PressingAKeyThatWasNotPickedPicksItAndShiftKeepsTheOthers()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        rig.Bench.Selection = new HashSet<KeyRef> { Of(0) };

        rig.Model.Start(frame, OnKey(frame, 1), shift: true);
        rig.Model.End();
        Assert.Equal(2, rig.Bench.Selection.Count);

        rig.Bench.Selection = new HashSet<KeyRef> { Of(0) };
        rig.Model.Start(frame, OnKey(frame, 1), shift: false);
        rig.Model.End();
        Assert.Equal([Of(1)], rig.Bench.Selection);
    }

    [Fact]
    public void ADragThatNeverMovesLeavesNothingToUndo()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);

        rig.Model.Start(frame, OnKey(frame, 1), shift: false);
        rig.Model.Update(frame, OnKey(frame, 1), shift: false, alt: false);
        rig.Model.End();

        Assert.Empty(rig.History.Labels);
    }

    [Fact]
    public void ADragThatStartsOnNothingOrNeverStartedDoesNothing()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        var empty = new Spot(frame.Axis.XOf(0.5), frame.YOf(frame.High));

        rig.Model.Update(frame, empty, shift: false, alt: false);
        rig.Model.Start(frame, empty, shift: false);
        rig.Model.Update(frame, new Spot(empty.X + 40, empty.Y + 40), shift: false, alt: false);
        rig.Model.End();

        Assert.Empty(rig.History.Labels);
        Assert.Equal(1, KeyOf(rig, 1).At, 1e-9);
    }

    [Fact]
    public void DraggingAHandleSetsTheSlopeThroughTheKeyOnBothSides()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        rig.Bench.Selection = new HashSet<KeyRef> { Of(1) };
        var handle = CurveModel.Handle(frame, 1, 0, KeySide.Leaving);

        rig.Model.Start(frame, handle, shift: false);
        rig.Model.Update(frame, frame.At(1.5, 14), shift: false, alt: false);
        rig.Model.End();

        // Four up in half a second.
        var key = KeyOf(rig, 1);
        Assert.Equal(8, SlopeOf(key.SlopeIn), 1e-9);
        Assert.Equal(8, SlopeOf(key.SlopeOut), 1e-9);
        Assert.Equal(Hold.Curve, key.Hold);
        Assert.Equal(["Bend curve"], rig.History.Labels);
    }

    [Fact]
    public void HoldingAltBreaksTheHandlesSoOnlyTheOneDraggedTurns()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        rig.Bench.Selection = new HashSet<KeyRef> { Of(1) };
        var handle = CurveModel.Handle(frame, 1, 0, KeySide.Leaving);

        rig.Model.Start(frame, handle, shift: false);
        rig.Model.Update(frame, frame.At(1.5, 14), shift: false, alt: true);
        rig.Model.End();

        var key = KeyOf(rig, 1);
        Assert.Null(key.SlopeIn);
        Assert.Equal(8, SlopeOf(key.SlopeOut), 1e-9);
    }

    [Fact]
    public void AHandleCannotBePulledBackOverItsKey()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        rig.Bench.Selection = new HashSet<KeyRef> { Of(1) };
        var handle = CurveModel.Handle(frame, 1, 0, KeySide.Leaving);

        rig.Model.Start(frame, handle, shift: false);
        rig.Model.Update(frame, frame.At(0.5, 14), shift: false, alt: false);
        rig.Model.End();

        // Held to two pixels past the key: four up in a fiftieth of a second.
        var key = KeyOf(rig, 1);
        Assert.Equal(200, SlopeOf(key.SlopeOut), 1e-6);
    }

    [Fact]
    public void AViewTooShortToHoldAValueWritesNothing()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig, height: 20);
        var key = OnKey(frame, 1);

        rig.Model.Start(frame, key, shift: false);
        rig.Model.Update(frame, new Spot(key.X + 50, key.Y - 30), shift: false, alt: false);
        rig.Model.End();

        Assert.Empty(rig.History.Labels);
        Assert.Equal(1, KeyOf(rig, 1).At, 1e-9);
        Assert.Equal(10, ValueOf(rig, 1), 1e-9);
    }

    [Fact]
    public void KeysFromAnotherChannelAreNotPickedHere()
    {
        var rig = MakeRig();
        var frame = FrameOf(rig);
        rig.Bench.Selection = new HashSet<KeyRef> { Of(2), new(new ChannelAddress("box", null, "light.range"), 0), Of(9) };

        Assert.Equal([2], rig.Model.Picked(frame));
    }

    [Theory]
    [InlineData(0.7, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(12, 20)]
    [InlineData(0.03, 0.05)]
    [InlineData(0, 0)]
    [InlineData(-4, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void TheGridStepIsTheNextRoundNumberUp(double rough, double step) =>
        Assert.Equal(step, CurveGrid.NiceStep(rough), 1e-12);

    [Fact]
    public void GridLinesAreWholeMultiplesOfTheStepInsideTheRange()
    {
        Assert.Equal([0.0, 5, 10], CurveGrid.Lines(-1, 11, 5));
        Assert.Empty(CurveGrid.Lines(0.5, 0.9, 1));
    }

    [Fact]
    public void TheLineAtZeroIsNeverNegativeZero()
    {
        var lines = CurveGrid.Lines(-0.25, 0.25, 0.1).ToList();

        Assert.Equal(5, lines.Count);
        Assert.False(double.IsNegative(lines[2]));
        Assert.Equal(0, lines[2]);
    }

    [Theory]
    [InlineData(5, 5, "5")]
    [InlineData(0.5, 0.1, "0.5")]
    [InlineData(0.25, 0.05, "0.25")]
    [InlineData(-0.0, 1, "0")]
    [InlineData(-20, 10, "-20")]
    public void AGridLineIsWrittenWithAsManyDecimalsAsItsStepNeeds(double value, double step, string text) =>
        Assert.Equal(text, CurveGrid.Label(value, step));
}
