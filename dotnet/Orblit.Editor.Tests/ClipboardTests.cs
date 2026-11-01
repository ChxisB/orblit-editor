namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

public sealed class ClipboardTests
{
    // Ids that are unique but readable, so a failure says what it means.
    private static Func<string> Counter()
    {
        var next = 0;
        return () => $"new{next++}";
    }

    private static (Workspace Workspace, History History, EditorScene Scene) Rig()
    {
        var scene = EditorScene.Starter();
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        return (workspace, new History(workspace), scene);
    }

    private static SceneClipboard Taken(EditorScene scene, params string[] ids)
    {
        var clipboard = new SceneClipboard();
        clipboard.Take(scene, ids);
        return clipboard;
    }

    private static PasteObjects Paste(string sceneId, ClipboardContents content, string what, bool withWorlds = false) =>
        new(sceneId, content.Objects, content.Roots, what, withWorlds ? content.Worlds : null);

    [Fact]
    public void CopyingTakesTheWholeSubtreeNotJustTheThingClicked()
    {
        var pasted = Taken(EditorScene.Starter(), "props").Contents(Counter()).Objects;

        // Props and its two children.
        Assert.Equal(3, pasted.Count);
        Assert.Contains("Props", pasted.Select(one => one.Name));
        Assert.Contains("Cube", pasted.Select(one => one.Name));
        Assert.Contains("Crate", pasted.Select(one => one.Name));
    }

    [Fact]
    public void CopyingWhatItHoldsIsDetachedFromTheSceneItCameFrom()
    {
        var scene = EditorScene.Starter();
        var clipboard = Taken(scene, "cube");

        // The scene is thrown away, as it is when one is unloaded.
        scene.Remove("cube");

        Assert.Equal("Cube", clipboard.Contents(Counter()).Objects.Single().Name);
    }

    [Fact]
    public void CopyingTheCopyDoesNotChangeWhenTheOriginalDoes()
    {
        var scene = EditorScene.Starter();
        var clipboard = Taken(scene, "cube");

        scene["cube"]!.Position.SetValues(9, 9, 9);

        Assert.Equal(Vector3.Zero(), clipboard.Contents(Counter()).Objects.Single().Position);
    }

    [Fact]
    public void CopyingNamesWhatIsOnItSoAMenuCanSayWhatPasteWouldDo()
    {
        Assert.Equal("Cube", Taken(EditorScene.Starter(), "cube").Description);
    }

    [Fact]
    public void CopyingAnEmptyClipboardIsEmptyRatherThanASubtreeOfNothing()
    {
        Assert.True(new SceneClipboard().IsEmpty);
        Assert.Equal("", new SceneClipboard().Description);
    }

    [Fact]
    public void PastingGivesEverythingANewId()
    {
        var pasted = Taken(EditorScene.Starter(), "props").Contents(Counter()).Objects;

        Assert.All(pasted, one => Assert.StartsWith("new", one.Id));
        Assert.Equal(3, pasted.Select(one => one.Id).Distinct().Count());
    }

