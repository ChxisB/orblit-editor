using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

// Dart: orblit-editor/test/blend_inspector_test.dart, six tests, and the condition and state fields it
// reaches through them. The rest is added in C#: a number that is refused, a point that moves in a line
// while its box is still being typed in, and the plural of change.
public sealed class BlendInspectorTests
{
    private const string MovesPath = "/project/moves.oblend";

    private static readonly string[] Clips = ["idle", "walk", "run"];

    private static BlendDocument Moves() => new(
        "Moves",
        [new BlendState("idle", new BlendClip("idle")), new BlendState("move", new BlendClip("walk"))],
        new Dictionary<string, double> { ["speed"] = 0 },
        [new BlendChange("move", from: "idle", when: BlendCondition.Above("speed", 0.1))]);

    private static BlendDocument Mixed() => new(
        "Mixed",
        [
            new BlendState(
                "move",
                new BlendLine("speed", [new LinePoint(0, new BlendClip("idle")), new LinePoint(1, new BlendClip("walk")), new LinePoint(2, new BlendClip("run"))])),
            new BlendState("idle", new BlendClip("idle")),
        ],
        new Dictionary<string, double> { ["speed"] = 0 });

    private sealed class Rig
    {
        public Rig(BlendDocument? blend = null, BlendPick? pick = null)
        {
            History = new History(new Workspace("/project"));
            Bench = new BlendBench(History);
            Bench.OpenDocument(MovesPath, blend ?? Moves());
            if (pick is not null)
            {
                Bench.Pick = pick;
            }

            Window = new Window { Width = 400, Height = 1800, Content = new BlendInspector(Bench, History, () => Clips) };
            Window.Show();
            Headless.Render();
        }

        public History History { get; }

        public BlendBench Bench { get; }

        public Window Window { get; }

        public BlendDocument Blend => Bench.Blend!;

        public T Find<T>(string name)
            where T : Control => Headless.Find<T>(Window, name);

        public bool Has(string name) => Headless.All<Control>(Window).Any(one => one.Name == name);

        public string TextOf(string name) => Find<TextBox>(name).Text ?? "";

        public string ShownBy(string pick) => Assert.IsType<TextBlock>(Find<Button>(pick).Content).Text ?? "";

        public void Click(string name) => Headless.Click(Window, name);

        public void Type(string box, string text)
        {
            var field = Find<TextBox>(box);
            Assert.True(field.Focus());
            field.Text = text;
            Headless.Render();
        }

        public void Choose(string pick, string item)
        {
            var menu = Assert.IsType<MenuFlyout>(Find<Button>(pick).Flyout);
            menu.Items.OfType<MenuItem>().First(one => one.Name == item).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Headless.Render();
        }
    }

    private static List<string> Names(BlendDocument blend) => [.. blend.WrittenStates.Select(static state => state.Name)];

    [Fact]
    public Task NothingChosenShowsTheBlendItsCountsAndItsInputs() => Headless.Run(() =>
    {
        var rig = new Rig();

        Assert.Equal("Moves", rig.TextOf("name_name"));
        Assert.Contains("2 states and 1 change.", Assert.IsType<TextBlock>(rig.Find<Control>("blend_summary")).Text);
        Assert.Equal("0", rig.TextOf("number_speed"));
        Assert.Equal("idle", rig.ShownBy("pick_starts_in"));
    });

