using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// The strips of a device's screen a game keeps clear of, shaded over the picture. Hatched as well as
/// tinted, so they do not rely on colour alone.
/// </summary>
internal sealed class UnsafeEdgesOverlay : Control
{
    private const double Gap = 8;

    private static readonly IBrush Wash = new ImmutableSolidColorBrush(Color.FromUInt32(0x33E5893F));
    private static readonly Pen Hatch = new(new ImmutableSolidColorBrush(Color.FromUInt32(0x66E5893F)), 1);

    private DeviceFrame _frame = DeviceFrame.Desktop;

    public UnsafeEdgesOverlay()
    {
        Name = "game_view_unsafe_edges";
        // A press on the shading is a press on the picture under it.
        IsHitTestVisible = false;
    }

    public void Show(DeviceFrame frame)
    {
        _frame = frame;
        InvalidateVisual();
    }

    /// <summary>
    /// The shaded strips for <paramref name="frame"/> drawn into <paramref name="size"/>, which has the
    /// frame's shape. The top and bottom run the whole width, so the sides stop at them and no corner
    /// is shaded twice.
    /// </summary>
    internal static IReadOnlyList<Rect> Bands(DeviceFrame frame, Size size)
    {
        var scale = size.Width / frame.Width;
        var (left, top, right, bottom) = (frame.Edges.Left * scale, frame.Edges.Top * scale, frame.Edges.Right * scale, frame.Edges.Bottom * scale);
        var between = size.Height - top - bottom;
        List<Rect> bands = [];
        if (top > 0)
        {
            bands.Add(new Rect(0, 0, size.Width, top));
        }

        if (bottom > 0)
        {
            bands.Add(new Rect(0, size.Height - bottom, size.Width, bottom));
        }

        if (left > 0)
        {
            bands.Add(new Rect(0, top, left, between));
        }

        if (right > 0)
        {
            bands.Add(new Rect(size.Width - right, top, right, between));
        }

        return bands;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        foreach (var band in Bands(_frame, Bounds.Size))
        {
            context.DrawRectangle(Wash, null, band);
            using (context.PushClip(band))
            {
                for (var x = band.Left - band.Height; x < band.Right; x += Gap)
                {
                    context.DrawLine(Hatch, new Point(x, band.Bottom), new Point(x + band.Height, band.Top));
                }
            }
        }
    }
}
