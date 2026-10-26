namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

// What a press, a drag and a tap on the sheet of keys do. The clip is two seconds long and drawn 220
// pixels wide, so a second is 100 pixels and a key at t stands at x = 10 + 100 t. A row is 22 pixels, and
// the rows are: 0 the box's heading, 1 its power, 2 its range, 3 the heading of whatever plays the clip,
// 4 its power.
public sealed class SheetModelTests
{
    private const string Wave = "/project/wave.oclip";
    private const double Row = 22;

    private static readonly ChannelAddress Power = new("box", null, "light.power");
    private static readonly ChannelAddress Range = new("box", null, "light.range");
    private static readonly ChannelAddress Loose = new(string.Empty, null, "light.power");

    private sealed record Rig(History History, ClipBench Bench, SheetModel Sheet, TimeAxis Axis);

    private static ClipChannel<double> Channel(string target, string property, params double[] times) =>
        new ClipChannel<double>(target, property, ChannelKind.Number, [.. times.Select(at => new Key<double>(at, at))]);

    private static Rig MakeRig(params ClipChannel[] channels)
    {
        var scene = new EditorScene([new SceneObject("box", "Box", ObjectKind.Group)]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);
        var bench = new ClipBench(history, () => scene);
        var clip = new ClipDocument("wave", 2, channels: channels.Length > 0 ? channels : Standard());
        bench.OpenDocument(Wave, clip);
        return new Rig(history, bench, new SheetModel(bench, Row), new TimeAxis(clip, 220));
    }

    private static ClipChannel[] Standard() =>
    [
        Channel("box", "light.power", 0, 1),
        Channel("box", "light.range", 1, 2),
        Channel(string.Empty, "light.power", 0, 2),
    ];

    private static double X(double at) => 10 + at * 100;

    private static double Middle(int row) => row * Row + Row / 2;

    private static KeyRef Of(ChannelAddress channel, int index) => new(channel, index);

    private static ClipDocument ClipOf(Rig rig)
    {
        Assert.NotNull(rig.Bench.Clip);
        return rig.Bench.Clip;
    }

    private static IReadOnlyList<double> TimesOf(Rig rig, ChannelAddress channel)
    {
        var found = ClipEdits.ChannelAt(ClipOf(rig), channel);
        Assert.NotNull(found);
        return [.. found.BoxedKeys.Select(key => key.At)];
    }

    [Fact]
    public void PressingOnAKeyFindsThatKeyAndNothingElse()
    {
        var rig = MakeRig();

        var hit = rig.Sheet.Hit(rig.Axis, X(1), Middle(1));

        Assert.Equal([Of(Power, 1)], hit);
    }

    [Fact]
    public void AKeyIsFoundWithinSevenPixelsAndNotBeyond()
    {
        var rig = MakeRig();

        Assert.Single(rig.Sheet.Hit(rig.Axis, X(1) + 7, Middle(1)));
        Assert.Single(rig.Sheet.Hit(rig.Axis, X(1) - 7, Middle(1)));
        Assert.Empty(rig.Sheet.Hit(rig.Axis, X(1) + 7.5, Middle(1)));
    }

    [Fact]
    public void PressingAboveOrBelowTheRowsFindsNothing()
    {
        var rig = MakeRig();

        Assert.Empty(rig.Sheet.Hit(rig.Axis, X(1), -3));
        Assert.Empty(rig.Sheet.Hit(rig.Axis, X(0), Middle(5)));
    }

    [Fact]
    public void ARowFindsOnlyItsOwnChannelsKeys()
    {
        var rig = MakeRig();

        // The range channel is keyed at 1 as well, one row down.
        Assert.Equal([Of(Range, 0)], rig.Sheet.Hit(rig.Axis, X(1), Middle(2)));
    }

