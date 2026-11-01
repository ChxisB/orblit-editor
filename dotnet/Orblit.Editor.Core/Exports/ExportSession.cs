namespace Orblit.Editor.Core;

using Orblit.Asset;

/// <summary>What <see cref="ExportSession"/> needs of the disk: a cook to run, a folder to list and a pack to read.</summary>
public interface IExportWork
{
    /// <summary>Cooks and packs each target, telling <paramref name="onTarget"/> as each lands.</summary>
    Task<IReadOnlyList<TargetExport>> RunAsync(ExportRequest request, Action<TargetExport> onTarget);

    Task<ExportListing> ListAsync(string projectDirectory);

    Task<PackContents> ReadAsync(string projectDirectory, string path);
}

/// <summary>The export work done for real, on this machine.</summary>
public sealed class DiskExports : IExportWork
{
    public Task<IReadOnlyList<TargetExport>> RunAsync(ExportRequest request, Action<TargetExport> onTarget) =>
        ExportRun.RunAsync(request, onTarget);

    public Task<ExportListing> ListAsync(string projectDirectory) => OpenedExports.ListAsync(projectDirectory);

    public Task<PackContents> ReadAsync(string projectDirectory, string path) => PackContents.ReadAsync(projectDirectory, path);
}

/// <summary>What one export dialog is showing and what it has been asked to do.</summary>
/// <remarks>
/// Held by the dialog and not by the editor. An export is a thing somebody sets up, runs and reads, and
/// nothing outside the dialog needs to know it is half set up. The cook and the folder are reached through
/// <see cref="IExportWork"/>, so a test never cooks.
/// </remarks>
public sealed class ExportSession : IDisposable
{
    private readonly IExportWork _work;
    private readonly string? _cachePath;
    private readonly HashSet<string> _targets = [CookProject.CurrentCookTarget.Name];
    private bool _disposed;

    /// <param name="cachePath">Where cooked bytes are kept, or null for the machine's own cache.</param>
    public ExportSession(string projectDirectory, IExportWork work, string? cachePath = null)
    {
        ProjectDirectory = projectDirectory;
        _work = work;
        _cachePath = cachePath;
    }

    /// <summary>Raised when anything the dialog shows changes. Never raised after <see cref="Dispose"/>.</summary>
    public event Action? Changed;

    public string ProjectDirectory { get; }

    public ExportListing Listing { get; private set; } = ExportListing.Empty;

    /// <summary>The pack a patch is built on, or null for a whole pack.</summary>
    public ExportedPack? PatchOf { get; private set; }

    public bool Running { get; private set; }

    /// <summary>The target being cooked now, or null between runs.</summary>
    public string? Cooking { get; private set; }

    /// <summary>How each target went, in the order they were asked for.</summary>
    public IReadOnlyList<TargetExport> Results { get; private set; } = [];

    /// <summary>The pack whose contents are on show, or null.</summary>
    public ExportedPack? Shown { get; private set; }

    public PackContents? Contents { get; private set; }

    /// <summary>Why the contents could not be read, or null.</summary>
    public string? ContentsProblem { get; private set; }

    /// <summary>The targets an export would run for.</summary>
    /// <remarks>A patch is for its base's target and no other, so the choice of target is the choice of base.</remarks>
    public IReadOnlyList<string> Targets =>
        PatchOf is { } patchOf ? [patchOf.Target] : [.. CookTargets.Names.Where(_targets.Contains)];

    public bool CanExport => !Running && Targets.Count > 0;

    /// <summary>Reads the exports folder.</summary>
    public async Task LoadAsync()
    {
        Listing = await _work.ListAsync(ProjectDirectory);
        if (PatchOf is { } patchOf && !Listing.Packs.Any(pack => pack.Path == patchOf.Path))
        {
            PatchOf = null;
        }

        Tell();
    }

    public void ToggleTarget(string name)
    {
        if (!_targets.Add(name))
        {
            _targets.Remove(name);
        }

        Tell();
    }

    public void ChoosePatchOf(ExportedPack? patchOf)
    {
        PatchOf = patchOf;
        Tell();
    }

    /// <summary>Cooks and packs each target in turn.</summary>
    public async Task StartAsync()
    {
        if (!CanExport)
        {
            return;
        }

        var request = new ExportRequest
        {
            ProjectDirectory = ProjectDirectory,
            Targets = Targets,
            PatchOf = PatchOf?.Path,
            CachePath = _cachePath,
        };
        Running = true;
        Results = [];
        Cooking = request.Targets[0];
        Tell();
        try
        {
            await _work.RunAsync(request, done => Landed(request.Targets, done));
        }
        finally
        {
            Running = false;
            Cooking = null;
            await LoadAsync();
        }
    }

    private void Landed(IReadOnlyList<string> asked, TargetExport done)
    {
        Results = [.. Results, done];
        Cooking = Results.Count < asked.Count ? asked[Results.Count] : null;
        Tell();
    }

    /// <summary>Reads what <paramref name="pack"/> serves, with the packs under it.</summary>
    public async Task ShowAsync(ExportedPack pack)
    {
        Shown = pack;
        Contents = null;
        ContentsProblem = null;
        Tell();
        PackContents? read = null;
        string? problem = null;
        try
        {
            read = await _work.ReadAsync(ProjectDirectory, pack.Path);
        }
        catch (ExportChainBrokenException error)
        {
            problem = error.Message;
        }

        // A slow read for a pack somebody has moved on from is dropped.
        if (Shown == pack)
        {
            Contents = read;
            ContentsProblem = problem;
        }

        Tell();
    }

    public void Dispose() => _disposed = true;

    // A read that lands after the dialog has closed has nobody to tell.
    private void Tell()
    {
        if (!_disposed)
        {
            Changed?.Invoke();
        }
    }
}