    [Fact]
    public Task TypingAStateANameIsOneStepAndKeepsTheBox() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new StatePicked("move"));
        var box = rig.Find<TextBox>("name_name");

        foreach (var typed in new[] { "r", "ru", "run" })
        {
            rig.Type("name_name", typed);
        }

        Assert.Equal(["idle", "run"], Names(rig.Blend));
        Assert.Equal(new StatePicked("run"), rig.Bench.Pick);
        Assert.Equal(["Rename state"], rig.History.Labels);
        Assert.Same(box, rig.Find<TextBox>("name_name"));
        Assert.True(box.IsFocused);

        rig.History.Undo();

        Assert.Equal(["idle", "move"], Names(rig.Blend));
        Assert.False(rig.History.CanUndo);
    });

    [Fact]
    public Task ChoosingAnotherStateShowsItsOwnFields() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Bench.Pick = new StatePicked("move");
        Headless.Render();

        Assert.Equal("move", rig.TextOf("name_name"));
        Assert.Equal("One clip", rig.ShownBy("pick_plays"));
        Assert.Equal("walk", rig.TextOf("name_clip"));
        Assert.Equal("1", rig.TextOf("number_speed"));
    });

    [Fact]
    public Task AddingAChangeFromAStatePicksTheNewChange() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new StatePicked("idle"));

        rig.Click("button_add_a_change");

        Assert.Equal(new ChangePicked(1), rig.Bench.Pick);
        Assert.Equal(2, rig.Blend.WrittenChanges.Count);
        Assert.Equal("idle", rig.Blend.WrittenChanges[1].From);
        Assert.Equal("move", rig.Blend.WrittenChanges[1].To);
        Assert.Equal(["Add change"], rig.History.Labels);
    });

    [Fact]
    public Task AStateWithNoOtherStateCannotAddAChange() => Headless.Run(() =>
    {
        var alone = new BlendDocument("Alone", [new BlendState("idle", new BlendClip("idle"))]);
        var rig = new Rig(alone, new StatePicked("idle"));

        Assert.False(rig.Find<Button>("button_add_a_change").IsEnabled);
        Assert.False(rig.Find<Button>("button_delete").IsEnabled);
    });

    [Fact]
    public Task EarlierAndLaterChangeWhichChangeIsTakenFirst() => Headless.Run(() =>
    {
        var two = Moves().CopyWith(changes: [.. Moves().WrittenChanges, new BlendChange("idle", from: "move")]);
        var rig = new Rig(two, new ChangePicked(1));

        Assert.True(rig.Find<Button>("button_earlier").IsEnabled);
        Assert.False(rig.Find<Button>("button_later").IsEnabled);

        rig.Click("button_earlier");

        Assert.Equal(new ChangePicked(0), rig.Bench.Pick);
        Assert.Equal("move", rig.Blend.WrittenChanges[0].From);
        Assert.False(rig.Find<Button>("button_earlier").IsEnabled);

        rig.Click("button_later");

        Assert.Equal(new ChangePicked(1), rig.Bench.Pick);
        Assert.Equal("idle", rig.Blend.WrittenChanges[0].From);
    });

    [Fact]
    public Task AChangeCanBeDeletedAndNothingIsLeftChosen() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new ChangePicked(0));

        rig.Click("button_delete");

        Assert.Empty(rig.Blend.WrittenChanges);
        Assert.Equal(BlendPick.Nothing, rig.Bench.Pick);
        Assert.Equal(["Delete change"], rig.History.Labels);
    });

    [Fact]
    public Task AnInputCanBeTakenOut() => Headless.Run(() =>
    {
        var rig = new Rig(Moves().CopyWith(changes: []));

        rig.Click("remove_input_speed");

        Assert.Empty(rig.Blend.Inputs);
        Assert.Equal(["Remove input speed"], rig.History.Labels);
    });

    [Fact]
    public Task AnInputIsAddedByNameAndTheBoxIsEmptiedAfterwards() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Type("add_new_input", " slope ");
        rig.Click("add_new_input_button");

        Assert.Equal(0, rig.Blend.Inputs["slope"]);
        Assert.Equal("", rig.TextOf("add_new_input"));
    });

    [Fact]
    public Task AStateIsAddedByClipAndStartsFromTheFirstClipWhenTheBoxIsEmpty() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Click("add_new_state_button");

        Assert.Equal(["idle", "move", "idle 2"], Names(rig.Blend));
        Assert.Equal(new StatePicked("idle 2"), rig.Bench.Pick);

        // The new state is chosen, so the blend's own fields are gone until it is let go.
        rig.Bench.Pick = BlendPick.Nothing;
        Headless.Render();
        rig.Choose("add_new_state_choices", "option_new_state_run");
        rig.Click("add_new_state_button");

        Assert.Equal("run", Names(rig.Blend)[^1]);
        Assert.Equal(new StatePicked("run"), rig.Bench.Pick);
    });

    [Fact]
    public Task StartingInAnotherStateIsAStep() => Headless.Run(() =>
    {
        var rig = new Rig();

        rig.Choose("pick_starts_in", "pick_starts_in_move");

        Assert.Equal("move", rig.Blend.WrittenStart);
        Assert.Equal(["Set start state"], rig.History.Labels);
    });

    [Fact]
    public Task TheStartStateCannotBeToldToStartHereAgain() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new StatePicked("idle"));

        Assert.Equal("Starts here", rig.Find<Button>("button_starts_here").Content);
        Assert.False(rig.Find<Button>("button_starts_here").IsEnabled);

        rig.Bench.Pick = new StatePicked("move");
        Headless.Render();
        rig.Click("button_start_here");

        Assert.Equal("move", rig.Blend.WrittenStart);
    });

    [Fact]
    public Task ASpeedBelowNoughtIsRefusedAndMakesNoStep() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new StatePicked("idle"));

        rig.Type("number_speed", "-1");

        Assert.False(rig.History.CanUndo);
        Assert.Equal(1, rig.Blend.WrittenStates[0].Speed);

        rig.Type("number_speed", "2");

        Assert.Equal(2, rig.Blend.WrittenStates[0].Speed);
        Assert.Equal(["Change speed"], rig.History.Labels);
    });

    [Fact]
    public Task ANumberThatIsNotAFiniteNumberMakesNoStep() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new ChangePicked(0));

        rig.Type("number_length", "Infinity");
        rig.Type("number_length", "NaN");

        Assert.False(rig.History.CanUndo);
        Assert.Equal(0, rig.Blend.WrittenChanges[0].Fade);
    });

    [Fact]
    public Task AnEditBeingTypedWhenThePanelLeavesIsSealedSoTheNextOneIsAStepOfItsOwn() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new StatePicked("idle"));
        rig.Type("name_name", "r");

        rig.Window.Content = null;
        Headless.Render();
        rig.Bench.Edit("Rename state", one => one.WithStateRenamed("r", "ru"), new BlendEditOptions(Gesture: "Rename state"));

        Assert.Equal(["Rename state", "Rename state"], rig.History.Labels);
    });

    [Fact]
    public Task OneSyncMarkIsRefusedAndTwoAreKept() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new StatePicked("idle"));

        rig.Type("name_sync_marks", "left");

        Assert.False(rig.History.CanUndo);

        rig.Type("name_sync_marks", "left, right");

        Assert.Equal(["left", "right"], rig.Blend.WrittenStates[0].Sync);
        Assert.Equal(["Change sync marks"], rig.History.Labels);
    });

    [Fact]
    public Task WhatHappensAtTheEndIsChosenAndTheClipsOwnClearsIt() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new StatePicked("idle"));

        Assert.Equal("The clip's own", rig.ShownBy("pick_at_the_end"));

        rig.Choose("pick_at_the_end", "pick_at_the_end_bounce");

        Assert.Equal(WhenDone.Bounce, rig.Blend.WrittenStates[0].WhenDone);

        rig.Choose("pick_at_the_end", "pick_at_the_end_the_clip_s_own");

        Assert.Null(rig.Blend.WrittenStates[0].WhenDone);
    });

    [Fact]
    public Task ASingleClipCanBecomeAMixAlongAnInputAndBackAgain() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new StatePicked("move"));

        rig.Choose("pick_plays", "pick_plays_a_mix_along_an_input");

        var line = Assert.IsType<BlendLine>(rig.Blend.WrittenStates[1].Plays);
        Assert.Equal("speed", line.Input);
        Assert.Equal(["walk", "walk"], line.Clips);
        Assert.Equal("A mix along an input", rig.ShownBy("pick_plays"));

        rig.Choose("pick_plays", "pick_plays_one_clip");

        Assert.Equal("walk", Assert.IsType<BlendClip>(rig.Blend.WrittenStates[1].Plays).Clip);
    });

    [Fact]
    public Task AStateThatPlaysAPlaneIsReadAndLeftToTheFile() => Headless.Run(() =>
    {
        var plane = new BlendDocument(
            "Plane",
            [new BlendState("move", new BlendPlane("speed", "turn", [new PlanePoint(0, 0, new BlendClip("idle"))]))],
            new Dictionary<string, double> { ["speed"] = 0, ["turn"] = 0 });
        var rig = new Rig(plane, new StatePicked("move"));

        Assert.False(rig.Has("pick_plays"));
        Assert.Contains("is edited in the file", Assert.IsType<TextBlock>(rig.Find<Control>("plays_note")).Text);
    });

    [Fact]
    public Task AMovedPointIsStillTheOneBeingTypedInOnceItsPlaceInTheLineChanges() => Headless.Run(() =>
    {
        var rig = new Rig(Mixed(), new StatePicked("move"));

        rig.Type("number_at_1", "3");
        rig.Type("number_at_1", "3.5");

        var line = Assert.IsType<BlendLine>(rig.Blend.WrittenStates[0].Plays);
        Assert.Equal([1d, 2d, 3.5], line.Points.Select(static point => point.At));
        Assert.Equal(["walk", "run", "idle"], line.Clips);
        Assert.Equal(["Move a point"], rig.History.Labels);
    });

    [Fact]
    public Task PointsAreAddedAndTakenOutOfALineAndTheLastCannotBeTaken() => Headless.Run(() =>
    {
        var rig = new Rig(Mixed(), new StatePicked("move"));

        rig.Click("button_add_a_point");

        Assert.Equal(4, Assert.IsType<BlendLine>(rig.Blend.WrittenStates[0].Plays).Points.Count);

        rig.Click("remove_point_1");
        rig.Click("remove_point_1");
        rig.Click("remove_point_1");

        Assert.Single(Assert.IsType<BlendLine>(rig.Blend.WrittenStates[0].Plays).Points);
        Assert.False(rig.Has("remove_point_1"));
    });

    [Fact]
    public Task AChangeShowsItsConditionAndAKindIsChosenFromAMenu() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new ChangePicked(0));

        Assert.Equal("Above", rig.ShownBy("pick_test"));
        Assert.Equal("speed", rig.TextOf("name_input"));
        Assert.Equal("0.1", rig.TextOf("number_value"));

        rig.Choose("pick_test", "pick_test_is_on");

        Assert.Equal(ConditionKind.On, BlendConditionModel.KindOf(rig.Blend.WrittenChanges[0].When));
        Assert.Equal("speed", BlendConditionModel.InputOf(rig.Blend.WrittenChanges[0].When));
        Assert.Equal(["Change condition"], rig.History.Labels);
        Assert.False(rig.Has("number_value"));
    });

    [Fact]
    public Task TypingAConditionsNumberIsOneStep() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new ChangePicked(0));

        rig.Type("number_value", "0.2");
        rig.Type("number_value", "0.25");

        Assert.Equal(0.25, BlendConditionModel.NumberOf(rig.Blend.WrittenChanges[0].When));
        Assert.Equal(["Change condition"], rig.History.Labels);
    });

    [Fact]
    public Task AnInputIsChosenFromThoseTheBlendHasOrTypedBy() => Headless.Run(() =>
    {
        var rig = new Rig(Moves().CopyWith(inputs: new Dictionary<string, double> { ["speed"] = 0, ["turn"] = 0 }), new ChangePicked(0));

        rig.Choose("name_input_choices", "option_input_turn");

        Assert.Equal("turn", BlendConditionModel.InputOf(rig.Blend.WrittenChanges[0].When));
        Assert.Equal("turn", rig.TextOf("name_input"));
    });

    [Fact]
    public Task AGroupOfTestsHoldsTheOneItWasMadeFromAndTestsCanBeAddedAndRemoved() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new ChangePicked(0));

        rig.Choose("pick_test", "pick_test_all_of");

        var all = rig.Blend.WrittenChanges[0].When;
        Assert.Equal(ConditionKind.All, BlendConditionModel.KindOf(all));
        Assert.Equal("Above", rig.ShownBy("pick_test_1"));

        rig.Click("button_add_a_test");

        Assert.Equal(2, BlendConditionModel.PartsOf(rig.Blend.WrittenChanges[0].When).Count);
        Assert.Equal("Is on", rig.ShownBy("pick_test_2"));

        rig.Click("button_remove_1");

        var left = BlendConditionModel.PartsOf(rig.Blend.WrittenChanges[0].When);
        Assert.Single(left);
        Assert.Equal(ConditionKind.On, BlendConditionModel.KindOf(left[0]));
    });

    [Fact]
    public Task APartOfAGroupIsEditedWhereItStandsAndANotHasOnePartOnly() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new ChangePicked(0));

        rig.Choose("pick_test", "pick_test_all_of");
        rig.Type("number_value_1", "0.7");

        var part = BlendConditionModel.PartsOf(rig.Blend.WrittenChanges[0].When)[0];
        Assert.Equal(0.7, BlendConditionModel.NumberOf(part));

        rig.Choose("pick_test", "pick_test_not");

        Assert.False(rig.Has("button_add_a_test"));
        Assert.False(rig.Has("button_remove_1"));
    });

    [Fact]
    public Task AFadeHasALengthThatCannotGoBelowNoughtAShapeAndTwoSwitches() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new ChangePicked(0));

        rig.Type("number_length", "-2");

        Assert.Equal(0, rig.Blend.WrittenChanges[0].Fade);

        rig.Type("number_length", "0.4");
        rig.Choose("pick_shape", "pick_shape_overshoot");
        rig.Click("toggle_keep_step");

        var change = rig.Blend.WrittenChanges[0];
        Assert.Equal(0.4, change.Fade);
        Assert.Equal(Easing.Back, change.Shape);
        Assert.True(change.InStep);
        Assert.False(change.FromPose);
    });

    [Fact]
    public Task AChangeCanLeaveFromAnyStateAndGoSomewhereElse() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new ChangePicked(0));

        rig.Choose("pick_from", "pick_from_any_state");
        rig.Choose("pick_to", "pick_to_idle");

        Assert.Null(rig.Blend.WrittenChanges[0].From);
        Assert.Equal("idle", rig.Blend.WrittenChanges[0].To);
        Assert.Equal(["Change where it leaves", "Change where it goes"], rig.History.Labels);
    });

    [Fact]
    public Task TheChangesOfAStateAreListedAndTappingOneChoosesIt() => Headless.Run(() =>
    {
        var rig = new Rig(pick: new StatePicked("move"));

        Assert.Contains("idle to move", Assert.IsType<TextBlock>(rig.Find<Control>("change_text_0")).Text);

        rig.Click("change_0");

        Assert.Equal(new ChangePicked(0), rig.Bench.Pick);
        Assert.Equal("idle", rig.ShownBy("pick_from"));
    });
}
