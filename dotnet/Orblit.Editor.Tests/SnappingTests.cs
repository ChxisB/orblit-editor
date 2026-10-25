namespace Orblit.Editor.Tests;

using Orblit.Editor.Viewport;
using Orblit.VectorMath;

public sealed class SnappingTests
{
    private const double Tolerance = 1e-9;

    private static double Degrees(double radians) => radians * 180 / Math.PI;

    [Fact]
    public void OffItChangesNothing()
    {
        var snap = new Snapping(on: false);
        Assert.Equal(1.234, snap.Place(1.234));
        Assert.Equal(0.1, snap.Turn(0.1));
        Assert.Equal(1.1, snap.PlaceAll(new Vector3(1.1, 2.2, 3.3)).X);
    }

    [Fact]
    public void APositionLandsOnTheNearestLine()
    {
        var snap = new Snapping(step: 0.25);
        Assert.Equal(0.25, snap.Place(0.3), Tolerance);
        Assert.Equal(0.5, snap.Place(0.4), Tolerance);
        Assert.Equal(-0.25, snap.Place(-0.3), Tolerance);
        Assert.Equal(0, snap.Place(0));
    }

    [Fact]
    public void TwoThingsDraggedAlongAnAxisLandOnTheSameLines()
    {
        var snap = new Snapping(step: 0.5);
        // The point of snapping the position rather than the movement: these two start at
        // different fractions and have to meet, not stay 0.13 apart.
        var a = new Vector3(0.13, 0, 0);
        var b = new Vector3(0.61, 0, 0);
        var axis = new Vector3(1, 0, 0);

        var movedA = a + snap.Along(a, a + new Vector3(1.9, 0, 0), axis);
        var movedB = b + snap.Along(b, b + new Vector3(1.42, 0, 0), axis);

        Assert.Equal(0, Doubles.Mod(movedA.X, 0.5), Tolerance);
        Assert.Equal(0, Doubles.Mod(movedB.X, 0.5), Tolerance);
        Assert.Equal(movedA.X, movedB.X, Tolerance);
    }

    [Fact]
    public void AMovementAcrossTheAxisIsIgnored()
    {
        var snap = new Snapping(step: 1);
        var from = Vector3.Zero();
        // The pointer wandered up and sideways; only what is along the axis counts, because that
        // is the handle somebody grabbed.
        var moved = snap.Along(from, new Vector3(2.4, 9, -7), new Vector3(1, 0, 0));
        Assert.Equal(0, moved.Y);
        Assert.Equal(0, moved.Z);
        Assert.Equal(2, moved.X, Tolerance);
    }

    [Fact]
    public void ATurnLandsOnAWholeNumberOfDegrees()
    {
        var snap = new Snapping(angle: 15);
        Assert.Equal(15, Degrees(snap.Turn(20 * Math.PI / 180)), Tolerance);
        Assert.Equal(30, Degrees(snap.Turn(23 * Math.PI / 180)), Tolerance);
        Assert.Equal(-15, Degrees(snap.Turn(-8 * Math.PI / 180)), Tolerance);
    }

    [Fact]
    public void TheStepsDoubleSoAShapeMadeAtOneLinesUpAtTheNext()
    {
        for (var i = 1; i < Snapping.Steps.Count; i++)
        {
            var bigger = Snapping.Steps[i];
            var smaller = Snapping.Steps[i - 1];
            Assert.True(bigger > smaller);
            // Every coarser step is a whole number of finer ones, which is what makes changing
            // the grid mid-build safe.
            Assert.True(Math.Abs(Doubles.Mod(bigger / smaller, 1)) < Tolerance, $"{bigger} over {smaller}");
        }
    }

    [Fact]
    public void CyclingStopsAtTheEndsRatherThanWrappingRound()
    {
        var snap = new Snapping(step: Snapping.Steps[^1]);
        Assert.Equal(Snapping.Steps[^1], snap.Coarser);
        snap.Step = Snapping.Steps[0];
        Assert.Equal(Snapping.Steps[0], snap.Finer);
        snap.Step = 0.1;
        Assert.Equal(0.5, snap.Coarser);
        Assert.Equal(0.05, snap.Finer);
    }

