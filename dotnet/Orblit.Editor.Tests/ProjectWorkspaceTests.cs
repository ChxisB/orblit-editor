namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class ProjectWorkspaceTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");

    public void Dispose() => _temp.Delete(recursive: true);

    private string Scenes(string file) => Path.Combine(_temp.FullName, "scenes", file);

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string SceneText(string name, int objects)
    {
        var scene = new EditorScene([], name: name);
        for (var at = 0; at < objects; at++)
        {
            scene.Add(new SceneObject(id: $"o{at}", name: $"Object {at}", kind: ObjectKind.Group));
        }

        return SceneDocument.Encode(scene, name);
    }

    [Fact]
    public void AFolderWithNoSceneOpensTheStarterAndSaysNothing()
    {
        var opened = ProjectWorkspace.Open(_temp.FullName);

        var entry = Assert.Single(opened.Workspace.Entries);
        Assert.Equal("scene0", entry.Id);
        Assert.True(entry.NeverWritten);
        Assert.True(entry.IsLoaded);
        Assert.Equal(Scenes("main.oscene"), entry.Path);
        Assert.Empty(opened.Problems);
    }

    [Fact]
    public void TheMainSceneIsReadAndLoaded()
    {
        Write(Scenes("main.oscene"), SceneText("Level", 3));

        var opened = ProjectWorkspace.Open(_temp.FullName);

        var entry = Assert.Single(opened.Workspace.Entries);
        Assert.False(entry.NeverWritten);
        Assert.Equal("Level", entry.Name);
        Assert.Equal(3, entry.Scene!.Length);
        Assert.Same(entry, opened.Workspace.Loaded);
    }

    [Fact]
    public void OtherScenesAreListedButNotLoaded()
    {
        Write(Scenes("main.oscene"), SceneText("Main", 1));
        Write(Scenes("cave.oscene"), SceneText("Cave", 2));
        Write(Scenes("attic.oscene"), SceneText("Attic", 2));
        Write(Scenes("notes.txt"), "not a scene");

        var entries = ProjectWorkspace.Open(_temp.FullName).Workspace.Entries;

        Assert.Equal(new[] { "Main", "attic", "cave" }, entries.Select(entry => entry.Name));
        Assert.Equal(new[] { "scene0", "scene1", "scene2" }, entries.Select(entry => entry.Id));
        Assert.All(entries.Skip(1), entry => Assert.False(entry.IsLoaded));
    }

    [Fact]
    public void ADamagedMainSceneOpensTheStarterWithNoFileSoNothingOverwritesIt()
    {
        Write(Scenes("main.oscene"), "{not a scene");

        var opened = ProjectWorkspace.Open(_temp.FullName);

        // The damaged file still shows in the list, as Dart lists it, so it can be found and fixed.
        var entry = opened.Workspace.Entries[0];
        Assert.Null(entry.Path);
        Assert.True(entry.IsLoaded);
        Assert.Equal(Scenes("main.oscene"), opened.Workspace.Entries[1].Path);
        Assert.NotEmpty(opened.Problems);
        Assert.Equal("{not a scene", File.ReadAllText(Scenes("main.oscene")));
    }

    [Fact]
    public void TheSharedObjectsAreReadWhenThereIsAFile()
    {
        Write(Path.Combine(_temp.FullName, Workspace.SharedFileName), SceneText("Shared", 2));

        var opened = ProjectWorkspace.Open(_temp.FullName);

        Assert.Equal(2, opened.Workspace.Shared.Length);
        Assert.False(opened.Workspace.SharedEntry.NeverWritten);
    }

    [Fact]
    public void WithNoSharedFileTheSetIsEmptyAndNeverWritten()
    {
        var opened = ProjectWorkspace.Open(_temp.FullName);

        Assert.Equal(0, opened.Workspace.Shared.Length);
        Assert.True(opened.Workspace.SharedEntry.NeverWritten);
        Assert.Empty(opened.Problems);
    }

    [Fact]
    public void ADamagedSharedFileSaysSoAndLeavesTheSetEmpty()
    {
        Write(Path.Combine(_temp.FullName, Workspace.SharedFileName), "{nope");

        var opened = ProjectWorkspace.Open(_temp.FullName);

        Assert.Equal(0, opened.Workspace.Shared.Length);
        Assert.StartsWith("The shared objects could not be read", Assert.Single(opened.Problems));
    }

    [Fact]
    public void ReportOneProblemAsItIsAndSeveralAsACount()
    {
        var workspace = ProjectWorkspace.Open(_temp.FullName).Workspace;
        var session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var said = new List<(string Text, SayLevel Level)>();
        session.Said += (text, level) => said.Add((text, level));

        session.Report([]);
        session.Report(["one"]);
        session.Report(["first", "second"]);

        Assert.Equal(2, said.Count);
        Assert.Equal(("one", SayLevel.Error), said[0]);
        Assert.Equal(SayLevel.Error, said[1].Level);
        Assert.Contains("2 things", said[1].Text);
        Assert.Contains("first", said[1].Text);
    }
}
