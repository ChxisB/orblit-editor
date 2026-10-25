namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;

public sealed class EditorSessionObjectsTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_objects");
    private readonly InMemoryTextClipboard _text = new();
    private readonly List<(string Text, SayLevel Level)> _said = [];

    public void Dispose() => _project.Delete(recursive: true);

    private EditorSession Open(bool loaded = true, ITextClipboard? clipboard = null)
    {
        var workspace = new Workspace(_project.FullName);
        var path = Path.Combine(_project.FullName, "scenes", "main.oscene");
        workspace.Add(loaded
            ? new SceneEntry("a", "A", path, EditorScene.Starter(), neverWritten: true)
            : new SceneEntry("a", "A", path));
        var session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn())
        {
            TextClipboard = clipboard ?? _text,
            Clock = new FixedClock(new DateTime(2026, 10, 25, 9, 0, 0)),
        };
        session.Said += (text, level) => _said.Add((text, level));
        return session;
    }

    private static EditorScene SceneOf(EditorSession session) => session.Workspace["a"]!.Scene!;

    private static string[] Names(EditorSession session) => [.. SceneOf(session).Objects.Select(one => one.Name)];

    [Fact]
    public void SelectingOneReplacesWhatWasSelected()
    {
        var session = Open();
        session.SelectObject("sun");

        session.SelectObject("ground");

        Assert.Equal(["ground"], session.Selected);
        Assert.Equal("ground", session.Primary);
    }

    [Fact]
    public void CommandClickAddsToTheSelection()
    {
        var session = Open();
        session.SelectObject("sun");

        session.SelectObject("ground", additive: true);

        Assert.Equal(["sun", "ground"], session.Selected);
        Assert.Equal("ground", session.Primary);
    }

    [Fact]
    public void ShiftClickTakesEverythingBetweenInTheOrderTheTreeDraws()
    {
        var session = Open();
        session.SelectObject("sun");

        session.SelectObject("cube", range: true);

        // Sun, Ground, Props, Cube. Crate is below Cube so it is not between.
        Assert.Equal(["sun", "ground", "props", "cube"], session.Selected);
        Assert.Equal("cube", session.Primary);
    }

    [Fact]
    public void ShiftClickWorksUpTheTreeAsWellAsDownIt()
    {
        var session = Open();
        session.SelectObject("camera");

        session.SelectObject("props", range: true);

        Assert.Equal(4, session.Selected.Count);
        Assert.Contains("crate", session.Selected);
    }

    [Fact]
    public void CommandClickAgainTakesOneBackOut()
    {
        var session = Open();
        session.SelectObject("sun");
        session.SelectObject("ground", additive: true);

        session.SelectObject("ground", additive: true);

        Assert.Equal(["sun"], session.Selected);
        Assert.Equal("sun", session.Primary);
    }

    [Fact]
    public void SelectingNothingIsLeftAloneWhenNoSceneIsLoaded()
    {
        var session = Open(loaded: false);

        session.SelectObject("sun");

        Assert.Empty(session.Selected);
        Assert.Null(session.Primary);
    }

    [Fact]
    public void AddingNamesThingsAfterTheirKindAndNumbersTheRest()
    {
        var session = Open();

        session.Add(ObjectKind.Light);
        session.Add(ObjectKind.Light);

        Assert.Contains("Light", Names(session));
        Assert.Contains("Light 2", Names(session));
    }

    [Fact]
    public void AnAddedObjectIsSelectedAndUndoesInOneStep()
    {
        var session = Open();

        session.Add(ObjectKind.Camera);

        Assert.Equal("Camera 2", SceneOf(session).Objects.Single(one => session.Selected.Contains(one.Id)).Name);
        Assert.Equal("Add Camera 2", session.History.UndoLabel);

        session.Undo();
        Assert.DoesNotContain("Camera 2", Names(session));
    }

    [Fact]
    public void AddingWithAGroupSelectedPutsTheNewObjectInsideIt()
    {
        var session = Open();
        session.SelectObject("props");

        session.Add(ObjectKind.Mesh);

        Assert.Equal("props", SceneOf(session).Objects.Single(one => session.Selected.Contains(one.Id)).ParentId);
    }

    [Fact]
    public void AddingWithAnObjectSelectedPutsTheNewOneBesideIt()
    {
        var session = Open();
        session.SelectObject("crate");

        session.Add(ObjectKind.Mesh);

        Assert.Equal("props", SceneOf(session).Objects.Single(one => session.Selected.Contains(one.Id)).ParentId);
    }

    [Fact]
    public void AddingWithNoSceneLoadedSaysSoAsAWarning()
    {
        var session = Open(loaded: false);

        session.Add(ObjectKind.Mesh);

        Assert.Equal(("There is no scene loaded to add to.", SayLevel.Warning), Assert.Single(_said));
    }

    [Fact]
    public void AShapeIsAddedParametricAndNamedAfterItsKind()
    {
        var session = Open();

        session.AddShape(ShapeKind.Sphere);

        var added = SceneOf(session).Objects.Single(one => one.Name == "Sphere");
        Assert.Equal(ObjectKind.Shape, added.Kind);
        Assert.Equal(ShapeKind.Sphere, added.Shape!.Kind);
        Assert.Equal([added.Id], session.Selected);
    }

    [Fact]
    public void TwoObjectsMadeAtTheSameInstantStillGetDifferentIds()
    {
        var session = Open();

        session.Add(ObjectKind.Mesh);
        session.Add(ObjectKind.Mesh);

        var ids = SceneOf(session).Objects.Select(one => one.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void IdsCarryTheTimeTheyWereMadeAt()
    {
        var session = Open();

        session.Add(ObjectKind.Mesh);

        var id = session.Primary!;
        Assert.StartsWith($"o{new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc).Ticks / 10}_", id);
    }

    [Fact]
    public void DeletingOneKeepsTheRestOfTheSelection()
    {
        var session = Open();
        session.SelectObject("sun");
        session.SelectObject("ground", additive: true);

        session.Delete("ground");

        Assert.Equal(["sun"], session.Selected);
        Assert.Equal("sun", session.Primary);
        Assert.Equal("Delete Ground", session.History.UndoLabel);
    }

    [Fact]
    public void DeletingSeveralIsOneStep()
    {
        var session = Open();
        session.SelectObject("sun");
        session.SelectObject("ground", additive: true);

        session.DeleteSelection();

        Assert.DoesNotContain("Sun", Names(session));
        Assert.DoesNotContain("Ground", Names(session));
        Assert.Equal("Delete 2 objects", session.History.UndoLabel);
        Assert.Empty(session.Selected);

        session.Undo();
        Assert.Contains("Sun", Names(session));
        Assert.Contains("Ground", Names(session));
    }

    [Fact]
    public void MovingDownTheListLandsWhereTheTreeShowedItEvenThoughItWasTakenOutFirst()
    {
        var session = Open();

        // The index is the one read off the tree as drawn: after Weather, which is past the end.
        session.Move("sun", new Drop("a", null, 5));

        Assert.Equal("sun", SceneOf(session).Roots[^1].Id);
        Assert.Equal("Reorder Sun", session.History.UndoLabel);
    }

    [Fact]
    public void MovingToWhereItAlreadyIsDoesNothing()
    {
        var session = Open();

        // Dropped straight after itself, which is where it already sits.
        session.Move("ground", new Drop("a", null, 2));

        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void MovingIntoAGroupReparentsIt()
    {
        var session = Open();

        session.Move("sun", new Drop("a", "props", 0));

        Assert.Equal("props", SceneOf(session)["sun"]!.ParentId);
        Assert.Equal("Move Sun", session.History.UndoLabel);
    }

    [Fact]
    public void MovingIntoTheSharedSetTakesItOutOfTheScene()
    {
        var session = Open();

        session.Move("sun", new Drop(Workspace.SharedSceneId, null, 0));

        Assert.False(SceneOf(session).Contains("sun"));
        Assert.True(session.Workspace.Shared.Contains("sun"));
    }

    [Fact]
    public async Task CutRemovesItFromTheSceneItWasIn()
    {
        var session = Open();
        session.SelectObject("crate");

        await session.CutAsync();

        Assert.DoesNotContain("Crate", Names(session));
        Assert.Equal("Delete Crate", session.History.UndoLabel);
    }

    [Fact]
    public void DuplicateLeavesTheOriginalAlone()
    {
        var session = Open();
        session.SelectObject("crate");

        session.Duplicate();

        // Two now, sharing a name and nothing else.
        Assert.Equal(2, Names(session).Count(name => name == "Crate"));
        Assert.Equal("Paste Crate", session.History.UndoLabel);
        Assert.Contains("crate", SceneOf(session).Objects.Select(one => one.Id));
    }

    [Fact]
    public void DuplicateSelectsTheCopyAndNotTheOriginal()
    {
        var session = Open();
        session.SelectObject("crate");

        session.Duplicate();

        Assert.NotEqual("crate", session.Primary);
        Assert.Equal("Crate", SceneOf(session)[session.Primary!]!.Name);
    }

    [Fact]
    public async Task DuplicateDoesNotThrowAwayWhatWasCopiedEarlier()
    {
        var session = Open();
        session.SelectObject("sun");
        await session.CopyAsync();
        session.SelectObject("crate");

        session.Duplicate();

        Assert.Equal("Sun", session.ClipboardLabel);
    }

    [Fact]
    public void PastingIsUndoableInOneStep()
    {
        var session = Open();
        session.SelectObject("props");

        session.Duplicate();
        Assert.Equal(2, Names(session).Count(name => name == "Cube"));

        session.Undo();

        // The whole subtree went, not just its root.
        Assert.Equal(1, Names(session).Count(name => name == "Cube"));
        Assert.Equal(1, Names(session).Count(name => name == "Props"));
    }

    [Fact]
    public async Task ACopyLeavesReadableTextOnTheSystemClipboard()
    {
        var session = Open();
        session.SelectObject("crate");

        await session.CopyAsync();

        // What lands in a text editor is the scene's own encoding, not a blob.
        Assert.NotNull(_text.Text);
        Assert.Contains("\"Crate\"", _text.Text);
        Assert.Contains("orblit.objects", _text.Text);
        Assert.Equal(("Copied Crate.", SayLevel.Info), Assert.Single(_said));
    }

    [Fact]
    public async Task ACopyFromElsewhereCanBePastedIn()
    {
        var session = Open();

        // As though another window had put it there.
        var elsewhere = new SceneClipboard();
        elsewhere.Take(new EditorScene([new SceneObject(id: "x", name: "From Elsewhere", kind: ObjectKind.Mesh)]), ["x"]);
        _text.Text = elsewhere.ToText();

        await session.PasteAsync();

        Assert.Contains("From Elsewhere", Names(session));
    }

    [Fact]
    public async Task TextThatIsNotOursIsNotPasted()
    {
        var session = Open();
        _text.Text = "just some notes I had copied";

        await session.PasteAsync();

        Assert.Equal(("There is nothing on the clipboard to paste.", SayLevel.Warning), Assert.Single(_said));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public async Task PastingLandsBesideTheSelectionAndNotInsideIt()
    {
        var session = Open();
        session.SelectObject("crate");
        await session.CopyAsync();

        await session.PasteAsync();

        Assert.Equal("props", SceneOf(session)[session.Primary!]!.ParentId);
    }

    [Fact]
    public async Task ClipboardLabelNamesWhatIsHeld()
    {
        var session = Open();
        session.SelectObject("sun");
        session.SelectObject("ground", additive: true);

        await session.CopyAsync();

        Assert.Equal("2 objects", session.ClipboardLabel);
    }

    [Fact]
    public void WorkingIsTheSceneHoldingTheSelectionAndNotAlwaysTheLoadedOne()
    {
        var session = Open();
        session.Workspace.Shared.Add(new SceneObject(id: "manager", name: "Manager", kind: ObjectKind.Group));

        session.SelectObject("sun");
        Assert.Equal("a", session.Working!.Id);

        session.Select(["manager"]);
        Assert.Equal(Workspace.SharedSceneId, session.Working!.Id);
    }

    [Fact]
    public void TheMenuNamesWhatCopyAndPasteWouldDo()
    {
        var session = Open();

        // Nothing copied yet, so it is just Paste.
        Assert.Equal("Paste", session.PasteLabel);
        Assert.Equal("Copy", session.CopyLabel);

        session.SelectObject("sun");
        session.SelectObject("ground", additive: true);
        Assert.Equal("Copy 2 objects", session.CopyLabel);
        Assert.Equal("Cut 2 objects", session.CutLabel);

        session.SelectObject("crate");
        session.Copy();
        Assert.Equal("Paste Crate", session.PasteLabel);
    }

    [Fact]
    public void CommandClickAgainGoesBackToTheSingularLabel()
    {
        var session = Open();
        session.SelectObject("sun");
        session.SelectObject("ground", additive: true);

        session.SelectObject("ground", additive: true);

        Assert.Equal("Copy", session.CopyLabel);
    }

    [Fact]
    public void ACopyThatCannotReachTheSystemClipboardIsSaidAndNotThrown()
    {
        var session = Open(clipboard: new BrokenClipboard());
        session.SelectObject("crate");

        session.Copy();

        Assert.Equal(("The clipboard could not be used: no clipboard", SayLevel.Error), _said[^1]);
    }

    private sealed class BrokenClipboard : ITextClipboard
    {
        public Task<string?> GetTextAsync() => throw new InvalidOperationException("no clipboard");

        public Task SetTextAsync(string text) => throw new InvalidOperationException("no clipboard");
    }
}
