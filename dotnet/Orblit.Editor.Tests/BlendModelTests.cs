namespace Orblit.Editor.Tests;

using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

// What the blend graph editor knows of a blend, without a control.
public sealed class BlendModelTests
{
    private static BlendDocument Moves() => new(
        "Moves",
        [
            new BlendState("idle", new BlendClip("idle")),
            new BlendState(
                "move",
                new BlendLine("speed", [new LinePoint(1, new BlendClip("walk")), new LinePoint(3, new BlendClip("run"))])),
            new BlendState("jump", new BlendClip("jump")),
            new BlendState("lonely", new BlendClip("wave")),
        ],
        new Dictionary<string, double> { ["speed"] = 0, ["jump"] = 0 },
        [
            new BlendChange("move", from: "idle", when: BlendCondition.Above("speed", 0.1), fade: 0.2),
            new BlendChange("idle", from: "move", when: BlendCondition.Below("speed", 0.1)),
            new BlendChange("jump", when: BlendCondition.On("jump")),
            new BlendChange("idle", from: "jump", when: BlendCondition.Through(1)),
        ]);

    private static List<string> Names(BlendDocument blend) => [.. blend.WrittenStates.Select(static state => state.Name)];

    private static List<double> Ats(BlendLine line) => [.. line.Points.Select(static point => point.At)];

    [Fact]
    public void TheGraphHasOneArrowForEachPairOfStatesAChangeJoins()
    {
        var edges = BlendGraphModel.Edges(Moves());

        Assert.Equal(
            ["idle>move[0]", "move>idle[1]", "jump>idle[3]"],
            edges.Select(static edge => $"{edge.From}>{edge.To}[{string.Join(", ", edge.Changes)}]"));
    }

    [Fact]
    public void TwoChangesBetweenOnePairAreOneArrow()
    {
        var blend = Moves().WithChangeAdded("idle", "move");

        Assert.Equal([0, 4], BlendGraphModel.Edges(blend)[0].Changes);
    }

    [Fact]
    public void AChangeFromAnywhereIsLeftToTheStateItEnters()
    {
        var found = BlendGraphModel.ChangesFromAnywhere(Moves());

        Assert.Equal("jump", Assert.Single(found).Key);
        Assert.Equal([2], found["jump"]);
    }

    [Fact]
    public void EachStateIsAColumnFurtherThanTheOneThatLeadsToIt()
    {
        var cells = BlendGraphModel.Layout(Moves());

        Assert.Equal(new BlendCell(0, 0), cells["idle"]);
        Assert.Equal(new BlendCell(1, 0), cells["move"]);
    }

    [Fact]
    public void AStateNothingLeadsToGoesAfterTheLastColumn()
    {
        var cells = BlendGraphModel.Layout(Moves());

        Assert.Equal(2, cells["jump"].Column);
        Assert.Equal(2, cells["lonely"].Column);
        Assert.NotEqual(cells["jump"].Row, cells["lonely"].Row);
    }

    [Fact]
    public void TheStartStandsInTheFirstColumnHoweverItIsNamed()
    {
        var cells = BlendGraphModel.Layout(Moves().WithStart("move"));

        Assert.Equal(new BlendCell(0, 0), cells["move"]);
        Assert.Equal(1, cells["idle"].Column);
    }

    [Fact]
    public void ANestedGraphIsDrawnAsTheOneStateItIsInTheFile()
    {
        var inner = new BlendDocument("Inner", [new BlendState("a", new BlendClip("a")), new BlendState("b", new BlendClip("b"))]);
        var blend = new BlendDocument(
            "Outer",
            [new BlendState("ground", new BlendGraph(inner)), new BlendState("air", new BlendClip("air"))],
            changes: [new BlendChange("air", from: "ground/b")]);

        Assert.Equal("ground", BlendGraphModel.StateOwning(blend, "ground/b"));
        Assert.Equal("ground", BlendGraphModel.Edges(blend).Single().From);
        Assert.Equal(["ground", "air"], BlendGraphModel.Layout(blend).Keys);
    }

    [Fact]
    public void ANameNoStateHasIsOwnedByNone()
    {
        Assert.Null(BlendGraphModel.StateOwning(Moves(), "nothing/else"));
        Assert.Null(BlendGraphModel.StateOwning(Moves(), "/idle"));
    }

