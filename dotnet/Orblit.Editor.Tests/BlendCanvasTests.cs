using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Platform;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

// Dart: orblit-editor/test/editor_shell_blends_test.dart, the tests that press a state or an arrow and
// drag a link, here on the canvas alone. The rest is added in C#: the arrow label's plural, a link let
// go on nothing, a link pressed and let go, the drop target being marked, and the chosen arrow's colour.
public sealed class BlendCanvasTests
{
    private const string MovesPath = "/project/moves.oblend";

    // Past the right edge of every blend below, which is at most 708 wide, and below the lowest box.
    private static readonly Point Empty = new(820, 420);

    private static BlendDocument Moves() => new(
        "Moves",
        [new BlendState("idle", new BlendClip("idle")), new BlendState("move", new BlendClip("walk"))],
        new Dictionary<string, double> { ["speed"] = 0 },
        [new BlendChange("move", from: "idle", when: BlendCondition.Above("speed", 0.1))]);

    private sealed class Rig
    {
        public Rig(BlendDocument? blend = null)
        {
            History = new History(new Workspace("/project"));
            Bench = new BlendBench(History);
            Bench.OpenDocument(MovesPath, blend ?? Moves());
            Window = new Window { Width = 900, Height = 500, Content = new BlendCanvas(Bench) };
            Window.Show();
            Headless.Render();
        }

        public History History { get; }

        public BlendBench Bench { get; }

        public Window Window { get; }

        public BlendDocument Blend => Bench.Blend!;

        public Border Box(string name) => Headless.Find<Border>(Window, name);

        public bool Has(string name) => Headless.All<Control>(Window).Any(one => one.Name == name);

        public string TextOf(string name) => Headless.Find<TextBlock>(Window, name).Text ?? "";

        public Point Centre(string name) => Headless.Centre(Window, name);

        public void Click(string name) => Headless.Click(Window, name);

        public void Press(Point at)
        {
            Window.MouseDown(at, MouseButton.Left);
            Window.MouseUp(at, MouseButton.Left);
            Headless.Render();
        }

        // The ember pixels in a part of the window, for what is drawn and has no control to ask.
        public int Orange(Rect region)
        {
            Headless.Render();
            Headless.Render();
            var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame.");
            using var locked = frame.Lock();
            var bytes = new byte[locked.RowBytes * locked.Size.Height];
            Marshal.Copy(locked.Address, bytes, 0, bytes.Length);
            var (red, blue) = locked.Format == PixelFormat.Bgra8888 ? (2, 0) : (0, 2);
            var count = 0;
            for (var y = (int)region.Top; y < (int)region.Bottom; y++)
            {
                for (var x = (int)region.Left; x < (int)region.Right; x++)
                {
                    var at = (y * locked.RowBytes) + (x * 4);
                    count += IsOrange(bytes[at + red], bytes[at + 1], bytes[at + blue]) ? 1 : 0;
                }
            }

            return count;
        }

        // The theme's ember, or a blend of it over the dark ground, and not a grey.
        private static bool IsOrange(byte red, byte green, byte blue) => red > 110 && red > green + 25 && green > blue + 15;

        public void Drag(string handle, Point to)
        {
            var from = Centre(handle);
            Window.MouseDown(from, MouseButton.Left);
            foreach (var step in new[] { 0.25, 0.5, 0.75, 1 })
            {
                Window.MouseMove(new Point(from.X + ((to.X - from.X) * step), from.Y + ((to.Y - from.Y) * step)));
            }

            Window.MouseUp(to, MouseButton.Left);
            Headless.Render();
        }
    }

    [Fact]
    public Task EachStateIsABoxWithItsNameAndWhatItPlays() => Headless.Run(() =>
    {
        var rig = new Rig();

        Assert.Equal("idle", rig.TextOf("state_name_idle"));
        Assert.Equal("move", rig.TextOf("state_name_move"));
        Assert.Contains("walk", rig.TextOf("state_plays_move"));
    });

