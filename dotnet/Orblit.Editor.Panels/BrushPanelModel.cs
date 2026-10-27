namespace Orblit.Editor.Panels;

using System.Globalization;
using Orblit.Editor.Core;
using Orblit.Terrain;
using Ground = Orblit.Terrain.Terrain;

/// <summary>What the brush panel offers and says, apart from drawing it.</summary>
public static class BrushPanelModel
{
    public const string NothingToShape = "Nothing to shape. Select a terrain, or add one with Add › Terrain.";

    public const string NoSets = "This terrain has no sets to lay. Add one in the inspector.";

    /// <summary>
    /// Tints that read as ground. The colour multiplies what the textures paint, so white is no tint and
    /// anything darker darkens.
    /// </summary>
    public static readonly IReadOnlyList<Colour> Tints =
    [
        new(0xFF967C60), new(0xFFC9B48A), new(0xFF6E5A40), new(0xFF8FA05A), new(0xFFB07050), new(0xFF8A8A8A),
    ];

    /// <summary>A length in metres, with a decimal only where there is one to show.</summary>
    public static string Metres(double value) => value == Math.Round(value)
        ? Math.Round(value).ToString("F0", CultureInfo.InvariantCulture)
        : value.ToString("F1", CultureInfo.InvariantCulture);

    public static string Extent(Ground terrain)
    {
        var count = terrain.Regions.Count();
        var metres = Metres(terrain.RegionSize * terrain.Spacing);
        return count switch
        {
            0 => "No ground yet. Add a region in the inspector.",
            1 => $"One region, {metres} m across",
            _ => $"{count} regions, each {metres} m across",
        };
    }

    public static string SetDetail(TerrainSet set) =>
        $"{Metres(set.TileSize)} m{(set.Triplanar ? ", on cliffs" : "")}";

    public static Colour TintOf(GroundColour colour) => Colour.FromArgb(255, colour.Red, colour.Green, colour.Blue);

    /// <summary>What the colour tool paints once a tint is picked. The roughness is not painted with it.</summary>
    public static GroundColour Painted(GroundColour was, Colour picked) => was.WithColour(
        (int)Math.Round(picked.R * 255),
        (int)Math.Round(picked.G * 255),
        (int)Math.Round(picked.B * 255));
}