    [Fact]
    public void AHeadingFindsEveryKeyOnTheMomentAcrossItsChannels()
    {
        var rig = MakeRig();

        var hit = rig.Sheet.Hit(rig.Axis, X(1), Middle(0));

        Assert.Equal(2, hit.Count);
        Assert.Contains(Of(Power, 1), hit);
        Assert.Contains(Of(Range, 0), hit);
    }

    [Fact]
    public void AHeadingDoesNotReachTheKeysOfAnotherThing()
    {
        var rig = MakeRig();

        // Both the power of whatever plays the clip and the box's power are keyed at 0.
        Assert.Equal([Of(Power, 0)], rig.Sheet.Hit(rig.Axis, X(0), Middle(0)));
        Assert.Equal([Of(Loose, 0)], rig.Sheet.Hit(rig.Axis, X(0), Middle(3)));
    }

    [Fact]
    public void WhenTwoKeysAreInReachTheNearerOneIsFound()
    {
        var rig = MakeRig(Channel("box", "light.power", 1, 1.05));

        // 1 is at 110 and 1.05 at 115, so 113 is nearer the second.
        Assert.Equal([Of(Power, 1)], rig.Sheet.Hit(rig.Axis, 113, Middle(1)));
        Assert.Equal([Of(Power, 0)], rig.Sheet.Hit(rig.Axis, 111, Middle(1)));
    }

    [Fact]
    public void ABoxPicksTheKeysInsideItOnTheRowsItReaches()
    {
        var rig = MakeRig();

        var picked = rig.Sheet.Boxed(rig.Axis, new SheetBox(X(0.9), Row + 2, X(2.1), Row * 3 - 2));

        Assert.Equal(3, picked.Count);
        Assert.Contains(Of(Power, 1), picked);
        Assert.Contains(Of(Range, 0), picked);
        Assert.Contains(Of(Range, 1), picked);
    }

    [Fact]
    public void ABoxMissesKeysBesideItAndKeepsToTheRowsItReaches()
    {
        var rig = MakeRig();

        Assert.Empty(rig.Sheet.Boxed(rig.Axis, new SheetBox(X(0.2), Row + 2, X(0.8), Row * 3 - 2)));

        // Only the last row is reached, so only the keys of whatever plays the clip are in it.
        var low = rig.Sheet.Boxed(rig.Axis, new SheetBox(X(0), Row * 4 + 8, X(2), Row * 4 + 12));
        Assert.Equal([Of(Loose, 0), Of(Loose, 1)], low.OrderBy(key => key.Index));
    }

    [Fact]
    public void TappingAKeyPicksItAndTapsElsewhereClear()
    {
        var rig = MakeRig();

        rig.Sheet.Tap(rig.Axis, X(1), Middle(1), shift: false);
        Assert.Equal([Of(Power, 1)], rig.Bench.Selection);

        rig.Sheet.Tap(rig.Axis, X(1), Middle(2), shift: false);
        Assert.Equal([Of(Range, 0)], rig.Bench.Selection);

        rig.Sheet.Tap(rig.Axis, X(0.5), Middle(1), shift: false);
        Assert.Empty(rig.Bench.Selection);
    }

    [Fact]
    public void ShiftAddsAKeyAndTakesItOffAgainWhenItWasPicked()
    {
        var rig = MakeRig();
        rig.Sheet.Tap(rig.Axis, X(1), Middle(1), shift: false);

        rig.Sheet.Tap(rig.Axis, X(2), Middle(2), shift: true);
        Assert.Equal(2, rig.Bench.Selection.Count);

        rig.Sheet.Tap(rig.Axis, X(1), Middle(1), shift: true);
        Assert.Equal([Of(Range, 1)], rig.Bench.Selection);
    }

    [Fact]
    public void ShiftOnNothingKeepsWhatWasPicked()
    {
        var rig = MakeRig();
        rig.Sheet.Tap(rig.Axis, X(1), Middle(1), shift: false);

        rig.Sheet.Tap(rig.Axis, X(0.5), Middle(1), shift: true);

        Assert.Equal([Of(Power, 1)], rig.Bench.Selection);
    }

