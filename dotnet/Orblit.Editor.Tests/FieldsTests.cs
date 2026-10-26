using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Added in C#. Dart reaches its field widgets through the inspector tests, and those come with row 2c.
public sealed class FieldsTests
{
    /// <summary>A window with one panel of fields, built again the way the inspector builds again.</summary>
    private sealed class Rig
    {
        private readonly Func<Rig, Fields, Control> _build;

        public Rig(Func<Rig, Fields, Control> build)
        {
            _build = build;
            var workspace = new Workspace("/project");
            workspace.Add(new SceneEntry("a", "A", scene: Scene));
            History = new History(workspace);
            Host = new FieldHost(History, Rebuild);
            History.Changed += Host.Changed;
            Window = new Window { Width = 400, Height = 400, Content = Built() };
            Window.Show();
            Headless.Render();
        }

        public EditorScene Scene { get; } = EditorScene.Starter();

        public History History { get; }

        public FieldHost Host { get; }

        public Window Window { get; }

        public int Rebuilds { get; private set; }

        private Control Built()
        {
            Host.Reset();
            return _build(this, new Fields(Host));
        }

        private void Rebuild()
        {
            Rebuilds++;
            Window.Content = Built();
        }
    }

    private static Rig Of(Func<Fields, Control> build) => new((_, fields) => build(fields));

    private static T In<T>(Rig rig, string name)
        where T : Control => Headless.Find<T>(rig.Window, name);

    [Fact]
    public Task ADragIsOneUndoStepAndTheRowIsNotRebuiltUnderThePointer() => Headless.Run(() =>
    {
        var rig = new Rig((self, fields) => fields.Drag(
            "Power",
            () => [self.Scene["sun"]!.Power],
            numbers => self.History.Run(new SetPower("a", "sun", "Sun", self.Scene["sun"]!.Power, numbers[0])),
            new DragOptions { Step = 0.5 }));
        var start = rig.Scene["sun"]!.Power;
        var at = Headless.Centre(rig.Window, "drag_power_0");

        rig.Window.MouseDown(at, MouseButton.Left);
        rig.Window.MouseMove(at + new Vector(4, 0));
        rig.Window.MouseMove(at + new Vector(10, 0));

        Assert.Equal(start + 5, rig.Scene["sun"]!.Power, 6);
        Assert.Equal(0, rig.Rebuilds);
        var shown = Headless.All<TextBlock>(In<Control>(rig, "drag_power_0")).Last().Text!;
        Assert.Equal(start + 5, double.Parse(shown, CultureInfo.InvariantCulture), 2);

        rig.Window.MouseUp(at + new Vector(10, 0), MouseButton.Left);
        Headless.Render();

        Assert.Equal(1, rig.Rebuilds);
        Assert.False(rig.Host.Holding);
        rig.History.Undo();
        Assert.Equal(start, rig.Scene["sun"]!.Power, 6);
        Assert.False(rig.History.CanUndo);
    });

    [Fact]
    public Task AClickOnAChoiceReportsThatOptionAndRebuildsOnce() => Headless.Run(() =>
    {
        string? got = null;
        var rig = Of(fields => fields.Choice("Mode", ["a", "b"], "a", option => got = option));

        Headless.Click(rig.Window, "choice_mode_b");

        Assert.Equal("b", got);
        Assert.Equal(1, rig.Rebuilds);
    });

    [Fact]
    public Task AFoldedSectionStaysFoldedAfterTheRebuildItCauses() => Headless.Run(() =>
    {
        var rig = Of(fields => fields.Section("Light", new TextBlock { Name = "inside", Text = "x" }));
        Assert.True(In<TextBlock>(rig, "inside").IsVisible);

        Headless.Click(rig.Window, "section_light");

        Assert.True(rig.Host.IsFolded("Light"));
        Assert.Equal(1, rig.Rebuilds);
        Assert.False(In<TextBlock>(rig, "inside").IsVisible);
    });

    [Fact]
    public Task ASwitchReportsTheOtherValue() => Headless.Run(() =>
    {
        bool? got = null;
        var rig = Of(fields => fields.Toggle("Shadows", true, on => got = on));

        Headless.Click(rig.Window, "toggle_shadows");

        Assert.False(got);
    });

    [Fact]
    public Task ASliderMovedWithoutThePointerSettlesAtOnce() => Headless.Run(() =>
    {
        double? got = null;
        var rig = Of(fields => fields.Slider("Fade", 1, at => got = at, new SliderOptions { Min = 0, Max = 10 }));

        In<Slider>(rig, "slider_fade").Value = 3;
        Headless.Render();

        Assert.Equal(3, got);
        Assert.Equal(1, rig.Rebuilds);
    });

    [Fact]
    public Task TypedTextIsHeldUntilTheBoxLosesFocus() => Headless.Run(() =>
    {
        var got = "";
        var rig = Of(fields => new StackPanel { Children = { fields.Entry("Name", "", text => got = text), new Button { Name = "elsewhere" } } });
        var box = In<TextBox>(rig, "entry_name");

        Assert.True(box.Focus());
        Assert.True(box.IsFocused);
        box.Text = "lamp";
        Headless.Render();

        Assert.Equal("lamp", got);
        Assert.True(rig.Host.Holding);
        Assert.Equal(0, rig.Rebuilds);

        Assert.True(In<Button>(rig, "elsewhere").Focus());
        Assert.False(box.IsFocused);
        Headless.Render();

        Assert.False(rig.Host.Holding);
        Assert.Equal("lamp", got);
        Assert.Equal(1, rig.Rebuilds);
    });

    [Fact]
    public Task APaletteSwatchIsReportedAndTheOneInUseIsMarked() => Headless.Run(() =>
    {
        Colour[] swatches = [new(0xFFAA0000), new(0xFF00AA00)];
        Colour? got = null;
        var rig = Of(fields => fields.Palette("Tint", swatches[0], picked => got = picked, swatches));

        Assert.Equal(2, In<Border>(rig, "swatch_tint_0").BorderThickness.Left);
        Assert.Equal(1, In<Border>(rig, "swatch_tint_1").BorderThickness.Left);

        Headless.Click(rig.Window, "swatch_tint_1");

        Assert.Equal(swatches[1], got);
    });

    [Fact]
    public Task ALayerCellReportsItsIndexAndTheNameRidesInTheTooltip() => Headless.Run(() =>
    {
        int? got = null;
        var rig = Of(fields => fields.Layers("Flags", 0b101, ["Fast"], index => got = index));

        Assert.Equal("1: Fast", ToolTip.GetTip(In<Border>(rig, "layer_flags_0")));
        Assert.Equal("Layer 2", ToolTip.GetTip(In<Border>(rig, "layer_flags_1")));

        Headless.Click(rig.Window, "layer_flags_2");

        Assert.Equal(2, got);
    });

    [Fact]
    public Task APickedMenuItemIsReportedWithTheValueItStandsFor() => Headless.Run(() =>
    {
        var got = 0;
        var rig = Of(fields => fields.Pick<int>("Shape", "Box", [new(1, "Box"), new(2, "Ball")], value => got = value));
        var menu = Assert.IsType<MenuFlyout>(In<Button>(rig, "pick_shape").Flyout);

        menu.Items.OfType<MenuItem>().First(one => one.Name == "pick_shape_ball").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal(2, got);
    });
}
