using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Orblit.Editor;

/// <summary>The pictures a store sends, decoded once for each read the session hands out.</summary>
/// <remarks>
/// The session keeps one task for each picture, so keying on it decodes a picture once however often the grid
/// is rebuilt, and lets it go when the session forgets the read.
/// </remarks>
internal static class StorePictures
{
    private static readonly ConditionalWeakTable<Task<byte[]?>, Task<Bitmap?>> Decoded = [];

    /// <summary>Puts the picture <paramref name="image"/> brings into <paramref name="frame"/>, if it is one.</summary>
    public static async void Show(Border frame, Task<byte[]?> image, int width)
    {
        if (await Decoded.GetValue(image, read => Task.Run(() => DecodeAsync(read, width))) is { } bitmap)
        {
            frame.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
        }
    }

    // A file the store sent that is not a picture leaves the frame as it was.
    private static async Task<Bitmap?> DecodeAsync(Task<byte[]?> read, int width)
    {
        if (await read is not { } bytes)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            return Bitmap.DecodeToWidth(stream, width);
        }
#pragma warning disable CA1031 // The decoder throws a NullReferenceException on bytes it cannot read, and the picture is shown from an async void that would take the editor down with it.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
