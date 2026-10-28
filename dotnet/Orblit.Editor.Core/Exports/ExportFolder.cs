namespace Orblit.Editor.Core;

using Orblit.Asset;
using IoDirectory = System.IO.Directory;
using IoPath = System.IO.Path;

/// <summary>Where a project's exports go, and how a pack is named in a message.</summary>
public static class ExportFolder
{
    /// <summary>The exports folder of the project at <paramref name="projectDirectory"/>.</summary>
    public static string PathOf(string projectDirectory) => IoPath.Join(projectDirectory, "exports");

    /// <summary>A hash short enough for a file name or a message.</summary>
    public static string ShortHash(ContentHash hash) => hash.Hex[..12];
}

/// <summary>One pack in the exports folder, as the dialog lists it.</summary>
public sealed record ExportedPack
{
    public required string Path { get; init; }

    public required string Target { get; init; }

    /// <summary>The pack's hash, in hex.</summary>
    public required string Hash { get; init; }

    /// <summary>The hash of the pack this one was built against, or null for a whole pack.</summary>
    public string? Base { get; init; }

    public required int Assets { get; init; }

    public required int Removed { get; init; }

    public required long Bytes { get; init; }

    public required DateTime Modified { get; init; }

    public bool IsPatch => Base is not null;

    public string FileName => IoPath.GetFileName(Path);
}

/// <summary>What the exports folder holds, and the files in it that are not packs.</summary>
public sealed record ExportListing(IReadOnlyList<ExportedPack> Packs, IReadOnlyList<string> Unreadable)
{
    public static ExportListing Empty { get; } = new([], []);
}

/// <summary>A patch whose chain of bases cannot be followed through the exports folder.</summary>
public sealed class ExportChainBrokenException(string message) : Exception(message);

/// <summary>Every pack in a project's exports folder, open for reading.</summary>
/// <remarks>Only the directory of each pack is read. The blobs stay on disk.</remarks>
public sealed class OpenedExports : IAsyncDisposable
{
    private readonly IReadOnlyList<(string Path, AssetPack Pack, FilePackBytes Bytes)> _open;

    private OpenedExports(IReadOnlyList<(string, AssetPack, FilePackBytes)> open, IReadOnlyList<string> unreadable)
    {
        _open = open;
        Unreadable = unreadable;
    }

    /// <summary>Each file that ends in .opack and would not open, with what was wrong.</summary>
    public IReadOnlyList<string> Unreadable { get; }

    public IEnumerable<AssetPack> Packs => _open.Select(one => one.Pack);

    /// <summary>Opens each .opack file directly inside the exports folder, in order of path.</summary>
    public static async Task<OpenedExports> OpenAsync(string projectDirectory)
    {
        var folder = ExportFolder.PathOf(projectDirectory);
        List<(string, AssetPack, FilePackBytes)> open = [];
        List<string> unreadable = [];
        if (!IoDirectory.Exists(folder))
        {
            return new OpenedExports(open, unreadable);
        }

        var files = new DirectoryInfo(folder).EnumerateFiles("*.opack")
            .Where(file => file.LinkTarget is null)
            .Select(file => file.FullName)
            .Order(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var bytes = new FilePackBytes(file);
            try
            {
                open.Add((file, await AssetPack.OpenAsync(bytes).ConfigureAwait(false), bytes));
            }
            catch (Exception error)
            {
                // A file named .opack can be anything somebody put there. It is listed as unreadable, and the rest still open.
                await bytes.DisposeAsync().ConfigureAwait(false);
                unreadable.Add($"{IoPath.GetFileName(file)}: {error.Message}");
            }
        }

        return new OpenedExports(open, unreadable);
    }

    /// <summary>Opens the folder, lists it and closes it again.</summary>
    public static async Task<ExportListing> ListAsync(string projectDirectory)
    {
        await using var exports = await OpenAsync(projectDirectory).ConfigureAwait(false);
        return exports.Listing();
    }

    /// <summary>The pack at <paramref name="path"/>, or null when none of the open ones is there.</summary>
    public AssetPack? At(string path)
    {
        var full = IoPath.GetFullPath(path);
        return _open.FirstOrDefault(one => string.Equals(IoPath.GetFullPath(one.Path), full, StringComparison.Ordinal)).Pack;
    }

    /// <summary>The path <paramref name="pack"/> was opened from.</summary>
    public string PathOf(AssetPack pack) => _open.First(one => ReferenceEquals(one.Pack, pack)).Path;

    /// <summary>The packs <paramref name="top"/> stands on, the whole pack first and <paramref name="top"/> last.</summary>
    public IReadOnlyList<AssetPack> ChainTo(AssetPack top)
    {
        // A pack that comes back to an earlier state is in the folder twice, set aside and current. Both
        // hold the same assets, so either will do.
        var byHash = _open.DistinctBy(one => one.Pack.Hash).ToDictionary(one => one.Pack.Hash, one => one.Pack);
        List<AssetPack> chain = [top];
        var next = top;
        while (next.Base is { } under)
        {
            next = byHash.GetValueOrDefault(under) ?? throw new ExportChainBrokenException(
                $"{IoPath.GetFileName(PathOf(next))} was built on {ExportFolder.ShortHash(under)}, and no pack in the exports folder is that one.");
            chain.Insert(0, next);
        }

        return chain;
    }

    /// <summary>The open packs, the newest first.</summary>
    public ExportListing Listing()
    {
        var packs = _open
            .Select(one => new ExportedPack
            {
                Path = one.Path,
                Target = one.Pack.Target,
                Hash = one.Pack.Hash.Hex,
                Base = one.Pack.Base?.Hex,
                Assets = one.Pack.Manifest.Entries.Count,
                Removed = one.Pack.Removed.Count,
                Bytes = new FileInfo(one.Path).Length,
                Modified = File.GetLastWriteTimeUtc(one.Path),
            })
            .OrderByDescending(pack => pack.Modified);
        return new ExportListing([.. packs], Unreadable);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var one in _open)
        {
            await one.Bytes.DisposeAsync().ConfigureAwait(false);
        }
    }
}