    [Fact]
    public void ShiftOnAHeadingTakesAllOfItsKeysOffOnlyWhenEveryOneWasPicked()
    {
        var rig = MakeRig();
        rig.Sheet.Tap(rig.Axis, X(1), Middle(1), shift: false);

        // One of the heading's two keys is not picked yet, so the tap adds the other.
        rig.Sheet.Tap(rig.Axis, X(1), Middle(0), shift: true);
        Assert.Equal(2, rig.Bench.Selection.Count);

        rig.Sheet.Tap(rig.Axis, X(1), Middle(0), shift: true);
        Assert.Empty(rig.Bench.Selection);
    }

    [Fact]
    public void TappingOneKeyChoosesItsChannelForTheCurveButTappingAHeadingDoesNot()
    {
        var rig = MakeRig();

        rig.Sheet.Tap(rig.Axis, X(1), Middle(0), shift: false);
        Assert.Null(rig.Bench.Curve);

        rig.Sheet.Tap(rig.Axis, X(2), Middle(2), shift: false);
        Assert.Equal(Range, rig.Bench.Curve);
    }

    [Fact]
    public void DraggingFromNothingMakesABoxThatPicksAsItGrows()
    {
        var rig = MakeRig();

        rig.Sheet.Start(rig.Axis, X(0.5), Middle(1) - 5, shift: false);
        Assert.NotNull(rig.Sheet.Box);
        Assert.Empty(rig.Bench.Selection);

        rig.Sheet.Update(rig.Axis, X(1.1), Middle(1) + 2);
        Assert.Equal([Of(Power, 1)], rig.Bench.Selection);
        Assert.Equal(X(0.5), rig.Sheet.Box?.Left);
        Assert.Equal(X(1.1), rig.Sheet.Box?.Right);

        // Back the way it came, the key is out of it again.
        rig.Sheet.Update(rig.Axis, X(0.8), Middle(1) + 2);
        Assert.Empty(rig.Bench.Selection);

        rig.Sheet.End();
        Assert.Null(rig.Sheet.Box);
    }

    [Fact]
    public void ABoxDraggedOutBackwardsStillHasItsSidesInOrder()
    {
        var rig = MakeRig();

        rig.Sheet.Start(rig.Axis, X(1.5), Middle(2), shift: false);
        rig.Sheet.Update(rig.Axis, X(0.5), Middle(1));

        Assert.Equal(new SheetBox(X(0.5), Middle(1), X(1.5), Middle(2)), rig.Sheet.Box);
    }

    [Fact]
    public void AShiftBoxAddsToWhatWasPickedBeforeIt()
    {
        var rig = MakeRig();
        rig.Sheet.Tap(rig.Axis, X(2), Middle(2), shift: false);

        rig.Sheet.Start(rig.Axis, X(0.5), Middle(1) - 5, shift: true);
        rig.Sheet.Update(rig.Axis, X(1.1), Middle(1) + 2);
        rig.Sheet.End();

        Assert.Equal(2, rig.Bench.Selection.Count);
        Assert.Contains(Of(Range, 1), rig.Bench.Selection);
        Assert.Contains(Of(Power, 1), rig.Bench.Selection);
    }

    [Fact]
    public void DraggingAKeyMovesItByWholeFramesAndKeepsItPicked()
    {
        var rig = MakeRig();

        rig.Sheet.Start(rig.Axis, X(0), Middle(1), shift: false);
        rig.Sheet.Update(rig.Axis, X(0) + 50, Middle(1));
        rig.Sheet.End();

        Assert.Equal([0.5, 1], TimesOf(rig, Power));
        Assert.Equal([Of(Power, 0)], rig.Bench.Selection);
    }

