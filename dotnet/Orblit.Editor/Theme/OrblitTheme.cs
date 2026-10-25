using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

/// <summary>The editor's colours. One place, so a panel never picks its own grey.</summary>
public static class OrblitColors
{
    public static readonly Color EmberColor = Color.FromUInt32(0xFFE5893F);

    public static readonly IBrush Ground = Brush(0xFF0E1116);
    public static readonly IBrush Surface = Brush(0xFF161A21);
    public static readonly IBrush Raised = Brush(0xFF1D222B);
    public static readonly IBrush Line = Brush(0xFF2A313C);
    public static readonly IBrush Ink = Brush(0xFFE4E9F0);
    public static readonly IBrush InkMid = Brush(0xFFA6B0BF);
    public static readonly IBrush InkDim = Brush(0xFF8791A1);
    public static readonly IBrush Ember = new ImmutableSolidColorBrush(EmberColor);
    public static readonly IBrush EmberWash = Brush(0x24E5893F);
    public static readonly IBrush Bad = Brush(0xFFD9634F);

    // Immutable, so a brush made while the type loads is safe to hand to a control on any thread.
    private static ImmutableSolidColorBrush Brush(uint argb) => new(Color.FromUInt32(argb));
}

/// <summary>The gaps between things, in logical pixels.</summary>
public static class Space
{
    public const double Xs = 4;
    public const double Sm = 8;
    public const double Md = 12;
    public const double Lg = 16;
}

public static class Radii
{
    public static readonly CornerRadius Panel = new(10);
}

public static class OrblitText
{
    public const double Label = 12;
    public const double Caption = 11.5;
}
