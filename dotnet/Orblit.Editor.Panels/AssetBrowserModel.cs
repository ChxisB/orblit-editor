namespace Orblit.Editor.Panels;

using Orblit.Asset;
using Orblit.Editor.Core;
using IoPath = System.IO.Path;

/// <summary>
/// Where the asset browser is and what it lists: the open folder, the file picked, and the filter.
/// </summary>
/// <remarks>
/// The folder is read every time <see cref="Entries"/> is asked, so the list is never older than the
/// last redraw. A folder is small and the watch on <see cref="AssetTree"/> says when to ask again.
/// </remarks>
public sealed class AssetBrowserModel(AssetTree tree)
{
    // What the picture decoder handles. A .ktx2 is a compressed texture meant for a GPU and a .hdr
    // carries more range than a screen has, so those keep the glyph that names the kind.
    private static readonly HashSet<string> Decodable = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];

    public AssetTree Tree { get; } = tree;

    /// <summary>The folder being looked at. It starts at the project folder.</summary>
    public string Directory { get; private set; } = tree.Root;

    /// <summary>The path of the file picked, or null.</summary>
    public string? Selected { get; private set; }

    /// <summary>What was typed in the filter, as typed. It survives opening a folder.</summary>
    public string Filter { get; set; } = "";

    public bool CanGoUp => Tree.Relative(Directory).Length > 0;

    /// <summary>Where the open folder is, from the project folder down. Empty at the top.</summary>
    public string Crumb => Tree.Relative(Directory);

    /// <summary>Whether the filter narrows the list, which changes what an empty list means.</summary>
    public bool IsFiltering => Query.Length > 0;

    private string Query => Filter.Trim().ToLowerInvariant();

    /// <summary>Whether a file is shown as its own picture and not as the glyph of its kind.</summary>
    public static bool ShowsPicture(Asset asset) =>
        !asset.IsFolder && asset.Kind == AssetKind.Texture && Decodable.Contains(IoPath.GetExtension(asset.Path).ToLowerInvariant());

    public static string CountOf(int items) => $"{items} item{(items == 1 ? "" : "s")}";

    /// <summary>What the tooltip on a tile says: the file, its kind and size, and where the cook has it.</summary>
    public static string TipOf(Asset asset, CookState? state) =>
        $"{asset.Name}\n{asset.Kind.Label}{(asset.Bytes is null ? "" : $" · {asset.Size}")}"
        + (state is { } cook ? $"\n{cook.Label()}. {cook.Explanation()}" : "");

    /// <summary>The open folder's files, narrowed by the filter.</summary>
    public IReadOnlyList<Asset> Entries() =>
        [.. AssetTree.Read(Directory).Where(asset => asset.Name.ToLowerInvariant().Contains(Query, StringComparison.Ordinal))];

    public void Open(string directory)
    {
        Directory = directory;
        Selected = null;
    }

    public void Up()
    {
        if (CanGoUp)
        {
            Open(IoPath.GetDirectoryName(IoPath.TrimEndingDirectorySeparator(Directory)) ?? Tree.Root);
        }
    }

    public void Select(string? path) => Selected = path;
}
