using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

// The two thin rows between the ruler and the sheet. What a mark edit does is in MarkModelTests, so these
// check that flags and bars stand where the ruler says, and that the menus and prompts reach the model.
public sealed class TimelineStripTests
{
    private const string Intro = "/project/intro.oclip";

    private sealed record Rig(History History, ClipBench Bench, TimelinePanel Panel, Window Window);

    private static Rig Show(ClipDocument? clip = null, CutsceneDocument? cutscene = null)
    {
        var scene = new EditorScene([new SceneObject("box", "Box", ObjectKind.Group)]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);
        var bench = new ClipBench(history, () => scene);
        if (cutscene is not null)
        {
            bench.OpenDocument(Intro, cutscene);
        }
        else
        {
            bench.OpenDocument(Intro, clip ?? new ClipDocument("intro", 4));
        }

        var panel = new TimelinePanel(bench, new TimelineLinks { OnNew = () => { } });
        var window = new Window { Width = 900, Height = 500, Content = panel };
        window.Show();
        Headless.Render();
        return new Rig(history, bench, panel, window);
    }

    private static ClipDocument ClipWith(params Mark[] marks) => new("intro", 4, marks: marks);

    private static IReadOnlyList<Mark> MarksOf(Rig rig)
    {
        Assert.NotNull(rig.Bench.Clip);
        return rig.Bench.Clip.Marks;
    }

    private static bool Has(Window window, string name) => Headless.All<Control>(window).Any(one => one.Name == name);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    // The window the strip's prompt opened, once there is one.
    private static NamePrompt Prompt(Rig rig)
    {
        Assert.True(Headless.Until(() => rig.Window.OwnedWindows.Count == 1));
        return Assert.IsType<NamePrompt>(rig.Window.OwnedWindows[0]);
    }

    private static void Answer(NamePrompt prompt, string text)
    {
        Headless.Find<TextBox>(prompt, "name").Text = text;
        Click(Headless.Find<Button>(prompt, "ok"));
    }

    // Opens the menu on the mark's flag and takes the line named for it.
    private static void ChooseFor(Rig rig, int index, string line)
    {
        Headless.Click(rig.Window, $"timeline_mark_{index}");
        var menu = Headless.Find<Button>(rig.Window, $"timeline_mark_{index}").ContextMenu;
        Assert.NotNull(menu);
        var item = menu.Items.OfType<MenuItem>().First(one => one.Name == $"timeline_mark_{line}_{index}");
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.Render();
    }

    [Fact]
    public Task EveryMarkIsAFlagThatStandsWhereTheRulerPutsItsTime() => Headless.Run(() =>
    {
        var rig = Show(ClipWith(new Mark(1, "Door"), new Mark(3, "Gate")));
        var ruler = Headless.Find<TimelineRuler>(rig.Window, "timeline_ruler");
        var axis = new TimeAxis(Assert.IsType<ClipDocument>(rig.Bench.Clip), ruler.Bounds.Width);
        var origin = ruler.TranslatePoint(default, rig.Window);
        Assert.NotNull(origin);

        foreach (var (index, at) in new[] { (0, 1.0), (1, 3.0) })
        {
            var flag = Headless.Find<MarkFlag>(rig.Window, $"timeline_mark_{index}");
            var left = flag.TranslatePoint(default, rig.Window);
            Assert.NotNull(left);
            Assert.Equal(origin.Value.X + axis.XOf(at) - 6, left.Value.X, 1);
        }

        Assert.Equal("Door at 1", ToolTip.GetTip(Headless.Find<MarkFlag>(rig.Window, "timeline_mark_0")));
    });

    [Fact]
    public Task AClipWithNoMarksHasTheStripAndTheAddButtonOnly() => Headless.Run(() =>
    {
        var rig = Show();

        Assert.True(Has(rig.Window, "timeline_marks"));
        Assert.True(Has(rig.Window, "timeline_mark_add"));
        Assert.False(Has(rig.Window, "timeline_mark_0"));
    });

    [Fact]
    public Task TheAddButtonAsksForANameAndPutsTheMarkAtThePlayhead() => Headless.Run(() =>
    {
        var rig = Show();
        rig.Bench.At = 2;

        Headless.Click(rig.Window, "timeline_mark_add");
        var prompt = Prompt(rig);
        Assert.Equal("Add a mark", prompt.Title);
        Assert.Equal("Mark", Headless.Find<TextBox>(prompt, "name").Text);

        Answer(prompt, "Door");

        Assert.True(Headless.Until(() => MarksOf(rig).Count == 1));
        Assert.Equal(("Door", 2.0), (MarksOf(rig)[0].Name, MarksOf(rig)[0].At));
        Assert.True(Headless.Until(() => Has(rig.Window, "timeline_mark_0")));
    });

    [Fact]
    public Task CancellingOrLeavingTheNameBlankAddsNoMark() => Headless.Run(() =>
    {
        var rig = Show();

        Headless.Click(rig.Window, "timeline_mark_add");
        var cancelled = Prompt(rig);
        Click(Headless.Find<Button>(cancelled, "cancel"));
        Assert.True(Headless.Until(() => rig.Window.OwnedWindows.Count == 0));

        Headless.Click(rig.Window, "timeline_mark_add");
        Answer(Prompt(rig), "   ");
        Assert.True(Headless.Until(() => rig.Window.OwnedWindows.Count == 0));

        Assert.Empty(MarksOf(rig));
        Assert.Empty(rig.History.Labels);
    });

