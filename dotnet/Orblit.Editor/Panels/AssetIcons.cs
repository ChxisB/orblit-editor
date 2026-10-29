namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>Path data for the browser's pictures, each on a 24 unit square and drawn as an outline.</summary>
internal static class AssetIcons
{
    public const string Home = "M4 11l8-7 8 7M6 10v9h12v-9";
    public const string Up = "M12 19V5M6 11l6-6 6 6";
    public const string Add = "M12 5v14M5 12h14";
    public const string Eye = "M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM9 12a3 3 0 1 0 6 0a3 3 0 1 0-6 0";
    public const string EyeOff = Eye + "M4 4l16 16";
    public const string Pause = "M8 5v14M16 5v14";
    public const string Play = "M7 4l13 8-13 8z";
    public const string Refresh = "M20 11a8 8 0 1 0-2.3 5.7M20 4v7h-7";

    private const string File = "M6 3h8l4 4v14H6zM14 3v4h4";

    private static readonly Dictionary<AssetKind, string> ByKind = new()
    {
        [AssetKind.Folder] = "M3 6.5h6.5l2 2H21V19H3z",
        [AssetKind.Scene] = "M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0M3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18",
        [AssetKind.Mesh] = "M12 3l8 4.5v9L12 21l-8-4.5v-9zM4 7.5l8 4.5l8-4.5M12 12v9",
        [AssetKind.Texture] = "M4 5h16v14H4zM4 16l5-5 4 4 3-3 4 4M15 9h.01",
        [AssetKind.Material] = "M6 6h.01M12 6h.01M18 6h.01M6 12h.01M12 12h.01M18 12h.01M6 18h.01M12 18h.01M18 18h.01",
        [AssetKind.Surface] = "M5 19L15 9M13 4l1 2 2 1-2 1-1 2-1-2-2-1 2-1zM18 13l.7 1.3 1.3.7-1.3.7-.7 1.3-.7-1.3-1.3-.7 1.3-.7z",
        [AssetKind.Native] = "M7 7h10v10H7zM10 3v4M14 3v4M10 17v4M14 17v4M3 10h4M3 14h4M17 10h4M17 14h4",
        [AssetKind.Prefab] = "M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z",
        [AssetKind.DataObject] = "M4 5h16v14H4zM4 10h16M4 15h16M10 5v14",
        [AssetKind.Clip] = "M4 4h16v16H4zM9 4v16M15 4v16M4 9h5M4 15h5M15 9h5M15 15h5",
        [AssetKind.Blend] = "M9.5 12a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0-5 0M12 4v5.5M12 14.5V20M4 12h5.5M14.5 12H20",
        [AssetKind.BoneMap] = "M4 4h5v4H4zM15 4h5v4h-5zM9.5 16h5v4h-5zM6.5 8v4h11V8M12 12v4",
        [AssetKind.Cutscene] = "M4 7h16v12H4zM4 7l3-3 3 3 3-3 3 3 3-3",
        [AssetKind.Audio] = "M6 10v4M10 6v12M14 9v6M18 11v2",
        [AssetKind.Data] = "M9 4c-2 0-3 1-3 3v2c0 1.5-1 3-2 3 1 0 2 1.5 2 3v2c0 2 1 3 3 3M15 4c2 0 3 1 3 3v2c0 1.5 1 3 2 3-1 0-2 1.5-2 3v2c0 2-1 3-3 3",
        [AssetKind.Other] = File,
    };

    public static string Of(AssetKind kind) => ByKind.GetValueOrDefault(kind, File);
}