    [Fact]
    public void AConditionSaysWhatKindOfTestItIs()
    {
        Assert.Equal(ConditionKind.Always, BlendConditionModel.KindOf(BlendCondition.Always));
        Assert.Equal(ConditionKind.Above, BlendConditionModel.KindOf(BlendCondition.Above("speed", 1)));
        Assert.Equal(ConditionKind.Below, BlendConditionModel.KindOf(BlendCondition.Below("speed", 1)));
        Assert.Equal(ConditionKind.On, BlendConditionModel.KindOf(BlendCondition.On("jump")));
        Assert.Equal(ConditionKind.Through, BlendConditionModel.KindOf(BlendCondition.Through(1)));
        Assert.Equal(ConditionKind.All, BlendConditionModel.KindOf(BlendCondition.AllOf([])));
        Assert.Equal(ConditionKind.Any, BlendConditionModel.KindOf(BlendCondition.AnyOf([])));
        Assert.Equal(ConditionKind.Not, BlendConditionModel.KindOf(BlendCondition.Not(BlendCondition.Always)));
    }

    [Fact]
    public void AKindKnowsWhatItIsMadeOfAndWhatItReads()
    {
        Assert.True(ConditionKind.All.IsComposite());
        Assert.True(ConditionKind.Not.IsComposite());
        Assert.False(ConditionKind.Above.IsComposite());
        Assert.True(ConditionKind.On.ReadsInput());
        Assert.False(ConditionKind.Through.ReadsInput());
        Assert.Equal("Laps through", ConditionKind.Through.Label());
        Assert.Equal("Not", ConditionKind.Not.Label());
    }

    [Fact]
    public void AConditionIsReadAsWords()
    {
        Assert.Equal("speed above 0.1", BlendConditionModel.Describe(BlendCondition.Above("speed", 0.1)));
        Assert.Equal("jump is on", BlendConditionModel.Describe(BlendCondition.On("jump")));
        Assert.Equal("when it is done", BlendConditionModel.Describe(BlendCondition.Through(1)));
        Assert.Equal("after 0.8 laps", BlendConditionModel.Describe(BlendCondition.Through(0.8)));
        Assert.Equal(
            "speed above 2 and ground is on",
            BlendConditionModel.Describe(BlendCondition.AllOf([BlendCondition.Above("speed", 2), BlendCondition.On("ground")])));
        Assert.Equal("always", BlendConditionModel.Describe(BlendCondition.AllOf([])));
    }

    [Fact]
    public void ANumberHasNoTrailingZeros()
    {
        Assert.Equal("0.1", BlendConditionModel.NumberText(0.1));
        Assert.Equal("2", BlendConditionModel.NumberText(2));
        Assert.Equal("0", BlendConditionModel.NumberText(0));
        Assert.Equal("0.333", BlendConditionModel.NumberText(1.0 / 3));
        Assert.Equal("100", BlendConditionModel.NumberText(100));
    }

    [Fact]
    public void AConditionKeepsItsInputAndNumberWhenItsKindChanges()
    {
        var below = BlendConditionModel.OfKind(ConditionKind.Below, BlendCondition.Above("speed", 2));

        Assert.Equal("speed", BlendConditionModel.InputOf(below));
        Assert.Equal(2, BlendConditionModel.NumberOf(below));
        Assert.Equal(ConditionKind.Below, BlendConditionModel.KindOf(below));
    }

    [Fact]
    public void AConditionNestsWhatItWasWhenItBecomesAllOfThem()
    {
        var all = BlendConditionModel.OfKind(ConditionKind.All, BlendCondition.On("jump"));
        var none = BlendConditionModel.OfKind(ConditionKind.All, BlendCondition.Always);

        Assert.Equal(["jump is on"], BlendConditionModel.PartsOf(all).Select(BlendConditionModel.Describe));
        Assert.Empty(BlendConditionModel.PartsOf(none));
    }

    [Fact]
    public void AConditionThatIsNotAnythingNestsToo()
    {
        var not = BlendConditionModel.OfKind(ConditionKind.Not, BlendCondition.On("jump"));

        Assert.Equal("not jump is on", BlendConditionModel.Describe(not));
    }

    [Fact]
    public void AThroughKeepsItsLapsOnlyFromAThrough()
    {
        Assert.Equal(0.5, BlendConditionModel.NumberOf(BlendConditionModel.OfKind(ConditionKind.Through, BlendCondition.Through(0.5))));
        Assert.Equal(1, BlendConditionModel.NumberOf(BlendConditionModel.OfKind(ConditionKind.Through, BlendCondition.Above("x", 4))));
    }

