namespace Orblit.Editor.Tests;

using SkiaSharp;

// The editor decodes with Skia directly. The renderer reads alpha as height and roughness, so what these hold
// is that it is never premultiplied.
public sealed class SkiaPictureDecoderTests
{
    private const int Side = 8;

    private static byte[] Encode(int width, int height, Func<int, int, byte[]> pixel, SKEncodedImageFormat format)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixel(x, y).CopyTo(rgba, ((y * width) + x) * 4);
            }
        }

        using var image = SKImage.FromPixelCopy(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul), rgba);
        return image.Encode(format, 100).ToArray();
    }

    internal static byte[] Solid(int width, int height, byte[] rgba, SKEncodedImageFormat format = SKEncodedImageFormat.Png) =>
        Encode(width, height, (_, _) => rgba, format);

    // Every pixel is within a few steps of the colour, which is what a resize or a JPEG leaves.
    private static void AssertEverywhere(byte[] decoded, byte[] rgba, int tolerance)
    {
        Assert.Equal(Side * Side * 4, decoded.Length);
        for (var i = 0; i < decoded.Length; i++)
        {
            Assert.InRange(decoded[i], rgba[i % 4] - tolerance, rgba[i % 4] + tolerance);
        }
    }

    [Fact]
    public void APictureAlreadyTheRightSizeComesBackByteForByteWithItsAlphaStraight()
    {
        static byte[] Pixel(int x, int y) => [(byte)(x * 30), (byte)(y * 30), 7, (byte)(20 + x)];

        var decoded = new SkiaPictureDecoder().Decode(Encode(Side, Side, Pixel, SKEncodedImageFormat.Png), Side);

        Assert.Equal(Side * Side * 4, decoded.Length);
        for (var i = 0; i < decoded.Length; i += 4)
        {
            Assert.Equal(Pixel(i / 4 % Side, i / 4 / Side), decoded[i..(i + 4)]);
        }
    }

    [Theory]
    [InlineData(4, 4, 64)]
    [InlineData(4, 2, 64)]
    [InlineData(64, 64, 64)]
    [InlineData(64, 64, 1)]
    [InlineData(64, 64, 0)]
    public void APictureOfAnotherSizeIsStretchedToTheSideAndLowAlphaDoesNotDarkenIt(int width, int height, byte alpha)
    {
        byte[] rgba = [200, 100, 50, alpha];

        var decoded = new SkiaPictureDecoder().Decode(Solid(width, height, rgba), Side);

        AssertEverywhere(decoded, rgba, tolerance: 2);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(100)]
    public void AJpegIsDecodedToTheSideAndIsOpaque(int size)
    {
        byte[] rgba = [100, 150, 200, 255];

        var decoded = new SkiaPictureDecoder().Decode(Solid(size, size, rgba, SKEncodedImageFormat.Jpeg), Side);

        AssertEverywhere(decoded, rgba, tolerance: 8);
    }

    [Fact]
    public void BytesThatAreNoPictureAreRefused()
    {
        Assert.Throws<InvalidDataException>(() => new SkiaPictureDecoder().Decode([1, 2, 3, 4], Side));
    }
}
