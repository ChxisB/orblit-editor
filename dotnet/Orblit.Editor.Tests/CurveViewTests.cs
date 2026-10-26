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

// The curve as it is drawn and pressed, and the chips that bring it up. What a press does is in
// CurveModelTests, so these check that the chips swap the body, that pointer events reach the model at the
// right places, and that what is drawn is where the model says it is and looks picked when it is.
public sealed class CurveViewTests
{
    private const string Wave = "/project/wave.oclip";

    private static readonly ChannelAddress Power = new("box", null, "light.power");
    private static readonly ChannelAddress Flag = new("box", null, "visible");
    private static readonly Color InkColour = Color.Parse("#E4E9F0");

    private sealed record Rig(History History, ClipBench Bench, TimelinePanel Panel, Window Window, CurveView View);

    // Two seconds long. The power is 0 at the start, 10 at one second and 4 at the end, and the curve is up.
    private static Rig Show(double width = 900)
    {
        var scene = new EditorScene([new SceneObject("box", "Box", ObjectKind.Group)]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);
        var bench = new ClipBench(history, () => scene);
        var power = new ClipChannel<double>(
            "box",
            "light.power",
            ChannelKind.Number,
            [new Key<double>(0, 0, Hold.Linear), new Key<double>(1, 10, Hold.Linear), new Key<double>(2, 4, Hold.Linear)]);
        var visible = new ClipChannel<bool>("box", "visible", ChannelKind.Flag, [new Key<bool>(0, true), new Key<bool>(1, false)]);
        bench.OpenDocument(Wave, new ClipDocument("wave", 2, channels: [power, visible]));
        bench.Curve = Power;
        var panel = new TimelinePanel(bench, new TimelineLinks { OnNew = () => { } });
        var window = new Window { Width = width, Height = 500, Content = panel };
        window.Show();
        Headless.Render();
        Headless.Click(window, "timeline_view_curves");
        Headless.Render();
        return new Rig(history, bench, panel, window, Headless.Find<CurveView>(window, "timeline_curves"));
    }

    private static bool Has(Window window, string name) => Headless.All<Control>(window).Any(one => one.Name == name);

    private static CurveFrame FrameOf(Rig rig)
    {
        var frame = new CurveModel(rig.Bench).FrameOf(rig.View.Bounds.Width, rig.View.Bounds.Height);
        Assert.NotNull(frame);
        return frame;
    }

    // Where a point of the view is in the window.
    private static Point In(Rig rig, Spot spot)
    {
        var at = rig.View.TranslatePoint(new Point(spot.X, spot.Y), rig.Window);
        Assert.NotNull(at);
        return at.Value;
    }

    private static Point OnKey(Rig rig, int index)
    {
        var frame = FrameOf(rig);
        var key = frame.Channel.BoxedKeys[index];
        return In(rig, frame.At(key.At, (double)key.Value));
    }

