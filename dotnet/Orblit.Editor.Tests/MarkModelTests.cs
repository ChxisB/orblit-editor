namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

// What the mark strip does to a clip: every change is an edit that undoes, a blank or unchanged name is
// turned away before it makes a step, and a mark keeps what it carries when it is renamed or moved.
public sealed class MarkModelTests
{
    private const string Wave = "/project/wave.oclip";

    private sealed record Rig(History History, ClipBench Bench, MarkModel Marks);

    private static Rig MakeRig(params Mark[] marks)
    {
        var scene = new EditorScene([new SceneObject("box", "Box", ObjectKind.Group)]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);
        var bench = new ClipBench(history, () => scene);
        bench.OpenDocument(Wave, new ClipDocument("wave", 2, marks: marks));
        return new Rig(history, bench, new MarkModel(bench));
    }

    private static IReadOnlyList<Mark> MarksOf(Rig rig)
    {
        Assert.NotNull(rig.Bench.Clip);
        return rig.Bench.Clip.Marks;
    }

    [Fact]
    public void AddingPutsAMarkAtTheFrameTheTimelineIsOn()
    {
        var rig = MakeRig();
        rig.Bench.At = 0.5;

        rig.Marks.Add("Door");

        var mark = Assert.Single(MarksOf(rig));
        Assert.Equal("Door", mark.Name);
        Assert.Equal(0.5, mark.At);
        Assert.Equal(["Add mark Door"], rig.History.Labels);
    }

    [Fact]
    public void AddingSnapsToAFrameAndKeepsTheMarksInTimeOrder()
    {
        var rig = MakeRig(new Mark(1, "Late"));
        rig.Bench.At = 0.51;

        rig.Marks.Add("Early");

        Assert.Equal(["Early", "Late"], MarksOf(rig).Select(mark => mark.Name));
        Assert.Equal(0.5, MarksOf(rig)[0].At, 3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameAddsNothing(string name)
    {
        var rig = MakeRig();

        rig.Marks.Add(name);

        Assert.Empty(MarksOf(rig));
        Assert.Empty(rig.History.Labels);
    }

    [Fact]
    public void AddingUndoesToTheClipAsItWas()
    {
        var rig = MakeRig();

        rig.Marks.Add("Door");
        rig.History.Undo();

        Assert.Empty(MarksOf(rig));
    }

    [Fact]
    public void RenamingKeepsWhereTheMarkIsAndWhatItCarries()
    {
        var payload = new object();
        var rig = MakeRig(new Mark(0.5, "Door", payload));

        rig.Marks.Rename(0, "Gate");

        var mark = Assert.Single(MarksOf(rig));
        Assert.Equal("Gate", mark.Name);
        Assert.Equal(0.5, mark.At);
        Assert.Same(payload, mark.Payload);
        Assert.Equal(["Rename mark Door"], rig.History.Labels);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Door")]
    public void ABlankOrUnchangedNameRenamesNothing(string name)
    {
        var rig = MakeRig(new Mark(0.5, "Door"));

        rig.Marks.Rename(0, name);

        Assert.Equal("Door", MarksOf(rig)[0].Name);
        Assert.Empty(rig.History.Labels);
    }

    [Fact]
    public void MovingTakesAMarkToThePlayheadAndOverToItsPlaceInTime()
    {
        var payload = new object();
        var rig = MakeRig(new Mark(0.5, "First", payload), new Mark(1, "Second"));
        rig.Bench.At = 1.5;

        rig.Marks.MoveToPlayhead(0);

        Assert.Equal(["Second", "First"], MarksOf(rig).Select(mark => mark.Name));
        Assert.Equal(1.5, MarksOf(rig)[1].At);
        Assert.Same(payload, MarksOf(rig)[1].Payload);
        Assert.Equal(["Move mark First"], rig.History.Labels);
    }

    [Fact]
    public void DeletingTakesOnlyThatMarkAway()
    {
        var rig = MakeRig(new Mark(0.5, "First"), new Mark(1, "Second"));

        rig.Marks.Delete(0);

        Assert.Equal(["Second"], MarksOf(rig).Select(mark => mark.Name));
        Assert.Equal(["Delete mark First"], rig.History.Labels);

        rig.History.Undo();
        Assert.Equal(["First", "Second"], MarksOf(rig).Select(mark => mark.Name));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void AMarkThatIsNotThereIsLeftAlone(int index)
    {
        var rig = MakeRig(new Mark(0.5, "Door"));

        rig.Marks.Rename(index, "Gate");
        rig.Marks.MoveToPlayhead(index);
        rig.Marks.Delete(index);

        Assert.Equal("Door", Assert.Single(MarksOf(rig)).Name);
        Assert.Empty(rig.History.Labels);
    }

    [Fact]
    public void WithNothingOpenEveryChangeIsIgnored()
    {
        var history = new History(new Workspace("/project"));
        var marks = new MarkModel(new ClipBench(history, () => null));

        marks.Add("Door");
        marks.Rename(0, "Gate");
        marks.Delete(0);

        Assert.Empty(history.Labels);
    }
}
