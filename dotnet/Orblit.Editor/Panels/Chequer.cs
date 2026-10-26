using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

/// <summary>The squares behind a transparent picture, so transparency reads as transparency and not as whatever colour the panel is.</summary>
internal sealed class Chequer : Control
{
    private const double Square = 8;

    private static readonly IBrush Dark = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF171C24));
    private static readonly IBrush Light = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF20262F));

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Dark, null, new Rect(Bounds.Size));
        for (var y = 0; y * Square < Bounds.Height; y++)
        {
            for (var x = y % 2; x * Square < Bounds.Width; x += 2)
            {
                context.DrawRectangle(Light, null, new Rect(x * Square, y * Square, Square, Square));
            }
        }
    }
}
