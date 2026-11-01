namespace Orblit.Editor.Core;

using Orblit.Terrain;
using Ground = Orblit.Terrain.Terrain;

/// <summary>
/// A terrain read from the project, as it is now.
/// </summary>
/// <param name="file">Project-relative, as a terrain component names it.</param>
/// <param name="path">Where the settings file is on this machine. Its regions are beside it.</param>
/// <param name="terrain">
/// Edited in place: a brush writes straight into its regions, and the commands that change it keep
/// what they need to change it back.
/// </param>
public sealed class OpenTerrain(string file, string path, Ground terrain)
{
    public string File { get; } = file;

    public string Path { get; } = path;

    public Ground Terrain { get; } = terrain;

    /// <summary>The folder the settings file and its regions are in.</summary>
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;

    /// <summary>
    /// What the undo stack files this terrain's changes under.
    /// <para>
    /// A terrain is not a scene, but it is a document that changes and is saved, like a clip.
    /// Prefixed so no scene or clip can ever be given the same one.
    /// </para>
    /// </summary>
    public string Id => IdFor(File);

    public string Name => NameOf(File);

    /// <summary>What the renderer knows this terrain by from one frame to the next.</summary>
    public int Key => File.GetHashCode(StringComparison.Ordinal) & 0x3fffffff;

    /// <summary>The undo stack's stamp for this terrain when it was last written.</summary>
    internal int SavedStamp { get; set; }

    /// <summary>
    /// Each region's revision when it was written, which is also every region file there is. A region
    /// whose revision has moved on since is written again, and one no longer in the terrain has its
    /// file taken away.
    /// </summary>
    internal Dictionary<RegionKey, long> SavedRevisions { get; } = [];

    internal static string IdFor(string file) => $"terrain:{file}";

    internal static string NameOf(string file) => System.IO.Path.GetFileNameWithoutExtension(file);
}
