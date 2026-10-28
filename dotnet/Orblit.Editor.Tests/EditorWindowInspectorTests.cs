using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

/// <summary>The inspector and the scene view in a real window, reading the project's model and shader files.</summary>
public sealed class EditorWindowInspectorTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_inspector");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    // One triangle, from (-1, 0, -1) to (1, 4, 1).
    private (EditorSession Session, EditorWindow Window) Selecting(string id)
    {
        Directory.CreateDirectory(PathOf("scenes"));
        Directory.CreateDirectory(PathOf("assets"));
        File.WriteAllText(PathOf("assets/crate.obj"), "v -1 0 -1\nv 1 4 1\nv 1 0 -1\nf 1 2 3\n");
        var scene = new EditorScene(
        [
            new SceneObject("crate", "Crate", ObjectKind.Mesh, meshAsset: "assets/crate.obj"),
            new SceneObject("glow", "Glow", ObjectKind.Mesh, materialAsset: "assets/glow.oshader"),
        ]);
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(scene));
        var opened = ProjectWorkspace.Open(_project.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        Headless.Click(window, $"row_{id}");
        Headless.Render();
        return (session, window);
    }

    private static Doc.BodyComponent BodyOf(EditorSession session, string id) =>
        PhysicsComponents.PhysicsBodyOf(session.Workspace.Loaded!.Scene![id]!)!;

    private static void Press(Window window, string name)
    {
        Headless.Click(window, name);
        Headless.Render();
    }

    [Fact]
    public Task AnObjectWearingAShaderShowsItsSection() => Headless.Run(() =>
    {
        var (_, window) = Selecting("glow");

        Assert.Contains(Headless.All<TextBlock>(window), one => one.Text == "Cooking the shader.");
        window.Close();
    });

    // Nothing compiles this, with a compiler or without one, so the cook lands as a problem.
    private void SaveABrokenShader()
    {
        Directory.CreateDirectory(PathOf("assets"));
        File.WriteAllText(PathOf("assets/glow.oshader"), "not a shader");
    }

    private static bool Cooking(Window window) => Headless.All<TextBlock>(window).Any(one => one.Text == "Cooking the shader.");

    [Fact]
    public Task TheShadersAreCookedWhenTheWindowOpens() => Headless.Run(() =>
    {
        SaveABrokenShader();
        var (session, window) = Selecting("glow");

        Assert.True(Headless.Until(() => !Cooking(window)));
        Assert.True(session.Log.HasErrors);
        window.Close();
    });

    [Fact]
    public Task AShaderSavedWhileTheWindowIsOpenIsCooked() => Headless.Run(() =>
    {
        var (session, window) = Selecting("glow");
        // A beat for the watch to be in place before anything changes.
        Headless.Wait(120);

        SaveABrokenShader();

        Assert.True(Headless.Until(() => !Cooking(window)));
        Assert.True(session.Log.HasErrors);
        window.Close();
    });

    [Fact]
    public Task ABodyIsFittedToTheModelFile() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");

        Press(window, "button_add_body");

        Assert.Equal(new Vector3(2, 4, 2), BodyOf(session, "crate").Size);
        Assert.Equal(new Vector3(0, 2, 0), BodyOf(session, "crate").Centre);
        window.Close();
    });

    [Fact]
    public Task AColliderIsReadFromTheModelFile() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");

        Press(window, "button_collide_with_this_model");

        Assert.True(Headless.Until(() => session.Workspace.Loaded!.Scene!["crate"]!.Components.Count > 0));
        Assert.Equal(Doc.BodyShape.Mesh, BodyOf(session, "crate").Shape);
        Assert.Equal(3, BodyOf(session, "crate").MeshIndices.Count);
        window.Close();
    });

    [Fact]
    public Task TheSceneViewPicksAModelByTheSizeOfItsFile() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate");
        var view = Headless.All<SceneViewport>(window).First();

        var box = view.State.BoundsOf!(session.Workspace.Loaded!.Scene!["crate"]!)!.Value;

        Assert.Equal(new Vector3(-1, 0, -1), box.Min);
        Assert.Equal(new Vector3(1, 4, 1), box.Max);
        window.Close();
    });
}
