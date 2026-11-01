namespace Orblit.Editor.Core;

using Orblit.Asset;
using IoPath = System.IO.Path;

/// <summary>One asset a pack serves, and the file it comes from.</summary>
/// <param name="Hash">The first twelve characters of the asset's content hash.</param>
/// <param name="Pack">
/// The file name of the pack that answers for <paramref name="Id"/>. In a patch's chain it is the patch that
/// replaced the asset, or the whole pack beneath for one no patch touched.
/// </param>
public sealed record ContentsRow(string Id, long Bytes, string Hash, string Pack);

/// <summary>What an export serves once its patches are applied.</summary>
/// <param name="Packs">The file names in the chain, the whole pack first.</param>
/// <param name="Rows">Every asset served, the largest first.</param>
/// <param name="Removed">The ids the top pack takes away from the one under it.</param>
public sealed record PackContents(IReadOnlyList<string> Packs, IReadOnlyList<ContentsRow> Rows, IReadOnlyList<string> Removed)
{
    public long Bytes => Rows.Sum(row => row.Bytes);

    /// <summary>Whether more than one pack is mounted, which is when an asset's owner says something.</summary>
    public bool Layered => Packs.Count > 1;

    /// <summary>What the chain ending at <paramref name="path"/> serves, read from each pack's directory.</summary>
    /// <remarks>No blob is read, so the answer for a pack of several gigabytes comes back as fast as one of a few kilobytes.</remarks>
    public static async Task<PackContents> ReadAsync(string projectDirectory, string path)
    {
        await using var folder = await OpenedExports.OpenAsync(projectDirectory).ConfigureAwait(false);
        var top = folder.At(path) ?? throw new ExportChainBrokenException(
            $"{IoPath.GetFileName(path)} is not a readable pack in the exports folder.");
        return Of([.. folder.ChainTo(top).Select(pack => (IoPath.GetFileName(folder.PathOf(pack)), pack))]);
    }

    /// <summary>What <paramref name="chain"/> serves, mounted as an app would: the whole pack first, then each patch on the one before.</summary>
    public static PackContents Of(IReadOnlyList<(string Name, AssetPack Pack)> chain)
    {
        var mounted = new MountedPacks(TargetOf(chain[0].Pack));
        mounted.Start(chain[0].Pack);
        foreach (var (_, pack) in chain.Skip(1))
        {
            mounted.Patch(pack);
        }

        var names = chain.ToDictionary(link => link.Pack, link => link.Name);
        var rows = mounted.Manifest.Entries
            .Select(entry => new ContentsRow(
                entry.Key.ToString(),
                entry.Value.Bytes,
                ExportFolder.ShortHash(entry.Value.Hash),
                // Every id in the manifest has an owner, since the manifest is built from the same map.
                names[mounted.OwnerOf(entry.Key)!]))
            .OrderByDescending(row => row.Bytes)
            .ThenBy(row => row.Id, StringComparer.Ordinal);
        return new PackContents(
            [.. chain.Select(link => link.Name)],
            [.. rows],
            [.. chain[^1].Pack.Removed.Select(id => id.ToString())]);
    }

    private static CookTarget TargetOf(AssetPack pack) =>
        CookTargets.Find(pack.Target) ?? throw new ExportChainBrokenException(
            $"The pack is for {pack.Target}, which Orblit does not cook for.");
}