    [Fact]
    public void PastingTwiceGivesTwoThingsNotOneThingPastedOverItself()
    {
        var clipboard = Taken(EditorScene.Starter(), "cube");
        var ids = Counter();

        var first = clipboard.Contents(ids).Objects.Single().Id;
        var second = clipboard.Contents(ids).Objects.Single().Id;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void PastingChildrenPointAtThePastedParentNotTheOriginal()
    {
        var content = Taken(EditorScene.Starter(), "props").Contents(Counter());
        var root = content.Roots.Single();

        // A pasted child must not point back at the copy source.
        foreach (var child in content.Objects.Where(one => one.Id != root))
        {
            Assert.Equal(root, child.ParentId);
        }
    }

    [Fact]
    public void PastingTheTopOfWhatWasCopiedTakesTheParentItIsPastedInto()
    {
        var content = Taken(EditorScene.Starter(), "cube").Contents(Counter(), "somewhere");

        Assert.Equal("somewhere", content.Objects.Single().ParentId);
    }

    [Fact]
    public void PasteIntoASceneAddsTheSubtreeWithItsShapeIntact()
    {
        var (_, history, scene) = Rig();
        var content = Taken(scene, "props").Contents(Counter());

        history.Run(Paste("a", content, "Props", withWorlds: true));

        Assert.Equal(2, scene.ChildrenOf(content.Roots.Single()).Count);
    }

    [Fact]
    public void PasteIntoASceneUndoTakesTheWholePasteChildrenIncluded()
    {
        var (_, history, scene) = Rig();
        var before = scene.Length;
        var content = Taken(scene, "props").Contents(Counter());

        history.Run(Paste("a", content, "Props"));
        history.Undo();

        Assert.Equal(before, scene.Length);
    }

    [Fact]
    public void PasteIntoASceneRedoPutsItBackWithTheSameIds()
    {
        var (_, history, scene) = Rig();
        var content = Taken(scene, "cube").Contents(Counter());

        history.Run(Paste("a", content, "Cube"));
        history.Undo();
        history.Redo();

        // Same ids, so a selection or a later step still refers to the right object after an undo
        // and redo.
        Assert.True(scene.Contains(content.Roots.Single()));
    }

    [Fact]
    public void PasteIntoASceneThatIsGoneDoesNothingRatherThanThrow()
    {
        var (workspace, history, scene) = Rig();
        var content = Taken(scene, "cube").Contents(Counter());

        workspace.Unload(workspace["a"]!);

        history.Run(Paste("a", content, "Cube"));
    }

    [Fact]
    public void PasteIntoASceneNamesTheStepForWhatWasPasted()
    {
        var (_, history, scene) = Rig();
        var clipboard = Taken(scene, "crate");

        history.Run(Paste("a", clipboard.Contents(Counter()), clipboard.Description));

        Assert.Equal("Paste Crate", history.UndoLabel);
    }

    [Fact]
    public void ACopyFromOneSceneLandsInAnother()
    {
        // The reason the clipboard holds detached copies at all.
        var to = new EditorScene([]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("b", "B", scene: to));
        var history = new History(workspace);

        var content = Taken(EditorScene.Starter(), "props").Contents(Counter());
        history.Run(Paste("b", content, "Props"));

        Assert.Equal(3, to.Length);
        Assert.Equal("Props", to.Roots.Single().Name);
        var names = to.ChildrenOf(content.Roots.Single()).Select(one => one.Name).ToList();
        Assert.Contains("Cube", names);
        Assert.Contains("Crate", names);
    }

    [Fact]
    public void SeveralAtOnceCopiesEachSubtree()
    {
        var content = Taken(EditorScene.Starter(), "sun", "ground").Contents(Counter());

        Assert.Equal(2, content.Roots.Count);
        Assert.Equal(2, content.Objects.Count);
    }

    [Fact]
    public void SeveralAtOnceLeavesOutAnythingAlreadyInsideSomethingElseCopied()
    {
        // Copying a parent and its child together would paste the child twice: once as itself and
        // once inside its parent.
        var content = Taken(EditorScene.Starter(), "props", "cube").Contents(Counter());

        // Cube is inside Props.
        Assert.Single(content.Roots);
        Assert.Equal(3, content.Objects.Count);
    }

    [Fact]
    public void SeveralAtOnceNamesHowManyRatherThanListingThem()
    {
        Assert.Equal("2 objects", Taken(EditorScene.Starter(), "sun", "ground").Description);
    }

    [Fact]
    public void SeveralAtOnceAnIdThatIsNotInTheSceneIsIgnored()
    {
        var clipboard = Taken(EditorScene.Starter(), "cube", "ghost");

        Assert.Single(clipboard.Contents(Counter()).Roots);
    }

    [Fact]
    public void ThroughTextWhatIsWrittenCanBeReadBack()
    {
        var from = Taken(EditorScene.Starter(), "props");

        var to = new SceneClipboard();
        Assert.True(to.TakeText(from.ToText()));
        Assert.Equal("Props", to.Description);
        Assert.Equal(3, to.Contents(Counter()).Objects.Count);
    }

    [Fact]
    public void ThroughTextTheShapeSurvivesNotJustTheNames()
    {
        var scene = EditorScene.Starter();
        scene["crate"]!.Position.SetValues(1.5, -2, 0.25);
        scene.Invalidate();

        var to = new SceneClipboard();
        to.TakeText(Taken(scene, "props").ToText());

        var crate = to.Contents(Counter()).Objects.First(one => one.Name == "Crate");
        Assert.Equal(1.5, crate.Position.X, 1e-9);
        Assert.Equal(-2, crate.Position.Y, 1e-9);
    }

    [Fact]
    public void ThroughTextAnythingElseOnTheClipboardIsLeftAloneNotAnError()
    {
        var loaded = Taken(EditorScene.Starter(), "cube");

        // A path, a paragraph, JSON meaning something else.
        string?[] others =
        [
            null,
            "",
            "/Users/somebody/notes.txt",
            "{\"kind\": \"something else\"}",
            "{\"kind\": \"orblit.objects\"",
        ];
        foreach (var text in others)
        {
            Assert.False(loaded.TakeText(text), $"for \"{text}\"");
        }

        // And what was already on it is untouched.
        Assert.Equal("Cube", loaded.Description);
    }

    // The system clipboard is anybody's to write, so a world is kept only when it is sixteen numbers
    // for an object that came with it.
    [Theory]
    [InlineData("[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]")]
    [InlineData("[\"x\",0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]")]
    public void ThroughTextAWorldThatCannotBeUsedIsLeftOutAndPastingStillWorks(string ghost)
    {
        var text = Taken(EditorScene.Starter(), "cube").ToText()
            .Replace("\"worlds\": {", $"\"worlds\": {{\"ghost\": {ghost},", StringComparison.Ordinal);
        var to = Taken(EditorScene.Starter(), "props");

        Assert.True(to.TakeText(text));

        Assert.Equal("Cube", to.Description);
        var contents = to.Contents(Counter());
        Assert.Single(contents.Objects);
        Assert.Single(contents.Worlds);
    }

    [Fact]
    public void ThroughTextIsReadableRatherThanAnOpaqueBlob()
    {
        var text = Taken(EditorScene.Starter(), "cube").ToText();

        Assert.Contains("\"Cube\"", text, StringComparison.Ordinal);
        Assert.Contains("orblit.objects", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WhereAPasteLandsARootRemembersWhereItWasInTheWorld()
    {
        var scene = EditorScene.Starter();
        scene["props"]!.Position.SetValues(10, 0, 0);
        scene.Invalidate();

        var content = Taken(scene, "crate").Contents(Counter());

        // The crate sits inside Props, which is ten metres out.
        var world = content.Worlds[content.Roots.Single()];
        Assert.Equal(12.2, world.GetTranslation().X, 1e-6);
    }

    [Fact]
    public void WhereAPasteLandsIntoADifferentParentChainKeepsItWhereItWas()
    {
        var from = EditorScene.Starter();
        from["props"]!.Position.SetValues(10, 0, 0);
        from.Invalidate();
        var wasAt = from.WorldOf("crate").GetTranslation();

        var to = new EditorScene([]);
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("b", "B", scene: to));
        var history = new History(workspace);

        var content = Taken(from, "crate").Contents(Counter());
        history.Run(Paste("b", content, "Crate", withWorlds: true));

        // The other scene has no Props to inherit from, so the local transform has to absorb what
        // the parent used to contribute.
        var landed = to.WorldOf(content.Roots.Single()).GetTranslation();
        Assert.True((landed - wasAt).Length < 1e-6);
    }

    [Fact]
    public void WhereAPasteLandsDuplicatingInPlaceDoesNotMoveIt()
    {
        var scene = EditorScene.Starter();
        scene["props"]!.Position.SetValues(3, 1, -2);
        scene.Invalidate();
        var wasAt = scene.WorldOf("crate").GetTranslation();

        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        var history = new History(workspace);

        var content = Taken(scene, "crate").Contents(Counter(), "props");
        history.Run(Paste("a", content, "Crate", withWorlds: true));

        var copy = scene.WorldOf(content.Roots.Single()).GetTranslation();
        Assert.True((copy - wasAt).Length < 1e-6);
    }
}