    [Fact]
    public Task OnlyTheStartStateWearsAFlag() => Headless.Run(() =>
    {
        var rig = new Rig();

        Assert.True(rig.Has("start_idle"));
        Assert.False(rig.Has("start_move"));
    });

    [Fact]
    public Task AChangeIsALabelOnItsArrowSayingWhatMakesIt() => Headless.Run(() =>
    {
        var rig = new Rig();

        Assert.Equal(BlendConditionModel.Describe(BlendCondition.Above("speed", 0.1)), rig.TextOf("change_text_0"));
    });

    [Fact]
    public Task ChangesThatShareAnArrowShareItsLabelAndSayHowManyMore() => Headless.Run(() =>
    {
        var rig = new Rig(Moves().WithChangeAdded("idle", "move"));

        Assert.EndsWith(" +1", rig.TextOf("change_text_0"));
        Assert.False(rig.Has("change_1"));
    });

    [Fact]
    public Task AnArrowsLabelIsCentredOnTheMiddleOfItsArrow() => Headless.Run(() =>
    {
        var rig = new Rig();

        var middle = rig.Centre("change_0");

        // The arrow runs from the right edge of idle, at 184, to the left edge of move, at 276, at the height of the boxes' middles.
        Assert.Equal(230, middle.X, 1);
        Assert.Equal(54, middle.Y, 1);
    });

    [Fact]
    public Task ALoopsLabelHangsBelowItsStateFromItsLeftEdge() => Headless.Run(() =>
    {
        var rig = new Rig(Moves().WithChangeAdded("move", "move"));

        var label = rig.Box("change_1");
        var middle = rig.Centre("change_1");

        Assert.Equal(rig.Box("state_move").TranslatePoint(new Point(0, 0), rig.Window)!.Value.X, middle.X - (label.Bounds.Width / 2), 1);
        Assert.Equal(80 + 14, middle.Y, 1);
    });

    [Fact]
    public Task PressingAStateChoosesIt() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Click("state_move");

