namespace Orblit.Editor.Tests;

using Avalonia.Controls;
using Orblit.Editor.Core;

/// <summary>The top bar and the window while a project's game builds and plays.</summary>
[Collection(TinyGameBuild.Collection)]
public sealed class EditorWindowGameTests(TinyGameBuild fixture) : IClassFixture<TinyGameBuild>, IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_game");

    public void Dispose() => _project.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) Open(IBuildCommand build)
    {
        var session = GameTestSupport.Open(_project, GameTestSupport.Entry("TinyGame.Mover"), build);
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static Button ButtonNamed(EditorWindow window, string name) => Headless.Find<Button>(window, name);

    // Nothing here awaits, because a continuation back to the UI thread is only reached while the test renders.
    [Fact]
    public Task WhileTheGameBuildsPlayIsOffAndStopCancelsTheBuild() => Headless.Run(() =>
    {
        var started = new TaskCompletionSource();
        var (session, window) = Open(GameTestSupport.Blocked(started));

        Headless.Click(window, "play_toggle");
        Assert.True(Headless.Until(() => started.Task.IsCompleted));
        Assert.True(session.Building);
        Assert.False(ButtonNamed(window, "play_toggle").IsEnabled);
        Assert.True(ButtonNamed(window, "play_stop").IsEnabled);

        Headless.Click(window, "play_stop");
        Assert.True(Headless.Until(() => !session.Building));
        Assert.True(ButtonNamed(window, "play_toggle").IsEnabled);
        Assert.False(ButtonNamed(window, "play_stop").IsEnabled);
        window.Close();
    });

    [Fact]
    public Task ClosingTheWindowStopsTheGameAndLetsItsAssemblyUnload() => Headless.Run(() =>
    {
        var (session, window) = Open(GameTestSupport.Copying(fixture.Build));
        Headless.Click(window, "play_toggle");
        Assert.True(Headless.Until(() => session.Playing), "The game did not start.");
        Assert.True(GameTestSupport.AnyGameContext());

        window.Close();

        Assert.Null(session.Playback);
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
    });
}
