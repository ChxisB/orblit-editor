namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Data objects in the editor: which one the inspector shows, attaching one to the selection, and the
// bindings. Follows the data objects group of editor_shell_data_test, asked of the session, with
// AttachData for the drop. The rest are added in C#.
public sealed class EditorSessionDataTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_data_session");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private EditorSession Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        File.WriteAllText(PathOf("weight.odata"), DataObject.Blank("weight").ToText());
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
    }

    private static SceneObject Named(EditorSession session, string name) =>
        session.Workspace.Loaded!.Scene!.Objects.Single(one => one.Name == name);

    private static string IdOf(EditorSession session, string name) => Named(session, name).Id;

    [Fact]
    public void PickingADataObjectShowsItInTheInspector()
    {
        var session = Open();

        session.PickAsset(AssetTree.Describe(PathOf("weight.odata")));

        Assert.Equal("weight.odata", session.DataAsset);
    }

    [Fact]
    public void PickingAMeshDoesNotTakeTheInspector()
    {
        var session = Open();
        File.WriteAllBytes(PathOf("rock.glb"), [1]);
        session.Select([IdOf(session, "Crate")]);

        session.PickAsset(AssetTree.Describe(PathOf("rock.glb")));

        Assert.Null(session.DataAsset);
        Assert.Equal("Crate", session.InspectedItem?.Name);
    }

    [Fact]
    public void SelectingAnObjectPutsTheInspectorBack()
    {
        var session = Open();
        session.PickAsset(AssetTree.Describe(PathOf("weight.odata")));

        session.Select([IdOf(session, "Crate")]);

        Assert.Null(session.DataAsset);
    }

    // Added in C#. Dart leaves the data object showing, though the scene row was touched last.
    [Fact]
    public void SelectingASceneRowPutsTheInspectorBack()
    {
        var session = Open();
        session.PickAsset(AssetTree.Describe(PathOf("weight.odata")));

        session.SelectScene(session.Workspace.Loaded!.Id);

        Assert.Null(session.DataAsset);
    }

    [Fact]
    public void AttachingPointsTheSelectionAtIt()
    {
        var session = Open();
        session.Select([IdOf(session, "Crate")]);

        session.AttachData(PathOf("weight.odata"));

        Assert.Equal(["weight.odata"], Named(session, "Crate").Data);
        Assert.Equal("Added weight.odata to Crate.", session.Last?.Text);
    }

    [Fact]
    public void AttachingIsUndoable()
    {
        var session = Open();
        session.Select([IdOf(session, "Crate")]);
        session.AttachData(PathOf("weight.odata"));

        session.Undo();

        Assert.Empty(Named(session, "Crate").Data);
    }

    [Fact]
    public void WhatItSaysIsSavedWithTheScene()
    {
        var session = Open();
        session.Select([IdOf(session, "Crate")]);
        session.AttachData(PathOf("weight.odata"));

        session.Save();

        Assert.Contains("weight.odata", File.ReadAllText(PathOf("scenes/main.oscene")), StringComparison.Ordinal);
    }

    [Fact]
    public void AttachingWithNothingSelectedSaysWhatToDo()
    {
        var session = Open();

        session.AttachData(PathOf("weight.odata"));

        Assert.Equal("Select something first, then drop weight.odata on it.", session.Last?.Text);
    }

    [Fact]
    public void AFileThatIsNotADataObjectIsNotAttached()
    {
        var session = Open();
        File.WriteAllText(PathOf("broken.odata"), "nonsense");
        session.Select([IdOf(session, "Crate")]);

        session.AttachData(PathOf("broken.odata"));

        Assert.Empty(Named(session, "Crate").Data);
        Assert.Equal("broken.odata is not a readable data object.", session.Last?.Text);
    }

    [Fact]
    public void DetachingTakesItOff()
    {
        var session = Open();
        var crate = IdOf(session, "Crate");
        session.Select([crate]);
        session.AttachData(PathOf("weight.odata"));

        session.DetachData(crate, "weight.odata");

        Assert.Empty(Named(session, "Crate").Data);
    }

    [Fact]
    public void ShowingAFileThatHasGoneSaysSo()
    {
        var session = Open();

        session.ShowData("gone.odata");

        Assert.Null(session.DataAsset);
        Assert.Equal("gone.odata is not in the project any more.", session.Last?.Text);
    }

    [Fact]
    public void TheBindingsItWritesLandBesideIt()
    {
        var session = Open();
        Directory.CreateDirectory(PathOf("data"));
        File.WriteAllText(PathOf("data/ball.odata"), DataObject.Blank("Ball").ToText());

        session.WriteBindings("data/ball.odata");

        // Both languages from the one declaration, which is what makes a field renamed here a build
        // error in whatever reads it.
        Assert.Contains("export interface", File.ReadAllText(PathOf("data/ball.d.ts")), StringComparison.Ordinal);
        Assert.Contains("namespace Ball", File.ReadAllText(PathOf("data/ball.h")), StringComparison.Ordinal);
        Assert.Equal("Wrote ball.d.ts and ball.h.", session.Last?.Text);
    }
}
