using SkiaSharp;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// Reads a picture with SkiaSharp, which Avalonia already draws with, so no second image library ships.
/// </summary>
/// <remarks>
/// Alpha stays straight, because the renderer reads it as the albedo's height and the normal map's
/// roughness, and a premultiplied decode would darken the ground where either is low.
/// </remarks>
internal sealed class SkiaPictureDecoder : IPictureDecoder
{
    // A side of 8192 is 256 MiB of RGBA, which is more than any ground picture is worth reading whole.
    private const long MaxPixels = 8192L * 8192;

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    // Each decode holds the whole picture, so a project full of large ones would fill memory if they all
    // ran at once. The rest wait their turn.
    private static readonly SemaphoreSlim Slots = new(initialCount: 4);

    public byte[] Decode(byte[] file, int side)
    {
        Slots.Wait();
        try
        {
            return Read(file, side);
        }
        finally
        {
            Slots.Release();
        }
    }

    private static byte[] Read(byte[] file, int side)
    {
        using var data = SKData.CreateCopy(file);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("It is not a picture Skia can read.");
        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            throw new InvalidDataException($"It is {codec.Info.Width} by {codec.Info.Height} pixels, which is too large to read.");
        }

        var size = DecodedSize(codec, side);
        using var decoded = Allocate(size.Width, size.Height);
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
        {
            throw new InvalidDataException("It is cut short or damaged.");
        }

        if (size.Width == side && size.Height == side)
        {
            return decoded.GetPixelSpan().ToArray();
        }

        using var fitted = Allocate(side, side);
        return decoded.ScalePixels(fitted, Sampling)
            ? fitted.GetPixelSpan().ToArray()
            : throw new InvalidDataException("It could not be resized.");
    }

    private static SKBitmap Allocate(int width, int height)
    {
        var bitmap = new SKBitmap();
        if (bitmap.TryAllocPixels(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
        {
            return bitmap;
        }

        bitmap.Dispose();
        throw new InvalidDataException("There is not the memory to read it.");
    }

    // A JPEG can be decoded at a half, quarter or eighth of its size for much less work than decoding it
    // whole and resizing. It is taken only where the result is still no smaller than the side, so the
    // resize after it never grows a picture. Other formats only offer their own size.
    private static SKSizeI DecodedSize(SKCodec codec, int side)
    {
        var wanted = (float)side / Math.Min(codec.Info.Width, codec.Info.Height);
        if (wanted >= 1)
        {
            return codec.Info.Size;
        }

        var scaled = codec.GetScaledDimensions(wanted);
        return scaled.Width >= side && scaled.Height >= side ? scaled : codec.Info.Size;
    }
}