    [Fact]
    public void AConditionReadsAnotherInputAndNotANameWithNothingInIt()
    {
        var old = BlendCondition.Above("speed", 2);

        Assert.Equal("run", BlendConditionModel.InputOf(BlendConditionModel.Reading(old, "run")));
        Assert.Equal("speed", BlendConditionModel.InputOf(BlendConditionModel.Reading(old, string.Empty)));
        Assert.Equal(2, BlendConditionModel.NumberOf(BlendConditionModel.Reading(old, "run")));
    }

    [Fact]
    public void AConditionTakesANumberAndNotOneThatIsNot()
    {
        var old = BlendCondition.Above("speed", 2);

        Assert.Equal(5, BlendConditionModel.NumberOf(BlendConditionModel.Numbered(old, 5)));
        Assert.Equal(2, BlendConditionModel.NumberOf(BlendConditionModel.Numbered(old, double.NaN)));
        Assert.Equal(ConditionKind.On, BlendConditionModel.KindOf(BlendConditionModel.Numbered(BlendCondition.On("x"), 5)));
    }

    [Fact]
    public void AConditionIsRebuiltFromThePartsItIsGiven()
    {
        var old = BlendCondition.AnyOf([BlendCondition.On("a")]);

        var more = BlendConditionModel.MadeOf(old, [.. BlendConditionModel.PartsOf(old), BlendCondition.On("b")]);

        Assert.Equal("a is on or b is on", BlendConditionModel.Describe(more));
        Assert.Equal(
            "not c is on",
            BlendConditionModel.Describe(BlendConditionModel.MadeOf(BlendCondition.Not(BlendCondition.Always), [BlendCondition.On("c")])));
    }

    [Fact]
    public void WhatAStatePlaysReadsAsAPhrase()
    {
        Assert.Equal("walk", BlendSourceModel.Describe(new BlendClip("walk")));
        Assert.Equal(
            "2 points along speed",
            BlendSourceModel.Describe(new BlendLine("speed", [new LinePoint(0, new BlendClip("a")), new LinePoint(1, new BlendClip("b"))])));
        Assert.Equal(
            "1 points across x and y",
            BlendSourceModel.Describe(new BlendPlane("x", "y", [new PlanePoint(0, 0, new BlendClip("a"))])));
        Assert.Equal("a graph of 4 states", BlendSourceModel.Describe(new BlendGraph(Moves())));
    }

    [Fact]
    public void WhatAStatePlaysIsOnlyChangedWhenItIsAClipOrALineOfClips()
    {
        var mixed = new BlendLine("speed", [new LinePoint(0, new BlendLine("turn", [new LinePoint(0, new BlendClip("a"))]))]);

        Assert.True(BlendSourceModel.CanEdit(new BlendClip("a")));
        Assert.True(BlendSourceModel.CanEdit(BlendSourceModel.LineFrom("a", "speed")));
        Assert.False(BlendSourceModel.CanEdit(mixed));
        Assert.False(BlendSourceModel.CanEdit(new BlendPlane("x", "y", [new PlanePoint(0, 0, new BlendClip("a"))])));
    }

    [Fact]
    public void ALineMovesAPointAndKeepsThemInOrder()
    {
        var line = BlendSourceModel.LineFrom("walk", "speed");

        var moved = BlendSourceModel.PointChanged(line, 0, at: 5, clip: "run");

        Assert.Equal([1, 5], Ats(moved));
        Assert.Equal("walk", BlendSourceModel.FirstClipOf(moved));
        Assert.Equal("run", ((BlendClip)moved.Points[^1].Plays).Clip);
    }

    [Fact]
    public void ALineKeepsAPointWhenItIsMovedToSomewhereThatIsNot()
    {
        var line = BlendSourceModel.LineFrom("walk", "speed");

        var same = BlendSourceModel.PointChanged(line, 1, at: double.NaN, clip: string.Empty);

        Assert.Equal([0, 1], Ats(same));
    }

    [Fact]
    public void ALineGetsAPointAfterTheLastAndLosesOneButNotTheOnly()
    {
        var line = BlendSourceModel.PointAdded(BlendSourceModel.LineFrom("walk", "speed"));
        var one = new BlendLine("speed", [new LinePoint(0, new BlendClip("a"))]);

        Assert.Equal([0, 1, 2], Ats(line));
        Assert.Equal(2, BlendSourceModel.PointRemoved(line, 1).Points.Count);
        Assert.Same(one, BlendSourceModel.PointRemoved(one, 0));
    }

