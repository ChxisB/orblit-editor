namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Game;
using Orblit.Input;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

/// <summary>
/// Play with more than one player: a host that runs the game, a guest for each other player, and the
/// wire between them. The wire delivers on another thread, so a test advances frames until what it
/// waits for has arrived.
/// </summary>
[Collection(TinyGameBuild.Collection)]
public sealed class EditorSessionPlayersTests(TinyGameBuild fixture) : IClassFixture<TinyGameBuild>, IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_game_players");
    private readonly List<EditorSession> _sessions = [];

    public void Dispose()
    {
        foreach (var session in _sessions)
        {
            session.StopPlayback();
        }

        _project.Delete(recursive: true);
    }

    private EditorSession Open(string type, int players, IBuildCommand? build = null)
    {
        var session = GameTestSupport.Open(_project, GameTestSupport.Entry(type), build ?? GameTestSupport.Copying(fixture.Build));
        _sessions.Add(session);
        session.PlayerCount = players;
        return session;
    }

    private async Task<EditorSession> Playing(string type, int players)
    {
        var session = Open(type, players);
        await session.TogglePlayback();
        return session;
    }

    private static EditorScene SceneOf(EditorSession session, int player)
    {
        session.ShowPlayer(player);
        return session.ShownPlayback!.Workspace.Loaded!.Scene!;
    }

    private static double X(EditorSession session, int player, string id) => SceneOf(session, player)[id]?.Position.X ?? double.NaN;

    // A frame, then a short wait for the wire, until the thing awaited is there or two seconds have gone.
    private static bool Advancing(EditorSession session, Func<bool> arrived)
    {
        for (var frame = 0; frame < 400 && !arrived(); frame++)
        {
            session.AdvancePlayback(0.016);
            Thread.Sleep(5);
        }

        return arrived();
    }

    private static IEnumerable<LogEntry> Said(EditorSession session) => session.Log.Entries;

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

    private sealed class Held(params string[] keys) : IGameInputFeed
    {
        public GameInput Read() => new(new FrameControls(keys));

        public void EndFrame()
        {
        }
    }

    [Fact]
    public void TheCountStartsAtOneAndStaysBetweenOneAndFour()
    {
        var session = GameTestSupport.Open(_project, GameTestSupport.Entry("TinyGame.Tiny"), GameTestSupport.Copying(fixture.Build));
        Assert.Equal(1, session.PlayerCount);

        session.PlayerCount = 3;
        Assert.Equal(3, session.PlayerCount);
        session.PlayerCount = 0;
        Assert.Equal(1, session.PlayerCount);
        session.PlayerCount = 9;
        Assert.Equal(EditorSession.MaxPlayers, session.PlayerCount);
        Assert.Equal(4, EditorSession.MaxPlayers);
    }

    [Fact]
    public async Task OnePlayerRunsOneGameWithNoNet()
    {
        var session = await Playing("TinyGame.Replicated", 1);

        session.AdvancePlayback(0.1);
        session.AdvancePlayback(0.1);

        Assert.Equal(1, session.PlayersRunning);
        Assert.Equal(1, GameTestSupport.GameContextCount());
        Assert.Equal(0, X(session, 0, "crate"));
    }

    [Fact]
    public async Task TwoPlayersMakeTwoGamesEachInItsOwnContextAndCopyAfterOneBuild()
    {
        var build = GameTestSupport.Copying(fixture.Build);
        var session = Open("TinyGame.Replicated", 2, build);

        await session.TogglePlayback();

        Assert.Equal(2, session.PlayersRunning);
        Assert.Equal(2, GameTestSupport.GameContextCount());
        Assert.Equal(1, build.Runs);
        Assert.NotSame(SceneOf(session, 0), SceneOf(session, 1));
        Assert.Null(session.Workspace.Loaded!.Scene!["crate"]);
    }

    [Fact]
    public async Task WhatTheHostMovesAndSetsShowsInTheGuestsScene()
    {
        var session = await Playing("TinyGame.Replicated", 2);

        Assert.True(Advancing(session, () => X(session, 1, "crate") == 7 && X(session, 1, "readout") == 9));
        Assert.Equal(7, X(session, 0, "crate"));
    }

    [Fact]
    public async Task WhatAGuestOwnsShowsInTheHostsScene()
    {
        var session = await Playing("TinyGame.Replicated", 2);

        Assert.True(Advancing(session, () => X(session, 0, "p2") == 3));
        Assert.Equal(3, X(session, 1, "p2"));
        Assert.Equal(0, X(session, 1, "p1"));
        Assert.Equal(0, X(session, 0, "p3"));
    }

    [Fact]
    public async Task ThreePlayersGiveTheSecondAndThirdOwnerEntityToTheGuests()
    {
        var session = await Playing("TinyGame.Replicated", 3);

        Assert.True(Advancing(session, () => X(session, 0, "p2") == 3 && X(session, 0, "p3") == 3));
        Assert.Equal(0, X(session, 0, "p1"));
        Assert.Equal(3, session.PlayersRunning);
        Assert.Equal(3, GameTestSupport.GameContextCount());
    }

    [Fact]
    public async Task AGameWithNoVariablesPlaysWithTwoPlayers()
    {
        var session = await Playing("TinyGame.Tiny", 2);

        session.AdvancePlayback(0.1);

        Assert.Equal(2, session.PlayersRunning);
        Assert.DoesNotContain(Said(session), one => one.Level == SayLevel.Error);
    }

    [Fact]
    public async Task StopEndsEveryPlayerAndUnloadsEachContext()
    {
        var session = await Playing("TinyGame.Replicated", 2);
        session.ShowPlayer(1);

        session.StopPlayback();

        Assert.Equal(0, session.PlayersRunning);
        Assert.Null(session.Playback);
        Assert.Equal(0, session.ShownPlayer);
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
    }

    [Fact]
    public async Task PlayAfterStopStartsFreshPlayers()
    {
        var session = await Playing("TinyGame.Replicated", 2);
        Assert.True(Advancing(session, () => X(session, 1, "crate") == 7));
        session.StopPlayback();

        await session.TogglePlayback();

        Assert.Equal(2, session.PlayersRunning);
        Assert.Equal(0, X(session, 1, "crate"));
    }

    [Fact]
    public async Task AGameThatThrowsOnAGuestStopsEveryPlayerAndNamesTheGuest()
    {
        var session = await Playing("TinyGame.FallsOnAClient", 2);

        session.AdvancePlayback(0.1);

        Assert.Null(session.Playback);
        Assert.Equal(0, session.PlayersRunning);
        var entry = Said(session).Last(one => one.Level == SayLevel.Error);
        Assert.StartsWith("Player 2:", entry.Message);
        Assert.Contains("Play has stopped.", entry.Message);
        Assert.Contains("the client fell", entry.Detail);
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
    }

    [Fact]
    public async Task AGuestThatCannotStartStartsNoOneAndNamesTheGuest()
    {
        var session = await Playing("TinyGame.FallsStartingOnAClient", 3);

        Assert.Null(session.Playback);
        Assert.False(session.Playing);
        Assert.Equal(0, session.PlayersRunning);
        Assert.StartsWith("Player 2:", Said(session).Last(one => one.Level == SayLevel.Error).Message);
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
    }

    [Fact]
    public async Task EachPlayersLinesInTheLogCarryItsNumberAndOnePlayersDoNot()
    {
        var two = await Playing("TinyGame.Talker", 2);

        Assert.Contains(Said(two), one => one.Message == "Player 1: ready");
        Assert.Contains(Said(two), one => one.Message == "Player 2: ready");
        two.StopPlayback();

        var one = await Playing("TinyGame.Talker", 1);

        Assert.Contains(Said(one), entry => entry.Message == "ready");
    }

    [Fact]
    public async Task AHostThatCannotStartStartsNoGuestAndNamesItself()
    {
        var session = await Playing("TinyGame.FallsStartingOnTheServer", 2);

        Assert.Null(session.Playback);
        Assert.False(session.Playing);
        Assert.StartsWith("Player 1:", Said(session).Last(one => one.Level == SayLevel.Error).Message);
        Assert.DoesNotContain(Said(session), one => one.Message.EndsWith("ready", StringComparison.Ordinal));
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
    }

    [Fact]
    public async Task AThrowOnOnePlayerStopsTheFrameBeforeTheNextPlayersRun()
    {
        var session = await Playing("TinyGame.FallsOnTheSecondPlayer", 3);

        session.AdvancePlayback(0.1);

        Assert.Null(session.Playback);
        var updated = Said(session).Where(one => one.Message.EndsWith("updated", StringComparison.Ordinal)).ToList();
        Assert.Equal("Player 1: updated", Assert.Single(updated).Message);
        Assert.StartsWith("Player 2:", Said(session).Last(one => one.Level == SayLevel.Error).Message);
    }

    [Fact]
    public async Task EveryPlayersCopyPlaysItsOwnClips()
    {
        var cube = new SceneObject(
            "cube",
            "Cube",
            ObjectKind.Mesh,
            components: new OrderedDictionary<string, Doc.SceneComponent>
            {
                [Doc.SceneComponents.Motion] = new Doc.MotionComponent(["bounce.oclip"], "bounce.oclip"),
            });
        GameTestSupport.WriteProject(_project, GameTestSupport.Entry("TinyGame.Tiny"), new EditorScene([EditorScene.Starter()["camera"]!, cube]));
        File.WriteAllText(Path.Combine(_project.FullName, "bounce.oclip"), Bounce().Encode());
        var session = GameTestSupport.OpenWritten(_project, GameTestSupport.Copying(fixture.Build));
        _sessions.Add(session);
        session.PlayerCount = 3;
        await session.TogglePlayback();

        session.AdvancePlayback(0.5);

        Assert.All(Enumerable.Range(0, 3), player => Assert.Equal(1, SceneOf(session, player)["cube"]!.Position.Y, 1e-9));
    }

    [Fact]
    public async Task ShowingAPlayerShowsItsCopyAndAnUnknownPlayerChangesNothing()
    {
        var session = await Playing("TinyGame.Replicated", 2);
        var changes = 0;
        session.PlaybackChanged += () => changes++;
        var host = session.ShownPlayback;

        session.ShowPlayer(1);

        Assert.Equal(1, session.ShownPlayer);
        Assert.NotSame(host, session.ShownPlayback);
        Assert.Same(host, session.Playback);
        Assert.Equal(1, changes);

        session.ShowPlayer(2);
        session.ShowPlayer(-1);
        session.ShowPlayer(1);

        Assert.Equal(1, session.ShownPlayer);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task OnlyTheShownPlayerIsGivenTheKeys()
    {
        var session = await Playing("TinyGame.KeyReader", 2);
        session.InputFeed = new Held("KeyG");

        session.AdvancePlayback(0.016);

        Assert.Equal(1, X(session, 0, "crate"));
        Assert.Equal(0, X(session, 1, "crate"));

        session.ShowPlayer(0);
        session.ShowPlayer(1);
        session.AdvancePlayback(0.016);

        Assert.Equal(0, X(session, 0, "crate"));
        Assert.Equal(1, X(session, 1, "crate"));
    }

    [Fact]
    public async Task TheGameOfTheShownPlayerIsTheOneTheWindowAsksForAControl()
    {
        var session = await Playing("TinyGame.Faced", 2);
        var host = session.PlayingGame;

        session.ShowPlayer(1);

        Assert.NotNull(host);
        Assert.NotNull(session.PlayingGame);
        Assert.NotSame(host, session.PlayingGame);
    }

    [Fact]
    public async Task TakingBackTrustStopsEveryPlayer()
    {
        var session = await Playing("TinyGame.Tiny", 2);

        session.StopTrusting();

        Assert.Equal(0, session.PlayersRunning);
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
    }
}
