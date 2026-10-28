using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// Dart: orblit-editor/test/editor_shell_clips_test.dart, the retargeting group. A clip shown on the
// timeline made to move another model, through the clip menu and the dialog.
public sealed class EditorWindowRetargetTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_retarget");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private static ClipDocument Wave() => new(
        "wave",
        1,
        channels:
        [
            new ClipChannel<Quaternion>(
                "",
                "rotation",
                ChannelKind.Rotation,
                [
                    new Key<Quaternion>(0, Quaternion.Identity(), Hold.Linear),
                    new Key<Quaternion>(1, Quaternion.AxisAngle(new Vector3(1, 0, 0), 0.5), Hold.Linear),
                ],
                bone: "spine"),
        ]);

    // The wave clip open and shown, in a project with a hero and a dwarf to move it between.
    private (EditorSession Session, EditorWindow Window) OpenWave()
    {
        File.WriteAllBytes(PathOf("hero.glb"), SkinnedGlb.ChainGlb("hips", "spine", "head"));
        File.WriteAllBytes(PathOf("dwarf.glb"), SkinnedGlb.ChainGlb("hips", "spine", "neck"));
        File.WriteAllText(PathOf("wave.oclip"), Wave().Encode());
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        session.OpenClip(PathOf("wave.oclip"));
        Headless.Render();
        return (session, window);
    }

    private static RetargetDialog AskToRetarget(Window window)
    {
        Headless.Click(window, "timeline_clip");
        var menu = Headless.Find<Button>(window, "timeline_clip").ContextMenu!;
        menu.Items.OfType<MenuItem>().First(item => item.Name == "timeline_clip_retarget")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.Render();
        return Assert.IsType<RetargetDialog>(Assert.Single(window.OwnedWindows));
    }

    [Fact]
    public Task MakesAClipForAnotherModelAndShowsItInPlaceOfTheFirst() => Headless.Run(() =>
    {
        var (session, window) = OpenWave();
        var dialog = AskToRetarget(window);

        RetargetDialogTests.Pick(dialog, "models_from", "hero.glb");
        RetargetDialogTests.Pick(dialog, "models_to", "dwarf.glb");
        Headless.Click(dialog, "make");
        Headless.Render();

        var made = ClipDocument.Decode(File.ReadAllText(PathOf("clips/wave (dwarf).oclip"))).Clip;
        Assert.Equal("wave (dwarf)", made.Name);
        Assert.Equal(["spine"], made.Channels.Select(channel => channel.Bone).Distinct());
        Assert.Equal(PathOf("clips/wave (dwarf).oclip"), session.Clips.Shown!.Path);
        Assert.Empty(window.OwnedWindows);
        window.Close();
    });

    [Fact]
    public Task StartsOnTheModelOfTheObjectTheClipBelongsTo() => Headless.Run(() =>
    {
        var (session, window) = OpenWave();
        session.Workspace.Loaded!.Scene!["cube"]!.MeshAsset = "hero.glb";
        session.Clips.Owner = "cube";

        var dialog = AskToRetarget(window);

        Assert.Equal("hero.glb", RetargetDialogTests.ShownBy(dialog, "models_from"));
        window.Close();
    });

    [Fact]
    public Task LeavesTheProjectAloneWhenItIsCancelled() => Headless.Run(() =>
    {
        var (session, window) = OpenWave();
        var dialog = AskToRetarget(window);

        Headless.Click(dialog, "cancel");
        Headless.Render();

        Assert.False(Directory.Exists(PathOf("clips")));
        Assert.Equal(PathOf("wave.oclip"), session.Clips.Shown!.Path);
        window.Close();
    });
}
