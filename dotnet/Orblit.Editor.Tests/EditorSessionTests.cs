namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

public sealed class EditorSessionTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_session");

    public void Dispose() => _project.Delete(recursive: true);

    private string Scenes(string name) => Path.Combine(_project.FullName, "scenes", name);

    private string Orblit(string name) => Path.Combine(_project.FullName, ".orblit", name);

    private EditorSession Open(Registry<EditorMode>? modes = null)
    {
        var workspace = new Workspace(_project.FullName);
        workspace.Add(new SceneEntry("a", "A", Scenes("main.oscene"), EditorScene.Starter(), neverWritten: true));
        return new EditorSession(workspace, new History(workspace), modes ?? EditorModes.Standard(), PanelTypes.BuiltIn());
    }

    private static SetTransform Move(EditorScene scene, string id, double x) =>
        new("a", id, TransformField.Position, scene[id]!.Name, scene[id]!.Position, new Vector3(x, 0, 0));

    private static string[] PanelIds(DockLayout layout) => [.. layout.Panels.Select(panel => panel.Id)];

    [Fact]
    public void StartsInTheFirstModeWithItsOwnArrangement()
    {
        var session = Open();

        Assert.Equal("scene", session.Mode.Name);
        Assert.Equal(PanelIds(DockLayout.Standard()), PanelIds(session.Layout));
    }

    [Fact]
    public void TheRailFollowsTheOrderALevelIsMadeIn() =>
        Assert.Equal(new[] { "scene", "modelling", "terrain", "animation", "cinematics" }, EditorModes.Standard().All.Select(mode => mode.Name));

    [Fact]
    public void EachOneShowsThePanelsForItsJob()
    {
        var session = Open();

        session.EnterMode("modelling");
        Assert.Contains("modelling", PanelIds(session.Layout));
        Assert.DoesNotContain("game", PanelIds(session.Layout));

        session.EnterMode("animation");
        Assert.Contains("timeline", PanelIds(session.Layout));
        Assert.DoesNotContain("modelling", PanelIds(session.Layout));
    }

    [Fact]
    public void EnteringAModeThatIsNotThereDoesNothing()
    {
        var session = Open();

        session.EnterMode("nowhere");

        Assert.Equal("scene", session.Mode.Name);
    }

    [Fact]
    public void EachOneRemembersItsOwnArrangement()
    {
        var session = Open();
        session.EnterMode("animation");
        session.Relayout(session.Layout.Close("inspector"));

        session.EnterMode("scene");
        Assert.Contains("inspector", PanelIds(session.Layout));
        session.EnterMode("animation");

        Assert.DoesNotContain("inspector", PanelIds(session.Layout));
        Assert.True(File.Exists(Orblit("layout.animation.json")));
    }

    [Fact]
    public void ASceneLayoutKeepsTheFileNameItHadBeforeThereWereModes()
    {
        var session = Open();

        session.Relayout(session.Layout.Close("console"));

        Assert.True(File.Exists(Orblit("layout.json")));
        Assert.False(File.Exists(Orblit("layout.scene.json")));
    }

    [Fact]
    public void ASavedArrangementComesBackAfterReopening()
    {
        Open().Relayout(DockLayout.Standard().Close("console"));

        var again = Open();

        Assert.DoesNotContain("console", PanelIds(again.Layout));
    }

    [Fact]
    public void AnArrangementSavedBeforeTheModeChangedIsSetAside()
    {
        Directory.CreateDirectory(Orblit("x"));
        File.WriteAllText(Orblit("layout.json"), DockLayout.Standard().CopyWith(revision: 1).Close("console").ToText());

        Assert.Equal(PanelIds(DockLayout.Standard()), PanelIds(Open().Layout));
    }

    [Fact]
    public void ADamagedLayoutFileOpensTheModesOwn()
    {
        Directory.CreateDirectory(Orblit("x"));
        File.WriteAllText(Orblit("layout.json"), "{not json");

        Assert.Equal(PanelIds(DockLayout.Standard()), PanelIds(Open().Layout));
    }

    [Fact]
    public void ResetPanelsPutsBackTheModeInUseAndKeepsTheLock()
    {
        var session = Open();
        session.Relayout(session.Layout.Close("console").CopyWith(locked: true));

        session.ResetPanels();

        Assert.Equal(PanelIds(DockLayout.Standard()), PanelIds(session.Layout));
        Assert.True(session.Layout.Locked);
    }

    [Fact]
    public void FocusShowsOnlyTheViewAndKeepsTheArrangement()
    {
        var session = Open();
        var before = session.Layout;

        session.ToggleFocus();

        Assert.True(session.IsFocused);
        Assert.Equal(new[] { "scene" }, PanelIds(session.VisibleLayout));
        Assert.True(session.VisibleLayout.Locked);
        Assert.Same(before, session.Layout);

        session.ToggleFocus();
        Assert.Same(before, session.VisibleLayout);
    }

    [Fact]
    public void FocusKeepsEachWorkspaceArrangementIntact()
    {
        var session = Open();
        session.ToggleFocus();
        session.EnterMode("animation");

        Assert.False(session.IsFocused);
        Assert.Contains("timeline", PanelIds(session.VisibleLayout));

        session.EnterMode("scene");
        Assert.True(session.IsFocused);
    }

    [Fact]
    public void ChangingTheArrangementEndsFocus()
    {
        var session = Open();
        session.ToggleFocus();

        session.Relayout(session.Layout.Close("console"));

        Assert.False(session.IsFocused);
    }

    [Fact]
    public void PerformanceNumbersAreBehindStats()
    {
        var session = Open();
        Assert.False(session.ShowStats);

        session.ToggleStats();

        Assert.True(session.ShowStats);
    }

    [Fact]
    public void ANamedArrangementIsSavedLoadedAndDeleted()
    {
        var session = Open();
        session.Relayout(session.Layout.Close("console"));

        session.SaveLayout("  My layout ");
        session.ResetPanels();
        Assert.Equal(new[] { "My layout" }, session.SavedLayouts);

        session.ToggleFocus();
        session.LoadLayout("My layout");
        Assert.DoesNotContain("console", PanelIds(session.Layout));
        Assert.False(session.IsFocused);

        session.DeleteLayout("My layout");
        Assert.Empty(session.SavedLayouts);
    }

    [Fact]
    public void SavedArrangementsAreKeptForEachWorkspace()
    {
        var session = Open();
        session.SaveLayout("Mine");

        session.EnterMode("animation");

        Assert.Empty(session.SavedLayouts);
    }

    [Fact]
    public void ALoadedArrangementTakesTheRevisionOfTheModeSoItIsNotSetAsideNextTime()
    {
        var session = Open();
        session.SaveLayout("Old");
        var old = DockLayout.Standard().CopyWith(revision: 0);
        Directory.CreateDirectory(Orblit("x"));
        File.WriteAllText(Orblit("layouts.json"), "{\"scene\":{\"Old\":" + old.ToText() + "}}");
        var again = Open();

        again.LoadLayout("Old");

        Assert.Equal(DockLayout.Standard().Revision, again.Layout.Revision);
    }

    [Fact]
    public void ANamedArrangementFromBeforeTheChangeIsStillThere()
    {
        var old = DockLayout.Standard().CopyWith(revision: 2).Close("profile");
        Directory.CreateDirectory(Orblit("x"));
        File.WriteAllText(Orblit("layouts.json"), "{\"scene\":{\"Old\":" + old.ToText() + "}}");
        var session = Open();

        session.LoadLayout("Old");

        Assert.Equal(PanelIds(old), PanelIds(session.Layout));
    }

    [Fact]
    public void LoadingAnArrangementThatIsNotThereSaysSo()
    {
        var session = Open();
        var said = new List<(string Text, SayLevel Level)>();
        session.Said += (text, level) => said.Add((text, level));

        session.LoadLayout("Nothing");

        Assert.Equal([("That layout could not be read.", SayLevel.Error)], said);
    }

    [Fact]
    public void SavingAnArrangementWithNoNameDoesNothing()
    {
        var session = Open();

        session.SaveLayout("   ");

        Assert.Empty(session.SavedLayouts);
        Assert.False(File.Exists(Orblit("layouts.json")));
    }

    [Fact]
    public void OpeningAPanelShowsItAndAnUnknownKindDoesNothing()
    {
        var session = Open();

        session.OpenPanel(PanelKind.Profile);
        Assert.Contains("profile", PanelIds(session.Layout));

        var before = session.Layout;
        session.OpenPanel(new PanelKind("nothing", "Nothing", "x"));
        Assert.Same(before, session.Layout);
    }

    [Fact]
    public void LeavingAModeRunsItsOwnLeaveBeforeTheNextOnesEnter()
    {
        var calls = new List<string>();
        var modes = EditorModes.Standard();
        modes.Register(new EditorMode
        {
            Name = "extra",
            Label = "Extra",
            Icon = "x",
            Layout = DockLayout.Standard,
            OnEnter = () => calls.Add("enter extra"),
            OnLeave = () => calls.Add("leave extra"),
        });
        var session = Open(modes: modes);

        session.EnterMode("extra");
        session.EnterMode("scene");

        Assert.Equal(new[] { "enter extra", "leave extra" }, calls);
    }

    [Fact]
    public void UndoShowsWhatItChangedByDroppingWhatIsNoLongerThere()
    {
        var session = Open();
        var scene = session.Workspace.Loaded!.Scene!;
        session.History.Run(new AddObject(new SceneObject(id: "extra", name: "Extra", kind: ObjectKind.Mesh), "a"));
        session.Select(["cube", "extra"]);

        session.Undo();

        Assert.Equal(new[] { "cube" }, session.Selected);
        Assert.Equal("cube", session.Primary);
        Assert.False(scene.Contains("extra"));
    }

    [Fact]
    public void RedoPutsTheStepBackAndKeepsTheSelection()
    {
        var session = Open();
        session.History.Run(new AddObject(new SceneObject(id: "extra", name: "Extra", kind: ObjectKind.Mesh), "a"));
        session.Undo();
        session.Select(["cube"]);

        session.Redo();

        Assert.Equal(new[] { "cube" }, session.Selected);
        Assert.True(session.Workspace.Loaded!.Scene!.Contains("extra"));
    }

    [Fact]
    public void SelectingKeepsEachOnceAndTheLastIsThePrimary()
    {
        var session = Open();

        session.Select(["cube", "ground", "cube"]);

        Assert.Equal(new[] { "cube", "ground" }, session.Selected);
        Assert.Equal("ground", session.Primary);
    }

    [Fact]
    public void ASceneIsUnsavedUntilItIsWritten()
    {
        var session = Open();
        var entry = session.Workspace.Loaded!;
        Assert.True(session.IsUnsaved(entry));

        session.Save();
        Assert.False(session.IsUnsaved(entry));
        Assert.False(session.AnyUnsaved);

        session.History.Run(Move(entry.Scene!, "cube", 3));
        Assert.True(session.IsUnsaved(entry));
    }

    [Fact]
    public void SavingWritesTheSceneInTheFormatTheEditorReads()
    {
        var session = Open();
        session.History.Run(Move(session.Workspace.Loaded!.Scene!, "cube", 3));

        session.Save();

        var load = SceneDocument.Decode(File.ReadAllText(Scenes("main.oscene")));
        Assert.False(load.HasProblems, string.Join("; ", load.Problems));
        Assert.Equal(3, load.Scene["cube"]!.Position.X);
    }

    [Fact]
    public void SavingSaysWhichFileWasWritten()
    {
        var session = Open();
        var said = new List<string>();
        session.Said += (text, _) => said.Add(text);

        session.Save();

        Assert.Equal([$"Saved {Path.Combine("scenes", "main.oscene")}"], said);
    }

    [Fact]
    public void SavingASceneWithNoFileAsksForAName()
    {
        var session = Open();
        session.Workspace.Loaded!.Path = null;
        var asked = 0;
        var said = new List<string>();
        session.NameWanted += () => asked++;
        session.Said += (text, _) => said.Add(text);

        session.Save();

        Assert.Equal(1, asked);
        Assert.Empty(said);
        Assert.False(Directory.Exists(Scenes("")));
    }

    [Fact]
    public void SaveAsAsksForANameOnlyWhenASceneIsOpen()
    {
        var asked = 0;
        var session = Open();
        session.NameWanted += () => asked++;
        session.AskForName();
        Assert.Equal(1, asked);

        var workspace = new Workspace(_project.FullName);
        var empty = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        empty.NameWanted += () => asked++;
        empty.AskForName();
        Assert.Equal(1, asked);
    }

    [Theory]
    [InlineData("other")]
    [InlineData("other.oscene")]
    public void SaveAsGivesTheSceneAFileInScenesAndWritesIt(string name)
    {
        var session = Open();

        session.SaveAs(name);

        Assert.True(File.Exists(Scenes("other.oscene")));
        Assert.Equal(Scenes("other.oscene"), session.Workspace.Loaded!.Path);
    }

    [Fact]
    public void SaveAsOneOwnNameWritesTheSceneAgain()
    {
        var session = Open();
        session.Save();

        session.SaveAs("main");

        Assert.Equal(Scenes("main.oscene"), session.Workspace.Loaded!.Path);
        Assert.False(session.AnyUnsaved);
    }

    [Fact]
    public void SaveAsRefusesANameWithAPathInIt()
    {
        var session = Open();
        var said = new List<(string Text, SayLevel Level)>();
        session.Said += (text, level) => said.Add((text, level));

        session.SaveAs(Path.Combine("sub", "other"));

        Assert.Equal(("A scene name cannot contain a path.", SayLevel.Error), Assert.Single(said));
        Assert.Equal(Scenes("main.oscene"), session.Workspace.Loaded!.Path);
        Assert.False(Directory.Exists(Scenes("sub")));
    }

    [Fact]
    public void SaveAsRefusesTheNameOfAFileThatIsAlreadyThere()
    {
        Directory.CreateDirectory(Scenes(""));
        File.WriteAllText(Scenes("other.oscene"), "keep me");
        var session = Open();
        var said = new List<(string Text, SayLevel Level)>();
        session.Said += (text, level) => said.Add((text, level));

        session.SaveAs("other");

        Assert.Equal(("There is already a scene called other.", SayLevel.Error), Assert.Single(said));
        Assert.Equal("keep me", File.ReadAllText(Scenes("other.oscene")));
        Assert.Equal(Scenes("main.oscene"), session.Workspace.Loaded!.Path);
    }

    [Fact]
    public void SaveAsThatCannotWriteLeavesTheSceneWhereItWas()
    {
        // A folder where the new file belongs.
        Directory.CreateDirectory(Scenes("other.oscene"));
        var session = Open();
        session.Workspace.Loaded!.Path = null;
        var said = new List<string>();
        session.Said += (text, _) => said.Add(text);

        session.SaveAs("other");

        Assert.StartsWith("Could not save", Assert.Single(said));
        Assert.Null(session.Workspace.Loaded!.Path);
    }

    [Fact]
    public void ASceneThatCannotBeWrittenSaysWhyAndStaysUnsaved()
    {
        // A folder where the file belongs.
        Directory.CreateDirectory(Scenes("main.oscene"));
        var session = Open();
        var said = new List<(string Text, SayLevel Level)>();
        session.Said += (text, level) => said.Add((text, level));

        session.Save();

        Assert.StartsWith("Could not save", Assert.Single(said).Text);
        Assert.True(session.AnyUnsaved);
    }

    [Fact]
    public void TheSharedObjectsGoWithTheSceneOnlyOnceTheyHaveSomethingInThem()
    {
        var session = Open();
        session.Save();
        Assert.False(File.Exists(Path.Combine(_project.FullName, Workspace.SharedFileName)));

        session.History.Run(new AddObject(
            new SceneObject(id: "manager", name: "Manager", kind: ObjectKind.Group),
            Workspace.SharedSceneId));
        session.Save();

        Assert.True(File.Exists(Path.Combine(_project.FullName, Workspace.SharedFileName)));
        Assert.False(session.IsUnsaved(session.Workspace.SharedEntry));
    }

    [Fact]
    public void SavingTheSharedObjectsAlongsideSaysOnlyWhichSceneWasWritten()
    {
        var session = Open();
        session.History.Run(new AddObject(
            new SceneObject(id: "manager", name: "Manager", kind: ObjectKind.Group),
            Workspace.SharedSceneId));
        var said = new List<string>();
        session.Said += (text, _) => said.Add(text);

        session.Save();

        Assert.True(File.Exists(Path.Combine(_project.FullName, Workspace.SharedFileName)));
        Assert.Equal([$"Saved {Path.Combine("scenes", "main.oscene")}"], said);
    }

    [Fact]
    public void SharedObjectsThatCannotBeWrittenSayWhatFailed()
    {
        // A folder where the shared file belongs.
        Directory.CreateDirectory(Path.Combine(_project.FullName, Workspace.SharedFileName));
        var session = Open();
        session.History.Run(new AddObject(
            new SceneObject(id: "manager", name: "Manager", kind: ObjectKind.Group),
            Workspace.SharedSceneId));
        var said = new List<(string Text, SayLevel Level)>();
        session.Said += (text, level) => said.Add((text, level));

        session.Save();

        Assert.Equal(SayLevel.Info, said[0].Level);
        Assert.StartsWith("Could not save the shared objects:", said[^1].Text);
        Assert.Equal(SayLevel.Error, said[^1].Level);
        Assert.True(session.IsUnsaved(session.Workspace.SharedEntry));
    }

    [Fact]
    public void SavingWithNothingOpenDoesNothing()
    {
        var workspace = new Workspace(_project.FullName);
        var session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn());

        session.Save();

        Assert.False(Directory.Exists(Scenes("")));
    }

    [Fact]
    public void ChangesAreAnnouncedOnceEach()
    {
        var session = Open();
        var count = 0;
        session.Changed += () => count++;

        session.ToggleStats();
        session.ToggleFocus();
        session.EnterMode("animation");

        Assert.Equal(3, count);
    }

    [Fact]
    public void ChangedSaysWhenOnlyTheSelectionChanged()
    {
        var session = Open();
        var seen = new List<bool>();
        session.Changed += () => seen.Add(session.OnlySelectionChanged);

        session.Select(["sun"]);
        session.SelectScene("a");
        session.ClearSelection();
        session.ToggleFocus();
        session.EnterMode("animation");

        Assert.Equal([true, true, true, false, false], seen);
        Assert.False(session.OnlySelectionChanged);
    }

    [Fact]
    public void AChangeRaisedFromInsideASelectionRaiseIsNotOnlyTheSelection()
    {
        var session = Open();
        var seen = new List<bool>();
        var raised = false;
        session.Changed += () =>
        {
            seen.Add(session.OnlySelectionChanged);
            if (!raised)
            {
                raised = true;
                session.ToggleStats();
            }
        };

        session.Select(["sun"]);

        // The selection first, then the stats toggle that a handler made while it ran.
        Assert.Equal([true, false], seen);
    }
}
