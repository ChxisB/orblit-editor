namespace Orblit.Editor.Tests;

using System.Buffers.Binary;
using System.Text;
using Orblit.Asset;
using Orblit.Editor.Core;

public sealed class StoreInstallTests : IAsyncLifetime
{
    private const string Into = "assets/kits/crates";

    private readonly DirectoryInfo _scratch = Directory.CreateTempSubdirectory("orblit_store_install_");
    private readonly List<InstallSession> _sessions = [];
    private StoreFixture _fixture = null!; // set in InitializeAsync, which runs before every test
    private MapTransport _transport = null!; // set in Started, which every test that reads it calls first

    private string Project => Path.Combine(_scratch.FullName, "project");

    private string DownloadsFolder => Path.Combine(_scratch.FullName, "downloads");

    public async ValueTask InitializeAsync()
    {
        _fixture = await StoreFixture.BuildAsync(_scratch.FullName);
        var crate = InProject($"{Into}/models/crate.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(crate)!);
        File.WriteAllText(crate, "my own crate");
    }

    public ValueTask DisposeAsync()
    {
        foreach (var session in _sessions)
        {
            session.Dispose();
        }

        _scratch.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ListsTheScriptsAndTheFileThatWouldBeReplaced()
    {
        var session = await StartedAsync();

        Assert.Equal(InstallStage.Choosing, session.Stage);
        Assert.Equal(new[] { Id("scripts/spin.js") }, session.Kit!.Scripts);
        Assert.Equal(new[] { Id("models/crate.obj") }, await session.ConflictsAsync(Into));
        Assert.Empty(await session.ConflictsAsync());
    }

    [Fact]
    public async Task LeavesAConflictingFileAloneWhenItIsNotPicked()
    {
        var session = await StartedAsync();

        await session.InstallAsync(Into, only: new HashSet<AssetId> { Id("scripts/spin.js"), Id("shots/a.png") });

        Assert.Equal(InstallStage.Done, session.Stage);
        Assert.Equal("my own crate", File.ReadAllText(InProject($"{Into}/models/crate.obj")));
        Assert.Equal("spin();", File.ReadAllText(InProject($"{Into}/scripts/spin.js")));
        var locked = ReadLock().Find("brass-lamp", "crates")!;
        Assert.Equal(Into, locked.Into);
        Assert.Equal(_fixture.Hash, locked.Hash);
        Assert.Equal(
            new[] { $"{Into}/scripts/spin.js", $"{Into}/shots/a.png" },
            locked.Files.Keys.Select(file => file.ToString()));
        // The lock is renamed into place, so no file of the write is left behind.
        Assert.Equal(
            new[] { $"{Into}/models/crate.obj", $"{Into}/scripts/spin.js", $"{Into}/shots/a.png", KitRules.LockName },
            Held());
    }

    [Fact]
    public async Task ReplacesAConflictingFileOnlyWhenThePersonAgreed()
    {
        var session = await StartedAsync();

        await session.InstallAsync(Into);

        Assert.Equal(InstallStage.Choosing, session.Stage);
        Assert.Contains("models/crate.obj", session.Problem, StringComparison.Ordinal);
        Assert.False(session.Failed);
        Assert.Equal(new[] { $"{Into}/models/crate.obj" }, Held());

        await session.InstallAsync(Into, replacing: new HashSet<AssetId> { Id("models/crate.obj") });

        Assert.Equal(InstallStage.Done, session.Stage);
        Assert.Equal("a crate", File.ReadAllText(InProject($"{Into}/models/crate.obj")));
        Assert.Contains(KitRules.LockName, Held());
    }

    [Fact]
    public async Task RefusesAFolderAKitMayNotGoIntoAndWritesNothing()
    {
        var session = await StartedAsync();

        await session.InstallAsync(".git/hooks");

        Assert.Equal(InstallStage.Choosing, session.Stage);
        Assert.Contains(".git", session.Problem, StringComparison.Ordinal);
        Assert.Equal(new[] { $"{Into}/models/crate.obj" }, Held());
    }

    [Fact]
    public async Task WritesNothingWhenTheLockCannotBeRead()
    {
        File.WriteAllText(InProject(KitRules.LockName), "{not json");
        var session = await StartedAsync();

        await session.InstallAsync("assets/kits/other");

        Assert.Equal(InstallStage.Choosing, session.Stage);
        Assert.NotNull(session.Problem);
        Assert.Equal(new[] { $"{Into}/models/crate.obj", KitRules.LockName }, Held());
    }

    [Fact]
    public async Task GivesTheDialogBackWhenTheInstallThrows()
    {
        var session = await StartedAsync();

        await session.InstallAsync(Into, only: new HashSet<AssetId> { Id("nope.txt") });

        Assert.Equal(InstallStage.Choosing, session.Stage);
        Assert.Contains("nope.txt", session.Problem, StringComparison.Ordinal);
        Assert.Equal(new[] { $"{Into}/models/crate.obj" }, Held());
    }

    [Fact]
    public async Task WritesNoFileWhenTheLockHasNoRoomForTheKit()
    {
        var full = new KitLock(
        [
            .. Enumerable.Range(0, 500).Select(i => new LockedKit(
                publisher: "filler",
                slug: $"k{i}",
                version: "1.0.0",
                hash: _fixture.Hash,
                files: new Dictionary<AssetId, ContentHash>())),
        ]);
        File.WriteAllBytes(InProject(KitRules.LockName), full.Encode());
        var session = await StartedAsync();

        await session.InstallAsync(Into, replacing: new HashSet<AssetId> { Id("models/crate.obj") });

        Assert.Equal(InstallStage.Choosing, session.Stage);
        Assert.Contains("500 kits", session.Problem, StringComparison.Ordinal);
        Assert.Equal(new[] { $"{Into}/models/crate.obj", KitRules.LockName }, Held());
        Assert.Equal("my own crate", File.ReadAllText(InProject($"{Into}/models/crate.obj")));
    }

    [Fact]
    public async Task AddsToTheFilesListedWhenItInstallsIntoAFolderAgain()
    {
        var session = await StartedAsync();

        await session.InstallAsync(Into, only: new HashSet<AssetId> { Id("scripts/spin.js") });
        await session.InstallAsync(Into, only: new HashSet<AssetId> { Id("shots/a.png") });

        Assert.Equal(
            new[] { $"{Into}/scripts/spin.js", $"{Into}/shots/a.png" },
            Listed().Order(StringComparer.Ordinal));

        await session.InstallAsync("assets/other", only: new HashSet<AssetId> { Id("shots/a.png") });

        Assert.Equal(new[] { "assets/other/shots/a.png" }, Listed());
    }

    [Fact]
    public async Task TakesUpACutDownloadWhereItStopped()
    {
        var half = _fixture.Pack.Length / 2;
        _fixture.Pages[StoreFixture.CrateUrl] = new TransportPage(_fixture.Pack, stopAfter: half);

        var session = await StartedAsync();

        Assert.Equal(InstallStage.Choosing, session.Stage);
        var asked = _transport.Sent.Where(request => request.Url.Equals(StoreFixture.CrateUrl)).ToList();
        Assert.Equal(2, asked.Count);
        Assert.Equal(half, asked[^1].From);
        Assert.Equal(_fixture.Pack.Length, session.Progress!.Received);
    }

    [Fact]
    public async Task RefusesAnotherPackThanTheOneListedWithBothHashes()
    {
        // A file renamed in the pack's directory, so it is still a pack but not the one the entry hashed.
        var bad = _fixture.Pack.ToArray();
        var at = (int)BinaryPrimitives.ReadUInt32LittleEndian(bad.AsSpan(8));
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(bad.AsSpan(16));
        var text = Encoding.UTF8.GetString(bad, at, length);
        var renamed = text.IndexOf(".obj", StringComparison.Ordinal);
        Encoding.UTF8.GetBytes(text[..renamed] + ".obx" + text[(renamed + 4)..]).CopyTo(bad, at);
        _fixture.Pages[StoreFixture.CrateUrl] = new TransportPage(bad);

        var session = await StartedAsync();

        Assert.True(session.Failed);
        Assert.Matches(@"Expected \S+, got \S+", session.Problem);
        AssertNothingWritten();
    }

    [Fact]
    public async Task RefusesAPackWithADamagedFileInIt()
    {
        // The first byte after the header belongs to the first file.
        var bad = _fixture.Pack.ToArray();
        bad[32] ^= 0xff;
        _fixture.Pages[StoreFixture.CrateUrl] = new TransportPage(bad);

        var session = await StartedAsync();

        Assert.True(session.Failed);
        Assert.Contains("do not match the hashes", session.Problem, StringComparison.Ordinal);
        AssertNothingWritten();
    }

    [Fact]
    public async Task RefusesAPackThatIsNotTheReleaseListed()
    {
        var (row, release) = await _fixture.ListingAsync();
        var other = new KitRow(
            new KitManifest(
                slug: "barrels",
                publisher: row.Manifest.Publisher,
                name: "Barrels",
                summary: "Some barrels.",
                version: "1.0.0",
                licence: row.Manifest.Licence,
                engine: row.Manifest.Engine,
                containsScripts: false),
            row.Updated);
        var session = Session(other, release);

        await session.StartAsync();

        Assert.True(session.Failed);
        Assert.Contains("barrels", session.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WritesTheLockTheDartEditorWritesForTheSameKit()
    {
        var project = Path.Combine(_scratch.FullName, "parity");
        Directory.CreateDirectory(project);
        await using var bytes = new FilePackBytes(Path.Combine(_scratch.FullName, "crates.opack"));
        var kit = await Kit.OpenAsync(await AssetPack.OpenAsync(bytes));

        // The two steps tool/parity/kits_lock_test.dart takes.
        await KitInstall.InstallAsync(kit, project, Into, only: new HashSet<AssetId> { Id("scripts/spin.js") });
        await KitInstall.InstallAsync(
            kit, project, Into, only: new HashSet<AssetId> { Id("models/crate.obj"), Id("shots/a.png") });

        var dart = Path.Combine(AppContext.BaseDirectory, "Fixtures", "dart", "kits.lock");
        Assert.Equal(File.ReadAllBytes(dart), File.ReadAllBytes(Path.Combine(project, KitRules.LockName)));
    }

    private static AssetId Id(string name) => AssetId.Parse(name);

    private string InProject(string name) => Path.Combine(Project, name);

    /// <summary>Every file in the project, as a path under it.</summary>
    private List<string> Held() =>
        [
            .. Directory.EnumerateFiles(Project, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(Project, file).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal),
        ];

    /// <summary>Nothing reached the project, and no pack was filed as downloaded.</summary>
    private void AssertNothingWritten()
    {
        Assert.Equal(new[] { $"{Into}/models/crate.obj" }, Held());
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(DownloadsFolder), file => Path.GetExtension(file) == ".opack");
    }

    private KitLock ReadLock() => KitLock.Parse(File.ReadAllBytes(InProject(KitRules.LockName)));

    private List<string> Listed() =>
        [.. ReadLock().Find("brass-lamp", "crates")!.Files.Keys.Select(file => file.ToString())];

    private InstallSession Session(KitRow row, KitRelease release)
    {
        _transport = _fixture.Transport();
        var session = new InstallSession(
            row: row,
            release: release,
            projectDirectory: Project,
            downloads: DownloadsFolder,
            transport: _transport,
            backoff: TimeSpan.Zero);
        _sessions.Add(session);
        return session;
    }

    private async Task<InstallSession> StartedAsync()
    {
        var (row, release) = await _fixture.ListingAsync();
        var session = Session(row, release);
        await session.StartAsync();
        return session;
    }
}
