using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

// The timeline as it is drawn: the bar, the ruler and the list of what moves. What each control decides
// is in TimelineModelTests, so these check that the controls are there, say the right thing and reach it.
public sealed class TimelinePanelTests
{
    private const string Wave = "/project/wave.oclip";

    private static readonly ChannelAddress LampPower = new(string.Empty, null, "light.power");

    private sealed record Rig(EditorScene Scene, History History, ClipBench Bench, TimelinePanel Panel, Window Window, List<string> Calls);

    private static Rig Show(bool open = true, TimelineLinks? links = null, SkeletonLook? look = null, double width = 900)
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
        if (open)
        {
            bench.OpenDocument(Wave, new ClipDocument("wave", 2));
        }

        var calls = new List<string>();
        var panel = new TimelinePanel(bench, links ?? new TimelineLinks { OnNew = () => calls.Add("new") }, look);
        var window = new Window { Width = width, Height = 500, Content = panel };
        window.Show();
        Headless.Render();
        return new Rig(scene, history, bench, panel, window, calls);
    }

    // A clip with the lamp's power keyed at the start and that key picked.
    private static Rig ShowKeyed(double width = 900)
    {
        var rig = Show(width: width);
        var lamp = rig.Scene["lamp"];
        Assert.NotNull(lamp);
        rig.Bench.Key(lamp, "light.power");
        rig.Bench.Selection = new HashSet<KeyRef> { new(LampPower, 0) };
        Headless.Render();
        return rig;
    }

    private static ClipDocument ClipOf(ClipBench bench)
    {
        Assert.NotNull(bench.Clip);
        return bench.Clip;
    }

    private static string TextOf(Control root, string name) => Assert.IsType<TextBlock>(Headless.Find<Control>(root, name)).Text!;

    private static bool Has(Window window, string name) => Headless.All<Control>(window).Any(one => one.Name == name);

    private static bool Says(Control root, string text) => Headless.All<TextBlock>(root).Any(one => one.Text == text);

    private static List<MenuItem> MenuOf(Window window, string name)
    {
        Headless.Click(window, name);
        var menu = Headless.Find<Button>(window, name).ContextMenu;
        Assert.NotNull(menu);
        return [.. menu.Items.OfType<MenuItem>()];
    }

    private static void Pick(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static void PressKey(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Headless.Render();
    }

    [Fact]
    public Task WithNothingOpenItSaysSoAndOffersToMakeAClip() => Headless.Run(() =>
    {
        var rig = Show(open: false);

        Assert.Equal("No clip open", TextOf(rig.Window, "timeline_none"));
        Assert.False(Has(rig.Window, "timeline_ruler"));

        Headless.Click(rig.Window, "button_make_a_clip");
        Assert.Equal(["new"], rig.Calls);
    });

    [Fact]
    public Task OpeningAClipSwapsTheMessageForTheTimeline() => Headless.Run(() =>
    {
        var rig = Show(open: false);

        rig.Bench.OpenDocument(Wave, new ClipDocument("wave", 2));
        Headless.Render();

        Assert.True(Has(rig.Window, "timeline_ruler"));
        Assert.False(Has(rig.Window, "timeline_none"));
    });

    [Fact]
    public Task TappingTheRulerPutsThePlayheadOnAFrameAndSaysWhere() => Headless.Run(() =>
    {
        var rig = Show();
        Assert.Equal("0.00 s   frame 0", TextOf(rig.Window, "timeline_where"));

        var ruler = Headless.Find<TimelineRuler>(rig.Window, "timeline_ruler");
        var clip = ClipOf(rig.Bench);
        var x = new TimeAxis(clip, ruler.Bounds.Width).XOf(1);
        var at = ruler.TranslatePoint(new Point(x, ruler.Bounds.Height / 2), rig.Window);
        Assert.NotNull(at);
        rig.Window.MouseDown(at.Value, MouseButton.Left);
        rig.Window.MouseUp(at.Value, MouseButton.Left);
        Headless.Render();

        Assert.Equal(1, rig.Bench.At, 1.0 / clip.Rate);
        Assert.Equal(ClipEdits.Snapped(rig.Bench.At, clip.Rate), rig.Bench.At, 1e-9);
        Assert.Equal(TimelineWords.Where(rig.Bench.At, clip.Rate), TextOf(rig.Window, "timeline_where"));
    });

    [Fact]
    public Task DeleteTakesTheKeysOnlyOnceThePanelHasFocus() => Headless.Run(() =>
    {
        var rig = ShowKeyed();
        var keyed = rig.History.Labels.Count;

        // Nothing here has been touched, so a Delete is not for the timeline.
        PressKey(rig.Window, PhysicalKey.Delete);
        Assert.Equal(keyed, rig.History.Labels.Count);

        Headless.Click(rig.Window, "timeline_ruler");
        PressKey(rig.Window, PhysicalKey.Delete);
        Assert.Equal("Delete key", rig.History.Labels[^1]);
        Assert.Empty(rig.Bench.Selection);
    });

    [Fact]
    public Task SpaceAndTheArrowsPlayAndStepOnceTheTimelineHasFocus() => Headless.Run(() =>
    {
        var rig = Show();
        Headless.Click(rig.Window, "timeline_ruler");
        rig.Bench.At = 0;

        PressKey(rig.Window, PhysicalKey.ArrowRight);
        Assert.Equal(1.0 / 30, rig.Bench.At, 1e-9);

        PressKey(rig.Window, PhysicalKey.Space);
        Assert.True(rig.Bench.Playing);
        PressKey(rig.Window, PhysicalKey.Space);
        Assert.False(rig.Bench.Playing);
    });

    [Fact]
    public Task AHeldArrowKeepsSteppingAndAHeldSpaceActsOnce() => Headless.Run(() =>
    {
        var rig = Show();
        Headless.Click(rig.Window, "timeline_ruler");
        rig.Bench.At = 0;

        rig.Window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.Equal(2.0 / 30, rig.Bench.At, 1e-9);

        rig.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        rig.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Assert.True(rig.Bench.Playing);
    });

    [Fact]
    public Task APlayingClipMovesThePlayheadAFrameAtATime() => Headless.Run(() =>
    {
        var rig = Show();

        Headless.Click(rig.Window, "timeline_play");
        Assert.True(rig.Bench.Playing);

        // The first frame of a play only reads the clock, so the playhead moves on a later one.
        Assert.True(Headless.Until(() => rig.Bench.At > 0));
        Headless.Click(rig.Window, "timeline_play");
        Assert.False(rig.Bench.Playing);
    });

    [Fact]
    public Task ThePlayButtonSaysWhichItIsNow() => Headless.Run(() =>
    {
        var rig = Show();
        var play = Headless.Find<Button>(rig.Window, "timeline_play");
        Assert.Equal("Play (Space)", ToolTip.GetTip(play));

        Headless.Click(rig.Window, play);
        Assert.Equal("Pause (Space)", ToolTip.GetTip(play));

        Headless.Click(rig.Window, play);
        Assert.Equal("Play (Space)", ToolTip.GetTip(play));
    });

    [Fact]
    public Task TheClipMenuOffersToRetargetOnlyWhereTheShellCan() => Headless.Run(() =>
    {
        var plain = Show();
        var names = MenuOf(plain.Window, "timeline_clip").Select(item => item.Name).ToList();
        Assert.Contains("timeline_clip_new", names);
        Assert.DoesNotContain("timeline_clip_retarget", names);

        var retargets = Show(links: new TimelineLinks { OnNew = () => { }, OnRetarget = () => { } });
        var items = MenuOf(retargets.Window, "timeline_clip");
        var retarget = Assert.Single(items, item => item.Name == "timeline_clip_retarget");
        Assert.Equal("Retarget to another model…", retarget.Header);
        Assert.Equal("Make a clip", Assert.Single(items, item => item.Name == "timeline_clip_new").Header);
    });

    [Fact]
    public Task TypingALengthChangesTheClip() => Headless.Run(() =>
    {
        var rig = Show();

        Headless.Find<TextBox>(rig.Window, "timeline_length").Text = "3";
        Headless.Render();

        Assert.Equal(3, ClipOf(rig.Bench).Duration);
    });

    [Fact]
    public Task TheRateMenuChangesTheRateAndTheBarSaysIt() => Headless.Run(() =>
    {
        var rig = Show();
        Assert.True(Says(Headless.Find<Button>(rig.Window, "timeline_rate"), "30 fps"));

        Pick(Assert.Single(MenuOf(rig.Window, "timeline_rate"), item => item.Name == "timeline_rate_24"));
        Headless.Render();

        Assert.Equal(24, ClipOf(rig.Bench).Rate);
        Assert.True(Says(Headless.Find<Button>(rig.Window, "timeline_rate"), "24 fps"));
    });

    [Fact]
    public Task TheOwnerButtonWarnsUntilTheClipIsPlayedOnSomething() => Headless.Run(() =>
    {
        var rig = Show();
        Assert.True(Says(Headless.Find<Button>(rig.Window, "timeline_owner"), "Played on nothing"));

        var lamp = rig.Scene["lamp"];
        Assert.NotNull(lamp);
        rig.Bench.Key(lamp, "light.power");
        Headless.Render();

        Assert.True(Says(Headless.Find<Button>(rig.Window, "timeline_owner"), "On Lamp"));
    });

    [Fact]
    public Task EachThingTheClipMovesIsAHeadingOverItsChannels() => Headless.Run(() =>
    {
        var rig = ShowKeyed();

        Assert.True(Says(Headless.Find<Border>(rig.Window, "timeline_heading_0"), "Lamp"));
        Assert.True(Says(Headless.Find<Border>(rig.Window, "timeline_channel_1"), "power"));
    });

    [Fact]
    public Task ATargetNothingIsCalledWarnsAndKeepsItsName() => Headless.Run(() =>
    {
        var rig = Show(open: false);
        var clip = new ClipDocument(
            "ghosts",
            1,
            channels: [new ClipChannel<double>("gone", "light.power", ChannelKind.Number, [new Key<double>(0, 1)])]);
        rig.Bench.OpenDocument("/project/ghosts.oclip", clip);
        Headless.Render();

        var name = Headless.All<TextBlock>(Headless.Find<Border>(rig.Window, "timeline_heading_0")).Single(one => one.Text == "gone");
        Assert.Equal(OrblitColors.Warn, name.Foreground);
    });

    [Fact]
    public Task PressingAChannelPointsTheCurveAtIt() => Headless.Run(() =>
    {
        var rig = ShowKeyed();
        Assert.Null(rig.Bench.Curve);

        Headless.Click(rig.Window, "timeline_channel_1");

        Assert.Equal(LampPower, rig.Bench.Curve);
    });

    [Fact]
    public Task TheCloseButtonOfAChannelShowsOnHoverAndRemovesIt() => Headless.Run(() =>
    {
        var rig = ShowKeyed();
        var close = Headless.Find<Border>(rig.Window, "timeline_close_1");
        Assert.False(close.IsVisible);

        rig.Window.MouseMove(Headless.Centre(rig.Window, "timeline_channel_1"));
        Headless.Render();
        Assert.True(close.IsVisible);

        Headless.Click(rig.Window, close);

        Assert.Null(ClipEdits.ChannelAt(ClipOf(rig.Bench), LampPower));
        Assert.Equal("Remove power", rig.History.Labels[^1]);
    });

    [Fact]
    public Task AnEmptyClipSaysHowToKeySomething() => Headless.Run(() =>
    {
        var rig = Show();

        Assert.True(Says(rig.Window, "Nothing keyed yet. Select something, then press the diamond beside a value in the inspector to key it here."));
        Assert.False(Has(rig.Window, "timeline_tracks"));
    });

    [Fact]
    public Task TheBoneChipsAreLeftOutWithoutSettings() => Headless.Run(() =>
    {
        var rig = Show();

        Assert.False(Has(rig.Window, "timeline_bones"));
        Assert.False(Has(rig.Window, "timeline_onion"));
    });

    [Fact]
    public Task TheBonesChipSwitchesTheBonesOnAndOff() => Headless.Run(() =>
    {
        var look = new SkeletonLook();
        var rig = Show(look: look);

        Headless.Click(rig.Window, "timeline_bones");
        Assert.True(look.Value.Bones);

        Headless.Click(rig.Window, "timeline_bones");
        Assert.False(look.Value.Bones);
    });

    [Fact]
    public Task OnionBringsTheBonesAndSwitchingThemOffTakesItOff() => Headless.Run(() =>
    {
        var look = new SkeletonLook();
        var rig = Show(look: look);

        Headless.Click(rig.Window, "timeline_onion");
        Assert.True(look.Value.Onion);
        Assert.True(look.Value.Bones);

        Headless.Click(rig.Window, "timeline_bones");
        Assert.False(look.Value.Bones);
        Assert.False(look.Value.Onion);
    });

    [Fact]
    public Task TheGhostMenuAppearsWithOnionAndSaysWhatItDraws() => Headless.Run(() =>
    {
        var look = new SkeletonLook();
        var rig = Show(look: look);
        var ghosts = Headless.Find<Button>(rig.Window, "timeline_ghosts");
        Assert.False(ghosts.IsVisible);

        Headless.Click(rig.Window, "timeline_onion");
        Assert.True(ghosts.IsVisible);
        Assert.True(Says(ghosts, "2 each side, every 2 frames"));

        look.Value = look.Value with { Before = 1, After = 1, Stride = 1 };
        Headless.Render();
        Assert.True(Says(ghosts, "1 each side, every frame"));
    });

    [Fact]
    public Task ANarrowTimelineWithOnionKeepsItsChipsAndItsEditsInReach() => Headless.Run(() =>
    {
        var look = new SkeletonLook { Value = new SkeletonSettings(Bones: true, Onion: true) };
        var narrow = Show(look: look, width: 480);

        Assert.True(Has(narrow.Window, "timeline_bones"));
        Assert.True(Headless.Centre(narrow.Window, "timeline_delete").X <= 480);
        Assert.True(narrow.Panel.Bounds.Width <= 480);
    });
}
