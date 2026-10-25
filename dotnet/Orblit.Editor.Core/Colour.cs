namespace Orblit.Editor.Core;

using System.Globalization;
using Orblit.Light;
using Orblit.VectorMath;

/// <summary>
/// A colour as somebody picks it, eight bits a channel with alpha first.
/// </summary>
/// <remarks>
/// Everything below the editor states colour as a <see cref="Tint"/>, which knows nothing
/// about a window. The editor holds both, so the conversion lives here and not in every
/// panel that shows a swatch.
/// </remarks>
public readonly record struct Colour(uint Argb)
{
    private const uint Coral = 0xFFD9634F;

    public double A => Channel(24);

    public double R => Channel(16);

    public double G => Channel(8);

    public double B => Channel(0);

    public static Colour FromArgb(int alpha, int red, int green, int blue) =>
        new(((uint)(alpha & 0xFF) << 24) | ((uint)(red & 0xFF) << 16) | ((uint)(green & 0xFF) << 8) | (uint)(blue & 0xFF));

    public static Colour FromTint(Tint tint) =>
        FromArgb(255, Byte(tint.Red), Byte(tint.Green), Byte(tint.Blue));

    public Tint ToTint() => new(R, G, B);

    /// <summary>
    /// A colour from <c>#RRGGBB</c> or <c>#RRGGBBAA</c>, for the places a value arrives as text.
    /// Data objects and prefabs write hex because a person reading the file should see
    /// something they recognise.
    /// </summary>
    /// <remarks>
    /// Dart reads a sign in front of the digits. A sign here is not hex, so it falls back.
    /// </remarks>
    public static Colour FromHex(string hex, Colour? fallback = null)
    {
        var digits = hex.StartsWith('#') ? hex[1..] : hex;
        if (digits.Length is not (6 or 8)
            || !uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return fallback ?? new Colour(Coral);
        }

        return new Colour(digits.Length == 6 ? 0xFF000000 | value : value);
    }

    public string ToHex() => $"#{(Argb >> 16) & 0xFF:X2}{(Argb >> 8) & 0xFF:X2}{Argb & 0xFF:X2}";

    /// <summary>
    /// sRGB to linear, because the shading maths is linear and a colour handed over unconverted
    /// is washed out in a way that reads as a lighting bug.
    /// </summary>
    public Vector3 ToLinear() => new(Linear(R), Linear(G), Linear(B));

    private static double Linear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static int Byte(double channel) => (int)Math.Clamp(Doubles.Round(channel * 255), 0, 255);

    private double Channel(int shift) => ((Argb >> shift) & 0xFF) / 255.0;
}
