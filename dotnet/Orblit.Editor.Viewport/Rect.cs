namespace Orblit.Editor.Viewport;

/// <summary>A box on the view, in its own pixels.</summary>
public readonly record struct Rect(double Left, double Top, double Right, double Bottom)
{
    /// <summary>The box with two opposite corners, whichever way round they are.</summary>
    public static Rect Between(Offset a, Offset b) =>
        new(Math.Min(a.Dx, b.Dx), Math.Min(a.Dy, b.Dy), Math.Max(a.Dx, b.Dx), Math.Max(a.Dy, b.Dy));

    /// <summary>The top and left edges are inside and the bottom and right are not.</summary>
    public bool Contains(Offset point) => point.Dx >= Left && point.Dx < Right && point.Dy >= Top && point.Dy < Bottom;
}
