namespace Orblit.Editor.Core;

using System.Text;
using System.Text.RegularExpressions;
using Orblit.Asset;

public enum PublishStage
{
    Editing,
    Working,
    Done,
}

/// <summary>
/// One run of Publish kit: pack a folder of the project as a kit file and work out the entry that lists it.
/// </summary>
/// <remarks>
/// The kit file goes in <c>exports/kits/</c> of the project, named after the end of the address it will be
/// hosted at. Nothing is uploaded and no entry is written. The person copies the entry into a clone of the
/// store's entries. The clock is read once per run, for the day the release is dated.
/// </remarks>
// shortcut: a run cannot be cancelled, because the engine's publish takes no token. Add one when packing
// large kits makes the wait matter.
public sealed partial class PublishSession(string projectDirectory, TimeProvider? time = null) : IDisposable
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private bool _disposed;

    /// <summary>Raised when anything the dialog shows changes. Never raised after <see cref="Dispose"/>.</summary>
    public event Action? Changed;

    public string ProjectDirectory { get; } = projectDirectory;

    public PublishStage Stage { get; private set; }

    /// <summary>What the run made, once <see cref="Stage"/> is done.</summary>
    public KitPublished? Published { get; private set; }

    /// <summary>The path of the kit file the run wrote, once <see cref="Stage"/> is done.</summary>
    public string? KitFile { get; private set; }

    /// <summary>What went wrong, or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>The entry file as it would be saved, once <see cref="Stage"/> is done.</summary>
    public string EntryText =>
        Encoding.UTF8.GetString(
            (Published ?? throw new InvalidOperationException("Nothing is published yet.")).Entry.Encode());

    /// <summary>
    /// Packs <paramref name="folder"/>, a path inside the project, hosted at <paramref name="url"/>. A failure
    /// leaves the person at the form, with <see cref="Problem"/> saying why.
    /// </summary>
    public async Task StartAsync(string folder, string url, string notes = "")
    {
        Problem = null;
        Stage = PublishStage.Working;
        Tell();
        try
        {
            var address = url.Trim();
            var file = FileFor(address);
            var request = new KitPublishRequest
            {
                Folder = FolderIn(folder.Trim()),
                Url = address,
                OutPath = file,
                Released = _time.GetUtcNow().UtcDateTime,
                Notes = notes.Trim(),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(file)!); // a path joined under the project has a parent
            // Packing hashes every file of the kit, so it runs off the UI thread.
            Published = await Task.Run(() => KitPacking.PublishAsync(request));
            KitFile = file;
            Stage = PublishStage.Done;
        }
        catch (Exception error)
        {
            // Any failure must give the dialog back: it cannot be closed while the stage is working.
            Problem = error.Message;
            Stage = PublishStage.Editing;
        }

        Tell();
    }

    public void Dispose() => _disposed = true;

    /// <summary>A file name a url can end in and a shell can take as it stands.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*\z")]
    private static partial Regex PlainName();

    private string FileFor(string url)
    {
        var name = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Segments[^1] : "";
        if (!PlainName().IsMatch(name))
        {
            throw new FormatException(
                "The address must end in the name of the kit file, such as https://example.com/crates-1.0.0.opack.");
        }

        return Path.Join(ExportFolder.PathOf(ProjectDirectory), "kits", name);
    }

    private string FolderIn(string folder)
    {
        var root = Path.GetFullPath(ProjectDirectory);
        // An empty folder comes back as ".", the project itself.
        var inside = Path.IsPathRooted(folder)
            ? ".."
            : Path.GetRelativePath(root, Path.GetFullPath(folder, root));
        if (inside == "." || inside == ".." || inside.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new FormatException("Name a folder inside the project, such as assets/kits/crates.");
        }

        var path = Path.Join(root, inside);
        if (!Directory.Exists(path))
        {
            throw new FormatException($"The project has no folder {folder}.");
        }

        return path;
    }

    private void Tell()
    {
        if (!_disposed)
        {
            Changed?.Invoke();
        }
    }
}
