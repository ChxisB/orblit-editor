namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Doc = Orblit.Scene;

/// <summary>Loading, starting and closing scenes, and what is asked first when one has changes.</summary>
public sealed class EditorSessionScenesTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_scenes");
    private readonly List<(string Text, SayLevel Level)> _said = [];

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, "scenes", name + Doc.SceneDocument.SceneExtension);

    private void Write(string name, EditorScene scene)
    {
        Directory.CreateDirectory(Path.Combine(_project.FullName, "scenes"));
        File.WriteAllText(PathOf(name), SceneDocument.Encode(scene));
    }

    // A project with a saved main scene and a props scene beside it that is listed and not loaded.
    private EditorSession Open(EditorScene? props = null, Doc.PrefabSource? prefabs = null)
    {
        Write("main", EditorScene.Starter());
        Write("props", props ?? new EditorScene([], name: "Props"));
        var opened = ProjectWorkspace.Open(_project.FullName, prefabs);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn())
        {
            Prefabs = prefabs,
        };
        session.Said += (text, level) => _said.Add((text, level));
        return session;
    }

    private static SceneEntry Entry(EditorSession session, string title) =>
        session.Workspace.Entries.Single(one => one.Title == title);

    private static void Edit(EditorSession session) => session.Add(ObjectKind.Light);

    [Fact]
    public async Task LoadingAListedSceneTakesTheLoadedScenesPlaceAndUnloadsIt()
    {
        var session = Open();
        var replaced = 0;
        session.SceneReplaced += () => replaced++;

        await session.LoadSceneAsync(Entry(session, "props"));

        Assert.Equal("props", session.Workspace.Loaded!.Title);
        Assert.Empty(session.Workspace.Loaded.Scene!.Objects);
        Assert.False(Entry(session, "main").IsLoaded);
        Assert.Equal(1, replaced);
        Assert.False(session.IsUnsaved(session.Workspace.Loaded));
    }

    [Fact]
    public async Task LoadingTheSceneThatIsLoadedDoesNothing()
    {
        var session = Open();
        var replaced = 0;
        session.SceneReplaced += () => replaced++;

        await session.LoadSceneAsync(Entry(session, "main"));

        Assert.Equal(0, replaced);
        Assert.True(Entry(session, "main").IsLoaded);
    }

    [Fact]
    public async Task LoadingClearsWhatWasSelectedAndTheStepsOfTheSceneThatWasLeft()
    {
        var session = Open();
        session.Save();
        Edit(session);
        session.Save();
        Assert.True(session.History.CanUndo);
        Assert.NotEmpty(session.Selected);

        await session.LoadSceneAsync(Entry(session, "props"));

        Assert.Empty(session.Selected);
        Assert.Null(session.Primary);
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public async Task ASceneWithNoFileCannotBeLoadedAndTheLoadedSceneStays()
    {
        var session = Open();
        var untitled = new SceneEntry("scene9", "Untitled", neverWritten: true);
        session.Workspace.Add(untitled);

        await session.LoadSceneAsync(untitled);

        Assert.True(Entry(session, "main").IsLoaded);
        Assert.Equal(("Untitled has never been saved, so there is nothing to load.", SayLevel.Info), _said[^1]);
    }

    [Fact]
    public async Task ASceneFileThatIsGoneLoadsAsTheStarterAtThatPathAndSaysSo()
    {
        var session = Open();
        var missing = PathOf("props");
        File.Delete(missing);

        await session.LoadSceneAsync(Entry(session, "props"));

        var loaded = session.Workspace.Loaded!;
        Assert.Equal("props", loaded.Title);
        Assert.Equal(EditorScene.Starter().Objects.Count, loaded.Scene!.Objects.Count);
        Assert.Equal(($"There is no scene at {missing}.", SayLevel.Error), _said[^1]);
    }

    [Fact]
    public async Task AFileThatCannotBeReadIsSaidAndTheLoadedSceneStays()
    {
        var session = Open();
        File.WriteAllText(PathOf("props"), "not a scene");

        await session.LoadSceneAsync(Entry(session, "props"));

        Assert.True(Entry(session, "main").IsLoaded);
        Assert.False(Entry(session, "props").IsLoaded);
        Assert.Equal(SayLevel.Error, _said[^1].Level);
    }

    [Fact]
    public async Task EveryPrefabInUseIsNamedBeforeAnotherSceneIsRead()
    {
        var instance = new SceneObject(id: "p", name: "P", kind: ObjectKind.Group, prefab: new Doc.PrefabComponent(asset: "props/crate.oprefab"));
        var kept = new List<string>();
        Write("main", new EditorScene([instance], name: "Main"));
        Write("props", new EditorScene([], name: "Props"));
        var opened = ProjectWorkspace.Open(_project.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn())
        {
            KeepPrefabs = assets => kept.AddRange(assets),
        };

        await session.LoadSceneAsync(Entry(session, "props"));

        Assert.Equal(["props/crate.oprefab"], kept);
    }

    [Fact]
    public async Task WithNobodyToAskALoadedSceneWithChangesIsKept()
    {
        var session = Open();
        Edit(session);

        await session.LoadSceneAsync(Entry(session, "props"));

        Assert.True(Entry(session, "main").IsLoaded);
    }

    [Fact]
    public async Task StopKeepsYouWhereYouAre()
    {
        var session = Open();
        var asked = new List<string>();
        session.ConfirmLeaving = entry =>
        {
            asked.Add(entry.Title);
            return Task.FromResult(Leaving.Stop);
        };
        Edit(session);

        await session.LoadSceneAsync(Entry(session, "props"));

        Assert.Equal(["main"], asked);
        Assert.True(Entry(session, "main").IsLoaded);
        Assert.True(session.IsUnsaved(Entry(session, "main")));
    }

    [Fact]
    public async Task SaveWritesTheSceneThenMovesOn()
    {
        var session = Open();
        session.ConfirmLeaving = _ => Task.FromResult(Leaving.Save);
        Edit(session);
        var before = File.ReadAllText(PathOf("main"));

        await session.LoadSceneAsync(Entry(session, "props"));

        Assert.True(Entry(session, "props").IsLoaded);
        Assert.NotEqual(before, File.ReadAllText(PathOf("main")));
    }

    [Fact]
    public async Task DiscardMovesOnAndWritesNothing()
    {
        var session = Open();
        session.ConfirmLeaving = _ => Task.FromResult(Leaving.Discard);
        Edit(session);
        var before = File.ReadAllText(PathOf("main"));

        await session.LoadSceneAsync(Entry(session, "props"));

        Assert.True(Entry(session, "props").IsLoaded);
        Assert.Equal(before, File.ReadAllText(PathOf("main")));
    }

    [Fact]
    public async Task SavingASceneWithNoFileAsksForANameAndStaysUntilItHasOne()
    {
        var session = Open();
        await session.NewSceneAsync();
        var named = 0;
        session.NameWanted += () => named++;
        session.ConfirmLeaving = _ => Task.FromResult(Leaving.Save);

        await session.LoadSceneAsync(Entry(session, "props"));

        Assert.Equal(1, named);
        Assert.Equal("Untitled", session.Workspace.Loaded!.Title);
    }

    [Fact]
    public async Task ANewSceneIsListedAndLoadedAndTheOldOneIsNot()
    {
        var session = Open();
        var replaced = 0;
        session.SceneReplaced += () => replaced++;

        await session.NewSceneAsync();

        var loaded = session.Workspace.Loaded!;
        Assert.Equal("Untitled", loaded.Title);
        Assert.Null(loaded.Path);
        Assert.True(loaded.NeverWritten);
        Assert.Equal("Untitled", loaded.Scene!.Name);
        Assert.False(Entry(session, "main").IsLoaded);
        Assert.Equal(1, replaced);
    }

    [Fact]
    public async Task EachNewSceneGetsAFreeNameAndItsOwnId()
    {
        var session = Open();
        session.ConfirmLeaving = _ => Task.FromResult(Leaving.Discard);

        await session.NewSceneAsync();
        await session.NewSceneAsync();

        var made = session.Workspace.Entries.Where(one => one.Path is null).ToList();
        Assert.Equal(["Untitled", "Untitled 2"], made.Select(one => one.Title).Order());
        Assert.Equal(session.Workspace.Entries.Count, session.Workspace.Entries.Select(one => one.Id).Distinct().Count());
    }

    [Fact]
    public async Task ANewSceneAsksFirstWhenTheLoadedSceneHasChanges()
    {
        var session = Open();
        Edit(session);

        await session.NewSceneAsync();

        Assert.Equal("main", session.Workspace.Loaded!.Title);
        Assert.Equal(2, session.Workspace.Entries.Count);
    }

    [Fact]
    public async Task ClosingAnUnloadedSceneRemovesItFromTheList()
    {
        var session = Open();
        session.SelectScene(Entry(session, "props").Id);

        await session.CloseSceneAsync(Entry(session, "props"));

        Assert.DoesNotContain(session.Workspace.Entries, one => one.Title == "props");
        Assert.Null(session.SelectedScene);
        Assert.True(File.Exists(PathOf("props")));
    }

    [Fact]
    public async Task ClosingASceneWithChangesAsksFirst()
    {
        var session = Open();
        Edit(session);

        await session.CloseSceneAsync(Entry(session, "main"));

        Assert.True(Entry(session, "main").IsLoaded);
        session.ConfirmLeaving = _ => Task.FromResult(Leaving.Discard);

        await session.CloseSceneAsync(Entry(session, "main"));

        Assert.Null(session.Workspace.Loaded);
        Assert.False(session.History.CanUndo);
        Assert.Empty(session.Selected);
    }

    [Fact]
    public async Task ClosingAnotherSceneKeepsTheSelectedSceneRow()
    {
        var session = Open();
        session.SelectScene(Entry(session, "main").Id);

        await session.CloseSceneAsync(Entry(session, "props"));

        Assert.Equal("scene0", session.SelectedScene);
    }

    private static string[] Names(EditorSession session) => [.. session.Workspace.Loaded!.Scene!.Objects.Select(one => one.Name)];

    [Fact]
    public async Task AnObjectCanBeCopiedFromOneSceneIntoAnother()
    {
        var session = Open();
        session.SelectObject("props");
        await session.CopyAsync();

        await session.LoadSceneAsync(Entry(session, "props"));
        Assert.Empty(Names(session));
        await session.PasteAsync();

        Assert.Equal(["Crate", "Cube", "Props"], Names(session).Order());
    }

    [Fact]
    public async Task TheClipboardSurvivesTheSceneItCameFromBeingUnloaded()
    {
        var session = Open();
        session.SelectObject("crate");
        await session.CopyAsync();

        await session.LoadSceneAsync(Entry(session, "props"));
        await session.PasteAsync();
        session.Save();

        Assert.Contains("\"Crate\"", File.ReadAllText(PathOf("props")));
    }

    [Fact]
    public async Task SeveralCanBeCopiedIntoAnotherSceneAtOnce()
    {
        var session = Open();
        session.SelectObject("sun");
        session.SelectObject("ground", additive: true);
        await session.CopyAsync();

        await session.LoadSceneAsync(Entry(session, "props"));
        await session.PasteAsync();

        Assert.Equal(["Ground", "Sun"], Names(session).Order());
    }

    [Fact]
    public async Task APastedObjectLandsWhereItWasInTheWorld()
    {
        var session = Open();
        session.SelectObject("crate");
        await session.CopyAsync();

        await session.LoadSceneAsync(Entry(session, "props"));
        await session.PasteAsync();
        session.Save();

        // The other scene has no Props to inherit from, so the local transform has to carry what
        // the parent used to contribute.
        var crate = SceneDocument.Decode(File.ReadAllText(PathOf("props"))).Scene.Objects.First(one => one.Name == "Crate");
        Assert.Equal(2.2, crate.Position.X, 1e-6);
    }

    [Fact]
    public void SelectingASceneLeavesNoObjectSelectedAndSelectingAnObjectLeavesNoScene()
    {
        var session = Open();
        session.SelectObject("sun");

        session.SelectScene("scene0");

        Assert.Equal("scene0", session.SelectedScene);
        Assert.Empty(session.Selected);
        Assert.Null(session.Primary);

        session.SelectObject("sun");

        Assert.Null(session.SelectedScene);
        Assert.Equal(["sun"], session.Selected);
    }
}
