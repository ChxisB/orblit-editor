namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

public sealed class ClipEditsTests
{
    private static readonly ChannelAddress Height = new("lamp", null, "light.power");

    private static readonly ChannelAddress Place = new(string.Empty, null, "transform.position");

    private static ClipDocument ClipWith(params Key<double>[] keys) => new(
        "test",
        2,
        channels: [new ClipChannel<double>(Height.Target, Height.Property, ChannelKind.Number, keys)]);

    private static IReadOnlyList<Key<object>> KeysOf(ClipDocument clip, ChannelAddress address) =>
        Assert.IsAssignableFrom<ClipChannel>(ClipEdits.ChannelAt(clip, address)).BoxedKeys;

    private static List<double> TimesOf(ClipDocument clip, ChannelAddress address) =>
        [.. KeysOf(clip, address).Select(key => key.At)];

    private static List<double> ValuesOf(ClipDocument clip, ChannelAddress address) =>
        [.. KeysOf(clip, address).Select(key => Assert.IsType<double>(key.Value))];

    private static double SlopeValue(Slope<object>? slope) => Assert.IsType<double>(Assert.NotNull(slope).Value);

    private static HashSet<KeyRef> Refs(ChannelAddress address, params int[] indices) =>
        [.. indices.Select(index => new KeyRef(address, index))];

