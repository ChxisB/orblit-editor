namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>Play in a project that has a game: it builds, loads, runs in the playing copy and is let go on Stop.</summary>
[Collection(TinyGameBuild.Collection)]
public sealed class EditorSessionGameTests(TinyGameBuild fixture) : IClassFixture<TinyGameBuild>, IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_game_play");
    private readonly List<EditorSession> _sessions = [];

    public void Dispose()
    {
        foreach (var session in _sessions)
        {
            session.StopPlayback();
        }

        _project.Delete(recursive: true);
    }

    private EditorSession Open(string type, IBuildCommand build, IProjectTrust? trust = null) =>
        Track(GameTestSupport.Open(_project, GameTestSupport.Entry(type), build, trust));

    private EditorSession Track(EditorSession session)
    {
        _sessions.Add(session);
        return session;
    }

    private FakeBuild Copying() => GameTestSupport.Copying(fixture.Build);

    private static EditorScene Shown(EditorSession session) => session.Playback!.Workspace.Loaded!.Scene!;

    private static double CrateX(EditorScene scene) => scene["crate"]!.Position.X;

    [Fact]
    public async Task PlayBuildsTheGameLoadsItAndRunsItInTheCopyOnly()
    {
        var build = Copying();
        var session = Open("TinyGame.Mover", build);

        await session.TogglePlayback();
        session.AdvancePlayback(0.1);
        session.AdvancePlayback(0.1);
        session.AdvancePlayback(0.1);

        Assert.True(session.Playing);
        Assert.Equal(1, build.Runs);
        Assert.Equal(3, CrateX(Shown(session)));
        Assert.Null(session.Workspace.Loaded!.Scene!["crate"]);
    }

    [Fact]
    public async Task StopLetsTheGamesAssemblyUnload()
    {
        var session = Open("TinyGame.Mover", Copying());
        await session.TogglePlayback();
        Assert.True(GameTestSupport.AnyGameContext());

        session.StopPlayback();

        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
    }

    [Fact]
    public async Task PlayAfterStopStartsAFreshGame()
    {
        var session = Open("TinyGame.Mover", Copying());
        await session.TogglePlayback();
        session.AdvancePlayback(0.1);
        session.AdvancePlayback(0.1);
        session.StopPlayback();

        await session.TogglePlayback();
        session.AdvancePlayback(0.1);

        Assert.Equal(1, CrateX(Shown(session)));
        Assert.Single(Shown(session).Objects, one => one.Id == "crate");
    }

    [Fact]
    public async Task AGameThatThrowsInUpdateStopsPlayAndSaysWhereInTheLog()
    {
        var session = Open("TinyGame.FallsInUpdate", Copying());
        await session.TogglePlayback();

        session.AdvancePlayback(0.1);

        Assert.Null(session.Playback);
        Assert.False(session.Playing);
        var entry = session.Log.Entries.Last(one => one.Level == SayLevel.Error);
        Assert.Contains("Play has stopped.", entry.Message);
        Assert.Contains("the crate fell", entry.Detail);
        Assert.Contains("FallsInUpdate.Update", entry.Detail);
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
    }

    [Fact]
    public async Task AFailedBuildSaysTheCompilersWordsAndStartsNothing()
    {
        var build = GameTestSupport.Failing("Games.cs(4,9): error CS1002: ; expected");
        var session = Open("TinyGame.Mover", build);

        await session.TogglePlayback();

        Assert.Null(session.Playback);
        Assert.False(session.Building);
        var entry = session.Log.Entries[^1];
        Assert.Equal(SayLevel.Error, entry.Level);
        Assert.Equal("The game did not build.", entry.Message);
        Assert.Contains("error CS1002", entry.Detail);
        Assert.False(GameTestSupport.AnyGameContext());
    }

    [Fact]
    public async Task AProjectThatIsNotTrustedBuildsNothingAndSaysWhy()
    {
        var build = Copying();
        var session = Open("TinyGame.Mover", build, trust: new NoProjectTrust());

        await session.TogglePlayback();

        Assert.Equal(0, build.Runs);
        Assert.Null(session.Playback);
        var entry = session.Log.Entries[^1];
        Assert.Equal(SayLevel.Warning, entry.Level);
        Assert.Contains("not been trusted", entry.Message);
    }

    [Fact]
    public async Task AGameEntryThatCannotBeUsedSaysWhyAndBuildsNothing()
    {
        var build = Copying();
        var session = Track(GameTestSupport.Open(_project, """{ "project": "../outside/Game.csproj", "type": "Outside.Game" }""", build));

        await session.TogglePlayback();

        Assert.Equal(0, build.Runs);
        Assert.Null(session.Playback);
        var entry = session.Log.Entries[^1];
        Assert.Equal(SayLevel.Error, entry.Level);
        Assert.Contains("inside the project folder", entry.Message);
    }

    [Fact]
    public async Task PausingStopsTheGamesUpdatesAndPlayGoesOn()
    {
        var session = Open("TinyGame.Mover", Copying());
        await session.TogglePlayback();
        session.AdvancePlayback(0.1);

        await session.TogglePlayback();
        session.AdvancePlayback(0.1);
        Assert.Equal(1, CrateX(Shown(session)));

        await session.TogglePlayback();
        session.AdvancePlayback(0.1);
        Assert.Equal(2, CrateX(Shown(session)));
    }

    [Fact]
    public async Task AnEditMadeWhilePlayingReachesTheGame()
    {
        var session = Open("TinyGame.Echo", Copying());
        await session.TogglePlayback();
        session.AdvancePlayback(0.1);
        var start = Shown(session)["camera"]!.Position.X;
        Assert.Equal(start, CrateX(Shown(session)));

        session.Select(["camera"]);
        session.Nudge(new Vector3(2, 0, 0));
        session.AdvancePlayback(0.1);

        Assert.Equal(start + 2, CrateX(Shown(session)));
    }

    // A build that Stop does not cancel would wait for ever, so the test says so after a while.
    [Fact(Timeout = 20_000)]
    public async Task APressWhileTheGameBuildsIsIgnoredAndStopCancelsTheBuild()
    {
        var started = new TaskCompletionSource();
        var build = GameTestSupport.Blocked(started);
        var session = Open("TinyGame.Mover", build);

        var playing = session.TogglePlayback();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(session.Building);
        await session.TogglePlayback();
        Assert.Equal(1, build.Runs);

        session.StopPlayback();
        await playing.WaitAsync(TestContext.Current.CancellationToken);

        Assert.False(session.Building);
        Assert.Null(session.Playback);
        Assert.False(GameTestSupport.AnyGameContext());
    }
}