        Assert.Equal(new StatePicked("move"), rig.Bench.Pick);
        Assert.Equal(OrblitColors.EmberWash, rig.Box("state_move").Background);
        Assert.NotEqual(OrblitColors.EmberWash, rig.Box("state_idle").Background);
    });

    [Fact]
    public Task PressingBetweenTheBoxesChoosesNothing() => Headless.Run(() =>
    {
        var rig = new Rig();
        rig.Click("state_move");

        rig.Press(Empty);

        Assert.Equal(BlendPick.Nothing, rig.Bench.Pick);
        Assert.NotEqual(OrblitColors.EmberWash, rig.Box("state_move").Background);
    });

    [Fact]
    public Task PressingAnArrowChoosesItsChangeAndPressingAgainStepsToTheNextOneItStandsFor() => Headless.Run(() =>
    {
        var rig = new Rig(Moves().WithChangeAdded("idle", "move"));

        rig.Click("change_0");
        Assert.Equal(new ChangePicked(0), rig.Bench.Pick);
        Assert.Equal(OrblitColors.EmberWash, rig.Box("change_0").Background);

        rig.Click("change_0");
        Assert.Equal(new ChangePicked(1), rig.Bench.Pick);

        rig.Click("change_0");
        Assert.Equal(new ChangePicked(0), rig.Bench.Pick);
    });

    [Fact]
    public Task DraggingFromAStateOntoAnotherMakesAChangeAndChoosesIt() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Drag("link_move", rig.Centre("state_idle"));

        var change = rig.Blend.WrittenChanges[1];
        Assert.Equal(("move", "idle"), (change.From, change.To));
        Assert.Equal(new ChangePicked(1), rig.Bench.Pick);
        Assert.Equal(["Add change"], rig.History.Labels);
        Assert.True(rig.Has("change_1"));
    });

    [Fact]
    public Task ALinkLetGoOverItsOwnStateMakesAChangeThatStaysThere() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Drag("link_move", rig.Centre("state_move"));

        var change = rig.Blend.WrittenChanges[1];
        Assert.Equal(("move", "move"), (change.From, change.To));
    });

    [Fact]
    public Task ALinkMovedOnlyAHairBeforeItIsLetGoIsStillAPress() => Headless.Run(() =>
    {
        var rig = new Rig();
        var at = rig.Centre("link_move");

        rig.Window.MouseDown(at, MouseButton.Left);
        rig.Window.MouseMove(new Point(at.X + 2, at.Y));
        rig.Window.MouseUp(new Point(at.X + 2, at.Y), MouseButton.Left);
        Headless.Render();

        Assert.Single(rig.Blend.WrittenChanges);
        Assert.Empty(rig.History.Labels);
    });

    [Fact]
    public Task ALinkLetGoOnNothingMakesNoChange() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Drag("link_move", Empty);

        Assert.Single(rig.Blend.WrittenChanges);
        Assert.Empty(rig.History.Labels);
    });

    [Fact]
    public Task PressingALinkAndLettingGoMakesNoChangeAndChoosesNothing() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Click("link_move");

        Assert.Single(rig.Blend.WrittenChanges);
        Assert.Empty(rig.History.Labels);
        Assert.Equal(BlendPick.Nothing, rig.Bench.Pick);
    });

    [Fact]
    public Task ALinkFromTheBoxForAnyStateMakesAChangeThatStartsAnywhere() => Headless.Run(() =>
    {
        var rig = new Rig(Moves().WithChangeAdded(null, "move"));

        rig.Drag("link_anywhere", rig.Centre("state_idle"));

        var change = rig.Blend.WrittenChanges[2];
        Assert.Equal((null, "idle"), (change.From, change.To));
    });

    [Fact]
    public Task TheBoxUnderADragIsMarkedUntilItIsLetGo() => Headless.Run(() =>
    {
        var rig = new Rig();
        var from = rig.Centre("link_move");
        var over = rig.Centre("state_idle");

        rig.Window.MouseDown(from, MouseButton.Left);
        foreach (var step in new[] { 0.5, 1 })
        {
            rig.Window.MouseMove(new Point(from.X + ((over.X - from.X) * step), from.Y + ((over.Y - from.Y) * step)));
        }

        Headless.Render();
        Assert.Equal(new Thickness(2), rig.Box("state_idle").BorderThickness);
        Assert.Equal(OrblitColors.Ember, rig.Box("state_idle").BorderBrush);
        Assert.Equal(new Thickness(1), rig.Box("state_move").BorderThickness);
        Assert.NotEqual(OrblitColors.Ember, rig.Box("state_move").BorderBrush);

        rig.Window.MouseUp(over, MouseButton.Left);
        Headless.Render();
        Assert.Equal(new Thickness(1), rig.Box("state_idle").BorderThickness);
    });

    [Fact]
    public Task TheChosenChangesArrowIsDrawnInEmberAndTheOthersAreNot() => Headless.Run(() =>
    {
        var rig = new Rig(Moves().WithChangeAdded("move", "idle"));
        // Where the arrow back from move to idle leaves the foot of move, clear of its label.
        var foot = new Rect(350, 82, 8, 10);

        Assert.Equal(0, rig.Orange(foot));

        rig.Bench.Pick = new ChangePicked(1);

        Assert.True(rig.Orange(foot) > 0);
    });

    [Fact]
    public Task TheCanvasFollowsEditsMadeElsewhere() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Bench.Edit("Add state", one => one.WithStateAdded("run"));
        Headless.Render();

        Assert.True(rig.Has("state_run"));
    });

    [Fact]
    public Task WithNoBlendOpenThereIsNothingToDraw() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Bench.Close(MovesPath);
        Headless.Render();

        Assert.False(rig.Has("state_idle"));
    });
}
