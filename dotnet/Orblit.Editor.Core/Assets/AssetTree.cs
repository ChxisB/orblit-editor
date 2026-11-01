namespace Orblit.Editor.Core;

using IoDirectory = System.IO.Directory;
using IoFile = System.IO.File;
using IoPath = System.IO.Path;

/// <summary>A folder under the project and how deep it sits, the top ones at depth 0.</summary>
public readonly record struct FolderAt(string Path, int Depth);

/// <summary>A file or folder the browser made: where it is, or what went wrong.</summary>
public readonly record struct AssetMade(string? Problem, string? Path);

/// <summary>Reads what is actually in a project folder, and says when it changes.</summary>
/// <remarks>
/// Reads are synchronous and on demand: a folder is small and a build that blocks for a moment
/// beats a list that is a frame behind. <see cref="Changed"/> is what tells the browser a read is
/// worth doing again.
/// <para>
/// A link is not listed. A project that keeps a folder of assets elsewhere and links it in will not see it
/// here.
/// </para>
/// </remarks>
public sealed class AssetTree : IDisposable
{
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(180);

    private readonly object _gate = new();
    private readonly Timer _settle;
    private Action? _changed;
    private FileSystemWatcher? _watch;
    private bool _disposed;

    /// <param name="root">The project directory, which is the only place the browser will look.</param>
    public AssetTree(string root)
    {
        Root = root;
        _settle = new Timer(Settled, null, Timeout.Infinite, Timeout.Infinite);
    }

    public string Root { get; }

    /// <summary>
    /// Fires after the project folder changes, whoever changed it. The watch is open while somebody
    /// is listening.
    /// </summary>
    /// <remarks>
    /// Coalesced: saving one file from another program can produce several events, and a copy of a
    /// hundred assets produces hundreds. Rebuilding the browser for each one would make the editor
    /// stutter during exactly the operation somebody most wants to watch.
    /// <para>
    /// Raised on a thread of its own. A watch that cannot be had is not an error: a browser that
    /// cannot watch is still a browser, and the refresh button is the fallback.
    /// </para>
    /// </remarks>
    public event Action Changed
    {
        add
        {
            lock (_gate)
            {
                _changed += value;
                if (!_disposed)
                {
                    _watch ??= OpenWatch();
                }
            }
        }
        remove
        {
            lock (_gate)
            {
                _changed -= value;
                if (_changed is null)
                {
                    StopWatch();
                }
            }
        }
    }

