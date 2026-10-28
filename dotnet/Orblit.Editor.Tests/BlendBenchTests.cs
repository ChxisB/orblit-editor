namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;

// The graph's side of the editor: a blend opened, changed through the undo stack, and written back as
// the file a game reads.
public sealed class BlendBenchTests : IDisposable
{
    private const string MovesPath = "/project/moves.oblend";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("blend_bench");

    private static BlendDocument Moves() => new(
        "Moves",
        [new BlendState("idle", new BlendClip("idle")), new BlendState("move", new BlendClip("walk"))],
        new Dictionary<string, double> { ["speed"] = 0 },
        [new BlendChange("move", from: "idle", when: BlendCondition.Above("speed", 0.1))]);

    private static (History History, BlendBench Bench) Rig()
    {
        var history = new History(new Workspace("/project"));
        var bench = new BlendBench(history);
        bench.OpenDocument(MovesPath, Moves());
        return (history, bench);
    }

    private static List<string> Names(BlendDocument blend) => [.. blend.WrittenStates.Select(static state => state.Name)];

    // Assert.NotNull does not hand the value back for a reference type.
    private static T Some<T>(T? value)
        where T : class
    {
        Assert.NotNull(value);
        return value;
    }

    public void Dispose() => _folder.Delete(recursive: true);

    private string Written()
    {
        var path = Path.Combine(_folder.FullName, "moves.oblend");
        File.WriteAllText(path, Moves().Encode());
        return path;
    }

    [Fact]
    public void ChangesTheShownBlendAndUndoPutsTheOldOneBack()
    {
        var (history, bench) = Rig();

        bench.Edit("Add state", blend => blend.WithStateAdded("jump"));
        Assert.Equal(["idle", "move", "jump"], Names(Some(bench.Blend)));
        Assert.Equal(["Add state"], history.Labels);

        history.Undo();
        Assert.Equal(["idle", "move"], Names(Some(bench.Blend)));
        history.Redo();
        Assert.Equal(["idle", "move", "jump"], Names(Some(bench.Blend)));
    }

