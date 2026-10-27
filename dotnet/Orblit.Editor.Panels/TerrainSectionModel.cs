namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;
using Orblit.Terrain;
using Ground = Orblit.Terrain.Terrain;

/// <summary>The squares the region map draws, as inclusive bounds in region keys.</summary>
public readonly record struct RegionSpan(long X0, long X1, long Z0, long Z1)
{
    public int Columns => (int)(X1 - X0 + 1);

    public int Rows => (int)(Z1 - Z0 + 1);
}

/// <summary>
/// What the inspector's terrain section decides apart from drawing it: the settings each button
/// makes of the ones a terrain has, and the words it says.
/// </summary>
public static class TerrainSectionModel
{
    public static string RegionsCaption(Ground terrain) =>
        $"Each square is {BrushPanelModel.Metres(terrain.RegionSize * terrain.Spacing)} m of ground. "
        + "Click an empty one to add ground there, a full one to take it away.";

    /// <summary>
    /// Where the ground is, with a margin of one empty square round it to add to. With no ground yet
    /// it is the squares round the origin, so the first one added is where the camera already is.
    /// </summary>
    public static RegionSpan SpanOf(IEnumerable<RegionKey> keys)
    {
        var all = keys.ToList();
        if (all.Count == 0)
        {
            return new RegionSpan(-1, 0, -1, 0);
        }

        return new RegionSpan(
            all.Min(key => key.X) - 1,
            all.Max(key => key.X) + 1,
            all.Min(key => key.Z) - 1,
            all.Max(key => key.Z) + 1);
    }

    public static string SetHeading(int index, TerrainSet set) => $"{index + 1}. {set.Name}";

    public static TerrainSettings AddSet(TerrainSettings settings) =>
        settings with { Sets = [.. settings.Sets, new TerrainSet($"Set {settings.Sets.Count + 1}")] };

    /// <summary>
    /// Takes the last set away, and puts any cover that named it on the one before. Only the last: the
    /// ground names a set by its place in the list, so taking one from the middle would repaint
    /// everything after it.
    /// </summary>
    public static TerrainSettings RemoveLast(TerrainSettings settings)
    {
        var last = settings.Sets.Count - 2;
        var cover = settings.AutoCover;
        return settings with
        {
            Sets = [.. settings.Sets.Take(last + 1)],
            // Bare ground must not be left choosing a set that is not there.
            AutoCover = new AutoCover(
                Math.Clamp(cover.Steep, 0, last),
                Math.Clamp(cover.Flat, 0, last),
                cover.Slope,
                cover.HeightFalloff),
        };
    }

    public static TerrainSettings WithSet(TerrainSettings settings, int index, TerrainSet set) =>
        settings with { Sets = [.. settings.Sets.Select((one, at) => at == index ? set : one)] };

    public static TerrainSet WithAlbedo(TerrainSet set, string? path) =>
        new(set.Name, path, set.Normal, set.TileSize, set.Triplanar);

    public static TerrainSet WithNormal(TerrainSet set, string? path) =>
        new(set.Name, set.Albedo, path, set.TileSize, set.Triplanar);

    public static TerrainSet WithTileSize(TerrainSet set, double metres) =>
        new(set.Name, set.Albedo, set.Normal, metres, set.Triplanar);

    public static TerrainSet WithTriplanar(TerrainSet set, bool wrapped) =>
        new(set.Name, set.Albedo, set.Normal, set.TileSize, wrapped);

    public static AutoCover WithSteep(AutoCover cover, long set) => new(set, cover.Flat, cover.Slope, cover.HeightFalloff);

    public static AutoCover WithFlat(AutoCover cover, long set) => new(cover.Steep, set, cover.Slope, cover.HeightFalloff);

    public static AutoCover WithSlope(AutoCover cover, double slope) => new(cover.Steep, cover.Flat, slope, cover.HeightFalloff);

    public static AutoCover WithHeightFalloff(AutoCover cover, double falloff) => new(cover.Steep, cover.Flat, cover.Slope, falloff);

    /// <summary>The set a cover names, or what to say when it names one the terrain no longer has.</summary>
    public static string CoverShown(IReadOnlyList<TerrainSet> sets, long index) =>
        index >= 0 && index < sets.Count ? sets[(int)index].Name : $"Set {index + 1} (missing)";
}