    [Fact]
    public Task AFlagOffersToRenameMoveAndDelete() => Headless.Run(() =>
    {
        var rig = Show(ClipWith(new Mark(1, "Door")));

        Headless.Click(rig.Window, "timeline_mark_0");

        var menu = Headless.Find<Button>(rig.Window, "timeline_mark_0").ContextMenu;
        Assert.NotNull(menu);
        Assert.Equal(["Rename", "Move to the playhead", "Delete"], menu.Items.OfType<MenuItem>().Select(item => item.Header));
    });

    [Fact]
    public Task RenamingAsksWithTheCurrentNameAndKeepsTheMarkInPlace() => Headless.Run(() =>
    {
        var rig = Show(ClipWith(new Mark(1, "Door")));

        ChooseFor(rig, 0, "rename");
        var prompt = Prompt(rig);
        Assert.Equal("Rename mark", prompt.Title);
        Assert.Equal("Door", Headless.Find<TextBox>(prompt, "name").Text);
        Answer(prompt, "Gate");

        Assert.True(Headless.Until(() => MarksOf(rig)[0].Name == "Gate"));
        Assert.Equal(1, MarksOf(rig)[0].At);
    });

    [Fact]
    public Task MovingTakesTheMarkToThePlayheadAndTheFlagFollows() => Headless.Run(() =>
    {
        var rig = Show(ClipWith(new Mark(1, "Door")));
        rig.Bench.At = 3;
        Headless.Render();
        var before = Headless.Find<MarkFlag>(rig.Window, "timeline_mark_0").TranslatePoint(default, rig.Window);

        ChooseFor(rig, 0, "move");
        Headless.Render();

        Assert.Equal(3, MarksOf(rig)[0].At);
        var after = Headless.Find<MarkFlag>(rig.Window, "timeline_mark_0").TranslatePoint(default, rig.Window);
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.True(after.Value.X > before.Value.X);
    });

    [Fact]
    public Task DeletingTakesTheFlagAway() => Headless.Run(() =>
    {
        var rig = Show(ClipWith(new Mark(1, "Door")));

        ChooseFor(rig, 0, "delete");

        Assert.Empty(MarksOf(rig));
        Assert.False(Has(rig.Window, "timeline_mark_0"));
    });

    [Fact]
    public Task TheFlagsAreNotMadeAgainWhileThePlayheadMoves() => Headless.Run(() =>
    {
        var rig = Show(ClipWith(new Mark(1, "Door")));
        var flag = Headless.Find<MarkFlag>(rig.Window, "timeline_mark_0");

        // Each frame of a play is a change to the bench, and a flag rebuilt for it would close its own menu.
        rig.Bench.At = 1.5;
        rig.Bench.At = 2;
        Headless.Render();

        Assert.Same(flag, Headless.Find<MarkFlag>(rig.Window, "timeline_mark_0"));
    });

    [Fact]
    public Task TheFlagsFollowTheWidthOfTheWindow() => Headless.Run(() =>
    {
        var rig = Show(ClipWith(new Mark(2, "Door")));
        var flag = Headless.Find<MarkFlag>(rig.Window, "timeline_mark_0");
        var wide = flag.TranslatePoint(default, rig.Window);

        rig.Window.Width = 600;
        Headless.Render();

        var narrow = Headless.Find<MarkFlag>(rig.Window, "timeline_mark_0").TranslatePoint(default, rig.Window);
        Assert.NotNull(wide);
        Assert.NotNull(narrow);
        Assert.True(narrow.Value.X < wide.Value.X);
    });

    [Fact]
    public Task AClipThatIsNotACutsceneHasNoShotStrip() => Headless.Run(() =>
    {
        var rig = Show();

        Assert.False(Headless.Find<TimelineStrip>(rig.Window, "timeline_shots").IsVisible);
    });

    [Fact]
    public Task ACutsceneShowsABarForEachShotAsWideAsItIsLong() => Headless.Run(() =>
    {
        var rig = Show(cutscene: new CutsceneDocument(new ClipDocument("intro", 4), [new CutsceneShot("wide", 0, 2), new CutsceneShot("close", 1.5, 1)]));
        var ruler = Headless.Find<TimelineRuler>(rig.Window, "timeline_ruler");
        var axis = new TimeAxis(Assert.IsType<ClipDocument>(rig.Bench.Clip), ruler.Bounds.Width);

        Assert.True(Headless.Find<TimelineStrip>(rig.Window, "timeline_shots").IsVisible);
        var wide = Headless.Find<Border>(rig.Window, "timeline_shot_0");
        var close = Headless.Find<Border>(rig.Window, "timeline_shot_1");
        Assert.Equal(2 * axis.PerSecond, wide.Bounds.Width, 1);
        Assert.Equal(axis.PerSecond, close.Bounds.Width, 1);
        Assert.Equal("wide", Assert.IsType<TextBlock>(wide.Child).Text);
        Assert.Equal("close, 1.5 to 2.5", ToolTip.GetTip(close));

        // Overlapping shots stand in the same lane, so the second starts before the first ends.
        var first = wide.TranslatePoint(default, rig.Window);
        var second = close.TranslatePoint(default, rig.Window);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(second.Value.X < first.Value.X + wide.Bounds.Width);
    });

    [Fact]
    public Task EditingTheShotsRedrawsTheBars() => Headless.Run(() =>
    {
        var rig = Show(cutscene: new CutsceneDocument(new ClipDocument("intro", 4), [new CutsceneShot("wide", 0, 2)]));

        rig.Bench.EditShots("Add shot", shots => [.. shots, new CutsceneShot("close", 2, 1)]);
        Headless.Render();

        Assert.True(Has(rig.Window, "timeline_shot_1"));
    });
}