    [Fact]
    public void ALineRunsAlongAnotherInput()
    {
        var line = BlendSourceModel.LineAlong(BlendSourceModel.LineFrom("walk", "speed"), "turn");

        Assert.Equal("turn", line.Input);
        Assert.Equal(2, line.Points.Count);
    }

    [Fact]
    public void ASourceNamesItsFirstClipOrClipWhenItNamesNone()
    {
        Assert.Equal("walk", BlendSourceModel.FirstClipOf(new BlendClip("walk")));
        Assert.Equal("walk", BlendSourceModel.FirstClipOf(new BlendLine("speed", [new LinePoint(0, new BlendClip("walk"))])));
    }

    [Fact]
    public void ANewStateIsNamedAfterItsClipAndASecondIsNumbered()
    {
        var one = Moves().WithStateAdded("wave");
        var two = one.WithStateAdded("wave");

        Assert.Equal("wave", Names(one)[^1]);
        Assert.Equal("wave 2", Names(two)[^1]);
    }

    [Fact]
    public void ANewStateForNoClipIsNamedAsAClipNotAsNothing()
    {
        var blend = Moves().WithStateAdded(string.Empty);

        Assert.Equal("clip", ((BlendClip)blend.WrittenStates[^1].Plays).Clip);
    }

    [Fact]
    public void RenamingAStateRenamesItInEveryChangeAndTheStart()
    {
        var blend = Moves().WithStateRenamed("idle", "stand");

        Assert.Equal(["stand", "move", "jump", "lonely"], Names(blend));
        Assert.Equal("stand", blend.WrittenStart);
        Assert.Equal("stand", blend.WrittenChanges[0].From);
        Assert.Equal("stand", blend.WrittenChanges[1].To);
        Assert.Equal("stand", blend.WrittenChanges[3].To);
        Assert.Null(blend.WrittenChanges[2].From);
    }

    [Fact]
    public void ANameThatIsTakenOrEmptyIsRefused()
    {
        var blend = Moves();

        Assert.Same(blend, blend.WithStateRenamed("idle", "move"));
        Assert.Same(blend, blend.WithStateRenamed("idle", string.Empty));
        Assert.Same(blend, blend.WithStateRenamed("idle", "idle"));
        Assert.Same(blend, blend.WithStateRenamed("nothing", "x"));
    }

    [Fact]
    public void RenamingAGraphStateRenamesTheLeavesChangesNameToo()
    {
        var inner = new BlendDocument("Inner", [new BlendState("a", new BlendClip("a")), new BlendState("b", new BlendClip("b"))]);
        var blend = new BlendDocument(
            "Outer",
            [new BlendState("ground", new BlendGraph(inner)), new BlendState("air", new BlendClip("air"))],
            changes: [new BlendChange("air", from: "ground/b")]);

        var renamed = blend.WithStateRenamed("ground", "floor");

        Assert.Equal("floor/b", renamed.WrittenChanges[0].From);
    }

    [Fact]
    public void ATakenOutStateTakesEveryChangeIntoAndOutOfItWithIt()
    {
        var blend = Moves().WithStateRemoved("jump");

        Assert.Equal(["idle", "move", "lonely"], Names(blend));
        Assert.Equal(2, blend.WrittenChanges.Count);
    }

    [Fact]
    public void TheStartMovesOnWhenTheStartIsTakenOut()
    {
        var blend = Moves().WithStateRemoved("idle");

        Assert.Equal("move", blend.WrittenStart);
        Assert.Equal("move", blend.Start);
    }

    [Fact]
    public void TheLastStateIsKept()
    {
        var blend = Moves();
        foreach (var name in new[] { "idle", "move", "jump", "lonely" })
        {
            blend = blend.WithStateRemoved(name);
        }

        Assert.Equal(["lonely"], Names(blend));
    }

    [Fact]
    public void OneStateIsChangedAndTheOthersAreLeft()
    {
        var blend = Moves().WithState("idle", static state => state.CopyWith(speed: 2, whenDone: WhenDone.Hold));

        Assert.Equal(2, blend.WrittenStates[0].Speed);
        Assert.Equal(WhenDone.Hold, blend.WrittenStates[0].WhenDone);
        Assert.Equal(1, blend.WrittenStates[1].Speed);
    }