    [Fact]
    public void AChangeThatChangesNothingIsNotAStep()
    {
        var (history, bench) = Rig();

        bench.Edit("Rename", blend => blend.WithStateRenamed("idle", "idle"));
        bench.Edit("Remove", blend => blend.WithStateRemoved("nothing"));

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void NothingIsChangedWithNoBlendShown()
    {
        var history = new History(new Workspace("/project"));
        var bench = new BlendBench(history);
        var asked = false;

        bench.Edit("Add state", blend =>
        {
            asked = true;
            return blend.WithStateAdded("jump");
        });

        Assert.False(asked);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void ATypedRunTiesIntoOneStep()
    {
        var (history, bench) = Rig();
        var gesture = new object();

        foreach (var to in new[] { "r", "ru", "run" })
        {
            bench.Edit("Rename state", blend => blend.WithStateRenamed(blend.WrittenStates[^1].Name, to), new BlendEditOptions(Gesture: gesture));
        }

        Assert.Equal(["idle", "run"], Names(Some(bench.Blend)));
        history.Undo();
        Assert.Equal(["idle", "move"], Names(Some(bench.Blend)));
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void ARunInAnotherBlendIsNotTiedToThisOne()
    {
        var (history, bench) = Rig();
        var gesture = new object();
        bench.OpenDocument("/project/other.oblend", Moves());

        bench.Edit("Rename state", blend => blend.WithStateRenamed("move", "a"), new BlendEditOptions(Gesture: gesture));
        bench.Show(MovesPath);
        bench.Edit("Rename state", blend => blend.WithStateRenamed("move", "b"), new BlendEditOptions(Gesture: gesture));

        history.Undo();
        Assert.Equal(["idle", "move"], Names(Some(bench.Blend)));
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void AChoiceFollowsItsStateThroughARenameAndAnUndo()
    {
        var (history, bench) = Rig();
        bench.Pick = new StatePicked("move");

        bench.Edit("Rename state", blend => blend.WithStateRenamed("move", "run"), new BlendEditOptions(Pick: new StatePicked("run")));
        Assert.Equal(new StatePicked("run"), bench.Pick);

        history.Undo();
        Assert.Equal(new StatePicked("move"), bench.Pick);
    }

    [Fact]
    public void AChoiceThatIsNoLongerThereIsCleared()
    {
        var (_, bench) = Rig();
        bench.Pick = new StatePicked("move");

        bench.Edit("Remove state", blend => blend.WithStateRemoved("move"));

        Assert.Equal(BlendPick.Nothing, bench.Pick);
    }

    [Fact]
    public void AChosenChangeIsClearedWhenItsIndexIsGone()
    {
        var (history, bench) = Rig();
        bench.Pick = new ChangePicked(0);

        bench.Edit("Delete change", blend => blend.WithChangeRemoved(0));
        Assert.Equal(BlendPick.Nothing, bench.Pick);

        history.Undo();
        Assert.Equal(new ChangePicked(0), bench.Pick);
    }

    [Fact]
    public void ChoosingTheSameThingTwiceTellsNobodyTwice()
    {
        var (_, bench) = Rig();
        var told = 0;
        bench.Changed += () => told++;

        bench.Pick = new StatePicked("idle");
        bench.Pick = new StatePicked("idle");

        Assert.Equal(1, told);
    }

    [Fact]
    public void ShowingAnotherBlendClearsTheChoice()
    {
        var (_, bench) = Rig();
        bench.OpenDocument("/project/other.oblend", Moves());
        bench.Show(MovesPath);
        bench.Pick = new StatePicked("idle");

        bench.Show("/project/other.oblend");

        Assert.Equal(BlendPick.Nothing, bench.Pick);
    }

    [Fact]
    public void UndoingAStepInABlendShowsThatBlend()
    {
        var (history, bench) = Rig();
        bench.Edit("Add state", blend => blend.WithStateAdded("jump"));
        bench.OpenDocument("/project/other.oblend", Moves());
        Assert.Equal("/project/other.oblend", bench.Shown!.Path);

        history.Undo();

        Assert.Equal(MovesPath, bench.Shown!.Path);
    }

    [Fact]
    public void ClosingABlendForgetsItsSteps()
    {
        var (history, bench) = Rig();
        bench.Edit("Add state", blend => blend.WithStateAdded("jump"));

        bench.Close(MovesPath);

        Assert.False(history.CanUndo);
        Assert.Null(bench.Shown);
    }

    [Fact]
    public void ClosingTheShownBlendShowsTheLastOneOpen()
    {
        var (_, bench) = Rig();
        bench.OpenDocument("/project/other.oblend", Moves());
        bench.OpenDocument("/project/third.oblend", Moves());

        bench.Close("/project/third.oblend");

        Assert.Equal("/project/other.oblend", bench.Shown!.Path);
    }

    [Fact]
    public void WritesABlendThatChangedAndItReadsAsSaved()
    {
        var path = Written();
        var history = new History(new Workspace(_folder.FullName));
        var bench = new BlendBench(history);
        Assert.Empty(bench.OpenFile(path));
        Assert.False(bench.AnyUnsaved);

        bench.Edit("Add state", blend => blend.WithStateAdded("jump"));
        Assert.True(bench.AnyUnsaved);
        Assert.Empty(bench.SaveAll());
        Assert.False(bench.AnyUnsaved);

        Assert.Equal(["idle", "move", "jump"], Names(BlendDocument.Decode(File.ReadAllText(path)).Blend));

        history.Undo();
        Assert.True(bench.AnyUnsaved);
    }

    [Fact]
    public void SaysWhatCouldNotBeWritten()
    {
        var bench = new BlendBench(new History(new Workspace(_folder.FullName)));
        bench.OpenDocument(Path.Combine(_folder.FullName, "missing", "moves.oblend"), Moves());
        bench.Edit("Add state", blend => blend.WithStateAdded("jump"));

        var problems = bench.SaveAll();

        Assert.StartsWith("Moves could not be saved: ", Assert.Single(problems));
        Assert.True(bench.AnyUnsaved);
    }

    [Fact]
    public void SaysWhatCouldNotBeReadAndRefusesWhatIsNotABlend()
    {
        var path = Written();
        var bench = new BlendBench(new History(new Workspace(_folder.FullName)));
        File.WriteAllText(path, "{\"kind\":\"orblit.clip\"}");

        Assert.Throws<BlendFormatException>(() => bench.OpenFile(path));
        Assert.Empty(bench.Open);
    }

    [Fact]
    public void AsksNothingOfABlendThatIsAlreadyOpen()
    {
        var path = Written();
        var bench = new BlendBench(new History(new Workspace(_folder.FullName)));
        bench.OpenFile(path);
        File.Delete(path);

        Assert.Empty(bench.OpenFile(path));
        Assert.Equal(path, bench.Shown!.Path);
    }
}
