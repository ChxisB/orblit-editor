namespace Orblit.Editor.Viewport;

/// <summary>A point or a movement on the view, in its own pixels.</summary>
public readonly record struct Offset(double Dx, double Dy)
{
    public static Offset Zero => new(0, 0);

    /// <summary>How far it is from the origin.</summary>
    public double Distance => Math.Sqrt((Dx * Dx) + (Dy * Dy));

    public static Offset operator +(Offset a, Offset b) => new(a.Dx + b.Dx, a.Dy + b.Dy);

    public static Offset operator -(Offset a, Offset b) => new(a.Dx - b.Dx, a.Dy - b.Dy);

    public static Offset operator *(Offset a, double scale) => new(a.Dx * scale, a.Dy * scale);

    public static Offset operator /(Offset a, double scale) => new(a.Dx / scale, a.Dy / scale);
}

/// <summary>How many pixels across and down a view is.</summary>
public readonly record struct Size(double Width, double Height);
