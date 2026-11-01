namespace Orblit.Editor.Core;

using Orblit.Asset;

/// <summary>Puts a kit's files into a project and lists them in its <c>kits.lock</c>.</summary>
public static class KitInstall
{
    /// <summary>
    /// Writes the files of <paramref name="kit"/>, or those in <paramref name="only"/>, under
    /// <paramref name="into"/> in <paramref name="projectDirectory"/>, and lists them in the project's lock.
    /// A file the project holds with other bytes is overwritten only when it is in
    /// <paramref name="replacing"/>. Otherwise a <see cref="KitConflictsException"/> is thrown before anything
    /// is written. A second install of the same kit into the same folder adds to the files it lists.
    /// </summary>
    /// <remarks>
    /// The lock is read and the new one is made before any file is written, so a lock that cannot be read,
    /// or one with no room, leaves the project as it was.
    /// </remarks>
    // F2: five parameters, all but the first two optional and called by name.
    public static async Task<LockedKit> InstallAsync(
        Kit kit,
        string projectDirectory,
        string into = "",
        IReadOnlySet<AssetId>? only = null,
        IReadOnlySet<AssetId>? replacing = null)
    {
        var lockFile = Path.Combine(projectDirectory, KitRules.LockName);
        var kitLock = File.Exists(lockFile) ? KitLock.Parse(await File.ReadAllBytesAsync(lockFile)) : KitLock.Empty;
        var clashes = await kit.ConflictsInAsync(new DirectoryAssetSource(projectDirectory), into, only);
        var unagreed = clashes.Where(id => replacing is null || !replacing.Contains(id)).ToList();
        if (unagreed.Count > 0)
        {
            throw new KitConflictsException(unagreed);
        }

        var locked = LockedFor(kit, into, only, kitLock.Find(kit.Manifest.Publisher, kit.Manifest.Slug));
        var next = kitLock.Adding(locked).Encode();
        await ExtractAsync(kit, projectDirectory, new KitExtractionOptions { Into = into, Only = only }, [.. clashes]);
        // Written beside and renamed, so the lock is whole or as it was.
        var pending = lockFile + ".tmp";
        await File.WriteAllBytesAsync(pending, next);
        File.Move(pending, lockFile, overwrite: true);
        return locked;
    }

    private static LockedKit LockedFor(Kit kit, string into, IReadOnlySet<AssetId>? only, LockedKit? earlier)
    {
        var files = earlier is not null && earlier.Into == into
            ? new Dictionary<AssetId, ContentHash>(earlier.Files)
            : [];
        foreach (var id in kit.Chosen(only))
        {
            files[KitRules.Place(id, into)] = kit.Pack.Manifest.Entries[id].Hash;
        }

        var manifest = kit.Manifest;
        return new LockedKit(
            publisher: manifest.Publisher,
            slug: manifest.Slug,
            version: manifest.Version,
            hash: kit.Pack.Hash,
            files: files,
            into: into);
    }

    // Two calls, so that only the files the person agreed to lose may be overwritten. A file that appeared
    // after the check is refused by the first.
    private static async Task ExtractAsync(
        Kit kit, string projectDirectory, KitExtractionOptions options, HashSet<AssetId> replacing)
    {
        var fresh = kit.Chosen(options.Only).Where(id => !replacing.Contains(id)).ToHashSet();
        if (fresh.Count > 0)
        {
            await KitExtraction.ExtractAsync(kit, projectDirectory, options with { Only = fresh });
        }

        if (replacing.Count > 0)
        {
            await KitExtraction.ExtractAsync(kit, projectDirectory, options with { Only = replacing, Overwrite = true });
        }
    }
}
