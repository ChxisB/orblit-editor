using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>The window asks before the first Play of a project's game, and the File menu takes the answer back.</summary>
[Collection(TinyGameBuild.Collection)]
public sealed class EditorWindowTrustTests(TinyGameBuild fixture) : IClassFixture<TinyGameBuild>, IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_trust");
    private readonly DirectoryInfo _support = Directory.CreateTempSubdirectory("orblit_window_trust_file");

    public void Dispose()
    {
        _project.Delete(recursive: true);
        _support.Delete(recursive: true);
    }

    private string Folder => Path.TrimEndingDirectorySeparator(Path.GetFullPath(_project.FullName));

    private (EditorSession Session, EditorWindow Window, FakeBuild Build, ProjectTrust Trust) Open()
    {
        var build = GameTestSupport.Copying(fixture.Build);
        var trust = new ProjectTrust(Path.Combine(_support.FullName, "trusted-projects.json"));
        var session = GameTestSupport.Open(_project, GameTestSupport.Entry("TinyGame.Mover"), build, trust);
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window, build, trust);
    }

    private static TrustDialog TheQuestion(EditorWindow window) =>
        Assert.IsType<TrustDialog>(Assert.Single(window.OwnedWindows));

    private static void Answer(TrustDialog dialog, string button)
    {
        Headless.Click(dialog, button);
        Headless.Render();
        Headless.Render();
    }

    // Nothing here awaits, because a continuation back to the UI thread is only reached while the test renders.
    [Fact]
    public Task APlayOnAnUntrustedGameAsksAndCancelStartsNothing() => Headless.Run(() =>
    {
        var (session, window, build, trust) = Open();

        Headless.Click(window, "play_toggle");
        Headless.Render();
        var question = TheQuestion(window);
        Assert.Equal(0, build.Runs);
        Assert.True(Headless.Find<Button>(question, "cancel").IsDefault);

        Answer(question, "cancel");

        Assert.Empty(window.OwnedWindows);
        Assert.Equal(0, build.Runs);
        Assert.False(session.Playing);
        Assert.False(trust.IsTrusted(Folder));
        Assert.Contains("not been trusted", session.Log.Entries[^1].Message);
        window.Close();
    });

    [Fact]
    public Task TrustAndPlayBuildsTheGameAndTheNextPlayDoesNotAsk() => Headless.Run(() =>
    {
        var (session, window, build, trust) = Open();
        Headless.Click(window, "play_toggle");
        Headless.Render();

        Answer(TheQuestion(window), "trust");

        Assert.True(Headless.Until(() => session.Playing), "The game did not start.");
        Assert.Equal(1, build.Runs);
        Assert.True(trust.IsTrusted(Folder));

        Headless.Click(window, "play_stop");
        Headless.Click(window, "play_toggle");
        Assert.True(Headless.Until(() => session.Playing), "The game did not start the second time.");
        Assert.Empty(window.OwnedWindows);
        window.Close();
    });

    [Fact]
    public Task StopTrustingInTheFileMenuStopsTheGameAndMakesPlayAskAgain() => Headless.Run(() =>
    {
        var (session, window, build, trust) = Open();
        trust.Grant(Folder);
        Headless.Click(window, "play_toggle");
        Assert.True(Headless.Until(() => session.Playing), "The game did not start.");

        var item = window.GetLogicalDescendants().OfType<MenuItem>().First(one => one.Name == "menu_stop_trusting");
        Assert.Equal("Stop trusting this project", item.Header);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.Render();

        Assert.False(session.Playing);
        Assert.False(trust.IsTrusted(Folder));

        Headless.Click(window, "play_toggle");
        Headless.Render();
        Answer(TheQuestion(window), "cancel");
        Assert.Equal(1, build.Runs);
        window.Close();
    });
}