    [Fact]
    public void ASpeedAStateCannotPlayAtIsRefused()
    {
        var blend = Moves();

        Assert.Same(blend, blend.WithState("idle", static state => state.CopyWith(speed: -1)));
    }

    [Fact]
    public void AStateThatIsNotThereIsNotChanged()
    {
        var blend = Moves();

        Assert.Same(blend, blend.WithState("nothing", static state => state));
    }

    [Fact]
    public void TheBlendStartsSomewhereElse()
    {
        var blend = Moves().WithStart("jump");

        Assert.Equal("jump", blend.Start);
        Assert.Same(blend, blend.WithStart("jump"));
        Assert.Same(blend, blend.WithStart("nothing"));
    }

    [Fact]
    public void AChangeIsAddedLastInPrecedence()
    {
        var blend = Moves().WithChangeAdded("lonely", "idle");

        Assert.Equal("lonely", blend.WrittenChanges[^1].From);
        Assert.Equal(5, blend.WrittenChanges.Count);
    }

    [Fact]
    public void AChangeToAStateThatIsNotThereIsRefused()
    {
        var blend = Moves();

        Assert.Same(blend, blend.WithChangeAdded("idle", "nothing"));
    }

    [Fact]
    public void OneChangeIsChangedAndTheOthersAreLeft()
    {
        var blend = Moves().WithChange(0, static change => change.CopyWith(fade: 0.5, inStep: true));

        Assert.Equal(0.5, blend.WrittenChanges[0].Fade);
        Assert.True(blend.WrittenChanges[0].InStep);
        Assert.Equal(0, blend.WrittenChanges[1].Fade);
    }

    [Fact]
    public void AChangeIsTakenOutAndMovedUpAndDown()
    {
        var removed = Moves().WithChangeRemoved(1);
        var moved = Moves().WithChangeMoved(3, -3);

        Assert.Equal(3, removed.WrittenChanges.Count);
        Assert.Equal("jump", moved.WrittenChanges[0].From);
        Assert.Equal("idle", moved.WrittenChanges[1].From);
    }

    [Fact]
    public void AChangeStopsAtEitherEndOfThePrecedence()
    {
        var blend = Moves();

        Assert.Same(blend, blend.WithChangeMoved(0, -1));
        Assert.Same(blend, blend.WithChangeMoved(3, 1));
        Assert.Same(blend, blend.WithChangeMoved(9, 1));
    }

    // Added in C#. The clamp throws when it has no change to be its upper limit.
    [Fact]
    public void MovingAChangeOfABlendWithNoneAnswersTheSameBlend()
    {
        var blend = new BlendDocument("Bare", [new BlendState("only", new BlendClip("a"))]);

        Assert.Same(blend, blend.WithChangeMoved(0, 1));
        Assert.Same(blend, blend.WithChangeRemoved(0));
    }

    [Fact]
    public void AnInputIsSetAndTakenOut()
    {
        var set = Moves().WithInput("turn", 0.5);
        var blend = Moves();

        Assert.Equal(0.5, set.Inputs["turn"]);
        Assert.False(set.WithInputRemoved("turn").Inputs.ContainsKey("turn"));
        Assert.Same(blend, blend.WithInput(string.Empty, 1));
        Assert.Same(blend, blend.WithInput("speed", double.NaN));
        Assert.Same(blend, blend.WithInputRemoved("nothing"));
    }

    [Fact]
    public void AnInputIsAddedOnceAtNought()
    {
        var added = Moves().WithInputAdded("turn");
        var set = Moves().WithInput("speed", 2);

        Assert.Equal(0, added.Inputs["turn"]);
        Assert.Same(set, set.WithInputAdded("speed"));
        Assert.Same(set, set.WithInputAdded(string.Empty));
    }

    [Fact]
    public void TheBlendIsRenamedButNotToNothing()
    {
        var blend = Moves();

        Assert.Equal("Walks", blend.WithName("Walks").Name);
        Assert.Same(blend, blend.WithName(string.Empty));
        Assert.Same(blend, blend.WithName("Moves"));
    }

    [Fact]
    public void ABlendWritesAndReadsBackAsTheBlendItIs()
    {
        var blend = Moves()
            .WithStateRenamed("idle", "stand")
            .WithChange(0, static change => change.CopyWith(fade: 0.4))
            .WithStateAdded("wave");

        var back = BlendDocument.Decode(blend.Encode());

        Assert.Empty(back.Problems);
        Assert.Equal(blend.Encode(), back.Blend.Encode());
    }
}