    private static void AssertTimes(ClipDocument clip, ChannelAddress address, params double[] expected)
    {
        var times = TimesOf(clip, address);
        Assert.Equal(expected.Length, times.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], times[i], 1e-9);
        }
    }

    [Fact]
    public void SnappingLandsOnTheNearestFrameOfTheClipsOwnRate()
    {
        Assert.Equal(15.0 / 30, ClipEdits.Snapped(0.51, 30), 1e-12);
        Assert.Equal(12.0 / 24, ClipEdits.Snapped(0.51, 24), 1e-12);
        Assert.Equal(0.51, ClipEdits.Snapped(0.51, 0));
    }

    [Fact]
    public void KeyingMakesAChannelOfTheKindAskedFor()
    {
        var clip = ClipEdits.Keyed(
            new ClipDocument("empty", 1),
            Place,
            ChannelKind.Vector,
            new NewKey(0.5, new Vector3(1, 2, 3)));

        var channel = ClipEdits.ChannelAt(clip, Place);
        Assert.IsType<ClipChannel<Vector3>>(channel);
        Assert.Equal(Hold.Smooth, Assert.Single(KeysOf(clip, Place)).Hold);
        Assert.Equal(KeyMark.Keyed, ClipEdits.MarkOf(clip, Place, 0.5));
        Assert.Equal(KeyMark.Animated, ClipEdits.MarkOf(clip, Place, 0.25));
        Assert.Equal(KeyMark.Unkeyed, ClipEdits.MarkOf(clip, Height, 0.5));
    }

    [Fact]
    public void KeyingReplacesAKeyOnTheSameFrameAndKeepsHowItTravels()
    {
        var clip = ClipEdits.Keyed(
            ClipWith(new Key<double>(0, 1, Hold.Curve, slopeOut: 4.0), new Key<double>(1, 2)),
            Height,
            ChannelKind.Number,
            new NewKey(0, 5.0));

        var key = KeysOf(clip, Height)[0];
        Assert.Equal(5.0, Assert.IsType<double>(key.Value));
        Assert.Equal(Hold.Curve, key.Hold);
        Assert.Equal(4.0, SlopeValue(key.SlopeOut));
    }

    [Fact]
    public void ANewKeyTravelsTheWayTheKeyBeforeItDoes()
    {
        var clip = ClipEdits.Keyed(
            ClipWith(new Key<double>(0, 1, Hold.Linear), new Key<double>(1, 2)),
            Height,
            ChannelKind.Number,
            new NewKey(0.5, 9.0));

        AssertTimes(clip, Height, 0, 0.5, 1);
        Assert.Equal(Hold.Linear, KeysOf(clip, Height)[1].Hold);
    }

    [Fact]
    public void AFieldIsReadAsTheSceneWritesIt()
    {
        Assert.Same(ChannelKind.Number, ClipEdits.KindOfField(3L));
        Assert.Same(ChannelKind.Flag, ClipEdits.KindOfField(true));
        Assert.Same(ChannelKind.Vector, ClipEdits.KindOfField(new List<object?> { 1L, 2L, 3L }));
        Assert.Null(ClipEdits.KindOfField(new List<object?> { 1L, 2L, 3L, 1L }));
        Assert.Null(ClipEdits.KindOfField("#ffffff"));

        var turn = Assert.IsType<Quaternion>(ClipEdits.KeyValueOf(
            ChannelKind.Rotation,
            "transform.rotation",
            new List<object?> { 20L, 40L, 10L }));

        // Turns a model the way the field does, compared as the matrix a model is drawn with. Not through
        // a quaternion's own rotate, which turns by the inverse.
        var field = Doc.TransformComponent.RotationOf(new Vector3(20, 40, 10));
        var key = turn.AsRotationMatrix();
        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(field[i], key[i], 1e-9);
        }

        Assert.Equal(2.0, Assert.IsType<double>(ClipEdits.KeyValueOf(ChannelKind.Number, "light.power", 2L)));
    }

    [Fact]
    public void DeletingDropsTheKeysAndAChannelLeftWithNone()
    {
        var start = ClipWith(new Key<double>(0, 1), new Key<double>(1, 2));

        AssertTimes(ClipEdits.WithoutKeys(start, Refs(Height, 0)), Height, 1);
        Assert.Empty(ClipEdits.WithoutKeys(start, Refs(Height, 0, 1)).Channels);
    }

    private static ClipDocument ThreeKeys() =>
        ClipWith(new Key<double>(0, 1), new Key<double>(0.5, 2), new Key<double>(1, 3));

    [Fact]
    public void MovingGoesByWholeFramesAndSaysWhereTheKeysWent()
    {
        var moved = ClipEdits.MovedKeys(ThreeKeys(), Refs(Height, 0), 0.26);

        // 0.26 s is frame 7.8 at thirty, so eight frames.
        Assert.Equal(8.0 / 30, TimesOf(moved.Clip, Height)[0], 1e-9);
        Assert.True(moved.Keys.SetEquals(Refs(Height, 0)));
    }

    [Fact]
    public void PassingAnotherKeyReordersThemAndTheSelectionFollows()
    {
        var moved = ClipEdits.MovedKeys(ThreeKeys(), Refs(Height, 0), 0.8);

        AssertTimes(moved.Clip, Height, 0.5, 0.8, 1);
        Assert.Equal([2.0, 1.0, 3.0], ValuesOf(moved.Clip, Height));
        Assert.True(moved.Keys.SetEquals(Refs(Height, 1)));
    }

    [Fact]
    public void LandingOnAKeyReplacesIt()
    {
        var moved = ClipEdits.MovedKeys(ThreeKeys(), Refs(Height, 0), 0.5);

        AssertTimes(moved.Clip, Height, 0.5, 1);
        Assert.Equal([1.0, 3.0], ValuesOf(moved.Clip, Height));
        Assert.True(moved.Keys.SetEquals(Refs(Height, 0)));
    }

    [Fact]
    public void MovingStopsAtEitherEndOfTheClip()
    {
        var early = ClipEdits.MovedKeys(ThreeKeys(), Refs(Height, 1), -3);
        AssertTimes(early.Clip, Height, 0, 1);

        var late = ClipEdits.MovedKeys(ThreeKeys(), Refs(Height, 1, 2), 5);

        // The later key reaches the end, and the other keeps its distance.
        AssertTimes(late.Clip, Height, 0, 1.5, 2);
    }

    [Fact]
    public void MovingWorksFromTheClipAsTheDragBeganSoPassingOverAKeyAndBackLeavesIt()
    {
        var start = ThreeKeys();
        var refs = Refs(Height, 0);

        ClipEdits.MovedKeys(start, refs, 0.5);
        var back = ClipEdits.MovedKeys(start, refs, 0.1);

        AssertTimes(back.Clip, Height, 0.1, 0.5, 1);
    }

    [Fact]
    public void AShapedHoldWithNoShapeEasesInAndOut()
    {
        var clip = ClipEdits.WithHold(ClipWith(new Key<double>(0, 0), new Key<double>(1, 1)), Refs(Height, 0), Hold.Shaped);

        Assert.Equal(Easing.InOut, KeysOf(clip, Height)[0].Shape);
    }

    private static ClipDocument Hill() => ClipWith(new Key<double>(0, 0), new Key<double>(1, 1), new Key<double>(2, 0));

    [Fact]
    public void AnAlignedHandleSetsBothSlopesAndMakesBothSpansCurves()
    {
        var clip = ClipEdits.WithSlope(Hill(), new KeyRef(Height, 1), new SlopeDrag(KeySide.Leaving, 0, 3));

        var keys = KeysOf(clip, Height);
        Assert.Equal(3.0, SlopeValue(keys[1].SlopeIn));
        Assert.Equal(3.0, SlopeValue(keys[1].SlopeOut));
        Assert.Equal(Hold.Curve, keys[0].Hold);
        Assert.Equal(Hold.Curve, keys[1].Hold);
        Assert.Equal(Hold.Smooth, keys[2].Hold);
    }

    [Fact]
    public void ABrokenHandleSetsItsOwnSideOnly()
    {
        var clip = ClipEdits.WithSlope(Hill(), new KeyRef(Height, 1), new SlopeDrag(KeySide.Arriving, 0, -2, Broken: true));

        var keys = KeysOf(clip, Height);
        Assert.Equal(-2.0, SlopeValue(keys[1].SlopeIn));
        Assert.Null(keys[1].SlopeOut);
        Assert.Equal(Hold.Curve, keys[0].Hold);
        Assert.Equal(Hold.Smooth, keys[1].Hold);
    }

    [Fact]
    public void OnePartOfAVectorsSlopeTheRestAsTheCurveHadIt()
    {
        var clip = new ClipDocument(
            "test",
            2,
            channels:
            [
                new ClipChannel<Vector3>(
                    Place.Target,
                    Place.Property,
                    ChannelKind.Vector,
                    [
                        new Key<Vector3>(0, Vector3.Zero()),
                        new Key<Vector3>(1, new Vector3(1, 2, 3)),
                        new Key<Vector3>(2, new Vector3(2, 4, 6)),
                    ]),
            ]);

        var sloped = ClipEdits.WithSlope(clip, new KeyRef(Place, 1), new SlopeDrag(KeySide.Leaving, 1, 10));

        var slope = Assert.IsType<Vector3>(Assert.NotNull(KeysOf(sloped, Place)[1].SlopeOut).Value);

        // The worked-out slope through a straight line is the line's.
        Assert.Equal(1, slope.X, 1e-9);
        Assert.Equal(10, slope.Y);
        Assert.Equal(3, slope.Z, 1e-9);

        var reset = ClipEdits.WithAutoSlopes(sloped, Refs(Place, 1));
        Assert.Null(KeysOf(reset, Place)[1].SlopeOut);
        Assert.Equal(Hold.Curve, KeysOf(reset, Place)[1].Hold);
    }
}
