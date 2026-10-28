namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

public sealed class StoreSessionTests : IAsyncLifetime
{
    private readonly DirectoryInfo _scratch = Directory.CreateTempSubdirectory("orblit_store_session_");
    private readonly List<MapTransport> _made = [];
    private StoreFixture _fixture = null!; // set in InitializeAsync, which runs before every test

    public async ValueTask InitializeAsync() => _fixture = await StoreFixture.BuildAsync(_scratch.FullName);

    public ValueTask DisposeAsync()
    {
        _scratch.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }

    private StoreSession Session(string engine = "0.35.0", Func<IAssetTransport, IAssetTransport>? wrap = null) =>
        new(
            new StoreSettings(Path.Combine(_scratch.FullName, "store.json")),
            engine,
            () =>
            {
                var transport = _fixture.Transport();
                _made.Add(transport);
                return wrap is null ? transport : wrap(transport);
            });

    [Fact]
    public async Task SendsNothingUntilItIsToldToGoOnline()
    {
        using var store = Session();
        Assert.False(store.Online);
        Assert.Empty(_made);

        await store.GoOnlineAsync();

        Assert.True(store.Online);
        Assert.Equal("crates", Assert.Single(store.Shown).Manifest.Slug);
    }

    [Fact]
    public async Task HidesAKitForOtherEnginesUntilAskedToShowIt()
    {
        using var store = Session(engine: "0.50.0");
        await store.GoOnlineAsync();

        Assert.Single(store.Rows);
        Assert.Empty(store.Shown);
        Assert.False(store.Fits(store.Rows[0]));
        store.SetShowIncompatible(true);
        Assert.Single(store.Shown);
        store.SetQuery(new StoreQuery { Text = "nothing like it" });
        Assert.Empty(store.Shown);
    }

    [Fact]
    public async Task ReadsTheFileListOfAChosenKitOnlyWhenAsked()
    {
        using var store = Session();
        await store.GoOnlineAsync();
        await store.SelectAsync(store.Rows[0]);

        var release = Assert.Single(store.Releases!.Releases);
        Assert.Equal("1.0.0", release.Release.Version);
        Assert.Same(release, store.Chosen);
        Assert.Equal(FileListState.Unread, store.FilesState);
        Assert.DoesNotContain(Assert.Single(_made).Sent, sent => sent.Url.Equals(StoreFixture.CrateUrl));

        await store.LoadFilesAsync();

        Assert.Equal(FileListState.Read, store.FilesState);
        Assert.Equal(
            new[] { "models/crate.obj", "scripts/spin.js", "shots/a.png" },
            store.Files!.Keys.Select(id => id.ToString()));
        Assert.True(store.Chosen!.ContainsScripts);
    }

    [Fact]
    public async Task ListsNoFilesWhenTheServerCannotAnswerARange()
    {
        using var store = Session(wrap: inner => new NoRanges(inner));
        await store.GoOnlineAsync();
        await store.SelectAsync(store.Rows[0]);

        await store.LoadFilesAsync();

        Assert.NotNull(store.Chosen);
        Assert.Equal(FileListState.Unavailable, store.FilesState);
        Assert.Null(store.Files);
        Assert.Null(store.Problem);
    }

    [Fact]
    public async Task DropsAReplyForAKitThePersonHasLeft()
    {
        Held? held = null;
        using var store = Session(wrap: inner => held = new Held(inner));
        await store.GoOnlineAsync();
        held!.Holding = true;

        var pending = store.SelectAsync(store.Rows[0]);
        await store.SelectAsync(null);
        held.Open();
        await pending;

        Assert.Null(store.Selected);
        Assert.Null(store.Releases);
    }

    [Fact]
    public async Task ReadsAnImageOnceAndGivesNullForOneItCannotRead()
    {
        using var store = Session();
        await store.GoOnlineAsync();
        var row = store.Rows[0];

        Assert.Same(store.ThumbnailAsync(row), store.ThumbnailAsync(row));
        Assert.Equal(StoreFixture.PngBytes, await store.ThumbnailAsync(row));
        await store.SelectAsync(row);
        Assert.Equal(StoreFixture.PngBytes, await store.PreviewAsync("preview-0.png"));
        Assert.Null(await store.PreviewAsync("preview-1.png"));
    }

    [Fact]
    public async Task ReadsAFailedPictureAgainWhenThePersonMovesOn()
    {
        using var store = Session();
        await store.GoOnlineAsync();
        var row = store.Rows[0];
        var picture = StoreFixture.StoreBase.Resolve(WebUrl.Parse(row.Thumbnail!));
        Assert.True(_fixture.Pages.Remove(picture, out var page));
        Assert.Null(await store.ThumbnailAsync(row));
        _fixture.Pages[picture] = page;
        Assert.Null(await store.ThumbnailAsync(row));

        await store.SelectAsync(row);

        Assert.Equal(StoreFixture.PngBytes, await store.ThumbnailAsync(row));
    }

    [Fact]
    public async Task SaysWhatWentWrongWithAStoreThatCannotBeRead()
    {
        _fixture.Pages.Remove(StoreFixture.StoreBase.Resolve(WebUrl.Parse("index.json")));
        using var store = Session();

        await store.GoOnlineAsync();

        Assert.Empty(store.Rows);
        Assert.False(store.Loading);
        Assert.Contains("index.json", store.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadsAnotherStoreWhenItIsAddedAndGoesBackWhenRemoved()
    {
        var other = WebUrl.Parse("https://other.example/store/");
        foreach (var (url, page) in _fixture.Pages.ToList())
        {
            if (url.Host == StoreFixture.StoreBase.Host)
            {
                _fixture.Pages[other.Resolve(WebUrl.Parse(url.Path["/store/".Length..]))] = page;
            }
        }

        using var store = Session();
        await store.GoOnlineAsync();
        await store.AddRepositoryAsync("https://other.example/store");

        Assert.Equal(other, store.Repository);
        Assert.Single(store.Rows);
        Assert.Contains(other, store.Settings.Repositories);

        await store.RemoveRepositoryAsync(other);

        Assert.Equal(StoreSettings.DefaultStoreAddress, store.Repository);
        Assert.Equal(new[] { StoreSettings.DefaultStoreAddress }, store.Settings.Repositories);
    }

    /// <summary>A server that does not understand a range: it sends the whole file.</summary>
    private sealed class NoRanges(IAssetTransport inner) : IAssetTransport
    {
        public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
            inner.SendAsync(new FetchRequest(request.Url), cancellation);

        public void Dispose() => inner.Dispose();
    }

    // A reply held until the test lets it go, so it arrives after the person moved on. A pool thread would
    // race the test for the session.
    private sealed class Held(IAssetTransport inner) : IAssetTransport
    {
        private readonly TaskCompletionSource _open = new();

        public bool Holding { get; set; }

        public void Open() => _open.SetResult();

        public async Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
        {
            if (Holding)
            {
                await _open.Task;
            }

            return await inner.SendAsync(request, cancellation);
        }

        public void Dispose() => inner.Dispose();
    }
}
