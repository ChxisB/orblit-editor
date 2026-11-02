namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>Play on a project's game asks before it builds, keeps a yes, and never runs code on open.</summary>
[Collection(TinyGameBuild.Collection)]
public sealed class EditorSessionTrustTests(TinyGameBuild fixture) : IClassFixture<TinyGameBuild>, IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_trust_play");
    private readonly DirectoryInfo _support = Directory.CreateTempSubdirectory("orblit_trust_file");
    private readonly List<EditorSession> _sessions = [];
    private readonly List<string> _asked = [];

    public void Dispose()
    {
        foreach (var session in _sessions)
        {
            session.StopPlayback();
        }

        _project.Delete(recursive: true);
        _support.Delete(recursive: true);
    }

    private string Folder => Path.TrimEndingDirectorySeparator(Path.GetFullPath(_project.FullName));

    private ProjectTrust Trusted() => new(Path.Combine(_support.FullName, "trusted-projects.json"));

    // The person's answer to the question, and a record of what was asked.
    private Func<string, Task<bool>> Answering(bool answer) => folder =>
    {
        _asked.Add(folder);
        return Task.FromResult(answer);
    };

    private EditorSession Open(IBuildCommand build, IProjectTrust trust, bool answer = false)
    {
        var session = GameTestSupport.Open(_project, GameTestSupport.Entry("TinyGame.Mover"), build, trust);
        session.AskToTrust = Answering(answer);
        _sessions.Add(session);
        return session;
    }

    [Fact]
    public async Task AYesBuildsTheGameAndIsKeptSoTheNextPlayDoesNotAsk()
    {
        var build = GameTestSupport.Copying(fixture.Build);
        var trust = Trusted();
        var session = Open(build, trust, answer: true);

        await session.TogglePlayback();

        Assert.Equal([Folder], _asked);
        Assert.Equal(1, build.Runs);
        Assert.True(session.Playing);
        Assert.True(Trusted().IsTrusted(Folder));

        session.StopPlayback();
        await session.TogglePlayback();

        Assert.Single(_asked);
        Assert.Equal(2, build.Runs);
        Assert.True(session.Playing);
    }

    [Fact]
    public async Task ANoBuildsNothingSaysSoAndKeepsNothing()
    {
        var build = GameTestSupport.Copying(fixture.Build);
        var session = Open(build, Trusted(), answer: false);

        await session.TogglePlayback();

        Assert.Single(_asked);
        Assert.Equal(0, build.Runs);
        Assert.Null(session.Playback);
        Assert.False(GameTestSupport.AnyGameContext());
        Assert.False(Trusted().IsTrusted(Folder));
        var entry = session.Log.Entries[^1];
        Assert.Equal(SayLevel.Warning, entry.Level);
        Assert.Contains("not been trusted", entry.Message);
    }

    [Fact]
    public async Task ATrustedProjectIsNotAsked()
    {
        var trust = Trusted();
        trust.Grant(Folder);
        var session = Open(GameTestSupport.Copying(fixture.Build), trust);

        await session.TogglePlayback();

        Assert.Empty(_asked);
        Assert.True(session.Playing);
    }

    [Fact]
    public async Task ASessionWithNobodyToAskNeverRunsTheCode()
    {
        var build = GameTestSupport.Copying(fixture.Build);
        var session = GameTestSupport.Open(_project, GameTestSupport.Entry("TinyGame.Mover"), build, Trusted());
        _sessions.Add(session);

        await session.TogglePlayback();

        Assert.Equal(0, build.Runs);
        Assert.Null(session.Playback);
    }

    // The person said yes, so this run goes ahead. Only the next run asks again.
    [Fact]
    public async Task AYesThatCannotBeWrittenStillPlaysOnceAndSaysItWasNotKept()
    {
        var build = GameTestSupport.Copying(fixture.Build);
        var session = Open(build, new UnwritableTrust(), answer: true);

        await session.TogglePlayback();

        Assert.True(session.Playing);
        Assert.Equal(1, build.Runs);
        Assert.Contains(session.Log.Entries, entry =>
            entry.Level == SayLevel.Warning
            && entry.Message.Contains("could not be kept", StringComparison.Ordinal)
            && entry.Detail!.Contains("disk is full", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StopTrustingStopsARunningGameAndTheNextPlayAsksAgain()
    {
        var build = GameTestSupport.Copying(fixture.Build);
        var trust = Trusted();
        trust.Grant(Folder);
        var session = Open(build, trust, answer: false);
        await session.TogglePlayback();
        Assert.True(GameTestSupport.AnyGameContext());

        session.StopTrusting();

        Assert.Null(session.Playback);
        Assert.False(Trusted().IsTrusted(Folder));
        Assert.Equal("This project is no longer trusted to run its code.", session.Log.Entries[^1].Message);
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));

        await session.TogglePlayback();

        Assert.Single(_asked);
        Assert.Equal(1, build.Runs);
    }

    // A build that Stop does not cancel would wait for ever, so the test says so after a while.
    [Fact(Timeout = 20_000)]
    public async Task StopTrustingDuringABuildCancelsItAndStartsNoGame()
    {
        var started = new TaskCompletionSource();
        var trust = Trusted();
        trust.Grant(Folder);
        var session = Open(GameTestSupport.Blocked(started), trust);

        var playing = session.TogglePlayback();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        session.StopTrusting();
        await playing.WaitAsync(TestContext.Current.CancellationToken);

        Assert.False(session.Building);
        Assert.Null(session.Playback);
        Assert.False(GameTestSupport.AnyGameContext());
        Assert.False(Trusted().IsTrusted(Folder));
    }

    [Fact]
    public async Task AQuestionThatThrowsSaysSoAndStartsNothing()
    {
        var build = GameTestSupport.Copying(fixture.Build);
        var session = Open(build, Trusted());
        session.AskToTrust = _ => throw new InvalidOperationException("The dialog has no window.");

        await session.TogglePlayback();

        Assert.Equal(0, build.Runs);
        Assert.Null(session.Playback);
        var entry = session.Log.Entries[^1];
        Assert.Equal(SayLevel.Error, entry.Level);
        Assert.Contains("Play stopped", entry.Message);
        Assert.Contains("The dialog has no window.", entry.Detail);
    }

    // The question is open and nothing is building yet, so a second press has to be turned away here.
    [Fact(Timeout = 20_000)]
    public async Task APressWhileTheQuestionIsOpenIsIgnored()
    {
        var answer = new TaskCompletionSource<bool>();
        var asks = 0;
        var build = GameTestSupport.Copying(fixture.Build);
        var session = Open(build, Trusted());
        session.AskToTrust = _ =>
        {
            asks++;
            return answer.Task;
        };

        var first = session.TogglePlayback();
        var second = session.TogglePlayback();
        answer.SetResult(true);
        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, asks);
        Assert.Equal(1, build.Runs);
        Assert.True(session.Playing);
    }

    [Fact]
    public async Task AProjectFileThatCannotBeReadSaysSoAndPlaysNothing()
    {
        var build = GameTestSupport.Copying(fixture.Build);
        var session = Open(build, Trusted());

        // Holding the file open without sharing makes the read fail on every system.
        using (new FileStream(Path.Combine(_project.FullName, Project.FileName), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await session.TogglePlayback();
        }

        Assert.Equal(0, build.Runs);
        Assert.Empty(_asked);
        Assert.Null(session.Playback);
        Assert.False(session.Playing);
        var entry = session.Log.Entries[^1];
        Assert.Equal(SayLevel.Error, entry.Level);
        Assert.Contains("project file could not be read", entry.Message);
    }

    [Fact]
    public void StopTrustingThatCannotBeWrittenSaysSo()
    {
        var session = Open(GameTestSupport.Copying(fixture.Build), new UnwritableTrust());

        session.StopTrusting();

        var entry = session.Log.Entries[^1];
        Assert.Equal(SayLevel.Error, entry.Level);
        Assert.Contains("could not be taken back", entry.Message);
        Assert.Contains("disk is full", entry.Detail);
    }

    [Fact]
    public void OpeningAProjectWithAGameBuildsNothingAndLoadsNothing()
    {
        var build = GameTestSupport.Copying(fixture.Build);

        var session = Open(build, new NoProjectTrust());
        session.Save();
        session.Add(ObjectKind.Group);

        Assert.NotNull(ProjectStore.Open(_project.FullName)!.Game);
        Assert.Equal(0, build.Runs);
        Assert.Empty(_asked);
        Assert.False(GameTestSupport.AnyGameContext());
        Assert.False(Directory.Exists(Path.Combine(_project.FullName, ".orblit")));
    }

    // Trust that cannot be written, as on a read-only profile folder.
    private sealed class UnwritableTrust : IProjectTrust
    {
        public bool IsTrusted(string projectFolder) => false;

        public void Grant(string projectFolder) => throw new IOException("The disk is full.");

        public void Revoke(string projectFolder) => throw new IOException("The disk is full.");
    }
}
