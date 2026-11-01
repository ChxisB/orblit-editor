namespace Orblit.Editor.Core;

using System.Text.RegularExpressions;
using Orblit.Asset;

/// <summary>
/// Where the file list of the chosen release stands. It is read only when the person asks, because the kit
/// file may sit on another host than the store.
/// </summary>
public enum FileListState
{
    Unread,
    Reading,
    Read,
    Unavailable,
}

/// <summary>
/// What the store dialog shows and asks for: the kits of one store, the one a person is looking at and the
/// words and picks that narrow the list.
/// </summary>
/// <remarks>
/// Nothing is fetched until <see cref="GoOnlineAsync"/>, so opening the dialog sends nothing. A kit file is
/// not touched until <see cref="LoadFilesAsync"/>. A reply that arrives after the person moved on is dropped.
/// A store is a server somebody else runs, so whatever goes wrong reading it is said, not thrown.
/// </remarks>
public sealed partial class StoreSession : IDisposable
{
    private readonly Func<IAssetTransport> _newTransport;
    private readonly Dictionary<string, Task<byte[]?>> _images = [];
    private readonly HashSet<string> _failedImages = [];
    private IAssetTransport? _transport;
    private KitStoreClient? _client;
    private bool _disposed;

    /// <param name="engine">The engine version the project uses, or text that is not a version when it is not known.</param>
    public StoreSession(StoreSettings settings, string engine, Func<IAssetTransport> transport)
    {
        Settings = settings;
        Engine = engine;
        _newTransport = transport;
    }

    /// <summary>Raised when anything the dialog shows changes. Never raised after <see cref="Dispose"/>.</summary>
    public event Action? Changed;

    public StoreSettings Settings { get; }

    public string Engine { get; }

    public WebUrl Repository { get; private set; } = StoreSettings.DefaultStoreAddress;

    public bool Online => _client is not null;

    public bool Loading { get; private set; }

    public StoreQuery Query { get; private set; } = new();

    public KitRow? Selected { get; private set; }

    public KitReleases? Releases { get; private set; }

    public PublishedRelease? Chosen { get; private set; }

    public FileListState FilesState { get; private set; }

    /// <summary>The files of the chosen release once <see cref="FilesState"/> is read, or null.</summary>
    public IReadOnlyDictionary<AssetId, long>? Files { get; private set; }

    /// <summary>What went wrong with the last thing asked of the store, or null.</summary>
    public string? Problem { get; private set; }

    public IReadOnlyList<KitRow> Rows { get; private set; } = [];

    /// <summary>The rows that pass the query.</summary>
    public IReadOnlyList<KitRow> Shown => Query.Apply(Rows, Engine, Settings.ShowIncompatible);

    /// <summary>Whether <see cref="Engine"/> is a version, so that <see cref="Fits"/> is an answer and not a guess.</summary>
    // shortcut: the engine's own rule also bounds each number to six digits and refuses a leading zero, so
    // such a text reads as known here. Make that check public when it matters.
    public bool EngineKnown => VersionPattern().IsMatch(Engine);

    public bool Fits(KitRow row) => row.Manifest.Engine.Fits(Engine);

    public void SetQuery(StoreQuery query)
    {
        Query = query;
        Tell();
    }

    public void SetShowIncompatible(bool value)
    {
        Settings.SetShowIncompatible(value);
        Tell();
    }

    /// <summary>Starts reading the store. Later calls read it again.</summary>
    public async Task GoOnlineAsync()
    {
        _transport?.Dispose();
        var transport = _transport = _newTransport();
        var client = _client = new KitStoreClient(Repository, transport);
        Rows = [];
        ForgetKit();
        _images.Clear();
        _failedImages.Clear();
        Problem = null;
        Loading = true;
        Tell();
        try
        {
            var index = await client.IndexAsync();
            if (Stale(client))
            {
                return;
            }

            Rows = index.Rows;
        }
        catch (Exception error)
        {
            if (Stale(client))
            {
                return;
            }

            Problem = error.Message;
        }

        Loading = false;
        Tell();
    }

    /// <summary>Reads <paramref name="address"/> instead, when the session is online.</summary>
    public async Task UseRepositoryAsync(WebUrl address)
    {
        Repository = address;
        Tell();
        if (Online)
        {
            await GoOnlineAsync();
        }
    }

