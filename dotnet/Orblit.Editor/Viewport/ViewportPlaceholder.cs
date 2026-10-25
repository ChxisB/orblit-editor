using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

/// <summary>
/// What the view shows where the renderer would be: a plain grid and a label. A window that has no
/// renderer, such as a test, still has a surface to size and a place for the overlay to draw.
/// </summary>
internal sealed class ViewportPlaceholder : Control
{
    private const double Cell = 32;

    private static readonly Pen GridPen = new(new ImmutableSolidColorBrush(Color.FromUInt32(0x592A313C)), 1);
    private static readonly Typeface Face = new(FontFamily.Default);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        for (var x = 0.0; x <= Bounds.Width; x += Cell)
        {
            context.DrawLine(GridPen, new Point(x, 0), new Point(x, Bounds.Height));
        }

        for (var y = 0.0; y <= Bounds.Height; y += Cell)
        {
            context.DrawLine(GridPen, new Point(0, y), new Point(Bounds.Width, y));
        }

        var label = new FormattedText(
            "Viewport",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Face,
            OrblitText.Label,
            OrblitColors.InkDim);
        context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, (Bounds.Height - label.Height) / 2));
    }
}
