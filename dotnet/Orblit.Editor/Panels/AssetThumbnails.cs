using Avalonia.Media.Imaging;
using IoFile = System.IO.File;

namespace Orblit.Editor;

/// <summary>
/// The pictures the browser shows for texture files, decoded once and kept until the file changes.
/// </summary>
/// <remarks>
/// A tile is rebuilt whenever the folder is read again, and a folder of textures should not flicker
/// or decode four hundred files each time the watch fires.
/// </remarks>
internal static class AssetThumbnails
{
    // Decoded at the size it is drawn at, twice over for a sharp screen, rather than at the size it was
    // authored at. A folder of 4K maps is a gigabyte of pixels nobody is looking at closely.
    private const int DecodeWidth = 224;

    private const int Capacity = 512;

    private static readonly Dictionary<string, Bitmap?> Kept = [];
    private static readonly Lock Gate = new();

    /// <summary>
    /// The picture in the file at <paramref name="path"/>, or null when it cannot be read as one.
    /// </summary>
    /// <remarks>
    /// shortcut: the cache is emptied when it is full and not trimmed by age, so a project of thousands
    /// of textures redecodes the ones in view after it fills. Make it an LRU if that shows.
    /// </remarks>
    public static Task<Bitmap?> LoadAsync(string path) => Task.Run(() => Load(path));

    private static Bitmap? Load(string path)
    {
        string key;
        try
        {
            var info = new FileInfo(path);
            key = $"{info.FullName}:{info.LastWriteTimeUtc.Ticks}:{info.Length}";
        }
        catch (IOException)
        {
            return null;
        }

        lock (Gate)
        {
            if (Kept.TryGetValue(key, out var kept))
            {
                return kept;
            }
        }

        var decoded = Decode(path);
        lock (Gate)
        {
            if (Kept.Count >= Capacity)
            {
                Kept.Clear();
            }

            Kept[key] = decoded;
        }

        return decoded;
    }

    // A file that is half written, or is not the picture its name says, shows its glyph instead.
    private static Bitmap? Decode(string path)
    {
        try
        {
            using var stream = IoFile.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, DecodeWidth);
        }
#pragma warning disable CA1031 // The decoder throws a NullReferenceException on bytes it cannot read, and a tile's picture is loaded from an async void that would take the editor down with it.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