    /// <summary>
    /// Lists the store at <paramref name="text"/> in the settings and reads it. Throws an
    /// <see cref="ArgumentException"/> when <see cref="StoreSettings.RepositoryProblem"/> has something to say.
    /// </summary>
    public Task AddRepositoryAsync(string text)
    {
        Settings.AddRepository(text);
        return UseRepositoryAsync(Settings.Repositories[^1]);
    }

    /// <summary>Forgets a store a person added, and goes back to the default one when it was the one being read.</summary>
    public async Task RemoveRepositoryAsync(WebUrl address)
    {
        Settings.RemoveRepository(address);
        if (Repository.Equals(address))
        {
            await UseRepositoryAsync(StoreSettings.DefaultStoreAddress);
        }
    }

    public async Task SelectAsync(KitRow? row)
    {
        ForgetKit();
        RetryFailedImages();
        Selected = row;
        Problem = null;
        Tell();
        if (row is null || _client is not { } client)
        {
            return;
        }

        KitReleases releases;
        try
        {
            releases = await client.ReleasesAsync(row);
        }
        catch (Exception error)
        {
            if (!Stale(client) && Selected == row)
            {
                Problem = error.Message;
                Tell();
            }

            return;
        }

        if (Stale(client) || Selected != row)
        {
            return;
        }

        Releases = releases;
        ChooseRelease(releases.Releases[0]);
    }

    /// <summary>Makes <paramref name="release"/> the one shown. Its file list stays unread.</summary>
    public void ChooseRelease(PublishedRelease release)
    {
        Chosen = release;
        Files = null;
        FilesState = FileListState.Unread;
        Tell();
    }

    /// <summary>Reads the file list of the chosen release from the host of its kit file.</summary>
    /// <remarks>
    /// A server that does not answer a range leaves the list unavailable and the install still works,
    /// because the whole file is then downloaded.
    /// </remarks>
    public async Task LoadFilesAsync()
    {
        if (_client is not { } client || Chosen is not { } release || FilesState == FileListState.Reading)
        {
            return;
        }

        FilesState = FileListState.Reading;
        Tell();
        IReadOnlyDictionary<AssetId, long>? files = null;
        try
        {
            files = (await client.OpenAsync(release.Release)).Files;
        }
        catch (Exception)
        {
            // The list is a convenience. The install reads the whole file anyway.
        }

        if (Stale(client) || Chosen != release)
        {
            return;
        }

        Files = files;
        FilesState = files is null ? FileListState.Unavailable : FileListState.Read;
        Tell();
    }

    /// <summary>The card image of <paramref name="row"/>, or null when it has none or cannot be read.</summary>
    public Task<byte[]?> ThumbnailAsync(KitRow row) =>
        row.Thumbnail is { } path && _client is { } client
            ? CachedImageAsync(path, () => client.ThumbnailAsync(row))
            : Task.FromResult<byte[]?>(null);

    /// <summary>The preview image <paramref name="name"/> of <see cref="Selected"/>, or null when it cannot be read.</summary>
    public Task<byte[]?> PreviewAsync(string name)
    {
        if (Selected is not { } row || _client is not { } client)
        {
            return Task.FromResult<byte[]?>(null);
        }

        // The same key as a thumbnail has, so an image is read once.
        return CachedImageAsync(
            $"kits/{row.Manifest.Publisher}/{row.Manifest.Slug}/{name}",
            async () => await client.PreviewAsync(row, name));
    }

    public void Dispose()
    {
        _disposed = true;
        _transport?.Dispose();
    }

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+\z")]
    private static partial Regex VersionPattern();

    private Task<byte[]?> CachedImageAsync(string key, Func<Task<byte[]?>> read)
    {
        if (!_images.TryGetValue(key, out var image))
        {
            image = ReadImageAsync(key, read);
            _images[key] = image;
        }

        return image;
    }

    private async Task<byte[]?> ReadImageAsync(string key, Func<Task<byte[]?>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception)
        {
            // A picture that cannot be read leaves its card blank.
            _failedImages.Add(key);
            return null;
        }
    }

    // A picture that failed is read again when the person moves between the list and a kit, not on every
    // render of the grid.
    private void RetryFailedImages()
    {
        foreach (var key in _failedImages)
        {
            _images.Remove(key);
        }

        _failedImages.Clear();
    }

    private void ForgetKit()
    {
        Selected = null;
        Releases = null;
        Chosen = null;
        Files = null;
        FilesState = FileListState.Unread;
    }

    private bool Stale(KitStoreClient client) => _disposed || _client != client;

    private void Tell()
    {
        if (!_disposed)
        {
            Changed?.Invoke();
        }
    }
}