    /// <summary>Stops watching. Call when the browser goes away.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _changed = null;
            StopWatch();
            _settle.Dispose();
        }
    }

    /// <summary>
    /// What is directly inside <paramref name="directory"/>, folders first and then by name.
    /// </summary>
    /// <remarks>
    /// Nothing rather than an exception for a folder that has gone: a project browser open on a
    /// folder somebody deleted in Finder should show an empty list, not take the editor down.
    /// </remarks>
    public static IReadOnlyList<Asset> Read(string directory)
    {
        if (!IoDirectory.Exists(directory))
        {
            return [];
        }

        try
        {
            return
            [
                .. new DirectoryInfo(directory)
                    .EnumerateFileSystemInfos()
                    // Dotfiles are the tool's business, not the artist's.
                    .Where(entry => !entry.Name.StartsWith('.') && entry.LinkTarget is null)
                    .Select(entry => Describing(directory, entry))
                    .OrderBy(asset => !asset.IsFolder)
                    .ThenBy(asset => asset.Name.ToLowerInvariant(), StringComparer.Ordinal),
            ];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // An unreadable folder: an empty list is the truthful answer and the browser stays usable.
            return [];
        }
    }

    /// <summary>Every folder under the root, for the tree on the left.</summary>
    public IReadOnlyList<FolderAt> Folders(int maximumDepth = 8)
    {
        var found = new List<FolderAt>();
        Walk(Root, 0);
        return found;

        void Walk(string directory, int depth)
        {
            if (depth > maximumDepth)
            {
                return;
            }

            foreach (var folder in Read(directory).Where(asset => asset.IsFolder))
            {
                found.Add(new FolderAt(folder.Path, depth));
                Walk(folder.Path, depth + 1);
            }
        }
    }

    /// <summary>The project paths of every file under the project that <paramref name="test"/> takes.</summary>
    public IReadOnlyList<string> Find(Func<Asset, bool> test) =>
    [
        .. Folders()
            .Select(folder => folder.Path)
            .Prepend(Root)
            .SelectMany(Read)
            .Where(asset => !asset.IsFolder && test(asset))
            .Select(asset => Relative(asset.Path)),
    ];

    /// <summary>
    /// Where a path sits relative to the project, for the breadcrumb and for the asset references
    /// written into scene files, which is why this always answers with <c>/</c> whatever the host's
    /// own separator is.
    /// </summary>
    /// <remarks>
    /// A scene file is meant to open on whichever platform somebody has the project on. Windows
    /// reads a forward slash in a path perfectly well, but macOS and Linux do not read a backslash
    /// as a separator at all, only as an ordinary character in a single, wrong, file name.
    /// <para>
    /// Anything outside the project comes back as its own path rather than a string of <c>..</c>
    /// segments, which would be both ugly and a sign of a bug.
    /// </para>
    /// </remarks>
    public string Relative(string path)
    {
        if (SamePath(path, Root))
        {
            return "";
        }

        return IsWithin(Root, path) ? IoPath.GetRelativePath(Root, path).Replace(IoPath.DirectorySeparatorChar, '/') : path;
    }

    /// <summary>
    /// Deletes a file or a folder and everything in it. Returns what went wrong, or null. A deletion
    /// that fails silently is how somebody comes to believe a file is gone when it is not.
    /// </summary>
    public static string? Delete(string path)
    {
        try
        {
            if (IoDirectory.Exists(path))
            {
                IoDirectory.Delete(path, recursive: true);
            }
            else if (IoFile.Exists(path))
            {
                IoFile.Delete(path);
            }
            else
            {
                return "It is not there any more.";
            }

            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    /// <summary>
    /// Renames a file or folder within its own directory. Refuses to overwrite something that is
    /// already there, because a rename that silently replaces another file destroys work nobody
    /// asked about.
    /// </summary>
    /// <returns>What went wrong, or null.</returns>
    public static string? Rename(string path, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "A name cannot be empty.";
        }

        if (HasSeparator(trimmed) || trimmed is "." or "..")
        {
            return "A name cannot contain a path.";
        }

        var target = IoPath.Join(IoPath.GetDirectoryName(path), trimmed);
        if (SamePath(target, path))
        {
            return null;
        }

        // Case-only renames look like a collision on a case-insensitive disk, and are the one case
        // where the destination existing is fine.
        var sameFileDifferentCase = string.Equals(target, path, StringComparison.OrdinalIgnoreCase);
        if (!sameFileDifferentCase && Exists(target))
        {
            return $"There is already something called \"{trimmed}\" here.";
        }

        return Moved(path, target);
    }

    /// <summary>A path inside <paramref name="directory"/> that nothing is using yet, numbered after the name if it is taken.</summary>
    public static string Available(string directory, string name)
    {
        var stem = AssetNames.Stem(name);
        var extension = AssetNames.Extension(name);

        var candidate = IoPath.Join(directory, name);
        for (var i = 2; Exists(candidate); i++)
        {
            candidate = IoPath.Join(directory, $"{stem} {i}{extension}");
        }

        return candidate;
    }

    /// <summary>
    /// One entry, for a path already known to exist. For the caller that has just made a file and
    /// wants to hand it on without reading the whole folder again to find it.
    /// </summary>
    public static Asset Describe(string path)
    {
        var folder = IoDirectory.Exists(path);
        return new Asset(
            AssetNames.NameOf(path),
            path,
            folder ? AssetKind.Folder : AssetKind.Of(path),
            folder ? null : SizeOf(new FileInfo(path)));
    }

    /// <summary>
    /// Makes one of the things the browser knows how to make. The name is made unique first, so
    /// asking for a second New folder gets one rather than an error.
    /// </summary>
    public AssetMade Create(string directory, NewAsset what, string name)
    {
        if (!Holds(directory))
        {
            return new AssetMade("That folder is outside the project.", null);
        }

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return new AssetMade("A name is needed.", null);
        }

        if (HasSeparator(trimmed) || trimmed.Contains("..", StringComparison.Ordinal))
        {
            return new AssetMade("A name cannot be a path.", null);
        }

        var path = Available(directory, what.IsFolder ? trimmed : trimmed + what.Extension);
        try
        {
            Put(directory, what, path);
            return new AssetMade(null, path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new AssetMade(error.Message, null);
        }
    }

    /// <summary>
    /// Writes a file the editor has made itself, a prefab most of the time. Separate from
    /// <see cref="Create"/> because the contents are not a starter to be edited afterwards: they are
    /// the thing.
    /// </summary>
    public AssetMade Write(string directory, string name, string contents)
    {
        if (!Holds(directory))
        {
            return new AssetMade("That folder is outside the project.", null);
        }

        // The name comes from an object's own, and a scene file can hold any text.
        if (HasSeparator(name) || name is "." or "..")
        {
            return new AssetMade("A name cannot be a path.", null);
        }

        var path = Available(directory, name);
        try
        {
            IoFile.WriteAllText(path, contents);
            return new AssetMade(null, path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new AssetMade(error.Message, null);
        }
    }

    private static Asset Describing(string directory, FileSystemInfo entry)
    {
        var path = IoPath.Join(directory, entry.Name);
        return entry is FileInfo file
            ? new Asset(entry.Name, path, AssetKind.Of(path), SizeOf(file))
            : new Asset(entry.Name, path, AssetKind.Folder);
    }

    private static long? SizeOf(FileInfo file)
    {
        try
        {
            return file.Exists ? file.Length : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Written beside it and named after it, so the include in the source resolves without anybody
    // editing either file first. A header that is already there is somebody's work, and stays.
    private static void Put(string directory, NewAsset what, string path)
    {
        if (what.IsFolder)
        {
            IoDirectory.CreateDirectory(path);
            return;
        }

        var stem = AssetNames.Stem(path);
        IoFile.WriteAllText(path, what.Contents(stem));
        if (what.Companion is not { } companion)
        {
            return;
        }

        var beside = IoPath.Join(directory, stem + companion.Extension);
        if (!IoFile.Exists(beside))
        {
            IoFile.WriteAllText(beside, companion.Contents(stem));
        }
    }

    private static string? Moved(string path, string target)
    {
        try
        {
            if (IoDirectory.Exists(path))
            {
                IoDirectory.Move(path, target);
            }
            else
            {
                IoFile.Move(path, target);
            }

            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    private static bool Exists(string path) => IoFile.Exists(path) || IoDirectory.Exists(path);

    // Both separators, because Windows takes a forward slash for one too.
    private static bool HasSeparator(string name) =>
        name.IndexOfAny([IoPath.DirectorySeparatorChar, IoPath.AltDirectorySeparatorChar]) >= 0;

    private static bool SamePath(string a, string b) =>
        string.Equals(
            IoPath.TrimEndingDirectorySeparator(IoPath.GetFullPath(a)),
            IoPath.TrimEndingDirectorySeparator(IoPath.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private bool Holds(string directory) => SamePath(directory, Root) || IsWithin(Root, directory);

    /// <summary>Whether <paramref name="path"/> is somewhere under <paramref name="root"/>, and not the root itself.</summary>
    public static bool IsWithin(string root, string path)
    {
        var relative = IoPath.GetRelativePath(root, path);
        return relative != "."
            && relative != ".."
            && !IoPath.IsPathRooted(relative)
            && !relative.StartsWith(".." + IoPath.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private FileSystemWatcher? OpenWatch()
    {
        try
        {
            var watch = new FileSystemWatcher(Root) { IncludeSubdirectories = true };
            watch.Created += Touched;
            watch.Changed += Touched;
            watch.Deleted += Touched;
            watch.Renamed += Touched;
            watch.Error += Failed;
            watch.EnableRaisingEvents = true;
            return watch;
        }
        catch (Exception error) when (error is ArgumentException or IOException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void StopWatch()
    {
        _watch?.Dispose();
        _watch = null;
        _settle.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void Touched(object sender, FileSystemEventArgs e)
    {
        var before = e is RenamedEventArgs moved ? moved.OldFullPath : e.FullPath;
        if (!Hidden(e.FullPath) || !Hidden(before))
        {
            Settling();
        }
    }

    // Read lists no dotfile, so a write under .git or .orblit changes nothing the browser shows.
    private bool Hidden(string path) =>
        IoPath.GetRelativePath(Root, path)
            .Split(IoPath.DirectorySeparatorChar, IoPath.AltDirectorySeparatorChar)
            .Any(part => part.StartsWith('.') && part is not ("." or ".."));

    // A watch that lost events, as a full buffer does, leaves the list stale, so it asks for a read.
    private void Failed(object sender, ErrorEventArgs e) => Settling();

    // Each event pushes the answer back, so a burst ends in one.
    private void Settling()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _settle.Change(SettleDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void Settled(object? state)
    {
        Action? changed;
        lock (_gate)
        {
            changed = _changed;
        }

        changed?.Invoke();
    }
}
