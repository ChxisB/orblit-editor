using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Ui;

/// <summary>
/// What an interface is laid out against, drawn over it: the canvas edge, the safe area inside it and
/// the column grid.
/// </summary>
internal sealed class InterfaceGuides : Control
{
    private static readonly IBrush Band = new ImmutableSolidColorBrush(OrblitColors.EmberColor, 0.055);
    private static readonly IPen BandEdge = new Pen(new ImmutableSolidColorBrush(OrblitColors.EmberColor, 0.3), 1);
    private static readonly IPen Edge = new Pen(new ImmutableSolidColorBrush(OrblitColors.EmberColor, 0.65), 2);
    private static readonly IPen Safe = new Pen(new ImmutableSolidColorBrush(OrblitColors.GoodColor, 0.5), 1);

    private UiCanvas _canvas = new();

    public InterfaceGuides()
    {
        IsHitTestVisible = false;
    }

    /// <summary>Whether the edge and the safe area are drawn.</summary>
    public bool Edges { get; private set; }

    /// <summary>Whether the column grid is drawn.</summary>
    public bool Columns { get; private set; }

    /// <summary>Draws the guides of <paramref name="canvas"/> that are turned on.</summary>
    public void Show(UiCanvas canvas, bool edges, bool columns)
    {
        (_canvas, Edges, Columns) = (canvas, edges, columns);
        InvalidateVisual();
    }

    // The grid goes under the edges, because the grid is what things are laid out against and the edges
    // are what they are laid out inside.
    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (Columns)
        {
            foreach (var (left, right) in _canvas.ColumnsAcross(size.Width))
            {
                context.FillRectangle(Band, new Rect(left, 0, right - left, size.Height));
                context.DrawLine(BandEdge, new Point(left, 0), new Point(left, size.Height));
                context.DrawLine(BandEdge, new Point(right, 0), new Point(right, size.Height));
            }
        }

        if (Edges)
        {
            context.DrawRectangle(null, Edge, new Rect(size));
            SafeArea(context, size);
        }
    }

    // A guide and not a clip. An interface that bleeds to the edge on purpose can, and this says where a
    // television's overscan and a phone's notch will eat what is under them.
    private void SafeArea(DrawingContext context, Size size)
    {
        var inset = _canvas.SafeArea;
        if (inset > 0)
        {
            context.DrawRectangle(null, Safe, new Rect(size.Width * inset, size.Height * inset, size.Width * (1 - (2 * inset)), size.Height * (1 - (2 * inset))));
        }
    }
}
