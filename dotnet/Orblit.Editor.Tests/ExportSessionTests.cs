namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

// The cook and the folder are a fake, so nothing cooks.
public sealed class ExportSessionTests
{
    private readonly FakeExports _exports = new();

    private static ExportedPack Pack(string name, string target = "macos", string? @base = null) => new()
    {
        Path = $"/project/exports/{name}",
        Target = target,
        Hash = $"hash-of-{name}",
        Base = @base,
        Assets = 3,
        Removed = 0,
        Bytes = 2048,
        Modified = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
    };

    private static PackContents Contents(params string[] packs) => new(packs, [], []);

    private ExportSession Session() => new("/project", _exports, "/cache");

    // Chooses exactly these, whatever the machine started on.
    private static void Choose(ExportSession session, params string[] names)
    {
        foreach (var name in session.Targets.ToList())
        {
            session.ToggleTarget(name);
        }

        foreach (var name in names)
        {
            session.ToggleTarget(name);
        }
    }

    [Fact]
    public void StartsOnTheMachinesOwnTarget()
    {
        var session = Session();

        Assert.Equal([CookProject.CurrentCookTarget.Name], session.Targets);
        Assert.True(session.CanExport);
    }

    [Fact]
    public void ListsTargetsInTheOrderOrblitNamesThem()
    {
        var session = Session();

        Choose(session, "web", "android", "macos");

        Assert.Equal(["macos", "android", "web"], session.Targets);
    }

    [Fact]
    public void WithNoTargetChosenThereIsNothingToExport()
    {
        var session = Session();

        Choose(session);

        Assert.Empty(session.Targets);
        Assert.False(session.CanExport);
    }

    [Fact]
    public void APatchIsForItsBasesTargetAndNoOther()
    {
        var session = Session();
        Choose(session, "macos", "web");

        session.ChoosePatchOf(Pack("game.android.opack", "android"));
        Assert.Equal(["android"], session.Targets);

        session.ChoosePatchOf(null);
        Assert.Equal(["macos", "web"], session.Targets);
    }

    [Fact]
    public async Task LandsEachTargetAsItFinishes()
    {
        var session = Session();
        Choose(session, "macos", "android");

        var done = session.StartAsync();

        Assert.True(session.Running);
        Assert.Equal("macos", session.Cooking);
        Assert.False(session.CanExport);

        _exports.Land(0);
        Assert.Equal(["macos"], session.Results.Select(result => result.Target));
        Assert.Equal("android", session.Cooking);

        _exports.Land(1);
        await done;

        Assert.Equal(["macos", "android"], session.Results.Select(result => result.Target));
        Assert.False(session.Running);
        Assert.Null(session.Cooking);
    }

    [Fact]
    public async Task HandsTheRunnerTheBaseAndTheCache()
    {
        var session = Session();
        var @base = Pack("game.macos.opack");
        _exports.Held = [@base];
        await session.LoadAsync();
        session.ChoosePatchOf(@base);

        var done = session.StartAsync();
        _exports.Land(0);
        await done;

        var request = Assert.Single(_exports.Requests);
        Assert.Equal(@base.Path, request.PatchOf);
        Assert.Equal("/cache", request.CachePath);
        Assert.Equal("/project", request.ProjectDirectory);
        Assert.Equal(["macos"], request.Targets);
    }

    [Fact]
    public async Task ASecondStartWhileOneRunsDoesNothing()
    {
        var session = Session();

        var first = session.StartAsync();
        // Not awaited. A second run would wait on a target that never lands, and the test would hang.
        var second = session.StartAsync();
        _exports.Land(0);
        await first;

        Assert.True(second.IsCompleted);
        Assert.Single(_exports.Requests);
    }

    [Fact]
    public async Task ReadsTheFolderAgainWhenItIsDone()
    {
        var session = Session();
        await session.LoadAsync();
        var before = _exports.Listed;
        _exports.Held = [Pack("game.macos.opack")];

        var done = session.StartAsync();
        _exports.Land(0);
        await done;

        Assert.Equal(before + 1, _exports.Listed);
        Assert.Single(session.Listing.Packs);
    }

    [Fact]
    public async Task LetsGoOfTheRunningStateWhenTheRunnerThrows()
    {
        _exports.Refuse = new InvalidOperationException("no");
        var session = Session();

        await Assert.ThrowsAsync<InvalidOperationException>(session.StartAsync);

        Assert.False(session.Running);
        Assert.Null(session.Cooking);
        Assert.True(session.CanExport);
    }

    [Fact]
    public async Task DropsABaseThatIsNoLongerThere()
    {
        var @base = Pack("game.macos.opack");
        _exports.Held = [@base];
        var session = Session();
        await session.LoadAsync();
        session.ChoosePatchOf(@base);

        _exports.Held = [];
        await session.LoadAsync();

        Assert.Null(session.PatchOf);
    }

