using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

/// <summary>
/// What the view shows where the renderer would be: a plain grid and a label. A platform the renderer
/// has not reached, or a window built without it such as a test's, still has a surface to size and a
/// place for the overlay to draw.
/// </summary>
internal sealed class ViewportPlaceholder : Control
{
    /// <summary>
    /// What it says under its label. Not phrased as a failure, because nothing is broken: the renderer
    /// has not reached this platform yet, and a placeholder that reads like an error gets filed as a bug.
    /// </summary>
    public const string Unavailable = "The 3D viewport is not available on this platform yet.";

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

        var label = Text("Viewport", OrblitText.Label);
        var saying = Text(Unavailable, OrblitText.Caption);
        var top = (Bounds.Height - label.Height - Space.Xs - saying.Height) / 2;
        context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, top));
        context.DrawText(saying, new Point((Bounds.Width - saying.Width) / 2, top + label.Height + Space.Xs));
    }

    private static FormattedText Text(string text, double size) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, OrblitColors.InkDim);
}