    [Fact]
    public void ADragIsOneStepToUndoHoweverFarThePointerTravelled()
    {
        var rig = MakeRig();

        rig.Sheet.Start(rig.Axis, X(0), Middle(1), shift: false);
        foreach (var step in new[] { 10.0, 25, 40, 55 })
        {
            rig.Sheet.Update(rig.Axis, X(0) + step, Middle(1));
        }

        rig.Sheet.End();

        Assert.Equal(["Move keys"], rig.History.Labels);
        rig.History.Undo();
        Assert.Equal([0, 1], TimesOf(rig, Power));
    }

    [Fact]
    public void ADragThatNeverLeavesItsFrameMakesNoStep()
    {
        var rig = MakeRig();

        rig.Sheet.Start(rig.Axis, X(0), Middle(1), shift: false);
        rig.Sheet.Update(rig.Axis, X(0) + 1, Middle(1));
        rig.Sheet.End();

        Assert.Empty(rig.History.Labels);
    }

    [Fact]
    public void DraggingBackToWhereItStartedPutsTheKeysBackAsTheyWere()
    {
        var rig = MakeRig();

        rig.Sheet.Start(rig.Axis, X(0), Middle(1), shift: false);
        rig.Sheet.Update(rig.Axis, X(0) + 50, Middle(1));
        rig.Sheet.Update(rig.Axis, X(0), Middle(1));
        rig.Sheet.End();

        Assert.Equal([0, 1], TimesOf(rig, Power));
        Assert.Equal([Of(Power, 0)], rig.Bench.Selection);
    }

    [Fact]
    public void AKeyStopsAtTheEndsOfTheClip()
    {
        var rig = MakeRig();

        rig.Sheet.Start(rig.Axis, X(1), Middle(1), shift: false);
        rig.Sheet.Update(rig.Axis, X(1) + 500, Middle(1));
        rig.Sheet.End();

        Assert.Equal([0, 2], TimesOf(rig, Power));
    }

    [Fact]
    public void PressingOnAPickedKeyMovesEveryPickedKeyTogether()
    {
        var rig = MakeRig();
        rig.Sheet.Tap(rig.Axis, X(1), Middle(1), shift: false);
        rig.Sheet.Tap(rig.Axis, X(1), Middle(2), shift: true);

        rig.Sheet.Start(rig.Axis, X(1), Middle(1), shift: false);
        rig.Sheet.Update(rig.Axis, X(1) - 50, Middle(1));
        rig.Sheet.End();

        Assert.Equal([0, 0.5], TimesOf(rig, Power));
        Assert.Equal([0.5, 2], TimesOf(rig, Range));
        Assert.Equal(2, rig.Bench.Selection.Count);
    }

    [Fact]
    public void PressingOnAKeyThatIsNotPickedPicksItAloneBeforeMovingIt()
    {
        var rig = MakeRig();
        rig.Sheet.Tap(rig.Axis, X(2), Middle(2), shift: false);

        rig.Sheet.Start(rig.Axis, X(1), Middle(1), shift: false);
        rig.Sheet.Update(rig.Axis, X(1) - 50, Middle(1));
        rig.Sheet.End();

        Assert.Equal([0, 0.5], TimesOf(rig, Power));
        Assert.Equal([1, 2], TimesOf(rig, Range));
    }

    [Fact]
    public void AKeyDroppedOnAnotherReplacesItAndTheDragIsStillOneStep()
    {
        var rig = MakeRig(Channel("box", "light.power", 0, 1));

        rig.Sheet.Start(rig.Axis, X(0), Middle(1), shift: false);
        rig.Sheet.Update(rig.Axis, X(1), Middle(1));
        rig.Sheet.End();

        Assert.Equal([1], TimesOf(rig, Power));
        Assert.Equal(["Move keys"], rig.History.Labels);
    }

    [Fact]
    public void UpdatingAndEndingWithNothingStartedDoesNothing()
    {
        var rig = MakeRig();

        rig.Sheet.Update(rig.Axis, 50, 50);
        rig.Sheet.End();

        Assert.Empty(rig.History.Labels);
        Assert.Empty(rig.Bench.Selection);
        Assert.Null(rig.Sheet.Box);
    }
}