    [Fact]
    public async Task KeepsABaseThatIsStillThere()
    {
        var @base = Pack("game.macos.opack");
        _exports.Held = [@base];
        var session = Session();
        await session.LoadAsync();
        session.ChoosePatchOf(@base);

        await session.LoadAsync();

        Assert.Equal(@base, session.PatchOf);
    }

    [Fact]
    public async Task ReadsWhatAPackServes()
    {
        var pack = Pack("game.macos.opack");
        var contents = Contents("game.macos.opack");
        _exports.Reader = (directory, path) =>
        {
            Assert.Equal("/project", directory);
            Assert.Equal(pack.Path, path);
            return Task.FromResult(contents);
        };
        var session = Session();

        await session.ShowAsync(pack);

        Assert.Equal(pack, session.Shown);
        Assert.Same(contents, session.Contents);
        Assert.Null(session.ContentsProblem);
    }

    [Fact]
    public async Task SaysWhyABrokenChainCannotBeRead()
    {
        _exports.Reader = (_, _) => Task.FromException<PackContents>(new ExportChainBrokenException("no pack is that one"));
        var session = Session();

        await session.ShowAsync(Pack("game.macos.abc.opack", @base: "missing"));

        Assert.Null(session.Contents);
        Assert.Equal("no pack is that one", session.ContentsProblem);
    }

    [Fact]
    public async Task ASlowReadForAnEarlierPackDoesNotReplaceALaterOne()
    {
        TaskCompletionSource<PackContents> slow = new();
        var first = Pack("game.macos.opack");
        var second = Pack("game.macos.abc.opack");
        var answer = Contents("second");
        _exports.Reader = (_, path) => path == first.Path ? slow.Task : Task.FromResult(answer);
        var session = Session();

        var reading = session.ShowAsync(first);
        await session.ShowAsync(second);
        slow.SetResult(Contents("first"));
        await reading;

        Assert.Equal(second, session.Shown);
        Assert.Same(answer, session.Contents);
    }

    // A broken chain is caught whichever pack was shown, so a slow read for an earlier pack shows nothing.
    [Fact]
    public async Task ASlowBrokenReadForAnEarlierPackDoesNotShowItsProblem()
    {
        TaskCompletionSource<PackContents> slow = new();
        var first = Pack("game.macos.opack");
        _exports.Reader = (_, path) => path == first.Path ? slow.Task : Task.FromResult(Contents("second"));
        var session = Session();

        var reading = session.ShowAsync(first);
        await session.ShowAsync(Pack("game.macos.abc.opack"));
        slow.SetException(new ExportChainBrokenException("no pack is that one"));
        await reading;

        Assert.Null(session.ContentsProblem);
        Assert.NotNull(session.Contents);
    }

    // The session is listened to, so this shows nobody is told, and not only that nothing threw.
    [Fact]
    public async Task AReadThatLandsAfterDisposeTellsNobody()
    {
        TaskCompletionSource<ExportListing> slow = new();
        _exports.Lister = () => slow.Task;
        var session = Session();
        var told = 0;
        session.Changed += () => told++;
        var loading = session.LoadAsync();

        session.Dispose();
        slow.SetResult(ExportListing.Empty);
        await loading;

        Assert.Equal(0, told);
    }

    // A runner that waits for the test before landing each target, so the session can be looked at between them.
    private sealed class FakeExports : IExportWork
    {
        private readonly List<TaskCompletionSource> _landing = [];

        public FakeExports() => Lister = () =>
        {
            Listed++;
            return Task.FromResult(new ExportListing(Held, []));
        };

        public List<ExportRequest> Requests { get; } = [];

        public IReadOnlyList<ExportedPack> Held { get; set; } = [];

        public int Listed { get; private set; }

        public Exception? Refuse { get; set; }

        public Func<Task<ExportListing>> Lister { get; set; }

        public Func<string, string, Task<PackContents>> Reader { get; set; } = (_, _) => Task.FromResult(Contents());

        public void Land(int index) => _landing[index].SetResult();

        public async Task<IReadOnlyList<TargetExport>> RunAsync(ExportRequest request, Action<TargetExport> onTarget)
        {
            Requests.Add(request);
            if (Refuse is not null)
            {
                throw Refuse;
            }

            List<TargetExport> done = [];
            foreach (var target in request.Targets)
            {
                TaskCompletionSource gate = new();
                _landing.Add(gate);
                // Not back to the test's context, so a landing is seen as soon as Land returns.
                await gate.Task.ConfigureAwait(false);
                var result = new TargetExport { Target = target, Outcome = ExportOutcome.Packed };
                done.Add(result);
                onTarget(result);
            }

            return done;
        }

        public Task<ExportListing> ListAsync(string projectDirectory) => Lister();

        public Task<PackContents> ReadAsync(string projectDirectory, string path) => Reader(projectDirectory, path);
    }
}
