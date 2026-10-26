using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>The editor's colours. One place, so a panel never picks its own grey.</summary>
public static class OrblitColors
{
    public static readonly Color EmberColor = Color.FromUInt32(0xFFE5893F);

    public static readonly IBrush Ground = Brush(0xFF0E1116);
    public static readonly IBrush Surface = Brush(0xFF161A21);
    public static readonly IBrush Raised = Brush(0xFF1D222B);
    public static readonly IBrush Hover = Brush(0xFF242B36);
    public static readonly IBrush Line = Brush(0xFF2A313C);
    public static readonly IBrush LineSoft = Brush(0xFF212733);
    public static readonly IBrush Ink = Brush(0xFFE4E9F0);
    public static readonly IBrush InkMid = Brush(0xFFA6B0BF);
    public static readonly IBrush InkDim = Brush(0xFF8791A1);
    public static readonly IBrush Ember = new ImmutableSolidColorBrush(EmberColor);
    public static readonly IBrush EmberDeep = Brush(0xFFC25E22);
    public static readonly IBrush EmberWash = Brush(0x24E5893F);
    public static readonly IBrush Bad = Brush(0xFFD9634F);
    public static readonly IBrush Warn = Brush(0xFFE0B252);
    public static readonly IBrush Good = Brush(0xFF5FB483);
    public static readonly IBrush AxisX = Brush(0xFFF0736A);
    public static readonly IBrush AxisY = Brush(0xFF7FD083);
    public static readonly IBrush AxisZ = Brush(0xFF62A0E8);

    /// <summary>The brush for a place in the theme that a model names without drawing.</summary>
    public static IBrush For(CookMark mark) => mark switch
    {
        CookMark.Good => Good,
        CookMark.Warn => Warn,
        CookMark.Bad => Bad,
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, null),
    };

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
    public const double Xl = 24;
    public const double Xxl = 36;
}

public static class Radii
{
    public static readonly CornerRadius Control = new(6);
    public static readonly CornerRadius Card = new(8);
    public static readonly CornerRadius Panel = new(10);
    public static readonly CornerRadius Window = new(12);
}

public static class OrblitText
{
    public const double Label = 12;
    public const double Caption = 11.5;
    public const double Body = 13;
    public const double Display = 26;

    /// <summary>Paths, identifiers and numbers, where an even width helps reading.</summary>
    public static readonly FontFamily Mono = new("Menlo, SF Mono, Consolas, monospace");
}
