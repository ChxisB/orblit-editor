using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

// The sheet of keys as it is drawn and pressed. What a press does is in SheetModelTests, so these check
// that the sheet lines up with the ruler and the names, that pointer events reach the model with the
// right positions, and that the keys are drawn where they are and picked ones look it.
public sealed class DopeSheetTests
{
    private const string Wave = "/project/wave.oclip";
    private const double Row = 22;

    private static readonly ChannelAddress Power = new("box", null, "light.power");
    private static readonly ChannelAddress Range = new("box", null, "light.range");

    private sealed record Rig(History History, ClipBench Bench, TimelinePanel Panel, Window Window, DopeSheet Sheet);

    private static ClipChannel<double> Channel(string property, params double[] times) =>
        new("box", property, ChannelKind.Number, [.. times.Select(at => new Key<double>(at, at))]);

    // Two seconds long. The box's power is keyed at 0 and 1 and its range at 1 and 2, so the rows are
    // the box's heading, its power and its range.
    private static Rig Show(double width = 900, double rate = 30)
    {
        var scene = new EditorScene([new SceneObject("box", "Box", ObjectKind.Group)]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);
        var bench = new ClipBench(history, () => scene);
        bench.OpenDocument(Wave, new ClipDocument("wave", 2, rate: rate, channels: [Channel("light.power", 0, 1), Channel("light.range", 1, 2)]));
        var panel = new TimelinePanel(bench, new TimelineLinks { OnNew = () => { } });
        var window = new Window { Width = width, Height = 500, Content = panel };
        window.Show();
        Headless.Render();
        return new Rig(history, bench, panel, window, Headless.Find<DopeSheet>(window, "timeline_sheet"));
    }

    private static double Middle(int row) => row * Row + Row / 2;

    private static TimeAxis AxisOf(Rig rig) => new(Assert.IsType<ClipDocument>(rig.Bench.Clip), rig.Sheet.Bounds.Width);

    // Where a point of the sheet is in the window.
    private static Point In(Rig rig, double x, double y)
    {
        var at = rig.Sheet.TranslatePoint(new Point(x, y), rig.Window);
        Assert.NotNull(at);
        return at.Value;
    }

    private static Point OnKey(Rig rig, double time, int row) => In(rig, AxisOf(rig).XOf(time), Middle(row));

    private static void Tap(Rig rig, Point at, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        rig.Window.MouseDown(at, MouseButton.Left, modifiers);
        rig.Window.MouseUp(at, MouseButton.Left, modifiers);
        Headless.Render();
    }

    private static void Drag(Rig rig, Point from, Point to)
    {
        rig.Window.MouseDown(from, MouseButton.Left);
        foreach (var step in new[] { 0.25, 0.5, 0.75, 1 })
        {
            rig.Window.MouseMove(new Point(from.X + (to.X - from.X) * step, from.Y + (to.Y - from.Y) * step));
        }

        rig.Window.MouseUp(to, MouseButton.Left);
        Headless.Render();
    }

    private static IReadOnlyList<double> TimesOf(Rig rig, ChannelAddress channel)
    {
        var found = ClipEdits.ChannelAt(Assert.IsType<ClipDocument>(rig.Bench.Clip), channel);
        Assert.NotNull(found);
        return [.. found.BoxedKeys.Select(key => key.At)];
    }

