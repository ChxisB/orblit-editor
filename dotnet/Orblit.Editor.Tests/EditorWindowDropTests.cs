using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Dart: orblit-editor/test/editor_shell_drop_test.dart. A file is let go over the scene view as a real drop
// of what a browser tile's drag carries. The drag itself is the system's, and the headless platform has
// no way to start one, so the tile's half is not run here. F is in EditorWindowKeysTests. The cases after
// the Dart ones cover the kinds the session routes elsewhere and the ones it refuses.
public sealed class EditorWindowDropTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_drops");
    private readonly List<string> _said = [];

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private (EditorSession Session, EditorWindow Window) Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var prefabs = new PrefabFiles(_project.FullName);
        var opened = ProjectWorkspace.Open(_project.FullName, prefabs.Find);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn())
        {
            Prefabs = prefabs,
        };
        session.Said += (text, _) => _said.Add(text);
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static List<Border> Rows(Window window, string name) =>
    [
        .. Headless.All<Border>(Headless.All<OutlinerPanel>(window).Single())
            .Where(view => view.Name?.StartsWith("row_", StringComparison.Ordinal) == true && Headless.All<TextBlock>(view).Any(text => text.Text == name)),
    ];

    private static SceneObject Named(EditorSession session, string name) =>
        session.Workspace.Loaded!.Scene!.Objects.Single(one => one.Name == name);

    private static List<string> Words(Control on) => [.. Headless.All<TextBlock>(on).Select(text => text.Text ?? "")];

    private static void Carry(Window window, string path, params RawDragEventType[] steps)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(AssetDrops.Format, path));
        var at = Headless.Centre(window, Headless.All<SceneViewport>(window).Single());
        foreach (var step in steps)
        {
            window.DragDrop(at, step, data, DragDropEffects.Copy, RawInputModifiers.None);
        }

        Headless.Render();
    }

    private static void Drop(Window window, string path) =>
        Carry(window, path, RawDragEventType.DragEnter, RawDragEventType.DragOver, RawDragEventType.Drop);

    [Fact]
    public Task AMeshDroppedOnTheViewGoesInTheScene() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Drop(window, PathOf("assets/crate.glb"));

        Assert.Single(Rows(window, "crate"));
        Assert.Equal("Add crate", session.History.UndoLabel);
        Assert.Equal("assets/crate.glb", Named(session, "crate").MeshAsset);
        Assert.Equal(Named(session, "crate").Id, session.Primary);
        window.Close();
    });

    [Fact]
    public Task TheMeshIsSavedAsAPathFromTheProject() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Drop(window, PathOf("assets/crate.glb"));
        session.Save();

        Assert.Contains("\"assets/crate.glb\"", File.ReadAllText(PathOf("scenes/main.oscene")));
        window.Close();
    });

    [Fact]
    public Task ATextureWithNothingSelectedSaysWhatToDo() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Drop(window, PathOf("textures/rock.png"));

        Assert.Contains(_said, text => text.StartsWith("Select an object first", StringComparison.Ordinal));
        Assert.Empty(Rows(window, "rock"));
        window.Close();
    });

    [Fact]
    public Task ATextureGoesOnTheSelectedObject() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Headless.Click(window, Rows(window, "Crate").Single());

        Drop(window, PathOf("textures/rock.png"));

        var inspector = Headless.All<InspectorPanel>(window).Single();
        Assert.Contains("Texture", Words(inspector));
        Assert.Contains(Words(inspector), text => text.Contains("rock.png", StringComparison.Ordinal));
        Assert.DoesNotContain(_said, text => text.StartsWith("Select an object first", StringComparison.Ordinal));
        Assert.Equal("textures/rock.png", Named(session, "Crate").MaterialAsset);
        window.Close();
    });

    [Fact]
    public Task TheViewIsLitWhileAFileIsOverIt() => Headless.Run(() =>
    {
        var (_, window) = Open();
        var hint = Headless.Find<Border>(window, "drop_hint");

        Carry(window, PathOf("assets/crate.glb"), RawDragEventType.DragEnter, RawDragEventType.DragOver);
        Assert.True(hint.IsVisible);
        Carry(window, PathOf("assets/crate.glb"), RawDragEventType.DragLeave);
        Assert.False(hint.IsVisible);

        Carry(window, PathOf("assets/crate.glb"), RawDragEventType.DragEnter, RawDragEventType.DragOver, RawDragEventType.Drop);
        Assert.False(hint.IsVisible);
        window.Close();
    });

    [Fact]
    public Task ASceneDroppedOnTheViewOpens() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var other = EditorScene.Starter();
        other.Name = "Other";
        File.WriteAllText(PathOf("scenes/other.oscene"), SceneDocument.Encode(other));

        Drop(window, PathOf("scenes/other.oscene"));

        Assert.True(Headless.Until(() => session.Workspace.Loaded?.Path == PathOf("scenes/other.oscene")));
        window.Close();
    });

    [Fact]
    public Task AFileASceneCannotTakeSaysSo() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var count = session.Workspace.Loaded!.Scene!.Objects.Count;

        Drop(window, PathOf("sounds/beep.ogg"));

        Assert.Contains(_said, text => text.StartsWith("beep.ogg cannot go in a scene.", StringComparison.Ordinal));
        Assert.Equal(count, session.Workspace.Loaded!.Scene!.Objects.Count);
        window.Close();
    });

    [Fact]
    public Task AnIncludeGoesOnNothing() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Headless.Click(window, Rows(window, "Crate").Single());

        Drop(window, PathOf("shaders/Common.OSHADERINC"));

        Assert.Contains(_said, text => text.StartsWith("Common.OSHADERINC is an include.", StringComparison.Ordinal));
        Assert.Null(Named(session, "Crate").MaterialAsset);
        window.Close();
    });

    [Fact]
    public Task ALightHasNothingToTexture() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Headless.Click(window, Rows(window, "Sun").Single());

        Drop(window, PathOf("textures/rock.png"));

        Assert.Contains("Sun is a light, and has nothing to draw a texture or shader on.", _said);
        Assert.Null(Named(session, "Sun").MaterialAsset);
        window.Close();
    });

    [Fact]
    public Task APrefabDataObjectAndInterfaceGoWhereTheyBelong() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Drop(window, PathOf("prefabs/gone.oprefab"));
        Drop(window, PathOf("data/gone.odata"));
        Drop(window, PathOf("ui/hud.oui"));

        Assert.Contains(_said, text => text.EndsWith("is not in the project any more.", StringComparison.Ordinal));
        Assert.Contains("gone.odata is not a readable data object.", _said);
        Assert.Single(session.Workspace.Loaded!.Scene!.Objects, one => one.Kind == ObjectKind.Canvas);
        window.Close();
    });
}
