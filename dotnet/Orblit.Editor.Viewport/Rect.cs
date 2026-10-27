namespace Orblit.Editor.Viewport;

/// <summary>A box on the view, in its own pixels.</summary>
public readonly record struct Rect(double Left, double Top, double Right, double Bottom)
{
    /// <summary>The top and left edges are inside and the bottom and right are not, as in Flutter's <c>Rect</c>.</summary>
    public bool Contains(Offset point) => point.Dx >= Left && point.Dx < Right && point.Dy >= Top && point.Dy < Bottom;
}