    // The colour the sheet draws at a point of itself, as red, green and blue.
    private static (byte Red, byte Green, byte Blue) Drawn(Rig rig, double x, double y)
    {
        var size = new PixelSize((int)rig.Sheet.Bounds.Width, (int)rig.Sheet.Bounds.Height);
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(rig.Sheet);
        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            bitmap.CopyPixels(new PixelRect((int)x, (int)y, 1, 1), buffer, 4, 4);
            var pixel = new byte[4];
            Marshal.Copy(buffer, pixel, 0, 4);

            return bitmap.Format == PixelFormat.Bgra8888 ? (pixel[2], pixel[1], pixel[0]) : (pixel[0], pixel[1], pixel[2]);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static (byte Red, byte Green, byte Blue) Of(Color colour) => (colour.R, colour.G, colour.B);

    [Fact]
    public Task TheSheetStartsWhereTheRulerDoesAndStandsBesideTheNames() => Headless.Run(() =>
    {
        var rig = Show();
        var ruler = Headless.Find<TimelineRuler>(rig.Window, "timeline_ruler");
        var tracks = Headless.Find<TimelineTracks>(rig.Window, "timeline_tracks");

        Assert.Equal(ruler.TranslatePoint(default, rig.Window)?.X, rig.Sheet.TranslatePoint(default, rig.Window)?.X);
        Assert.Equal(ruler.Bounds.Width, rig.Sheet.Bounds.Width);
        Assert.Equal(tracks.TranslatePoint(default, rig.Window)?.Y, rig.Sheet.TranslatePoint(default, rig.Window)?.Y);
        Assert.True(rig.Sheet.Bounds.Height >= tracks.Bounds.Height);
    });

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.0)]
    public Task AClipWithARateThatRoundsToNoFramesStillDraws(double rate) => Headless.Run(() =>
    {
        var rig = Show(rate: rate);

        Headless.Render();

        Assert.NotNull(rig.Bench.Clip);
    });

    [Fact]
    public Task PressingAKeyPicksItAndShiftAddsAnother() => Headless.Run(() =>
    {
        var rig = Show();

        Tap(rig, OnKey(rig, 1, 1));
        Assert.Equal([new KeyRef(Power, 1)], rig.Bench.Selection);
        Assert.Equal(Power, rig.Bench.Curve);

        Tap(rig, OnKey(rig, 2, 2), RawInputModifiers.Shift);
        Assert.Equal(2, rig.Bench.Selection.Count);
    });

    [Fact]
    public Task PressingOnNothingClearsWhatWasPicked() => Headless.Run(() =>
    {
        var rig = Show();
        Tap(rig, OnKey(rig, 1, 1));

        Tap(rig, In(rig, AxisOf(rig).XOf(0.5), Middle(1)));

        Assert.Empty(rig.Bench.Selection);
    });

    [Fact]
    public Task TheRoomBelowTheRowsTakesAPressToo() => Headless.Run(() =>
    {
        var rig = Show();
        Tap(rig, OnKey(rig, 1, 1));
        Assert.True(rig.Sheet.Bounds.Height > Row * 4);

        Tap(rig, In(rig, AxisOf(rig).XOf(1), Row * 4 + 10));

        Assert.Empty(rig.Bench.Selection);
    });

    [Fact]
    public Task AWobbleOfAFewPixelsIsAPressAndNotADrag() => Headless.Run(() =>
    {
        var rig = Show();
        var at = OnKey(rig, 1, 1);

        rig.Window.MouseDown(at, MouseButton.Left);
        rig.Window.MouseMove(new Point(at.X + 2, at.Y));
        rig.Window.MouseUp(new Point(at.X + 2, at.Y), MouseButton.Left);
        Headless.Render();

        Assert.Equal([new KeyRef(Power, 1)], rig.Bench.Selection);
        Assert.Empty(rig.History.Labels);
    });

    [Fact]
    public Task DraggingAcrossEmptyRoomPicksTheKeysInTheBox() => Headless.Run(() =>
    {
        var rig = Show();
        var axis = AxisOf(rig);

        Drag(rig, In(rig, axis.XOf(0.5), Row + 2), In(rig, axis.XOf(2.2), Row * 3 - 2));

        Assert.Equal(3, rig.Bench.Selection.Count);
        Assert.Contains(new KeyRef(Power, 1), rig.Bench.Selection);
        Assert.Contains(new KeyRef(Range, 0), rig.Bench.Selection);
        Assert.Contains(new KeyRef(Range, 1), rig.Bench.Selection);
    });

    [Fact]
    public Task DraggingAKeyMovesItByFramesAndOneUndoPutsItBack() => Headless.Run(() =>
    {
        var rig = Show();
        var axis = AxisOf(rig);

        Drag(rig, OnKey(rig, 0, 1), In(rig, axis.XOf(0.5), Middle(1)));

        Assert.Equal([0.5, 1], TimesOf(rig, Power));
        Assert.Equal(["Move keys"], rig.History.Labels);
        rig.History.Undo();
        Assert.Equal([0, 1], TimesOf(rig, Power));
    });

    [Fact]
    public Task LettingGoOffTheSheetStillEndsTheDrag() => Headless.Run(() =>
    {
        var rig = Show();
        var axis = AxisOf(rig);

        // Well outside the sheet's own bounds, where the pointer is held by the capture and not by position.
        Drag(rig, OnKey(rig, 0, 1), In(rig, axis.XOf(1), Middle(1) - 400));

        // The key lands on the other and replaces it.
        Assert.Equal([1], TimesOf(rig, Power));
        Assert.Equal(["Move keys"], rig.History.Labels);
    });

    [Fact]
    public Task AKeyIsDrawnAtItsTimeAndAPickedOneLooksPicked() => Headless.Run(() =>
    {
        var rig = Show();
        var axis = AxisOf(rig);
        var plain = Drawn(rig, axis.XOf(1), Middle(1));
        Tap(rig, OnKey(rig, 1, 1));
        var picked = Drawn(rig, axis.XOf(1), Middle(1));

        Assert.Equal(Of(Color.Parse("#A6B0BF")), plain);
        Assert.Equal(Of(OrblitColors.EmberColor), picked);
    });

    [Fact]
    public Task AHeadingDrawsOneSummaryForEachMomentItsThingIsKeyedAt() => Headless.Run(() =>
    {
        var rig = Show();
        var axis = AxisOf(rig);

        // Both channels are keyed at 1, so the heading has the one diamond there, and none at 1.5.
        Assert.Equal(Of(Color.Parse("#A6B0BF")), Drawn(rig, axis.XOf(1), Middle(0)));
        Assert.NotEqual(Of(Color.Parse("#A6B0BF")), Drawn(rig, axis.XOf(1.5), Middle(0)));

        // It is picked only when every key on that moment is.
        Tap(rig, OnKey(rig, 1, 1));
        Assert.Equal(Of(Color.Parse("#A6B0BF")), Drawn(rig, axis.XOf(1), Middle(0)));
        Tap(rig, OnKey(rig, 1, 0));
        Assert.Equal(Of(OrblitColors.EmberColor), Drawn(rig, axis.XOf(1), Middle(0)));
    });

    [Fact]
    public Task ThePlayheadIsALineDownTheSheetAtTheCurrentTime() => Headless.Run(() =>
    {
        var rig = Show();
        var axis = AxisOf(rig);
        rig.Bench.At = 0.5;
        Headless.Render();

        // A line a pixel and a half wide seldom covers a whole pixel, so look for it being brighter than what is beside it.
        var line = Drawn(rig, axis.XOf(0.5), Row * 3 + 5);
        var beside = Drawn(rig, axis.XOf(0.5) + 6, Row * 3 + 5);
        Assert.True(line.Red > beside.Red + 60);
    });

    [Fact]
    public Task TheSheetFollowsTheWidthOfTheWindowAsTheRulerDoes() => Headless.Run(() =>
    {
        var rig = Show();
        var wide = OnKey(rig, 1, 1);

        rig.Window.Width = 600;
        Headless.Render();

        Assert.True(OnKey(rig, 1, 1).X < wide.X);
        Tap(rig, OnKey(rig, 1, 1));
        Assert.Equal([new KeyRef(Power, 1)], rig.Bench.Selection);
    });
}
