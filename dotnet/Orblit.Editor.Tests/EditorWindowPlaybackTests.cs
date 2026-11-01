using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

/// <summary>Play, pause and stop from the top bar, and the game view they show.</summary>
public sealed class EditorWindowPlaybackTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_playback");

    public void Dispose() => _project.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) Open(EditorScene scene)
    {
        Directory.CreateDirectory(Path.Combine(_project.FullName, "scenes"));
        File.WriteAllText(Path.Combine(_project.FullName, "scenes", "main.oscene"), SceneDocument.Encode(scene));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    // A cube that plays the clip it names when the scene starts, beside the starter's camera.
    private static EditorScene BouncingCube(string clip) => new(
    [
        EditorScene.Starter()["camera"]!,
        new SceneObject(
            "cube",
            "Cube",
            ObjectKind.Mesh,
            components: new OrderedDictionary<string, Doc.SceneComponent>
            {
                [Doc.SceneComponents.Motion] = new Doc.MotionComponent([clip], clip),
            }),
    ]);

    private static ClipDocument Bounce() => new(
        "Bounce",
        1,
        WhenDone.Hold,
        channels:
        [
            new ClipChannel<Vector3>(
                "",
                "transform.position",
                ChannelKind.Vector,
                [new Key<Vector3>(0, Vector3.Zero()), new Key<Vector3>(1, new Vector3(0, 2, 0))]),
        ]);

    private static double HeightOf(EditorScene scene) => scene["cube"]!.Position.Y;

    private static Button ButtonNamed(EditorWindow window, string name) => Headless.Find<Button>(window, name);

    private static GameViewPanel GameView(EditorWindow window) => Headless.All<GameViewPanel>(window).Single();

    [Fact]
    public Task PlayPauseAndStopRunTheSceneFromTheTopBar() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());
        Assert.True(ButtonNamed(window, "play_toggle").IsEnabled);
        Assert.False(ButtonNamed(window, "play_stop").IsEnabled);

        Headless.Click(window, "play_toggle");
        Headless.Render();
        Assert.True(session.Playing);
        Assert.True(ButtonNamed(window, "play_stop").IsEnabled);

        Headless.Click(window, "play_toggle");
        Headless.Render();
        Assert.False(session.Playing);
        Assert.NotNull(session.Playback);
        Assert.True(ButtonNamed(window, "play_stop").IsEnabled);

        Headless.Click(window, "play_stop");
        Headless.Render();
        Assert.Null(session.Playback);
        Assert.False(ButtonNamed(window, "play_stop").IsEnabled);
        window.Close();
    });

    // Play in four views previews unsaved animation without changing the scene. The preview is the game view
    // tab and not a four views layout.
    [Fact]
    public Task PlayPreviewsUnsavedAnimationWithoutChangingTheScene() => Headless.Run(() =>
    {
        var clip = Path.Combine(_project.FullName, "bounce.oclip");
        File.WriteAllText(clip, new ClipDocument("Bounce", 1).Encode());
        var (session, window) = Open(BouncingCube("bounce.oclip"));
        session.Clips.OpenFile(clip);
        session.Clips.Edit("Bounce", _ => Bounce());
        var editing = session.Workspace.Loaded!.Scene!;

        Headless.Click(window, "play_toggle");
        // The window's clock has run the scene for as long as the click took, which is longer on a slow
        // machine. Half a second is measured from where it stands, and the clip holds at two. The clock can
        // still add one frame of at most a tenth of a second, which is a fifth of the clip's height.
        var before = HeightOf(session.Playback!.Workspace.Loaded!.Scene!);
        session.AdvancePlayback(0.5);
        var playing = session.Playback!.Workspace.Loaded!.Scene!;
        Assert.Equal(Math.Min(2, before + 1), HeightOf(playing), 0.2);
        Assert.Equal(0, HeightOf(editing));

        Headless.Click(window, "play_toggle");
        var paused = HeightOf(playing);
        Headless.Wait(150);
        Assert.Equal(paused, HeightOf(playing));

        Headless.Click(window, "play_stop");
        Headless.Render();
        Assert.Null(session.Playback);
        Assert.Same(editing, session.Workspace.Loaded!.Scene);
        Assert.Equal(0, HeightOf(editing));
        window.Close();
    });

    [Fact]
    public Task WithNoSceneOpenThereIsNothingToPlay() => Headless.Run(() =>
    {
        var empty = new Workspace(_project.FullName);
        var session = new EditorSession(empty, new History(empty), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();

        Assert.False(ButtonNamed(window, "play_toggle").IsEnabled);
        Assert.False(ButtonNamed(window, "play_stop").IsEnabled);
        window.Close();
    });

    [Fact]
    public Task PlayShowsTheGameViewWithNothingOverThePictureWhenTheSceneHasACamera() => Headless.Run(() =>
    {
        var (_, window) = Open(EditorScene.Starter());

        Headless.Click(window, "play_toggle");
        Headless.Render();

        var view = GameView(window);
        Assert.True(view.IsEffectivelyVisible);
        Assert.Null(view.Saying);
        window.Close();
    });

    [Fact]
    public Task WithNoCameraTheGameViewSaysSoAndAddCameraMakesOne() => Headless.Run(() =>
    {
        var (session, window) = Open(new EditorScene([new SceneObject("cube", "Cube", ObjectKind.Mesh)]));

        session.OpenPanel(PanelKind.Game);
        Headless.Render();

        var view = GameView(window);
        Assert.StartsWith("This scene has no camera", view.Saying);
        var add = Headless.Find<Button>(window, "game_view_add_camera");
        Assert.True(add.IsEffectivelyVisible);

        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Headless.Render();

        Assert.Null(view.Saying);
        Assert.False(add.IsVisible);
        Assert.NotNull(SceneCameras.GameCamera(session.Workspace.Loaded!.Scene!));
        window.Close();
    });

    [Fact]
    public Task WhilePlayingOrPausedTheGameViewOffersNoNewCameraBecauseItShowsACopy() => Headless.Run(() =>
    {
        var (session, window) = Open(new EditorScene([new SceneObject("cube", "Cube", ObjectKind.Mesh)]));
        session.OpenPanel(PanelKind.Game);
        Headless.Render();
        var add = Headless.Find<Button>(window, "game_view_add_camera");

        Headless.Click(window, "play_toggle");
        Headless.Render();
        Assert.False(add.IsVisible);
        Assert.StartsWith("This scene has no camera", GameView(window).Saying);

        Headless.Click(window, "play_toggle");
        Headless.Render();
        Assert.False(add.IsVisible);

        Headless.Click(window, "play_stop");
        Headless.Render();
        Assert.True(add.IsVisible);
        window.Close();
    });

    [Fact]
    public Task TimePassesInThePlayingSceneForEachFrameTheWindowDraws() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());
        var steps = 0;
        session.PlaybackChanged += () => steps++;

        Headless.Click(window, "play_toggle");

        Assert.True(Headless.Until(() => steps >= 4), $"The scene took {steps} steps.");
        window.Close();
    });

    [Fact]
    public Task APausedSceneAsksForNoFrames() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());
        Headless.Click(window, "play_toggle");
        Headless.Until(() => session.Playing);
        Headless.Click(window, "play_toggle");
        Headless.Render();
        var steps = 0;
        session.PlaybackChanged += () => steps++;

        Headless.Wait(150);

        Assert.Equal(0, steps);
        window.Close();
    });

    [Fact]
    public Task APauseLongerThanAFrameDoesNotJumpTheSceneOnWhenPlayGoesOn() => Headless.Run(() =>
    {
        var clip = Path.Combine(_project.FullName, "bounce.oclip");
        File.WriteAllText(clip, Bounce().Encode());
        var (session, window) = Open(BouncingCube("bounce.oclip"));
        var steps = 0;
        session.PlaybackChanged += () => steps++;
        session.TogglePlayback();
        Assert.True(Headless.Until(() => steps >= 4), $"The scene took {steps} steps.");
        session.TogglePlayback();
        var copy = session.Playback!.Workspace.Loaded!.Scene!;
        var paused = HeightOf(copy);

        Thread.Sleep(300);
        session.TogglePlayback();
        Headless.Render();

        // A render may run a frame or two, each a few milliseconds. The jump from a stale time is a tenth of a second, which is 0.2 high.
        Assert.InRange(HeightOf(copy), paused, paused + 0.05);
        window.Close();
    });

    [Fact]
    public Task ClosingTheWindowEndsTheClock() => Headless.Run(() =>
    {
        var (session, window) = Open(EditorScene.Starter());
        window.Close();
        Headless.Render();
        var steps = 0;
        session.PlaybackChanged += () => steps++;

        session.TogglePlayback();
        Headless.Wait(150);

        Assert.Equal(1, steps);
    });
}