    private static void Tap(Rig rig, Point at)
    {
        rig.Window.MouseDown(at, MouseButton.Left);
        rig.Window.MouseUp(at, MouseButton.Left);
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

    private static Key<object> KeyOf(Rig rig, int index)
    {
        Assert.NotNull(rig.Bench.Clip);
        var found = ClipEdits.ChannelAt(rig.Bench.Clip, Power);
        Assert.NotNull(found);
        return found.BoxedKeys[index];
    }

    // The colours the view draws in a rectangle of itself, as red, green and blue, a row at a time.
    private static (byte Red, byte Green, byte Blue)[] Region(Rig rig, int x, int y, int width, int height)
    {
        var size = new PixelSize((int)rig.View.Bounds.Width, (int)rig.View.Bounds.Height);
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(rig.View);
        var stride = width * 4;
        var buffer = Marshal.AllocHGlobal(stride * height);
        try
        {
            bitmap.CopyPixels(new PixelRect(x, y, width, height), buffer, stride * height, stride);
            var bytes = new byte[stride * height];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            var bgra = bitmap.Format == PixelFormat.Bgra8888;

            return
            [
                .. Enumerable.Range(0, width * height).Select(at =>
                    bgra ? (bytes[at * 4 + 2], bytes[at * 4 + 1], bytes[at * 4]) : (bytes[at * 4], bytes[at * 4 + 1], bytes[at * 4 + 2])),
            ];
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static (byte Red, byte Green, byte Blue) Drawn(Rig rig, double x, double y) => Region(rig, (int)x, (int)y, 1, 1)[0];

    private static (byte Red, byte Green, byte Blue) Of(Color colour) => (colour.R, colour.G, colour.B);

    // The most red any pixel has in a stretch of one column.
    private static int Reddest(Rig rig, double x, double top, double bottom) =>
        Region(rig, (int)x, (int)top, 1, (int)(bottom - top) + 1).Max(pixel => pixel.Red);

    private static double SlopeOf(Key<object> key)
    {
        Assert.NotNull(key.SlopeOut);
        return (double)key.SlopeOut.Value.Value;
    }

    [Fact]
    public Task TheChipsSwapTheSheetForTheCurveAndBack() => Headless.Run(() =>
    {
        var rig = Show();
        Assert.True(Has(rig.Window, "timeline_curves"));
        Assert.False(Has(rig.Window, "timeline_sheet"));
        Assert.Equal(OrblitColors.Raised, Headless.Find<Button>(rig.Window, "timeline_view_curves").Background);

        Headless.Click(rig.Window, "timeline_view_keys");

        Assert.True(Has(rig.Window, "timeline_sheet"));
        Assert.False(Has(rig.Window, "timeline_curves"));
        Assert.Equal(OrblitColors.Raised, Headless.Find<Button>(rig.Window, "timeline_view_keys").Background);
        Assert.NotEqual(OrblitColors.Raised, Headless.Find<Button>(rig.Window, "timeline_view_curves").Background);
        Assert.Empty(rig.History.Labels);
    });

    [Fact]
    public Task TheNamesStayBesideTheCurveInEitherView() => Headless.Run(() =>
    {
        var rig = Show();

        Assert.True(Has(rig.Window, "timeline_channel_1"));
        Assert.Equal(
            Headless.Find<TimelineTracks>(rig.Window, "timeline_tracks").TranslatePoint(default, rig.Window)?.Y,
            rig.View.TranslatePoint(default, rig.Window)?.Y);

        Headless.Click(rig.Window, "timeline_view_keys");
        Assert.True(Has(rig.Window, "timeline_channel_1"));
    });

    [Fact]
    public Task TheCurveStartsWhereTheRulerDoesAndTakesTheHeightBesideTheNames() => Headless.Run(() =>
    {
        var rig = Show();
        var ruler = Headless.Find<TimelineRuler>(rig.Window, "timeline_ruler");

        Assert.Equal(ruler.TranslatePoint(default, rig.Window)?.X, rig.View.TranslatePoint(default, rig.Window)?.X);
        Assert.Equal(ruler.Bounds.Width, rig.View.Bounds.Width);
        Assert.True(rig.View.Bounds.Height > 100);
    });

    [Fact]
    public Task PressingAKeyPicksItAndItIsDrawnPicked() => Headless.Run(() =>
    {
        var rig = Show();
        var frame = FrameOf(rig);
        var key = frame.At(1, 10);
        Assert.Equal(Of(InkColour), Drawn(rig, key.X, key.Y));

        Tap(rig, OnKey(rig, 1));

        Assert.Equal([new KeyRef(Power, 1)], rig.Bench.Selection);
        Assert.Equal(Of(OrblitColors.EmberColor), Drawn(rig, key.X, key.Y));
    });

    [Fact]
    public Task PressingOnNothingClearsWhatWasPicked() => Headless.Run(() =>
    {
        var rig = Show();
        Tap(rig, OnKey(rig, 1));

        Tap(rig, In(rig, new Spot(FrameOf(rig).Axis.XOf(0.5), 2)));

        Assert.Empty(rig.Bench.Selection);
    });

    [Fact]
    public Task AWobbleOfAFewPixelsIsAPressAndNotADrag() => Headless.Run(() =>
    {
        var rig = Show();
        var at = OnKey(rig, 1);

        rig.Window.MouseDown(at, MouseButton.Left);
        rig.Window.MouseMove(new Point(at.X + 2, at.Y));
        rig.Window.MouseUp(new Point(at.X + 2, at.Y), MouseButton.Left);
        Headless.Render();

        Assert.Equal([new KeyRef(Power, 1)], rig.Bench.Selection);
        Assert.Empty(rig.History.Labels);
    });

    [Fact]
    public Task DraggingAKeyMovesItInTimeAndOneUndoPutsItBack() => Headless.Run(() =>
    {
        var rig = Show();
        var frame = FrameOf(rig);

        Drag(rig, OnKey(rig, 1), In(rig, frame.At(1.5, 10)));

        Assert.Equal(1.5, KeyOf(rig, 1).At, 1e-9);
        Assert.Equal(10, (double)KeyOf(rig, 1).Value, 1e-9);
        Assert.Equal(["Move keys"], rig.History.Labels);
        rig.History.Undo();
        Assert.Equal(1, KeyOf(rig, 1).At, 1e-9);
    });

    [Fact]
    public Task DraggingAKeyUpRaisesItsValue() => Headless.Run(() =>
    {
        var rig = Show();
        var frame = FrameOf(rig);

        Drag(rig, OnKey(rig, 1), In(rig, frame.At(1, 10 + 20 / frame.PerUnit)));

        Assert.Equal(1, KeyOf(rig, 1).At, 1e-9);
        Assert.Equal(10 + 20 / frame.PerUnit, (double)KeyOf(rig, 1).Value, 1e-6);
    });

    [Fact]
    public Task DraggingAHandleBendsTheCurveThroughItsKey() => Headless.Run(() =>
    {
        var rig = Show();
        var frame = FrameOf(rig);
        Tap(rig, OnKey(rig, 1));

        Drag(rig, In(rig, CurveModel.Handle(frame, 1, 0, KeySide.Leaving)), In(rig, frame.At(1.5, 14)));

        Assert.Equal(["Bend curve"], rig.History.Labels);
        Assert.Equal(1, KeyOf(rig, 1).At, 1e-9);
        Assert.Equal(8, SlopeOf(KeyOf(rig, 1)), 1e-6);
    });

    [Fact]
    public Task LettingGoOffTheViewStillEndsTheDrag() => Headless.Run(() =>
    {
        var rig = Show();
        var frame = FrameOf(rig);

        // Well outside the view's own bounds, where the pointer is held by the capture and not by position.
        Drag(rig, OnKey(rig, 1), In(rig, frame.At(1.5, 10)) + new Point(0, -400));

        Assert.Equal(["Move keys"], rig.History.Labels);
        Assert.Equal(1.5, KeyOf(rig, 1).At, 1e-9);
    });

    [Fact]
    public Task TheCurveIsDrawnThroughItsKeysAndNotAwayFromThem() => Headless.Run(() =>
    {
        var rig = Show();
        var frame = FrameOf(rig);
        var halfway = frame.At(0.5, 5);

        // A line a pixel and a half wide seldom covers a whole pixel, so look for it in a short stretch.
        Assert.True(Reddest(rig, halfway.X, halfway.Y - 4, halfway.Y + 4) > 150);
        Assert.True(Reddest(rig, halfway.X, halfway.Y + 30, halfway.Y + 40) < 100);
    });

    [Fact]
    public Task ThePlayheadIsALineDownTheCurveAtTheCurrentTime() => Headless.Run(() =>
    {
        var rig = Show();
        var frame = FrameOf(rig);
        rig.Bench.At = 0.5;
        Headless.Render();

        var column = Math.Round(frame.Axis.XOf(0.5));
        var line = Drawn(rig, column, 2);
        var beside = Drawn(rig, column + 6, 2);
        Assert.True(line.Red > beside.Red + 60);
    });

    [Fact]
    public Task WhenThereIsNoCurveAMessageIsWrittenInItsPlace() => Headless.Run(() =>
    {
        var rig = Show();

        rig.Bench.Curve = Flag;
        Headless.Render();

        // The note is centred, so some of it lies in a band across the middle.
        var middle = (int)(rig.View.Bounds.Height / 2);
        var band = Region(rig, 0, middle - 8, (int)rig.View.Bounds.Width, 16);
        Assert.Contains(band, pixel => pixel.Red > 100);
    });
}