    // A metre cube standing on the ground, whose origin is its own middle.
    private static (Vector3 Min, Vector3 Max) Box(double centre) =>
        (new Vector3(0, centre - 0.5, 0), new Vector3(0, centre + 0.5, 0));

    [Fact]
    public void TheBaseByDefaultSoAThingStandsOnTheLine()
    {
        var snap = new Snapping(step: 1);
        Assert.Equal(SnapTo.Base, snap.To);

        var origin = new Vector3(0, 0.3, 0);
        var anchor = snap.AnchorFor(new Vector3(0, 1, 0), origin, Box(0.3));
        Assert.Equal(-0.5, anchor, Tolerance);

        // Moved a little, the underside lands on a line and the origin ends up half a metre
        // above it. The base was at -0.2 and asked to go to 0.2, which is nearer nought than one.
        var moved = origin + snap.Along(origin, origin + new Vector3(0, 0.4, 0), new Vector3(0, 1, 0), anchor);
        Assert.Equal(0.5, moved.Y, Tolerance);
        Assert.Equal(0, Doubles.Mod(moved.Y + anchor, snap.Step), Tolerance);
    }

    [Fact]
    public void TheOriginForSomebodyWhoPutItWhereTheyMeantTo()
    {
        var snap = new Snapping(step: 1, to: SnapTo.Pivot);
        Assert.Equal(0, snap.AnchorFor(new Vector3(0, 1, 0), new Vector3(0, 0.3, 0), Box(0.3)));

        var origin = new Vector3(0, 0.3, 0);
        var moved = origin + snap.Along(origin, origin + new Vector3(0, 0.4, 0), new Vector3(0, 1, 0));
        Assert.Equal(1, moved.Y, Tolerance);
    }

    [Fact]
    public void TheMiddleStraddlesItWhichIsWhatAWallWants()
    {
        var snap = new Snapping(step: 1, to: SnapTo.Centre);
        var anchor = snap.AnchorFor(new Vector3(0, 1, 0), new Vector3(0, 0.3, 0), Box(0.3));
        Assert.Equal(0, anchor, Tolerance);
    }

    [Fact]
    public void TheTopForHangingSomething()
    {
        var snap = new Snapping(step: 1, to: SnapTo.Top);
        var anchor = snap.AnchorFor(new Vector3(0, 1, 0), new Vector3(0, 0.3, 0), Box(0.3));
        Assert.Equal(0.5, anchor, Tolerance);
    }

    [Fact]
    public void AnOriginThatIsNotInTheMiddleIsStillMeasuredProperly()
    {
        // A shape built from y nought to one, so its origin is its own base.
        var snap = new Snapping(step: 1);
        var anchor = snap.AnchorFor(
            new Vector3(0, 1, 0),
            new Vector3(0, 0.3, 0),
            (new Vector3(0, 0.3, 0), new Vector3(0, 1.3, 0)));
        Assert.Equal(0, anchor, Tolerance);
    }

    [Fact]
    public void DraggingTheOtherWayMeasuresFromTheSameEnd()
    {
        // Down is still the base: which end of the box is the base does not depend on which way
        // somebody happens to be pulling.
        var snap = new Snapping(step: 1);
        var down = snap.AnchorFor(new Vector3(0, -1, 0), new Vector3(0, 0.3, 0), Box(0.3));
        Assert.Equal(0.5, Math.Abs(down), Tolerance);
    }

    [Fact]
    public void WithTheGridOffTheAnchorChangesNothing()
    {
        var snap = new Snapping(on: false, step: 1);
        var origin = new Vector3(0, 0.3, 0);
        var moved = origin + snap.Along(origin, origin + new Vector3(0, 0.4, 0), new Vector3(0, 1, 0), -0.5);
        Assert.Equal(0.7, moved.Y, Tolerance);
    }

    [Fact]
    public void AStepNobodyOfferedStillCyclesSomewhereSensible()
    {
        var snap = new Snapping(step: 0.37);
        Assert.Contains(snap.Coarser, Snapping.Steps);
        Assert.Contains(snap.Finer, Snapping.Steps);
    }
}
