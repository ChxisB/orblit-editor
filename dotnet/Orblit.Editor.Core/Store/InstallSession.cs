namespace Orblit.Editor.Core;

using Orblit.Asset;

public enum InstallStage
{
    Downloading,
    Choosing,
    Installing,
    Done,
}

/// <summary>One install of one release: the download, the person's choice of files and folder, and the write.</summary>
/// <remarks>
/// The download is kept in <see cref="Downloads"/> by its hash, so a second try after a cut connection takes up
/// where the first stopped, and a kit already held is not fetched again. Installing never runs a file of the
/// kit. The session closes the transport when it is disposed. <see cref="Changed"/> may be raised on a pool
/// thread while the kit downloads.
/// </remarks>
// shortcut: the downloads folder is never cleaned, and a download cannot be cancelled. A session disposed
// mid-download lets it fail unseen. Prune and cancel when it matters.
public sealed class InstallSession : IDisposable
{
    private readonly IAssetTransport _transport;
    private readonly TimeSpan _backoff;
    private Kit? _kit;
    private bool _disposed;

    /// <param name="downloads">The folder downloaded kits are kept in.</param>
    /// <param name="backoff">The wait before the first new try of a download, doubled after each. One second when null.</param>
    // F2: six parameters, all named at each call.
    public InstallSession(
        KitRow row,
        KitRelease release,
        string projectDirectory,
        string downloads,
        IAssetTransport transport,
        TimeSpan? backoff = null)
    {
        Row = row;
        Release = release;
        ProjectDirectory = projectDirectory;
        Downloads = downloads;
        _transport = transport;
        _backoff = backoff ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>Raised when anything the dialog shows changes. Never raised after <see cref="Dispose"/>.</summary>
    public event Action? Changed;

    public KitRow Row { get; }

    public KitRelease Release { get; }

    public string ProjectDirectory { get; }

    public string Downloads { get; }

    public InstallStage Stage { get; private set; }

    /// <summary>How much of the download has arrived, or null before the first bytes.</summary>
    public FetchProgress? Progress { get; private set; }

    /// <summary>The kit once it is downloaded and checked, or null.</summary>
    public Kit? Kit => _kit;

    /// <summary>What the install wrote, once <see cref="Stage"/> is done.</summary>
    public LockedKit? Locked { get; private set; }

    /// <summary>What went wrong, or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>Whether the download or the check failed, so there is nothing to choose from.</summary>
    public bool Failed => Problem is not null && _kit is null;

    private Kit Opened => _kit ?? throw new InvalidOperationException("The kit is not downloaded and checked yet.");

    /// <summary>Downloads the kit and checks it, leaving the person to choose.</summary>
    public async Task StartAsync()
    {
        Problem = null;
        Progress = null;
        Tell();
        try
        {
            var pack = await new PackFolder(Downloads).DownloadAsync(
                Release.Offer,
                _transport,
                Release.Origin,
                backoff: _backoff,
                onProgress: progress =>
                {
                    Progress = progress;
                    Tell();
                });
            var kit = await Kit.OpenAsync(pack);
            CheckIsTheRelease(kit.Manifest);
            var problems = await kit.CheckAsync();
            if (problems.Count > 0)
            {
                throw new FormatException(string.Join('\n', problems));
            }

            _kit = kit;
            Stage = InstallStage.Choosing;
        }
        catch (Exception error)
        {
            // A kit comes from a server somebody else runs, so whatever goes wrong is said, not thrown.
            Problem = error.Message;
        }

        Tell();
    }

    /// <summary>The files <paramref name="only"/> would replace under <paramref name="into"/> in the project.</summary>
    public Task<IReadOnlyList<AssetId>> ConflictsAsync(string into = "", IReadOnlySet<AssetId>? only = null) =>
        Opened.ConflictsInAsync(new DirectoryAssetSource(ProjectDirectory), into, only);

    /// <summary>Writes the kit. A failure leaves the person where they were, with <see cref="Problem"/> saying why.</summary>
    public async Task InstallAsync(string into = "", IReadOnlySet<AssetId>? only = null, IReadOnlySet<AssetId>? replacing = null)
    {
        Problem = null;
        Stage = InstallStage.Installing;
        Tell();
        try
        {
            var kit = Opened;
            var misplaced = KitRules.PlaceProblem(into, kit.Chosen(only));
            if (misplaced is not null)
            {
                throw new FormatException(misplaced);
            }

            Locked = await KitInstall.InstallAsync(kit, ProjectDirectory, into, only, replacing);
            Stage = InstallStage.Done;
        }
        catch (Exception error)
        {
            // Any failure must give the dialog back: it cannot be closed while the stage is installing.
            Problem = error.Message;
            Stage = InstallStage.Choosing;
        }

        Tell();
    }

    public void Dispose()
    {
        _disposed = true;
        _transport.Dispose();
    }

    // A pack whose manifest names another kit than the one listed was not the one the store promised,
    // whatever its hash.
    private void CheckIsTheRelease(KitManifest manifest)
    {
        var listed = Row.Manifest;
        if (manifest.Publisher != listed.Publisher || manifest.Slug != listed.Slug || manifest.Version != Release.Version)
        {
            throw new FormatException(
                $"The file is {manifest.Publisher}/{manifest.Slug} {manifest.Version}, "
                + $"not {listed.Publisher}/{listed.Slug} {Release.Version}.");
        }
    }

    private void Tell()
    {
        if (!_disposed)
        {
            Changed?.Invoke();
        }
    }
}
